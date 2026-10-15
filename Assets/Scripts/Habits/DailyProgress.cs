using System;

// "Bugun kac tanesi tamamlandi?" -- baslik sayaci ve sovale ayni sayidan beslensin
// diye tek yerde.
//
// Sayim yalnizca O GUN PLANLI aliskanliklari kapsiyor. Tum listeyi saymak, hafta
// ici bir aliskanligi olan kullanicinin sovalesinin pazar gunu asla dolmamasi
// demekti; ustelik MoodService de gunu ayni tanimla degerlendiriyor.
public static class DailyProgress
{
    public static void Count(PlayerSaveData data, DateTime date, out int done, out int total)
    {
        done = 0;
        total = 0;

        for (int i = 0; i < data.habits.Count; i++)
        {
            HabitData habit = data.habits[i];
            if (!HabitScheduling.IsDueOn(habit, date))
            {
                continue;
            }

            total++;
            if (habit.isCompletedToday)
            {
                done++;
            }
        }
    }

    // 0..1. Planli aliskanlik yoksa 0: "izin gunu" sovaleyi doldurmamali.
    public static float Ratio(PlayerSaveData data, DateTime date)
    {
        Count(data, date, out int done, out int total);
        return total > 0 ? (float)done / total : 0f;
    }
}
