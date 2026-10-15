using System;
using System.Collections.Generic;
using NUnit.Framework;

// Rotus ozelliginin saf katmaninin EditMode testleri: RotusCycle (7 tiklik
// dongu), HabitGallery.RestoreOne (delik onarimi) ve HabitSaveSystem'in
// rotusCount/rotusProgress icin yaptigi gocmeli-kayit okumasi + sayisal
// kelepce. PaintingHoleTests.cs/SettingsLogicTests.cs ile AYNI fixture kalibi
// (paylasilan yardimci yok, bilincli -- 500 satir siniri icin dosyalar ayri).
public class RotusTests
{
    private class MemoryStorage : IHabitStorage
    {
        public string Json;

        public string Load() => Json;
        public void Write(string json) => Json = json;
        public void Flush() { }
        public void Delete() { Json = null; }
    }

    private static HabitData MakeHabit(string id = "h1", string activePaintingId = "p0")
    {
        return new HabitData
        {
            id = id,
            title = "Test",
            habitType = HabitTypes.Build,
            canvasSize = 16,
            activePaintingId = activePaintingId,
            startDate = HabitScheduling.ToKey(new DateTime(2020, 1, 1))
        };
    }

    // -----------------------------------------------------------------------
    // RotusCycle.Advance
    // -----------------------------------------------------------------------

    [Test]
    public void Advance_FirstSixTicks_ReturnFalseAndCountUp()
    {
        HabitData habit = MakeHabit();

        for (int i = 1; i <= 6; i++)
        {
            Assert.IsFalse(RotusCycle.Advance(habit), "Tik " + i + " donguyu doldurmamali.");
            Assert.AreEqual(i, habit.rotusProgress);
        }
    }

    [Test]
    public void Advance_SeventhTick_ReturnsTrueAndWrapsToZero()
    {
        HabitData habit = MakeHabit();
        habit.rotusProgress = 6;

        bool earned = RotusCycle.Advance(habit);

        Assert.IsTrue(earned);
        Assert.AreEqual(0, habit.rotusProgress);
    }

    [Test]
    public void Advance_EighthTick_StartsNewCycle()
    {
        HabitData habit = MakeHabit();
        habit.rotusProgress = 6;

        RotusCycle.Advance(habit); // 7. tik -- donguyu doldurur, 0'a doner

        Assert.IsFalse(RotusCycle.Advance(habit));
        Assert.AreEqual(1, habit.rotusProgress);
    }

    // -----------------------------------------------------------------------
    // RotusCycle.RollBack
    // -----------------------------------------------------------------------

    [Test]
    public void RollBack_FromMiddleOfCycle_DecrementsAndReturnsFalse()
    {
        HabitData habit = MakeHabit();
        habit.rotusProgress = 3;

        bool rolledBackReward = RotusCycle.RollBack(habit);

        Assert.IsFalse(rolledBackReward);
        Assert.AreEqual(2, habit.rotusProgress);
    }

    [Test]
    public void RollBack_FromZero_WrapsToLastTickAndReturnsTrue()
    {
        // Sayac 0 iken geri alinmak SARMA demek: son tik (7.) donguyu
        // doldurup sifira sarmisti, yani o odul de geri alinmali.
        HabitData habit = MakeHabit();
        habit.rotusProgress = 0;

        bool rolledBackReward = RotusCycle.RollBack(habit);

        Assert.IsTrue(rolledBackReward);
        Assert.AreEqual(RotusCycle.CycleLength - 1, habit.rotusProgress);
    }

    [Test]
    public void AdvanceThenRollBack_ReturnsToStartingValue()
    {
        HabitData habit = MakeHabit();
        habit.rotusProgress = 4;

        RotusCycle.Advance(habit);
        RotusCycle.RollBack(habit);

        Assert.AreEqual(4, habit.rotusProgress);
    }

    [Test]
    public void SeventhTickThenRollBack_ReturnsToSixAndUndoesReward()
    {
        HabitData habit = MakeHabit();
        habit.rotusProgress = 6;

        bool earned = RotusCycle.Advance(habit);
        bool rolledBackReward = RotusCycle.RollBack(habit);

        Assert.IsTrue(earned);
        Assert.IsTrue(rolledBackReward);
        Assert.AreEqual(6, habit.rotusProgress);
    }

    [Test]
    public void TicksToNext_ReflectsRemainingDistance()
    {
        HabitData habit = MakeHabit();
        habit.rotusProgress = 4;

        Assert.AreEqual(3, RotusCycle.TicksToNext(habit));
    }

    // -----------------------------------------------------------------------
    // HabitGallery.RestoreOne
    // -----------------------------------------------------------------------

    [Test]
    public void RestoreOne_MissingPainting_ReturnsMinusOne()
    {
        HabitData habit = MakeHabit();

        int restored = HabitGallery.RestoreOne(habit, "p0");

        Assert.AreEqual(-1, restored);
    }

    [Test]
    public void RestoreOne_PaintingWithNoHoles_ReturnsMinusOne()
    {
        HabitData habit = MakeHabit();
        HabitGallery.Record(habit, "p0", new List<int>());

        int restored = HabitGallery.RestoreOne(habit, "p0");

        Assert.AreEqual(-1, restored);
    }

    [Test]
    public void RestoreOne_RemovesExactlyFirstHole()
    {
        HabitData habit = MakeHabit();
        HabitGallery.Record(habit, "p0", new List<int> { 2, 5, 9 });

        int restored = HabitGallery.RestoreOne(habit, "p0");

        Assert.AreEqual(2, restored);
        Assert.IsTrue(HabitGallery.TryGetHoles(habit, "p0", out List<int> remaining));
        CollectionAssert.AreEqual(new[] { 5, 9 }, remaining);
    }

    [Test]
    public void RestoreOne_LastHole_RemovesEntryEntirely()
    {
        HabitData habit = MakeHabit();
        HabitGallery.Record(habit, "p0", new List<int> { 7 });

        int restored = HabitGallery.RestoreOne(habit, "p0");

        Assert.AreEqual(7, restored);
        Assert.IsFalse(HabitGallery.TryGetHoles(habit, "p0", out _));
    }

    [Test]
    public void RestoreOne_DoesNotTouchOtherPaintingsHoles()
    {
        HabitData habit = MakeHabit();
        HabitGallery.Record(habit, "p0", new List<int> { 1 });
        HabitGallery.Record(habit, "p1", new List<int> { 3, 4 });

        HabitGallery.RestoreOne(habit, "p0");

        Assert.IsTrue(HabitGallery.TryGetHoles(habit, "p1", out List<int> stillHoled));
        CollectionAssert.AreEqual(new[] { 3, 4 }, stillHoled);
    }

    // -----------------------------------------------------------------------
    // HabitSaveSystem -- gocmeli kayit + sayisal kelepce
    // -----------------------------------------------------------------------

    [Test]
    public void Load_OldSaveWithoutRotusFields_DefaultsToZeroWithoutBumpingSaveVersion()
    {
        // "rotusCount" ve habit'in "rotusProgress" anahtarlari hic yok -- ozellik
        // eklenmeden once yazilmis bir kayit.
        string oldJson = "{\"saveVersion\":1,\"lastLoginDate\":\"2024-01-01\"," +
            "\"habits\":[{\"id\":\"h1\",\"canvasSize\":16}]," +
            "\"painterMoodPoint\":50,\"lastMoodSettledDate\":null}";

        MemoryStorage storage = new MemoryStorage { Json = oldJson };
        HabitSaveSystem saveSystem = new HabitSaveSystem(storage);

        saveSystem.Load();

        Assert.AreEqual(1, saveSystem.Data.saveVersion);
        Assert.AreEqual(0, saveSystem.Data.rotusCount);
        Assert.AreEqual(0, saveSystem.Data.habits[0].rotusProgress);
    }

    [Test]
    public void Load_NegativeRotusCount_ClampsToZero()
    {
        string json = "{\"saveVersion\":1,\"lastLoginDate\":\"2024-01-01\"," +
            "\"habits\":[],\"painterMoodPoint\":50,\"lastMoodSettledDate\":null," +
            "\"rotusCount\":-3}";

        MemoryStorage storage = new MemoryStorage { Json = json };
        HabitSaveSystem saveSystem = new HabitSaveSystem(storage);

        saveSystem.Load();

        Assert.AreEqual(0, saveSystem.Data.rotusCount);
    }

    [Test]
    public void Load_OutOfRangeRotusProgress_IsClampedToCycleBounds()
    {
        string json = "{\"saveVersion\":1,\"lastLoginDate\":\"2024-01-01\"," +
            "\"habits\":[{\"id\":\"h1\",\"canvasSize\":16,\"rotusProgress\":99}]," +
            "\"painterMoodPoint\":50,\"lastMoodSettledDate\":null}";

        MemoryStorage storage = new MemoryStorage { Json = json };
        HabitSaveSystem saveSystem = new HabitSaveSystem(storage);

        saveSystem.Load();

        Assert.AreEqual(RotusCycle.CycleLength - 1, saveSystem.Data.habits[0].rotusProgress);
    }

    [Test]
    public void DeleteAll_ResetsRotusCountToZero()
    {
        MemoryStorage storage = new MemoryStorage();
        HabitSaveSystem saveSystem = new HabitSaveSystem(storage);
        saveSystem.Load();
        saveSystem.Data.rotusCount = 5;

        saveSystem.DeleteAll();

        Assert.AreEqual(0, saveSystem.Data.rotusCount);
    }
}
