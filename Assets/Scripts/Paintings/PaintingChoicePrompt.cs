using System;

// "Bu tile su an secim kutusunu mu bekliyor" karari -- iki tuketiciyi besliyor
// (tile'a dokunus kutuyu acsin mi, tile yanip sonsun mu). Tek kopya: aksi
// halde yanip sonen ama dokununca hicbir sey yapmayan bir kart ortaya cikardi.
public static class PaintingChoicePrompt
{
    public static bool IsPending(HabitData habit, DateTime now)
    {
        return habit != null
            && habit.awaitingPaintingChoice
            && !habit.isCompletedToday
            && HabitScheduling.IsDueOn(habit, now);
    }
}
