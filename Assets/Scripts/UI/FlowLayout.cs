using System.Collections.Generic;

// "Duvar" dizilimi: degisken genislikli ogeleri soldan saga dolduran, sigmayinca
// yeni satira gecen ve HER SATIRI yatayda ORTALAYAN saf aritmetik.
//
// uGUI'de hazir karsiligi yok -- HorizontalLayoutGroup sarmiyor, GridLayoutGroup
// tek hucre olcusu kullaniyor ve son satiri sola yapistiriyor. Galeri duvarinin
// teslim dosyasi "wrapping row, centered" istiyor; bu yuzden WorkshopBands
// kalibiyla ayni: aritmetik burada saf ve test edilebilir, RectTransform'a
// yazma islemi FlowLayoutGroup'ta.
public readonly struct FlowItem
{
    // Hangi satirda oldugu -- su an disaridan okunmuyor ama cagiran tarafin
    // (test, gelecekteki bir hata ayiklama) satir sinirlarini gorebilmesi icin
    // tutuluyor.
    public readonly int Row;
    public readonly float X;
    public readonly float Y;

    public FlowItem(int row, float x, float y)
    {
        Row = row;
        X = x;
        Y = y;
    }
}

public static class FlowLayout
{
    // widths: her ogenin genisligi, SIRAYLA. itemHeight: butun ogeler ayni
    // yukseklikte (duvardaki her yuva ayni -- resim + plaka). Satir tasmasi
    // genislikten, satirlar arasi gecis dikeyden kaynaklanir; ikisi BAGIMSIZ
    // aralik degeri alir cunku teslim dosyasinda 16/12 farkli.
    //
    // Doner: toplam yukseklik (son satirin ALT KENARI). buffer, widths ile AYNI
    // SIRADA, her ogenin (satir, x, y) ucgenini tutar -- X/Y tasarim koordinati
    // (sol ust = 0,0, asagi = +Y), UiRect/UiFactory'nin kalibiyla ayni.
    public static float Solve(
        IReadOnlyList<float> widths,
        float availableWidth,
        float gapX,
        float gapY,
        float itemHeight,
        List<FlowItem> buffer)
    {
        buffer.Clear();

        if (widths == null || widths.Count == 0)
        {
            return 0f;
        }

        // Bir satirin toplam genisligi (ortalamak icin) ONCE bilinmeli --
        // "doldukca yaz" tek gecisli bir yaklasim satiri bitirmeden
        // ortalayamaz. Bu yuzden once satir sinirlarini (rowStart/rowWidth)
        // cikariyoruz, sonra ikinci geciste yerlestiriyoruz.
        List<int> rowStart = new List<int>(4);
        List<float> rowWidth = new List<float>(4);

        int start = 0;
        float width = 0f;
        int count = 0;

        for (int i = 0; i < widths.Count; i++)
        {
            float itemWidth = widths[i];
            float next = count == 0 ? itemWidth : width + gapX + itemWidth;

            // count > 0 sarti BILEREK var: bir satirin TEK ogesi hicbir zaman
            // tasma yuzunden kendi satirina dusemez, yoksa cok genis bir oge
            // sonsuz bos satir uretirdi.
            if (count > 0 && next > availableWidth)
            {
                rowStart.Add(start);
                rowWidth.Add(width);

                start = i;
                width = itemWidth;
                count = 1;
                continue;
            }

            width = next;
            count++;
        }

        rowStart.Add(start);
        rowWidth.Add(width);

        float y = 0f;
        for (int row = 0; row < rowStart.Count; row++)
        {
            int first = rowStart[row];
            int last = row + 1 < rowStart.Count ? rowStart[row + 1] : widths.Count;

            float x = (availableWidth - rowWidth[row]) * 0.5f;
            for (int i = first; i < last; i++)
            {
                buffer.Add(new FlowItem(row, x, y));
                x += widths[i] + gapX;
            }

            y += itemHeight + gapY;
        }

        // Son satirin ARTIK ALTINDAKI bosluk (gapY) toplam yukseklige dahil
        // edilmiyor -- yukseklik "son satirin alt kenari", bir sonraki aralik
        // degil.
        return y > 0f ? y - gapY : 0f;
    }
}
