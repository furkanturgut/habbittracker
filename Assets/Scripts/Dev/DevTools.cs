using System;
using UnityEngine;

// Gelistirici modunun sahnedeki tek yuzu: Inspector'daki anahtar ve butonlarin
// arkasindaki komutlar.
//
// Burada KURAL YOK, yalnizca kapi var. Her komut once "dev mode acik mi,
// oynatiyor muyuz, yonetici var mi" diye bakiyor, sonra isi ilgili dev
// servisine devrediyor (DevClock, DevMoodTools, DevPaintingTools,
// DevHabitTools, DevSampleHabits). Boylece anahtarin anlami tek bir yerde
// tanimli kaliyor ve Editor tarafi (DevToolsEditor) yalnizca bu sinifi taniyor.
//
// Anahtar KAPALIYKEN bu bilesen hicbir sey yapmaz: saat kaydirmasi uygulanmaz ve
// butun komutlar sessizce reddedilir. Sahneye eklenmis olmasi tek basina
// uygulamanin davranisini degistirmez.
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public class DevTools : MonoBehaviour
{
    [Header("Gelistirici Modu")]
    [Tooltip("Kapaliyken butun dev komutlari reddedilir ve saat gercek tarihe doner.")]
    [SerializeField] private bool devMode;

    [Header("Tarih")]
    [Tooltip("Gercek tarihe eklenecek gun sayisi. Play moda girerken uygulanir; oyun sirasinda butonlarla degistirilir.")]
    [SerializeField] private int dayOffset;

    [Header("Baglantilar")]
    [Tooltip("Bos birakilirsa sahnede aranir.")]
    [SerializeField] private HabbitManager manager;

    public int DayOffset => dayOffset;

    // Yoneticiye dokunulabilir mi? Oynatma sarti SART: HabbitManager kaydi kendi
    // Awake'inde yukluyor, Play modu disinda Data'ya dokunmak kurulmamis bir
    // yoneticiye dokunmak olurdu. Anahtardan BAGIMSIZ: anahtari kapatan
    // ApplyClock'un da kaydi tazelemesi gerekiyor (saat gercege donuyor).
    private bool CanTouchManager => Application.isPlaying
        && manager != null
        && manager.isActiveAndEnabled;

    // Dev komutlari calisabilir mi?
    public bool IsLive => devMode && CanTouchManager;

    public HabbitManager Manager => manager;

    // Saat, HabbitManager.Awake'ten ONCE kurulmali: Load() ilk gun hesabini
    // (mood + reset) o anda yapiyor. Siralama DefaultExecutionOrder(-1000) ile
    // garanti ediliyor.
    //
    // Burada RefreshDayIfNeeded YOK: yoneticinin Awake'i daha calismadi, kaydi
    // henuz yuklenmedi. Bu asamada tek is saatin dogru baslamasi.
    private void Awake()
    {
        ResolveManager();
        AppClock.SetDayOffset(devMode ? AppClock.Clamp(dayOffset) : 0);
    }

    private void OnValidate()
    {
        dayOffset = AppClock.Clamp(dayOffset);
    }

    private void ResolveManager()
    {
        if (manager == null)
        {
            manager = FindFirstObjectByType<HabbitManager>();
        }
    }

    // -----------------------------------------------------------------------
    // Tarih
    // -----------------------------------------------------------------------

    // Alandaki kaydirmayi uygular ve gun dondurduyse uygulamanin gun donusu
    // hattini calistirir. Anahtar kapaliysa saat gercege doner ama ALAN
    // korunur: anahtari tekrar acinca ayni senaryoya geri donulebilsin.
    public void ApplyClock()
    {
        dayOffset = AppClock.Clamp(dayOffset);
        DevClock.Apply(CommandTarget(), devMode ? dayOffset : 0);
    }

    public void ShiftDays(int deltaDays)
    {
        if (!devMode)
        {
            return;
        }

        DevClock.Shift(CommandTarget(), deltaDays);
        dayOffset = AppClock.DayOffset;
    }

    public void GoToDate(DateTime target)
    {
        if (!devMode)
        {
            return;
        }

        DevClock.GoTo(CommandTarget(), target);
        dayOffset = AppClock.DayOffset;
    }

    public void ResetClock()
    {
        dayOffset = 0;
        ApplyClock();
    }

    // -----------------------------------------------------------------------
    // Mood
    // -----------------------------------------------------------------------

    public void SetMoodPoint(int point)
    {
        if (IsLive)
        {
            DevMoodTools.SetPoint(manager, point);
        }
    }

    public void StepMood(int delta)
    {
        if (IsLive)
        {
            DevMoodTools.Step(manager, delta);
        }
    }

    public void SetMood(PainterManager.PainterMoods mood)
    {
        if (IsLive)
        {
            DevMoodTools.SetMood(manager, mood);
        }
    }

    // -----------------------------------------------------------------------
    // Bugun
    // -----------------------------------------------------------------------

    public int CompleteAllDue()
    {
        return IsLive ? DevHabitTools.CompleteAllDue(manager) : 0;
    }

    public int UncompleteAll()
    {
        return IsLive ? DevHabitTools.UncompleteAll(manager) : 0;
    }

    // -----------------------------------------------------------------------
    // Tablo
    // -----------------------------------------------------------------------

    public void SetPaintingRatio(HabitData habit, float ratio)
    {
        if (IsLive)
        {
            DevPaintingTools.SetOpenedRatio(manager, habit, ratio);
        }
    }

    public void FillPainting(HabitData habit)
    {
        if (IsLive)
        {
            DevPaintingTools.Fill(manager, habit);
        }
    }

    public void ClearPainting(HabitData habit)
    {
        if (IsLive)
        {
            DevPaintingTools.Clear(manager, habit);
        }
    }

    public void FinishPainting(HabitData habit)
    {
        if (IsLive)
        {
            DevPaintingTools.FinishPainting(manager, habit);
        }
    }

    // "count kadar gun kacirilsin" senaryosunu elle kurar: gercekte bu,
    // MissedPieceService'in gun sonunda otomatik yaptigi seyin ayni kodla
    // tetiklenen manuel karsiligidir.
    public int MissPainting(HabitData habit, int count)
    {
        return IsLive ? DevPaintingTools.MissPieces(manager, habit, count) : 0;
    }

    public int OpenedPieces(HabitData habit)
    {
        return manager != null ? DevPaintingTools.OpenedCount(manager.Library, habit) : 0;
    }

    // -----------------------------------------------------------------------
    // Rotus
    // -----------------------------------------------------------------------

    public void GrantRotus(int amount)
    {
        if (IsLive)
        {
            DevRotusTools.Grant(manager, amount);
        }
    }

    public void ResetRotus()
    {
        if (IsLive)
        {
            DevRotusTools.Grant(manager, -manager.RotusCount);
        }
    }

    // Secili aliskanligin donguyu CycleLength-1'e oturtur: bir sonraki gercek
    // tik rotus verir, 7 gun atlamadan "dongu doldu" senaryosunu test etmek icin.
    public void FillRotusCycle(HabitData habit)
    {
        if (IsLive)
        {
            DevRotusTools.SetProgress(manager, habit, RotusCycle.CycleLength - 1);
        }
    }

    // -----------------------------------------------------------------------
    // Aliskanlik ve veri
    // -----------------------------------------------------------------------

    public void SetStreak(HabitData habit, int current, int longest)
    {
        if (IsLive)
        {
            DevHabitTools.SetStreak(manager, habit, current, longest);
        }
    }

    public int SeedSampleHabits()
    {
        return IsLive ? DevSampleHabits.Create(manager) : 0;
    }

    public void DeleteAllData()
    {
        if (IsLive)
        {
            manager.DeleteAllData();
        }
    }

    // -----------------------------------------------------------------------
    // Disa/Ice aktarma
    // -----------------------------------------------------------------------

    // Normal "Verileri disa aktar" dugmesiyle AYNI cagri -- Inspector'dan
    // tetiklenebilir.
    public SaveExportService.ExportResult ExportSave()
    {
        return IsLive ? DevSaveTransferTools.ExportToFile(manager) : default;
    }

    // Cihazdaki en yeni kurtarma dosyasindan geri yukler -- gercek bir pano/
    // dosya secici olmadan round-trip denemek icin.
    public bool ImportNewestBackup(out string error)
    {
        if (!IsLive)
        {
            error = "Dev modu kapali ya da oynatma disinda.";
            return false;
        }

        return DevSaveTransferTools.ImportNewestFile(manager, out error);
    }

    // Sistem dosya secicisinin kendisini dener (Ayarlar ekranindaki akisin
    // AYNISI) -- Editor'de gercek Windows diyaloglari acilir. Sonuc konsola
    // dusuyor, bkz. DevSaveTransferTools'un ilgili yorumu.
    public void ExportSaveViaPicker()
    {
        if (IsLive)
        {
            DevSaveTransferTools.ExportViaPicker(manager);
        }
    }

    public void ImportSaveViaPicker()
    {
        if (IsLive)
        {
            DevSaveTransferTools.ImportViaPicker(manager);
        }
    }

    // Saat komutlari oynatma DISINDA da calisiyor (yalnizca kaydirmayi yazarlar),
    // ama kaydi tazeleyecek yonetici yalnizca oynatirken var.
    private HabbitManager CommandTarget()
    {
        ResolveManager();
        return CanTouchManager ? manager : null;
    }
}
