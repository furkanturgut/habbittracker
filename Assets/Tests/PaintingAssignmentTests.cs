using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// Tablo siparisi mantiginin EditMode testleri.
//
// Bu kurallar daha once HabbitManager'in icindeydi ve test edilemiyordu: galeriye
// bir kez ekleme, canvasSize'in secilen tabloya yeniden hizalanmasi, acilista
// bozuk siparislerin onarilmasi. Ucu de kayitla gorunen tablonun ayrisabildigi
// noktalar.
public class PaintingAssignmentTests
{
    private static PaintingLibrary MakeLibrary(int count, int pieceCount, bool sequential = false)
    {
        PaintingLibrary library = ScriptableObject.CreateInstance<PaintingLibrary>();
        List<PaintingDefinition> definitions = new List<PaintingDefinition>();

        for (int i = 0; i < count; i++)
        {
            PaintingDefinition definition = ScriptableObject.CreateInstance<PaintingDefinition>();
            definition.id = "p" + i;
            definition.resolution = (PaintPixels.OverlayResolution)pieceCount;
            definition.revealOrder = sequential
                ? PaintPixels.RevealOrder.Sequential
                : PaintPixels.RevealOrder.Random;
            definitions.Add(definition);
        }

        // paintings alani private [SerializeField]; Editor API'sine bagimli
        // kalmadan doldurmanin yolu yansima.
        FieldInfo field = typeof(PaintingLibrary).GetField(
            "paintings",
            BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(library, definitions);

        library.BuildIndex();
        return library;
    }

    private static HabitData MakeHabit(int canvasSize = 16, string activePaintingId = "p0")
    {
        return new HabitData
        {
            id = "h1",
            title = "Test",
            habitType = HabitTypes.Build,
            canvasSize = canvasSize,
            activePaintingId = activePaintingId,
            startDate = HabitScheduling.ToKey(new System.DateTime(2020, 1, 1))
        };
    }

    private static void FillPainting(HabitData habit, int pieceCount)
    {
        for (int i = 0; i < pieceCount; i++)
        {
            habit.randomOpenedPixels.Add(i);
        }
    }

    private struct MixedEntry
    {
        public string Id;
        public int PieceCount;
    }

    // ForSize testleri icin boyutlari KARISIK bir havuz kuran yardimci.
    // MakeLibrary tek boyut uretiyor, ForSize'in "sadece tam eslesen kova"
    // kuralini sinamak icin birden fazla boyut gerekiyor.
    private static PaintingLibrary MakeMixedLibrary(params MixedEntry[] entries)
    {
        PaintingLibrary library = ScriptableObject.CreateInstance<PaintingLibrary>();
        List<PaintingDefinition> definitions = new List<PaintingDefinition>();

        for (int i = 0; i < entries.Length; i++)
        {
            PaintingDefinition definition = ScriptableObject.CreateInstance<PaintingDefinition>();
            definition.id = entries[i].Id;
            definition.resolution = (PaintPixels.OverlayResolution)entries[i].PieceCount;
            definitions.Add(definition);
        }

        FieldInfo field = typeof(PaintingLibrary).GetField(
            "paintings",
            BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(library, definitions);

        library.BuildIndex();
        return library;
    }

    [Test]
    public void AssignNext_ResyncsCanvasSizeToPickedPainting()
    {
        // Havuzda yalnizca 64 parcalik tablo var, aliskanlik 16 istiyor: PickNext
        // en yakin boyuta duser ve canvasSize ona gore duzeltilmeli. Duzeltilmezse
        // "tablo bitti" esigi (16) hic yakalanmaz.
        PaintingLibrary library = MakeLibrary(2, 64);
        HabitData habit = MakeHabit(canvasSize: 16);

        UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
        Assert.IsTrue(PaintingAssignment.AssignNext(library, habit));
        UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;

        Assert.AreEqual(64, habit.canvasSize, "canvasSize secilen tablonun parca sayisina esitlenmeli.");
    }

    [Test]
    public void AssignNext_ResetsProgress()
    {
        PaintingLibrary library = MakeLibrary(3, 16);
        HabitData habit = MakeHabit();
        habit.totalPixelsPainted = 9;
        habit.randomOpenedPixels.AddRange(new[] { 1, 2, 3 });

        PaintingAssignment.AssignNext(library, habit);

        Assert.AreEqual(0, habit.totalPixelsPainted);
        Assert.AreEqual(0, habit.randomOpenedPixels.Count);
    }

    [Test]
    public void AssignNext_NoLibrary_ReturnsFalseAndKeepsData()
    {
        HabitData habit = MakeHabit();
        habit.totalPixelsPainted = 5;

        Assert.IsFalse(PaintingAssignment.AssignNext(null, habit));
        Assert.AreEqual("p0", habit.activePaintingId, "Havuz yoksa siparis degismemeli.");
        Assert.AreEqual(5, habit.totalPixelsPainted, "Havuz yoksa ilerleme silinmemeli.");
    }

    [Test]
    public void IsSequential_UnknownPainting_FallsBackToRandom()
    {
        // Tablo cozulemezse Random varsayilmali: indeks listesi hem sirali hem
        // rastgele ilerlemeyi tasiyabilir, tek sayi tasiyamaz.
        PaintingLibrary library = MakeLibrary(1, 16, sequential: true);
        HabitData habit = MakeHabit(activePaintingId: "yok");

        Assert.IsFalse(PaintingAssignment.IsSequential(library, habit));
        Assert.IsFalse(PaintingAssignment.IsSequential(null, habit));
    }

    [Test]
    public void TryCompleteActive_IncompletePainting_DoesNothing()
    {
        PaintingLibrary library = MakeLibrary(3, 16);
        HabitData habit = MakeHabit();
        habit.randomOpenedPixels.Add(0);

        Assert.IsFalse(PaintingAssignment.TryCompleteActive(library, habit, out string finishedId));
        Assert.IsNull(finishedId);
        Assert.AreEqual("p0", habit.activePaintingId);
        Assert.AreEqual(0, habit.habitGallery.Count);
    }

    [Test]
    public void TryCompleteActive_AddsToGalleryButKeepsPaintingAndSetsFlag()
    {
        PaintingLibrary library = MakeLibrary(3, 16);
        HabitData habit = MakeHabit();
        FillPainting(habit, 16);

        Assert.IsTrue(PaintingAssignment.TryCompleteActive(library, habit, out string finishedId));
        Assert.AreEqual("p0", finishedId);
        CollectionAssert.AreEqual(new[] { "p0" }, habit.habitGallery);

        // Tablo ekranda tam haliyle kalmali: yeni tablo atanmaz, ilerleme dolu kalir.
        Assert.AreEqual("p0", habit.activePaintingId);
        Assert.AreEqual(16, habit.randomOpenedPixels.Count, "Ilerleme SIFIRLANMAMALI -- tablo dolu durmali.");
        Assert.IsTrue(habit.awaitingPaintingChoice);

        // IDEMPOTENT: ayni aliskanligin ikinci goruntusu de tamamlandi bildirir.
        // Bayrak acik oldugu icin ikincisi bastaki guard'da false donmeli.
        Assert.IsFalse(PaintingAssignment.TryCompleteActive(library, habit, out _));
        Assert.AreEqual(1, habit.habitGallery.Count, "Galeriye ikinci kez eklenmemeli.");
    }

    [Test]
    public void RepairAll_MissingPainting_AssignsNew()
    {
        PaintingLibrary library = MakeLibrary(3, 16);
        PlayerSaveData data = new PlayerSaveData();
        HabitData habit = MakeHabit(activePaintingId: "havuzdan-kalkmis");
        data.habits.Add(habit);

        Assert.IsTrue(PaintingAssignment.RepairAll(library, data));
        Assert.IsTrue(library.TryGet(habit.activePaintingId, out _), "Yeni tablo havuzdan gelmeli.");
        Assert.IsFalse(habit.awaitingPaintingChoice, "Atama yapildi, bekleme olmamali.");
    }

    [Test]
    public void RepairAll_CanvasSizeMismatch_RealignsWithoutReassigning()
    {
        PaintingLibrary library = MakeLibrary(3, 32);
        PlayerSaveData data = new PlayerSaveData();
        HabitData habit = MakeHabit(canvasSize: 16);
        habit.randomOpenedPixels.Add(4);
        data.habits.Add(habit);

        Assert.IsTrue(PaintingAssignment.RepairAll(library, data));
        Assert.AreEqual(32, habit.canvasSize);
        Assert.AreEqual("p0", habit.activePaintingId, "Tablo gecerliyken siparis degismemeli.");
        CollectionAssert.AreEqual(new[] { 4 }, habit.randomOpenedPixels, "Ilerleme korunmali.");
    }

    [Test]
    public void RepairAll_FinishedPainting_CompletesAndHolds()
    {
        // Uygulama kutlama sirasinda oldurulmus: kayit dolu bir tabloyla aciliyor.
        // Bu artik NORMAL bir durum -- bayrak acilir, yeni tablo atanmaz.
        PaintingLibrary library = MakeLibrary(3, 16);
        PlayerSaveData data = new PlayerSaveData();
        HabitData habit = MakeHabit();
        FillPainting(habit, 16);
        data.habits.Add(habit);

        Assert.IsTrue(PaintingAssignment.RepairAll(library, data));
        CollectionAssert.AreEqual(new[] { "p0" }, habit.habitGallery);
        Assert.AreEqual(16, habit.randomOpenedPixels.Count, "Ilerleme dolu kalmali.");
        Assert.IsTrue(habit.awaitingPaintingChoice);
    }

    [Test]
    public void RepairAll_AlreadyAwaitingChoice_DoesNotChange()
    {
        PaintingLibrary library = MakeLibrary(3, 16);
        PlayerSaveData data = new PlayerSaveData();
        HabitData habit = MakeHabit();
        FillPainting(habit, 16);
        habit.awaitingPaintingChoice = true;
        habit.habitGallery.Add("p0");
        data.habits.Add(habit);

        Assert.IsFalse(PaintingAssignment.RepairAll(library, data), "Beklemedeki habit zaten saglam.");
        Assert.AreEqual("p0", habit.activePaintingId);
        CollectionAssert.AreEqual(new[] { "p0" }, habit.habitGallery);
    }

    [Test]
    public void RepairAll_MissingPaintingWhileAwaitingChoice_DropsFlagAfterAssigning()
    {
        // Tablo kitaplikta kalkmis, habit halen bekleme bayragiyla acilmis:
        // eski/gecersiz durumdan cikis yeni atamayla olmali.
        PaintingLibrary library = MakeLibrary(3, 16);
        PlayerSaveData data = new PlayerSaveData();
        HabitData habit = MakeHabit(activePaintingId: "havuzdan-kalkmis");
        habit.awaitingPaintingChoice = true;
        data.habits.Add(habit);

        Assert.IsTrue(PaintingAssignment.RepairAll(library, data));
        Assert.IsTrue(library.TryGet(habit.activePaintingId, out _));
        Assert.IsFalse(habit.awaitingPaintingChoice);
    }

    [Test]
    public void RepairAll_HealthyHabit_ReportsNoChange()
    {
        PaintingLibrary library = MakeLibrary(3, 16);
        PlayerSaveData data = new PlayerSaveData();
        data.habits.Add(MakeHabit());

        Assert.IsFalse(PaintingAssignment.RepairAll(library, data),
            "Saglam kayitta gereksiz yazma tetiklenmemeli.");
    }

    [Test]
    public void Assign_SetsPaintingAndResyncsCanvasSize()
    {
        PaintingLibrary library = MakeLibrary(3, 64);
        HabitData habit = MakeHabit(canvasSize: 16, activePaintingId: "p0");

        Assert.IsTrue(PaintingAssignment.Assign(library, habit, "p2"));
        Assert.AreEqual("p2", habit.activePaintingId);
        Assert.AreEqual(64, habit.canvasSize, "canvasSize secilen tablonun parca sayisina esitlenmeli.");
    }

    [Test]
    public void Assign_ResetsProgress()
    {
        PaintingLibrary library = MakeLibrary(3, 16);
        HabitData habit = MakeHabit();
        FillPainting(habit, 16);

        Assert.IsTrue(PaintingAssignment.Assign(library, habit, "p1"));
        Assert.AreEqual(0, habit.totalPixelsPainted);
        Assert.AreEqual(0, habit.randomOpenedPixels.Count);
    }

    [Test]
    public void Assign_UnknownId_ReturnsFalseAndKeepsHabitUntouched()
    {
        // AssignNext'e dusebilsin diye: cozulemeyen id habit'e DOKUNMAMALI.
        PaintingLibrary library = MakeLibrary(3, 16);
        HabitData habit = MakeHabit();
        habit.totalPixelsPainted = 5;

        Assert.IsFalse(PaintingAssignment.Assign(library, habit, "yok"));
        Assert.AreEqual("p0", habit.activePaintingId);
        Assert.AreEqual(16, habit.canvasSize);
        Assert.AreEqual(5, habit.totalPixelsPainted);
    }

    [Test]
    public void Assign_NullLibraryOrEmptyId_IsNoOp()
    {
        PaintingLibrary library = MakeLibrary(3, 16);
        HabitData habit = MakeHabit();

        Assert.IsFalse(PaintingAssignment.Assign(null, habit, "p1"));
        Assert.IsFalse(PaintingAssignment.Assign(library, habit, string.Empty));
        Assert.IsFalse(PaintingAssignment.Assign(library, habit, null));
        Assert.AreEqual("p0", habit.activePaintingId);
    }

    [Test]
    public void ChooseNext_ExplicitId_AssignsAndDropsFlag()
    {
        PaintingLibrary library = MakeLibrary(3, 16);
        HabitData habit = MakeHabit();
        FillPainting(habit, 16);
        habit.awaitingPaintingChoice = true;
        habit.habitGallery.Add("p0");

        Assert.IsTrue(PaintingAssignment.ChooseNext(library, habit, "p1"));
        Assert.AreEqual("p1", habit.activePaintingId);
        Assert.AreEqual(0, habit.randomOpenedPixels.Count);
        Assert.IsFalse(habit.awaitingPaintingChoice);
    }

    [Test]
    public void ChooseNext_EmptyId_FallsBackToAssignNext()
    {
        // "" = Rastgele karesi secildi.
        PaintingLibrary library = MakeLibrary(3, 16);
        HabitData habit = MakeHabit();
        habit.awaitingPaintingChoice = true;

        Assert.IsTrue(PaintingAssignment.ChooseNext(library, habit, ""));
        Assert.IsTrue(library.TryGet(habit.activePaintingId, out _));
        Assert.IsFalse(habit.awaitingPaintingChoice);
    }

    [Test]
    public void ChooseNext_UnresolvableId_FallsBackToAssignNext()
    {
        // Kitaplik degismis olabilir: cozulemeyen id habit'i bayrak acik,
        // tablosuz BIRAKMAMALI.
        PaintingLibrary library = MakeLibrary(3, 16);
        HabitData habit = MakeHabit();
        habit.awaitingPaintingChoice = true;

        Assert.IsTrue(PaintingAssignment.ChooseNext(library, habit, "yok"));
        Assert.IsTrue(library.TryGet(habit.activePaintingId, out _));
        Assert.IsFalse(habit.awaitingPaintingChoice);
    }

    [Test]
    public void TryReleaseHold_DropsFlagWithoutTouchingGallery()
    {
        HabitData habit = MakeHabit();
        habit.awaitingPaintingChoice = true;
        habit.habitGallery.Add("p0");

        Assert.IsTrue(PaintingAssignment.TryReleaseHold(habit));
        Assert.IsFalse(habit.awaitingPaintingChoice);
        CollectionAssert.AreEqual(new[] { "p0" }, habit.habitGallery);
    }

    [Test]
    public void TryReleaseHold_NotAwaiting_ReturnsFalse()
    {
        HabitData habit = MakeHabit();
        habit.awaitingPaintingChoice = false;

        Assert.IsFalse(PaintingAssignment.TryReleaseHold(habit));
    }

    [Test]
    public void ForSize_ReturnsOnlyExactBucket()
    {
        PaintingLibrary library = MakeMixedLibrary(
            new MixedEntry { Id = "a16", PieceCount = 16 },
            new MixedEntry { Id = "b16", PieceCount = 16 },
            new MixedEntry { Id = "a32", PieceCount = 32 },
            new MixedEntry { Id = "a64", PieceCount = 64 });

        IReadOnlyList<PaintingDefinition> bucket = library.ForSize(16);

        Assert.AreEqual(2, bucket.Count);
        CollectionAssert.AreEquivalent(
            new[] { "a16", "b16" },
            new[] { bucket[0].id, bucket[1].id });
    }

    [Test]
    public void ForSize_MissingBucketIsEmptyWithoutNearestFallback()
    {
        // PickNext'in "en yakin kovaya dus" dususunun AKSINE: ForSize formun
        // izgarasi icin cagriliyor ve gostermedigi bir boyutu secili yapmamali.
        PaintingLibrary library = MakeLibrary(2, 32);

        IReadOnlyList<PaintingDefinition> bucket = library.ForSize(16);

        Assert.AreEqual(0, bucket.Count);
    }
}
