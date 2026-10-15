using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// Delikli tablolarin galeri ve gorunum tarafinin EditMode testleri:
// HabitGallery (delik kaydi), PaintingAssignment'in TryCompleteActive/Resize
// uzerinden delikleri tasimasi, ve PaintingPieceState'in parca basina durum
// haritasi. Yakma/sayma mantigi Tests/MissedPieceTests.cs'te (ayni fixture
// kalibi, 500 satir siniri icin bolundu).
public class PaintingHoleTests
{
    private static PaintingLibrary MakeLibrary(int count, int pieceCount, bool sequential)
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

    // -----------------------------------------------------------------------
    // HabitGallery
    // -----------------------------------------------------------------------

    [Test]
    public void Record_AddsToGalleryOnceAndTracksHoles()
    {
        HabitData habit = MakeHabit();

        HabitGallery.Record(habit, "p0", new List<int> { 2, 5 });

        CollectionAssert.AreEqual(new[] { "p0" }, habit.habitGallery);
        Assert.IsTrue(HabitGallery.TryGetHoles(habit, "p0", out List<int> holes));
        CollectionAssert.AreEqual(new[] { 2, 5 }, holes);
    }

    [Test]
    public void Record_NoMissedPieces_NoHolesEntry()
    {
        HabitData habit = MakeHabit();

        HabitGallery.Record(habit, "p0", new List<int>());

        Assert.IsFalse(HabitGallery.TryGetHoles(habit, "p0", out _));
        Assert.AreEqual(0, habit.galleryHoles.Count);
    }

    [Test]
    public void Record_SecondCompletion_OverwritesHolesWithoutDuplicateMembership()
    {
        HabitData habit = MakeHabit();

        HabitGallery.Record(habit, "p0", new List<int> { 1 });
        HabitGallery.Record(habit, "p0", new List<int> { 2, 3 });

        Assert.AreEqual(1, habit.habitGallery.Count, "Ikinci cagri galeriye tekrar eklememeli.");
        Assert.IsTrue(HabitGallery.TryGetHoles(habit, "p0", out List<int> holes));
        CollectionAssert.AreEqual(new[] { 2, 3 }, holes);
    }

    [Test]
    public void Record_SecondCompletionWithoutHoles_ClearsOldHolesEntry()
    {
        HabitData habit = MakeHabit();

        HabitGallery.Record(habit, "p0", new List<int> { 1 });
        HabitGallery.Record(habit, "p0", new List<int>());

        Assert.IsFalse(HabitGallery.TryGetHoles(habit, "p0", out _));
    }

    [Test]
    public void TryGetHoles_UnknownId_ReturnsFalse()
    {
        HabitData habit = MakeHabit();
        Assert.IsFalse(HabitGallery.TryGetHoles(habit, "nope", out _));
    }

    [Test]
    public void Record_EmptyOrNullId_IsNoOp()
    {
        HabitData habit = MakeHabit();

        HabitGallery.Record(habit, "", new List<int> { 1 });
        HabitGallery.Record(habit, null, new List<int> { 1 });

        Assert.AreEqual(0, habit.habitGallery.Count);
    }

    // -----------------------------------------------------------------------
    // PaintingAssignment entegrasyonu
    // -----------------------------------------------------------------------

    [Test]
    public void TryCompleteActive_WithHoles_RecordsHolesInGallery()
    {
        PaintingLibrary library = MakeLibrary(2, 4, false);
        HabitData habit = MakeHabit(canvasSize: 4);
        habit.randomOpenedPixels.AddRange(new[] { 0, 1, 3 });
        habit.missedPixels.Add(2);

        Assert.IsTrue(PaintingAssignment.TryCompleteActive(library, habit, out string finishedId));
        Assert.IsTrue(HabitGallery.TryGetHoles(habit, finishedId, out List<int> holes));
        CollectionAssert.AreEqual(new[] { 2 }, holes);
    }

    [Test]
    public void TryCompleteActive_NoHoles_NoHolesEntryRecorded()
    {
        PaintingLibrary library = MakeLibrary(2, 4, false);
        HabitData habit = MakeHabit(canvasSize: 4);
        habit.randomOpenedPixels.AddRange(new[] { 0, 1, 2, 3 });

        Assert.IsTrue(PaintingAssignment.TryCompleteActive(library, habit, out string finishedId));
        Assert.IsFalse(HabitGallery.TryGetHoles(habit, finishedId, out _));
    }

    [Test]
    public void Resize_CarriesMissedCountAfterOpenedPieces()
    {
        PaintingLibrary library = MakeLibrary(2, 16, false);
        HabitData habit = MakeHabit(canvasSize: 8);
        habit.randomOpenedPixels.AddRange(new[] { 0, 1, 2 });
        habit.missedPixels.AddRange(new[] { 3, 4 });

        Assert.IsTrue(PaintingAssignment.Resize(library, habit, 16, new List<int>(64), out bool completed));

        Assert.AreEqual(16, habit.canvasSize);
        Assert.AreEqual(3, habit.randomOpenedPixels.Count, "Acik parca sayisi tasinmali.");
        Assert.AreEqual(2, habit.missedPixels.Count, "Yakilan parca sayisi da tasinmali.");
        Assert.IsFalse(completed);
    }

    [Test]
    public void Resize_Smaller_PrioritizesOpenedOverMissedWhenClamping()
    {
        PaintingLibrary library = MakeLibrary(2, 4, false);
        HabitData habit = MakeHabit(canvasSize: 16);
        habit.randomOpenedPixels.AddRange(new[] { 0, 1, 2 });
        habit.missedPixels.AddRange(new[] { 3, 4, 5 });

        // Tuketilen 6 -> 4'e kirpilir. Acik (3) ONCELIKLI tasinir, yakilan
        // sadece kalan 1 yere sigar.
        Assert.IsTrue(PaintingAssignment.Resize(library, habit, 4, new List<int>(64), out bool completed));

        Assert.AreEqual(4, habit.canvasSize);
        Assert.AreEqual(3, habit.randomOpenedPixels.Count);
        Assert.AreEqual(1, habit.missedPixels.Count);
        Assert.IsTrue(completed, "3 acik + 1 yakilan = 4: tuval tam dolu.");
    }

    [Test]
    public void Resize_Sequential_CarriesMissedCount()
    {
        PaintingLibrary library = MakeLibrary(2, 16, true);
        HabitData habit = MakeHabit(canvasSize: 8);
        habit.totalPixelsPainted = 3;
        habit.missedPixels.AddRange(new[] { 3, 4 });

        Assert.IsTrue(PaintingAssignment.Resize(library, habit, 16, new List<int>(64), out _));

        Assert.AreEqual(3, habit.totalPixelsPainted);
        CollectionAssert.AreEqual(new[] { 3, 4 }, habit.missedPixels, "Yakilan parcalar acik onegin HEMEN ardina eklenmeli.");
    }

    // -----------------------------------------------------------------------
    // PaintingPieceState
    // -----------------------------------------------------------------------

    [Test]
    public void StatusOf_Sequential_WithHoles_MatchesTouchedPrefix()
    {
        PaintingLibrary library = MakeLibrary(1, 8, true);
        HabitData habit = MakeHabit(canvasSize: 8);
        habit.totalPixelsPainted = 2;
        habit.missedPixels.Add(2);

        PaintingPieceState state = PaintingPieceState.For(library, habit);

        Assert.AreEqual(PaintingPieceStatus.Opened, state.StatusOf(0));
        Assert.AreEqual(PaintingPieceStatus.Opened, state.StatusOf(1));
        Assert.AreEqual(PaintingPieceStatus.Missed, state.StatusOf(2));
        Assert.AreEqual(PaintingPieceStatus.Closed, state.StatusOf(3));
    }

    [Test]
    public void StatusOf_Random_WithHoles()
    {
        PaintingLibrary library = MakeLibrary(1, 8, false);
        HabitData habit = MakeHabit(canvasSize: 8);
        habit.randomOpenedPixels.AddRange(new[] { 0, 5 });
        habit.missedPixels.Add(3);

        PaintingPieceState state = PaintingPieceState.For(library, habit);

        Assert.AreEqual(PaintingPieceStatus.Opened, state.StatusOf(0));
        Assert.AreEqual(PaintingPieceStatus.Missed, state.StatusOf(3));
        Assert.AreEqual(PaintingPieceStatus.Opened, state.StatusOf(5));
        Assert.AreEqual(PaintingPieceStatus.Closed, state.StatusOf(1));
    }

    [Test]
    public void CollectOpenedIndices_Sequential_SkipsMissed()
    {
        PaintingLibrary library = MakeLibrary(1, 8, true);
        HabitData habit = MakeHabit(canvasSize: 8);
        List<int> scratch = new List<int>();

        PaintingProgress.OpenNextPiece(habit, true, scratch); // 0
        PaintingProgress.OpenNextPiece(habit, true, scratch); // 1
        PaintingProgress.MissPiece(habit, true, scratch); // 2
        PaintingProgress.OpenNextPiece(habit, true, scratch); // 3

        PaintingPieceState state = PaintingPieceState.For(library, habit);
        List<int> buffer = new List<int>();
        state.CollectOpenedIndices(buffer);

        CollectionAssert.AreEqual(new[] { 0, 1, 3 }, buffer);
    }

    [Test]
    public void CollectOpenedIndices_Random_MatchesOpenedIndices()
    {
        PaintingLibrary library = MakeLibrary(1, 8, false);
        HabitData habit = MakeHabit(canvasSize: 8);
        habit.randomOpenedPixels.AddRange(new[] { 5, 1, 3 });

        PaintingPieceState state = PaintingPieceState.For(library, habit);
        List<int> buffer = new List<int>();
        state.CollectOpenedIndices(buffer);

        CollectionAssert.AreEqual(new[] { 5, 1, 3 }, buffer);
    }

    [Test]
    public void For_DefinitionOverload_UsesItsOrderWithoutLibraryLookup()
    {
        PaintingDefinition definition = ScriptableObject.CreateInstance<PaintingDefinition>();
        definition.revealOrder = PaintPixels.RevealOrder.Random;

        HabitData habit = MakeHabit(canvasSize: 4);
        habit.randomOpenedPixels.Add(1);

        PaintingPieceState state = PaintingPieceState.For(definition, habit);

        Assert.IsFalse(state.Sequential);
        Assert.AreEqual(PaintingPieceStatus.Opened, state.StatusOf(1));
    }

    [Test]
    public void For_NullDefinition_FallsBackToRandom()
    {
        HabitData habit = MakeHabit();
        PaintingPieceState state = PaintingPieceState.For((PaintingDefinition)null, habit);

        Assert.IsFalse(state.Sequential);
    }
}
