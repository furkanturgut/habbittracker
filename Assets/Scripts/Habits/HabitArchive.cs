using System;

// Aliskanlik arsivleme: karar burada, olay (HabitChanged/HabitListChanged) ve
// mood el degistirmesi HabbitManager.Archive.cs'te -- StreakFlame'in
// ShouldCelebrate/StampCelebrated ciftiyle ayni bolunme.
//
// Saf statik: sahne, MonoBehaviour ya da zaman kaynagi bagimliligi yok.
public static class HabitArchive
{
    // Arsivler. Seri, lastCompletedDate, tablo ilerlemesi, rotusProgress,
    // isCompletedToday -- hicbirine dokunulmuyor, hepsi donuyor. HabitScheduling.
    // IsDueOn arsivdeyken false dondugu icin mood/parca/alev tarafinda baska
    // hicbir sey yapmaya gerek yok.
    public static void Archive(HabitData habit)
    {
        habit.isArchived = true;
    }

    // Arsivden cikarir ve seriyi YENIDEN CIPALAR.
    //
    // NEDEN GEREKLI: TryGetPreviousDueDay IsActiveOn'u cagirmiyor (400 iterasyonluk
    // dongude bir kez cozulen baslangic/bitis sinirlarini iceride tutuyor), yani
    // arsiv boslugundan habersiz. isArchived kaldirildiktan sonraki ilk gun
    // donumunde BreakMissedStreaks "en yakin planli gun" diye arsiv icindeki bir
    // gunu bulur; lastCompletedDate o gunden ONCEYSE seri geriye donuk kirilir.
    // Cipalamak bunu onluyor -- DevHabitTools.SetStreak'in elle yazilan bir
    // seriyi ayakta tutmak icin kullandigi AYNI satir.
    //
    // Uc koruma: sifir seride uydurma bir tamamlama damgasi yazilmaz; damga
    // asla GERIYE tasinmaz (su an zaten daha yeniyse dokunulmaz); gecmiste hic
    // planli gun yoksa (ornegin startDate bugun) hicbir sey yazilmaz.
    public static void Unarchive(HabitData habit, DateTime now)
    {
        habit.isArchived = false;

        if (habit.currentStreak <= 0)
        {
            return;
        }

        if (!HabitScheduling.TryGetPreviousDueDay(habit, now, out DateTime previousDue))
        {
            return;
        }

        bool hasLastCompleted = HabitScheduling.TryParse(habit.lastCompletedDate, out DateTime lastCompleted);

        if (hasLastCompleted && lastCompleted.Date >= previousDue.Date)
        {
            return;
        }

        habit.lastCompletedDate = HabitScheduling.ToKey(previousDue);
    }
}
