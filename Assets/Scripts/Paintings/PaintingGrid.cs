// Tablo izgarasinin hucre <-> parca eslemesi.
//
// Parca 0 UST-SOL kabul edilir (bkz. PaintPixels.TextureRowForPiece); uGUI'nin
// GridLayoutGroup'u da hucreleri SOL USTTEN doldurur, yani iki taraf ayni yonden
// sayiyor ve asagidaki iki metot KASITLI OLARAK ozdeslik -- cevirme PaintPixels
// tarafinda bir kez yapiliyor, cunku texture'da y = 0 ALTTADIR ve o taraf
// kendi basina kalan tek fark.
//
// Metotlar SILINMIYOR: "her uGUI ortusu buradan gecer" kuralinin tek yeri burasi.
// Biri cikarilip dogrudan cellIndex/pieceIndex kullanilirsa bir sonraki izgara
// degisikliginde (sutundan onde siralama, spiral, 8'den farkli sutun) sessizce
// her kucuk resim yine aynalanir.
public static class PaintingGrid
{
    // Sutun sayisi her cozunurlukte sabit (bkz. PaintPixels.Columns).
    public const int Columns = 8;

    public static int RowsFor(int pieceCount)
    {
        return pieceCount > 0 ? pieceCount / Columns : 0;
    }

    // uGUI hucre indeksi (0 = sol ust) -> PaintPixels parca indeksi (0 = sol ust).
    public static int PieceIndexForCell(int cellIndex, int pieceCount)
    {
        int rows = RowsFor(pieceCount);
        if (rows <= 0 || cellIndex < 0 || cellIndex >= pieceCount)
        {
            return -1;
        }

        return cellIndex;
    }

    // Ters yon: parca indeksi -> uGUI hucre indeksi.
    public static int CellIndexForPiece(int pieceIndex, int pieceCount)
    {
        int rows = RowsFor(pieceCount);
        if (rows <= 0 || pieceIndex < 0 || pieceIndex >= pieceCount)
        {
            return -1;
        }

        return pieceIndex;
    }
}
