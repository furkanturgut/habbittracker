// Aliskanlik basina 7 tiklik rotus dongusu. Takvim haftasindan BILEREK bagimsiz --
// WeekDays'in kendi basligi "WeekStart yalnizca EKRANDA ne gosterildigini
// degistirir, KAYIT hep Pazartesi-indeksli kalir" diyor; takvim haftasina bagli
// bir odul, Ayarlar'daki gorsel bir tercihi ilk kez kayit mantigina sizdirirdi.
//
// Saf statik: sahne, MonoBehaviour ya da zaman kaynagi bagimliligi yok. Karar
// burada, olay (RotusEarned/RotusChanged) HabbitManager'da -- StreakFlame'in
// ShouldCelebrate/StampCelebrated ciftiyle ayni bolunme.
public static class RotusCycle
{
    public const int CycleLength = 7;

    // Dongude su an kactik (0..CycleLength-1).
    public static int Progress(HabitData habit)
    {
        return habit.rotusProgress;
    }

    // Bir tik: sayaci bir ilerletir. Donguyu TAMAMLADIYSA (7. tik) true doner ve
    // sayac 0'a sarar -- cagiran taraf bunu "bir rotus kazanildi" olarak okur.
    public static bool Advance(HabitData habit)
    {
        habit.rotusProgress++;

        if (habit.rotusProgress >= CycleLength)
        {
            habit.rotusProgress = 0;
            return true;
        }

        return false;
    }

    // Bir tikin geri alinmasi: sayaci bir geri alir. Sayac 0 iken cagrilmak SARMA
    // demektir -- UncompleteHabit yalnizca isCompletedToday iken calisir, yani o
    // tik donguyu mutlaka ilerletmisti (ya normal artisla ya da donguyu doldurup
    // sarmayla). CycleLength-1'e doner ve true der: cagiran taraf bunu "kazanilan
    // odulu geri al" olarak okur.
    public static bool RollBack(HabitData habit)
    {
        if (habit.rotusProgress <= 0)
        {
            habit.rotusProgress = CycleLength - 1;
            return true;
        }

        habit.rotusProgress--;
        return false;
    }

    // Pill/ilerleme gostergesi icin: bir sonraki rotusa kac tik kaldi.
    public static int TicksToNext(HabitData habit)
    {
        return CycleLength - habit.rotusProgress;
    }
}
