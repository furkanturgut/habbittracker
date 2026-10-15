using System;
using System.Collections.Generic;
using UnityEngine;

// Aliskanlik duzeyindeki dev komutlari: bugunu tek tusla kapat/ac ve streak
// degerlerini elle yaz.
//
// Tikleme URETIMDEKI yoldan (CompleteHabit / UncompleteHabit) geciyor -- yani
// streak, tablo parcasi, mood odulu ve olaylar tam olarak gercek bir dokunustaki
// gibi isliyor. "isCompletedToday = true" yazmak daha kisa olurdu ama test
// ettigimiz sey artik uygulama olmazdi.
public static class DevHabitTools
{
    // Bugun planli olan her aliskanligi tikler. Tiklenebilen sayisini doner.
    public static int CompleteAllDue(HabbitManager manager)
    {
        if (manager == null)
        {
            return 0;
        }

        int count = 0;
        IReadOnlyList<HabitData> habits = manager.Habits;

        // Planli olmayan / zaten tikli olanlari CompleteHabit kendisi reddediyor;
        // burada ikinci bir kural yazmiyoruz.
        for (int i = 0; i < habits.Count; i++)
        {
            if (manager.CompleteHabit(habits[i].id))
            {
                count++;
            }
        }

        return count;
    }

    public static int UncompleteAll(HabbitManager manager)
    {
        if (manager == null)
        {
            return 0;
        }

        int count = 0;
        IReadOnlyList<HabitData> habits = manager.Habits;

        for (int i = 0; i < habits.Count; i++)
        {
            if (manager.UncompleteHabit(habits[i].id))
            {
                count++;
            }
        }

        return count;
    }

    // --- Saf kisim: streak degerleri + tarih damgasi ---

    // Streak'i yazar. longestStreak asla currentStreak'in altinda kalmaz
    // (DailyResetService.AdvanceStreak'teki kuralin aynisi).
    //
    // lastCompletedDate'i de yaziyoruz, cunku streak tek basina ayakta
    // DURAMIYOR: damgasiz bir streak'i bir sonraki gun donusunde
    // BreakMissedStreaks sifirlar ve "30 yazdim, sabah 0 oldu" gibi gorunurdu.
    // Damga, AdvanceStreak'in ardisiklik testini aynen gecen en gec tarih:
    //   bugun tikliyse  -> bugun,
    //   degilse         -> bugunden onceki en yakin PLANLI gun.
    public static void SetStreak(HabitData habit, int current, int longest, DateTime now)
    {
        if (habit == null)
        {
            return;
        }

        habit.currentStreak = Mathf.Max(0, current);
        habit.longestStreak = Mathf.Max(habit.currentStreak, Mathf.Max(0, longest));

        if (habit.currentStreak == 0)
        {
            habit.lastCompletedDate = null;
            return;
        }

        if (habit.isCompletedToday)
        {
            habit.lastCompletedDate = HabitScheduling.ToKey(now);
            return;
        }

        habit.lastCompletedDate = HabitScheduling.TryGetPreviousDueDay(habit, now, out DateTime previousDue)
            ? HabitScheduling.ToKey(previousDue)
            : HabitScheduling.ToKey(now);
    }

    // --- Sahneli kisim ---

    public static void SetStreak(HabbitManager manager, HabitData habit, int current, int longest)
    {
        if (manager == null || habit == null)
        {
            return;
        }

        SetStreak(habit, current, longest, AppClock.Now);
        manager.DevNotifyHabitChanged(habit);
    }
}
