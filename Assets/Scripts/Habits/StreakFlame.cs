using System;
using UnityEngine;

// Seri alevinin basamak mantigi: kac gunluk seri hangi alevi yakar, alev su an
// bekliyor mu, bir sonraki aleve kac gun kaldi ve yeni bir basamaga gecildiginde
// kutlama acilmali mi.
//
// Saf statik -- MonoBehaviour yok, DateTime.Now yok, tarih disaridan veriliyor
// (DailyResetService / HabitScheduling / MoodService ile ayni kalip). Boylece
// "365. gun" ya da "ayni gun ikinci tik" senaryolari sahne kurmadan test ediliyor.
//
// Basamak ADLARI (Kivilcim, Mum, Kandil, ...) burada BILEREK yok: teslim dosyasi
// onlari "yalnizca gelistirme icin" diye isaretliyor, kullanici hicbir yerde
// gormuyor -- ekranda olan tek sey alev, gun sayisi ve "kac gun kaldi".
public static class StreakFlame
{
    public const int TierCount = 9;

    // T0..T8'in alt sinirlari. Artan sirada olmak ZORUNDA: Tier() tek gecisle
    // tariyor ve Progress/DaysToNext komsu iki esigin arasini olcuyor.
    public static readonly int[] Thresholds = { 0, 1, 7, 14, 30, 50, 100, 200, 365 };

    public static int Tier(int streak)
    {
        if (streak < 0)
        {
            streak = 0;
        }

        int tier = 0;

        for (int i = 0; i < Thresholds.Length; i++)
        {
            if (streak >= Thresholds[i])
            {
                tier = i;
            }
        }

        return tier;
    }

    // Bir sonraki basamagin esigi; en ust basamakta 0 ("daha yukarisi yok").
    // Kutlama ekraninin "Sonraki alev N. gunde" satiri buradan besleniyor --
    // Thresholds'a disaridan [tier + 1] ile uzanmak sinir kontrolunu cagiran
    // tarafa birakirdi.
    public static int NextThreshold(int tier)
    {
        return tier >= 0 && tier < TierCount - 1 ? Thresholds[tier + 1] : 0;
    }

    // Icinde bulunulan basamakta ne kadar ilerlendigi, 0..1. En ust basamakta 1.
    public static float Progress(int streak)
    {
        if (streak < 0)
        {
            streak = 0;
        }

        int tier = Tier(streak);

        if (tier == TierCount - 1)
        {
            return 1f;
        }

        int from = Thresholds[tier];
        int to = Thresholds[tier + 1];

        return Mathf.Clamp01((streak - from) / (float)(to - from));
    }

    // Bir sonraki aleve kalan gun; en ust basamakta 0.
    public static int DaysToNext(int streak)
    {
        if (streak < 0)
        {
            streak = 0;
        }

        int tier = Tier(streak);

        return tier == TierCount - 1 ? 0 : Thresholds[tier + 1] - streak;
    }

    // Gri ("bekleyen") alev: seri YASIYOR ama bugun planli olan tik henuz
    // atilmadi. Bugun planli OLMAYAN bir aliskanlik beklemiyor -- alevi yanik
    // kalir, cunku o gun zaten yapmakla yukumlu degil (BreakMissedStreaks'in
    // "planli olmayan gun streak'i kirmaz" karariyla ayni cizgi).
    public static bool IsPending(HabitData habit, DateTime now)
    {
        return habit != null
            && habit.currentStreak > 0
            && HabitScheduling.IsDueOn(habit, now)
            && !habit.isCompletedToday;
    }

    // Kutlama acilmali mi? Yalnizca basamak YUKSELDIYSE, ve ayni gun icinde o
    // basamak icin zaten acilmadiysa.
    //
    // Damganin TARIHI de tutulmasinin sebebi: yalnizca lastCelebratedTier
    // saklansaydi, seri kirilip sifirdan 1'e ciktiginda 1 > 2 olmadigi icin
    // kutlama bir daha HIC acilmazdi. Tarih, bastirmayi yalnizca o gune
    // sinirliyor -- lastMoodSettledDate'in cift-sayim muhafizi ile ayni fikir.
    public static bool ShouldCelebrate(HabitData habit, int beforeTier, int afterTier, DateTime now)
    {
        if (habit == null || afterTier <= beforeTier)
        {
            return false;
        }

        bool stampedToday = string.Equals(
            habit.lastCelebratedDate, HabitScheduling.ToKey(now), StringComparison.Ordinal);

        return !stampedToday || habit.lastCelebratedTier < afterTier;
    }

    public static void StampCelebrated(HabitData habit, int tier, DateTime now)
    {
        if (habit == null)
        {
            return;
        }

        habit.lastCelebratedTier = tier;
        habit.lastCelebratedDate = HabitScheduling.ToKey(now);
    }
}
