using System.Collections.Generic;

// Bir parcanin UC hali: hic dokunulmamis, acik, ya da kalici yakilmis.
public enum PaintingPieceStatus
{
    Closed,
    Opened,
    Missed
}

// Bir aliskanligin aktif tablosunun parca parca GORUNUMU.
//
// HabitCard, HabitTile ve PaintingView.Bind'daki "Sequential mi Random mi"
// dallanmasini TEK yere topluyor: yakilan parca eklenince bu dallanmanin uc
// kopyasini ayni anda guncellemek gerekirdi, ilk degisen kuralda sessizce
// ayrisirdi.
//
// readonly struct + listeleri referansla tutuyor: tahsis uretmez, kodun geri
// kalaniyla ayni kural.
public readonly struct PaintingPieceState
{
    public readonly int PieceCount;
    public readonly bool Sequential;
    public readonly int OpenedCount;

    // Random modda gecerli (habit.randomOpenedPixels), Sequential'da null --
    // o modda acik kume sayidan turetilir (bkz. frontier).
    public readonly List<int> OpenedIndices;

    public readonly List<int> MissedPieces;

    // Sequential modda TUKETILEN (acik + yakilmis) onegin uzunlugu. Parcalar
    // her zaman 0,1,2... sirasiyla tuketildigi icin (ac ya da yak, farketmez)
    // bu tek sayi "hangi indeksler dokunulmus" sorusunu cevaplamaya yeter --
    // bkz. PaintingProgress ustundeki aciklama.
    private readonly int frontier;

    private PaintingPieceState(
        int pieceCount,
        bool sequential,
        int openedCount,
        List<int> openedIndices,
        List<int> missedPieces,
        int frontier)
    {
        PieceCount = pieceCount;
        Sequential = sequential;
        OpenedCount = openedCount;
        OpenedIndices = openedIndices;
        MissedPieces = missedPieces;
        this.frontier = frontier;
    }

    public static PaintingPieceState For(PaintingLibrary library, HabitData habit)
    {
        return For(PaintingAssignment.IsSequential(library, habit), habit);
    }

    // PaintingView.Bind'in zaten cozulmus bir tanimi oldugunda kullandigi
    // kisayol -- kitapliga tekrar basvurup ayni tanimi bulmasina gerek yok.
    public static PaintingPieceState For(PaintingDefinition definition, HabitData habit)
    {
        return For(definition != null && definition.IsSequential, habit);
    }

    private static PaintingPieceState For(bool sequential, HabitData habit)
    {
        return new PaintingPieceState(
            habit.canvasSize,
            sequential,
            habit.OpenedPieceCount(sequential),
            sequential ? null : habit.randomOpenedPixels,
            habit.missedPixels,
            sequential ? habit.totalPixelsPainted + habit.missedPixels.Count : 0);
    }

    // BITMIS bir tablonun gorunumu (Galeri): tuketilen onek TUM tabloyu kaplar --
    // tablo bittigi icin geriye kapali parca kalmaz, delik olmayan HER parca
    // acik sayilir. sequential=true burada SADECE "frontier = PieceCount"
    // kestirmesini kullanmak icin -- bitmis bir tabloda hangi sirayla doldugu
    // artik gorunumu degistirmiyor, reveal order'in anlami kalmiyor.
    public static PaintingPieceState ForFinished(int pieceCount, List<int> holes)
    {
        return new PaintingPieceState(pieceCount, true, pieceCount - holes.Count, null, holes, pieceCount);
    }

    public PaintingPieceStatus StatusOf(int pieceIndex)
    {
        if (ListLookup.Contains(MissedPieces, pieceIndex))
        {
            return PaintingPieceStatus.Missed;
        }

        bool opened = Sequential
            ? pieceIndex < frontier
            : ListLookup.Contains(OpenedIndices, pieceIndex);

        return opened ? PaintingPieceStatus.Opened : PaintingPieceStatus.Closed;
    }

    // Acik parca indekslerini artan sirada buffer'a yazar. Random modda zaten
    // elde olan OpenedIndices'in Sequential karsiligi -- PaintingView.Bind
    // ikisini AYNI bicimde (acik indeks listesi) modulu beslemek icin kullaniyor.
    public void CollectOpenedIndices(List<int> buffer)
    {
        buffer.Clear();

        if (!Sequential)
        {
            buffer.AddRange(OpenedIndices);
            return;
        }

        for (int i = 0; i < frontier; i++)
        {
            if (!ListLookup.Contains(MissedPieces, i))
            {
                buffer.Add(i);
            }
        }
    }
}
