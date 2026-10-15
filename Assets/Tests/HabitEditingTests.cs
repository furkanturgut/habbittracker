using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Tuval boyutu degisiminin (duzenleme ekrani) EditMode testleri.
//
// Bu kural kritik cunku: canvasSize ile aktif tablonun parca sayisi ayrisirsa
// "tablo bitti" esigi hic yakalanmaz, ve ilerlemenin hangi kayit alaninda
// durdugunu TABLO belirledigi icin boyut degisimi iki alan arasinda gecis yapar.
public class HabitEditingTests
{
    private struct Spec
    {
        public int Pieces;
        public bool Sequential;
    }

    private static Spec S(int pieces, bool sequential)
    {
        return new Spec { Pieces = pieces, Sequential = sequential };
    }

    private static PaintingLibrary MakeLibrary(params Spec[] specs)
    {
        PaintingLibrary library = ScriptableObject.CreateInstance<PaintingLibrary>();
        List<PaintingDefinition> definitions = new List<PaintingDefinition>();

        for (int i = 0; i < specs.Length; i++)
        {
            PaintingDefinition definition = ScriptableObject.CreateInstance<PaintingDefinition>();
            definition.id = "p" + i;
            definition.resolution = (PaintPixels.OverlayResolution)specs[i].Pieces;
            definition.revealOrder = specs[i].Sequential
                ? PaintPixels.RevealOrder.Sequential
                : PaintPixels.RevealOrder.Random;
            definitions.Add(definition);
        }

        // paintings alani private [SerializeField]; Editor API'sine bagimli
        // kalmadan doldurmanin yolu yansima (PaintingAssignmentTests ile ayni).
        FieldInfo field = typeof(PaintingLibrary).GetField(
            "paintings",
            BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(library, definitions);

        library.BuildIndex();
        return library;
    }

    private static HabitData MakeHabit(int canvasSize, string activePaintingId)
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

    private static void FillRandom(HabitData habit, int count)
    {
        for (int i = 0; i < count; i++)
        {
            habit.randomOpenedPixels.Add(i);
        }
    }

    private static List<int> Scratch()
    {
        return new List<int>(64);
    }

    [Test]
    public void Resize_SameSize_ReturnsFalseAndKeepsPainting()
    {
        PaintingLibrary library = MakeLibrary(S(32, false), S(32, false));
        HabitData habit = MakeHabit(32, "p0");
        FillRandom(habit, 10);

        Assert.IsFalse(PaintingAssignment.Resize(library, habit, 32, Scratch(), out bool completed));
        Assert.IsFalse(completed);
        Assert.AreEqual("p0", habit.activePaintingId, "Boyut degismediyse tablo da degismemeli.");
        Assert.AreEqual(10, habit.randomOpenedPixels.Count);
    }

    [Test]
    public void Resize_Smaller_CarriesMinOpened()
    {
        PaintingLibrary library = MakeLibrary(S(32, false), S(16, false), S(16, false));
        HabitData habit = MakeHabit(32, "p0");
        FillRandom(habit, 20);

        Assert.IsTrue(PaintingAssignment.Resize(library, habit, 16, Scratch(), out bool completed));

        Assert.AreEqual(16, habit.canvasSize);
        Assert.AreEqual(16, habit.randomOpenedPixels.Count, "Kucultmede acik parca yeni boyuta sigmali.");
        Assert.IsTrue(completed, "16/16 dolu: tamamlanma bildirilmeli.");
    }

    [Test]
    public void Resize_Larger_CarriesAllOpened()
    {
        PaintingLibrary library = MakeLibrary(S(16, false), S(64, false), S(64, false));
        HabitData habit = MakeHabit(16, "p0");
        FillRandom(habit, 9);

        Assert.IsTrue(PaintingAssignment.Resize(library, habit, 64, Scratch(), out bool completed));

        Assert.AreEqual(64, habit.canvasSize);
        Assert.AreEqual(9, habit.randomOpenedPixels.Count);
        Assert.IsFalse(completed);
    }

    [Test]
    public void Resize_AwaitingChoice_DoesNotCarryProgress()
    {
        // Beklemede ESKI tablo bitmis: parcalarini tasimak carry == canvasSize
        // demek olurdu, yani kullanici tuval boyutunu degistirdi diye hic
        // boyamadigi ikinci bir eser kazanirdi.
        PaintingLibrary library = MakeLibrary(S(32, false), S(16, false), S(16, false));
        HabitData habit = MakeHabit(32, "p0");
        FillRandom(habit, 32);
        habit.awaitingPaintingChoice = true;

        Assert.IsTrue(PaintingAssignment.Resize(library, habit, 16, Scratch(), out bool completed));

        Assert.AreEqual(0, habit.randomOpenedPixels.Count, "Beklemedeki ilerleme tasinmamali.");
        Assert.IsFalse(completed);
    }

    [Test]
    public void Resize_AwaitingChoice_DropsFlagAfterAssigning()
    {
        PaintingLibrary library = MakeLibrary(S(32, false), S(16, false), S(16, false));
        HabitData habit = MakeHabit(32, "p0");
        habit.awaitingPaintingChoice = true;

        Assert.IsTrue(PaintingAssignment.Resize(library, habit, 16, Scratch(), out _));

        Assert.IsFalse(habit.awaitingPaintingChoice, "Bir tablo atamak beklemeyi bitirir.");
    }

    [Test]
    public void Resize_AlwaysMatchesActiveDefinitionPieceCount()
    {
        PaintingLibrary library = MakeLibrary(S(16, false), S(32, true), S(64, false));
        HabitData habit = MakeHabit(16, "p0");
        FillRandom(habit, 4);

        Assert.IsTrue(PaintingAssignment.Resize(library, habit, 64, Scratch(), out _));

        Assert.IsTrue(PaintingAssignment.TryGetDefinition(library, habit, out PaintingDefinition definition));
        Assert.AreEqual(definition.PieceCount, habit.canvasSize,
            "canvasSize her zaman aktif tablonun parca sayisina esit olmali.");
    }

    [Test]
    public void Resize_BucketMissing_ClampsCarryToRequestedSize()
    {
        // Havuzda yalnizca 64 parcalik tablo var, kullanici 16 istiyor: PickNext en
        // yakin boyuta duser ve canvasSize 64 olur -- ama tasinan parca sayisi
        // kullanicinin ISTEDIGI boyutla sinirli kalmali, yoksa eski 64 parca
        // oldugu gibi tasinip tabloyu aninda bitirirdi.
        PaintingLibrary library = MakeLibrary(S(64, false), S(64, false));
        HabitData habit = MakeHabit(64, "p0");
        FillRandom(habit, 20);

        LogAssert.ignoreFailingMessages = true;
        Assert.IsTrue(PaintingAssignment.Resize(library, habit, 16, Scratch(), out bool completed));
        LogAssert.ignoreFailingMessages = false;

        Assert.AreEqual(64, habit.canvasSize);
        Assert.AreEqual(16, habit.randomOpenedPixels.Count);
        Assert.IsFalse(completed);
    }

    [Test]
    public void Resize_CarryFillsNewPainting_ReportsCompleted()
    {
        PaintingLibrary library = MakeLibrary(S(16, false), S(16, false));
        HabitData habit = MakeHabit(64, "p0");
        FillRandom(habit, 64);

        LogAssert.ignoreFailingMessages = true;
        Assert.IsTrue(PaintingAssignment.Resize(library, habit, 16, Scratch(), out bool completed));
        LogAssert.ignoreFailingMessages = false;

        Assert.AreEqual(16, habit.canvasSize);
        Assert.AreEqual(16, habit.randomOpenedPixels.Count);
        Assert.IsTrue(completed);
    }

    [Test]
    public void Resize_SequentialTarget_UsesTotalPixelsPainted()
    {
        PaintingLibrary library = MakeLibrary(S(16, true), S(32, true), S(32, true));
        HabitData habit = MakeHabit(16, "p0");
        habit.totalPixelsPainted = 9;

        Assert.IsTrue(PaintingAssignment.Resize(library, habit, 32, Scratch(), out _));

        Assert.AreEqual(9, habit.totalPixelsPainted);
        Assert.AreEqual(0, habit.randomOpenedPixels.Count, "Sirali tabloda indeks listesi kullanilmamali.");
    }

    [Test]
    public void Resize_RandomTarget_UsesIndexList()
    {
        PaintingLibrary library = MakeLibrary(S(16, false), S(32, false), S(32, false));
        HabitData habit = MakeHabit(16, "p0");
        FillRandom(habit, 5);

        Assert.IsTrue(PaintingAssignment.Resize(library, habit, 32, Scratch(), out _));

        Assert.AreEqual(0, habit.totalPixelsPainted, "Rastgele tabloda tek sayi kullanilmamali.");
        Assert.AreEqual(5, habit.randomOpenedPixels.Count);
        CollectionAssert.AllItemsAreUnique(habit.randomOpenedPixels);

        for (int i = 0; i < habit.randomOpenedPixels.Count; i++)
        {
            Assert.GreaterOrEqual(habit.randomOpenedPixels[i], 0);
            Assert.Less(habit.randomOpenedPixels[i], habit.canvasSize);
        }
    }

    [Test]
    public void Resize_SequentialToRandom_MovesProgressToTheOtherField()
    {
        // Eski tablo sirali (tek sayi), yeni tablo rastgele (indeks listesi). Sayi
        // ESKI tablonun sirasiyla okunmazsa 0 tasinir ve ilerleme sessizce kaybolur.
        PaintingLibrary library = MakeLibrary(S(16, true), S(32, false), S(32, false));
        HabitData habit = MakeHabit(16, "p0");
        habit.totalPixelsPainted = 9;

        Assert.IsTrue(PaintingAssignment.Resize(library, habit, 32, Scratch(), out _));

        Assert.AreEqual(9, habit.randomOpenedPixels.Count);
        Assert.AreEqual(0, habit.totalPixelsPainted);
    }

    [Test]
    public void Resize_NoLibrary_ReturnsFalseAndKeepsProgress()
    {
        HabitData habit = MakeHabit(32, "p0");
        FillRandom(habit, 7);

        Assert.IsFalse(PaintingAssignment.Resize(null, habit, 16, Scratch(), out bool completed));
        Assert.IsFalse(completed);
        Assert.AreEqual(32, habit.canvasSize);
        Assert.AreEqual(7, habit.randomOpenedPixels.Count);
    }
}
