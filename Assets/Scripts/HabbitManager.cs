using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Aliskanliklarin, tablo siparislerinin ve kullanici verisinin tek yoneticisi.
//
// Kararlarin saf olan kismi bu sinifta DEGIL: gun/streak DailyResetService'te,
// mood MoodService'te, tablo siparisi PaintingAssignment'ta, parca secimi
// PaintingProgress'te. Burasi onlari dogru SIRAYLA cagirip kaydi, goruntuleri ve
// olaylari yonetiyor. Sira kurallari icin bkz. CLAUDE.md > Invariants.
//
// Sinif PARTIAL: gelistirici araclarinin ihtiyaci olan kapilar (kaydi kirletmek,
// goruntuleri yeniden baglamak, olay yaymak) uretimde private kalsin diye
// Dev/HabbitManager.Dev.cs dosyasinda duruyor. Bu dosya o kapilari bilmiyor;
// Dev klasoru silinirse uretim kodu oldugu gibi calismaya devam eder.
//daron acemoglu
[DisallowMultipleComponent]
public partial class HabbitManager : MonoBehaviour
{
    [Header("Tablo Havuzu")]
    [SerializeField] private PaintingLibrary paintingLibrary;

    [Header("Goruntuler (istege bagli)")]
    [Tooltip("Bos birakilirsa tablolar yalnizca veri duzeyinde ilerler; gorsel baglama yapilmaz.")]
    [SerializeField] private PaintingViewPool viewPool;

    [Header("Ses Kutuphanesi")]
    [Tooltip("Bos birakilirsa SfxPlayer.Play cagrilari sessizce yok sayilir.")]
    [SerializeField] private SfxLibrary sfxLibrary;

    [Header("Yerellestirme")]
    [Tooltip("Bos birakilirsa Loc.Tr her anahtari oldugu gibi dondurur (ceviri yok).")]
    [SerializeField] private LocalizationTable localizationTable;

    [Header("Davranis")]
    [Tooltip("Uygulama acikken gece yarisi gecilirse tikleri kendiliginden sifirla.")]
    [SerializeField] private bool watchMidnightWhileOpen = true;

    [Tooltip("Tik atildiktan sonra diske yazmadan once beklenecek sure. Art arda atilan tikler tek yazmada birlesir.")]
    [SerializeField] private float saveDebounceSeconds = 1.5f;

    private HabitSaveSystem saveSystem;

    // id -> aliskanlik. Her erisimde listeyi taramamak icin.
    private readonly Dictionary<string, HabitData> habitsById =
        new Dictionary<string, HabitData>(32, StringComparer.Ordinal);

    // Alan olarak duruyor ki tik atma yolu tahsis uretmesin.
    private readonly List<int> pieceScratch = new List<int>(64);

    // Tuval boyutu degisiminin kendi scratch'i. pieceScratch'i paylasmiyor:
    // ikisi de parca secimine giriyor ve birinin ortasinda digerini calistirmak
    // (tamamlanma -> yeniden atama) tamponu sessizce bozardi.
    private readonly List<int> resizeScratch = new List<int>(64);

    // Gun sonu yakma islemiyle (MissedPieceService) parca secimi icin kendi
    // tamponu -- ayni gerekce, parca secimine giren tamponlar birbirini
    // paylasmaz.
    private readonly List<int> missedScratch = new List<int>(64);

    // SetFilterTokens'in HabitFilters.Collect icin yeniden kullandigi tampon --
    // ayar degisikligi hot path olmasa da ayni "alan olarak dur" aliskanligi.
    private readonly List<HabitFilters.HabitFilter> filterScratch = new List<HabitFilters.HabitFilter>(HabitFilters.MaxSlots);

    // MissedPieceService.SettleAll'in bu cagridan etkilenen aliskanliklari
    // yazdigi tampon. Sahne disi kullanim yok; Load/RefreshDayIfNeeded
    // icindeki tek seferlik okumadan baska bir yerde tutulmaz.
    private readonly List<HabitData> missedHabits = new List<HabitData>(8);

    // Bugunun tarihi, onbellekli: "gun degisti mi" kontrolu sik calisiyor ve
    // DateTime karsilastirmasi string uretmiyor. ToKey yalnizca gun gercekten
    // dondugunde cagriliyor.
    private DateTime todayDate;

    private Coroutine saveDebounceRoutine;
    private Coroutine midnightRoutine;

    // Goruntulere piksel dagitiliyor mu? Instant modda PaintPixels.Completed
    // dagitim dongusunun ICINDE tetiklendigi icin tamamlama erteleniyor.
    private bool revealingViews;
    private HabitData deferredCompletion;

    // Load()'da hesaplanip Start()'ta islenen gun sonu cezasi. Awake'te islenemez:
    // PainterManager baska bir GameObject'te ve Awake sirasi garanti degil.
    private int pendingMoodSteps;

    public event Action<HabitData> HabitChanged;
    public event Action<HabitData> HabitCompletedToday;

    // Tablo bitti: (aliskanlik, biten tablonun id'si). Kutlama ekrani icin.
    public event Action<HabitData, string> PaintingFinished;

    // Seri yeni bir alev basamagina girdi: (aliskanlik, yeni basamak).
    // YALNIZCA CompleteHabit tetikler -- tiki geri almak ASLA tetiklemez ve
    // gun sonu kirilmasi da tetiklemez (kutlanacak bir sey yok). Ayni gun
    // icinde geri al + tekrar tik ikinci kez tetiklemez; karari
    // StreakFlame.ShouldCelebrate veriyor (bkz. lastCelebratedTier/Date).
    public event Action<HabitData, int> StreakTierReached;

    // Yeni gun basladi ve tikler sifirlandi.
    public event Action DayRolledOver;

    // Aliskanlik LISTESININ yapisi degisti: eklendi, silindi, kategorisi degisti.
    // HabitChanged tek bir aliskanligin ICERIGI icin; bu olay listeyi bastan
    // kuran ekranlar icin. Ikisini ayirmak sart -- silinen bir aliskanlikla
    // HabitChanged tetiklemek, aboneleri artik var olmayan bir id'yi aramaya iter.
    public event Action HabitListChanged;

    // Bir ayar degisti. HabitChanged/HabitListChanged ayrimiyla ayni mantik:
    // bir ayar ne bir aliskanligin icerigi ne listenin yapisi, bu yuzden
    // ucuncu bir olay. AppSettings.Changed ile AYNI anda tetiklenir (bkz.
    // AppSettings.cs ustundeki yorum).
    public event Action SettingsChanged;

    public PlayerSaveData Data => saveSystem.Data;
    public IReadOnlyList<HabitData> Habits => saveSystem.Data.habits;
    public PaintingLibrary Library => paintingLibrary;
    public AppSettingsData Settings => saveSystem.Data.settings;

    private void Awake()
    {
        // Kayittan bagimsiz: SfxPlayer.Play'in Load() icindeki ilk gun-tamamlandi
        // kontrolunden ONCE calisir hale gelmesi gerekiyor.
        SfxPlayer.Apply(sfxLibrary);

        // LocalizedText'ler sahne acilisinda OnEnable/Start ile Loc.Tr
        // cagirir; tablo Load()'dan ONCE hazir olmali ki ilk kare dogru
        // metinle gelsin (bkz. LOCALIZATION_PLAN.md). Idempotent: Load()
        // icindeki hicbir yol bunu tekrar cagirmiyor.
        Loc.Init(localizationTable);

        saveSystem = new HabitSaveSystem(new PlayerPrefsHabitStorage());
        Load();
    }

    private void OnEnable()
    {
        if (viewPool != null)
        {
            viewPool.PaintingCompleted += HandleViewPaintingCompleted;
        }
    }

    private void OnDisable()
    {
        if (viewPool != null)
        {
            viewPool.PaintingCompleted -= HandleViewPaintingCompleted;
        }
    }

    private void Start()
    {
        // Awake'te degil burada: PainterManager.Instance o an henuz kurulmamis olabilir.
        if (PainterManager.Instance != null)
        {
            PainterManager.Instance.RestoreMoodPoint(saveSystem.Data.painterMoodPoint);
        }

        if (pendingMoodSteps > 0)
        {
            ApplyMood(-pendingMoodSteps);
            pendingMoodSteps = 0;

            // Load()'daki flush bu noktadan ONCE calisti, yani bu ceza henuz diske
            // inmedi. Debounce'a birakiyoruz ki arada kapanirsa puan kaybolmasin.
            MarkDirtyDeferred();
        }

        if (watchMidnightWhileOpen)
        {
            midnightRoutine = StartCoroutine(WatchMidnight());
        }
    }

    // Arka plana alinma ve cikis: kaydi diske indirmek icin son sans.
    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            FlushNow();
            return;
        }

        ReminderService.ClearDelivered();
        RefreshDayIfNeeded();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused)
        {
            FlushNow();
            return;
        }

        ReminderService.ClearDelivered();
        RefreshDayIfNeeded();
    }

    private void OnApplicationQuit()
    {
        FlushNow();
    }

    private void OnDestroy()
    {
        // Pisirilmis tablo kopyalari (yakilan parcali) Texture2D tutuyor ve
        // bunlar cop toplayici ile gitmiyor; sahne degisiminde birakiyoruz.
        BurnedPaintingCache.Clear();
    }

    // Mood puanini delta kadar degistirir ve kayda yazar. PainterManager yalnizca
    // PaintersScene'de oldugu icin null olabilir; puan yine de kayitta guncellenir.
    private void ApplyMood(int delta)
    {
        PainterManager painter = PainterManager.Instance;
        if (painter == null)
        {
            return;
        }

        painter.ChangeMoodPoint(delta);
        saveSystem.Data.painterMoodPoint = painter.MoodPoint;
    }

    public void Load()
    {
        saveSystem.Load();
        ApplyLoadedSave(AppClock.Now);
    }

    // Load()'un "kayit zaten hazir" sonrasi -- Awake'teki ilk acilis VE
    // TryImportSave'in ReplaceWith sonrasi tekrarladigi ayni govde. Mood
    // MUTLAKA ApplyDailyReset'ten ONCE cagrilmali (reset isCompletedToday'i
    // sifirlar ve o alanlar kapanan gunun tek kaynagi); bu sira tek bir yerde
    // yazili kalsin diye iki cagiran da BURAYA geliyor, kendi kopyasini
    // yazmiyor (bkz. EXPORT_IMPORT_PLAN.md kritik nokta #5).
    private void ApplyLoadedSave(DateTime now)
    {
        RebuildHabitIndex();

        todayDate = now.Date;

        // Mood, ApplyDailyReset'ten ONCE: reset isCompletedToday'i sifirlar ve o
        // alanlar kapanan gunun tek kaynagi. Sonuc Start()'a birakiliyor.
        pendingMoodSteps = MoodService.SettleClosedDays(saveSystem.Data, now);

        // Kacirilan planli gunlerin parcalari da ayni sebeple ApplyDailyReset'ten
        // ONCE yakilir -- isCompletedToday son giris gunu icin hala kayitta.
        // Etkilenen aliskanliklarla burada ilgilenmemize gerek yok: RepairAll
        // hemen altta, delikli de olsa bitmis her tabloyu yakalayip kapatiyor.
        int missedPieces = MissedPieceService.SettleAll(paintingLibrary, saveSystem.Data, now, missedScratch, missedHabits);

        bool changed = DailyResetService.ApplyDailyReset(saveSystem.Data, now);
        changed |= pendingMoodSteps > 0;
        changed |= missedPieces > 0;

        // Tablosu olmayan / havuzdan kalkmis / kutu icinde bitmis kalmis siparisler.
        changed |= PaintingAssignment.RepairAll(paintingLibrary, saveSystem.Data);

        // Ilk kurulum tespiti: kullanici hic dil secmemisse (yeni kayit ya da
        // bu alani hic tanimayan eski kayit) sistem diline bak, desteklenmiyorsa
        // Ingilizce'ye dus (bkz. SystemLanguageMap.Detect). Bir kez yazilinca
        // bir daha asla buraya ugramaz -- Ayarlar'dan elle degistirmek disinda.
        if (saveSystem.Data.settings.language == AppLanguage.Unset)
        {
            saveSystem.Data.settings.language = SystemLanguageMap.Detect();
            changed = true;
        }

        AppSettings.Apply(saveSystem.Data.settings);
        RotusState.Apply(saveSystem.Data.rotusCount);

        // HER ACILISTA yeniden planla -- cihaz yeniden baslatma Android
        // alarmlarini siler, "Reschedule on Device Restart" BILEREK kapali
        // (REMINDER_PLAN.md kritik duzeltme #3). Sabit NotificationId sayesinde
        // fazladan cagri zararsiz: eskisini DEGISTIRIR, yigmaz.
        ReminderService.Schedule(saveSystem.Data.settings.reminderOn, saveSystem.Data.settings.reminderTime, now);

        if (changed)
        {
            saveSystem.MarkDirty();
            FlushNow();
        }
    }

    public void FlushNow()
    {
        CancelPendingFlush();
        saveSystem.FlushIfDirty(true);
    }

    // Butun kullanici verisini siler ve yoneticiyi ilk acilis durumuna dondurur.
    //
    // Sira onemli: goruntuler KAYITTAN ONCE birakilmali, yoksa artik var olmayan
    // aliskanliklara bagli kalip havuzu tuketirler.
    public void DeleteAllData()
    {
        CancelPendingFlush();

        List<HabitData> habits = saveSystem.Data.habits;
        for (int i = 0; i < habits.Count; i++)
        {
            ReleaseViewsFor(habits[i].id);
        }

        // Pisirilmis tablo kopyalari habitId'ye gore onbelleklenmis; silinen
        // id'ler bir dahaki acilista yeniden kullanilabilir, bayat sprite
        // birakilmamali.
        BurnedPaintingCache.Clear();

        saveSystem.DeleteAll();
        RebuildHabitIndex();

        todayDate = AppClock.Now.Date;

        // Silinen veriye ait bir ceza yeni kayda islenmemeli.
        pendingMoodSteps = 0;

        if (PainterManager.Instance != null)
        {
            PainterManager.Instance.RestoreMoodPoint(saveSystem.Data.painterMoodPoint);
        }

        // DeleteAll() ayarlari sifirlar (language = Unset) ve bu da "yeni
        // kurulum" hissi vermeli -- ApplyLoadedSave'deki AYNI tespit, burada
        // da tekrarlanir (bkz. yukaridaki yorum).
        if (saveSystem.Data.settings.language == AppLanguage.Unset)
        {
            saveSystem.Data.settings.language = SystemLanguageMap.Detect();
        }

        // "Butun kullanici verisini sil" ayarlari da kapsiyor (bkz. yukaridaki
        // sinif yorumu) -- ayna ve onun dinleyicileri de ilk acilis durumuna donsun.
        AppSettings.Apply(saveSystem.Data.settings);
        RotusState.Apply(saveSystem.Data.rotusCount);

        // Ayarlar varsayilana donuyor (reminderOn=false) -- eski saatli alarm
        // asili kalmasin (REMINDER_PLAN.md kritik duzeltme #5). reminderOn
        // false oldugu icin bu cagri Cancel()'a esdeger.
        ReminderService.Schedule(saveSystem.Data.settings.reminderOn, saveSystem.Data.settings.reminderTime);

        SettingsChanged?.Invoke();
        RotusChanged?.Invoke(saveSystem.Data.rotusCount);

        HabitListChanged?.Invoke();
    }

    // --- Ayar yazma yuzeyi: alan basina tek metot (CreateHabit/UpdateHabit ile
    // ayni tarz). Her biri DEGISMEDIYSE hicbir sey yapmaz -- MarkDirtyDeferred'in
    // debounce'u bosa calismasin (bkz. NAVIGATION_SETTINGS_PLAN.md Faz 1).

    public void SetPainterName(string name)
    {
        string value = NormalizePainterName(name);
        if (string.Equals(saveSystem.Data.settings.painterName, value, StringComparison.Ordinal))
        {
            return;
        }

        saveSystem.Data.settings.painterName = value;
        ApplySettingsChange();
    }

    // Ayarlar ekranindaki TMP_InputField zaten characterLimit=14 ile sinirli,
    // ama bu bir SAVUNMA hatti: Dev araclari ya da ornek veri UI'dan GECMEDEN
    // cagirabilir, bu yuzden kirpma/varsayilan kurali burada da tekrarlanir.
    // Public: saf bir fonksiyon, Tests/SettingsLogicTests dogrudan cagiriyor
    // (HabbitManager'in kendisi MonoBehaviour oldugu icin sahnesiz kurulamiyor).
    public static string NormalizePainterName(string name)
    {
        string trimmed = name == null ? string.Empty : name.Trim();
        if (trimmed.Length == 0)
        {
            return AppSettingsData.DefaultPainterName;
        }

        return trimmed.Length > 14 ? trimmed.Substring(0, 14) : trimmed;
    }

    public void SetReminder(bool on, string time)
    {
        string value = TimeOfDay.IsValid(time) ? time : AppSettingsData.DefaultReminderTime;

        AppSettingsData settings = saveSystem.Data.settings;
        if (settings.reminderOn == on && string.Equals(settings.reminderTime, value, StringComparison.Ordinal))
        {
            return;
        }

        settings.reminderOn = on;
        settings.reminderTime = value;
        ApplySettingsChange();
    }

    public void SetSoundOn(bool on)
    {
        if (saveSystem.Data.settings.soundOn == on)
        {
            return;
        }

        saveSystem.Data.settings.soundOn = on;
        ApplySettingsChange();
    }

    public void SetHapticsOn(bool on)
    {
        if (saveSystem.Data.settings.hapticsOn == on)
        {
            return;
        }

        saveSystem.Data.settings.hapticsOn = on;
        ApplySettingsChange();
    }

    public void SetWeekStart(WeekStart start)
    {
        if (saveSystem.Data.settings.weekStart == start)
        {
            return;
        }

        saveSystem.Data.settings.weekStart = start;
        ApplySettingsChange();
    }

    public void SetLanguage(AppLanguage language)
    {
        if (saveSystem.Data.settings.language == language)
        {
            return;
        }

        saveSystem.Data.settings.language = language;
        ApplySettingsChange();
    }

    public void SetPremium(bool premium)
    {
        if (saveSystem.Data.settings.isPremium == premium)
        {
            return;
        }

        saveSystem.Data.settings.isPremium = premium;
        ApplySettingsChange();
    }

    // Normalize edilmis adi (basariliysa) ya da "" (reddedildiyse -- bos,
    // yerlesikle/varolan ozelle cakisma, MaxCustom'a dolmus) doner.
    public string AddCustomCategory(string raw)
    {
        AppSettingsData settings = saveSystem.Data.settings;

        if (settings.customCategories.Count >= HabitCategories.MaxCustom)
        {
            return string.Empty;
        }

        string normalized = HabitCategories.NormalizeCustom(raw, settings.customCategories);
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        settings.customCategories.Add(normalized);
        ApplySettingsChange();

        return normalized;
    }

    // Yeni adi (basariliysa) ya da "" doner. Bu kategoriyi kullanan her
    // aliskanligin categoryGroup'u da guncellenir -- yoksa kayitta eski ad
    // asili kalir ve o aliskanlik hic eslesmeyen bir baslik altinda gorunur.
    public string RenameCustomCategory(string oldName, string raw)
    {
        AppSettingsData settings = saveSystem.Data.settings;
        int index = settings.customCategories.FindIndex(
            c => string.Equals(c, oldName, StringComparison.Ordinal));

        if (index < 0)
        {
            return string.Empty;
        }

        // Cakisma kontrolu kendi eski adini saymamali -- yeniden adlandirmayi
        // ayni ada (farkli buyuk/kucuk harfle) birakmak mesru bir islem.
        List<string> others = new List<string>(settings.customCategories);
        others.RemoveAt(index);

        string normalized = HabitCategories.NormalizeCustom(raw, others);
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        string previous = settings.customCategories[index];
        settings.customCategories[index] = normalized;

        List<HabitData> habits = saveSystem.Data.habits;
        for (int i = 0; i < habits.Count; i++)
        {
            if (string.Equals(habits[i].categoryGroup, previous, StringComparison.Ordinal))
            {
                habits[i].categoryGroup = normalized;
            }
        }

        ApplySettingsChange();
        HabitListChanged?.Invoke();

        return normalized;
    }

    // Bu kategoriyi kullanan aliskanliklar "Diger"e duser; kategoriyi
    // hedefleyen bir filtre token'i varsa o da listeden kalkar.
    public bool RemoveCustomCategory(string name)
    {
        AppSettingsData settings = saveSystem.Data.settings;
        int index = settings.customCategories.FindIndex(
            c => string.Equals(c, name, StringComparison.Ordinal));

        if (index < 0)
        {
            return false;
        }

        settings.customCategories.RemoveAt(index);

        List<HabitData> habits = saveSystem.Data.habits;
        for (int i = 0; i < habits.Count; i++)
        {
            if (string.Equals(habits[i].categoryGroup, name, StringComparison.Ordinal))
            {
                habits[i].categoryGroup = HabitCategories.Other;
            }
        }

        settings.filterTokens.RemoveAll(t => string.Equals(t, name, StringComparison.Ordinal));
        if (settings.filterTokens.Count == 0)
        {
            settings.filterTokens.AddRange(HabitFilters.DefaultTokens);
        }

        ApplySettingsChange();
        HabitListChanged?.Invoke();

        return true;
    }

    // Ana sayfa filtre satirinin gosterdigi secimi yazar. tokens
    // HabitFilters.Collect'ten gecirilip MaxSlots'a kirpilir -- cagrici
    // (Ayarlar ekrani) kendi basina sinira uymak zorunda degil.
    public void SetFilterTokens(List<string> tokens)
    {
        HabitFilters.Collect(tokens, filterScratch);

        List<string> resolved = new List<string>(filterScratch.Count);
        for (int i = 0; i < filterScratch.Count; i++)
        {
            resolved.Add(HabitFilters.Token(filterScratch[i]));
        }

        if (TokensEqual(saveSystem.Data.settings.filterTokens, resolved))
        {
            return;
        }

        saveSystem.Data.settings.filterTokens = resolved;
        ApplySettingsChange();
    }

    private static bool TokensEqual(List<string> a, List<string> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (int i = 0; i < a.Count; i++)
        {
            if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    // Butun Setxxx metotlarinin ortak kuyrugu: aynaya yaz, debounce'a birak,
    // ekranlara bildir. AppSettings.Changed ile SettingsChanged AYNI anda
    // tetiklenir (bkz. AppSettings.cs ustundeki yorum).
    private void ApplySettingsChange()
    {
        AppSettings.Apply(saveSystem.Data.settings);
        ReminderService.Schedule(saveSystem.Data.settings.reminderOn, saveSystem.Data.settings.reminderTime);
        MarkDirtyDeferred();
        SettingsChanged?.Invoke();
    }

    // Panel kapanirken gameObject.SetActive(false) oluyor, bu kendi
    // coroutine'lerini oldurur -- OS izin diyalogu acikken panel kapatilsa
    // geri cagri hic calismaz. HabitSystem nesnesi hic deaktif olmadigi icin
    // coroutine'i BURADA kosturuyoruz (REMINDER_PLAN.md kritik duzeltme #2).
    public void RequestReminderPermission(Action<bool> done)
    {
        if (!isActiveAndEnabled)
        {
            done?.Invoke(false);
            return;
        }

        StartCoroutine(ReminderService.RequestPermission(done));
    }

    // Art arda atilan tikler tek yazmada birlesir.
    private void MarkDirtyDeferred()
    {
        saveSystem.MarkDirty();

        if (!isActiveAndEnabled)
        {
            // Coroutine calistiramayiz; bekletmek yerine hemen yaziyoruz.
            saveSystem.FlushIfDirty(true);
            return;
        }

        if (saveDebounceRoutine == null)
        {
            saveDebounceRoutine = StartCoroutine(FlushAfterDelay());
        }
    }

    private IEnumerator FlushAfterDelay()
    {
        yield return new WaitForSeconds(saveDebounceSeconds);
        saveDebounceRoutine = null;
        saveSystem.FlushIfDirty(true);
    }

    // Hemen yazan ya da kaydi silen her yol buradan geciyor: aksi halde coroutine
    // sonradan uyanip artik gecersiz bir durumu yaziyor.
    private void CancelPendingFlush()
    {
        if (saveDebounceRoutine != null)
        {
            StopCoroutine(saveDebounceRoutine);
            saveDebounceRoutine = null;
        }
    }

    private void RebuildHabitIndex()
    {
        habitsById.Clear();

        List<HabitData> habits = saveSystem.Data.habits;
        for (int i = 0; i < habits.Count; i++)
        {
            HabitData habit = habits[i];

            if (string.IsNullOrEmpty(habit.id))
            {
                habit.id = Guid.NewGuid().ToString("N");
            }

            if (habitsById.ContainsKey(habit.id))
            {
                Debug.LogError("HabbitManager: '" + habit.id + "' id'si birden fazla aliskanlikta. Ikincisine yeni id veriliyor.");
                habit.id = Guid.NewGuid().ToString("N");
            }

            habitsById.Add(habit.id, habit);
        }
    }

    // Gun degistiyse uygular. Gece yarisi gecildiginde ve arka plandan donuste.
    public bool RefreshDayIfNeeded()
    {
        DateTime now = AppClock.Now;

        // Hizli yol: gunun donmedigi olagan durumda hicbir tahsis olmamali.
        if (now.Date == todayDate)
        {
            return false;
        }

        // Mood, ApplyDailyReset'ten ONCE (bkz. Load).
        int moodSteps = MoodService.SettleClosedDays(saveSystem.Data, now);

        // Kacirilan gunlerin parcalari da ayni sebeple ApplyDailyReset'ten ONCE
        // yakilir -- isCompletedToday son giris gunu icin hala kayitta.
        MissedPieceService.SettleAll(paintingLibrary, saveSystem.Data, now, missedScratch, missedHabits);

        if (!DailyResetService.ApplyDailyReset(saveSystem.Data, now))
        {
            // Onbellek bayatlamis ama kayit zaten bugune damgali: gun donusu
            // olayini ikinci kez bildirmemeliyiz.
            todayDate = now.Date;
            return false;
        }

        todayDate = now.Date;

        ApplyMood(-moodSteps);

        // Uygulama acikken bir parca yanmissa goruntu ve ekran bunu HEMEN
        // gormeli -- RepairAll gibi acilista degil, burada kendimiz bildiriyoruz.
        // UpdateHabit'teki Resize-tamamlama dallanmasiyla ayni kalip: tamamlandiysa
        // galeri + olay CompleteActivePainting'in isi, degilse biz tazeliyoruz.
        for (int i = 0; i < missedHabits.Count; i++)
        {
            HabitData habit = missedHabits[i];
            bool sequential = PaintingAssignment.IsSequential(paintingLibrary, habit);

            if (PaintingProgress.IsComplete(habit, sequential))
            {
                CompleteActivePainting(habit);
            }
            else
            {
                RebindViewsFor(habit);
                HabitChanged?.Invoke(habit);
            }
        }

        saveSystem.MarkDirty();
        FlushNow();

        DayRolledOver?.Invoke();
        return true;
    }

    // Her kare tarih karsilastirmak yerine tek bir uyku: uygulama saatlerce acik
    // kalsa bile maliyet sifir.
    private IEnumerator WatchMidnight()
    {
        while (true)
        {
            float wait = DailyResetService.SecondsUntilNextMidnight(AppClock.Now);
            // Bir saniye pay: uyandigimizda tarih gercekten donmus olsun.
            yield return new WaitForSecondsRealtime(wait + 1f);

            RefreshDayIfNeeded();
        }
    }

    public bool TryGetHabit(string habitId, out HabitData habit)
    {
        if (string.IsNullOrEmpty(habitId))
        {
            habit = null;
            return false;
        }

        return habitsById.TryGetValue(habitId, out habit);
    }

    public HabitData CreateHabit(
        string title,
        string categoryGroup,
        string habitType,
        List<DayOfWeek> frequency,
        int canvasSize = HabitData.DefaultCanvasSize,
        bool hasEndDate = false,
        string endDate = null,
        string paintingId = null,

        // Sonda ve istege bagli: hedefi vermeyen eski cagrilar (DevSampleHabits)
        // bugunku davranisi aynen alir.
        int targetPerDay = HabitReps.MinTarget)
    {
        // Tarihi tazelemeden yaratmak, gece yarisini gecmis bir oturumda yeni
        // aliskanliga DUNUN startDate'ini verirdi.
        RefreshDayIfNeeded();

        DateTime now = AppClock.Now;

        HabitData habit = new HabitData
        {
            id = Guid.NewGuid().ToString("N"),
            title = title,
            categoryGroup = categoryGroup,
            habitType = string.IsNullOrEmpty(habitType) ? HabitTypes.Build : habitType,
            hasEndDate = hasEndDate,
            startDate = HabitScheduling.ToKey(now),
            endDate = endDate,
            canvasSize = canvasSize,
            targetPerDay = HabitReps.ClampTarget(targetPerDay)
        };

        if (frequency != null)
        {
            for (int i = 0; i < frequency.Count; i++)
            {
                habit.frequency.Add(frequency[i]);
            }
        }

        // Kullanici tablo sectiyse onu atiyoruz; secmediyse YA DA id artik
        // cozulemiyorsa eski yola dusuyoruz: kitapliktan rastgele.
        if (!PaintingAssignment.Assign(paintingLibrary, habit, paintingId)
            && !PaintingAssignment.AssignNext(paintingLibrary, habit))
        {
            Debug.LogError("HabbitManager: tablo atanamadi, Painting Library atanmis mi?", this);
        }

        saveSystem.Data.habits.Add(habit);
        habitsById.Add(habit.id, habit);

        // Gunun odulu verilmisken yeni bir planli aliskanlik eklenirse gun artik
        // tamamlanmis sayilmaz. Odulu geri almazsak "hepsini tikla, sonra ekle"
        // bedava puan olurdu -- UncompleteHabit ile ayni yol, ayni damga kurali.
        if (HabitScheduling.IsDueOn(habit, now))
        {
            RollBackTodayMood(now);
        }

        MarkDirtyDeferred();
        HabitChanged?.Invoke(habit);
        HabitListChanged?.Invoke();

        return habit;
    }

    // Var olan bir aliskanligi duzenler.
    //
    // startDate'e BILEREK dokunulmuyor: degistirmek IsActiveOn ve
    // TryGetPreviousDueDay icin gecmisi yeniden yazar, yani kazanilmis bir
    // streak'i sessizce kirabilir.
    public bool UpdateHabit(
        string habitId,
        string title,
        string categoryGroup,
        string habitType,
        List<DayOfWeek> frequency,
        int canvasSize,
        bool hasEndDate,
        string endDate,
        int targetPerDay)
    {
        if (!habitsById.TryGetValue(habitId, out HabitData habit))
        {
            return false;
        }

        RefreshDayIfNeeded();

        habit.title = title;
        habit.categoryGroup = categoryGroup;
        habit.habitType = string.IsNullOrEmpty(habitType) ? HabitTypes.Build : habitType;
        habit.hasEndDate = hasEndDate;
        habit.endDate = endDate;

        // Hedef degisikligi bugunku TAMAMLANMA durumuna dokunmuyor, yalnizca
        // sayaci mesru araliga cekiyor -- frequency/endDate duzenlemelerinin
        // bugune dokunmamasiyla ayni sinif (bkz. HabitReps.ApplyTarget).
        HabitReps.ApplyTarget(habit, targetPerDay);

        // Liste YERINDE tazeleniyor: yeni bir List atamak her duzenlemede cop
        // uretir ve disaridaki referanslari bayatlatirdi.
        habit.frequency.Clear();
        if (frequency != null)
        {
            for (int i = 0; i < frequency.Count; i++)
            {
                habit.frequency.Add(frequency[i]);
            }
        }

        // Tuval boyutu tablonun ozelligi: degistiyse yeni bir tablo gerekiyor.
        if (canvasSize != habit.canvasSize
            && PaintingAssignment.Resize(paintingLibrary, habit, canvasSize, resizeScratch, out bool completed))
        {
            if (completed)
            {
                // TryCompleteActive DEGIL: galeri, olay ve dagitim dongusu
                // ertelemesi CompleteActivePainting'te.
                CompleteActivePainting(habit);
            }
            else
            {
                RebindViewsFor(habit);
            }
        }

        // Program degisikligi gunu tamamlamis olabilir (ornegin aliskanlik
        // bugunden cikarildi). lastMoodSettledDate damgasi ikinci odulu onler.
        TryAwardTodayMood(AppClock.Now);

        MarkDirtyDeferred();

        HabitChanged?.Invoke(habit);
        HabitListChanged?.Invoke();

        return true;
    }

    public bool DeleteHabit(string habitId)
    {
        if (!habitsById.TryGetValue(habitId, out HabitData habit))
        {
            return false;
        }

        habitsById.Remove(habitId);
        saveSystem.Data.habits.Remove(habit);

        ReleaseViewsFor(habitId);

        // Pisirilmis tablo kopyasi da gitsin: Texture2D cop toplayici ile
        // toplanmiyor (bkz. BurnedPaintingCache).
        BurnedPaintingCache.Forget(habitId);

        // Geriye kalan planli aliskanliklarin hepsi tikliyse gun artik gercekten
        // tamamlandi: eksik olani silmek gunu mesru sekilde kapatir.
        TryAwardTodayMood(AppClock.Now);

        MarkDirtyDeferred();

        // HabitChanged BILEREK tetiklenmiyor: silinmis bir aliskanligi arayan
        // abone onu bulamaz. Yapisal degisim HabitListChanged ile bildiriliyor.
        HabitListChanged?.Invoke();

        return true;
    }

    // Bugun icin tik atar. Hedef (HabitReps.Target) 1 ise ilk tik gunu kapatir:
    // streak ilerler, tablodan bir piksel acilir. Hedef daha buyukse yalnizca
    // SON tik gunu kapatir, oncekiler sayaci ilerletir ve yine true doner.
    // Bugun planli degilse, zaten tikliyse ya da tarih araligi disindaysa false doner.
    public bool CompleteHabit(string habitId)
    {
        if (!habitsById.TryGetValue(habitId, out HabitData habit) || habit.isCompletedToday)
        {
            return false;
        }

        // Bir onceki tablo bitmis, siradaki secim kutusunu bekliyor: hangi
        // yoldan gelirse gelsin (dev tick-all dahil) secim yapilmadan parca
        // acilamaz.
        if (habit.awaitingPaintingChoice)
        {
            return false;
        }

        // Asagidaki IsDueOn kontrolu arsivdekini de yakalar ama kapiyi burada
        // ACIKCA kapatmak awaitingPaintingChoice ile ayni kalipta kaliyor.
        if (habit.isArchived)
        {
            return false;
        }

        // Tarihi tazelemeden tik atmak dunku tiki bugune sayardi.
        RefreshDayIfNeeded();

        DateTime now = AppClock.Now;
        if (!HabitScheduling.IsDueOn(habit, now))
        {
            return false;
        }

        // Gunluk tur: hedefe ULASMAYAN tik yalnizca sayaci ilerletiyor --
        // streak, tablo parcasi, mood odulu, rotus ve kutlama hepsi
        // dokunulmamis kaliyor. Gunun kapandigini hala tek bir yer soyluyor
        // (isCompletedToday), yani alt sistemlerin hicbiri bu ozelligi
        // bilmiyor (bkz. HabitReps).
        if (!HabitReps.Advance(habit))
        {
            MarkDirtyDeferred();
            HabitChanged?.Invoke(habit);
            return true;
        }

        habit.isCompletedToday = true;

        int beforeTier = StreakFlame.Tier(habit.currentStreak);
        DailyResetService.AdvanceStreak(habit, now);
        int afterTier = StreakFlame.Tier(habit.currentStreak);

        RevealNextPixel(habit);
        TryAwardTodayMood(now);
        bool earnedRotus = TryAwardRotus(habit);

        MarkDirtyDeferred();

        HabitChanged?.Invoke(habit);
        HabitCompletedToday?.Invoke(habit);

        // Rotus kutlamadan ONCE: kutlama overlay'i 0.7 s gecikmeyle aciliyor ve
        // ekrani kapliyor, "rotus kazandin" toast'inin kutlamadan sonra gelmesi
        // gerekir -- StreakFlame.ShouldCelebrate asagida, kutlama hep EN SONDA.
        if (earnedRotus)
        {
            RotusEarned?.Invoke(habit);
        }

        // Kutlama EN SON: once HabitChanged tile'i tazeliyor, yani kutlama
        // kapandiginda altinda zaten YENI alev duruyor. Ters sirada overlay
        // kapanir kapanmaz bir kare boyunca eski alev gorunurdu.
        if (StreakFlame.ShouldCelebrate(habit, beforeTier, afterTier, now))
        {
            StreakFlame.StampCelebrated(habit, afterTier, now);
            StreakTierReached?.Invoke(habit, afterTier);
        }

        return true;
    }

    // Bugunku tamamlamayi geri alir: seri geri sarilir, acilan parca kapanir ve
    // gunluk tur sayaci SIFIRA doner. Acilmis pikseli kapatmanin yolu modulde
    // yok, bu yuzden goruntu sifirdan baglanip guncel ilerlemeye getiriliyor.
    //
    // Kismi bir gun (ornegin 1/2) buradan GECMEZ: giris kosulu hala
    // isCompletedToday -- kismi tik hicbir sey odullendirmedigi icin geri
    // alinacak bir sey de yok (bkz. DAILY_REPS_PLAN.md).
    public bool UncompleteHabit(string habitId)
    {
        if (!habitsById.TryGetValue(habitId, out HabitData habit) || !habit.isCompletedToday)
        {
            return false;
        }

        // Bugun tiklendikten SONRA arsivlenmis bir aliskanlik hala
        // isCompletedToday=true tasiyabilir (Archive bu alana dokunmuyor);
        // boyle birini (ornegin dev UncompleteAll) geri almak dondurulmus
        // seriyi/parcayi bozmamali.
        if (habit.isArchived)
        {
            return false;
        }

        DateTime now = AppClock.Now;

        habit.isCompletedToday = false;

        // Geri alma gunu TAMAMEN sifirliyor (hedef 3 ise 2/3'e degil 0/3'e):
        // asagidaki geri sarma tek seferde ve tam olarak isliyor, sayaci
        // "hedef - 1"de birakmak o atomik geri almayi bolerdi. Ayrica hedefe
        // ulasmis bir tile'a dokunmanin sifira donusun TEK yolu olmasi
        // bundan geliyor (bkz. HabitReps.Reset).
        HabitReps.Reset(habit);

        DailyResetService.RollBackStreak(habit, now);

        // Tiki geri almak biten tabloyu da geri alir -- yoksa ilerlemesi artik
        // dolu olmayan bir tabloda bayrak asili kalir ve o tablo bir daha
        // bitirilemez.
        PaintingAssignment.TryReleaseHold(habit);
        PaintingProgress.UndoLastPiece(habit, PaintingAssignment.IsSequential(paintingLibrary, habit));

        RebindViewsFor(habit);
        RollBackTodayMood(now);
        RollBackRotus(habit);

        MarkDirtyDeferred();
        HabitChanged?.Invoke(habit);

        return true;
    }

    // Kullanicinin secim kutusundan yaptigi secimi uygular. paintingId bos
    // ("") ise "Rastgele" karesi secilmistir. Tik ATILMAZ -- kullanici karari;
    // tile'a bir daha dokunmak ilk parcayi yeni tabloda acar.
    public bool ChooseNextPainting(string habitId, string paintingId)
    {
        if (!habitsById.TryGetValue(habitId, out HabitData habit))
        {
            return false;
        }

        RefreshDayIfNeeded();

        if (!habit.awaitingPaintingChoice)
        {
            return false;
        }

        if (!PaintingAssignment.ChooseNext(paintingLibrary, habit, paintingId))
        {
            return false;
        }

        RebindViewsFor(habit);

        SfxPlayer.Play(SfxCue.NewPainting);

        // Tablo secimi MarkDirtyDeferred debounce'una birakiliyor -- "per-tick
        // save yok" kurali ile tutarli, FlushNow burada gerekmiyor.
        MarkDirtyDeferred();
        HabitChanged?.Invoke(habit);

        return true;
    }

    // Bu tile su an siradaki tabloyu secmeyi bekliyor mu -- hem tile'in
    // dokunusu hem de nabiz animasyonu bu tek karari okur.
    public bool NeedsPaintingChoice(HabitData habit)
    {
        return PaintingChoicePrompt.IsPending(habit, AppClock.Now);
    }

    // Gunun butun planli aliskanliklari tamamlandiysa odulu HEMEN verir; kullanici
    // gece yarisini beklemesin. lastMoodSettledDate damgasi gun icinde bir kez
    // calismasini garanti eder (bkz. CLAUDE.md > lastMoodSettledDate).
    private void TryAwardTodayMood(DateTime now)
    {
        string todayKey = HabitScheduling.ToKey(now);

        if (string.Equals(saveSystem.Data.lastMoodSettledDate, todayKey, StringComparison.Ordinal))
        {
            return;
        }

        if (!MoodService.HasAnyDueHabit(saveSystem.Data, now)
            || !MoodService.AreAllDueHabitsCompleted(saveSystem.Data, now))
        {
            return;
        }

        ApplyMood(1);
        saveSystem.Data.lastMoodSettledDate = todayKey;

        // Bu damga gun icinde TEK kez yazilir (bkz. yukaridaki erken donusler),
        // yani titresim+ses de gunde tam bir kez calar.
        HapticFeedback.Play(HapticCue.Success);
        SfxPlayer.Play(SfxCue.DayCompleted);
    }

    // Bugun verilmis odulu geri alir ve damgayi bir gun geriye ceker, boylece gun
    // sonu kontrolu bu gunu islenmemis gorup normal cezayi uygular. Yoksa "hepsini
    // tikla, birini geri al" bedava puan olurdu.
    private void RollBackTodayMood(DateTime now)
    {
        if (!string.Equals(saveSystem.Data.lastMoodSettledDate, HabitScheduling.ToKey(now), StringComparison.Ordinal))
        {
            return;
        }

        ApplyMood(-1);
        saveSystem.Data.lastMoodSettledDate = HabitScheduling.ToKey(now.Date.AddDays(-1));
    }

    // Sradaki pikseli acar ve o aliskanliga bagli TUM goruntulere ayni indeksi
    // gonderir -- ilerlemenin tek sahibi burasi.
    private void RevealNextPixel(HabitData habit)
    {
        bool sequential = PaintingAssignment.IsSequential(paintingLibrary, habit);

        int pieceIndex = PaintingProgress.OpenNextPiece(habit, sequential, pieceScratch);
        if (pieceIndex < 0)
        {
            // Tablo zaten dolu: normalde tamamlanma akisi bunu onceden yakalar.
            CompleteActivePainting(habit);
            return;
        }

        bool revealedInAnyView = false;

        if (viewPool != null && viewPool.TryGetViews(habit.id, out List<PaintingView> views))
        {
            // Instant modda Completed bu dongunun ICINDE tetiklenir; tamamlama
            // hemen calissa kalan goruntuler TAZE tabloya piksel acardi.
            revealingViews = true;

            for (int i = 0; i < views.Count; i++)
            {
                if (views[i].RevealPiece(pieceIndex, true))
                {
                    revealedInAnyView = true;
                }
                else
                {
                    // Komut kabul edilmedi (modul kurulmamis ya da parca zaten
                    // acik): kayitla ayrismasin diye yeniden bagliyoruz.
                    BindViewToHabit(views[i], habit);
                }
            }

            revealingViews = false;

            if (deferredCompletion != null)
            {
                HabitData pending = deferredCompletion;
                deferredCompletion = null;
                CompleteActivePainting(pending);
                return;
            }
        }

        // Goruntu komutu aldiysa tamamlanmayi ONA birakiyoruz: son parcanin fade'i
        // bitsin ve kullanici tablonun tamamlandigini gorsun. Hicbir goruntu
        // almadiysa (aliskanlik ekranda degil) veriden tespit ediyoruz.
        if (!revealedInAnyView && PaintingProgress.IsComplete(habit, sequential))
        {
            CompleteActivePainting(habit);
        }
    }

    private void HandleViewPaintingCompleted(PaintingView view)
    {
        if (TryGetHabit(view.BoundHabitId, out HabitData habit))
        {
            CompleteActivePainting(habit);
        }
    }

    // Galeri ve yeni tablo kurali PaintingAssignment.TryCompleteActive'de; burada
    // yalnizca zamanlama, goruntu ve olay isi var.
    private void CompleteActivePainting(HabitData habit)
    {
        // Dagitim dongusunun icindeysek tabloyu simdi degistiremeyiz.
        if (revealingViews)
        {
            deferredCompletion = habit;
            return;
        }

        if (!PaintingAssignment.TryCompleteActive(paintingLibrary, habit, out string finishedId))
        {
            // Zaten islenmis (baska bir goruntunun Completed'i once geldi).
            return;
        }

        RebindViewsFor(habit);

        MarkDirtyDeferred();
        FlushNow();

        PaintingFinished?.Invoke(habit, finishedId);
        HabitChanged?.Invoke(habit);
    }

    // Donen goruntu AttachTarget ile bir RawImage'a takilmali.
    public PaintingView BindView(string habitId)
    {
        if (viewPool == null || !habitsById.TryGetValue(habitId, out HabitData habit))
        {
            return null;
        }

        PaintingView view = viewPool.Acquire(habitId);
        if (view == null)
        {
            return null;
        }

        BindViewToHabit(view, habit);
        return view;
    }

    public void ReleaseView(PaintingView view)
    {
        if (viewPool != null)
        {
            viewPool.Release(view);
        }
    }

    private void BindViewToHabit(PaintingView view, HabitData habit)
    {
        if (PaintingAssignment.TryGetDefinition(paintingLibrary, habit, out PaintingDefinition definition))
        {
            view.Bind(habit.id, definition, habit);
        }
    }

    // Tablo degistiginde ve tik geri alindiginda gerekiyor.
    private void RebindViewsFor(HabitData habit)
    {
        if (viewPool == null || !viewPool.TryGetViews(habit.id, out List<PaintingView> views))
        {
            return;
        }

        for (int i = 0; i < views.Count; i++)
        {
            BindViewToHabit(views[i], habit);
        }
    }

    private void ReleaseViewsFor(string habitId)
    {
        if (viewPool == null || !viewPool.TryGetViews(habitId, out List<PaintingView> views))
        {
            return;
        }

        // Release listeyi degistirdigi icin sondan basa geziliyor.
        for (int i = views.Count - 1; i >= 0; i--)
        {
            viewPool.Release(views[i]);
        }
    }
}
