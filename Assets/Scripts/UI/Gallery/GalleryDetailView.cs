using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 03 Galeri ekraninin 2. seviyesi: tiklanan karttan tam ekrana buyuyen
// aliskanlik detayi. GalleryScreen'in cocugu -- kendi HabbitManager
// aboneligi YOK, Open'a verilen HabitData/PaintingLibrary'nin su andaki
// anlik goruntusunu cizer (bkz. CLAUDE.md > Screens, "ilk tazeleme Start'ta"
// kuralinin burada karsiligi yok cunku bu bir ekran degil, GalleryScreen'in
// zaten abone oldugu bir alt-bilesen).
//
// "panel" tasarimdaki kart<->tam-ekran rect morph'unu tasir: Open() tiklanan
// kartin EKRANDAKI (kaydirma dahil) konumunu RectTransformUtility ile
// ebeveynin yerel uzayina cozup oradan tam ekrana genisletir -- teslim
// dosyasinin getBoundingClientRect() + CSS transition kalibinin uGUI
// karsiligi. Kose yaricapi 20->44 morph'u BILINCLI YAPILMIYOR (bkz.
// GALLERY_PLAN.md "Teslim dosyasindan bilincli iki sapma") -- panel ve skin
// hep KOSESIZ dikdortgen; kartin kendi gorunumunu veren "skin" katmani
// bunun yerine 1->0 soluyor ve geri kalan sure zaten tam ekran kapliyor.
[DisallowMultipleComponent]
public class GalleryDetailView : MonoBehaviour
{
    private const float OpenSeconds = 0.38f;
    private const float ContentFadeSeconds = 0.22f;
    private const float ContentFadeDelay = 0.14f;

    [SerializeField] private RectTransform panel;
    [SerializeField] private CanvasGroup skinGroup;
    [SerializeField] private CanvasGroup contentGroup;
    [SerializeField] private Button backButton;

    [SerializeField] private TMP_Text subLabel;
    [SerializeField] private TMP_Text titleLabel;

    [SerializeField] private TMP_Text heroTotal;
    [SerializeField] private TMP_Text heroLabel;
    [SerializeField] private TMP_Text heroNote;

    // Sira sabit, iki satir 3'er karo: 1. satir seri / en iyi seri / eser
    // (teslim dosyasinin x.stats'i ile ayni), 2. satir tamamlanma% / kacirma% /
    // eser basina ortalama gun (GalleryWorks.ConsistencyStats) -- bu yuzden ayri
    // alanlar degil 6'lik dizi.
    [SerializeField] private TMP_Text[] statValues;
    [SerializeField] private TMP_Text[] statLabels;

    // 0. karonun ("Su anki seri") kendine ait uc parcasi: alev, bir sonraki
    // aleve ilerleme cubugu ve "Yeni alev: N gun" alt yazisi. Diger bes karoda
    // karsiligi yok, o yuzden dizi degil tekil alanlar.
    [SerializeField] private FlameSprite streakFlame;
    [SerializeField] private Image streakBar;
    [SerializeField] private TMP_Text streakNext;

    [SerializeField] private UiTheme theme;

    [SerializeField] private TMP_Text planLabel;
    [SerializeField] private DayChipRow planDays;

    [SerializeField] private TMP_Text worksLabel;
    [SerializeField] private GalleryWorkTile tilePrefab;
    [SerializeField] private Transform tileRoot;

    // GalleryWorks.Totals zaten Collect'i kendi icinde cagirip bu tamponu
    // dolduruyor -- hem gun/kacirma toplamlarini hem de "Eserler" izgarasinin
    // SIRASINI ayni cagridan aliyoruz, Collect'i ikinci kez cagirmaya gerek yok.
    private readonly List<GalleryWork> works = new List<GalleryWork>(8);
    private readonly List<GalleryWorkTile> tiles = new List<GalleryWorkTile>(8);

    private float sourceLeft;
    private float sourceTop;
    private float sourceWidth;
    private float sourceHeight;

    private Coroutine routine;

    public HabitData CurrentHabit { get; private set; }

    // GalleryScreen'in Seviye 3'u (GalleryPaintingViewer) acmak icin tuketti --
    // bkz. HandleTileClicked.
    public event Action<int> WorkSelected;

    // GalleryScreen'in Seviye 3'u de ANINDA kapatmasi icin -- "Seviye 2
    // kapanirken goruntuleyici de kapanmali" kurali (bkz. Galeri.dc.html
    // README, Level 3 "Close animation"). Dongu olusturmamak icin
    // GalleryPaintingViewer burayi DINLEMIYOR; GalleryScreen ikisini de
    // kendisi yonetiyor (bkz. GALLERY_PLAN.md "Sahiplik").
    public event Action Closing;

    // "ESERLER" izgarasindaki herhangi bir karonun rozetinden gelen onarim
    // istegi -- GalleryScreen'e habitId+paintingId olarak iletiliyor.
    public event Action<string, string> RepairRequested;

    private void Awake()
    {
        if (backButton != null)
        {
            backButton.onClick.AddListener(Close);
        }
    }

    private void OnDestroy()
    {
        if (backButton != null)
        {
            backButton.onClick.RemoveListener(Close);
        }
    }

    // source: tiklanan GalleryHabitCard'in KENDI rect'i -- panel'in ebeveynine
    // (Panel_Gallery) gore EKRANDAKI konumu kaydirma pozisyonu dahil cozulup
    // buyumenin baslangic degeri olarak saklanir (bkz. sourceLeft/.. alanlari).
    public void Open(HabitData habit, PaintingLibrary library, RectTransform source)
    {
        if (habit == null || source == null || panel == null)
        {
            return;
        }

        Bind(habit, library);

        RectTransform parentRect = (RectTransform)panel.parent;
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parentRect, source);
        float parentWidth = parentRect.rect.width;
        float parentHeight = parentRect.rect.height;

        sourceLeft = bounds.min.x + parentWidth * 0.5f;
        sourceTop = parentHeight * 0.5f - bounds.max.y;
        sourceWidth = bounds.size.x;
        sourceHeight = bounds.size.y;

        gameObject.SetActive(true);
        ApplyRect(sourceLeft, sourceTop, sourceWidth, sourceHeight);
        skinGroup.alpha = 1f;
        contentGroup.alpha = 0f;

        StopAnimation();
        routine = StartCoroutine(Animate(true));
    }

    public void Close()
    {
        if (!gameObject.activeSelf)
        {
            return;
        }

        Closing?.Invoke();

        StopAnimation();
        routine = StartCoroutine(Animate(false));
    }

    // GalleryScreen'in HabitChanged'de cagirdigi yerinde yenileme -- rotus
    // onarimi gibi, bu habit ACIKKEN gorunumu degistiren bir olay geldiginde
    // Open()'in ac/buyu animasyonunu TEKRAR OYNATMADAN ayni Bind() yolunu
    // calistirir. Bunsuz bir parca onarildiktan sonra ekranda eski (hala
    // yanik) tablo kalir, kullanici galeriye cikip tekrar girene kadar.
    public void Refresh(PaintingLibrary library)
    {
        if (!gameObject.activeSelf || CurrentHabit == null)
        {
            return;
        }

        Bind(CurrentHabit, library);
    }

    private void Bind(HabitData habit, PaintingLibrary library)
    {
        CurrentHabit = habit;

        bool quit = HabitTypes.IsQuit(habit);

        subLabel.text = string.Format(Loc.Tr("gallery_detail.sub_label"),
            HabitCategories.DisplayName(habit.categoryGroup), Loc.Tr(quit ? "habit_type.quit" : "habit_type.build"));
        titleLabel.text = habit.title;

        GalleryWorks.Totals(library, habit, works, out int days, out int missed);

        heroTotal.text = days.ToString();
        heroLabel.text = Loc.Tr(quit ? "gallery_detail.hero_label_quit" : "gallery_detail.hero_label_build");
        heroNote.text = HeroNote(habit, missed);

        if (statValues != null && statValues.Length >= 6 && statLabels != null && statLabels.Length >= 6)
        {
            statValues[0].text = habit.currentStreak.ToString();
            statLabels[0].text = Loc.Tr("gallery_detail.stat_current_streak");

            BindStreakFlame(habit);

            statValues[1].text = habit.longestStreak.ToString();
            statLabels[1].text = Loc.Tr("gallery_detail.stat_best_streak");

            int finishedCount = GalleryWorks.FinishedCount(habit);
            statValues[2].text = finishedCount.ToString();
            statLabels[2].text = Loc.Tr("gallery_detail.stat_works");

            GalleryWorks.ConsistencyStats(habit.startDate, AppClock.Now, days, missed, finishedCount,
                out float completionRate, out float missRate, out float averageDaysPerPainting);

            statValues[3].text = Mathf.RoundToInt(completionRate) + "%";
            statLabels[3].text = Loc.Tr("gallery_detail.stat_completion");

            statValues[4].text = Mathf.RoundToInt(missRate) + "%";
            statLabels[4].text = Loc.Tr("gallery_detail.stat_miss_rate");

            statValues[5].text = finishedCount > 0 ? averageDaysPerPainting.ToString("0.0") : "-";
            statLabels[5].text = Loc.Tr("gallery_detail.stat_days_per_work");
        }

        planLabel.text = habit.frequency != null && habit.frequency.Count > 0
            ? string.Format(Loc.Tr("gallery_detail.plan_days_per_week"), habit.frequency.Count)
            : Loc.Tr("gallery_detail.plan_every_day");
        planDays.ShowFrequency(habit.frequency);

        int gallery = GalleryWorks.FinishedCount(habit);
        worksLabel.text = gallery > 0
            ? string.Format(Loc.Tr("gallery_detail.works_count"), gallery)
            : Loc.Tr("gallery_detail.no_works_yet");

        for (int i = 0; i < works.Count; i++)
        {
            GalleryWorkTile tile = TileAt(i);
            tile.gameObject.SetActive(true);
            tile.Bind(habit, works[i], i);
        }

        for (int i = works.Count; i < tiles.Count; i++)
        {
            tiles[i].gameObject.SetActive(false);
        }
    }

    // 0. karonun alev/cubuk/alt-yazi uclusu. Alanlar bos olabilir: yeniden
    // derlenmemis bir sahnede (eski GalleryDetail agaci) seri karosu hala
    // duz StatTile olur ve bu uclu bagli gelmez -- diger bes karo ve sayinin
    // kendisi yine dogru cizilir (HabitTile.flame ile ayni tolerans).
    private void BindStreakFlame(HabitData habit)
    {
        int tier = StreakFlame.Tier(habit.currentStreak);
        bool pending = StreakFlame.IsPending(habit, AppClock.Now);

        if (streakFlame != null)
        {
            streakFlame.SetTier(tier, pending, large: false);
        }

        if (streakBar != null)
        {
            streakBar.fillAmount = StreakFlame.Progress(habit.currentStreak);
            streakBar.color = pending ? theme.flameBarPending : theme.FlameNumber(tier);
        }

        if (streakNext != null)
        {
            streakNext.text = tier == StreakFlame.TierCount - 1
                ? Loc.Tr("gallery_detail.streak_max")
                : string.Format(Loc.Tr("gallery_detail.streak_next"), StreakFlame.DaysToNext(habit.currentStreak));
        }
    }

    // "{n} gun kacirildi" ve "{tarih} tarihinden beri" birbirinden bagimsiz --
    // ikisi de yoksa not BOS kalir (teslim dosyasinin filter(Boolean).join).
    private static string HeroNote(HabitData habit, int missed)
    {
        bool hasStart = DateLabels.TryFormat(habit.startDate, out string startLabel);

        if (missed > 0 && hasStart)
        {
            return string.Format(Loc.Tr("gallery_detail.missed_since"), missed, startLabel);
        }

        if (missed > 0)
        {
            return string.Format(Loc.Tr("gallery_detail.missed_only"), missed);
        }

        return hasStart ? string.Format(Loc.Tr("gallery_detail.since_only"), startLabel) : string.Empty;
    }

    private GalleryWorkTile TileAt(int index)
    {
        while (tiles.Count <= index)
        {
            GalleryWorkTile created = Instantiate(tilePrefab, tileRoot);
            created.Clicked += HandleTileClicked;
            created.RepairRequested += HandleTileRepairRequested;
            tiles.Add(created);
        }

        return tiles[index];
    }

    private void HandleTileClicked(int index)
    {
        WorkSelected?.Invoke(index);
    }

    private void HandleTileRepairRequested(string habitId, string paintingId)
    {
        RepairRequested?.Invoke(habitId, paintingId);
    }

    private void StopAnimation()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    private void ApplyRect(float left, float top, float width, float height)
    {
        panel.anchoredPosition = new Vector2(left, -top);
        panel.sizeDelta = new Vector2(width, height);
    }

    // Tek coroutine ucunu de surer: rect + skin OpenSeconds boyunca, icerik
    // ContentFadeSeconds boyunca (acilista ContentFadeDelay kadar geciktirilmis,
    // kapanista gecikmesiz) -- teslim dosyasinin "transition .38s" / "opacity
    // .22s ease {{delay}}s" ikilisinin ayni zamanlamasi.
    private IEnumerator Animate(bool opening)
    {
        RectTransform parentRect = (RectTransform)panel.parent;
        float parentWidth = parentRect.rect.width;
        float parentHeight = parentRect.rect.height;

        Vector4 fullScreen = new Vector4(0f, 0f, parentWidth, parentHeight);
        Vector4 cardRect = new Vector4(sourceLeft, sourceTop, sourceWidth, sourceHeight);

        Vector4 from = opening ? cardRect : fullScreen;
        Vector4 to = opening ? fullScreen : cardRect;

        float skinFrom = opening ? 1f : 0f;
        float skinTo = opening ? 0f : 1f;
        float contentDelay = opening ? ContentFadeDelay : 0f;

        float elapsed = 0f;

        while (elapsed < OpenSeconds)
        {
            elapsed += Time.unscaledDeltaTime;

            float k = Mathf.Clamp01(elapsed / OpenSeconds);
            float eased = Easing.OutEmphasized(k);

            Vector4 rect = Vector4.Lerp(from, to, eased);
            ApplyRect(rect.x, rect.y, rect.z, rect.w);
            skinGroup.alpha = Mathf.Lerp(skinFrom, skinTo, eased);

            float contentK = Mathf.Clamp01((elapsed - contentDelay) / ContentFadeSeconds);
            float contentEased = Easing.OutCubic(contentK);
            contentGroup.alpha = opening ? contentEased : 1f - contentEased;

            yield return null;
        }

        ApplyRect(to.x, to.y, to.z, to.w);
        skinGroup.alpha = skinTo;
        contentGroup.alpha = opening ? 1f : 0f;
        routine = null;

        if (!opening)
        {
            gameObject.SetActive(false);
        }
    }
}
