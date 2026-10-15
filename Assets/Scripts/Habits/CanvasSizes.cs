// Formun sundugu tuval boyutlari.
//
// Deger PARCA SAYISI (kenar uzunlugu degil) ve PaintPixels'in destekledigi uc
// cozunurlukle birebir ayni olmak zorunda: 16 -> 8x2, 32 -> 8x4, 64 -> 8x8.
// Buraya dorduncu bir sayi eklemek, tablo havuzunda o boyutta tanim olmadigi
// surece PickNext'i en yakin kovaya dusurur (bkz. PaintingAssignment.Resize).
public static class CanvasSizes
{
    public static readonly int[] All = { 16, 32, 64 };

    // Kullaniciya parca sayisi degil, ne kadar sureceği anlatiliyor.
    public static string Hint(int size)
    {
        switch (size)
        {
            case 16: return Loc.Tr("canvas_size.hint_short");
            case 64: return Loc.Tr("canvas_size.hint_long");
            default: return Loc.Tr("canvas_size.hint_medium");
        }
    }
}
