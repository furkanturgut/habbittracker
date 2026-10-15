using System;
using System.Collections.Generic;

// Elle on aliskanlik girmeden ekranlari doldurmak icin ornek veri.
//
// Listede kasitli bir cesitlilik var: dort kategori, uc tuval boyutu, hem Build
// hem Quit, hem her gun hem secili gunler, bos/yarim/neredeyse dolu tablolar ve
// farkli streak'ler. Filtre satiri, kategori gruplari, izgara ve tablet
// yerlesimi ancak boyle bir listeyle gercekten sinanabiliyor.
//
// Olusturma URETIMDEKI yoldan (HabbitManager.CreateHabit) geciyor: tablo atamasi,
// mood geri alma ve olaylar gercek bir eklemedeki gibi isliyor.
public static class DevSampleHabits
{
    // Tohumun saf tanimi: sahne, manager ve Unity tipi yok -- testler bu diziyi
    // dogrudan okuyabiliyor.
    public readonly struct SampleHabit
    {
        public readonly string Title;
        public readonly string Category;
        public readonly string Type;
        public readonly int CanvasSize;

        // Pazartesi-basli UI indeksleri (0 = Pt). null => her gun
        // (HabitScheduling'in bos frequency kurali).
        public readonly int[] Days;

        public readonly int Streak;

        // Aktif tabloda acilacak oran (0..1).
        public readonly float Progress;

        public SampleHabit(string title, string category, string type, int canvasSize, int[] days, int streak, float progress)
        {
            Title = title;
            Category = category;
            Type = type;
            CanvasSize = canvasSize;
            Days = days;
            Streak = streak;
            Progress = progress;
        }
    }

    // startDate bu kadar gun geriye cekiliyor. SART: CreateHabit baslangici
    // BUGUN yapiyor ve gecmiste hic planli gun olmayan bir aliskanlikta
    // TryGetPreviousDueDay false doner -- yani verdigimiz streak ilk tikte 1'e
    // duserdi. 60 gun her program icin (hafta sonu dahil) fazlasiyla gecmis.
    public const int StartDateDaysBack = 60;

    private static readonly int[] WeekdaysOnly = { 0, 1, 2, 3, 4 };
    private static readonly int[] Weekend = { 5, 6 };
    private static readonly int[] MonWedFri = { 0, 2, 4 };

    // Kategori 4'ten 12'ye cikinca (Beslenme/Finans/Fitness/Sanat/Saglik/
    // Sosyal/Calisma/Is/Diger/Sabah/Gun/Aksam) liste de her birini kapsayacak
    // sekilde buyudu -- DevToolsTests.DevSampleHabits_All_CoverEveryCategoryAndCanvasSize
    // bunu zorluyor.
    public static readonly SampleHabit[] All =
    {
        new SampleHabit("Su iç", HabitCategories.Nutrition, HabitTypes.Build, 16, null, 12, 0.9f),
        new SampleHabit("Bütçe takip et", HabitCategories.Finance, HabitTypes.Build, 16, MonWedFri, 4, 0.4f),
        new SampleHabit("Spor yap", HabitCategories.Fitness, HabitTypes.Build, 32, MonWedFri, 5, 0.5f),
        new SampleHabit("Resim çiz", HabitCategories.Art, HabitTypes.Build, 32, Weekend, 2, 0.2f),
        new SampleHabit("Sigara yok", HabitCategories.Health, HabitTypes.Quit, 64, null, 21, 0.95f),
        new SampleHabit("Arkadaşını ara", HabitCategories.Social, HabitTypes.Build, 16, Weekend, 1, 0.1f),
        new SampleHabit("Kitap oku", HabitCategories.Study, HabitTypes.Build, 64, null, 3, 0.25f),
        new SampleHabit("Günlük yaz", HabitCategories.Work, HabitTypes.Build, 32, null, 7, 0.6f),
        new SampleHabit("Şeker yok", HabitCategories.Other, HabitTypes.Quit, 16, null, 0, 0f),
        new SampleHabit("Erken kalk", HabitCategories.Morning, HabitTypes.Build, 16, WeekdaysOnly, 1, 0.1f),
        new SampleHabit("Meditasyon", HabitCategories.Day, HabitTypes.Build, 32, WeekdaysOnly, 0, 0f),
        new SampleHabit("Esneme yap", HabitCategories.Evening, HabitTypes.Build, 16, null, 6, 0.55f)
    };

    // Her cagride yeni liste ayirmamak icin: CreateHabit listeyi kopyaliyor.
    private static readonly List<DayOfWeek> frequencyBuffer = new List<DayOfWeek>(7);

    // Listedeki aliskanliklari ekler ve kac tane eklendigini doner. Ayni basliga
    // sahip bir aliskanlik varsa ATLANIR: butona iki kez basmak listeyi
    // ikiye katlamasin.
    public static int Create(HabbitManager manager)
    {
        if (manager == null)
        {
            return 0;
        }

        DateTime backdated = AppClock.Now.Date.AddDays(-StartDateDaysBack);
        int created = 0;

        for (int i = 0; i < All.Length; i++)
        {
            SampleHabit sample = All[i];
            if (Exists(manager, sample.Title))
            {
                continue;
            }

            HabitData habit = manager.CreateHabit(
                sample.Title,
                sample.Category,
                sample.Type,
                BuildFrequency(sample.Days),
                sample.CanvasSize);

            if (habit == null)
            {
                continue;
            }

            // Gecmisi olmayan bir aliskanlikta streak ayakta kalmaz (yukaridaki
            // StartDateDaysBack notu). UpdateHabit startDate'e bilerek
            // dokunmuyor, bu yuzden alan burada dogrudan yaziliyor.
            habit.startDate = HabitScheduling.ToKey(backdated);

            DevHabitTools.SetStreak(manager, habit, sample.Streak, sample.Streak);
            DevPaintingTools.SetOpenedRatio(manager, habit, sample.Progress);

            created++;
        }

        if (created > 0)
        {
            manager.DevNotifyListChanged();
        }

        return created;
    }

    private static List<DayOfWeek> BuildFrequency(int[] mondayIndices)
    {
        frequencyBuffer.Clear();

        if (mondayIndices == null)
        {
            // Bos liste "her gun" demek; gunleri tek tek eklemekle ayni sonuc
            // ama kullanicinin formda gordugu durumun aynisi.
            return frequencyBuffer;
        }

        for (int i = 0; i < mondayIndices.Length; i++)
        {
            frequencyBuffer.Add(WeekDays.ToDayOfWeek(mondayIndices[i]));
        }

        return frequencyBuffer;
    }

    private static bool Exists(HabbitManager manager, string title)
    {
        IReadOnlyList<HabitData> habits = manager.Habits;
        for (int i = 0; i < habits.Count; i++)
        {
            if (string.Equals(habits[i].title, title, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
