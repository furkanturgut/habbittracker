using System;
using System.Collections.Generic;

// Planli oldugu halde yapilmayan gunlerde tablonun parca kaybetmesi.
//
// MoodService.SettleClosedDays ile AYNI cizgide: son giristen bu yana kapanan
// gunleri gezer, planli ama tamamlanmamis her gunu sayar. Fark, mood TUM
// aliskanliklarin ortak "hepsi tamamlandi mi" sorusuna bakarken burasi TEK bir
// aliskanligin kendi planina bakiyor -- "o aliskanlik o gun yapilsaydi acilacak
// olan parca" sorusunun cevabi aliskanlik basina degisir.
//
// AYNI ANTI-CHEAT CIZGISI: saat geri alinmissa (now <= lastLogin) sifir doner.
//
// AYRI BIR "ISLENDI" DAMGASI YOK: cagiran taraf (HabbitManager) bunu sadece
// GERCEK bir gun donusunde (RefreshDayIfNeeded'in erken-cikis kontrolu gun
// degismediyse hic cagirmaz) ve lastLoginDate ilerletilmeden ONCE cagiriyor,
// yani ayni gun iki kez yakilamaz -- mood'un lastMoodSettledDate'e ihtiyac
// duymasinin sebebi (gun ICINDE de odul verebilmesi) burada yok.
public static class MissedPieceService
{
    // Bir aliskanlik icin: son giristen bu yana kac PLANLI gunu kacirildi?
    // Veriye DOKUNMAZ, yalnizca sayar.
    public static int CountMissedDueDays(HabitData habit, DateTime lastLogin, DateTime now)
    {
        if (now.Date <= lastLogin.Date)
        {
            return 0;
        }

        int missed = 0;

        // Son giris gunu icin tikler hala kayitta (ApplyDailyReset henuz
        // calismadi): gercek durumu goruyoruz.
        if (HabitScheduling.IsDueOn(habit, lastLogin) && !habit.isCompletedToday)
        {
            missed++;
        }

        // Aradaki gunlerde uygulama hic acilmadi, yani hicbir tik atilmadi.
        DateTime cursor = lastLogin.Date.AddDays(1);
        for (int i = 0; i < HabitScheduling.MaxCatchUpDays && cursor.Date < now.Date; i++)
        {
            if (HabitScheduling.IsDueOn(habit, cursor))
            {
                missed++;
            }

            cursor = cursor.AddDays(1);
        }

        return missed;
    }

    // Bir aliskanlik icin kacirilan gun sayisini parcaya cevirir: her kacirilan
    // gun icin bir parca yakar. Gercekten yakilan sayiyi doner -- tablo
    // tukenmisse (ya da secim bekliyorsa) bu, istenenden az olabilir.
    public static int Settle(PaintingLibrary library, HabitData habit, DateTime lastLogin, DateTime now, List<int> scratch)
    {
        int missedDays = CountMissedDueDays(habit, lastLogin, now);
        if (missedDays <= 0)
        {
            return 0;
        }

        bool sequential = PaintingAssignment.IsSequential(library, habit);
        int burned = 0;

        for (int i = 0; i < missedDays; i++)
        {
            if (PaintingProgress.MissPiece(habit, sequential, scratch) < 0)
            {
                // Yakacak parca kalmadi (tablo tukenmis ya da secim bekliyor).
                break;
            }

            burned++;
        }

        return burned;
    }

    // Kayittaki butun aliskanliklar icin kacirilan gunleri yakar. En az bir
    // parcasi yanan aliskanliklari affected'e ekler -- cagiran taraf bunlar
    // icin goruntuleri yeniden baglamali ve tamamlanmayi kontrol etmeli
    // (bkz. HabbitManager.RefreshDayIfNeeded).
    public static int SettleAll(
        PaintingLibrary library,
        PlayerSaveData data,
        DateTime now,
        List<int> scratch,
        List<HabitData> affected)
    {
        affected.Clear();

        // Ilk acilis: kiyaslanacak bir gecmis yok (MoodService.SettleClosedDays
        // ile ayni cizgi).
        if (!HabitScheduling.TryParse(data.lastLoginDate, out DateTime lastLogin))
        {
            return 0;
        }

        int totalBurned = 0;
        List<HabitData> habits = data.habits;

        for (int i = 0; i < habits.Count; i++)
        {
            HabitData habit = habits[i];
            int burned = Settle(library, habit, lastLogin, now, scratch);

            if (burned > 0)
            {
                totalBurned += burned;
                affected.Add(habit);
            }
        }

        return totalBurned;
    }
}
