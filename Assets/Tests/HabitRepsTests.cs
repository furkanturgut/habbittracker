using NUnit.Framework;

// Gunluk tur sayisinin EditMode testleri: hedef okuma, tik ilerletme,
// sifirlama, hedef duzenlemesi ve bozuk kayit onarimi. Sahne, kayit ya da
// manager gerekmiyor -- HabitReps saf (bkz. DAILY_REPS_PLAN.md).
//
// Butun testlerin ortak sozu: isCompletedToday true ise todayReps == hedef,
// false ise todayReps < hedef. AssertInvariant onu her yoldan sonra dogruluyor.
public class HabitRepsTests
{
    private static HabitData MakeHabit(int target = 1, int reps = 0, bool completed = false)
    {
        return new HabitData
        {
            id = "h1",
            title = "Kediyle oyna",
            habitType = HabitTypes.Build,
            targetPerDay = target,
            todayReps = reps,
            isCompletedToday = completed
        };
    }

    private static void AssertInvariant(HabitData habit)
    {
        int target = HabitReps.Target(habit);

        if (habit.isCompletedToday)
        {
            Assert.AreEqual(target, habit.todayReps,
                "Tamamlanmis gunde sayac hedefe esit olmali.");
            return;
        }

        Assert.Less(habit.todayReps, target,
            "Tamamlanmamis gunde sayac hedefin ALTINDA olmali.");
        Assert.GreaterOrEqual(habit.todayReps, 0);
    }

    // ---------------------------------------------------------------------
    // Hedef okuma
    // ---------------------------------------------------------------------

    // Eski bir kayitta alan hic yoktu; elle bozulmus bir JSON 0 ya da negatif
    // verebilir. Tek okuma kapisi her ikisini de 1'e cekiyor, yani hicbir
    // cagiran "0 tikle kapanan gun" gormuyor.
    [Test]
    public void Target_ZeroOrNegative_ReadsAsOne()
    {
        Assert.AreEqual(1, HabitReps.Target(MakeHabit(target: 0)));
        Assert.AreEqual(1, HabitReps.Target(MakeHabit(target: -5)));
    }

    [Test]
    public void Target_AboveMax_ReadsAsMax()
    {
        Assert.AreEqual(HabitReps.MaxTarget, HabitReps.Target(MakeHabit(target: 99)));
    }

    [Test]
    public void Target_NullHabit_ReadsAsMin()
    {
        Assert.AreEqual(HabitReps.MinTarget, HabitReps.Target(null));
    }

    [Test]
    public void ClampTarget_StaysInRange()
    {
        Assert.AreEqual(HabitReps.MinTarget, HabitReps.ClampTarget(0));
        Assert.AreEqual(HabitReps.MaxTarget, HabitReps.ClampTarget(HabitReps.MaxTarget + 1));
        Assert.AreEqual(3, HabitReps.ClampTarget(3));
    }

    [Test]
    public void IsMulti_OnlyAboveOne()
    {
        Assert.IsFalse(HabitReps.IsMulti(MakeHabit(target: 1)));
        Assert.IsFalse(HabitReps.IsMulti(MakeHabit(target: 0)), "0 == eski kayit == tek tik.");
        Assert.IsTrue(HabitReps.IsMulti(MakeHabit(target: 2)));
    }

    // ---------------------------------------------------------------------
    // Tik ilerletme
    // ---------------------------------------------------------------------

    // Hedef 1: bugunku davranisin birebir aynisi -- ilk tik gunu kapatiyor.
    [Test]
    public void Advance_SingleTarget_ClosesDayOnFirstTick()
    {
        HabitData habit = MakeHabit(target: 1);

        Assert.IsTrue(HabitReps.Advance(habit), "Hedef 1 ise ilk tik gunu kapatmali.");
        Assert.AreEqual(1, habit.todayReps);
    }

    [Test]
    public void Advance_TargetTwo_OnlySecondTickClosesDay()
    {
        HabitData habit = MakeHabit(target: 2);

        Assert.IsFalse(HabitReps.Advance(habit), "Ilk tik yalnizca sayaci ilerletmeli.");
        Assert.AreEqual(1, habit.todayReps);
        Assert.AreEqual(1, HabitReps.DoneCount(habit));
        AssertInvariant(habit);

        Assert.IsTrue(HabitReps.Advance(habit), "Ikinci tik gunu kapatmali.");
        Assert.AreEqual(2, habit.todayReps);

        // Cagiran taraf (HabbitManager.CompleteHabit) bayragi tam bu noktada
        // yaziyor; sozun tutmasi ancak ondan sonra olculebilir.
        habit.isCompletedToday = true;
        AssertInvariant(habit);
    }

    [Test]
    public void Advance_TargetThree_ClosesDayOnThirdTick()
    {
        HabitData habit = MakeHabit(target: 3);

        Assert.IsFalse(HabitReps.Advance(habit));
        Assert.IsFalse(HabitReps.Advance(habit));
        Assert.IsTrue(HabitReps.Advance(habit));
        Assert.AreEqual(3, habit.todayReps);
    }

    // Elle bozulmus bir kayit sayaci hedefin UZERINDE verebilir; ilerletmek
    // onu daha da buyutmemeli.
    [Test]
    public void Advance_CorruptCounter_NeverExceedsTarget()
    {
        HabitData habit = MakeHabit(target: 2, reps: 9);

        Assert.IsTrue(HabitReps.Advance(habit));
        Assert.AreEqual(2, habit.todayReps);
    }

    [Test]
    public void DoneCount_Completed_ReturnsTarget()
    {
        Assert.AreEqual(3, HabitReps.DoneCount(MakeHabit(target: 3, reps: 0, completed: true)),
            "Tamamlanmis gun sayac ne yazarsa yazsin hedefi gosterir.");
    }

    [Test]
    public void DoneCount_NotCompleted_NeverReachesTarget()
    {
        Assert.AreEqual(1, HabitReps.DoneCount(MakeHabit(target: 2, reps: 7)));
    }

    [Test]
    public void Reset_ClearsCounter()
    {
        HabitData habit = MakeHabit(target: 3, reps: 2);

        HabitReps.Reset(habit);

        Assert.AreEqual(0, habit.todayReps);
        AssertInvariant(habit);
    }

    // ---------------------------------------------------------------------
    // Hedef duzenlemesi
    // ---------------------------------------------------------------------

    // Hedef yukseldi ama gun zaten kapanmisti: bir duzenleme gecmisi geri
    // almamali (frequency/endDate duzenlemeleriyle ayni sinif).
    [Test]
    public void ApplyTarget_Raised_WhileCompleted_StaysCompleted()
    {
        HabitData habit = MakeHabit(target: 1, reps: 1, completed: true);

        HabitReps.ApplyTarget(habit, 3);

        Assert.IsTrue(habit.isCompletedToday);
        Assert.AreEqual(3, habit.todayReps);
        AssertInvariant(habit);
    }

    [Test]
    public void ApplyTarget_Raised_WhilePartial_KeepsCounter()
    {
        HabitData habit = MakeHabit(target: 2, reps: 1);

        HabitReps.ApplyTarget(habit, 4);

        Assert.AreEqual(4, habit.targetPerDay);
        Assert.AreEqual(1, habit.todayReps, "Atilan tik korunmali.");
        AssertInvariant(habit);
    }

    // Hedef dustu: fazla tiklar affediliyor ve gunu kapatmak icin bir dokunus
    // kaliyor -- tamamlama kuyrugu BURADAN cagrilmiyor.
    [Test]
    public void ApplyTarget_Lowered_WhilePartial_ForgivesExtraTicks()
    {
        HabitData habit = MakeHabit(target: 4, reps: 3);

        HabitReps.ApplyTarget(habit, 2);

        Assert.AreEqual(2, habit.targetPerDay);
        Assert.AreEqual(1, habit.todayReps);
        Assert.IsFalse(habit.isCompletedToday, "Duzenleme gunu kendiliginden kapatmamali.");
        AssertInvariant(habit);
    }

    [Test]
    public void ApplyTarget_Lowered_WhileCompleted_StaysCompleted()
    {
        HabitData habit = MakeHabit(target: 4, reps: 4, completed: true);

        HabitReps.ApplyTarget(habit, 2);

        Assert.IsTrue(habit.isCompletedToday);
        Assert.AreEqual(2, habit.todayReps);
        AssertInvariant(habit);
    }

    [Test]
    public void ApplyTarget_OutOfRange_Clamped()
    {
        HabitData habit = MakeHabit(target: 2, reps: 1);

        HabitReps.ApplyTarget(habit, 0);
        Assert.AreEqual(HabitReps.MinTarget, habit.targetPerDay);
        Assert.AreEqual(0, habit.todayReps, "Hedef 1'e dusunce sayac sifira inmeli.");
        AssertInvariant(habit);

        HabitReps.ApplyTarget(habit, 500);
        Assert.AreEqual(HabitReps.MaxTarget, habit.targetPerDay);
        AssertInvariant(habit);
    }

    // ---------------------------------------------------------------------
    // Onarim (kayit yuklemesi ve ice aktarim)
    // ---------------------------------------------------------------------

    [Test]
    public void Repair_CorruptTargetAndCounter_RestoresInvariant()
    {
        HabitData habit = MakeHabit(target: 0, reps: 42);

        HabitReps.Repair(habit);

        Assert.AreEqual(1, habit.targetPerDay);
        Assert.AreEqual(0, habit.todayReps);
        AssertInvariant(habit);
    }

    [Test]
    public void Repair_CompletedDay_CounterMatchesTarget()
    {
        HabitData habit = MakeHabit(target: 3, reps: 0, completed: true);

        HabitReps.Repair(habit);

        Assert.AreEqual(3, habit.todayReps);
        AssertInvariant(habit);
    }

    [Test]
    public void Repair_HugeTarget_ClampedToMax()
    {
        HabitData habit = MakeHabit(target: 1000, reps: 1000);

        HabitReps.Repair(habit);

        Assert.AreEqual(HabitReps.MaxTarget, habit.targetPerDay);
        Assert.AreEqual(HabitReps.MaxTarget - 1, habit.todayReps);
        AssertInvariant(habit);
    }
}
