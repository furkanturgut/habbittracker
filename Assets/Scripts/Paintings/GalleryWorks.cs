using System;
using System.Collections.Generic;

// Galeri ekraninin tek veri sorusu: bir aliskanligin TUM eserleri (suren +
// bitmisler), goruntulenecek SIRAYLA.
//
// PaintingChoices.cs'in galeri tarafindaki kardesi: saf ve static, sahne ya da
// MonoBehaviour gerekmiyor.
//
// Sira: SUREN tablo once, sonra bitmisler YENIDEN ESKIYE. habitGallery eskiden
// yeniye saklaniyor (bkz. HabitGallery), yani bitmisler listede TERSTEN gezilir.
public readonly struct GalleryWork
{
    public readonly PaintingDefinition Definition;
    public readonly bool IsFinished;

    // Bitmis tabloda delikler galleryHoles'tan, suren tabloda missedPixels'ten --
    // hangisi gecerli oldugunu IsFinished belirler. Hic delik yoksa BOS liste
    // (hic bir yerde null degil), cagiran taraf null kontrolune gerek kalmadan
    // .Count okuyabilsin.
    public readonly List<int> Holes;

    // Bitmis tabloda PieceCount - Holes.Count (her delik-olmayan parca acik
    // sayilir); suren tabloda habit.OpenedPieceCount(sequential). Ikisi de
    // PaintingPieceState'in kendi icinde turetecegi sayinin AYNISI -- burada
    // tekrar tutuluyor cunku GalleryWorks.Totals baska bir PaintingPieceState
    // kurmadan bu toplami okuyabilsin.
    public readonly int OpenedCount;

    // 1 tabanli "{n}. eser" -- habitGallery'deki SAKLAMA sirasina gore (goruntu
    // sirasina gore DEGIL, o tersten geziliyor). Suren tablo icin 0.
    public readonly int GalleryIndex;

    public GalleryWork(PaintingDefinition definition, bool isFinished, List<int> holes, int openedCount, int galleryIndex)
    {
        Definition = definition;
        IsFinished = isFinished;
        Holes = holes;
        OpenedCount = openedCount;
        GalleryIndex = galleryIndex;
    }
}

public static class GalleryWorks
{
    private static readonly List<int> EmptyHoles = new List<int>(0);

    // buffer, suren tablo (varsa) ile basliyor, sonra bitmisler yeniden eskiye.
    public static void Collect(PaintingLibrary library, HabitData habit, List<GalleryWork> buffer)
    {
        buffer.Clear();

        if (library == null || habit == null)
        {
            return;
        }

        // Suren tablo iki durumda ATLANIR: bekleyen bir secim varsa
        // (activePaintingId HALA biten tabloyu gosteriyor ve o zaten galeride,
        // bkz. CLAUDE.md > bekleyen secim degismezi) ya da hic tablo atanmamissa.
        bool skipActive = habit.awaitingPaintingChoice || string.IsNullOrEmpty(habit.activePaintingId);

        if (!skipActive && PaintingAssignment.TryGetDefinition(library, habit, out PaintingDefinition active))
        {
            bool sequential = active.IsSequential;
            buffer.Add(new GalleryWork(active, false, habit.missedPixels, habit.OpenedPieceCount(sequential), 0));
        }

        List<string> gallery = habit.habitGallery;
        for (int i = gallery.Count - 1; i >= 0; i--)
        {
            string paintingId = gallery[i];

            // Cozulemeyen id (kitaplik degismis, tanim silinmis) atlanir --
            // bozuk bir kayit butun galeriyi patlatmamali.
            if (!library.TryGet(paintingId, out PaintingDefinition finished))
            {
                continue;
            }

            List<int> holes = HabitGallery.TryGetHoles(habit, paintingId, out List<int> pieces) ? pieces : EmptyHoles;
            buffer.Add(new GalleryWork(finished, true, holes, finished.PieceCount - holes.Count, i + 1));
        }
    }

    // Gun/kacirma toplamlari Collect'in UZERINDEN turetilir (ikinci bir
    // "skipActive" kopyasi degil): "suren tablo bekleyen secimdeyken sayilmaz"
    // kurali tek yerde yazili kalsin diye. scratch, cagiran tarafin zaten
    // kendi alaninda tuttugu, cagrilar arasi yeniden kullanilan bir tampon.
    public static void Totals(PaintingLibrary library, HabitData habit, List<GalleryWork> scratch, out int days, out int missed)
    {
        days = 0;
        missed = 0;

        Collect(library, habit, scratch);

        for (int i = 0; i < scratch.Count; i++)
        {
            days += scratch[i].OpenedCount;
            missed += scratch[i].Holes.Count;
        }
    }

    public static int FinishedCount(HabitData habit)
    {
        return habit != null ? habit.habitGallery.Count : 0;
    }

    // Detay ekraninin ikinci sira karolari (tamamlanma %, kacirma %, eser basina
    // ortalama gun). Pay Totals'tan gelen days/missed, payda uc gostergenin
    // ikisinde de ayni: bugun - startDate (en az 1 -- startDate bugunse ya da
    // parse edilemiyorsa sifira bolme olmasin). averageDaysPerPainting
    // finishedCount uzerinden -- "Eser" karosuyla AYNI sayim (habitGallery),
    // suren tablo dahil degil.
    public static void ConsistencyStats(
        string startDate,
        DateTime now,
        int days,
        int missed,
        int finishedCount,
        out float completionRate,
        out float missRate,
        out float averageDaysPerPainting)
    {
        completionRate = 0f;
        missRate = 0f;
        averageDaysPerPainting = finishedCount > 0 ? (float)days / finishedCount : 0f;

        if (!HabitScheduling.TryParse(startDate, out DateTime start))
        {
            return;
        }

        int elapsedDays = Math.Max(1, (now.Date - start.Date).Days);

        completionRate = days * 100f / elapsedDays;
        missRate = missed * 100f / elapsedDays;
    }
}
