using System;
using System.Collections.Generic;
using NUnit.Framework;

// UI katmaninin saf mantiginin EditMode testleri: gun indeksi donusumu, form
// dogrulamasi, izgara hucre eslemesi, gunluk ilerleme ve filtreler.
//
// Hicbiri sahne, Canvas ya da MonoBehaviour gerektirmiyor -- UI'nin sessizce
// yanlis yapabilecegi seyler burada, gorunur yerde tutuluyor.
public class HabitUiLogicTests
{
    // 1 Ocak 2024 bir PAZARTESI. Gun hesaplarinin sabit bir capasi olsun diye.
    private static readonly DateTime Monday = new DateTime(2024, 1, 1);

    private static HabitData MakeHabit(string id = "h1", params DayOfWeek[] frequency)
    {
        HabitData habit = new HabitData
        {
            id = id,
            title = "Test",
            habitType = HabitTypes.Build,
            categoryGroup = HabitCategories.Health,
            canvasSize = 16,
            activePaintingId = "p1",
            startDate = HabitScheduling.ToKey(new DateTime(2020, 1, 1))
        };

        for (int i = 0; i < frequency.Length; i++)
        {
            habit.frequency.Add(frequency[i]);
        }

        return habit;
    }

    // ---------------------------------------------------------------------
    // WeekDays -- 0=Pazartesi ile System.DayOfWeek (0=Pazar) arasindaki donusum
    // ---------------------------------------------------------------------

    [Test]
    public void WeekDays_MondayIndexRoundTrips()
    {
        for (int i = 0; i < WeekDays.Count; i++)
        {
            DayOfWeek day = WeekDays.ToDayOfWeek(i);
            Assert.AreEqual(i, WeekDays.ToMondayIndex(day), "Index " + i + " gidis-donuste bozuldu.");
        }
    }

    [Test]
    public void WeekDays_MondayIsZeroAndSundayIsSix()
    {
        Assert.AreEqual(DayOfWeek.Monday, WeekDays.ToDayOfWeek(0));
        Assert.AreEqual(DayOfWeek.Sunday, WeekDays.ToDayOfWeek(6));
        Assert.AreEqual(0, WeekDays.ToMondayIndex(DayOfWeek.Monday));
        Assert.AreEqual(6, WeekDays.ToMondayIndex(DayOfWeek.Sunday));
    }

    [Test]
    public void WeekDays_EmptyFrequencyMeansEveryDaySelected()
    {
        // HabitScheduling.IsScheduledOn ile ayni sozlesme: bos liste "her gun".
        List<DayOfWeek> empty = new List<DayOfWeek>();

        for (int i = 0; i < WeekDays.Count; i++)
        {
            Assert.IsTrue(WeekDays.IsSelected(empty, i));
        }

        List<DayOfWeek> weekdays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Wednesday };
        Assert.IsTrue(WeekDays.IsSelected(weekdays, 0));
        Assert.IsFalse(WeekDays.IsSelected(weekdays, 1));
        Assert.IsTrue(WeekDays.IsSelected(weekdays, 2));
    }

    // ---------------------------------------------------------------------
    // HabitForm
    // ---------------------------------------------------------------------

    [Test]
    public void HabitForm_BlankIsInvalid()
    {
        HabitForm form = new HabitForm();
        Assert.IsFalse(form.IsValid);
    }

    [Test]
    public void HabitForm_WhitespaceTitleIsInvalid()
    {
        HabitForm form = new HabitForm { Title = "   " };
        Assert.IsFalse(form.IsValid);

        form.Title = "  Su iç  ";
        Assert.IsTrue(form.IsValid);
        Assert.AreEqual("Su iç", form.TrimmedTitle);
    }

    [Test]
    public void HabitForm_UnparsableEndDateIsInvalidOnlyWhenEnabled()
    {
        HabitForm form = new HabitForm { Title = "Su iç", EndDate = "yarin" };
        Assert.IsTrue(form.IsValid, "Bitis tarihi kapaliyken metnin gecersizligi onemsiz.");

        form.HasEndDate = true;
        Assert.IsFalse(form.IsValid, "Kayda cozulemeyen bir tarih yazmak IsActiveOn'u bozar.");

        form.EndDate = "2026-12-31";
        Assert.IsTrue(form.IsValid);
    }

    [Test]
    public void HabitForm_LoadFrom_MapsFrequencyToMondayIndices()
    {
        HabitData habit = MakeHabit("h1", DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Friday);
        habit.title = "Yürüyüş";
        habit.canvasSize = 64;

        HabitForm form = new HabitForm();
        form.LoadFrom(habit);

        Assert.AreEqual("Yürüyüş", form.Title);
        Assert.AreEqual(64, form.CanvasSize);
        // Pazartesi 0, Cuma 4, Pazar 6 -- ve sirali.
        CollectionAssert.AreEqual(new[] { 0, 4, 6 }, form.Days);
    }

    [Test]
    public void HabitForm_FillFrequency_EmptySelectionStaysEmpty()
    {
        // Bos secim yedi gune ACILMAMALI: acilirsa "hic secilmemis" ile "yedisi de
        // secilmis" ayirt edilemez ve HabitScheduling'in "bos = her gun" kurali
        // tekrar duzenlemede sessizce kaybolur.
        HabitForm form = new HabitForm { Title = "Su iç" };
        List<DayOfWeek> result = new List<DayOfWeek> { DayOfWeek.Tuesday };

        form.FillFrequency(result);

        Assert.AreEqual(0, result.Count);
    }

    [Test]
    public void HabitForm_ToggleDay_KeepsDaysSortedAndUnique()
    {
        HabitForm form = new HabitForm();

        form.ToggleDay(4);
        form.ToggleDay(0);
        form.ToggleDay(2);
        form.ToggleDay(4);
        form.ToggleDay(4);

        CollectionAssert.AreEqual(new[] { 0, 2, 4 }, form.Days);
        Assert.IsTrue(form.IsDaySelected(2));
        Assert.IsFalse(form.IsDaySelected(1));
    }

    [Test]
    public void HabitForm_FillFrequency_ConvertsSelectedDays()
    {
        HabitForm form = new HabitForm { Title = "Kitap oku" };
        form.ToggleDay(0);
        form.ToggleDay(6);

        List<DayOfWeek> result = new List<DayOfWeek>();
        form.FillFrequency(result);

        CollectionAssert.AreEqual(new[] { DayOfWeek.Monday, DayOfWeek.Sunday }, result);
    }

    [Test]
    public void HabitForm_SetCanvasSize_ClearsPaintingSelection()
    {
        // Izgara yeni boyuta gore yeniden filtreleniyor: eski secim artik
        // ekranda gorunmeyen bir tabloyu isaret ederdi.
        HabitForm form = new HabitForm { CanvasSize = 16, PaintingId = "painting_214" };

        form.SetCanvasSize(32);

        Assert.AreEqual(32, form.CanvasSize);
        Assert.AreEqual(string.Empty, form.PaintingId);
    }

    [Test]
    public void HabitForm_SetCanvasSize_SameSizeKeepsSelection()
    {
        HabitForm form = new HabitForm { CanvasSize = 32, PaintingId = "painting_19" };

        form.SetCanvasSize(32);

        Assert.AreEqual("painting_19", form.PaintingId);
    }

    [Test]
    public void HabitForm_ResetAndLoadFrom_ClearPaintingSelection()
    {
        HabitForm form = new HabitForm { PaintingId = "painting_19" };
        form.Reset();
        Assert.AreEqual(string.Empty, form.PaintingId);

        // Duzenleme ekraninda izgara gizli (bkz. HabitSheet.Open); bayat bir
        // id kayda sessizce tasinmasin.
        form.PaintingId = "painting_19";
        HabitData habit = MakeHabit("h1", DayOfWeek.Monday);
        form.LoadFrom(habit);
        Assert.AreEqual(string.Empty, form.PaintingId);
    }

    // ---------------------------------------------------------------------
    // PaintingGrid -- uGUI ve PaintPixels ikisi de ust-soldan sayiyor
    // ---------------------------------------------------------------------

    [Test]
    public void PaintingGrid_RowsFollowPieceCount()
    {
        Assert.AreEqual(2, PaintingGrid.RowsFor(16));
        Assert.AreEqual(4, PaintingGrid.RowsFor(32));
        Assert.AreEqual(8, PaintingGrid.RowsFor(64));
    }

    [Test]
    public void PaintingGrid_TopLeftCellIsPieceZero()
    {
        // Sirali tablolarda ILK acilan parca 0'dir ve uGUI'nin ilk hucresinde
        // (sol ust) gorunmeli -- doldurma UST-SOLDAN baslar.
        Assert.AreEqual(0, PaintingGrid.PieceIndexForCell(0, 32));
        Assert.AreEqual(7, PaintingGrid.PieceIndexForCell(7, 32));
    }

    [Test]
    public void PaintingGrid_BottomLeftCellIsLastRowPiece()
    {
        // 32 parca = 8x4. Izgaranin son satirinin ilk hucresi (alt-sol) parca 24;
        // o satirin son hucresi (alt-sag) parca 31. Son acilan parca hep sag-altta.
        Assert.AreEqual(24, PaintingGrid.PieceIndexForCell(24, 32));
        Assert.AreEqual(31, PaintingGrid.PieceIndexForCell(31, 32));
        Assert.AreEqual(8, PaintingGrid.PieceIndexForCell(8, 16));
        Assert.AreEqual(15, PaintingGrid.PieceIndexForCell(15, 16));
    }

    [Test]
    public void PaintingGrid_CellIndexEqualsPieceIndex()
    {
        // Ozdeslik kasitli (bkz. dosya basligi): PaintingCoverGrid ve PaintPixels
        // ayni parcayi ayni yerde gostersin diye ikisi de ust-soldan sayiyor.
        int[] sizes = { 16, 32, 64 };

        for (int s = 0; s < sizes.Length; s++)
        {
            int size = sizes[s];
            for (int i = 0; i < size; i++)
            {
                Assert.AreEqual(i, PaintingGrid.PieceIndexForCell(i, size),
                    size + " parcada hucre " + i + " parca indeksiyle eslesmedi.");
                Assert.AreEqual(i, PaintingGrid.CellIndexForPiece(i, size),
                    size + " parcada parca " + i + " hucre indeksiyle eslesmedi.");
            }
        }
    }

    [Test]
    public void PaintingGrid_CellAndPieceRoundTrip()
    {
        int[] sizes = { 16, 32, 64 };

        for (int s = 0; s < sizes.Length; s++)
        {
            int size = sizes[s];
            for (int cell = 0; cell < size; cell++)
            {
                int piece = PaintingGrid.PieceIndexForCell(cell, size);
                Assert.AreEqual(cell, PaintingGrid.CellIndexForPiece(piece, size),
                    size + " parcada hucre " + cell + " gidis-donuste bozuldu.");
            }
        }
    }

    [Test]
    public void PaintingGrid_OutOfRangeReturnsMinusOne()
    {
        Assert.AreEqual(-1, PaintingGrid.PieceIndexForCell(-1, 32));
        Assert.AreEqual(-1, PaintingGrid.PieceIndexForCell(32, 32));
        Assert.AreEqual(-1, PaintingGrid.CellIndexForPiece(64, 32));
    }

    [Test]
    public void PaintPixels_PieceZeroIsTheTopTextureRow()
    {
        // PaintingGrid'in ust-sol = parca 0 kurali, PaintPixels'in texture satirina
        // cevirdigi deger ile uyusmali -- yoksa uGUI ortusu ile PaintPixels'in
        // render ettigi tablo birbirinden ayrisir (biri dikeyde aynalanir).
        int[] sizes = { 16, 32, 64 };

        for (int s = 0; s < sizes.Length; s++)
        {
            int size = sizes[s];
            int rows = PaintingGrid.RowsFor(size);

            for (int piece = 0; piece < size; piece++)
            {
                int cell = PaintingGrid.CellIndexForPiece(piece, size);
                int expectedTextureRow = rows - 1 - cell / PaintingGrid.Columns;

                Assert.AreEqual(expectedTextureRow, PaintPixels.TextureRowForPiece(piece, rows),
                    size + " parcada parca " + piece + " icin texture satiri uyusmadi.");
            }
        }

        // Uc kucuk kontrol: en ust satirdaki parcalar rows-1'e, en alttakiler 0'a dusmeli.
        Assert.AreEqual(1, PaintPixels.TextureRowForPiece(0, 2));
        Assert.AreEqual(0, PaintPixels.TextureRowForPiece(15, 2));
        Assert.AreEqual(7, PaintPixels.TextureRowForPiece(0, 8));
        Assert.AreEqual(0, PaintPixels.TextureRowForPiece(63, 8));
    }

    // ---------------------------------------------------------------------
    // DailyProgress
    // ---------------------------------------------------------------------

    [Test]
    public void DailyProgress_CountsOnlyDueHabits()
    {
        PlayerSaveData data = new PlayerSaveData();
        // Her gun planli, tikli.
        HabitData daily = MakeHabit("h1");
        daily.isCompletedToday = true;
        // Yalnizca sali planli: pazartesi sayilmamali.
        HabitData tuesdayOnly = MakeHabit("h2", DayOfWeek.Tuesday);
        // Her gun planli, tiksiz.
        HabitData pending = MakeHabit("h3");

        data.habits.Add(daily);
        data.habits.Add(tuesdayOnly);
        data.habits.Add(pending);

        DailyProgress.Count(data, Monday, out int done, out int total);

        Assert.AreEqual(2, total, "Pazartesi planli olmayan aliskanlik sayilmamali.");
        Assert.AreEqual(1, done);
        Assert.AreEqual(0.5f, DailyProgress.Ratio(data, Monday), 0.0001f);
    }

    [Test]
    public void DailyProgress_NoDueHabits_RatioIsZero()
    {
        PlayerSaveData data = new PlayerSaveData();
        data.habits.Add(MakeHabit("h1", DayOfWeek.Tuesday));

        DailyProgress.Count(data, Monday, out int done, out int total);

        Assert.AreEqual(0, total);
        Assert.AreEqual(0, done);
        Assert.AreEqual(0f, DailyProgress.Ratio(data, Monday), 0.0001f);
    }

    [Test]
    public void DailyProgress_AllDone_RatioIsOne()
    {
        PlayerSaveData data = new PlayerSaveData();
        for (int i = 0; i < 3; i++)
        {
            HabitData habit = MakeHabit("h" + i);
            habit.isCompletedToday = true;
            data.habits.Add(habit);
        }

        Assert.AreEqual(1f, DailyProgress.Ratio(data, Monday), 0.0001f);
    }

    // ---------------------------------------------------------------------
    // HabitFilters
    // ---------------------------------------------------------------------

    [Test]
    public void HabitFilters_MatchTypeAndCategory()
    {
        HabitData build = MakeHabit("h1");
        HabitData quit = MakeHabit("h2");
        quit.habitType = HabitTypes.Quit;
        quit.categoryGroup = HabitCategories.Mind;

        HabitFilters.HabitFilter healthFilter = new HabitFilters.HabitFilter(HabitFilters.Kind.Category, HabitCategories.Health);
        HabitFilters.HabitFilter mindFilter = new HabitFilters.HabitFilter(HabitFilters.Kind.Category, HabitCategories.Mind);

        Assert.IsTrue(HabitFilters.Matches(HabitFilters.HabitFilter.Build, build));
        Assert.IsFalse(HabitFilters.Matches(HabitFilters.HabitFilter.Quit, build));
        Assert.IsTrue(HabitFilters.Matches(HabitFilters.HabitFilter.Quit, quit));

        Assert.IsTrue(HabitFilters.Matches(healthFilter, build));
        Assert.IsFalse(HabitFilters.Matches(healthFilter, quit));
        Assert.IsTrue(HabitFilters.Matches(mindFilter, quit));
    }

    [Test]
    public void HabitFilters_AllMatchesEverythingAndCounts()
    {
        List<HabitData> habits = new List<HabitData>();
        habits.Add(MakeHabit("h1"));

        HabitData quit = MakeHabit("h2");
        quit.habitType = HabitTypes.Quit;
        habits.Add(quit);

        HabitFilters.HabitFilter healthFilter = new HabitFilters.HabitFilter(HabitFilters.Kind.Category, HabitCategories.Health);
        HabitFilters.HabitFilter mindFilter = new HabitFilters.HabitFilter(HabitFilters.Kind.Category, HabitCategories.Mind);

        Assert.AreEqual(2, HabitFilters.Count(HabitFilters.HabitFilter.All, habits));
        Assert.AreEqual(1, HabitFilters.Count(HabitFilters.HabitFilter.Build, habits));
        Assert.AreEqual(1, HabitFilters.Count(HabitFilters.HabitFilter.Quit, habits));
        Assert.AreEqual(2, HabitFilters.Count(healthFilter, habits));
        Assert.AreEqual(0, HabitFilters.Count(mindFilter, habits));
        Assert.AreEqual(0, HabitFilters.Count(HabitFilters.HabitFilter.All, null));
    }

    [Test]
    public void HabitFilters_TokenRoundTrips()
    {
        AssertFilterRoundTrips(HabitFilters.HabitFilter.All);
        AssertFilterRoundTrips(HabitFilters.HabitFilter.Build);
        AssertFilterRoundTrips(HabitFilters.HabitFilter.Quit);
        AssertFilterRoundTrips(new HabitFilters.HabitFilter(HabitFilters.Kind.Category, HabitCategories.Fitness));
    }

    private static void AssertFilterRoundTrips(HabitFilters.HabitFilter filter)
    {
        string token = HabitFilters.Token(filter);
        Assert.IsTrue(HabitFilters.TryParse(token, out HabitFilters.HabitFilter parsed), token + " cozulemedi");
        Assert.IsTrue(filter.Equals(parsed), token + " gidis-donuste degisti");
    }

    [Test]
    public void HabitFilters_TryParse_UnknownAtTokenFails()
    {
        // "@" ile baslayan bilinmeyen bir token bir kategori gibi
        // yorumlanmamali -- gelecekte eklenecek bir yerlesik bu yuzden eski
        // bir kayitta sessizce kategori sanilmiyor.
        Assert.IsFalse(HabitFilters.TryParse("@unknown", out _));
    }

    [Test]
    public void HabitFilters_Collect_AddsAllFirstDropsInvalidAndDuplicatesCapsAtMaxSlots()
    {
        List<string> tokens = new List<string>
        {
            HabitCategories.Fitness, "@unknown", HabitCategories.Fitness,
            HabitCategories.Health, HabitCategories.Art, HabitCategories.Social
        };

        List<HabitFilters.HabitFilter> result = new List<HabitFilters.HabitFilter>();
        HabitFilters.Collect(tokens, result);

        Assert.AreEqual(HabitFilters.MaxSlots, result.Count,
            "sonuc hicbir zaman bugunku elmas sayisini gecmemeli");
        Assert.AreEqual(HabitFilters.Kind.All, result[0].Kind, "Tumu her zaman ilk slot");
    }

    [Test]
    public void HabitFilters_Collect_NullTokensYieldsOnlyAll()
    {
        List<HabitFilters.HabitFilter> result = new List<HabitFilters.HabitFilter>();
        HabitFilters.Collect(null, result);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(HabitFilters.Kind.All, result[0].Kind);
    }

    // ---------------------------------------------------------------------
    // MoodScale -- enum sirasi (en iyiden kotuye) ile ekran sirasi (kotuden
    // iyiye) arasindaki donusum. Karistirmak yanlis dairenin yanmasina yol
    // acar ve hicbir sey patlamadigi icin hata sessiz kalir.
    // ---------------------------------------------------------------------

    [Test]
    public void MoodScale_OrderCoversEveryMood()
    {
        Assert.AreEqual(
            Enum.GetValues(typeof(PainterManager.PainterMoods)).Length,
            MoodScale.Order.Length,
            "Yeni bir mod eklenirse gostergedeki siraya da girmeli.");

        Assert.AreEqual(MoodScale.Order.Length, MoodScale.Count);
    }

    [Test]
    public void MoodScale_OrderRunsFromWorstToBest()
    {
        Assert.AreEqual(PainterManager.PainterMoods.Starve, MoodScale.Order[0],
            "Soldaki ilk daire en kotu mod olmali.");
        Assert.AreEqual(PainterManager.PainterMoods.UltraRich, MoodScale.Order[MoodScale.Count - 1],
            "Sagdaki son daire en iyi mod olmali.");
    }

    [Test]
    public void MoodScale_NoDuplicateMoodInOrder()
    {
        for (int i = 0; i < MoodScale.Order.Length; i++)
        {
            for (int j = i + 1; j < MoodScale.Order.Length; j++)
            {
                Assert.AreNotEqual(MoodScale.Order[i], MoodScale.Order[j],
                    "Ayni mod iki daireye dusemez.");
            }
        }
    }

    [Test]
    public void MoodScale_SlotRoundTripsForEveryMood()
    {
        Array moods = Enum.GetValues(typeof(PainterManager.PainterMoods));

        for (int i = 0; i < moods.Length; i++)
        {
            PainterManager.PainterMoods mood = (PainterManager.PainterMoods)moods.GetValue(i);
            int slot = MoodScale.SlotForMood(mood);

            Assert.GreaterOrEqual(slot, 0);
            Assert.Less(slot, MoodScale.Count);
            Assert.AreEqual(mood, MoodScale.MoodForSlot(slot), "Gidis-donus modu degistirmemeli.");
        }
    }

    [Test]
    public void MoodScale_OutOfRangeSlotFallsBackToNormal()
    {
        Assert.AreEqual(PainterManager.PainterMoods.Normal, MoodScale.MoodForSlot(-1));
        Assert.AreEqual(PainterManager.PainterMoods.Normal, MoodScale.MoodForSlot(MoodScale.Count));
    }

    // Mod tablosunun her puani gostergede bir daireye dusmeli: PainterManager
    // 0..MaxMoodPoint arasi her puanda bir mod uretiyor ve hicbiri ekranda
    // karsiliksiz kalmamali.
    [Test]
    public void MoodScale_EveryMoodPointMapsToASlot()
    {
        for (int point = PainterManager.MinMoodPoint; point <= PainterManager.MaxMoodPoint; point++)
        {
            PainterManager.PainterMoods mood = PainterManager.MoodForPoint(point);
            int slot = MoodScale.SlotForMood(mood);

            Assert.GreaterOrEqual(slot, 0, "Puan " + point + " icin daire yok.");
            Assert.Less(slot, MoodScale.Count, "Puan " + point + " icin daire yok.");
        }
    }
}
