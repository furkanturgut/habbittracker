using System;

// Ressamin mood puanini besleyen gunluk "tumu tamamlandi mi" kontrolu.
//
// DailyResetService / HabitScheduling ile ayni kalipta: static, MonoBehaviour
// yok, tarih disaridan veriliyor.
public static class MoodService
{
    // O tarihte tik atilabilir (planli + yururlukte) en az bir aliskanlik var mi?
    // Yoksa "izin gunu" demektir; odul de ceza de verilmemeli.
    public static bool HasAnyDueHabit(PlayerSaveData data, DateTime date)
    {
        for (int i = 0; i < data.habits.Count; i++)
        {
            if (HabitScheduling.IsDueOn(data.habits[i], date))
            {
                return true;
            }
        }

        return false;
    }

    // O tarihte planli TUM aliskanliklar tikli mi?
    //
    // Planli aliskanlik yoksa FALSE doner -- "yapilacak is yoktu" ile "hepsini
    // yapti" ayni sey degil. Cagiran taraf once HasAnyDueHabit ile bakmali.
    public static bool AreAllDueHabitsCompleted(PlayerSaveData data, DateTime date)
    {
        bool hasDue = false;

        for (int i = 0; i < data.habits.Count; i++)
        {
            HabitData habit = data.habits[i];
            if (!HabitScheduling.IsDueOn(habit, date))
            {
                continue;
            }

            hasDue = true;

            if (!habit.isCompletedToday)
            {
                return false;
            }
        }

        return hasDue;
    }

    // Son giristen bu yana kapanmis gunleri gezer ve her biri icin "planli bir sey
    // vardi ama tamamlanmadi" ise ceza sayar. Kac ceza cikardigini doner.
    //
    // DIKKAT: DailyResetService.ApplyDailyReset isCompletedToday alanlarini
    // sifirlar; bu fonksiyon MUTLAKA ondan ONCE cagrilmali, yoksa son giris
    // gununun tikleri kaybolmus olur.
    public static int SettleClosedDays(PlayerSaveData data, DateTime now)
    {
        if (!HabitScheduling.TryParse(data.lastLoginDate, out DateTime lastLogin))
        {
            // Ilk acilis: kiyaslanacak bir gecmis yok.
            return 0;
        }

        // Ayni gun ya da saat geri alinmis: DailyResetService'teki anti-cheat
        // kuraliyla ayni cizgi -- ceza da odul de yok.
        if (now.Date <= lastLogin.Date)
        {
            return 0;
        }

        int penalties = 0;

        // Son giris gunu icin tikler hala kayitta (ApplyDailyReset henuz
        // calismadi), gercek durumu goruyoruz. Zaten bu gun icin odul verilip
        // damgalanmissa (gun icinde hepsi tiklenmisse) atlanir.
        bool lastDayAlreadySettled = !string.IsNullOrEmpty(data.lastMoodSettledDate)
            && string.CompareOrdinal(data.lastMoodSettledDate, data.lastLoginDate) >= 0;

        if (!lastDayAlreadySettled
            && HasAnyDueHabit(data, lastLogin)
            && !AreAllDueHabitsCompleted(data, lastLogin))
        {
            penalties++;
        }

        // Aradaki gunlerde uygulama hic acilmadi (acilsaydi lastLoginDate
        // guncellenirdi), yani hicbir tik atilmadi. Planli bir sey varsa direkt
        // ceza; izin gunuyse atlanir.
        DateTime cursor = lastLogin.Date.AddDays(1);
        for (int i = 0; i < HabitScheduling.MaxCatchUpDays && cursor.Date < now.Date; i++)
        {
            if (HasAnyDueHabit(data, cursor))
            {
                penalties++;
            }

            cursor = cursor.AddDays(1);
        }

        data.lastMoodSettledDate = HabitScheduling.ToKey(now.Date.AddDays(-1));
        return penalties;
    }
}
