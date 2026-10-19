using UnityEngine;

// Gunluk tur sayisi: bir aliskanligin gunu kapanmasi icin kac tik gerektigi ve
// bugun kacinin atildigi. Saf mantik -- DateTime okumuyor, sahne gerektirmiyor.
//
// OTORITE BURADA DEGIL: gunun kapandigini hala yalnizca HabitData.
// isCompletedToday soyluyor (mood, streak, sovale sayaci, filtreler, galeri
// hepsi oraya bakiyor ve bu teslimde hicbiri degismedi). Bu sinif yalnizca "o
// bool'u ne zaman true yapmali" sorusunu cevapliyor.
//
// Degismeyen soz (butun yollar bunu korur):
//   isCompletedToday == true  ->  todayReps == Target
//   isCompletedToday == false ->  todayReps <  Target
public static class HabitReps
{
    public const int MinTarget = 1;

    // Formdaki adimlayicinin tasarlandigi tavan -- HabitFilters.MaxSlots gibi
    // bilincli bir sinir, yuvarlak bir sayi degil. Tek zorlama kapisi
    // ClampTarget; kayit onarimi (HabitSaveSystem.RepairHabits) ve ice aktarim
    // temizligi (SaveTransfer.Sanitize) de buradan geciyor.
    public const int MaxTarget = 10;

    public static int ClampTarget(int target)
    {
        return Mathf.Clamp(target, MinTarget, MaxTarget);
    }

    // Hedefin TEK okuma kapisi. Eski bir kayitta alan hic yoktu (JsonUtility
    // alan baslaticisini birakir, yani 1); elle bozulmus bir JSON'da 0 ya da
    // sacma bir sayi gelebilir -- ikisi de burada mesru bir hedefe duser, yani
    // cagiran hicbir yer habit.targetPerDay'i DOGRUDAN okumuyor.
    public static int Target(HabitData habit)
    {
        return habit == null ? MinTarget : ClampTarget(habit.targetPerDay);
    }

    // Hedef 1 ise bu ozellik bu aliskanlik icin yok sayilir: UI pili hic
    // gostermiyor ve dongu bugunku davranisin birebir aynisi oluyor.
    public static bool IsMulti(HabitData habit)
    {
        return Target(habit) > MinTarget;
    }

    // Ekranda gosterilecek sayi. Tamamlanmis gun her zaman hedefin kendisini
    // gosterir; tamamlanmamis gun en fazla "hedef - 1" gosterebilir (yukaridaki
    // degismeyen soz) -- boylece bozuk bir kayit bile "2/2 ama tik yok" gibi
    // okunmuyor.
    public static int DoneCount(HabitData habit)
    {
        if (habit == null)
        {
            return 0;
        }

        int target = Target(habit);
        return habit.isCompletedToday ? target : Mathf.Clamp(habit.todayReps, 0, target - 1);
    }

    // Bir tik isler. Bu tik gunu KAPATIYORSA true doner -- tamamlama kuyrugunu
    // (streak + parca + mood + rotus + kutlama) cagiran taraf o zaman
    // calistirir (bkz. HabbitManager.CompleteHabit). false donerse yapilan is
    // yalnizca sayaci ilerletmekti.
    //
    // Zaten tamamlanmis bir gunu cagiran taraf kendi kapisinda eliyor; buraya
    // gelse bile sayac hedefin uzerine cikamaz.
    public static bool Advance(HabitData habit)
    {
        if (habit == null)
        {
            return false;
        }

        int target = Target(habit);
        int next = DoneCount(habit) + 1;

        if (next >= target)
        {
            habit.todayReps = target;
            return true;
        }

        habit.todayReps = next;
        return false;
    }

    // Gun donusu ve tikin geri alinmasi: ikisi de sayaci SIFIRA cekiyor.
    // Geri almanin tek adim geri sarmamasi bilincli -- tamamlanmis bir gunu
    // geri almak streak/parca/mood/rotus'u tek seferde geri sariyor, sayaci
    // "hedef - 1"de birakmak o atomik geri almayi bolerdi (bkz.
    // DAILY_REPS_PLAN.md "dokunma dongusu").
    public static void Reset(HabitData habit)
    {
        if (habit != null)
        {
            habit.todayReps = 0;
        }
    }

    // Hedef duzenlemesi. Bugunku TAMAMLANMA durumuna bilerek dokunmuyor:
    // tamamlama kuyrugunu ikinci bir yerden cagirmak gerekirdi ve bir duzenleme
    // gecmisi geri almamali (frequency/endDate duzenlemelerinin bugune
    // dokunmamasiyla ayni sinif -- bkz. CLAUDE.md arsivleme kusuru).
    //
    //   hedef yukseldi + gun tamamlanmis -> tamamlanmis KALIR, sayac hedefe esitlenir
    //   hedef dustu   + gun tamamlanmamis -> fazla tiklar affedilir, gunu
    //                                        kapatmak icin bir dokunus kalir
    public static void ApplyTarget(HabitData habit, int target)
    {
        if (habit == null)
        {
            return;
        }

        int clamped = ClampTarget(target);
        habit.targetPerDay = clamped;
        habit.todayReps = habit.isCompletedToday
            ? clamped
            : Mathf.Clamp(habit.todayReps, 0, clamped - 1);
    }

    // Sayaci bozuk/elle duzenlenmis bir kayitta degismeyen soze geri getirir.
    // RepairHabits ve SaveTransfer.Sanitize ayni cevabi iki kez yazmasin diye
    // kural burada tek yerde duruyor.
    public static void Repair(HabitData habit)
    {
        if (habit == null)
        {
            return;
        }

        habit.targetPerDay = ClampTarget(habit.targetPerDay);
        habit.todayReps = DoneCount(habit);
    }

    // "1/2". Hedef 1 ise UI bunu hic cagirmiyor (IsMulti false), ama cagirsa da
    // dogru okur.
    public static string Label(HabitData habit)
    {
        return string.Format(Loc.Tr("habit.reps_progress"), DoneCount(habit), Target(habit));
    }
}
