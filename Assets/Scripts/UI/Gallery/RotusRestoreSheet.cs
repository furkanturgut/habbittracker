using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Rotus harcayip bitmis bir tablonun yanan parcalarini onaran alttan acilan
// kutu. PaintingChoiceSheet'in kalibini izliyor ("Sekil B": ScreenNavigator
// kilidi, panel pasif baslar -- Awake degil EnsureWired, kapanista kilit
// animasyonun SONUNDA acilir) -- HabitSheet'in anchoredPosition/MaxHeightFitter
// regresyon gecmisi (bkz. CLAUDE.md) yuzunden kaydirma kodu yine BILEREK ayri
// bir kopya.
//
// Panel_Gallery'nin EN SON cocugu (GalleryPaintingViewer'in de ustunde) --
// teslim dosyasi bu sayfayi "Ayarlar'in altinda, viewer'in ustunde" istiyor.
[DisallowMultipleComponent]
public class RotusRestoreSheet : MonoBehaviour
{
    private const float FlashSeconds = 0.9f;
    private const float FlashFromScale = 0.4f;
    private const float FlashToScale = 2.4f;

    // Tablo kutusunun hedef olcusu (teslim dosyasi: "~200x170 pt'ye sigdir").
    private const float BoxMaxWidth = 200f;
    private const float BoxMaxHeight = 170f;

    // Tablonun altindaki yumusak golgenin tasma/kayma miktari -- GalleryBands'
    // in 2x-olcege kilitli Spread/Drop'unu odunc almak yerine bu sayfaya ozel
    // kucuk sabitler (painting/shadow ikisi de paintingBox'un merkezinde,
    // pivot 0.5/0.5).
    private const float ShadowSpread = 6f;
    private const float ShadowDrop = 4f;

    [SerializeField] private HabbitManager manager;

    [Tooltip("Acikken ressamin dokunmasi kapatiliyor.")]
    [SerializeField] private ScreenNavigator navigator;

    [Header("Kutu")]
    [SerializeField] private CanvasGroup scrimGroup;
    [SerializeField] private Button scrimButton;
    [SerializeField] private RectTransform panel;
    [SerializeField] private float slideSeconds = 0.32f;

    [Header("Ust")]
    [SerializeField] private Image icon;
    [SerializeField] private SpriteFrameLoop iconLoop;
    [SerializeField] private TMP_Text subLabel;

    [Header("Tablo")]
    [SerializeField] private Image shadow;
    [SerializeField] private Image painting;
    [SerializeField] private Image flash;

    [Header("Govde")]
    [SerializeField] private TMP_Text bodyLabel;

    [Header("Alt bar")]
    [SerializeField] private Button primaryButton;
    [SerializeField] private UiGradient primaryGradient;
    [SerializeField] private Shadow primaryShadow;
    [SerializeField] private TMP_Text primaryLabel;
    [SerializeField] private Button secondaryButton;
    [SerializeField] private TMP_Text secondaryLabel;

    [SerializeField] private UiTheme theme;

    // Yanik hucrelerin 1px cercevesi (teslim dosyasi: design_handoff_rotus/
    // Rotus.dc.html, ring: "inset 0 0 0 1px") -- PaintingCoverGrid'in cell-
    // anchor aritmetigini paylasan, bu sayfaya ozel kucuk bir havuz (bkz.
    // PaintingCoverGrid.CellAnchors basindaki "tek edit noktasi" notu).
    //
    // Tek bir Sliced Image + theme.Rounded(N) DEGIL: Rounded sprite'lari
    // koseli degil YUVARLAK kose tasiyor ve kenarlik kalinligi N piksele
    // (en incesi 5) kilitli -- tasarimin ince, koseli 1px halkasindan kat
    // kat kalin dururdu. Bunun yerine OutlineThickness kalinliginda, koseli,
    // dort kenar seridinden olusan bir cerceve (CreateEdge).
    private const float OutlineThickness = 1.5f;

    private readonly List<RectTransform> outlines = new List<RectTransform>(8);

    private string habitId;
    private string paintingId;
    private PaintingDefinition definition;

    private Coroutine slideRoutine;
    private Coroutine flashRoutine;
    private Vector2 openPosition;
    private bool openPositionCached;
    private bool closing;
    private bool wired;

    public void Open(HabitData habit, string paintingId)
    {
        if (habit == null || manager == null || manager.Library == null
            || !manager.Library.TryGet(paintingId, out PaintingDefinition found))
        {
            return;
        }

        EnsureWired();

        habitId = habit.id;
        this.paintingId = paintingId;
        definition = found;

        Refresh();

        if (!openPositionCached)
        {
            openPosition = panel.anchoredPosition;
            openPositionCached = true;
        }

        closing = false;
        gameObject.SetActive(true);

        if (navigator != null)
        {
            navigator.SetModalOpen(true);
        }

        StartSlide();
    }

    // Close()'in aynasi PaintingChoiceSheet.Close ile ayni gerekceyle: kilit,
    // animasyon BITIMINDE aciliyor, SIRASINDA degil.
    public void Close()
    {
        if (closing || !gameObject.activeSelf)
        {
            return;
        }

        closing = true;
        StopSlide();
        slideRoutine = StartCoroutine(CloseSlide());
    }

    private void EnsureWired()
    {
        if (wired)
        {
            return;
        }

        wired = true;

        if (scrimButton != null)
        {
            scrimButton.onClick.AddListener(Close);
        }

        if (secondaryButton != null)
        {
            secondaryButton.onClick.AddListener(Close);
            ButtonPressFeedback.EnsureOn(secondaryButton);
        }

        if (primaryButton != null)
        {
            primaryButton.onClick.AddListener(UseRotus);
            ButtonPressFeedback.EnsureOn(primaryButton);
        }
    }

    // Sayfa acikken manager.PieceRestored'i dinler: OnEnable/OnDisable (Awake/
    // OnDestroy degil) cunku bu sayfa yalnizca kendi SetActive(true) oldugu
    // surece ilgili -- kapaliyken baska bir habit'in parcasi onarilsa bile
    // bu sayfanin bundan haberi olmamali.
    private void OnEnable()
    {
        if (manager != null)
        {
            manager.PieceRestored += HandlePieceRestored;
        }
    }

    private void OnDisable()
    {
        if (manager != null)
        {
            manager.PieceRestored -= HandlePieceRestored;
        }

        slideRoutine = null;
        closing = false;
    }

    private void UseRotus()
    {
        if (manager == null)
        {
            return;
        }

        manager.UseRotus(habitId, paintingId);
    }

    // RotusChanged yerine PieceRestored'a abone oluyoruz: bu sayfanin ilgilendigi
    // TEK sey "benim actigim tablonun bir parcasi onarildi mi" -- RotusChanged
    // yalnizca sayaci tasir, hangi parcanin acildigini bilmez (flash icin sart).
    private void HandlePieceRestored(HabitData habit, string restoredPaintingId, int pieceIndex)
    {
        if (!string.Equals(habit.id, habitId, StringComparison.Ordinal)
            || !string.Equals(restoredPaintingId, paintingId, StringComparison.Ordinal))
        {
            return;
        }

        PlayFlash(pieceIndex);
        Refresh();
    }

    private void Refresh()
    {
        if (manager == null || definition == null || !manager.TryGetHabit(habitId, out HabitData habit))
        {
            return;
        }

        bool hasHoles = HabitGallery.TryGetHoles(habit, paintingId, out List<int> holes);
        int holeCount = hasHoles ? holes.Count : 0;
        int rotusCount = manager.RotusCount;
        bool canUse = rotusCount > 0 && holeCount > 0;

        if (iconLoop != null && theme != null)
        {
            iconLoop.Show(theme.RotusFrames(rotusCount > 0), 0.4f);
        }

        if (subLabel != null)
        {
            subLabel.text = holeCount > 0
                ? string.Format(Loc.Tr("rotus_restore.sub_with_holes"), definition.displayName, holeCount)
                : Loc.Tr("rotus_restore.sub_no_holes");
        }

        BindPainting(habit, holes);
        BuildOutlines(hasHoles ? holes : null);
        ApplyButtons(canUse, holeCount, rotusCount);
    }

    private void BindPainting(HabitData habit, List<int> holes)
    {
        Sprite sprite = BurnedPaintingCache.SpriteFor(definition, habit.id, holes);
        painting.sprite = sprite;
        painting.enabled = sprite != null;

        Vector2 native = definition.sprite != null ? definition.sprite.rect.size : Vector2.zero;
        int scale = ScaleToFit(native.x, native.y);
        Vector2 size = native * scale;
        painting.rectTransform.sizeDelta = size;

        if (shadow != null)
        {
            shadow.enabled = sprite != null;
            shadow.rectTransform.sizeDelta = size + new Vector2(ShadowSpread * 2f, ShadowSpread * 2f);
            shadow.rectTransform.anchoredPosition = new Vector2(0f, -ShadowDrop);
        }
    }

    // GalleryBands.ViewerScale'in AYNI formulu, bu sayfanin kendi kutu
    // olcusune gore -- ViewerScale'e ikinci bir max cifti eklemek yerine
    // (o zaten Seviye 3'un kendi sabitlerine kilitli) burada kucuk bir kopya.
    private static int ScaleToFit(float nativeWidth, float nativeHeight)
    {
        if (nativeWidth <= 0f || nativeHeight <= 0f)
        {
            return 1;
        }

        return Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(BoxMaxWidth / nativeWidth, BoxMaxHeight / nativeHeight)));
    }

    private void BuildOutlines(List<int> holes)
    {
        int count = holes?.Count ?? 0;

        for (int i = 0; i < count; i++)
        {
            RectTransform outline = OutlineAt(i);
            outline.gameObject.SetActive(true);

            PaintingCoverGrid.CellAnchors(holes[i], definition.PieceCount, out Vector2 min, out Vector2 max);
            outline.anchorMin = min;
            outline.anchorMax = max;
            outline.offsetMin = Vector2.one;
            outline.offsetMax = -Vector2.one;
        }

        for (int i = count; i < outlines.Count; i++)
        {
            outlines[i].gameObject.SetActive(false);
        }
    }

    // Hucreler painting'in KENDI rect'inin cocugu: CellAnchors'un 0..1
    // kesirleri boylece dogrudan tablonun gercek alanina eslenir, tablonun
    // olcegi (ScaleToFit) ne olursa olsun.
    private RectTransform OutlineAt(int index)
    {
        while (outlines.Count <= index)
        {
            GameObject go = new GameObject("Outline " + outlines.Count, typeof(RectTransform));
            go.transform.SetParent(painting.transform, false);

            RectTransform container = (RectTransform)go.transform;
            Color color = theme != null ? theme.rotusBlueLight : Color.white;

            CreateEdge(container, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -OutlineThickness), Vector2.zero, color);
            CreateEdge(container, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, OutlineThickness), color);
            CreateEdge(container, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(OutlineThickness, 0f), color);
            CreateEdge(container, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-OutlineThickness, 0f), Vector2.zero, color);

            outlines.Add(container);
        }

        return outlines[index];
    }

    // Tek bir kenar seridi: anchorMin/anchorMax o kenari (ust/alt/sol/sag)
    // boydan boya gerer, offsetMin/offsetMax OutlineThickness kadar iceri
    // keser -- dort kenarin koselerde ust uste binip koseli, ince bir
    // cerceve olusturmasinin standart yontemi (Sliced+yuvarlak sprite yerine).
    private static void CreateEdge(
        RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private void ApplyButtons(bool canUse, int holeCount, int rotusCount)
    {
        if (primaryButton != null)
        {
            primaryButton.interactable = canUse;
        }

        if (primaryGradient != null && theme != null)
        {
            primaryGradient.Set(
                canUse ? theme.goldButtonTop : theme.goldDisabled,
                canUse ? theme.goldButtonBottom : theme.goldDisabled);
        }

        if (primaryShadow != null)
        {
            primaryShadow.enabled = canUse;
        }

        if (primaryLabel != null && theme != null)
        {
            primaryLabel.text = canUse
                ? Loc.Tr("rotus_restore.primary_can_repair")
                : holeCount == 0 ? Loc.Tr("rotus_restore.primary_fully_repaired") : Loc.Tr("rotus_restore.primary_no_rotus");

            Color disabledColor = theme.textPrimary;
            disabledColor.a = 0.55f;
            primaryLabel.color = canUse ? theme.textOnGold : disabledColor;
        }

        if (secondaryLabel != null)
        {
            secondaryLabel.text = holeCount == 0 ? Loc.Tr("common.close_button") : Loc.Tr("common.cancel_button");
        }

        if (bodyLabel != null && theme != null)
        {
            string highlightedCount = "<color=#" + ColorUtility.ToHtmlStringRGB(theme.textPrimary)
                + "><b>" + rotusCount + "</b></color>";
            bodyLabel.text = string.Format(Loc.Tr("rotus_restore.body"), highlightedCount);
        }
    }

    private void PlayFlash(int pieceIndex)
    {
        if (flash == null || definition == null || theme == null)
        {
            return;
        }

        PaintingCoverGrid.CellAnchors(pieceIndex, definition.PieceCount, out Vector2 min, out Vector2 max);
        RectTransform rect = flash.rectTransform;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one * FlashFromScale;

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }

        flashRoutine = StartCoroutine(Flash());
    }

    private IEnumerator Flash()
    {
        flash.gameObject.SetActive(true);

        float elapsed = 0f;

        while (elapsed < FlashSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Easing.OutCubic(Mathf.Clamp01(elapsed / FlashSeconds));

            flash.rectTransform.localScale = Vector3.one * Mathf.Lerp(FlashFromScale, FlashToScale, k);

            Color color = theme.rotusGlow;
            color.a = Mathf.Lerp(1f, 0f, k);
            flash.color = color;

            yield return null;
        }

        flash.gameObject.SetActive(false);
        flashRoutine = null;
    }

    // -----------------------------------------------------------------------
    // Animasyon -- PaintingChoiceSheet.Slide/CloseSlide ile ayni maths,
    // bilerek ayri kopya (bkz. dosya basindaki not).
    // -----------------------------------------------------------------------

    private void StartSlide()
    {
        StopSlide();
        slideRoutine = StartCoroutine(Slide());
    }

    private void StopSlide()
    {
        if (slideRoutine != null)
        {
            StopCoroutine(slideRoutine);
            slideRoutine = null;
        }
    }

    private IEnumerator Slide()
    {
        float height = panel.rect.height;
        Vector2 target = openPosition;
        Vector2 start = new Vector2(target.x, target.y - height);

        for (float t = 0f; t < slideSeconds; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / slideSeconds);
            float eased = Easing.OutCubic(k);

            panel.anchoredPosition = Vector2.Lerp(start, target, eased);
            scrimGroup.alpha = k;

            yield return null;
        }

        panel.anchoredPosition = target;
        scrimGroup.alpha = 1f;
        slideRoutine = null;
    }

    private IEnumerator CloseSlide()
    {
        float height = panel.rect.height;
        Vector2 start = panel.anchoredPosition;
        Vector2 target = new Vector2(openPosition.x, openPosition.y - height);
        float startAlpha = scrimGroup.alpha;

        for (float t = 0f; t < slideSeconds; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / slideSeconds);
            float eased = Easing.OutCubic(k);

            panel.anchoredPosition = Vector2.Lerp(start, target, eased);
            scrimGroup.alpha = Mathf.Lerp(startAlpha, 0f, eased);

            yield return null;
        }

        panel.anchoredPosition = target;
        scrimGroup.alpha = 0f;
        slideRoutine = null;
        closing = false;

        if (navigator != null)
        {
            navigator.SetModalOpen(false);
        }

        gameObject.SetActive(false);
    }
}
