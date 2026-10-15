using System;
using UnityEngine;

// Yeni gun kontrolu ve streak bakimi. Saf mantik: tarih disaridan veriliyor,
// boylece "dun" / "uc gun sonra" senaryolari sahne kurmadan test edilebiliyor.
public static class DailyResetService
{
    // Yeni gune gecildi mi? Sadece sorar, hicbir sey degistirmez -- reset
    // uygulamadan "gun dondu mu" bilmek isteyen taraf icin.
    public static bool IsNewDay(PlayerSaveData data, DateTime now)
    {
        return IsNewDay(data, HabitScheduling.ToKey(now));
    }

    // Gun anahtari zaten elde oldugunda kullanilan yol: karsilastirma tek yerde
    // kalirken ApplyDailyReset ToKey'i ikinci kez cagirmiyor.
    //
    // Ordinal karsilastirma: tarihler sabit formatta saklandigi icin parse
    // etmeye gerek yok, kulturden de bagimsiz.
    private static bool IsNewDay(PlayerSaveData data, string todayKey)
    {
        return !string.Equals(data.lastLoginDate, todayKey, StringComparison.Ordinal);
    }

    // Gun degistiyse tikleri sifirlar ve kacirilan planli gunlerde streak'i kirar.
    // Degisiklik yapildiysa true doner (cagiran taraf buna gore kaydeder).
    public static bool ApplyDailyReset(PlayerSaveData data, DateTime now)
    {
        string todayKey = HabitScheduling.ToKey(now);

        if (!IsNewDay(data, todayKey))
        {
            return false;
        }

        bool firstRun = !HabitScheduling.TryParse(data.lastLoginDate, out DateTime lastLogin);

        // Cihaz saati geri alinmis: ilerleme vermiyoruz, streak'i de kirmiyoruz.
        // Sadece damgayi guncelleyip cikiyoruz -- aksi halde kullanici saati
        // ileri/geri oynatarak streak'i istedigi gibi yonetebilirdi.
        bool clockWentBackwards = !firstRun && now.Date < lastLogin.Date;

        if (!firstRun && !clockWentBackwards)
        {
            BreakMissedStreaks(data, now);
        }

        for (int i = 0; i < data.habits.Count; i++)
        {
            data.habits[i].isCompletedToday = false;
        }

        data.lastLoginDate = todayKey;
        return true;
    }

    // Her aliskanlik icin: bugunden onceki en yakin PLANLI gun tamamlanmis mi?
    //
    // Planli olmayan gunler (ornegin hafta ici bir aliskanlikta pazar) streak'i
    // kirmaz -- kullanici o gun zaten yapmakla yukumlu degildi.
    private static void BreakMissedStreaks(PlayerSaveData data, DateTime now)
    {
        for (int i = 0; i < data.habits.Count; i++)
        {
            HabitData habit = data.habits[i];

            if (habit.currentStreak <= 0)
            {
                continue;
            }

            // Arsivdeki aliskanlik icin "kacirilan planli gun" hesaplanmaz --
            // seri dondurulmus sayilir (bkz. HabitArchive.Unarchive, ciplama
            // ile birlikte okunmali).
            if (habit.isArchived)
            {
                continue;
            }

            if (!HabitScheduling.TryGetPreviousDueDay(habit, now, out DateTime previousDue))
            {
                // Gecmiste hic planli gun yok: kiracak bir sey de yok.
                continue;
            }

            if (HabitScheduling.TryParse(habit.lastCompletedDate, out DateTime lastCompleted)
                && lastCompleted.Date >= previousDue.Date)
            {
                continue;
            }

            habit.currentStreak = 0;
        }
    }

    // Bir aliskanlik bugun tamamlandiginda streak'i ilerletir.
    //
    // Ardisiklik lastCompletedDate uzerinden dogrulaniyor, "dun acildi mi"
    // uzerinden degil: uygulamanin hic acilmadigi araliklar da dogru sonuc verir.
    public static void AdvanceStreak(HabitData habit, DateTime now)
    {
        bool continuesStreak =
            HabitScheduling.TryGetPreviousDueDay(habit, now, out DateTime previousDue)
            && HabitScheduling.TryParse(habit.lastCompletedDate, out DateTime lastCompleted)
            && lastCompleted.Date >= previousDue.Date;

        habit.currentStreak = continuesStreak ? habit.currentStreak + 1 : 1;
        habit.lastCompletedDate = HabitScheduling.ToKey(now);

        if (habit.currentStreak > habit.longestStreak)
        {
            habit.longestStreak = habit.currentStreak;
        }
    }

    // Tik geri alindiginda streak'i bir adim geri sarar.
    //
    // longestStreak bilerek DUSURULMUYOR: rekor gecmise ait bir olcum, bugunku
    // bir geri almayla silinmemeli.
    public static void RollBackStreak(HabitData habit, DateTime now)
    {
        if (habit.currentStreak > 0)
        {
            habit.currentStreak--;
        }

        // Son tamamlama damgasi bugunse geri alinmali; degilse zaten baska bir
        // gune ait, dokunmuyoruz.
        if (string.Equals(habit.lastCompletedDate, HabitScheduling.ToKey(now), StringComparison.Ordinal))
        {
            habit.lastCompletedDate = HabitScheduling.TryGetPreviousDueDay(habit, now, out DateTime previousDue)
                && habit.currentStreak > 0
                    ? HabitScheduling.ToKey(previousDue)
                    : null;
        }
    }

    // Bir sonraki gece yarisina kalan sure. Uygulama acikken gun degisimini
    // yakalamak icin.
    public static float SecondsUntilNextMidnight(DateTime now)
    {
        DateTime nextMidnight = now.Date.AddDays(1);
        double seconds = (nextMidnight - now).TotalSeconds;

        // Saat degisimlerinde (DST) negatife dusmesin.
        return Mathf.Max(1f, (float)seconds);
    }
}
