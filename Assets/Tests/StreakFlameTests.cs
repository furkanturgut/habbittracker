using System;
using NUnit.Framework;

// StreakFlame'in EditMode testleri: basamak sinirlari, ilerleme orani, bir
// sonraki aleve kalan gun, gri ("bekleyen") alev kosulu ve kutlamanin ne zaman
// acilip ne zaman bastirilacagi.
//
// Ayri bir dosya: HabitUiLogicTests.cs zaten 543 satir, projenin 500 satirlik
// dosya butcesinin uzerinde (bkz. MissedPieceTests / PaintingHoleTests ayrimi).
public class StreakFlameTests
{
    // 1 Ocak 2024 bir PAZARTESI -- diger test dosyalariyla ayni capa.
    private static readonly DateTime Monday = new DateTime(2024, 1, 1);

    private static HabitData MakeHabit(int streak, params DayOfWeek[] frequency)
    {
        HabitData habit = new HabitData
        {
            id = "h1",
            title = "Test",
            habitType = HabitTypes.Build,
            categoryGroup = HabitCategories.Health,
            canvasSize = 16,
            activePaintingId = "p1",
            currentStreak = streak,
            startDate = HabitScheduling.ToKey(new DateTime(2020, 1, 1))
        };

        for (int i = 0; i < frequency.Length; i++)
        {
            habit.frequency.Add(frequency[i]);
        }

        return habit;
    }

    // ---------------------------------------------------------------------
    // Tier -- basamak sinirlari
    // ---------------------------------------------------------------------

    [Test]
    public void Tier_BoundaryValuesLandOnTheRightStep()
    {
        Assert.AreEqual(0, StreakFlame.Tier(0), "0 gun T0 olmali.");
        Assert.AreEqual(1, StreakFlame.Tier(1));
        Assert.AreEqual(1, StreakFlame.Tier(6));
        Assert.AreEqual(2, StreakFlame.Tier(7));
        Assert.AreEqual(2, StreakFlame.Tier(13));
        Assert.AreEqual(3, StreakFlame.Tier(14));
        Assert.AreEqual(3, StreakFlame.Tier(29));
        Assert.AreEqual(4, StreakFlame.Tier(30));
        Assert.AreEqual(4, StreakFlame.Tier(49));
        Assert.AreEqual(5, StreakFlame.Tier(50));
        Assert.AreEqual(5, StreakFlame.Tier(99));
        Assert.AreEqual(6, StreakFlame.Tier(100));
        Assert.AreEqual(6, StreakFlame.Tier(199));
        Assert.AreEqual(7, StreakFlame.Tier(200));
        Assert.AreEqual(7, StreakFlame.Tier(364));
        Assert.AreEqual(8, StreakFlame.Tier(365));
        Assert.AreEqual(8, StreakFlame.Tier(1000), "365 ustu hep en ust basamak.");
    }

    // Bozuk bir kayittan negatif gelirse sonuc yine de gecerli bir basamak
    // olmali -- UI bunu dizi indeksi olarak kullaniyor.
    [Test]
    public void Tier_NegativeStreakClampsToZero()
    {
        Assert.AreEqual(0, StreakFlame.Tier(-5));
    }

    [Test]
    public void Thresholds_AreAscendingAndMatchTierCount()
    {
        Assert.AreEqual(StreakFlame.TierCount, StreakFlame.Thresholds.Length);

        for (int i = 1; i < StreakFlame.Thresholds.Length; i++)
        {
            Assert.Less(
                StreakFlame.Thresholds[i - 1], StreakFlame.Thresholds[i],
                "Esikler artan sirada olmali; Tier/Progress buna dayaniyor.");
        }
    }

    [Test]
    public void NextThreshold_IsTheFollowingStepAndZeroAtTop()
    {
        for (int tier = 0; tier < StreakFlame.TierCount - 1; tier++)
        {
            Assert.AreEqual(StreakFlame.Thresholds[tier + 1], StreakFlame.NextThreshold(tier));
        }

        Assert.AreEqual(0, StreakFlame.NextThreshold(StreakFlame.TierCount - 1));
    }

    // ---------------------------------------------------------------------
    // Progress -- basamak ici ilerleme
    // ---------------------------------------------------------------------

    [Test]
    public void Progress_StaysInRangeForEveryStreakUpToAYearAndAHalf()
    {
        for (int streak = 0; streak <= 550; streak++)
        {
            float progress = StreakFlame.Progress(streak);

            Assert.GreaterOrEqual(progress, 0f, "Seri " + streak + " icin ilerleme negatif.");
            Assert.LessOrEqual(progress, 1f, "Seri " + streak + " icin ilerleme 1'i asti.");
        }
    }

    [Test]
    public void Progress_IsZeroAtTheStartOfEveryStep()
    {
        // En ust basamak haric: orada ilerleme tanimli olarak 1.
        for (int tier = 0; tier < StreakFlame.TierCount - 1; tier++)
        {
            Assert.AreEqual(
                0f, StreakFlame.Progress(StreakFlame.Thresholds[tier]), 1e-5f,
                "T" + tier + " basinda ilerleme 0 olmali.");
        }
    }

    [Test]
    public void Progress_IsOneAtTopTier()
    {
        Assert.AreEqual(1f, StreakFlame.Progress(365), 1e-5f);
        Assert.AreEqual(1f, StreakFlame.Progress(5000), 1e-5f);
    }

    [Test]
    public void Progress_IsPartWayAcrossAStep()
    {
        // T2: 7..13, yani 7 gunluk aralik. 10. gun 3/7'si.
        Assert.AreEqual(3f / 7f, StreakFlame.Progress(10), 1e-5f);
    }

    // ---------------------------------------------------------------------
    // DaysToNext
    // ---------------------------------------------------------------------

    [Test]
    public void DaysToNext_CountsDownToTheNextStep()
    {
        Assert.AreEqual(1, StreakFlame.DaysToNext(0), "0'dan ilk aleve 1 gun.");
        Assert.AreEqual(6, StreakFlame.DaysToNext(1));
        Assert.AreEqual(1, StreakFlame.DaysToNext(6));
        Assert.AreEqual(7, StreakFlame.DaysToNext(7));
        Assert.AreEqual(1, StreakFlame.DaysToNext(364));
    }

    [Test]
    public void DaysToNext_IsZeroAtTopTier()
    {
        Assert.AreEqual(0, StreakFlame.DaysToNext(365));
        Assert.AreEqual(0, StreakFlame.DaysToNext(900));
    }

    // Her basamakta "kalan gun" ile esik aritmetigi tutarli olmali: su anki
    // seri + kalan gun tam olarak bir sonraki esige dusmeli.
    [Test]
    public void DaysToNext_LandsExactlyOnTheNextThreshold()
    {
        for (int streak = 0; streak < 365; streak++)
        {
            int tier = StreakFlame.Tier(streak);
            Assert.AreEqual(
                StreakFlame.Thresholds[tier + 1], streak + StreakFlame.DaysToNext(streak),
                "Seri " + streak + ": kalan gun bir sonraki esige dusmuyor.");
        }
    }

    // ---------------------------------------------------------------------
    // IsPending -- gri alev
    // ---------------------------------------------------------------------

    [Test]
    public void IsPending_DueTodayAndNotDoneWaits()
    {
        HabitData habit = MakeHabit(5);
        habit.isCompletedToday = false;

        Assert.IsTrue(StreakFlame.IsPending(habit, Monday));
    }

    [Test]
    public void IsPending_DueTodayAndDoneBurns()
    {
        HabitData habit = MakeHabit(5);
        habit.isCompletedToday = true;

        Assert.IsFalse(StreakFlame.IsPending(habit, Monday));
    }

    // Bugun planli degilse alev beklemez: o gun yapmakla yukumlu degil.
    [Test]
    public void IsPending_NotScheduledTodayDoesNotWait()
    {
        HabitData habit = MakeHabit(5, DayOfWeek.Tuesday);
        habit.isCompletedToday = false;

        Assert.IsFalse(
            StreakFlame.IsPending(habit, Monday),
            "Monday bir pazartesi; aliskanlik yalnizca salilari planli.");
    }

    [Test]
    public void IsPending_ZeroStreakIsOutNotWaiting()
    {
        HabitData habit = MakeHabit(0);
        habit.isCompletedToday = false;

        Assert.IsFalse(StreakFlame.IsPending(habit, Monday), "Seri yoksa alev sonuk, bekleyen degil.");
    }

    // ---------------------------------------------------------------------
    // ShouldCelebrate / StampCelebrated
    // ---------------------------------------------------------------------

    [Test]
    public void ShouldCelebrate_OpensWhenTheTierRises()
    {
        HabitData habit = MakeHabit(7);

        Assert.IsTrue(StreakFlame.ShouldCelebrate(habit, 1, 2, Monday));
    }

    [Test]
    public void ShouldCelebrate_DoesNotOpenWithoutARise()
    {
        HabitData habit = MakeHabit(8);

        Assert.IsFalse(StreakFlame.ShouldCelebrate(habit, 2, 2, Monday), "Ayni basamak kutlanmaz.");
        Assert.IsFalse(StreakFlame.ShouldCelebrate(habit, 3, 2, Monday), "Dusus hic kutlanmaz.");
    }

    // Ayni gun: tiki geri al, tekrar tikle -> kutlama IKINCI kez acilmamali.
    [Test]
    public void ShouldCelebrate_SameDayRetickDoesNotOpenTwice()
    {
        HabitData habit = MakeHabit(7);

        Assert.IsTrue(StreakFlame.ShouldCelebrate(habit, 1, 2, Monday));
        StreakFlame.StampCelebrated(habit, 2, Monday);

        Assert.IsFalse(
            StreakFlame.ShouldCelebrate(habit, 1, 2, Monday),
            "Ayni gun ayni basamak icin kutlama tekrar acilmamali.");
    }

    // Seri kirildi, gunler sonra yeniden tirmanildi -> YENIDEN acilir. Tarih
    // damgasinin var olma sebebi tam olarak bu (bkz. StreakFlame yorumu).
    [Test]
    public void ShouldCelebrate_OpensAgainOnALaterDayAfterTheStreakBroke()
    {
        HabitData habit = MakeHabit(7);
        StreakFlame.StampCelebrated(habit, 2, Monday);

        // Seri kirildi ve yeniden 1'e cikti: daha DUSUK bir basamak, baska bir gun.
        DateTime later = Monday.AddDays(20);

        Assert.IsTrue(
            StreakFlame.ShouldCelebrate(habit, 0, 1, later),
            "Farkli gunde, kirilan serinin ilk alevi yeniden kutlanmali.");
    }

    // Ayni gun icinde DAHA YUKSEK bir basamaga cikmak hala kutlanir: damga
    // "bu gun hic kutlama yok" demiyor, "bu basamak bu gun kutlandi" diyor.
    [Test]
    public void ShouldCelebrate_HigherTierOnTheSameDayStillOpens()
    {
        HabitData habit = MakeHabit(7);
        StreakFlame.StampCelebrated(habit, 2, Monday);

        Assert.IsTrue(StreakFlame.ShouldCelebrate(habit, 2, 3, Monday));
    }

    [Test]
    public void StampCelebrated_WritesTierAndDay()
    {
        HabitData habit = MakeHabit(30);
        StreakFlame.StampCelebrated(habit, 4, Monday);

        Assert.AreEqual(4, habit.lastCelebratedTier);
        Assert.AreEqual(HabitScheduling.ToKey(Monday), habit.lastCelebratedDate);
    }

    // Eski bir kayitta bu alanlar hic yok (JsonUtility 0 / null birakiyor) --
    // ilk yukselis yine de kutlanmali.
    [Test]
    public void ShouldCelebrate_OldSaveWithNoStampCelebratesNormally()
    {
        HabitData habit = MakeHabit(1);
        habit.lastCelebratedTier = 0;
        habit.lastCelebratedDate = null;

        Assert.IsTrue(StreakFlame.ShouldCelebrate(habit, 0, 1, Monday));
    }
}
