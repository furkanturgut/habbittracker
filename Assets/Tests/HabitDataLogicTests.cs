using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Veri katmaninin EditMode testleri: sahne, kamera ya da PaintPixels
// gerekmiyor. Kaydin, gunluk reset'in, streak'in ve tablo ilerlemesinin
// dogrulugu buradan dogrulaniyor.
//
// Calistirmak icin: Window > General > Test Runner > EditMode > Run All.
public class HabitDataLogicTests
{
    // ---------------------------------------------------------------------
    // Yardimcilar
    // ---------------------------------------------------------------------

    // Testler icin bellek ici depo: PlayerPrefs'e dokunmadan ayni kayit
    // yolunun tamami (JsonUtility serialize + deserialize) calisiyor.
    private class MemoryStorage : IHabitStorage
    {
        public string Json;
        public int FlushCount;

        public string Load() => Json;
        public void Write(string json) => Json = json;
        public void Flush() => FlushCount++;
        public void Delete() { Json = null; }
    }

    private static HabitData MakeHabit(
        string id = "h1",
        int canvasSize = 16,
        params DayOfWeek[] frequency)
    {
        HabitData habit = new HabitData
        {
            id = id,
            title = "Test",
            habitType = HabitTypes.Build,
            canvasSize = canvasSize,
            activePaintingId = "p1",
            // Gecmis bir baslangic: planli gun aramasi baslangictan geriye tasmasin.
            startDate = HabitScheduling.ToKey(new DateTime(2020, 1, 1))
        };

        for (int i = 0; i < frequency.Length; i++)
        {
            habit.frequency.Add(frequency[i]);
        }

        return habit;
    }

    // ---------------------------------------------------------------------
    // Kayit gidis-donusu
    // ---------------------------------------------------------------------

    [Test]
    public void Save_RoundTrip_PreservesAllFields()
    {
        MemoryStorage storage = new MemoryStorage();
        HabitSaveSystem writer = new HabitSaveSystem(storage);

        writer.Data.lastLoginDate = "2026-09-01";

        HabitData habit = MakeHabit("habit-a", 32, DayOfWeek.Monday, DayOfWeek.Thursday);
        habit.title = "Su ic";
        habit.categoryGroup = "Saglik";
        habit.currentStreak = 7;
        habit.longestStreak = 12;
        habit.lastCompletedDate = "2026-08-31";
        habit.isCompletedToday = true;
        habit.hasEndDate = true;
        habit.endDate = "2026-12-31";
        habit.totalPixelsPainted = 5;
        habit.randomOpenedPixels.AddRange(new[] { 3, 9, 14 });
        habit.habitGallery.Add("finished-painting");
        writer.Data.habits.Add(habit);

        writer.SaveNow();

        // Ayni JSON'u yeni bir sisteme okutuyoruz.
        HabitSaveSystem reader = new HabitSaveSystem(storage);
        reader.Load();

        Assert.AreEqual(1, reader.Data.habits.Count);
        HabitData loaded = reader.Data.habits[0];

        Assert.AreEqual("2026-09-01", reader.Data.lastLoginDate);
        Assert.AreEqual("habit-a", loaded.id);
        Assert.AreEqual("Su ic", loaded.title);
        Assert.AreEqual("Saglik", loaded.categoryGroup);
        Assert.AreEqual(7, loaded.currentStreak);
        Assert.AreEqual(12, loaded.longestStreak);
        Assert.AreEqual("2026-08-31", loaded.lastCompletedDate);
        Assert.IsTrue(loaded.isCompletedToday);
        Assert.IsTrue(loaded.hasEndDate);
        Assert.AreEqual("2026-12-31", loaded.endDate);
        Assert.AreEqual(32, loaded.canvasSize);
        Assert.AreEqual(5, loaded.totalPixelsPainted);
        CollectionAssert.AreEqual(new[] { 3, 9, 14 }, loaded.randomOpenedPixels);
        CollectionAssert.AreEqual(new[] { "finished-painting" }, loaded.habitGallery);
    }

    // Iki yeni alan saveVersion ARTMADAN eklendi: eski bir kayitta anahtarlar
    // hic yok. Bu testin dogruladigi sey tam olarak o iddia -- anahtarsiz bir
    // JSON "gunde tek tik, bugun hic tiklanmamis" olarak okunuyor, yani
    // migrasyon gerekmiyor (bkz. PlayerSaveData yorumu).
    [Test]
    public void Load_OldSaveWithoutRepsKeys_ReadsAsSingleTick()
    {
        MemoryStorage storage = new MemoryStorage
        {
            Json = "{\"saveVersion\":1,\"habits\":[{\"id\":\"h1\",\"title\":\"Su ic\",\"canvasSize\":16}]}"
        };

        HabitSaveSystem reader = new HabitSaveSystem(storage);
        Assert.IsTrue(reader.Load());

        HabitData loaded = reader.Data.habits[0];
        Assert.AreEqual(1, HabitReps.Target(loaded), "Eski kayit gunde tek tik demek.");
        Assert.AreEqual(0, loaded.todayReps);
        Assert.IsFalse(HabitReps.IsMulti(loaded));
    }

    [Test]
    public void Save_RoundTrip_PreservesRepsFields()
    {
        MemoryStorage storage = new MemoryStorage();
        HabitSaveSystem writer = new HabitSaveSystem(storage);

        HabitData habit = MakeHabit("habit-reps", 16);
        habit.targetPerDay = 3;
        habit.todayReps = 2;
        writer.Data.habits.Add(habit);

        writer.SaveNow();

        HabitSaveSystem reader = new HabitSaveSystem(storage);
        reader.Load();

        HabitData loaded = reader.Data.habits[0];
        Assert.AreEqual(3, loaded.targetPerDay);
        Assert.AreEqual(2, loaded.todayReps, "Yarim kalan gun uygulama kapanip acilinca korunmali.");
    }

    // JsonUtility'nin enum LISTESINI dogru tasidigini ayrica dogruluyoruz:
    // List<DayOfWeek> desteklenmeseydi frequency sessizce bosalir ve butun
    // program mantigi "her gun planli" gibi davranirdi.
    [Test]
    public void Save_RoundTrip_PreservesDayOfWeekList()
    {
        MemoryStorage storage = new MemoryStorage();
        HabitSaveSystem writer = new HabitSaveSystem(storage);

        writer.Data.habits.Add(MakeHabit("h", 16, DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Sunday));
        writer.SaveNow();

        HabitSaveSystem reader = new HabitSaveSystem(storage);
        reader.Load();

        CollectionAssert.AreEqual(
            new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Sunday },
            reader.Data.habits[0].frequency);
    }

    [Test]
    public void Load_CorruptJson_FallsBackToEmptySave()
    {
        MemoryStorage storage = new MemoryStorage { Json = "{ bu gecerli json degil" };
        HabitSaveSystem system = new HabitSaveSystem(storage);

        // Bozuk kayit hata loglar ama uygulamayi durdurmamali.
        UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
        system.Load();
        UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;

        Assert.IsNotNull(system.Data);
        Assert.IsNotNull(system.Data.habits);
        Assert.AreEqual(0, system.Data.habits.Count);
    }

    [Test]
    public void FlushIfDirty_DoesNothingWhenClean()
    {
        MemoryStorage storage = new MemoryStorage();
        HabitSaveSystem system = new HabitSaveSystem(storage);

        system.FlushIfDirty(true);
        Assert.AreEqual(0, storage.FlushCount, "Kirli olmayan kayit diske yazilmamali.");

        system.MarkDirty();
        system.FlushIfDirty(true);
        Assert.AreEqual(1, storage.FlushCount);

        // Ayni bayrak ikinci kez yazmamali.
        system.FlushIfDirty(true);
        Assert.AreEqual(1, storage.FlushCount);
    }

    // ---------------------------------------------------------------------
    // Gunluk reset
    // ---------------------------------------------------------------------

    [Test]
    public void DailyReset_NewDay_ClearsTodayFlags()
    {
        DateTime yesterday = new DateTime(2026, 9, 1);
        DateTime today = new DateTime(2026, 9, 2);

        PlayerSaveData data = new PlayerSaveData { lastLoginDate = HabitScheduling.ToKey(yesterday) };
        HabitData habit = MakeHabit();
        habit.isCompletedToday = true;
        habit.currentStreak = 3;
        habit.lastCompletedDate = HabitScheduling.ToKey(yesterday);
        data.habits.Add(habit);

        bool changed = DailyResetService.ApplyDailyReset(data, today);

        Assert.IsTrue(changed);
        Assert.IsFalse(habit.isCompletedToday, "Yeni gunde tik sifirlanmali.");
        Assert.AreEqual(HabitScheduling.ToKey(today), data.lastLoginDate);
        Assert.AreEqual(3, habit.currentStreak, "Dun tamamlanmisti, seri korunmali.");
    }

    // Gunluk tur sayaci tikin kendisiyle AYNI dongude sifirlaniyor: biri
    // calisip oteki calismasa ekran "0 tik ama 1/2" gibi bir ara durum
    // gosterirdi (bkz. HabitReps degismeyen sozu).
    [Test]
    public void DailyReset_NewDay_ClearsTodayReps()
    {
        DateTime yesterday = new DateTime(2026, 9, 1);
        DateTime today = new DateTime(2026, 9, 2);

        PlayerSaveData data = new PlayerSaveData { lastLoginDate = HabitScheduling.ToKey(yesterday) };

        HabitData partial = MakeHabit("partial");
        partial.targetPerDay = 3;
        partial.todayReps = 2;

        HabitData finished = MakeHabit("finished");
        finished.targetPerDay = 2;
        finished.todayReps = 2;
        finished.isCompletedToday = true;

        data.habits.Add(partial);
        data.habits.Add(finished);

        Assert.IsTrue(DailyResetService.ApplyDailyReset(data, today));

        Assert.AreEqual(0, partial.todayReps, "Yarim kalan sayac yeni gunde sifirlanmali.");
        Assert.AreEqual(0, finished.todayReps, "Tamamlanmis gunun sayaci da sifirlanmali.");
        Assert.IsFalse(finished.isCompletedToday);
        Assert.AreEqual(3, partial.targetPerDay, "Hedef bir AYAR -- gun donusu ona dokunmamali.");
        Assert.AreEqual(2, finished.targetPerDay);
    }

    [Test]
    public void DailyReset_SameDay_IsNoOp()
    {
        DateTime today = new DateTime(2026, 9, 2);

        PlayerSaveData data = new PlayerSaveData { lastLoginDate = HabitScheduling.ToKey(today) };
        HabitData habit = MakeHabit();
        habit.isCompletedToday = true;
        data.habits.Add(habit);

        Assert.IsFalse(DailyResetService.ApplyDailyReset(data, today));
        Assert.IsTrue(habit.isCompletedToday, "Ayni gun icinde tik korunmali.");
    }

    [Test]
    public void DailyReset_MissedScheduledDay_BreaksStreak()
    {
        // 1 Eylul 2026 Sali. Habit her gun planli.
        DateTime lastLogin = new DateTime(2026, 9, 1);
        DateTime today = new DateTime(2026, 9, 3);

        PlayerSaveData data = new PlayerSaveData { lastLoginDate = HabitScheduling.ToKey(lastLogin) };
        HabitData habit = MakeHabit();
        habit.currentStreak = 5;
        // En son 1 Eylul'de tamamlanmis; 2 Eylul planliydi ve kacirildi.
        habit.lastCompletedDate = HabitScheduling.ToKey(lastLogin);
        data.habits.Add(habit);

        DailyResetService.ApplyDailyReset(data, today);

        Assert.AreEqual(0, habit.currentStreak, "Kacirilan planli gun seriyi kirmali.");
    }

    [Test]
    public void DailyReset_UnscheduledGap_KeepsStreak()
    {
        // Habit yalnizca Pazartesi. 31 Agustos 2026 Pazartesi, 3 Eylul Persembe.
        DateTime lastCompleted = new DateTime(2026, 8, 31);
        DateTime today = new DateTime(2026, 9, 3);

        Assert.AreEqual(DayOfWeek.Monday, lastCompleted.DayOfWeek, "Test verisi varsayimi");

        PlayerSaveData data = new PlayerSaveData { lastLoginDate = HabitScheduling.ToKey(lastCompleted) };
        HabitData habit = MakeHabit("h", 16, DayOfWeek.Monday);
        habit.currentStreak = 4;
        habit.lastCompletedDate = HabitScheduling.ToKey(lastCompleted);
        data.habits.Add(habit);

        DailyResetService.ApplyDailyReset(data, today);

        Assert.AreEqual(4, habit.currentStreak,
            "Araya giren gunler planli degilse seri kirilmamali.");
    }

    [Test]
    public void DailyReset_ClockMovedBackwards_DoesNotBreakStreak()
    {
        DateTime lastLogin = new DateTime(2026, 9, 10);
        DateTime tampered = new DateTime(2026, 9, 1);

        PlayerSaveData data = new PlayerSaveData { lastLoginDate = HabitScheduling.ToKey(lastLogin) };
        HabitData habit = MakeHabit();
        habit.currentStreak = 9;
        habit.lastCompletedDate = HabitScheduling.ToKey(lastLogin);
        data.habits.Add(habit);

        DailyResetService.ApplyDailyReset(data, tampered);

        Assert.AreEqual(9, habit.currentStreak,
            "Saat geri alindiginda seri kirilmamali (odul de verilmemeli).");
        Assert.AreEqual(HabitScheduling.ToKey(tampered), data.lastLoginDate);
    }

    // ---------------------------------------------------------------------
    // Streak
    // ---------------------------------------------------------------------

    [Test]
    public void AdvanceStreak_ConsecutiveDueDays_Increments()
    {
        HabitData habit = MakeHabit();
        DateTime day1 = new DateTime(2026, 9, 1);
        DateTime day2 = new DateTime(2026, 9, 2);

        DailyResetService.AdvanceStreak(habit, day1);
        Assert.AreEqual(1, habit.currentStreak);

        DailyResetService.AdvanceStreak(habit, day2);
        Assert.AreEqual(2, habit.currentStreak);
        Assert.AreEqual(2, habit.longestStreak);
    }

    [Test]
    public void AdvanceStreak_AfterGap_RestartsAtOne()
    {
        HabitData habit = MakeHabit();
        habit.currentStreak = 5;
        habit.longestStreak = 5;
        habit.lastCompletedDate = HabitScheduling.ToKey(new DateTime(2026, 9, 1));

        // 4 Eylul: 3 Eylul planliydi ve tamamlanmamis.
        DailyResetService.AdvanceStreak(habit, new DateTime(2026, 9, 4));

        Assert.AreEqual(1, habit.currentStreak, "Kopan seri 1'den baslamali.");
        Assert.AreEqual(5, habit.longestStreak, "Rekor korunmali.");
    }

    [Test]
    public void RollBackStreak_KeepsLongestStreak()
    {
        HabitData habit = MakeHabit();
        DateTime today = new DateTime(2026, 9, 2);

        habit.lastCompletedDate = HabitScheduling.ToKey(new DateTime(2026, 9, 1));
        DailyResetService.AdvanceStreak(habit, today);
        Assert.AreEqual(1, habit.currentStreak);
        Assert.AreEqual(1, habit.longestStreak);

        DailyResetService.RollBackStreak(habit, today);

        Assert.AreEqual(0, habit.currentStreak);
        Assert.AreEqual(1, habit.longestStreak, "Rekor geri almayla dusmemeli.");
    }

    // ---------------------------------------------------------------------
    // Program (frequency / tarih araligi)
    // ---------------------------------------------------------------------

    [Test]
    public void IsScheduledOn_EmptyFrequency_MeansEveryDay()
    {
        HabitData habit = MakeHabit();
        for (int i = 0; i < 7; i++)
        {
            Assert.IsTrue(HabitScheduling.IsScheduledOn(habit, new DateTime(2026, 9, 1).AddDays(i)));
        }
    }

    [Test]
    public void IsActiveOn_RespectsStartAndEndDates()
    {
        HabitData habit = MakeHabit();
        habit.startDate = "2026-09-05";
        habit.hasEndDate = true;
        habit.endDate = "2026-09-10";

        Assert.IsFalse(HabitScheduling.IsActiveOn(habit, new DateTime(2026, 9, 4)), "Baslangictan once");
        Assert.IsTrue(HabitScheduling.IsActiveOn(habit, new DateTime(2026, 9, 5)), "Baslangic gunu dahil");
        Assert.IsTrue(HabitScheduling.IsActiveOn(habit, new DateTime(2026, 9, 10)), "Bitis gunu dahil");
        Assert.IsFalse(HabitScheduling.IsActiveOn(habit, new DateTime(2026, 9, 11)), "Bitisten sonra");
    }

    // ---------------------------------------------------------------------
    // Tablo ilerlemesi
    // ---------------------------------------------------------------------

    [Test]
    public void Progress_Sequential_OpensInOrderAndCompletes()
    {
        HabitData habit = MakeHabit(canvasSize: 16);
        List<int> scratch = new List<int>();

        for (int i = 0; i < 16; i++)
        {
            int index = PaintingProgress.OpenNextPiece(habit, true, scratch);
            Assert.AreEqual(i, index, "Sirali modda parcalar 0'dan itibaren acilmali.");
        }

        Assert.AreEqual(16, habit.totalPixelsPainted);
        Assert.IsTrue(PaintingProgress.IsComplete(habit, true));
        Assert.AreEqual(-1, PaintingProgress.OpenNextPiece(habit, true, scratch),
            "Dolu tabloda acilacak parca kalmamali.");
    }

    [Test]
    public void Progress_Random_OpensEveryIndexExactlyOnce()
    {
        HabitData habit = MakeHabit(canvasSize: 32);
        List<int> scratch = new List<int>();
        HashSet<int> seen = new HashSet<int>();

        for (int i = 0; i < 32; i++)
        {
            int index = PaintingProgress.OpenNextPiece(habit, false, scratch);
            Assert.GreaterOrEqual(index, 0);
            Assert.Less(index, 32, "Indeks tuval sinirlari icinde olmali.");
            Assert.IsTrue(seen.Add(index), "Ayni parca iki kez acilmamali.");
        }

        Assert.AreEqual(32, habit.randomOpenedPixels.Count);
        Assert.IsTrue(PaintingProgress.IsComplete(habit, false));
        Assert.AreEqual(-1, PaintingProgress.OpenNextPiece(habit, false, scratch));
    }

    [Test]
    public void Progress_UndoLastPiece_ReversesBothModes()
    {
        List<int> scratch = new List<int>();

        HabitData sequential = MakeHabit(canvasSize: 16);
        PaintingProgress.OpenNextPiece(sequential, true, scratch);
        PaintingProgress.OpenNextPiece(sequential, true, scratch);
        Assert.AreEqual(1, PaintingProgress.UndoLastPiece(sequential, true));
        Assert.AreEqual(1, sequential.totalPixelsPainted);

        HabitData random = MakeHabit(canvasSize: 16);
        int first = PaintingProgress.OpenNextPiece(random, false, scratch);
        int second = PaintingProgress.OpenNextPiece(random, false, scratch);
        Assert.AreEqual(second, PaintingProgress.UndoLastPiece(random, false));
        CollectionAssert.AreEqual(new[] { first }, random.randomOpenedPixels);
    }

    [Test]
    public void Progress_ResetClearsBothRepresentations()
    {
        HabitData habit = MakeHabit(canvasSize: 16);
        habit.totalPixelsPainted = 9;
        habit.randomOpenedPixels.AddRange(new[] { 1, 2, 3 });

        PaintingProgress.ResetProgress(habit);

        Assert.AreEqual(0, habit.totalPixelsPainted);
        Assert.AreEqual(0, habit.randomOpenedPixels.Count);
    }

    // ---------------------------------------------------------------------
    // Tablo havuzu: galeri ve yeni tablo secimi
    // ---------------------------------------------------------------------

    private static PaintingLibrary MakeLibrary(int count, int pieceCount)
    {
        PaintingLibrary library = ScriptableObject.CreateInstance<PaintingLibrary>();
        List<PaintingDefinition> definitions = new List<PaintingDefinition>();

        for (int i = 0; i < count; i++)
        {
            PaintingDefinition definition = ScriptableObject.CreateInstance<PaintingDefinition>();
            definition.id = "p" + i;
            definition.resolution = (PaintPixels.OverlayResolution)pieceCount;
            definition.revealOrder = PaintPixels.RevealOrder.Random;
            definitions.Add(definition);
        }

        // paintings alani private; testte SerializedObject yerine dogrudan
        // yansima kullanmak yerine kutuphaneyi disaridan doldurmanin yolu yok,
        // bu yuzden JsonUtility uzerinden yaziyoruz (Editor API'sine bagimli
        // kalmadan calisir).
        SetPaintings(library, definitions);
        library.BuildIndex();
        return library;
    }

    private static void SetPaintings(PaintingLibrary library, List<PaintingDefinition> definitions)
    {
        System.Reflection.FieldInfo field = typeof(PaintingLibrary).GetField(
            "paintings",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field.SetValue(library, definitions);
    }

    [Test]
    public void Library_PickNext_SkipsPaintingsAlreadyInGallery()
    {
        PaintingLibrary library = MakeLibrary(4, 16);
        List<string> gallery = new List<string> { "p0", "p1", "p2" };

        for (int attempt = 0; attempt < 20; attempt++)
        {
            PaintingDefinition picked = library.PickNext(16, gallery);
            Assert.IsNotNull(picked);
            Assert.AreEqual("p3", picked.id, "Galeride olmayan tek tablo secilmeli.");
        }
    }

    [Test]
    public void Library_PickNext_RecyclesWhenPoolExhausted()
    {
        PaintingLibrary library = MakeLibrary(3, 16);
        List<string> gallery = new List<string> { "p0", "p1", "p2" };

        PaintingDefinition picked = library.PickNext(16, gallery);

        Assert.IsNotNull(picked, "Havuz tukendiginde null donmemeli, bastan kullanilmali.");
        Assert.IsTrue(gallery.Contains(picked.id));
    }

    [Test]
    public void Library_TryGet_ResolvesById()
    {
        PaintingLibrary library = MakeLibrary(3, 32);

        Assert.IsTrue(library.TryGet("p1", out PaintingDefinition found));
        Assert.AreEqual("p1", found.id);
        Assert.AreEqual(32, found.PieceCount);

        Assert.IsFalse(library.TryGet("yok", out _));
        Assert.IsFalse(library.TryGet(null, out _));
    }

    // ---------------------------------------------------------------------
    // Mood (ressamin gunluk "hepsi tamamlandi mi" kontrolu)
    // ---------------------------------------------------------------------

    [Test]
    public void Mood_AllDueCompleted_ReturnsTrue()
    {
        DateTime today = new DateTime(2026, 9, 3);
        PlayerSaveData data = new PlayerSaveData();

        HabitData a = MakeHabit("a");
        a.isCompletedToday = true;
        HabitData b = MakeHabit("b");
        b.isCompletedToday = true;
        data.habits.Add(a);
        data.habits.Add(b);

        Assert.IsTrue(MoodService.HasAnyDueHabit(data, today));
        Assert.IsTrue(MoodService.AreAllDueHabitsCompleted(data, today));
    }

    [Test]
    public void Mood_OneDueMissing_ReturnsFalse()
    {
        DateTime today = new DateTime(2026, 9, 3);
        PlayerSaveData data = new PlayerSaveData();

        HabitData a = MakeHabit("a");
        a.isCompletedToday = true;
        HabitData b = MakeHabit("b");
        b.isCompletedToday = false;
        data.habits.Add(a);
        data.habits.Add(b);

        Assert.IsFalse(MoodService.AreAllDueHabitsCompleted(data, today));
    }

    [Test]
    public void Mood_UnscheduledHabit_DoesNotBlockCompletion()
    {
        // 6 Eylul 2026 Pazar. Habit yalnizca Pazartesi planli -> bugun planli degil.
        DateTime sunday = new DateTime(2026, 9, 6);
        Assert.AreEqual(DayOfWeek.Sunday, sunday.DayOfWeek, "Test verisi varsayimi");

        PlayerSaveData data = new PlayerSaveData();
        HabitData mondayOnly = MakeHabit("a", 16, DayOfWeek.Monday);
        mondayOnly.isCompletedToday = false;
        HabitData everyDay = MakeHabit("b");
        everyDay.isCompletedToday = true;
        data.habits.Add(mondayOnly);
        data.habits.Add(everyDay);

        Assert.IsTrue(MoodService.AreAllDueHabitsCompleted(data, sunday),
            "Pazartesiye planli aliskanlik pazar gunu sayilmamali.");
    }

    [Test]
    public void Mood_NoDueHabits_HasAnyDueHabitIsFalse()
    {
        // Izin gunu: hicbir aliskanlik bugun planli degil -> ne odul ne ceza.
        DateTime sunday = new DateTime(2026, 9, 6);
        PlayerSaveData data = new PlayerSaveData();
        data.habits.Add(MakeHabit("a", 16, DayOfWeek.Monday));

        Assert.IsFalse(MoodService.HasAnyDueHabit(data, sunday));
        Assert.IsFalse(MoodService.AreAllDueHabitsCompleted(data, sunday),
            "Yapilacak is yoktu ile hepsini yapti ayni sey degil.");
    }

    [Test]
    public void Mood_SettleClosedDays_IncompleteLastDay_ReturnsOne()
    {
        DateTime lastLogin = new DateTime(2026, 9, 1);
        DateTime now = new DateTime(2026, 9, 2);

        PlayerSaveData data = new PlayerSaveData { lastLoginDate = HabitScheduling.ToKey(lastLogin) };
        HabitData habit = MakeHabit();
        habit.isCompletedToday = false;
        data.habits.Add(habit);

        int penalties = MoodService.SettleClosedDays(data, now);

        Assert.AreEqual(1, penalties);
        Assert.AreEqual(HabitScheduling.ToKey(now.AddDays(-1)), data.lastMoodSettledDate);
    }

    [Test]
    public void Mood_SettleClosedDays_ThreeDayGap_ReturnsThree()
    {
        // Uygulama 1 Eylul'den sonra hic acilmamis, 5 Eylul'de aciliyor.
        // Kapanan gunler: 1 (tikler kayitta, tamamlanmamis), 2, 3, 4 -- hepsi
        // her gun planli bir aliskanlikla -> 4 ceza degil, senaryo 3 gunluk
        // BOSLUK (2, 3, 4) arti son giris gunu (1) = 4 gun kapanmis olurdu.
        // Kullanicinin sordugu "3 gun gelmedi" senaryosunu tam karsilamak icin
        // son giris gunu ZATEN tamamlanmis kabul ediliyor, sadece aradaki 3
        // gun (2, 3, 4) ceza aliyor.
        DateTime lastLogin = new DateTime(2026, 9, 1);
        DateTime now = new DateTime(2026, 9, 5);

        PlayerSaveData data = new PlayerSaveData { lastLoginDate = HabitScheduling.ToKey(lastLogin) };
        HabitData habit = MakeHabit();
        habit.isCompletedToday = true; // son giris gunu tamamlanmisti
        data.habits.Add(habit);

        int penalties = MoodService.SettleClosedDays(data, now);

        Assert.AreEqual(3, penalties, "2, 3 ve 4 Eylul hic acilmadi ve planli bir sey vardi.");
    }

    [Test]
    public void Mood_SettleClosedDays_GapWithUnscheduledDays_SkipsThem()
    {
        // Habit yalnizca Pazartesi planli. 31 Agustos 2026 Pazartesi'de son
        // giris, 3 Eylul Persembe'de acilis. Aradaki gunler (1, 2 Eylul) hic
        // planli degil -> ceza yok. Son giris gunu de tamamlanmisti.
        DateTime lastLogin = new DateTime(2026, 8, 31);
        Assert.AreEqual(DayOfWeek.Monday, lastLogin.DayOfWeek, "Test verisi varsayimi");
        DateTime now = new DateTime(2026, 9, 3);

        PlayerSaveData data = new PlayerSaveData { lastLoginDate = HabitScheduling.ToKey(lastLogin) };
        HabitData habit = MakeHabit("h", 16, DayOfWeek.Monday);
        habit.isCompletedToday = true;
        data.habits.Add(habit);

        int penalties = MoodService.SettleClosedDays(data, now);

        Assert.AreEqual(0, penalties, "Araya giren gunler planli degilse ceza olmamali.");
    }

    [Test]
    public void Mood_SettleClosedDays_AlreadySettledLastDay_SkipsThatDay()
    {
        // Gun icinde hepsi tiklenip odul verilmis (lastMoodSettledDate bugune
        // damgali). Gun sonu kontrolu bu gunu bir daha cezalandirmamali.
        DateTime lastLogin = new DateTime(2026, 9, 1);
        DateTime now = new DateTime(2026, 9, 2);

        PlayerSaveData data = new PlayerSaveData
        {
            lastLoginDate = HabitScheduling.ToKey(lastLogin),
            lastMoodSettledDate = HabitScheduling.ToKey(lastLogin)
        };
        HabitData habit = MakeHabit();
        habit.isCompletedToday = false; // onemli degil, damga zaten islenmis sayiyor
        data.habits.Add(habit);

        int penalties = MoodService.SettleClosedDays(data, now);

        Assert.AreEqual(0, penalties, "Zaten odullendirilmis gun tekrar cezalandirilmamali.");
    }

    [Test]
    public void Mood_SettleClosedDays_ClockMovedBackwards_ReturnsZero()
    {
        DateTime lastLogin = new DateTime(2026, 9, 10);
        DateTime tampered = new DateTime(2026, 9, 1);

        PlayerSaveData data = new PlayerSaveData { lastLoginDate = HabitScheduling.ToKey(lastLogin) };
        data.habits.Add(MakeHabit());

        int penalties = MoodService.SettleClosedDays(data, tampered);

        Assert.AreEqual(0, penalties, "Saat geri alindiginda ceza verilmemeli.");
        Assert.IsNull(data.lastMoodSettledDate, "Damga ilerletilmemeli.");
    }

    [Test]
    public void Mood_SettleClosedDays_FirstRun_ReturnsZero()
    {
        PlayerSaveData data = new PlayerSaveData { lastLoginDate = null };
        data.habits.Add(MakeHabit());

        int penalties = MoodService.SettleClosedDays(data, new DateTime(2026, 9, 2));

        Assert.AreEqual(0, penalties, "Ilk acilista kiyaslanacak gecmis yok.");
    }
}
