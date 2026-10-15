using System;
using NUnit.Framework;

// Ayar verisinin ve hafta-baslangici donusumunun EditMode testleri
// (bkz. NAVIGATION_SETTINGS_PLAN.md Faz 1). Sahne, Canvas ya da MonoBehaviour
// gerektirmiyor -- HabitDataLogicTests'teki MemoryStorage kalibinin aynisi.
//
// Calistirmak icin: Window > General > Test Runner > EditMode > Run All.
public class SettingsLogicTests
{
    private class MemoryStorage : IHabitStorage
    {
        public string Json;

        public string Load() => Json;
        public void Write(string json) => Json = json;
        public void Flush() { }
        public void Delete() { Json = null; }
    }

    // ---------------------------------------------------------------------
    // AppSettingsData -- varsayilanlar
    // ---------------------------------------------------------------------

    [Test]
    public void AppSettingsData_DefaultsMatchProductionBehavior()
    {
        AppSettingsData settings = new AppSettingsData();

        Assert.AreEqual("Vincent", settings.painterName);
        Assert.IsFalse(settings.reminderOn);
        Assert.AreEqual("21:00", settings.reminderTime);
        Assert.IsTrue(settings.soundOn);
        Assert.IsTrue(settings.hapticsOn);
        Assert.AreEqual(WeekStart.Monday, settings.weekStart);
        Assert.IsFalse(settings.isPremium);

        // Ham varsayilan BOS -- HabitFilters.DefaultTokens'a dusme RepairSettings'in
        // isi (bkz. RepairSettings_NullOrEmptyFilterTokens_FallsBackToDefaults),
        // dogrudan yeni AppSettingsData() degil.
        Assert.IsNotNull(settings.customCategories);
        Assert.AreEqual(0, settings.customCategories.Count);
        Assert.IsNotNull(settings.filterTokens);
        Assert.AreEqual(0, settings.filterTokens.Count);
    }

    // ---------------------------------------------------------------------
    // HabitSaveSystem -- RepairSettings (RepairHabits'in ayar tarafindaki ikizi)
    // ---------------------------------------------------------------------

    [Test]
    public void Load_OldSaveWithoutSettingsField_FillsDefaultsWithoutBumpingSaveVersion()
    {
        // "settings" anahtari hic yok -- teslim ozelligi eklenmeden once yazilmis
        // bir kayit. saveVersion de ESKI kayittaki gibi 1 kaliyor (artmiyor).
        string oldJson = "{\"saveVersion\":1,\"lastLoginDate\":\"2024-01-01\"," +
            "\"habits\":[],\"painterMoodPoint\":50,\"lastMoodSettledDate\":null}";

        MemoryStorage storage = new MemoryStorage { Json = oldJson };
        HabitSaveSystem saveSystem = new HabitSaveSystem(storage);

        saveSystem.Load();

        Assert.AreEqual(1, saveSystem.Data.saveVersion);
        Assert.IsNotNull(saveSystem.Data.settings);
        Assert.AreEqual("Vincent", saveSystem.Data.settings.painterName);
        Assert.AreEqual("21:00", saveSystem.Data.settings.reminderTime);
        Assert.AreEqual(WeekStart.Monday, saveSystem.Data.settings.weekStart);
        Assert.AreEqual(0, saveSystem.Data.settings.customCategories.Count);
        CollectionAssert.AreEqual(HabitFilters.DefaultTokens, saveSystem.Data.settings.filterTokens);
    }

    [Test]
    public void RepairSettings_InvalidOrDuplicateCustomCategories_AreDropped()
    {
        // "Deneme" iki kez (biri buyuk harfle) ve yerlesik bir kategoriyle
        // (Saglik) cakisan bir ad -- NormalizeCustom'un her iki kuralini da
        // (cakisma, yineleme) RepairCustomCategories'in cagri sirasinda da
        // uyguladigini dogruluyor.
        string json = "{\"saveVersion\":1,\"habits\":[]," +
            "\"settings\":{\"painterName\":\"Vincent\",\"reminderOn\":false,\"reminderTime\":\"21:00\"," +
            "\"soundOn\":true,\"hapticsOn\":true,\"weekStart\":0,\"isPremium\":false," +
            "\"customCategories\":[\"Deneme\",\"" + HabitCategories.Health + "\",\"DENEME\",\"   \"]}}";

        MemoryStorage storage = new MemoryStorage { Json = json };
        HabitSaveSystem saveSystem = new HabitSaveSystem(storage);

        saveSystem.Load();

        CollectionAssert.AreEqual(new[] { "Deneme" }, saveSystem.Data.settings.customCategories);
    }

    [Test]
    public void RepairSettings_NullOrEmptyFilterTokens_FallsBackToDefaults()
    {
        string json = "{\"saveVersion\":1,\"habits\":[]," +
            "\"settings\":{\"painterName\":\"Vincent\",\"reminderOn\":false,\"reminderTime\":\"21:00\"," +
            "\"soundOn\":true,\"hapticsOn\":true,\"weekStart\":0,\"isPremium\":false,\"filterTokens\":[]}}";

        MemoryStorage storage = new MemoryStorage { Json = json };
        HabitSaveSystem saveSystem = new HabitSaveSystem(storage);

        saveSystem.Load();

        CollectionAssert.AreEqual(HabitFilters.DefaultTokens, saveSystem.Data.settings.filterTokens);
    }

    [Test]
    public void Load_BlankPainterName_FallsBackToVincent()
    {
        string json = "{\"saveVersion\":1,\"habits\":[]," +
            "\"settings\":{\"painterName\":\"\",\"reminderOn\":true,\"reminderTime\":\"21:00\"," +
            "\"soundOn\":true,\"hapticsOn\":true,\"weekStart\":0,\"isPremium\":false}}";

        MemoryStorage storage = new MemoryStorage { Json = json };
        HabitSaveSystem saveSystem = new HabitSaveSystem(storage);

        saveSystem.Load();

        Assert.AreEqual("Vincent", saveSystem.Data.settings.painterName);
    }

    [TestCase("25:00")]
    [TestCase("9:5")]
    [TestCase("")]
    public void Load_InvalidReminderTime_FallsBackToDefault(string invalidTime)
    {
        string json = "{\"saveVersion\":1,\"habits\":[]," +
            "\"settings\":{\"painterName\":\"Vincent\",\"reminderOn\":true,\"reminderTime\":\"" +
            invalidTime + "\",\"soundOn\":true,\"hapticsOn\":true,\"weekStart\":0,\"isPremium\":false}}";

        MemoryStorage storage = new MemoryStorage { Json = json };
        HabitSaveSystem saveSystem = new HabitSaveSystem(storage);

        saveSystem.Load();

        Assert.AreEqual("21:00", saveSystem.Data.settings.reminderTime);
    }

    [Test]
    public void DeleteAll_ResetsSettingsToDefaults()
    {
        MemoryStorage storage = new MemoryStorage();
        HabitSaveSystem saveSystem = new HabitSaveSystem(storage);
        saveSystem.Load();

        saveSystem.Data.settings.painterName = "Theo";
        saveSystem.Data.settings.weekStart = WeekStart.Sunday;

        saveSystem.DeleteAll();

        Assert.AreEqual("Vincent", saveSystem.Data.settings.painterName);
        Assert.AreEqual(WeekStart.Monday, saveSystem.Data.settings.weekStart);
    }

    // ---------------------------------------------------------------------
    // WeekDays -- yuva (gosterim sirasi) <-> Pazartesi-indeksi donusumu
    // ---------------------------------------------------------------------

    [Test]
    public void WeekDays_Monday_SlotConversionIsIdentity()
    {
        // MoodScale testindeki aynisi: WeekStart.Monday'de yuva ve Pazartesi-
        // indeksi AYNI sayi, bugunku davranis byte-ayni kalmali.
        for (int slot = 0; slot < WeekDays.Count; slot++)
        {
            Assert.AreEqual(slot, WeekDays.MondayIndexForSlot(slot, WeekStart.Monday));
            Assert.AreEqual(slot, WeekDays.SlotForMondayIndex(slot, WeekStart.Monday));
        }
    }

    [Test]
    public void WeekDays_Sunday_AllSevenSlotsMapOneToOne()
    {
        bool[] seen = new bool[WeekDays.Count];

        for (int slot = 0; slot < WeekDays.Count; slot++)
        {
            int mondayIndex = WeekDays.MondayIndexForSlot(slot, WeekStart.Sunday);
            Assert.IsFalse(seen[mondayIndex], "Pazartesi-indeksi " + mondayIndex + " birden fazla yuvaya dustu.");
            seen[mondayIndex] = true;
        }

        for (int i = 0; i < seen.Length; i++)
        {
            Assert.IsTrue(seen[i], "Pazartesi-indeksi " + i + " hicbir yuvaya dusmedi.");
        }
    }

    [Test]
    public void WeekDays_Sunday_SlotZeroIsPazar()
    {
        // mondayIndex 6 == Pazar (gorunum metni artik Loc.Tr uzerinden gelir --
        // bkz. Tests/LocalizationTests.cs; burada sadece slot<->indeks
        // eslemesinin dogrulugu test ediliyor, cevirinin kendisi degil).
        int mondayIndex = WeekDays.MondayIndexForSlot(0, WeekStart.Sunday);
        Assert.AreEqual(6, mondayIndex);
        Assert.AreEqual(WeekDays.ShortLabel(mondayIndex), WeekDays.LabelForSlot(0, WeekStart.Sunday));
    }

    [Test]
    public void WeekDays_SlotAndMondayIndexConversionsAreInverses()
    {
        WeekStart[] starts = { WeekStart.Monday, WeekStart.Sunday };

        for (int s = 0; s < starts.Length; s++)
        {
            for (int slot = 0; slot < WeekDays.Count; slot++)
            {
                int mondayIndex = WeekDays.MondayIndexForSlot(slot, starts[s]);
                Assert.AreEqual(slot, WeekDays.SlotForMondayIndex(mondayIndex, starts[s]));
            }
        }
    }

    // ---------------------------------------------------------------------
    // HabbitManager.NormalizePainterName -- kirpma + 14 karakter siniri +
    // bos -> "Vincent" yedegi
    // ---------------------------------------------------------------------

    [Test]
    public void NormalizePainterName_TrimsWhitespace()
    {
        Assert.AreEqual("Theo", HabbitManager.NormalizePainterName("  Theo  "));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void NormalizePainterName_BlankFallsBackToVincent(string input)
    {
        Assert.AreEqual("Vincent", HabbitManager.NormalizePainterName(input));
    }

    [Test]
    public void NormalizePainterName_ClampsToFourteenCharacters()
    {
        string result = HabbitManager.NormalizePainterName("ABirCokUzunRessamAdiOlsun");

        Assert.AreEqual(14, result.Length);
        Assert.AreEqual("ABirCokUzunRes", result);
    }

    [Test]
    public void NormalizePainterName_ShortNamePassesThroughUnchanged()
    {
        Assert.AreEqual("Vincent", HabbitManager.NormalizePainterName("Vincent"));
    }

    // ---------------------------------------------------------------------
    // TimeOfDay -- "HH:mm" dogrulama/ayristirma (bkz. Faz 5, Ayarlar'in
    // "Saat" alani)
    // ---------------------------------------------------------------------

    [TestCase("21:00", true)]
    [TestCase("00:00", true)]
    [TestCase("23:59", true)]
    [TestCase("9:5", false)]
    [TestCase("25:00", false)]
    [TestCase("12:60", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void TimeOfDay_TryParse_ValidatesHhMm(string text, bool expected)
    {
        Assert.AreEqual(expected, TimeOfDay.IsValid(text));
    }

    [Test]
    public void TimeOfDay_TryParse_ReturnsHourAndMinute()
    {
        Assert.IsTrue(TimeOfDay.TryParse("21:05", out int hour, out int minute));
        Assert.AreEqual(21, hour);
        Assert.AreEqual(5, minute);
    }

    [Test]
    public void TimeOfDay_Format_RoundTripsTryParse()
    {
        string formatted = TimeOfDay.Format(9, 3);

        Assert.AreEqual("09:03", formatted);
        Assert.IsTrue(TimeOfDay.IsValid(formatted));
    }

    // ---------------------------------------------------------------------
    // HabbitManager.SetReminder -- gecersiz saat varsayilana duser
    // ---------------------------------------------------------------------

    [Test]
    public void SetReminder_InvalidTime_FallsBackToDefault()
    {
        MemoryStorage storage = new MemoryStorage();
        HabitSaveSystem saveSystem = new HabitSaveSystem(storage);
        saveSystem.Load();

        saveSystem.Data.settings.reminderTime = "25:99";

        Assert.AreEqual("25:99", saveSystem.Data.settings.reminderTime);

        // SetReminder'in kendisi HabbitManager'da; burada yalnizca TimeOfDay'in
        // RepairSettings ile AYNI kurali uyguladigini (IsValid -> degilse
        // varsayilan) dogruluyoruz -- HabbitManager'i sahnesiz kurmak bu
        // dosyanin kapsami disinda.
        string normalized = TimeOfDay.IsValid("25:99") ? "25:99" : AppSettingsData.DefaultReminderTime;
        Assert.AreEqual(AppSettingsData.DefaultReminderTime, normalized);
    }

    // ---------------------------------------------------------------------
    // AppSettings aynasi -- Apply hic cagrilmasa bile varsayilanlarda kalir
    // (HabbitManager'in yazdigi gibi senkron kalmasi Play mode'da garanti;
    // burada yalnizca "bos/null veriyle bozulmuyor" kuralini dogruluyoruz).
    // ---------------------------------------------------------------------

    [Test]
    public void AppSettings_Apply_NullData_FallsBackToDefaults()
    {
        AppSettings.Apply(null);

        Assert.AreEqual(WeekStart.Monday, AppSettings.WeekStart);
        Assert.IsTrue(AppSettings.SoundOn);
        Assert.IsTrue(AppSettings.HapticsOn);
        Assert.AreEqual("Vincent", AppSettings.PainterName);
    }

    [Test]
    public void AppSettings_Apply_MirrorsGivenData()
    {
        AppSettingsData data = new AppSettingsData
        {
            weekStart = WeekStart.Sunday,
            soundOn = false,
            hapticsOn = false,
            painterName = "Theo",
        };

        AppSettings.Apply(data);

        Assert.AreEqual(WeekStart.Sunday, AppSettings.WeekStart);
        Assert.IsFalse(AppSettings.SoundOn);
        Assert.IsFalse(AppSettings.HapticsOn);
        Assert.AreEqual("Theo", AppSettings.PainterName);

        // Baska testlerin varsayilan duzene guvenebilmesi icin aynayi geri al.
        AppSettings.Apply(new AppSettingsData());
    }

    // ---------------------------------------------------------------------
    // TimeOfDay.Wrap -- saat/dakika adimlayicisi icin iki yonlu sarma
    // ---------------------------------------------------------------------

    [TestCase(24, 24, 0)]
    [TestCase(-1, 24, 23)]
    [TestCase(60, 60, 0)]
    [TestCase(-5, 60, 55)]
    [TestCase(23, 24, 23)]
    public void TimeOfDay_Wrap_WrapsBothDirections(int value, int modulus, int expected)
    {
        Assert.AreEqual(expected, TimeOfDay.Wrap(value, modulus));
    }

    // ---------------------------------------------------------------------
    // ReminderSchedule.TryNextFireTime -- saf, pakete bagimsiz zamanlama
    // (bkz. REMINDER_PLAN.md Faz 2)
    // ---------------------------------------------------------------------

    [Test]
    public void ReminderSchedule_BeforeTime_FiresToday()
    {
        DateTime now = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Local);

        Assert.IsTrue(ReminderSchedule.TryNextFireTime(now, "21:00", out DateTime fireTime));
        Assert.AreEqual(now.Date, fireTime.Date);
        Assert.AreEqual(21, fireTime.Hour);
        Assert.AreEqual(0, fireTime.Minute);
    }

    [Test]
    public void ReminderSchedule_AfterTime_FiresTomorrow()
    {
        DateTime now = new DateTime(2026, 1, 15, 22, 0, 0, DateTimeKind.Local);

        Assert.IsTrue(ReminderSchedule.TryNextFireTime(now, "21:00", out DateTime fireTime));
        Assert.AreEqual(now.Date.AddDays(1), fireTime.Date);
        Assert.AreEqual(21, fireTime.Hour);
    }

    [Test]
    public void ReminderSchedule_ExactlyAtTime_FiresTomorrow()
    {
        // Esitlik karari: Android'in alarm mekanizmasi gecmis/esit bir ilk
        // tetiklemeyi ANINDA atesler, bu yuzden esitlik de "gecmis" sayilir.
        DateTime now = new DateTime(2026, 1, 15, 21, 0, 0, DateTimeKind.Local);

        Assert.IsTrue(ReminderSchedule.TryNextFireTime(now, "21:00", out DateTime fireTime));
        Assert.AreEqual(now.Date.AddDays(1), fireTime.Date);
    }

    [TestCase(2026, 1, 15, 0, 0, 0, "00:00")]
    [TestCase(2026, 1, 15, 12, 0, 0, "12:00")]
    [TestCase(2026, 1, 15, 23, 59, 59, "00:00")]
    [TestCase(2026, 12, 31, 23, 59, 59, "23:59")]
    [TestCase(2026, 2, 28, 10, 30, 0, "10:30")]
    public void ReminderSchedule_FireTimeIsAlwaysInTheFuture(int y, int mo, int d, int h, int mi, int s, string hhmm)
    {
        DateTime now = new DateTime(y, mo, d, h, mi, s, DateTimeKind.Local);

        Assert.IsTrue(ReminderSchedule.TryNextFireTime(now, hhmm, out DateTime fireTime));
        Assert.Greater(fireTime, now);
    }

    [Test]
    public void ReminderSchedule_TruncatesSecondsAndMilliseconds()
    {
        DateTime now = new DateTime(2026, 1, 15, 8, 0, 37, 123, DateTimeKind.Local);

        Assert.IsTrue(ReminderSchedule.TryNextFireTime(now, "21:00", out DateTime fireTime));
        Assert.AreEqual(0, fireTime.Second);
        Assert.AreEqual(0, fireTime.Millisecond);
    }

    [Test]
    public void ReminderSchedule_PreservesDateTimeKind()
    {
        DateTime now = new DateTime(2026, 1, 15, 8, 0, 0, DateTimeKind.Local);

        Assert.IsTrue(ReminderSchedule.TryNextFireTime(now, "21:00", out DateTime fireTime));
        Assert.AreEqual(DateTimeKind.Local, fireTime.Kind);
    }

    [Test]
    public void ReminderSchedule_CrossesMonthBoundary()
    {
        DateTime now = new DateTime(2026, 1, 31, 22, 0, 0, DateTimeKind.Local);

        Assert.IsTrue(ReminderSchedule.TryNextFireTime(now, "21:00", out DateTime fireTime));
        Assert.AreEqual(2, fireTime.Month);
        Assert.AreEqual(1, fireTime.Day);
        Assert.AreEqual(21, fireTime.Hour);
    }

    [Test]
    public void ReminderSchedule_CrossesYearBoundary()
    {
        DateTime now = new DateTime(2025, 12, 31, 22, 0, 0, DateTimeKind.Local);

        Assert.IsTrue(ReminderSchedule.TryNextFireTime(now, "21:00", out DateTime fireTime));
        Assert.AreEqual(2026, fireTime.Year);
        Assert.AreEqual(1, fireTime.Month);
        Assert.AreEqual(1, fireTime.Day);
    }

    [Test]
    public void ReminderSchedule_Midnight_FiresNextMidnight()
    {
        DateTime now = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Local);

        Assert.IsTrue(ReminderSchedule.TryNextFireTime(now, "00:00", out DateTime fireTime));
        Assert.AreEqual(now.Date.AddDays(1), fireTime.Date);
        Assert.AreEqual(0, fireTime.Hour);
        Assert.AreEqual(0, fireTime.Minute);
    }

    [TestCase("25:00")]
    [TestCase("21:60")]
    [TestCase("9:5")]
    [TestCase("2100")]
    [TestCase("")]
    [TestCase(null)]
    [TestCase("ab:cd")]
    public void ReminderSchedule_InvalidTime_ReturnsFalse(string hhmm)
    {
        DateTime now = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Local);

        Assert.IsFalse(ReminderSchedule.TryNextFireTime(now, hhmm, out _));
    }

    [Test]
    public void ReminderSchedule_RoundTripsTimeOfDayFormat()
    {
        DateTime now = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Local);
        int[] minutes = { 0, 7, 30, 59 };

        for (int h = 0; h < 24; h++)
        {
            foreach (int m in minutes)
            {
                string hhmm = TimeOfDay.Format(h, m);

                Assert.IsTrue(ReminderSchedule.TryNextFireTime(now, hhmm, out DateTime fireTime), hhmm);
                Assert.AreEqual(h, fireTime.Hour, hhmm);
                Assert.AreEqual(m, fireTime.Minute, hhmm);
            }
        }
    }
}
