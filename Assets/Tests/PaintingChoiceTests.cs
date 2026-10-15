using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// Tablo secim kutusunun saf mantiginin EditMode testleri: PaintingChoices.Collect
// ve PaintingChoicePrompt.IsPending. ChooseNext / TryReleaseHold
// PaintingAssignmentTests.cs'te (ayni dosyadaki diger sozlesmelerle birlikte).
public class PaintingChoiceTests
{
    // PaintingAssignmentTests.cs'teki fixture'larin aynisi -- suite'te zaten
    // iki kopyasi var, kalip bu.
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
            startDate = HabitScheduling.ToKey(new DateTime(2020, 1, 1))
        };
    }

    private static void FillPainting(HabitData habit, int pieceCount)
    {
        for (int i = 0; i < pieceCount; i++)
        {
            habit.randomOpenedPixels.Add(i);
        }
    }

    // -----------------------------------------------------------------------
    // PaintingChoices.Collect
    // -----------------------------------------------------------------------

    [Test]
    public void Collect_ExcludesGalleryEntries()
    {
        PaintingLibrary library = MakeLibrary(3, 16);
        HabitData habit = MakeHabit();
        habit.habitGallery.Add("p0");

        List<PaintingDefinition> buffer = new List<PaintingDefinition>();
        PaintingChoices.Collect(library, habit, buffer);

        Assert.AreEqual(2, buffer.Count);
        CollectionAssert.DoesNotContain(Ids(buffer), "p0");
    }

    [Test]
    public void Collect_AllInGallery_ReturnsWholeBucket()
    {
        // PaintingLibrary.PickNext'teki "hepsi bitirilmis, havuzu bastan ac"
        // kuralinin aynisi.
        PaintingLibrary library = MakeLibrary(3, 16);
        HabitData habit = MakeHabit();
        habit.habitGallery.AddRange(new[] { "p0", "p1", "p2" });

        List<PaintingDefinition> buffer = new List<PaintingDefinition>();
        PaintingChoices.Collect(library, habit, buffer);

        Assert.AreEqual(3, buffer.Count);
    }

    [Test]
    public void Collect_EmptyBucket_ReturnsEmpty()
    {
        PaintingLibrary library = MakeLibrary(2, 32);
        HabitData habit = MakeHabit(canvasSize: 16);

        List<PaintingDefinition> buffer = new List<PaintingDefinition>();
        PaintingChoices.Collect(library, habit, buffer);

        Assert.AreEqual(0, buffer.Count);
    }

    [Test]
    public void Collect_ClearsBufferEveryCall()
    {
        PaintingLibrary library = MakeLibrary(3, 16);
        HabitData habit = MakeHabit();

        List<PaintingDefinition> buffer = new List<PaintingDefinition> { null, null, null, null, null };
        PaintingChoices.Collect(library, habit, buffer);

        Assert.AreEqual(3, buffer.Count);
    }

    [Test]
    public void Collect_NullLibraryOrHabit_ClearsBufferAndReturns()
    {
        List<PaintingDefinition> buffer = new List<PaintingDefinition> { null };

        PaintingChoices.Collect(null, MakeHabit(), buffer);
        Assert.AreEqual(0, buffer.Count);

        buffer.Add(null);
        PaintingChoices.Collect(MakeLibrary(1, 16), null, buffer);
        Assert.AreEqual(0, buffer.Count);
    }

    private static List<string> Ids(List<PaintingDefinition> definitions)
    {
        List<string> ids = new List<string>();
        for (int i = 0; i < definitions.Count; i++)
        {
            ids.Add(definitions[i].id);
        }

        return ids;
    }

    // -----------------------------------------------------------------------
    // PaintingChoicePrompt.IsPending
    // -----------------------------------------------------------------------

    private static readonly DateTime Today = new DateTime(2024, 6, 10);

    [Test]
    public void IsPending_FlagSetDueNotCompleted_ReturnsTrue()
    {
        HabitData habit = MakeHabit();
        habit.awaitingPaintingChoice = true;
        habit.isCompletedToday = false;

        Assert.IsTrue(PaintingChoicePrompt.IsPending(habit, Today));
    }

    [Test]
    public void IsPending_CompletedToday_ReturnsFalse()
    {
        // Tablonun BITTIGI gun: tile dolu altin cerceveyle durur, yanip sonmez.
        HabitData habit = MakeHabit();
        habit.awaitingPaintingChoice = true;
        habit.isCompletedToday = true;

        Assert.IsFalse(PaintingChoicePrompt.IsPending(habit, Today));
    }

    [Test]
    public void IsPending_NotDueToday_ReturnsFalse()
    {
        HabitData habit = MakeHabit();
        habit.awaitingPaintingChoice = true;
        habit.frequency.Add(DayOfWeek.Monday);
        habit.frequency.Add(DayOfWeek.Wednesday);
        habit.frequency.Add(DayOfWeek.Friday);

        // 2024-06-10 bir Pazartesi; planli olmayan bir gune kaydiralim.
        DateTime notDue = new DateTime(2024, 6, 11); // Sali
        Assert.IsFalse(PaintingChoicePrompt.IsPending(habit, notDue));
    }

    [Test]
    public void IsPending_FlagNotSet_ReturnsFalse()
    {
        HabitData habit = MakeHabit();
        habit.awaitingPaintingChoice = false;

        Assert.IsFalse(PaintingChoicePrompt.IsPending(habit, Today));
    }

    [Test]
    public void IsPending_NullHabit_ReturnsFalse()
    {
        Assert.IsFalse(PaintingChoicePrompt.IsPending(null, Today));
    }
}
