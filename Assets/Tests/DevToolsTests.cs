using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// Gelistirici araclarinin SAF kisminin EditMode testleri.
//
// Dev araclarinin sahneli yollari (DevTools, manager cagrilari) burada yok:
// onlar zaten uretim API'sini cagiriyor ve o API'nin testi kendi dosyasinda.
// Buradaki dort sey ise dev katmanina OZGU karar:
//   * AppClock'un gun kaydirmasi ve kirpmasi,
//   * hedef tarihten gun sayisi cikarma (DevClock),
//   * "su kadar parca acik olsun" yazmasi (DevPaintingTools),
//   * elle yazilan streak'in gun donusunden SAG CIKMASI (DevHabitTools) --
//     damgayi yazmayi unutursak bu test kirmiziya doner.
public class DevToolsTests
{
    // ---------------------------------------------------------------------
    // Yardimcilar
    // ---------------------------------------------------------------------

    private static PaintingLibrary MakeLibrary(int pieceCount, bool sequential)
    {
        PaintingLibrary library = ScriptableObject.CreateInstance<PaintingLibrary>();

        PaintingDefinition definition = ScriptableObject.CreateInstance<PaintingDefinition>();
        definition.id = "p1";
        definition.resolution = (PaintPixels.OverlayResolution)pieceCount;
        definition.revealOrder = sequential
            ? PaintPixels.RevealOrder.Sequential
            : PaintPixels.RevealOrder.Random;

        // paintings alani private [SerializeField]; PaintingAssignmentTests ile
        // ayni gerekce: Editor API'sine bagimli kalmadan doldurmanin yolu yansima.
        FieldInfo field = typeof(PaintingLibrary).GetField(
            "paintings",
            BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(library, new List<PaintingDefinition> { definition });

        library.BuildIndex();
        return library;
    }

    private static HabitData MakeHabit(int canvasSize = 16, params DayOfWeek[] frequency)
    {
        HabitData habit = new HabitData
        {
            id = "h1",
            title = "Test",
            habitType = HabitTypes.Build,
            canvasSize = canvasSize,
            activePaintingId = "p1",
            startDate = HabitScheduling.ToKey(new DateTime(2020, 1, 1))
        };

        for (int i = 0; i < frequency.Length; i++)
        {
            habit.frequency.Add(frequency[i]);
        }

        return habit;
    }

    // AppClock static: bir testin kaydirmasi digerine sizmasin.
    [TearDown]
    public void ResetClock()
    {
        AppClock.Reset();
    }

    // ---------------------------------------------------------------------
    // AppClock
    // ---------------------------------------------------------------------

    [Test]
    public void AppClock_Default_MatchesRealDate()
    {
        Assert.IsFalse(AppClock.IsShifted);
        Assert.AreEqual(DateTime.Now.Date, AppClock.Today);
    }

    [Test]
    public void AppClock_SetDayOffset_ShiftsTodayByWholeDays()
    {
        AppClock.SetDayOffset(5);

        Assert.IsTrue(AppClock.IsShifted);
        Assert.AreEqual(DateTime.Now.Date.AddDays(5), AppClock.Today);

        // Gunun ICINDEKI saat kaydirilmiyor: gece yarisi coroutine'i gercek
        // gece yarisinda uyanmaya devam etmeli. Tolerans iki okuma arasindaki
        // sureyi karsiliyor (0.001 gun ~ 86 saniye).
        Assert.AreEqual(5.0, (AppClock.Now - DateTime.Now).TotalDays, 0.001);
    }

    [Test]
    public void AppClock_SetDayOffset_ClampsToMaxOffset()
    {
        Assert.AreEqual(AppClock.MaxDayOffset, AppClock.SetDayOffset(AppClock.MaxDayOffset + 50));
        Assert.AreEqual(-AppClock.MaxDayOffset, AppClock.SetDayOffset(-AppClock.MaxDayOffset - 50));
    }

    [Test]
    public void AppClock_MaxOffset_MatchesCatchUpWindow()
    {
        // Gun sonu telafisi bu kadar gun geriye bakiyor; kaydirmanin daha
        // ileriye gitmesinin gozlemlenebilir bir karsiligi yok.
        Assert.AreEqual(HabitScheduling.MaxCatchUpDays, AppClock.MaxDayOffset);
    }

    [Test]
    public void AppClock_Reset_ReturnsToRealTime()
    {
        AppClock.SetDayOffset(9);
        AppClock.Reset();

        Assert.AreEqual(0, AppClock.DayOffset);
        Assert.AreEqual(DateTime.Now.Date, AppClock.Today);
    }

    // ---------------------------------------------------------------------
    // DevClock
    // ---------------------------------------------------------------------

    [Test]
    public void DevClock_DayOffsetFor_CountsWholeDaysOnly()
    {
        DateTime realNow = new DateTime(2026, 6, 10, 23, 30, 0);
        DateTime target = new DateTime(2026, 6, 13, 1, 15, 0);

        // Saat farki 2 gun 1.75 saat; cevap 2 degil 3 olmali -- karsilastirma
        // takvim gunu uzerinden.
        Assert.AreEqual(3, DevClock.DayOffsetFor(target, realNow));
    }

    [Test]
    public void DevClock_DayOffsetFor_SameDay_IsZero()
    {
        DateTime realNow = new DateTime(2026, 6, 10, 8, 0, 0);
        Assert.AreEqual(0, DevClock.DayOffsetFor(new DateTime(2026, 6, 10, 22, 0, 0), realNow));
    }

    [Test]
    public void DevClock_DayOffsetFor_PastTarget_IsNegative()
    {
        DateTime realNow = new DateTime(2026, 6, 10);
        Assert.AreEqual(-4, DevClock.DayOffsetFor(new DateTime(2026, 6, 6), realNow));
    }

    // ---------------------------------------------------------------------
    // DevPaintingTools
    // ---------------------------------------------------------------------

    [Test]
    public void DevPainting_SetOpenedPieces_Sequential_WritesCount()
    {
        PaintingLibrary library = MakeLibrary(16, true);
        HabitData habit = MakeHabit();
        List<int> scratch = new List<int>();

        Assert.AreEqual(9, DevPaintingTools.SetOpenedPieces(library, habit, 9, scratch));
        Assert.AreEqual(9, habit.totalPixelsPainted);
        Assert.AreEqual(0, habit.randomOpenedPixels.Count);
    }

    [Test]
    public void DevPainting_SetOpenedPieces_Random_OpensUniqueValidIndices()
    {
        PaintingLibrary library = MakeLibrary(16, false);
        HabitData habit = MakeHabit();
        List<int> scratch = new List<int>();

        Assert.AreEqual(7, DevPaintingTools.SetOpenedPieces(library, habit, 7, scratch));
        Assert.AreEqual(7, habit.randomOpenedPixels.Count);

        HashSet<int> seen = new HashSet<int>();
        for (int i = 0; i < habit.randomOpenedPixels.Count; i++)
        {
            int index = habit.randomOpenedPixels[i];
            Assert.IsTrue(index >= 0 && index < habit.canvasSize, "indeks tuvalin disinda: " + index);
            Assert.IsTrue(seen.Add(index), "ayni indeks iki kez acildi: " + index);
        }
    }

    [Test]
    public void DevPainting_SetOpenedPieces_AboveCanvasSize_ClampsToFull()
    {
        PaintingLibrary library = MakeLibrary(16, false);
        HabitData habit = MakeHabit();
        List<int> scratch = new List<int>();

        Assert.AreEqual(16, DevPaintingTools.SetOpenedPieces(library, habit, 99, scratch));
        Assert.IsTrue(PaintingProgress.IsComplete(habit, false));
    }

    [Test]
    public void DevPainting_SetOpenedPieces_DoesNotAccumulate()
    {
        PaintingLibrary library = MakeLibrary(16, false);
        HabitData habit = MakeHabit();
        List<int> scratch = new List<int>();

        DevPaintingTools.SetOpenedPieces(library, habit, 10, scratch);
        DevPaintingTools.SetOpenedPieces(library, habit, 4, scratch);

        // "Su kadar acik olsun" bir ATAMA, toplama degil: ikinci cagri 14
        // uretirse azaltma yonu hic calismiyor demektir.
        Assert.AreEqual(4, habit.randomOpenedPixels.Count);
    }

    [Test]
    public void DevPainting_SetOpenedPieces_Zero_ClearsBothRepresentations()
    {
        PaintingLibrary library = MakeLibrary(16, true);
        HabitData habit = MakeHabit();
        habit.randomOpenedPixels.Add(3);
        List<int> scratch = new List<int>();

        DevPaintingTools.SetOpenedPieces(library, habit, 0, scratch);

        Assert.AreEqual(0, habit.totalPixelsPainted);
        Assert.AreEqual(0, habit.randomOpenedPixels.Count);
    }

    // ---------------------------------------------------------------------
    // DevHabitTools (streak)
    // ---------------------------------------------------------------------

    [Test]
    public void DevHabit_SetStreak_LongestNeverBelowCurrent()
    {
        HabitData habit = MakeHabit();

        DevHabitTools.SetStreak(habit, 12, 3, new DateTime(2026, 6, 10));

        Assert.AreEqual(12, habit.currentStreak);
        Assert.AreEqual(12, habit.longestStreak);
    }

    [Test]
    public void DevHabit_SetStreak_Zero_ClearsStamp()
    {
        HabitData habit = MakeHabit();
        habit.lastCompletedDate = "2026-06-09";

        DevHabitTools.SetStreak(habit, 0, 30, new DateTime(2026, 6, 10));

        Assert.AreEqual(0, habit.currentStreak);
        Assert.AreEqual(30, habit.longestStreak, "rekor gecmise ait: sifirlanmamali");
        Assert.IsNull(habit.lastCompletedDate);
    }

    [Test]
    public void DevHabit_SetStreak_CompletedToday_StampsToday()
    {
        HabitData habit = MakeHabit();
        habit.isCompletedToday = true;

        DateTime now = new DateTime(2026, 6, 10);
        DevHabitTools.SetStreak(habit, 4, 4, now);

        Assert.AreEqual(HabitScheduling.ToKey(now), habit.lastCompletedDate);
    }

    [Test]
    public void DevHabit_SetStreak_NotCompletedToday_StampsPreviousDueDay()
    {
        // Yalnizca pazartesi planli; "simdi" carsamba.
        HabitData habit = MakeHabit(16, DayOfWeek.Monday);
        DateTime wednesday = new DateTime(2026, 6, 3);
        Assert.AreEqual(DayOfWeek.Wednesday, wednesday.DayOfWeek);

        DevHabitTools.SetStreak(habit, 5, 5, wednesday);

        Assert.AreEqual("2026-06-01", habit.lastCompletedDate, "onceki planli gun pazartesi olmali");
    }

    [Test]
    public void DevHabit_SetStreak_SurvivesNextDailyReset_EveryDay()
    {
        // Elle yazilan streak'in var olma sebebi bu: damgayi yazmazsak
        // BreakMissedStreaks gun donusunde streak'i sifirlar ve panelde
        // "30 yazdim, sabah 0 oldu" gibi gorunur.
        PlayerSaveData data = new PlayerSaveData();
        HabitData habit = MakeHabit();
        habit.isCompletedToday = true;
        data.habits.Add(habit);

        DateTime today = new DateTime(2026, 6, 10);
        DevHabitTools.SetStreak(habit, 30, 30, today);

        data.lastLoginDate = HabitScheduling.ToKey(today);
        Assert.IsTrue(DailyResetService.ApplyDailyReset(data, today.AddDays(1)));

        Assert.AreEqual(30, habit.currentStreak);
        Assert.IsFalse(habit.isCompletedToday, "yeni gun tikleri sifirlamali");
    }

    [Test]
    public void DevHabit_SetStreak_SurvivesNextDailyReset_WeeklySchedule()
    {
        // Haftada bir planli aliskanlikta damga PAZARTESI olmali: persembeye
        // gecerken onceki planli gun hala pazartesi, yani streak kirilmaz.
        PlayerSaveData data = new PlayerSaveData();
        HabitData habit = MakeHabit(16, DayOfWeek.Monday);
        data.habits.Add(habit);

        DateTime wednesday = new DateTime(2026, 6, 3);
        DevHabitTools.SetStreak(habit, 8, 8, wednesday);

        data.lastLoginDate = HabitScheduling.ToKey(wednesday);
        Assert.IsTrue(DailyResetService.ApplyDailyReset(data, wednesday.AddDays(1)));

        Assert.AreEqual(8, habit.currentStreak);
    }

    // ---------------------------------------------------------------------
    // DevMoodTools
    // ---------------------------------------------------------------------

    [Test]
    public void DevMood_LowestPointFor_ResolvesEveryMoodOnTheScale()
    {
        for (int slot = 0; slot < MoodScale.Count; slot++)
        {
            PainterManager.PainterMoods mood = MoodScale.MoodForSlot(slot);
            int point = DevMoodTools.LowestPointFor(mood);

            Assert.AreEqual(mood, PainterManager.MoodForPoint(point), "puan " + point + " bu moda dusmuyor");
        }
    }

    [Test]
    public void DevMood_LowestPointFor_IsTheLowestRepresentative()
    {
        for (int slot = 0; slot < MoodScale.Count; slot++)
        {
            PainterManager.PainterMoods mood = MoodScale.MoodForSlot(slot);
            int point = DevMoodTools.LowestPointFor(mood);

            if (point <= PainterManager.MinMoodPoint)
            {
                continue;
            }

            // Bir asagisi BASKA bir mod olmali: oraya oturunca -1 adimi modu
            // gercekten degistirsin (gecisleri tek tusla gezebilmek icin).
            Assert.AreNotEqual(mood, PainterManager.MoodForPoint(point - 1));
        }
    }

    // ---------------------------------------------------------------------
    // DevSampleHabits
    // ---------------------------------------------------------------------

    [Test]
    public void DevSampleHabits_All_UseCanonicalOptions()
    {
        HashSet<string> titles = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < DevSampleHabits.All.Length; i++)
        {
            DevSampleHabits.SampleHabit sample = DevSampleHabits.All[i];

            Assert.IsTrue(titles.Add(sample.Title), "ayni baslik iki kez: " + sample.Title);

            // Baslik tekrarlanirsa Create() onu atlardi (iki kez eklemeyi
            // onleyen kural), yani tohum sessizce eksik kalirdi.
            Assert.GreaterOrEqual(HabitCategories.IndexOf(sample.Category), 0, sample.Title + ": kategori listede yok");
            Assert.Contains(sample.CanvasSize, CanvasSizes.All, sample.Title + ": tuval boyutu listede yok");
            Assert.IsTrue(sample.Progress >= 0f && sample.Progress <= 1f, sample.Title + ": oran 0..1 disinda");
            Assert.GreaterOrEqual(sample.Streak, 0);

            if (sample.Days == null)
            {
                continue;
            }

            for (int d = 0; d < sample.Days.Length; d++)
            {
                Assert.IsTrue(
                    sample.Days[d] >= 0 && sample.Days[d] < WeekDays.Count,
                    sample.Title + ": gun indeksi 0..6 disinda");
            }
        }
    }

    [Test]
    public void DevSampleHabits_All_CoverEveryCategoryAndCanvasSize()
    {
        // Ornek verinin tek isi ekranlari gercekci doldurmak: bir kategori ya da
        // tuval boyutu listeden duserse filtre satiri ve izgara eksik test edilir.
        for (int c = 0; c < HabitCategories.All.Length; c++)
        {
            Assert.IsTrue(HasCategory(HabitCategories.All[c]), "kategori ornek veride yok: " + HabitCategories.All[c]);
        }

        for (int s = 0; s < CanvasSizes.All.Length; s++)
        {
            Assert.IsTrue(HasCanvasSize(CanvasSizes.All[s]), "tuval boyutu ornek veride yok: " + CanvasSizes.All[s]);
        }
    }

    private static bool HasCategory(string category)
    {
        for (int i = 0; i < DevSampleHabits.All.Length; i++)
        {
            if (string.Equals(DevSampleHabits.All[i].Category, category, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasCanvasSize(int size)
    {
        for (int i = 0; i < DevSampleHabits.All.Length; i++)
        {
            if (DevSampleHabits.All[i].CanvasSize == size)
            {
                return true;
            }
        }

        return false;
    }
}
