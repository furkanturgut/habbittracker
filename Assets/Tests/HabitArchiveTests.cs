using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// Aliskanlik arsivleme ozelliginin EditMode testleri: HabitScheduling.IsDueOn'un
// arsiv kapisi, HabitArchive.Archive/Unarchive'in seri cipalamasi,
// DailyResetService.BreakMissedStreaks'in arsivi atlamasi, MoodService ve
// MissedPieceService'in arsivdeki aliskanligi cezalandirmamasi ve
// HabitFilters.Matches'in onu hic bir filtreye sokmamasi. RotusTests.cs/
// MissedPieceTests.cs ile AYNI fixture kalibi (paylasilan yardimci yok,
// bilincli -- 500 satir siniri icin dosyalar ayri).
public class HabitArchiveTests
{
    private class MemoryStorage : IHabitStorage
    {
        public string Json;

        public string Load() => Json;
        public void Write(string json) => Json = json;
        public void Flush() { }
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
            activePaintingId = "p0",
            // Gecmis bir baslangic: planli gun aramasi baslangictan geriye tasmasin.
            startDate = HabitScheduling.ToKey(new DateTime(2020, 1, 1))
        };

        for (int i = 0; i < frequency.Length; i++)
        {
            habit.frequency.Add(frequency[i]);
        }

        return habit;
    }

    private static PaintingLibrary MakeLibrary(int count, int pieceCount, bool sequential)
    {
        PaintingLibrary library = ScriptableObject.CreateInstance<PaintingLibrary>();
        List<PaintingDefinition> definitions = new List<PaintingDefinition>();

        for (int i = 0; i < count; i++)
        {
            PaintingDefinition definition = ScriptableObject.CreateInstance<PaintingDefinition>();
            definition.id = "p" + i;
            definition.resolution = (PaintPixels.OverlayResolution)pieceCount;
            definition.revealOrder = sequential
                ? PaintPixels.RevealOrder.Sequential
                : PaintPixels.RevealOrder.Random;
            definitions.Add(definition);
        }

        FieldInfo field = typeof(PaintingLibrary).GetField(
            "paintings",
            BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(library, definitions);

        library.BuildIndex();
        return library;
    }

    // -----------------------------------------------------------------------
    // HabitScheduling.IsDueOn -- arsiv kapisi
    // -----------------------------------------------------------------------

    [Test]
    public void IsDueOn_ArchivedHabit_ReturnsFalseEvenWhenScheduledAndActive()
    {
        HabitData habit = MakeHabit();
        habit.isArchived = true;

        Assert.IsFalse(HabitScheduling.IsDueOn(habit, new DateTime(2026, 9, 1)));
    }

    [Test]
    public void IsActiveOn_ArchivedHabit_StillReturnsTrue()
    {
        // isArchived IsActiveOn'a SIZMAZ -- o saf bir tarih sorusu kalmali.
        HabitData habit = MakeHabit();
        habit.isArchived = true;

        Assert.IsTrue(HabitScheduling.IsActiveOn(habit, new DateTime(2026, 9, 1)));
    }

    [Test]
    public void TryGetPreviousDueDay_ArchivedHabit_StillWalksBack()
    {
        // TryGetPreviousDueDay BILEREK arsivden habersiz -- HabitArchive.
        // Unarchive bunun uzerine kuruluyor.
        HabitData habit = MakeHabit();
        habit.isArchived = true;

        bool found = HabitScheduling.TryGetPreviousDueDay(habit, new DateTime(2026, 9, 5), out DateTime previous);

        Assert.IsTrue(found);
        Assert.AreEqual(new DateTime(2026, 9, 4), previous.Date);
    }

    // -----------------------------------------------------------------------
    // HabitArchive.Unarchive -- seri cipalama
    // -----------------------------------------------------------------------

    [Test]
    public void Unarchive_MidStreakAfterThirtyDays_MovesStampToPreviousDueDay()
    {
        HabitData habit = MakeHabit();
        habit.isArchived = true;
        habit.currentStreak = 10;
        habit.lastCompletedDate = HabitScheduling.ToKey(new DateTime(2026, 9, 1));

        DateTime unarchiveDay = new DateTime(2026, 10, 1); // 30 gun sonra

        HabitArchive.Unarchive(habit, unarchiveDay);

        Assert.IsFalse(habit.isArchived);
        Assert.AreEqual(10, habit.currentStreak, "Unarchive kendisi seriyi degistirmemeli.");
        Assert.AreEqual(HabitScheduling.ToKey(new DateTime(2026, 9, 30)), habit.lastCompletedDate);
    }

    [Test]
    public void Unarchive_TickedToday_DoesNotMoveStampBackwards()
    {
        // Ayni gun arsivlenip geri alinan bir aliskanlik: damga BUGUNE ait,
        // dune cekilmemeli.
        HabitData habit = MakeHabit();
        habit.currentStreak = 6;
        DateTime today = new DateTime(2026, 9, 11);
        habit.lastCompletedDate = HabitScheduling.ToKey(today);
        habit.isArchived = true;

        HabitArchive.Unarchive(habit, today);

        Assert.AreEqual(HabitScheduling.ToKey(today), habit.lastCompletedDate);
    }

    [Test]
    public void Unarchive_ZeroStreak_LeavesStampNull()
    {
        HabitData habit = MakeHabit();
        habit.isArchived = true;
        habit.currentStreak = 0;
        habit.lastCompletedDate = null;

        HabitArchive.Unarchive(habit, new DateTime(2026, 9, 11));

        Assert.IsNull(habit.lastCompletedDate, "Olu bir seri diriltilmemeli.");
    }

    [Test]
    public void Unarchive_WeeklyHabit_AnchorsToMostRecentMonday()
    {
        // Habit yalnizca Pazartesi planli. 31 Agustos 2026 Pazartesi.
        DateTime lastMonday = new DateTime(2026, 8, 31);
        Assert.AreEqual(DayOfWeek.Monday, lastMonday.DayOfWeek, "Test verisi varsayimi");

        HabitData habit = MakeHabit(frequency: new[] { DayOfWeek.Monday });
        habit.isArchived = true;
        habit.currentStreak = 4;
        habit.lastCompletedDate = HabitScheduling.ToKey(lastMonday);

        // 3 hafta sonra, bir Persembe gunu geri alinir.
        DateTime unarchiveDay = new DateTime(2026, 9, 24);
        Assert.AreEqual(DayOfWeek.Thursday, unarchiveDay.DayOfWeek, "Test verisi varsayimi");

        HabitArchive.Unarchive(habit, unarchiveDay);

        // En yakin planli (Pazartesi) gun 21 Eylul.
        Assert.AreEqual(HabitScheduling.ToKey(new DateTime(2026, 9, 21)), habit.lastCompletedDate);
    }

    [Test]
    public void Unarchive_NoScheduledDayInThePast_WritesNothing()
    {
        // startDate BUGUN: gecmiste hic planli gun yok.
        HabitData habit = MakeHabit();
        DateTime now = new DateTime(2026, 9, 11);
        habit.startDate = HabitScheduling.ToKey(now);
        habit.isArchived = true;
        habit.currentStreak = 1;
        habit.lastCompletedDate = null;

        HabitArchive.Unarchive(habit, now);

        Assert.IsNull(habit.lastCompletedDate, "Ustune yazilacak gercek bir gecmis yok.");
    }

    [Test]
    public void Unarchive_CalledOnNonArchivedHabit_StillReanchorsIdempotently()
    {
        HabitData habit = MakeHabit();
        habit.isArchived = false;
        habit.currentStreak = 3;
        habit.lastCompletedDate = HabitScheduling.ToKey(new DateTime(2026, 9, 1));

        HabitArchive.Unarchive(habit, new DateTime(2026, 9, 11));

        Assert.IsFalse(habit.isArchived);
        Assert.AreEqual(HabitScheduling.ToKey(new DateTime(2026, 9, 10)), habit.lastCompletedDate);
    }

    // -----------------------------------------------------------------------
    // DailyResetService.BreakMissedStreaks -- arsivi atlar
    // -----------------------------------------------------------------------

    [Test]
    public void DailyReset_ArchivedHabitMissesScheduledDay_StreakSurvives()
    {
        // HabitDataLogicTests.DailyReset_MissedScheduledDay_BreaksStreak ile
        // AYNI senaryo, tek fark: aliskanlik arsivde. Negatif kontrol o testte.
        DateTime lastLogin = new DateTime(2026, 9, 1);
        DateTime today = new DateTime(2026, 9, 3);

        PlayerSaveData data = new PlayerSaveData { lastLoginDate = HabitScheduling.ToKey(lastLogin) };
        HabitData habit = MakeHabit();
        habit.isArchived = true;
        habit.currentStreak = 5;
        habit.lastCompletedDate = HabitScheduling.ToKey(lastLogin);
        data.habits.Add(habit);

        DailyResetService.ApplyDailyReset(data, today);

        Assert.AreEqual(5, habit.currentStreak, "Arsivdeki aliskanligin serisi kirilmamali.");
    }

    // -----------------------------------------------------------------------
    // MoodService -- arsivdeki aliskanlik ceza/odule girmez
    // -----------------------------------------------------------------------

    [Test]
    public void SettleClosedDays_ArchivedHabitIsTheOnlyOne_NoPenalty()
    {
        PlayerSaveData data = new PlayerSaveData { lastLoginDate = HabitScheduling.ToKey(new DateTime(2026, 9, 1)) };
        HabitData habit = MakeHabit();
        habit.isArchived = true;
        data.habits.Add(habit);

        int penalties = MoodService.SettleClosedDays(data, new DateTime(2026, 9, 6));

        Assert.AreEqual(0, penalties, "Arsivdeki tek aliskanlik izin gunu gibi davranmali.");
    }

    [Test]
    public void SettleClosedDays_ArchivedPlusCompletedActiveHabit_NoPenalty()
    {
        DateTime lastLogin = new DateTime(2026, 9, 1);
        PlayerSaveData data = new PlayerSaveData { lastLoginDate = HabitScheduling.ToKey(lastLogin) };

        HabitData archived = MakeHabit("h1");
        archived.isArchived = true;
        data.habits.Add(archived);

        HabitData active = MakeHabit("h2");
        active.isCompletedToday = true;
        data.habits.Add(active);

        int penalties = MoodService.SettleClosedDays(data, new DateTime(2026, 9, 2));

        Assert.AreEqual(0, penalties, "Aktif aliskanlik tiklanmis, arsivdeki sayilmamali.");
    }

    [Test]
    public void HasAnyDueHabit_OnlyArchivedHabit_ReturnsFalse()
    {
        PlayerSaveData data = new PlayerSaveData();
        HabitData habit = MakeHabit();
        habit.isArchived = true;
        data.habits.Add(habit);

        Assert.IsFalse(MoodService.HasAnyDueHabit(data, new DateTime(2026, 9, 1)));
    }

    // -----------------------------------------------------------------------
    // MissedPieceService -- arsivdeki aliskanlik parca yakmaz
    // -----------------------------------------------------------------------

    [Test]
    public void CountMissedDueDays_ArchivedHabit_ReturnsZero()
    {
        HabitData habit = MakeHabit();
        habit.isArchived = true;

        int missed = MissedPieceService.CountMissedDueDays(
            habit, new DateTime(2026, 9, 1), new DateTime(2026, 9, 10));

        Assert.AreEqual(0, missed);
    }

    [Test]
    public void SettleAll_ArchivedHabit_BurnsNothing()
    {
        PaintingLibrary library = MakeLibrary(1, 8, true);
        PlayerSaveData data = new PlayerSaveData { lastLoginDate = HabitScheduling.ToKey(new DateTime(2026, 9, 1)) };

        HabitData habit = MakeHabit(canvasSize: 8);
        habit.isArchived = true;
        data.habits.Add(habit);

        int burned = MissedPieceService.SettleAll(
            library, data, new DateTime(2026, 9, 10), new List<int>(), new List<HabitData>());

        Assert.AreEqual(0, burned);
        Assert.AreEqual(0, habit.missedPixels.Count);
    }

    // -----------------------------------------------------------------------
    // HabitFilters -- arsivdeki aliskanlik hicbir filtreye girmez
    // -----------------------------------------------------------------------

    [Test]
    public void Matches_ArchivedHabit_FalseForEveryKindIncludingAll()
    {
        HabitData habit = MakeHabit();
        habit.isArchived = true;
        habit.categoryGroup = HabitCategories.Health;

        Assert.IsFalse(HabitFilters.Matches(HabitFilters.HabitFilter.All, habit));
        Assert.IsFalse(HabitFilters.Matches(HabitFilters.HabitFilter.Build, habit));
        Assert.IsFalse(HabitFilters.Matches(HabitFilters.HabitFilter.Quit, habit));

        bool parsed = HabitFilters.TryParse(HabitCategories.Health, out HabitFilters.HabitFilter categoryFilter);
        Assert.IsTrue(parsed);
        Assert.IsFalse(HabitFilters.Matches(categoryFilter, habit));
    }

    [Test]
    public void Count_ExcludesArchivedHabits()
    {
        List<HabitData> habits = new List<HabitData>
        {
            MakeHabit("h1"),
            MakeHabit("h2")
        };
        habits[1].isArchived = true;

        int count = HabitFilters.Count(HabitFilters.HabitFilter.All, habits);

        Assert.AreEqual(1, count);
    }

    // -----------------------------------------------------------------------
    // Tam gun boru hatti: arsivle -> 30 gunluk bosluk -> geri al -> tikle
    // -----------------------------------------------------------------------

    [Test]
    public void ArchiveThenUnarchive_AcrossThirtyDays_FirstTickContinuesStreakWithNoBurnsOrPenalties()
    {
        PaintingLibrary library = MakeLibrary(1, 8, true);
        PlayerSaveData data = new PlayerSaveData { lastLoginDate = HabitScheduling.ToKey(new DateTime(2026, 9, 1)) };

        HabitData habit = MakeHabit(canvasSize: 8);
        habit.currentStreak = 10;
        habit.lastCompletedDate = HabitScheduling.ToKey(new DateTime(2026, 9, 1));
        habit.isArchived = true; // arsivlenirken aninda yapildigi varsayilan adim
        data.habits.Add(habit);

        DateTime unarchiveDay = new DateTime(2026, 10, 1); // 30 gun sonra

        // 30 gunluk bosluk, arsivdeyken tek bir SettleAll + ApplyDailyReset
        // cagrisiyla "kapatiliyor" -- HabbitManager.Load/RefreshDayIfNeeded'in
        // gercek sirasi.
        int penalties = MoodService.SettleClosedDays(data, unarchiveDay);
        int burned = MissedPieceService.SettleAll(
            library, data, unarchiveDay, new List<int>(), new List<HabitData>());
        DailyResetService.ApplyDailyReset(data, unarchiveDay);

        Assert.AreEqual(0, penalties, "Eksi gun sayilmamali.");
        Assert.AreEqual(0, burned, "Tablodan parca yanmamali.");
        Assert.AreEqual(10, habit.currentStreak, "Seri bozulmamali.");

        // Simdi geri al ve tikle.
        HabitArchive.Unarchive(habit, unarchiveDay);
        DailyResetService.AdvanceStreak(habit, unarchiveDay);

        Assert.AreEqual(11, habit.currentStreak, "Geri alindiktan sonraki ilk tik seriyi ilerletmeli.");
        Assert.AreEqual(0, habit.missedPixels.Count);
    }

    // -----------------------------------------------------------------------
    // HabitSaveSystem -- gocmeli kayit
    // -----------------------------------------------------------------------

    [Test]
    public void Load_OldSaveWithoutArchiveField_DefaultsToFalseWithoutBumpingSaveVersion()
    {
        // "isArchived" anahtari hic yok -- ozellik eklenmeden once yazilmis
        // bir kayit.
        string oldJson = "{\"saveVersion\":1,\"lastLoginDate\":\"2024-01-01\"," +
            "\"habits\":[{\"id\":\"h1\",\"canvasSize\":16}]," +
            "\"painterMoodPoint\":50,\"lastMoodSettledDate\":null}";

        MemoryStorage storage = new MemoryStorage { Json = oldJson };
        HabitSaveSystem saveSystem = new HabitSaveSystem(storage);

        saveSystem.Load();

        Assert.AreEqual(1, saveSystem.Data.saveVersion);
        Assert.IsFalse(saveSystem.Data.habits[0].isArchived);
    }

    [Test]
    public void Save_RoundTrip_PreservesArchiveFlag()
    {
        MemoryStorage storage = new MemoryStorage();
        HabitSaveSystem writer = new HabitSaveSystem(storage);

        HabitData habit = MakeHabit("h1");
        habit.isArchived = true;
        writer.Data.habits.Add(habit);
        writer.SaveNow();

        HabitSaveSystem reader = new HabitSaveSystem(storage);
        reader.Load();

        Assert.IsTrue(reader.Data.habits[0].isArchived);
    }
}
