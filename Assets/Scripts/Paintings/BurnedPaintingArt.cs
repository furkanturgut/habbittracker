using UnityEngine;

// Yakilan (kacirilan gun) parcanin GORUNUMU -- tek yer.
//
// Tasarim (Galeri handoff, "Piece states"): yakilan parca tabloyu GIZLEMEZ,
// soldurur. Parca pikselleri `grayscale(1) sepia(.35) brightness(.62)` suzgecinden
// geciyor, uzerine 45 derecelik bir tarama (hatch) biniyor: 1.5 pt cizgi,
// 4 pt periyot, rgba(43,27,18,.45).
//
// NEDEN BURADA, PERDEDE / ORTUDE DEGIL: tabloyu iki ayri cizici gosteriyor
// (PaintPixels'in RenderTexture yolu ve uGUI'deki PaintingCoverGrid) ve ikisi de
// ALTINDAKINI SOLDURAMAZ -- alfa harmanlamasi grayscale uretemez, projede de
// shader yok. Bu yuzden gorunum tablonun KENDI piksellerine pisiriliyor: her iki
// cizici de yakilan parcada artik hicbir sey cizmiyor (perde saydam, ortu hucresi
// saydam) ve gorunumun tek kopyasi burada kaliyor. Kopya olmayinca iki yol
// birbirinden ayrisamiyor.
//
// Olcek 2: tasarimin kendi modeli ("point filtering, integer scale 2x"), ve 2x'te
// handoff'un sayilari BIRE BIR piksel: 4 px periyot, 1.5 px cizgi.
public static class BurnedPaintingArt
{
    // Tablo bu kat sayida buyutulerek pisiriliyor (nearest, tam sayi -> bulanma yok).
    public const int Scale = 2;

    private const float HatchPeriod = 4f;
    private const float HatchLine = 1.5f;

    // rgba(43,27,18,.45) -- .45 * 255 = 114.75 -> 115.
    private static readonly Color32 HatchColor = new Color32(0x2B, 0x1B, 0x12, 115);

    // CSS filtreleri sRGB uzerinde tanimli, bu yuzden hesap da 0..1 sRGB'de.
    private const float LumaR = 0.2126f;
    private const float LumaG = 0.7152f;
    private const float LumaB = 0.0722f;

    // grayscale(1) -> sepia(.35) -> brightness(.62) zinciri TEK bir carpana iniyor:
    //   grayscale(1): R=G=B = g = 0.2126R + 0.7152G + 0.0722B
    //   sepia(a) bir griye uygulandiginda her satir kendi satir toplamina carpiyor;
    //   sepia(1) satir toplamlari 1.351 / 1.203 / 0.937, a=.35 ile lerp edilince
    //   1.12285 / 1.07105 / 0.97795
    //   brightness(.62): hepsi x 0.62
    // => rgb = (0.696167, 0.664051, 0.606329) * g
    private const float BurnR = 0.696167f;
    private const float BurnG = 0.664051f;
    private const float BurnB = 0.606329f;

    // Tek pikselin yanmis hali. Alfaya DOKUNMAZ: tablonun saydam kenari saydam kalir.
    public static Color32 Burn(Color32 source)
    {
        float g = (source.r * LumaR + source.g * LumaG + source.b * LumaB) / 255f;

        return new Color32(
            ToByte(g * BurnR),
            ToByte(g * BurnG),
            ToByte(g * BurnB),
            source.a);
    }

    // Taramanin bir texel'i ne kadar kapladigi (0..1).
    //
    // (u, v) parcanin KENDI sol ustune gore: tasarimda tarama her hucrenin kendi
    // `background`'i, yani faz her hucrede bastan basliyor -- komsu iki delikte
    // cizgiler hizalanmiyor ve bu kasitli.
    //
    // 2x2 ust-ornekleme: 45 derecelik kenar tek esikle merdiven gibi cikardi,
    // tarayici da gradyanin kenarini yumusatiyor.
    public static float HatchCoverage(int u, int v)
    {
        int hits = 0;

        for (int sy = 0; sy < 2; sy++)
        {
            for (int sx = 0; sx < 2; sx++)
            {
                float x = u + 0.25f + sx * 0.5f;
                float y = v + 0.25f + sy * 0.5f;

                // 45 derece: cizgiler x + y = sabit dogrulari boyunca uzaniyor.
                float d = (x + y) * 0.70710678f;

                if (Mathf.Repeat(d, HatchPeriod) < HatchLine)
                {
                    hits++;
                }
            }
        }

        return hits * 0.25f;
    }

    public static int DestinationWidth(int sourceWidth)
    {
        return sourceWidth * Scale;
    }

    public static int DestinationHeight(int sourceHeight)
    {
        return sourceHeight * Scale;
    }

    // Tamponlar girer, tamponlar cikar: testlerin surdugu saf cekirdek.
    //
    // Her iki dizi de Texture2D duzeninde (satir 0 = EN ALT), cunku kaynak
    // GetPixels32'den geliyor ve sonuc SetPixels32'ye gidiyor.
    public static void Compose(
        Color32[] source,
        int sourceWidth,
        int sourceHeight,
        in PaintingPieceState state,
        int pieceCount,
        Color32[] destination)
    {
        int width = DestinationWidth(sourceWidth);
        int height = DestinationHeight(sourceHeight);

        // 1) Duz nearest buyutme. Tam sayi olcek oldugu icin her kaynak pikseli
        // Scale x Scale'lik bir bloga birebir dusuyor.
        for (int y = 0; y < height; y++)
        {
            int sourceRow = (y / Scale) * sourceWidth;
            int row = y * width;

            for (int x = 0; x < width; x++)
            {
                destination[row + x] = source[sourceRow + x / Scale];
            }
        }

        // 2) Yalnizca yakilan parcalarin bloklarini ezeriz.
        int rows = PaintingGrid.RowsFor(pieceCount);
        if (rows <= 0 || state.MissedPieces == null)
        {
            return;
        }

        for (int i = 0; i < state.MissedPieces.Count; i++)
        {
            int piece = state.MissedPieces[i];
            if (piece < 0 || piece >= pieceCount)
            {
                continue;
            }

            // Geometri PaintPixels'ten geliyor: perdenin yazdigi blokla AYNI
            // blok olmak zorunda, ikinci bir kopya cikarmak yok
            // (bkz. CLAUDE.md > parca indeksi -> geometri tek yerde).
            PaintPixels.PieceBounds(
                piece, width, height, rows,
                out int startX, out int endX, out int startY, out int endY);

            for (int y = startY; y < endY; y++)
            {
                int row = y * width;

                // v parcanin UST satirindan asagiya sayiyor (ekran yonu);
                // texture'da y = 0 alttadir.
                int v = endY - 1 - y;

                for (int x = startX; x < endX; x++)
                {
                    destination[row + x] = HatchOver(
                        Burn(destination[row + x]),
                        HatchCoverage(x - startX, v));
                }
            }
        }
    }

    // Tabloyu yakilan parcalariyla birlikte yeni bir sprite'a pisirir.
    //
    // null donerse cagiran ESKI gorunume duser (duz missedColor / lockedColor):
    // kaynak texture okunabilir degilse (PaintingLibraryBuilder'in isReadable'i
    // uygulanmamissa) GetPixels32 patlar, o yuzden once sorup cikiyoruz.
    public static Sprite Build(Sprite source, in PaintingPieceState state, int pieceCount)
    {
        if (source == null || source.texture == null || !source.texture.isReadable)
        {
            return null;
        }

        Rect rect = source.textureRect;
        int sourceWidth = Mathf.RoundToInt(rect.width);
        int sourceHeight = Mathf.RoundToInt(rect.height);

        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            return null;
        }

        Color32[] sourcePixels = source.texture.GetPixels32();

        // Sprite tek basina bir PNG olsa da textureRect'i kirpmak dogrusu:
        // atlasa girmis bir sprite'ta tum texture yanlis olurdu.
        if (sourceWidth != source.texture.width || sourceHeight != source.texture.height)
        {
            sourcePixels = Crop(
                sourcePixels,
                source.texture.width,
                Mathf.RoundToInt(rect.x),
                Mathf.RoundToInt(rect.y),
                sourceWidth,
                sourceHeight);
        }

        int width = DestinationWidth(sourceWidth);
        int height = DestinationHeight(sourceHeight);
        Color32[] pixels = new Color32[width * height];

        Compose(sourcePixels, sourceWidth, sourceHeight, state, pieceCount, pixels);

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = source.name + " (burned)",
            // Piksel sanati: bilinear tabloyu bulaniklastirirdi.
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        texture.SetPixels32(pixels);
        texture.Apply(false);

        // pixelsPerUnit de Scale kadar buyuyor: PaintPixels kadraji ve perdeyi
        // backgroundSprite.bounds'tan turettigi icin kopya tablonun DUNYA
        // dikdortgeni aslininkiyle TAM AYNI olmak zorunda.
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            source.pixelsPerUnit * Scale,
            0,
            SpriteMeshType.FullRect);
    }

    // Taramayi yanmis pikselin uzerine normal (source-over) harmanlar. Opak bir
    // kaynakta sonuc duz bir lerp'e iniyor; saydam kenarda ise tasarimdaki gibi
    // tarama kendi alfasiyla gorunur kaliyor (hucrenin background'i tablonun
    // saydamligini maskelemiyor).
    private static Color32 HatchOver(Color32 burned, float coverage)
    {
        if (coverage <= 0f)
        {
            return burned;
        }

        float sa = HatchColor.a / 255f * coverage;
        float da = burned.a / 255f;
        float outA = sa + da * (1f - sa);

        if (outA <= 0f)
        {
            return new Color32(0, 0, 0, 0);
        }

        float keep = da * (1f - sa);
        float inverse = 1f / outA;

        return new Color32(
            ToByte((HatchColor.r / 255f * sa + burned.r / 255f * keep) * inverse),
            ToByte((HatchColor.g / 255f * sa + burned.g / 255f * keep) * inverse),
            ToByte((HatchColor.b / 255f * sa + burned.b / 255f * keep) * inverse),
            ToByte(outA));
    }

    private static Color32[] Crop(
        Color32[] pixels, int stride, int originX, int originY, int width, int height)
    {
        Color32[] result = new Color32[width * height];

        for (int y = 0; y < height; y++)
        {
            int sourceRow = (originY + y) * stride + originX;
            int row = y * width;

            for (int x = 0; x < width; x++)
            {
                result[row + x] = pixels[sourceRow + x];
            }
        }

        return result;
    }

    private static byte ToByte(float value)
    {
        return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
    }
}
