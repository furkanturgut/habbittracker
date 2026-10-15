using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 03 Galeri ekraninin 3. seviyesi: bir aliskanligin TEK eserini tam ekranda
// acan, onceki/sonraki ile gezilen goruntuleyici. GalleryDetailView'in
// USTUNE cizilir (bkz. GalleryViewerBuilder, panelin EN SON cocugu) ve
// GalleryScreen tarafindan acilir -- GalleryDetailView.WorkSelected ->
// GalleryScreen -> GalleryPaintingViewer.Open, dongusuz (bkz.
// GALLERY_PLAN.md "Sahiplik"). GalleryDetailView'i hic bilmiyor.
//
// Kendi HabbitManager aboneligi YOK (GalleryDetailView ile ayni sebep): Open'a
// verilen habit/library'nin o anki goruntusunu GalleryWorks.Collect ile
// KENDISI cozer -- cagiranin kendi works listesine bagli kalmaz.
[DisallowMultipleComponent]
public class GalleryPaintingViewer : MonoBehaviour
{
    private const float FadeSeconds = 0.2f;

    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private Button closeButton;

    [SerializeField] private TMP_Text habitLabel;
    [SerializeField] private TMP_Text posLabel;

    // Onceki/sonraki ile degisen tek alt agac -- PaintingArea + Info.
    // Thumbnails ve Header disarida kalir (bkz. AnimateTo basindaki not).
    [SerializeField] private RectTransform contentRect;
    [SerializeField] private CanvasGroup contentGroup;

    [SerializeField] private GalleryWorkFrame frame;

    [SerializeField] private Button prevButton;
    [SerializeField] private CanvasGroup prevGroup;
    [SerializeField] private Button nextButton;
    [SerializeField] private CanvasGroup nextGroup;

    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text metaLabel;

    [SerializeField] private Image statusFill;
    [SerializeField] private TMP_Text statusLabel;

    [SerializeField] private GameObject holesChip;
    [SerializeField] private Image holesChipBorder;
    [SerializeField] private Image holesChipFill;
    [SerializeField] private TMP_Text holesLabel;
    [SerializeField] private Button holesChipButton;
    [SerializeField] private Image holesChipGlow;

    [SerializeField] private GalleryThumb thumbPrefab;
    [SerializeField] private Transform thumbRoot;

    [SerializeField] private UiTheme theme;

    private readonly List<GalleryWork> works = new List<GalleryWork>(8);
    private readonly List<GalleryThumb> thumbs = new List<GalleryThumb>(8);

    private HabitData habit;
    private int index;

    private Coroutine fadeRoutine;
    private Coroutine navRoutine;

    // GalleryScreen'e iletilen onarim istegi -- bu sinif habit/index'i zaten
    // elinde tuttugu icin GalleryWorkFrame'in aksine ayri bir habitId/
    // paintingId onbellegine gerek yok.
    public event Action<string, string> RepairRequested;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }

        if (prevButton != null)
        {
            prevButton.onClick.AddListener(GoPrev);
        }

        if (nextButton != null)
        {
            nextButton.onClick.AddListener(GoNext);
        }

        if (holesChipButton != null)
        {
            holesChipButton.onClick.AddListener(RaiseRepairRequested);
        }
    }

    private void OnEnable()
    {
        RotusState.Changed += HandleRotusChanged;
    }

    private void OnDisable()
    {
        RotusState.Changed -= HandleRotusChanged;
    }

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }

        if (prevButton != null)
        {
            prevButton.onClick.RemoveListener(GoPrev);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(GoNext);
        }

        if (holesChipButton != null)
        {
            holesChipButton.onClick.RemoveListener(RaiseRepairRequested);
        }
    }

    public void Open(HabitData habit, PaintingLibrary library, int startIndex)
    {
        if (habit == null || library == null)
        {
            return;
        }

        GalleryWorks.Collect(library, habit, works);
        if (works.Count == 0)
        {
            return;
        }

        this.habit = habit;
        index = Mathf.Clamp(startIndex, 0, works.Count - 1);

        gameObject.SetActive(true);
        Bind();

        StopNav();
        contentRect.anchoredPosition = Vector2.zero;
        contentGroup.alpha = 1f;

        StopFade();
        fadeGroup.alpha = 0f;
        fadeRoutine = StartCoroutine(Fade(0f, 1f, true));
    }

    public void Close()
    {
        if (!gameObject.activeSelf)
        {
            return;
        }

        StopNav();
        StopFade();
        fadeRoutine = StartCoroutine(Fade(fadeGroup.alpha, 0f, false));
    }

    // Seviye 2 kapanirken bu goruntuleyici de uzerindeyse ANINDA kaybolmali --
    // GalleryDetailView.Closing'in tek tuketicisi (bkz. GalleryScreen).
    public void CloseImmediate()
    {
        StopNav();
        StopFade();
        fadeGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    // GalleryDetailView.Refresh ile AYNI gerekce: rotus onarimi gibi bu habit
    // ACIKKEN gorunumu degistiren bir olay geldiginde works listesini yeniden
    // toplayip ayni indexte Bind() eder, fade/slide OYNATMADAN. habitId kontrolu
    // sart -- bu goruntuleyici baska bir aliskanligi gosteriyor olabilir, onun
    // onarimi BU ekrani etkilememeli.
    public void Refresh(PaintingLibrary library, string habitId)
    {
        if (!gameObject.activeSelf || habit == null
            || !string.Equals(habit.id, habitId, StringComparison.Ordinal))
        {
            return;
        }

        GalleryWorks.Collect(library, habit, works);
        index = Mathf.Clamp(index, 0, works.Count - 1);
        Bind();
    }

    private void GoPrev()
    {
        if (index > 0)
        {
            AnimateTo(index - 1, -1f);
        }
    }

    private void GoNext()
    {
        if (index < works.Count - 1)
        {
            AnimateTo(index + 1, 1f);
        }
    }

    // Sifirdan "zero animasyon" izlenimini duzeltiyor: yeni eser, geldigi
    // yondeki kenardan (sign: sonraki = +1 sagdan, onceki = -1 soldan) kayip
    // solarak yerine oturuyor. GoTo (kucuk resme dokunma) bunu BILEREK
    // oynatmiyor -- teslim dosyasi yalniz onceki/sonraki icin gecis istiyor
    // (bkz. dosya basindaki not), kucuk resme dokunma aninda atliyor.
    private void AnimateTo(int target, float sign)
    {
        index = Mathf.Clamp(target, 0, works.Count - 1);
        Bind();

        StopNav();
        navRoutine = StartCoroutine(SlideIn(sign));
    }

    private IEnumerator SlideIn(float sign)
    {
        Vector2 from = new Vector2(sign * GalleryBands.NavSlideDistance, 0f);
        float elapsed = 0f;

        while (elapsed < GalleryBands.NavTransitionSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Easing.OutCubic(Mathf.Clamp01(elapsed / GalleryBands.NavTransitionSeconds));
            contentRect.anchoredPosition = Vector2.LerpUnclamped(from, Vector2.zero, t);
            contentGroup.alpha = t;
            yield return null;
        }

        contentRect.anchoredPosition = Vector2.zero;
        contentGroup.alpha = 1f;
        navRoutine = null;
    }

    private void StopNav()
    {
        if (navRoutine != null)
        {
            StopCoroutine(navRoutine);
            navRoutine = null;
        }
    }

    // Kucuk resme tiklamak onceki/sonrakinin aksine kaydirma/solma OYNATMAZ --
    // teslim dosyasi yalniz acilis gecisini ve ok dugmelerini belirtiyor,
    // serit tiklamasi icerigi aninda degistiriyor.
    private void GoTo(int target)
    {
        index = Mathf.Clamp(target, 0, works.Count - 1);
        Bind();
    }

    private void Bind()
    {
        GalleryWork work = works[index];
        PaintingDefinition definition = work.Definition;

        habitLabel.text = habit.title;
        posLabel.text = (index + 1) + " / " + works.Count;

        int scale = 1;
        if (definition.sprite != null)
        {
            Vector2 native = definition.sprite.rect.size;
            scale = GalleryBands.ViewerScale(native.x, native.y);
        }

        frame.Bind(habit, work, scale);

        nameLabel.text = definition.displayName;
        metaLabel.text = work.IsFinished
            ? string.Format(Loc.Tr("gallery_viewer.meta_finished"), definition.PieceCount, work.GalleryIndex)
            : string.Format(Loc.Tr("gallery_viewer.meta_in_progress"), definition.PieceCount);

        if (work.IsFinished)
        {
            statusLabel.text = Loc.Tr("gallery_viewer.status_finished");
            statusLabel.color = theme.textOnGold;
            statusFill.color = theme.gold;
        }
        else
        {
            statusLabel.text = string.Format(Loc.Tr("gallery_tile.in_progress_sub"), work.OpenedCount, definition.PieceCount);
            statusLabel.color = theme.gold;
            statusFill.color = Color.clear;
        }

        ApplyHolesChip(work);

        SetNav(prevButton, prevGroup, index > 0);
        SetNav(nextButton, nextGroup, index < works.Count - 1);

        BindThumbs();
    }

    // Rotus stogu varken cip mavi + parildayan + tiklanabilir olur (bkz.
    // GalleryWorkFrame.ApplyBadgeStyle ile AYNI karar: RotusState.Count ve
    // delik sayisi birlikte "aktif" olup olmadigini belirliyor).
    private void ApplyHolesChip(in GalleryWork work)
    {
        bool hasHoles = work.Holes.Count > 0;
        holesChip.SetActive(hasHoles);

        if (!hasHoles)
        {
            StopHolesGlow();
            return;
        }

        bool rotusActive = RotusState.Count > 0;

        holesLabel.text = rotusActive
            ? string.Format(Loc.Tr("gallery_viewer.holes_count_label"), work.Holes.Count)
                + "<color=#" + ColorUtility.ToHtmlStringRGB(theme.rotusBlueLight) + "> · "
                + Loc.Tr("gallery_viewer.holes_repair_action") + "</color>"
            : string.Format(Loc.Tr("gallery_viewer.holes_count_label"), work.Holes.Count);

        if (holesChipBorder != null)
        {
            holesChipBorder.color = rotusActive ? theme.rotusGlow : theme.galleryHolesChipBorder;
            holesChipBorder.raycastTarget = rotusActive;
        }

        if (holesChipFill != null)
        {
            Color fill = rotusActive ? theme.rotusBlue : Color.clear;
            fill.a = rotusActive ? 0.35f : 0f;
            holesChipFill.color = fill;
        }

        if (holesChipButton != null)
        {
            holesChipButton.enabled = rotusActive;
        }

        if (rotusActive)
        {
            StartHolesGlow();
        }
        else
        {
            StopHolesGlow();
        }
    }

    private void StartHolesGlow()
    {
        if (holesChipGlow == null)
        {
            return;
        }

        holesChipGlow.gameObject.SetActive(true);
        holesChipGlow.color = theme.rotusGlow;

        GlowPulse.EnsureOn(holesChipGlow).Play(
            GalleryBands.GlowMinAlpha, GalleryBands.GlowMaxAlpha,
            GalleryBands.GlowMinScale, GalleryBands.GlowMaxScale,
            GalleryBands.GlowPeriodSeconds);
    }

    private void StopHolesGlow()
    {
        if (holesChipGlow == null)
        {
            return;
        }

        GlowPulse pulse = holesChipGlow.GetComponent<GlowPulse>();
        if (pulse != null)
        {
            pulse.Stop();
        }

        holesChipGlow.gameObject.SetActive(false);
    }

    // RotusRestoreSheet kapanip stok degisince (orn. son rotus harcanince)
    // acik duran goruntuleyicinin cipi KENDI BASINA yeniden bicimlenir --
    // GalleryWorkFrame'in ayni gerekcesi.
    private void HandleRotusChanged()
    {
        if (!gameObject.activeSelf || works.Count == 0)
        {
            return;
        }

        ApplyHolesChip(works[index]);
    }

    private void RaiseRepairRequested()
    {
        if (habit == null || works.Count == 0)
        {
            return;
        }

        RepairRequested?.Invoke(habit.id, works[index].Definition.id);
    }

    // Uclardaki dugme hem tiklanamaz hem soluk (.3) -- CanvasGroup ikisini
    // birden tasir, Button.interactable kendi disabledColor'ini degil.
    private static void SetNav(Button button, CanvasGroup group, bool enabled)
    {
        button.interactable = enabled;
        group.alpha = enabled ? 1f : 0.3f;
    }

    private void BindThumbs()
    {
        for (int i = 0; i < works.Count; i++)
        {
            GalleryThumb thumb = ThumbAt(i);
            thumb.gameObject.SetActive(true);
            thumb.Bind(works[i], i == index, i);
        }

        for (int i = works.Count; i < thumbs.Count; i++)
        {
            thumbs[i].gameObject.SetActive(false);
        }
    }

    private GalleryThumb ThumbAt(int i)
    {
        while (thumbs.Count <= i)
        {
            GalleryThumb created = Instantiate(thumbPrefab, thumbRoot);
            created.Clicked += GoTo;
            thumbs.Add(created);
        }

        return thumbs[i];
    }

    private void StopFade()
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
    }

    private IEnumerator Fade(float from, float to, bool opening)
    {
        float elapsed = 0f;

        while (elapsed < FadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Lerp(from, to, Easing.OutCubic(Mathf.Clamp01(elapsed / FadeSeconds)));
            yield return null;
        }

        fadeGroup.alpha = to;
        fadeRoutine = null;

        if (!opening)
        {
            gameObject.SetActive(false);
        }
    }
}
