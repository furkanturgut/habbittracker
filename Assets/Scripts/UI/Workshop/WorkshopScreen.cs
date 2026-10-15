using System;
using TMPro;
using UnityEngine;

// 01 Atolye ekraninin beyni: sayac, sovale, toast ve izgara.
//
// HabbitManager'a abone olan TEK yer burasi; alt bilesenler (tile, filtre)
// durumu kendileri aramiyor, buradan besleniyor. Boylece "hangi olay neyi
// tazeler" sorusunun tek bir cevabi oluyor.
//
// Abonelikler OnEnable/OnDisable'da, ustune OnEnable'da TAM tazeleme var: panel
// kapaliyken gelen degisiklikleri kacirmamak icin olaya guvenmek yetmiyor,
// ayrica HabbitManager kaydi Awake'te yukluyor -- yani ilk durum hicbir olay
// uretmeden hazir oluyor.
[DisallowMultipleComponent]
public class WorkshopScreen : MonoBehaviour
{
    [SerializeField] private HabbitManager manager;
    [SerializeField] private TMP_Text counterLabel;
    [SerializeField] private TMP_Text painterNameLabel;
    [SerializeField] private EaselCover easel;
    [SerializeField] private ToastView toast;
    [SerializeField] private HabitFilterBar filterBar;
    [SerializeField] private HabitTileGrid tileGrid;
    [SerializeField] private PaintingChoiceSheet choiceSheet;

    [Tooltip("Yeni alev basamagi kutlamasi. Olay buraya WorkshopScreen uzerinden geliyor.")]
    [SerializeField] private StreakCelebration celebration;

    // Ilk tazeleme Start'ta: HabbitManager kaydi kendi Awake'inde yukluyor ve
    // Unity nesneler arasi Awake sirasini garanti etmiyor -- OnEnable'da
    // manager.Habits okumak, yoneticinin Awake'i henuz calismadiysa patlardi.
    private bool started;

    private void Start()
    {
        started = true;
        RefreshAll();
    }

    private void OnEnable()
    {
        if (manager == null)
        {
            return;
        }

        manager.HabitChanged += HandleHabitChanged;
        manager.HabitCompletedToday += HandleHabitCompleted;
        manager.PaintingFinished += HandlePaintingFinished;
        manager.StreakTierReached += HandleStreakTierReached;
        manager.RotusEarned += HandleRotusEarned;
        manager.HabitListChanged += HandleHabitListChanged;
        manager.DayRolledOver += HandleDayRolledOver;
        manager.SettingsChanged += HandleSettingsChanged;

        if (filterBar != null)
        {
            filterBar.FilterChanged += HandleFilterChanged;
        }

        if (tileGrid != null)
        {
            tileGrid.TileClicked += HandleTileClicked;
        }

        if (choiceSheet != null)
        {
            choiceSheet.PaintingChosen += HandlePaintingChosen;
        }

        // Panel kapaliyken gelen degisiklikler olay olarak ulasmadi; ama ilk
        // acilista Start'i bekliyoruz (yukaridaki nota bakin).
        if (started)
        {
            RefreshAll();
        }
    }

    private void OnDisable()
    {
        if (manager != null)
        {
            manager.HabitChanged -= HandleHabitChanged;
            manager.HabitCompletedToday -= HandleHabitCompleted;
            manager.PaintingFinished -= HandlePaintingFinished;
            manager.StreakTierReached -= HandleStreakTierReached;
            manager.RotusEarned -= HandleRotusEarned;
            manager.HabitListChanged -= HandleHabitListChanged;
            manager.DayRolledOver -= HandleDayRolledOver;
            manager.SettingsChanged -= HandleSettingsChanged;
        }

        if (filterBar != null)
        {
            filterBar.FilterChanged -= HandleFilterChanged;
        }

        if (tileGrid != null)
        {
            tileGrid.TileClicked -= HandleTileClicked;
        }

        if (choiceSheet != null)
        {
            choiceSheet.PaintingChosen -= HandlePaintingChosen;
        }
    }

    // -----------------------------------------------------------------------
    // Tazeleme
    // -----------------------------------------------------------------------

    public void RefreshAll()
    {
        if (filterBar != null)
        {
            filterBar.Refresh();
        }

        if (tileGrid != null)
        {
            tileGrid.Rebuild();
        }

        RefreshHeader();
    }

    // Sayac ve sovale ayni sayidan besleniyor (DailyProgress): ikisinin
    // ayrismasi "1/1 tamam ama sovale yarim" gibi gorunurdu.
    private void RefreshHeader()
    {
        if (manager == null)
        {
            return;
        }

        DailyProgress.Count(manager.Data, AppClock.Now, out int done, out int total);

        if (counterLabel != null)
        {
            counterLabel.text = done + "/" + total;
        }

        if (easel != null)
        {
            easel.SetRevealedRatio(total > 0 ? (float)done / total : 0f);
        }

        if (painterNameLabel != null)
        {
            string name = manager.Settings.painterName;
            painterNameLabel.text = string.IsNullOrEmpty(name) ? AppSettingsData.DefaultPainterName : name;
        }
    }

    // -----------------------------------------------------------------------
    // Olaylar
    // -----------------------------------------------------------------------

    private void HandleTileClicked(string habitId)
    {
        if (manager == null || !manager.TryGetHabit(habitId, out HabitData habit))
        {
            return;
        }

        // Onceki tablo bitmis, siradaki secim kutusunu bekliyor: tik atmadan
        // once kullanici secmeli.
        if (manager.NeedsPaintingChoice(habit))
        {
            if (choiceSheet != null)
            {
                choiceSheet.Open(habit);
                return;
            }

            // Sahne builder'i yeniden calistirilmamis: secim kutusu yok. Sessiz
            // kalmak aliskanligi KALICI olarak tiklenemez birakirdi (CompleteHabit
            // bayrak yuzunden false donuyor), bu yuzden eski davranisa dusup bir
            // kez uyariyoruz.
            Debug.LogWarning("WorkshopScreen: tablo secim kutusu bagli degil; " +
                "HabitTracker > Build Painter Habits UI calistirin. Siradaki tablo rastgele atandi.", this);
            manager.ChooseNextPainting(habitId, null);
            return;
        }

        // Ilerlemenin sahibi manager: burada yalnizca niyeti iletiyoruz.
        if (habit.isCompletedToday)
        {
            manager.UncompleteHabit(habitId);
        }
        else
        {
            manager.CompleteHabit(habitId);
        }
    }

    private void HandlePaintingChosen(string habitId, string paintingId)
    {
        manager.ChooseNextPainting(habitId, paintingId);
    }

    private void HandleHabitChanged(HabitData habit)
    {
        if (tileGrid != null)
        {
            tileGrid.Refresh(habit);
        }

        RefreshHeader();
    }

    // "Hepsi tamam" icin yeni bir olay eklemedik: mood karari zaten MoodService'in
    // saf kurallarinda ve ayni sorulari buradan da sorabiliyoruz.
    private void HandleHabitCompleted(HabitData habit)
    {
        if (manager == null || toast == null)
        {
            return;
        }

        DateTime now = AppClock.Now;
        if (MoodService.HasAnyDueHabit(manager.Data, now)
            && MoodService.AreAllDueHabitsCompleted(manager.Data, now))
        {
            toast.Show(Loc.Tr("workshop.toast_all_done"));
        }
    }

    // Kutlama overlay'i HabbitManager'a KENDISI abone olmuyor: ekran 01'de
    // yoneticiyi dinleyen tek yer burasi (bkz. CLAUDE.md -- MoodRow'un
    // PainterManager'a abone olma gerekcesiyle ayni kural). Olay buradan
    // tek satirla iletiliyor.
    private void HandleStreakTierReached(HabitData habit, int tier)
    {
        if (celebration != null)
        {
            celebration.Enqueue(habit, tier);
        }
    }

    private void HandlePaintingFinished(HabitData habit, string finishedPaintingId)
    {
        if (toast != null)
        {
            toast.Show(Loc.Tr("workshop.toast_painting_finished"));
        }
    }

    // Rotus kutlamadan AYRI bir toast: StreakTierReached ekrani kapliyor ve
    // kutlama HER ZAMAN en sonda aciliyor (bkz. HabbitManager.CompleteHabit),
    // bu yuzden ikisi ayni tikte gelse bile toast kutlamanin ALTINDA kalip
    // o kapaninca zaten gorulebilir oluyor.
    private void HandleRotusEarned(HabitData habit)
    {
        if (toast != null)
        {
            toast.Show(Loc.Tr("workshop.toast_rotus_earned"));
        }
    }

    private void HandleHabitListChanged()
    {
        RefreshAll();
    }

    private void HandleDayRolledOver()
    {
        RefreshAll();
    }

    private void HandleSettingsChanged()
    {
        RefreshHeader();
    }

    private void HandleFilterChanged(HabitFilters.HabitFilter filter)
    {
        if (tileGrid != null)
        {
            tileGrid.SetFilter(filter);
        }
    }
}
