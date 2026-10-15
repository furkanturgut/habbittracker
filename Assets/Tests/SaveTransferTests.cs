using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// SaveTransfer'in (disa/ice aktarma zarfi) EditMode testleri: zarf
// dogrulamasi, redaksiyon, ve ice aktarmanin sert onarim pass'i (Sanitize).
//
// HabitDataLogicTests/SettingsLogicTests'teki MemoryStorage kalibinin aynisi --
// paylasilan bir yardimci yok, her test dosyasi kendi nested kopyasini tutuyor.
//
// Calistirmak icin: Window > General > Test Runner > EditMode > Run All.
public class SaveTransferTests
{
    private class MemoryStorage : IHabitStorage
    {
        public string Json;

        public string Load() => Json;
        public void Write(string json) => Json = json;
        public void Flush() { }
        public void Delete() { Json = null; }
    }

    private static HabitData MakeHabit(string id = "h1", int canvasSize = 16)
    {
        return new HabitData
        {
            id = id,
            title = "Test",
            habitType = HabitTypes.Build,
            canvasSize = canvasSize,
            activePaintingId = "p1",
            startDate = HabitScheduling.ToKey(new DateTime(2020, 1, 1))
        };
    }

    private static SaveTransferPackage MakeValidPackage(PlayerSaveData data, int habitCount)
    {
        return new SaveTransferPackage
        {
            format = SaveTransferPackage.FormatId,
            formatVersion = SaveTransferPackage.CurrentFormatVersion,
            exportedAt = "2026-01-01 00:00",
            appVersion = "1.0",
            habitCount = habitCount,
            data = data
        };
    }

    // ---------------------------------------------------------------------
    // Build
    // ---------------------------------------------------------------------

    [Test]
    public void Build_RoundTrips_EveryHabitField()
    {
        PlayerSaveData data = new PlayerSaveData
        {
            lastLoginDate = "2026-09-01",
            lastMoodSettledDate = "2026-08-31",
            painterMoodPoint = 4,
            rotusCount = 2
        };
        data.settings.painterName = "Tulpar";
        data.settings.reminderOn = true;
        data.settings.customCategories.Add("Ozel");
        data.settings.filterTokens.Add("@build");

        HabitData habit = MakeHabit("habit-a", 32);
        habit.title = "Su ic";
        habit.currentStreak = 7;
        habit.longestStreak = 12;
        habit.totalPixelsPainted = 5;
        habit.randomOpenedPixels.AddRange(new[] { 3, 9 });
        habit.missedPixels.Add(14);
        habit.habitGallery.Add("finished-painting");
        habit.galleryHoles.Add(new PaintingHoles { paintingId = "finished-painting", pieces = new List<int> { 2 } });
        data.habits.Add(habit);

        string json = SaveTransfer.Build(data, new DateTime(2026, 10, 14, 18, 35, 0), "0.1");

        Assert.IsTrue(SaveTransfer.TryRead(json, out SaveTransferPackage package, out string error), error);
        Assert.AreEqual("0.1", package.appVersion);
        Assert.AreEqual(1, package.habitCount);

        PlayerSaveData read = package.data;
        Assert.AreEqual("2026-09-01", read.lastLoginDate);
        Assert.AreEqual("2026-08-31", read.lastMoodSettledDate);
        Assert.AreEqual(4, read.painterMoodPoint);
        Assert.AreEqual(2, read.rotusCount);
        Assert.AreEqual("Tulpar", read.settings.painterName);
        Assert.IsTrue(read.settings.reminderOn);
        CollectionAssert.AreEqual(new[] { "Ozel" }, read.settings.customCategories);
        CollectionAssert.AreEqual(new[] { "@build" }, read.settings.filterTokens);

        HabitData readHabit = read.habits[0];
        Assert.AreEqual("habit-a", readHabit.id);
        Assert.AreEqual("Su ic", readHabit.title);
        Assert.AreEqual(7, readHabit.currentStreak);
        Assert.AreEqual(12, readHabit.longestStreak);
        Assert.AreEqual(32, readHabit.canvasSize);
        Assert.AreEqual(5, readHabit.totalPixelsPainted);
        CollectionAssert.AreEqual(new[] { 3, 9 }, readHabit.randomOpenedPixels);
        CollectionAssert.AreEqual(new[] { 14 }, readHabit.missedPixels);
        CollectionAssert.AreEqual(new[] { "finished-painting" }, readHabit.habitGallery);
        Assert.AreEqual(1, readHabit.galleryHoles.Count);
        Assert.AreEqual("finished-painting", readHabit.galleryHoles[0].paintingId);
        CollectionAssert.AreEqual(new[] { 2 }, readHabit.galleryHoles[0].pieces);
    }

    [Test]
    public void Build_StripsPremium()
    {
        PlayerSaveData data = new PlayerSaveData();
        data.settings.isPremium = true;

        string json = SaveTransfer.Build(data, DateTime.Now, "0.1");

        Assert.IsTrue(SaveTransfer.TryRead(json, out SaveTransferPackage package, out string error), error);
        Assert.IsFalse(package.data.settings.isPremium);
    }

    [Test]
    public void Build_DoesNotMutateSource()
    {
        PlayerSaveData data = new PlayerSaveData();
        data.settings.isPremium = true;
        data.habits.Add(MakeHabit());

        SaveTransfer.Build(data, DateTime.Now, "0.1");

        Assert.IsTrue(data.settings.isPremium);
        Assert.AreEqual(1, data.habits.Count);
    }

    // ---------------------------------------------------------------------
    // TryRead
    // ---------------------------------------------------------------------

    [Test]
    public void TryRead_Null_IsRejected()
    {
        Assert.IsFalse(SaveTransfer.TryRead(null, out SaveTransferPackage package, out string error));
        Assert.IsNull(package);
        Assert.IsNotEmpty(error);
    }

    [Test]
    public void TryRead_Empty_IsRejected()
    {
        Assert.IsFalse(SaveTransfer.TryRead(string.Empty, out SaveTransferPackage package, out string error));
        Assert.IsNull(package);
    }

    [TestCase("merhaba")]
    [TestCase("{\"format\":\"habittracker.save\"")]
    [TestCase("{}")]
    [TestCase("{\"foo\":1,\"bar\":\"baz\"}")]
    public void TryRead_ForeignOrGarbageJson_IsRejected(string json)
    {
        Assert.IsFalse(SaveTransfer.TryRead(json, out SaveTransferPackage package, out string error));
        Assert.IsNull(package);
        Assert.IsNotEmpty(error);
    }

    [Test]
    public void TryRead_NewerFormatVersion_IsRejected()
    {
        PlayerSaveData data = new PlayerSaveData();
        SaveTransferPackage package = MakeValidPackage(data, 0);
        package.formatVersion = SaveTransferPackage.CurrentFormatVersion + 1;

        string json = JsonUtility.ToJson(package);

        Assert.IsFalse(SaveTransfer.TryRead(json, out SaveTransferPackage parsed, out string error));
        Assert.IsNull(parsed);
        Assert.IsNotEmpty(error);
    }

    [Test]
    public void TryRead_NewerSaveVersion_IsRejected()
    {
        PlayerSaveData data = new PlayerSaveData { saveVersion = PlayerSaveData.CurrentSaveVersion + 1 };
        SaveTransferPackage package = MakeValidPackage(data, 0);

        string json = JsonUtility.ToJson(package);

        Assert.IsFalse(SaveTransfer.TryRead(json, out SaveTransferPackage parsed, out string error));
        Assert.IsNull(parsed);
    }

    [Test]
    public void TryRead_HabitCountMismatch_IsRejected()
    {
        PlayerSaveData data = new PlayerSaveData();
        data.habits.Add(MakeHabit());
        SaveTransferPackage package = MakeValidPackage(data, 5);

        string json = JsonUtility.ToJson(package);

        Assert.IsFalse(SaveTransfer.TryRead(json, out SaveTransferPackage parsed, out string error));
        Assert.IsNull(parsed);
    }

    [Test]
    public void TryRead_ValidPackage_IsAccepted()
    {
        PlayerSaveData data = new PlayerSaveData();
        data.habits.Add(MakeHabit());
        string json = SaveTransfer.Build(data, DateTime.Now, "0.1");

        Assert.IsTrue(SaveTransfer.TryRead(json, out SaveTransferPackage package, out string error));
        Assert.IsNull(error);
        Assert.IsNotNull(package);
    }

    // ---------------------------------------------------------------------
    // Sanitize
    // ---------------------------------------------------------------------

    [Test]
    public void Sanitize_OutOfRangeOrDuplicatePieceIndices_AreDropped()
    {
        PlayerSaveData data = new PlayerSaveData();
        HabitData habit = MakeHabit(canvasSize: 16);
        habit.randomOpenedPixels.AddRange(new[] { -1, 3, 3, 20, 5 });
        data.habits.Add(habit);

        SaveTransfer.Sanitize(data, DateTime.Now);

        CollectionAssert.AreEqual(new[] { 3, 5 }, habit.randomOpenedPixels);
    }

    [Test]
    public void Sanitize_IndexInBothOpenedAndMissed_OpenedWins()
    {
        PlayerSaveData data = new PlayerSaveData();
        HabitData habit = MakeHabit(canvasSize: 16);
        habit.randomOpenedPixels.AddRange(new[] { 2, 5 });
        habit.missedPixels.AddRange(new[] { 5, 9 });
        data.habits.Add(habit);

        SaveTransfer.Sanitize(data, DateTime.Now);

        CollectionAssert.AreEqual(new[] { 2, 5 }, habit.randomOpenedPixels);
        CollectionAssert.AreEqual(new[] { 9 }, habit.missedPixels);
    }

    [Test]
    public void Sanitize_TotalPixelsPaintedBeyondCanvas_IsClamped()
    {
        PlayerSaveData data = new PlayerSaveData();
        HabitData habit = MakeHabit(canvasSize: 16);
        habit.totalPixelsPainted = 20;
        habit.missedPixels.AddRange(new[] { 2, 5 });
        data.habits.Add(habit);

        SaveTransfer.Sanitize(data, DateTime.Now);

        // Acik parcalar ONCELIKLI: once yakilan parcalardan kirpilir.
        Assert.AreEqual(0, habit.missedPixels.Count);
        Assert.AreEqual(16, habit.totalPixelsPainted);
    }

    [Test]
    public void Sanitize_FutureDates_AreClampedToToday()
    {
        PlayerSaveData data = new PlayerSaveData
        {
            lastLoginDate = "2030-01-01",
            lastMoodSettledDate = "2030-01-01"
        };
        HabitData habit = MakeHabit();
        habit.lastCompletedDate = "2030-01-01";
        habit.lastCelebratedDate = "2030-01-01";
        data.habits.Add(habit);

        DateTime now = new DateTime(2026, 10, 14);
        SaveTransfer.Sanitize(data, now);

        string todayKey = HabitScheduling.ToKey(now);
        Assert.AreEqual(todayKey, data.lastLoginDate);
        Assert.AreEqual(todayKey, data.lastMoodSettledDate);
        Assert.AreEqual(todayKey, habit.lastCompletedDate);
        Assert.AreEqual(todayKey, habit.lastCelebratedDate);
    }

    [Test]
    public void Sanitize_NegativeStreaksAreClamped()
    {
        PlayerSaveData data = new PlayerSaveData();
        HabitData habit = MakeHabit();
        habit.currentStreak = -5;
        habit.longestStreak = -1;
        data.habits.Add(habit);

        SaveTransfer.Sanitize(data, DateTime.Now);

        Assert.AreEqual(0, habit.currentStreak);
        Assert.AreEqual(0, habit.longestStreak);
    }

    [Test]
    public void Sanitize_LongestNeverBelowCurrent()
    {
        PlayerSaveData data = new PlayerSaveData();
        HabitData habit = MakeHabit();
        habit.currentStreak = 10;
        habit.longestStreak = 3;
        data.habits.Add(habit);

        SaveTransfer.Sanitize(data, DateTime.Now);

        Assert.AreEqual(10, habit.longestStreak);
    }

    [Test]
    public void Sanitize_IllegalFrequencyValues_AreDropped()
    {
        PlayerSaveData data = new PlayerSaveData();
        HabitData habit = MakeHabit();
        habit.frequency.Add(DayOfWeek.Monday);
        habit.frequency.Add((DayOfWeek)9);
        habit.frequency.Add((DayOfWeek)(-1));
        data.habits.Add(habit);

        SaveTransfer.Sanitize(data, DateTime.Now);

        CollectionAssert.AreEqual(new[] { DayOfWeek.Monday }, habit.frequency);
    }

    [TestCase(-1)]
    [TestCase(9)]
    [TestCase(999)]
    public void Sanitize_OutOfRangeLanguage_FallsBackToUnset(int rawValue)
    {
        PlayerSaveData data = new PlayerSaveData();
        data.settings.language = (AppLanguage)rawValue;

        SaveTransfer.Sanitize(data, DateTime.Now);

        Assert.AreEqual(AppLanguage.Unset, data.settings.language);
    }

    [Test]
    public void Sanitize_ValidLanguage_IsKept()
    {
        PlayerSaveData data = new PlayerSaveData();
        data.settings.language = AppLanguage.Spanish;

        SaveTransfer.Sanitize(data, DateTime.Now);

        Assert.AreEqual(AppLanguage.Spanish, data.settings.language);
    }

    // ---------------------------------------------------------------------
    // HabitSaveSystem.ReplaceWith
    // ---------------------------------------------------------------------

    [Test]
    public void ReplaceWith_EmptyIncomingHabits_ClearsExistingHabits()
    {
        MemoryStorage storage = new MemoryStorage();
        HabitSaveSystem system = new HabitSaveSystem(storage);
        system.Data.habits.Add(MakeHabit("old-1"));
        system.Data.habits.Add(MakeHabit("old-2"));
        system.SaveNow();

        system.ReplaceWith(new PlayerSaveData());

        Assert.AreEqual(0, system.Data.habits.Count);
    }

    [Test]
    public void ReplaceWith_NullListsInIncoming_AreRepaired()
    {
        MemoryStorage storage = new MemoryStorage();
        HabitSaveSystem system = new HabitSaveSystem(storage);

        PlayerSaveData incoming = new PlayerSaveData();
        HabitData habit = MakeHabit("new-1");
        habit.frequency = null;
        habit.randomOpenedPixels = null;
        habit.missedPixels = null;
        habit.habitGallery = null;
        habit.galleryHoles = null;
        incoming.habits.Add(habit);

        system.ReplaceWith(incoming);

        HabitData loaded = system.Data.habits[0];
        Assert.IsNotNull(loaded.frequency);
        Assert.IsNotNull(loaded.randomOpenedPixels);
        Assert.IsNotNull(loaded.missedPixels);
        Assert.IsNotNull(loaded.habitGallery);
        Assert.IsNotNull(loaded.galleryHoles);
    }

    [Test]
    public void ReplaceWith_PreservesDataInstanceIdentity()
    {
        MemoryStorage storage = new MemoryStorage();
        HabitSaveSystem system = new HabitSaveSystem(storage);
        PlayerSaveData before = system.Data;

        system.ReplaceWith(new PlayerSaveData());

        Assert.AreSame(before, system.Data);
    }
}
