using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// Kacirilan gun -> kalici kapanan parca hattinin EditMode testleri:
// MissedPieceService (sayma + yakma) ve PaintingProgress'in delikli Sequential
// semantigi. Galeri delikleri ve gorunum haritasi Tests/PaintingHoleTests.cs'te
// (ayni fixture kalibi, 500 satir siniri icin bolundu).
public class MissedPieceTests
{
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

    private static HabitData MakeHabit(string id = "h1", int canvasSize = 16, string activePaintingId = "p0")
    {
        return new HabitData
        {
            id = id,
            title = "Test",
            habitType = HabitTypes.Build,
            canvasSize = canvasSize,
            activePaintingId = activePaintingId,
            startDate = HabitScheduling.ToKey(new DateTime(2020, 1, 1))
        };
    }

    // -----------------------------------------------------------------------
    // MissedPieceService.CountMissedDueDays
    // -----------------------------------------------------------------------

    [Test]
    public void CountMissedDueDays_LastLoginDueNotCompleted_CountsOne()
    {
        HabitData habit = MakeHabit();
        habit.isCompletedToday = false;

        int missed = MissedPieceService.CountMissedDueDays(
            habit, new DateTime(2026, 9, 1), new DateTime(2026, 9, 2));

        Assert.AreEqual(1, missed);
    }

    [Test]
    public void CountMissedDueDays_LastLoginCompleted_CountsZero()
    {
        HabitData habit = MakeHabit();
        habit.isCompletedToday = true;

        int missed = MissedPieceService.CountMissedDueDays(
            habit, new DateTime(2026, 9, 1), new DateTime(2026, 9, 2));

        Assert.AreEqual(0, missed);
    }

    [Test]
    public void CountMissedDueDays_ThreeDayGap_EveryDayDue_CountsThree()
    {
        HabitData habit = MakeHabit();
        habit.isCompletedToday = true; // son giris gunu tamamlanmisti

        int missed = MissedPieceService.CountMissedDueDays(
            habit, new DateTime(2026, 9, 1), new DateTime(2026, 9, 5));

        Assert.AreEqual(3, missed, "2, 3 ve 4 Eylul hic acilmadi ve planli bir sey vardi.");
    }

    [Test]
    public void CountMissedDueDays_UnscheduledDaysAreSkipped()
    {
        // 1 Eylul 2026 Sali. Habit sadece Pazartesi planli: aradaki tek
        // planli gun 7 Eylul.
        HabitData habit = MakeHabit();
        habit.frequency.Add(DayOfWeek.Monday);

        int missed = MissedPieceService.CountMissedDueDays(
            habit, new DateTime(2026, 9, 1), new DateTime(2026, 9, 8));

        Assert.AreEqual(1, missed);
    }

    [Test]
    public void CountMissedDueDays_RespectsHabitStartDate()
    {
        HabitData habit = MakeHabit();
        habit.startDate = HabitScheduling.ToKey(new DateTime(2026, 9, 4));

        // Habit 4 Eylul'den once yururlukte degil: sadece 4 ve 5 Eylul sayilir.
        int missed = MissedPieceService.CountMissedDueDays(
            habit, new DateTime(2026, 9, 1), new DateTime(2026, 9, 6));

        Assert.AreEqual(2, missed);
    }

    [Test]
    public void CountMissedDueDays_ClockMovedBackwards_ReturnsZero()
    {
        HabitData habit = MakeHabit();

        int missed = MissedPieceService.CountMissedDueDays(
            habit, new DateTime(2026, 9, 5), new DateTime(2026, 9, 1));

        Assert.AreEqual(0, missed);
    }

    [Test]
    public void CountMissedDueDays_SameDay_ReturnsZero()
    {
        HabitData habit = MakeHabit();
        DateTime day = new DateTime(2026, 9, 5);

        Assert.AreEqual(0, MissedPieceService.CountMissedDueDays(habit, day, day));
    }

    // -----------------------------------------------------------------------
    // MissedPieceService.Settle / SettleAll
    // -----------------------------------------------------------------------

    [Test]
    public void Settle_Sequential_BurnsOnePiecePerMissedDay()
    {
        PaintingLibrary library = MakeLibrary(1, 8, true);
        HabitData habit = MakeHabit(canvasSize: 8);
        List<int> scratch = new List<int>();

        // 1 Eylul (kacirildi) + 2, 3 Eylul (bosluk) = 3 gun.
        int burned = MissedPieceService.Settle(
            library, habit, new DateTime(2026, 9, 1), new DateTime(2026, 9, 4), scratch);

        Assert.AreEqual(3, burned);
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, habit.missedPixels);
    }

    [Test]
    public void Settle_StopsWhenPaintingExhausted()
    {
        PaintingLibrary library = MakeLibrary(1, 2, true);
        HabitData habit = MakeHabit(canvasSize: 2);
        habit.totalPixelsPainted = 1; // tuvalde 1 yer kaldi
        List<int> scratch = new List<int>();

        int burned = MissedPieceService.Settle(
            library, habit, new DateTime(2026, 9, 1), new DateTime(2026, 9, 4), scratch);

        Assert.AreEqual(1, burned, "Tuvalde yer kalmayinca yakma durmali.");
        Assert.AreEqual(1, habit.missedPixels.Count);
    }

    [Test]
    public void SettleAll_FirstRun_NoLastLoginDate_ReturnsZero()
    {
        PaintingLibrary library = MakeLibrary(1, 8, true);
        PlayerSaveData data = new PlayerSaveData();
        data.habits.Add(MakeHabit());

        List<HabitData> affected = new List<HabitData>();
        int burned = MissedPieceService.SettleAll(library, data, new DateTime(2026, 9, 5), new List<int>(), affected);

        Assert.AreEqual(0, burned);
        Assert.AreEqual(0, affected.Count);
    }

    [Test]
    public void SettleAll_OnlyAffectedHabitsAreReturned()
    {
        PaintingLibrary library = MakeLibrary(1, 8, true);
        PlayerSaveData data = new PlayerSaveData { lastLoginDate = HabitScheduling.ToKey(new DateTime(2026, 9, 1)) };

        HabitData missedHabit = MakeHabit("h-missed");
        HabitData safeHabit = MakeHabit("h-safe");
        safeHabit.isCompletedToday = true;
        data.habits.Add(missedHabit);
        data.habits.Add(safeHabit);

        List<HabitData> affected = new List<HabitData>();
        int burned = MissedPieceService.SettleAll(library, data, new DateTime(2026, 9, 2), new List<int>(), affected);

        Assert.AreEqual(1, burned);
        CollectionAssert.AreEqual(new[] { missedHabit }, affected);
    }

    // -----------------------------------------------------------------------
    // PaintingProgress: delikli Sequential semantigi
    // -----------------------------------------------------------------------

    [Test]
    public void OpenAndMiss_Sequential_ConsumeInTouchedOrder()
    {
        HabitData habit = MakeHabit(canvasSize: 8);
        List<int> scratch = new List<int>();

        Assert.AreEqual(0, PaintingProgress.OpenNextPiece(habit, true, scratch));
        Assert.AreEqual(1, PaintingProgress.OpenNextPiece(habit, true, scratch));
        Assert.AreEqual(2, PaintingProgress.MissPiece(habit, true, scratch), "Yakilan parca da SIRADAKI olmali.");
        Assert.AreEqual(3, PaintingProgress.OpenNextPiece(habit, true, scratch), "Acma, yakilan parcayi ATLAMALI.");

        Assert.AreEqual(3, habit.totalPixelsPainted, "totalPixelsPainted sadece ACIK sayisi.");
        CollectionAssert.AreEqual(new[] { 2 }, habit.missedPixels);
    }

    [Test]
    public void UndoLastPiece_Sequential_WithHoles_ReopensFollowingSlot()
    {
        HabitData habit = MakeHabit(canvasSize: 8);
        List<int> scratch = new List<int>();

        PaintingProgress.OpenNextPiece(habit, true, scratch); // 0
        PaintingProgress.OpenNextPiece(habit, true, scratch); // 1
        PaintingProgress.MissPiece(habit, true, scratch); // 2
        PaintingProgress.OpenNextPiece(habit, true, scratch); // 3 (son tik)

        int undone = PaintingProgress.UndoLastPiece(habit, true);

        Assert.AreEqual(3, undone, "Son ACILAN parca geri alinmali, yakilan degil.");
        Assert.AreEqual(2, habit.totalPixelsPainted);
        CollectionAssert.AreEqual(new[] { 2 }, habit.missedPixels, "Yakilan parca undo'dan etkilenmemeli.");
        Assert.AreEqual(3, PaintingProgress.NextSequentialPiece(habit), "Geri alinan indeks tekrar siradaki olmali.");
    }

    [Test]
    public void MissPiece_Random_NeverPicksOpenedOrAlreadyMissed()
    {
        HabitData habit = MakeHabit(canvasSize: 4);
        List<int> scratch = new List<int>();
        habit.randomOpenedPixels.AddRange(new[] { 0, 1 });

        int missed1 = PaintingProgress.MissPiece(habit, false, scratch);
        int missed2 = PaintingProgress.MissPiece(habit, false, scratch);

        Assert.AreNotEqual(missed1, missed2);
        CollectionAssert.AreEquivalent(new[] { 2, 3 }, new[] { missed1, missed2 });

        Assert.AreEqual(-1, PaintingProgress.MissPiece(habit, false, scratch), "Tuval tukendi.");
        Assert.AreEqual(-1, PaintingProgress.OpenNextPiece(habit, false, scratch), "Tuval tukendi.");
    }

    [Test]
    public void IsComplete_OpenedPlusMissedReachesCanvasSize()
    {
        HabitData habit = MakeHabit(canvasSize: 4);
        habit.totalPixelsPainted = 3;
        habit.missedPixels.Add(3);

        Assert.IsTrue(PaintingProgress.IsComplete(habit, true));
    }

    [Test]
    public void IsComplete_OpenedAloneNotEnough_WhenHolesExist()
    {
        HabitData habit = MakeHabit(canvasSize: 4);
        habit.totalPixelsPainted = 2;
        habit.missedPixels.Add(2);

        Assert.IsFalse(PaintingProgress.IsComplete(habit, true), "Tuketilen 3/4, hala eksik.");
    }

    [Test]
    public void ResetProgress_ClearsMissedPixelsToo()
    {
        HabitData habit = MakeHabit();
        habit.missedPixels.AddRange(new[] { 1, 2 });
        habit.totalPixelsPainted = 5;
        habit.randomOpenedPixels.Add(3);

        PaintingProgress.ResetProgress(habit);

        Assert.AreEqual(0, habit.totalPixelsPainted);
        Assert.AreEqual(0, habit.randomOpenedPixels.Count);
        Assert.AreEqual(0, habit.missedPixels.Count);
    }

    // -----------------------------------------------------------------------
    // Tam gun boru hatti: yakma + reset ayni cagri sirasinda
    // -----------------------------------------------------------------------

    [Test]
    public void Pipeline_MissedDayBurnsPieceAndBreaksStreak()
    {
        PaintingLibrary library = MakeLibrary(1, 8, true);
        PlayerSaveData data = new PlayerSaveData { lastLoginDate = HabitScheduling.ToKey(new DateTime(2026, 9, 1)) };

        HabitData habit = MakeHabit(canvasSize: 8);
        habit.currentStreak = 5;
        habit.lastCompletedDate = HabitScheduling.ToKey(new DateTime(2026, 9, 1));
        habit.isCompletedToday = true;
        data.habits.Add(habit);

        DateTime now = new DateTime(2026, 9, 3); // 2 Eylul kacirildi

        List<HabitData> affected = new List<HabitData>();
        int burned = MissedPieceService.SettleAll(library, data, now, new List<int>(), affected);
        DailyResetService.ApplyDailyReset(data, now);

        Assert.AreEqual(1, burned);
        Assert.AreEqual(1, habit.missedPixels.Count);
        Assert.AreEqual(0, habit.currentStreak, "2 Eylul kacirildigi icin seri kirilmali.");
    }
}
