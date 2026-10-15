using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// Galeri ekraninin SAF mantik katmaninin EditMode testleri: GalleryWorks
// (hangi eserler hangi sirada), PaintingPieceState.ForFinished (bitmis bir
// tablonun gorunumu), FlowLayout (duvarin sarma + ortalama aritmetigi) ve
// GalleryBands.SlotWidth. Sahne, MonoBehaviour ya da Editor builder'i
// gerektirmiyor -- fixture'lar PaintingHoleTests/PaintingAssignmentTests'teki
// kalibin ayni kopyasi (suite'te zaten defalarca tekrarlanan desen).
public class GalleryLogicTests
{
    private static PaintingLibrary MakeLibrary(bool sequential, params (string id, int pieceCount)[] specs)
    {
        PaintingLibrary library = ScriptableObject.CreateInstance<PaintingLibrary>();
        List<PaintingDefinition> definitions = new List<PaintingDefinition>();

        for (int i = 0; i < specs.Length; i++)
        {
            PaintingDefinition definition = ScriptableObject.CreateInstance<PaintingDefinition>();
            definition.id = specs[i].id;
            definition.displayName = specs[i].id;
            definition.resolution = (PaintPixels.OverlayResolution)specs[i].pieceCount;
            definition.revealOrder = sequential
                ? PaintPixels.RevealOrder.Sequential
                : PaintPixels.RevealOrder.Random;
            definitions.Add(definition);
        }

        FieldInfo field = typeof(PaintingLibrary).GetField(
            "paintings",
            BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(library, definitions);

        library.BuildIndex();
        return library;
    }

    private static HabitData MakeHabit(int canvasSize = 8, string activePaintingId = "p0")
    {
        return new HabitData
        {
            id = "h1",
            title = "Test",
            habitType = HabitTypes.Build,
            canvasSize = canvasSize,
            activePaintingId = activePaintingId,
            startDate = HabitScheduling.ToKey(new DateTime(2020, 1, 1))
        };
    }

    // -----------------------------------------------------------------------
    // GalleryWorks.Collect
    // -----------------------------------------------------------------------

    [Test]
    public void Collect_ActiveFirst_ThenFinishedNewestToOldest()
    {
        PaintingLibrary library = MakeLibrary(false, ("p0", 8), ("p1", 8), ("p2", 8));
        HabitData habit = MakeHabit(activePaintingId: "p2");
        habit.randomOpenedPixels.AddRange(new[] { 0, 1 });
        habit.habitGallery.AddRange(new[] { "p0", "p1" }); // eskiden yeniye saklaniyor

        List<GalleryWork> buffer = new List<GalleryWork>();
        GalleryWorks.Collect(library, habit, buffer);

        Assert.AreEqual(3, buffer.Count);
        Assert.AreEqual("p2", buffer[0].Definition.id);
        Assert.IsFalse(buffer[0].IsFinished);
        Assert.AreEqual(2, buffer[0].OpenedCount);
        Assert.AreEqual(0, buffer[0].GalleryIndex);

        // Yeniden eskiye: once p1 (en son eklenen), sonra p0.
        Assert.AreEqual("p1", buffer[1].Definition.id);
        Assert.IsTrue(buffer[1].IsFinished);
        Assert.AreEqual(2, buffer[1].GalleryIndex);

        Assert.AreEqual("p0", buffer[2].Definition.id);
        Assert.AreEqual(1, buffer[2].GalleryIndex);
    }

    [Test]
    public void Collect_AwaitingChoice_SkipsActiveSlot()
    {
        PaintingLibrary library = MakeLibrary(false, ("p0", 8));
        HabitData habit = MakeHabit(activePaintingId: "p0");
        habit.awaitingPaintingChoice = true;
        habit.habitGallery.Add("p0");

        List<GalleryWork> buffer = new List<GalleryWork>();
        GalleryWorks.Collect(library, habit, buffer);

        Assert.AreEqual(1, buffer.Count, "Bekleyen secimde suren yuva ikinci kez gosterilmemeli.");
        Assert.IsTrue(buffer[0].IsFinished);
    }

    [Test]
    public void Collect_EmptyActivePaintingId_SkipsActiveSlot()
    {
        PaintingLibrary library = MakeLibrary(false, ("p0", 8));
        HabitData habit = MakeHabit(activePaintingId: "");
        habit.habitGallery.Add("p0");

        List<GalleryWork> buffer = new List<GalleryWork>();
        GalleryWorks.Collect(library, habit, buffer);

        Assert.AreEqual(1, buffer.Count);
        Assert.IsTrue(buffer[0].IsFinished);
    }

    [Test]
    public void Collect_UnresolvedGalleryId_IsSkippedButIndexSurvives()
    {
        PaintingLibrary library = MakeLibrary(false, ("p0", 8));
        HabitData habit = MakeHabit(activePaintingId: "");
        habit.habitGallery.AddRange(new[] { "missing", "p0" });

        List<GalleryWork> buffer = new List<GalleryWork>();
        GalleryWorks.Collect(library, habit, buffer);

        Assert.AreEqual(1, buffer.Count, "Kitapliktan kalkmis bir id butun galeriyi patlatmamali.");
        Assert.AreEqual("p0", buffer[0].Definition.id);
        Assert.AreEqual(2, buffer[0].GalleryIndex, "Index SAKLAMA sirasina gore (1-tabanli), atlanan kayit dahil.");
    }

    [Test]
    public void Collect_FinishedHoles_ComeFromGalleryHolesNotMissedPixels()
    {
        PaintingLibrary library = MakeLibrary(false, ("p0", 8));
        HabitData habit = MakeHabit(activePaintingId: "");
        HabitGallery.Record(habit, "p0", new List<int> { 2, 5 });

        List<GalleryWork> buffer = new List<GalleryWork>();
        GalleryWorks.Collect(library, habit, buffer);

        CollectionAssert.AreEqual(new[] { 2, 5 }, buffer[0].Holes);
        Assert.AreEqual(6, buffer[0].OpenedCount);
    }

    [Test]
    public void Collect_NoWorksAtAll_ReturnsEmptyBuffer()
    {
        PaintingLibrary library = MakeLibrary(false, ("p0", 8));
        HabitData habit = MakeHabit(activePaintingId: "");

        List<GalleryWork> buffer = new List<GalleryWork>();
        GalleryWorks.Collect(library, habit, buffer);

        Assert.AreEqual(0, buffer.Count);
    }

    // -----------------------------------------------------------------------
    // GalleryWorks.Totals / FinishedCount
    // -----------------------------------------------------------------------

    [Test]
    public void Totals_SumsOpenedAndHolesAcrossActiveAndFinished()
    {
        PaintingLibrary library = MakeLibrary(false, ("p0", 8), ("p1", 8));
        HabitData habit = MakeHabit(canvasSize: 8, activePaintingId: "p1");
        habit.randomOpenedPixels.AddRange(new[] { 0, 1, 2 }); // suren: 3 acik, yakilan yok
        HabitGallery.Record(habit, "p0", new List<int> { 4 }); // bitmis: 7 acik, 1 delik

        List<GalleryWork> scratch = new List<GalleryWork>();
        GalleryWorks.Totals(library, habit, scratch, out int days, out int missed);

        Assert.AreEqual(3 + 7, days);
        Assert.AreEqual(1, missed);
    }

    [Test]
    public void Totals_AwaitingChoice_DoesNotDoubleCountMissedPixels()
    {
        // TryCompleteActive habit.missedPixels'i SIFIRLAMAZ -- bitmis tablonun
        // delikleri hem galleryHoles'ta hem HALA missedPixels'te durur.
        // Totals bunu iki kez saymamali.
        PaintingLibrary library = MakeLibrary(false, ("p0", 4));
        HabitData habit = MakeHabit(canvasSize: 4, activePaintingId: "p0");
        habit.randomOpenedPixels.AddRange(new[] { 0, 1, 3 });
        habit.missedPixels.Add(2);

        Assert.IsTrue(PaintingAssignment.TryCompleteActive(library, habit, out _));

        List<GalleryWork> scratch = new List<GalleryWork>();
        GalleryWorks.Totals(library, habit, scratch, out int days, out int missed);

        Assert.AreEqual(3, days);
        Assert.AreEqual(1, missed, "Bekleyen secimdeki habit.missedPixels, galleryHoles'taki ile AYNI delik -- bir kez sayilmali.");
    }

    [Test]
    public void FinishedCount_ReturnsGalleryListLength()
    {
        HabitData habit = MakeHabit(activePaintingId: "");
        habit.habitGallery.AddRange(new[] { "p0", "p1" });

        Assert.AreEqual(2, GalleryWorks.FinishedCount(habit));
    }

    // -----------------------------------------------------------------------
    // GalleryWorks.ConsistencyStats
    // -----------------------------------------------------------------------

    [Test]
    public void ConsistencyStats_SplitsDaysAndMissedOverElapsedWindow()
    {
        string start = HabitScheduling.ToKey(new DateTime(2026, 1, 1));
        DateTime now = new DateTime(2026, 1, 11); // 10 gun gecti

        GalleryWorks.ConsistencyStats(start, now, days: 7, missed: 2, finishedCount: 2,
            out float completionRate, out float missRate, out float averageDaysPerPainting);

        Assert.AreEqual(70f, completionRate, 0.001f);
        Assert.AreEqual(20f, missRate, 0.001f);
        Assert.AreEqual(3.5f, averageDaysPerPainting, 0.001f);
    }

    [Test]
    public void ConsistencyStats_StartDateIsToday_ClampsElapsedToOneInsteadOfDividingByZero()
    {
        DateTime now = new DateTime(2026, 1, 1);
        string start = HabitScheduling.ToKey(now);

        GalleryWorks.ConsistencyStats(start, now, days: 1, missed: 0, finishedCount: 0,
            out float completionRate, out float missRate, out float _);

        Assert.AreEqual(100f, completionRate, 0.001f);
        Assert.AreEqual(0f, missRate, 0.001f);
    }

    [Test]
    public void ConsistencyStats_NoFinishedPaintings_AverageIsZero()
    {
        string start = HabitScheduling.ToKey(new DateTime(2026, 1, 1));
        DateTime now = new DateTime(2026, 1, 6);

        GalleryWorks.ConsistencyStats(start, now, days: 5, missed: 0, finishedCount: 0,
            out float _, out float _, out float averageDaysPerPainting);

        Assert.AreEqual(0f, averageDaysPerPainting);
    }

    [Test]
    public void ConsistencyStats_UnparsableStartDate_ReturnsZeroRatesButStillAverages()
    {
        GalleryWorks.ConsistencyStats(null, DateTime.Now, days: 9, missed: 1, finishedCount: 3,
            out float completionRate, out float missRate, out float averageDaysPerPainting);

        Assert.AreEqual(0f, completionRate);
        Assert.AreEqual(0f, missRate);
        Assert.AreEqual(3f, averageDaysPerPainting, 0.001f, "Ortalama gun/eser startDate'e bagli degil, elinde olan tek bilgiden hesaplanmali.");
    }

    // -----------------------------------------------------------------------
    // PaintingPieceState.ForFinished
    // -----------------------------------------------------------------------

    [Test]
    public void ForFinished_NonHolePieces_AreOpened_HolesAreMissed()
    {
        PaintingPieceState state = PaintingPieceState.ForFinished(8, new List<int> { 2, 5 });

        Assert.AreEqual(6, state.OpenedCount);
        Assert.AreEqual(PaintingPieceStatus.Missed, state.StatusOf(2));
        Assert.AreEqual(PaintingPieceStatus.Missed, state.StatusOf(5));
        Assert.AreEqual(PaintingPieceStatus.Opened, state.StatusOf(0));
        Assert.AreEqual(PaintingPieceStatus.Opened, state.StatusOf(7));
    }

    [Test]
    public void ForFinished_NoHoles_EveryPieceIsOpened()
    {
        PaintingPieceState state = PaintingPieceState.ForFinished(4, new List<int>());

        Assert.AreEqual(4, state.OpenedCount);
        for (int i = 0; i < 4; i++)
        {
            Assert.AreEqual(PaintingPieceStatus.Opened, state.StatusOf(i));
        }
    }

    // -----------------------------------------------------------------------
    // FlowLayout
    // -----------------------------------------------------------------------

    [Test]
    public void FlowLayout_SingleRow_CentersAroundAvailableWidth()
    {
        List<FlowItem> buffer = new List<FlowItem>();
        float height = FlowLayout.Solve(new[] { 50f, 50f }, 200f, 10f, 12f, 112f, buffer);

        // Satir genisligi 50+10+50=110; (200-110)/2 = 45 sol bosluk.
        Assert.AreEqual(45f, buffer[0].X, 0.001f);
        Assert.AreEqual(45f + 50f + 10f, buffer[1].X, 0.001f);
        Assert.AreEqual(0f, buffer[0].Y);
        Assert.AreEqual(112f, height, 0.001f);
    }

    [Test]
    public void FlowLayout_Overflow_WrapsToSecondRow()
    {
        List<FlowItem> buffer = new List<FlowItem>();
        float height = FlowLayout.Solve(new[] { 90f, 90f, 90f }, 200f, 10f, 12f, 112f, buffer);

        Assert.AreEqual(0, buffer[0].Row);
        Assert.AreEqual(0, buffer[1].Row);
        Assert.AreEqual(1, buffer[2].Row, "Ucuncu oge (90+10+90+10+90=290 > 200) ikinci satira tasmali.");
        Assert.AreEqual(112f + 12f, buffer[2].Y, 0.001f);
        Assert.AreEqual(112f * 2f + 12f, height, 0.001f);
    }

    [Test]
    public void FlowLayout_SingleOversizedItem_StillPlacedAlone()
    {
        List<FlowItem> buffer = new List<FlowItem>();
        FlowLayout.Solve(new[] { 500f }, 200f, 10f, 12f, 112f, buffer);

        Assert.AreEqual(1, buffer.Count, "Tek bir ogenin kendi genisligi tasmayi tetiklememeli (sonsuz bos satir riski).");
    }

    [Test]
    public void FlowLayout_EmptyList_ReturnsZeroHeight()
    {
        List<FlowItem> buffer = new List<FlowItem>();
        float height = FlowLayout.Solve(Array.Empty<float>(), 200f, 10f, 12f, 112f, buffer);

        Assert.AreEqual(0f, height);
        Assert.AreEqual(0, buffer.Count);
    }

    // -----------------------------------------------------------------------
    // GalleryBands
    // -----------------------------------------------------------------------

    [Test]
    public void SlotWidth_PicksLargestOfMinimumNativeAndPlaque()
    {
        Assert.AreEqual(84f, GalleryBands.SlotWidth(40f, 50f), "Her iki aday da asgarinin altindaysa asgari kazanir.");
        Assert.AreEqual(120f, GalleryBands.SlotWidth(120f, 50f), "Tablo plakadan genisse tablo kazanir.");
        Assert.AreEqual(140f, GalleryBands.SlotWidth(90f, 140f), "Plaka tablodan genisse plaka kazanir.");
    }

    // -----------------------------------------------------------------------
    // GalleryBands (Seviye 3)
    // -----------------------------------------------------------------------

    [Test]
    public void ViewerScale_PicksIntegerFloorOfTighterAxis()
    {
        // min(260/42, 290/55) = min(6.19, 5.27) -> floor(5.27) = 5.
        Assert.AreEqual(5, GalleryBands.ViewerScale(42f, 55f));
        // min(260/60, 290/46) = min(4.33, 6.30) -> floor(4.33) = 4.
        Assert.AreEqual(4, GalleryBands.ViewerScale(60f, 46f));
    }

    [Test]
    public void ViewerScale_NeverBelowOne_EvenForOversizedSprite()
    {
        Assert.AreEqual(1, GalleryBands.ViewerScale(400f, 400f), "Tam sayi kat hic sifira inmemeli.");
    }

    [Test]
    public void ViewerScale_InvalidSize_ReturnsOneInsteadOfDividingByZero()
    {
        Assert.AreEqual(1, GalleryBands.ViewerScale(0f, 42f));
        Assert.AreEqual(1, GalleryBands.ViewerScale(42f, 0f));
    }

    [Test]
    public void ThumbFit_PreservesAspectRatio_WithinThirtySixBox()
    {
        Vector2 fitted = GalleryBands.ThumbFit(42f, 55f);

        Assert.LessOrEqual(fitted.x, 36f + 0.001f);
        Assert.LessOrEqual(fitted.y, 36f + 0.001f);
        Assert.AreEqual(42f / 55f, fitted.x / fitted.y, 0.001f);
    }

    [Test]
    public void ThumbFit_InvalidSize_ReturnsZero()
    {
        Assert.AreEqual(Vector2.zero, GalleryBands.ThumbFit(0f, 10f));
    }

    // -----------------------------------------------------------------------
    // DateLabels -- ay kisaltmalari artik Loc.Tr uzerinden geliyor
    // (LOCALIZATION_PLAN.md Faz 5), bu yuzden her test kendi mini Turkce
    // tablosunu kurup TearDown'da birakiyor (Tests/LocalizationTests.cs'teki
    // AYNI "gecici tablo + TearDown'da Loc.Init(null)" kalibi).
    // -----------------------------------------------------------------------

    [TearDown]
    public void TearDownLocalization()
    {
        Loc.Init(null);
    }

    private static void InitTurkishMonths()
    {
        LocalizationTable table = ScriptableObject.CreateInstance<LocalizationTable>();
        string[] months = { "Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara" };
        int turkishSlot = AppLanguageNames.IndexOf(AppLanguage.Turkish);

        List<LocalizationEntry> entries = new List<LocalizationEntry>(months.Length);
        for (int i = 0; i < months.Length; i++)
        {
            LocalizationEntry entry = new LocalizationEntry { key = "month." + (i + 1) };
            entry.values[turkishSlot] = months[i];
            entries.Add(entry);
        }

        table.ReplaceAll(entries);
        Loc.Init(table);
    }

    [Test]
    public void Format_UsesTurkishMonthAbbreviation()
    {
        InitTurkishMonths();

        Assert.AreEqual("25 Ağu 2026", DateLabels.Format(new DateTime(2026, 8, 25)));
        Assert.AreEqual("1 Oca 2020", DateLabels.Format(new DateTime(2020, 1, 1)));
        Assert.AreEqual("31 Ara 2025", DateLabels.Format(new DateTime(2025, 12, 31)));
    }

    [Test]
    public void TryFormat_ValidKey_ReturnsTrueAndFormattedLabel()
    {
        InitTurkishMonths();

        bool ok = DateLabels.TryFormat(HabitScheduling.ToKey(new DateTime(2026, 3, 5)), out string label);

        Assert.IsTrue(ok);
        Assert.AreEqual("5 Mar 2026", label);
    }

    [Test]
    public void TryFormat_EmptyOrMalformedKey_ReturnsFalse()
    {
        Assert.IsFalse(DateLabels.TryFormat(null, out string a));
        Assert.IsFalse(DateLabels.TryFormat(string.Empty, out string b));
        Assert.IsFalse(DateLabels.TryFormat("not-a-date", out string c));
    }
}
