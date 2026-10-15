using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Yakilan parcanin GORUNUMUNUN EditMode testleri: BurnedPaintingArt'in saf
// cekirdegi (soldurma zinciri, 45 derecelik tarama, parca bloklarinin
// bestelenmesi) ve PaintPixels.PieceBounds'un geometrisi.
//
// Gorunum iki ayri cizici tarafindan DEGIL, tek yerde uretildigi icin burada
// pinlenmesi gerekiyor: ekranda yanlis cikan bir delik hicbir derleyici
// hatasi vermez (bkz. CLAUDE.md > yakilan parca).
public class BurnedPaintingArtTests
{
    private static HabitData MakeHabit(int canvasSize, params int[] missed)
    {
        HabitData habit = new HabitData
        {
            id = "h1",
            title = "Test",
            habitType = HabitTypes.Build,
            canvasSize = canvasSize,
            activePaintingId = "p0",
            startDate = HabitScheduling.ToKey(new DateTime(2020, 1, 1))
        };

        habit.missedPixels.AddRange(missed);
        return habit;
    }

    private static PaintingDefinition MakeDefinition(int pieceCount, bool sequential = true)
    {
        PaintingDefinition definition = ScriptableObject.CreateInstance<PaintingDefinition>();
        definition.id = "p0";
        definition.resolution = (PaintPixels.OverlayResolution)pieceCount;
        definition.revealOrder = sequential
            ? PaintPixels.RevealOrder.Sequential
            : PaintPixels.RevealOrder.Random;
        return definition;
    }

    private static Color32[] Fill(int width, int height, Color32 color)
    {
        Color32[] pixels = new Color32[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }

        return pixels;
    }

    // -----------------------------------------------------------------------
    // Burn: grayscale(1) -> sepia(.35) -> brightness(.62)
    // -----------------------------------------------------------------------

    [Test]
    public void Burn_Black_StaysBlack()
    {
        Color32 burned = BurnedPaintingArt.Burn(new Color32(0, 0, 0, 255));

        Assert.AreEqual(0, burned.r);
        Assert.AreEqual(0, burned.g);
        Assert.AreEqual(0, burned.b);
    }

    [Test]
    public void Burn_White_LandsOnSepiaDarkGrey()
    {
        Color32 burned = BurnedPaintingArt.Burn(new Color32(255, 255, 255, 255));

        // g = 1 oldugu icin sonuc dogrudan zincirin carpani:
        // 0.696167 / 0.664051 / 0.606329 -> 178 / 169 / 155.
        Assert.AreEqual(178, burned.r);
        Assert.AreEqual(169, burned.g);
        Assert.AreEqual(155, burned.b);
    }

    [Test]
    public void Burn_PreservesAlpha()
    {
        Assert.AreEqual(0, BurnedPaintingArt.Burn(new Color32(200, 30, 10, 0)).a);
        Assert.AreEqual(140, BurnedPaintingArt.Burn(new Color32(200, 30, 10, 140)).a);
    }

    // grayscale(1)'in anlami TAM OLARAK bu: ayni parlakliktaki iki ayri renk ayni
    // sonuca dusuyor. Soldurmayi "sabit bir renge lerp" diye sadelestiren bir
    // degisiklik tam burada yakalanir -- lerp tonu korur, grayscale korumaz.
    [Test]
    public void Burn_EqualLuma_DifferentHues_ProduceSameColor()
    {
        // Luma katsayilari 0.2126 / 0.7152 / 0.0722: kirmizidan 255, yesilden
        // 76 (~0.2126 * 255 / 0.7152) yaklasik ayni parlakligi veriyor.
        Color32 red = BurnedPaintingArt.Burn(new Color32(255, 0, 0, 255));
        Color32 green = BurnedPaintingArt.Burn(new Color32(0, 76, 0, 255));

        Assert.AreEqual(red.r, green.r, "ayni luma -> ayni kanal");
        Assert.AreEqual(red.g, green.g);
        Assert.AreEqual(red.b, green.b);

        // Ve sonuc gercekten GRI DEGIL, sepia: kanallar birbirinden ayri.
        Assert.Greater(red.r, red.g);
        Assert.Greater(red.g, red.b);
    }

    [Test]
    public void Burn_IsMonotonicInLuma()
    {
        byte dark = BurnedPaintingArt.Burn(new Color32(40, 40, 40, 255)).r;
        byte mid = BurnedPaintingArt.Burn(new Color32(120, 120, 120, 255)).r;
        byte light = BurnedPaintingArt.Burn(new Color32(240, 240, 240, 255)).r;

        Assert.Less(dark, mid);
        Assert.Less(mid, light);
    }

    [Test]
    public void Burn_AlwaysDarkensAndDesaturates()
    {
        // brightness(.62) tavani: en parlak sonuc bile 255'in cok altinda.
        for (int value = 0; value <= 255; value += 17)
        {
            Color32 burned = BurnedPaintingArt.Burn(new Color32((byte)value, (byte)value, (byte)value, 255));
            Assert.LessOrEqual(burned.r, 178);
            Assert.LessOrEqual(burned.r, value);
        }
    }

    // -----------------------------------------------------------------------
    // HatchCoverage: 45 derece, 1.5 px cizgi, 4 px periyot
    // -----------------------------------------------------------------------

    [Test]
    public void HatchCoverage_OverOnePeriod_AveragesLineRatio()
    {
        // Periyot boyunca ortalama kapsama cizgi / periyot = 1.5 / 4 = 0.375.
        float total = 0f;
        int samples = 0;

        for (int v = 0; v < 16; v++)
        {
            for (int u = 0; u < 16; u++)
            {
                total += BurnedPaintingArt.HatchCoverage(u, v);
                samples++;
            }
        }

        Assert.AreEqual(0.375f, total / samples, 0.03f);
    }

    [Test]
    public void HatchCoverage_IsConstantAlongTheDiagonal()
    {
        // 45 derece: cizgiler x + y = sabit dogrulari boyunca uzaniyor, yani
        // (u, v) ile (u + 1, v - 1) ayni kapsamada.
        for (int u = 0; u < 12; u++)
        {
            for (int v = 1; v < 12; v++)
            {
                Assert.AreEqual(
                    BurnedPaintingArt.HatchCoverage(u, v),
                    BurnedPaintingArt.HatchCoverage(u + 1, v - 1),
                    1e-5f,
                    "u=" + u + " v=" + v);
            }
        }
    }

    [Test]
    public void HatchCoverage_HasBothCoveredAndEmptyTexels()
    {
        bool covered = false;
        bool empty = false;

        for (int u = 0; u < 8; u++)
        {
            float coverage = BurnedPaintingArt.HatchCoverage(u, 0);
            covered |= coverage > 0.5f;
            empty |= coverage <= 0f;
        }

        Assert.IsTrue(covered, "taranan texel bulunmali");
        Assert.IsTrue(empty, "bos texel bulunmali -- yoksa duz dolgu olurdu");
    }

    // -----------------------------------------------------------------------
    // Compose
    // -----------------------------------------------------------------------

    [Test]
    public void Compose_NoMissedPieces_IsPlainNearestUpscale()
    {
        const int width = 8;
        const int height = 2;

        Color32[] source = new Color32[width * height];
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = new Color32((byte)(i * 7), 90, 200, 255);
        }

        Color32[] destination = new Color32[width * 2 * height * 2];
        PaintingPieceState state = PaintingPieceState.For(MakeDefinition(16), MakeHabit(16));

        BurnedPaintingArt.Compose(source, width, height, state, 16, destination);

        int destinationWidth = width * BurnedPaintingArt.Scale;

        for (int y = 0; y < height * BurnedPaintingArt.Scale; y++)
        {
            for (int x = 0; x < destinationWidth; x++)
            {
                Color32 expected = source[(y / BurnedPaintingArt.Scale) * width + x / BurnedPaintingArt.Scale];
                Color32 actual = destination[y * destinationWidth + x];

                Assert.AreEqual(expected.r, actual.r, "x=" + x + " y=" + y);
                Assert.AreEqual(expected.g, actual.g);
                Assert.AreEqual(expected.b, actual.b);
                Assert.AreEqual(expected.a, actual.a);
            }
        }
    }

    [Test]
    public void Compose_MissedPiece_OnlyTouchesItsOwnBlock()
    {
        // 16 parca -> 8 x 2 izgara. Kaynak 8 x 2 piksel, hedef 16 x 4: her parca
        // tam 2 x 2 texel.
        const int width = 8;
        const int height = 2;
        Color32 source = new Color32(255, 255, 255, 255);

        Color32[] destination = new Color32[width * 2 * height * 2];
        PaintingPieceState state = PaintingPieceState.For(MakeDefinition(16), MakeHabit(16, 0));

        BurnedPaintingArt.Compose(Fill(width, height, source), width, height, state, 16, destination);

        int destinationWidth = width * BurnedPaintingArt.Scale;
        int destinationHeight = height * BurnedPaintingArt.Scale;

        // Parca 0 UST-SOL: texture'da y = 0 ALTTA oldugu icin ust satirlar
        // dizinin SONUNDA (bkz. PaintPixels.TextureRowForPiece).
        PaintPixels.PieceBounds(
            0, destinationWidth, destinationHeight, 2,
            out int startX, out int endX, out int startY, out int endY);

        Assert.AreEqual(0, startX);
        Assert.AreEqual(2, endX);
        Assert.AreEqual(2, startY, "parca 0 texture'in UST satirinda");
        Assert.AreEqual(4, endY);

        int changed = 0;

        for (int y = 0; y < destinationHeight; y++)
        {
            for (int x = 0; x < destinationWidth; x++)
            {
                Color32 pixel = destination[y * destinationWidth + x];
                bool inPiece = x >= startX && x < endX && y >= startY && y < endY;

                if (inPiece)
                {
                    Assert.Less(pixel.r, source.r, "yakilan parca soldurulmali (x=" + x + " y=" + y + ")");
                    changed++;
                }
                else
                {
                    Assert.AreEqual(source.r, pixel.r, "komsu parca DEGISMEMELI (x=" + x + " y=" + y + ")");
                    Assert.AreEqual(source.g, pixel.g);
                    Assert.AreEqual(source.b, pixel.b);
                }
            }
        }

        Assert.AreEqual(4, changed, "2 x 2 texel");
    }

    [Test]
    public void Compose_MissedPiece_HatchDarkensSomeTexelsMoreThanOthers()
    {
        // 64 parca -> 8 x 8; kaynak 16 x 16 piksel, hedef 32 x 32: parca 4 x 4
        // texel, yani 4 px periyotlu tarama parcanin icinde gorunur.
        const int size = 16;

        Color32[] destination = new Color32[size * 2 * size * 2];
        PaintingPieceState state = PaintingPieceState.For(MakeDefinition(64), MakeHabit(64, 9));

        BurnedPaintingArt.Compose(
            Fill(size, size, new Color32(255, 255, 255, 255)),
            size, size, state, 64, destination);

        int destinationSize = size * BurnedPaintingArt.Scale;
        PaintPixels.PieceBounds(
            9, destinationSize, destinationSize, 8,
            out int startX, out int endX, out int startY, out int endY);

        byte darkest = 255;
        byte lightest = 0;

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                byte value = destination[y * destinationSize + x].r;
                darkest = Math.Min(darkest, value);
                lightest = Math.Max(lightest, value);
            }
        }

        // Taranan texel taranmayandan KOYU: duz bir soldurma olsa ikisi esit olurdu.
        Assert.Greater(lightest, darkest, "tarama parcanin icinde gorunmeli");

        // Taranmayan texel saf soldurmaya esit.
        Assert.AreEqual(BurnedPaintingArt.Burn(new Color32(255, 255, 255, 255)).r, lightest);
    }

    [Test]
    public void Compose_HatchPhaseRestartsOnEveryPiece()
    {
        // Tasarimda tarama her hucrenin KENDI background'i, yani faz hucre basinda
        // sifirlaniyor: iki komsu delik birebir ayni deseni tasiyor.
        const int size = 16;

        Color32[] destination = new Color32[size * 2 * size * 2];
        PaintingPieceState state = PaintingPieceState.For(MakeDefinition(64), MakeHabit(64, 9, 10));

        BurnedPaintingArt.Compose(
            Fill(size, size, new Color32(255, 255, 255, 255)),
            size, size, state, 64, destination);

        int destinationSize = size * BurnedPaintingArt.Scale;

        PaintPixels.PieceBounds(9, destinationSize, destinationSize, 8,
            out int leftX, out _, out int leftY, out int leftEndY);
        PaintPixels.PieceBounds(10, destinationSize, destinationSize, 8,
            out int rightX, out _, out int rightY, out _);

        Assert.AreEqual(leftY, rightY, "ayni satirdaki iki parca");

        for (int y = 0; y < leftEndY - leftY; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                Assert.AreEqual(
                    destination[(leftY + y) * destinationSize + leftX + x].r,
                    destination[(rightY + y) * destinationSize + rightX + x].r,
                    "faz her parcada bastan baslamali (x=" + x + " y=" + y + ")");
            }
        }
    }

    [Test]
    public void Compose_IgnoresOutOfRangeMissedIndices()
    {
        const int width = 8;
        const int height = 2;
        Color32[] destination = new Color32[width * 2 * height * 2];

        PaintingPieceState state = PaintingPieceState.For(MakeDefinition(16), MakeHabit(16, -1, 16, 99));

        Assert.DoesNotThrow(() => BurnedPaintingArt.Compose(
            Fill(width, height, new Color32(255, 255, 255, 255)),
            width, height, state, 16, destination));

        for (int i = 0; i < destination.Length; i++)
        {
            Assert.AreEqual(255, destination[i].r, "gecersiz indeks hicbir seyi degistirmemeli");
        }
    }

    // -----------------------------------------------------------------------
    // PaintPixels.PieceBounds -- parca indeksi -> geometri, TEK yer
    // -----------------------------------------------------------------------

    [Test]
    public void PieceBounds_TilesTheTextureWithoutGapOrOverlap()
    {
        // 84 x 110: tam bolunmeyen boy (42 px tablo 2x ile 84 olur, 84 / 8 = 10.5).
        // Naif ters cevirme burada bir kayar; sinirlar uctan uca dosemeli.
        const int width = 84;
        const int height = 110;
        const int rows = 2;

        int[] hits = new int[width * height];

        for (int piece = 0; piece < PaintingGrid.Columns * rows; piece++)
        {
            PaintPixels.PieceBounds(piece, width, height, rows,
                out int startX, out int endX, out int startY, out int endY);

            Assert.Less(startX, endX, "parca " + piece + " bos olmamali");
            Assert.Less(startY, endY, "parca " + piece + " bos olmamali");

            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    hits[y * width + x]++;
                }
            }
        }

        for (int i = 0; i < hits.Length; i++)
        {
            Assert.AreEqual(1, hits[i], "her texel TAM BIR parcaya ait olmali (i=" + i + ")");
        }
    }

    [Test]
    public void PieceBounds_PieceZeroIsTopLeft()
    {
        const int size = 32;
        const int rows = 4;

        PaintPixels.PieceBounds(0, size, size, rows,
            out int startX, out int endX, out int startY, out int endY);

        Assert.AreEqual(0, startX, "sol");
        Assert.AreEqual(size / PaintingGrid.Columns, endX);
        // Texture'da y = 0 ALTTA: ust satir en buyuk y araligi.
        Assert.AreEqual(size, endY, "ust");
        Assert.AreEqual(size - size / rows, startY);
    }

    [Test]
    public void PieceBounds_AgreesWithTextureRowForPiece()
    {
        const int size = 64;
        const int rows = 8;

        for (int piece = 0; piece < 64; piece++)
        {
            PaintPixels.PieceBounds(piece, size, size, rows,
                out int startX, out _, out int startY, out _);

            int expectedColumn = piece % PaintingGrid.Columns;
            int expectedRow = PaintPixels.TextureRowForPiece(piece, rows);

            Assert.AreEqual(expectedColumn * size / PaintingGrid.Columns, startX, "parca " + piece);
            Assert.AreEqual(expectedRow * size / rows, startY, "parca " + piece);
        }
    }

    // -----------------------------------------------------------------------
    // BurnedPaintingCache
    // -----------------------------------------------------------------------

    [Test]
    public void SpriteFor_NoMissedPieces_ReturnsTheDefinitionsOwnSprite()
    {
        PaintingDefinition definition = MakeDefinition(16);
        definition.sprite = Sprite.Create(
            new Texture2D(8, 2), new Rect(0f, 0f, 8f, 2f), new Vector2(0.5f, 0.5f));

        Sprite result = BurnedPaintingCache.SpriteFor(definition, MakeHabit(16));

        Assert.AreSame(definition.sprite, result, "delik yoksa hicbir sey pisirilmemeli");
    }

    [Test]
    public void SpriteFor_UnreadableTexture_FallsBackToTheDefinitionsSprite()
    {
        // Testte uretilen Texture2D okunabilir; isReadable olmayan bir asset'i
        // EditMode'da uretemiyoruz, ama kayit yolu ayni: null donen Build
        // cagiranin eski gorunume dusmesine yol aciyor. Burada en azindan
        // sprite'i olmayan tanimin patlamadigini pinliyoruz.
        PaintingDefinition definition = MakeDefinition(16);

        Assert.IsNull(BurnedPaintingCache.SpriteFor(definition, MakeHabit(16, 0)));
        Assert.IsNull(BurnedPaintingCache.SpriteFor(null, MakeHabit(16, 0)));
    }

    [Test]
    public void SpriteFor_MissedPieces_BakesOnceAndReusesIt()
    {
        PaintingDefinition definition = MakeDefinition(16);
        Texture2D texture = new Texture2D(8, 2, TextureFormat.RGBA32, false);
        texture.SetPixels32(Fill(8, 2, new Color32(255, 255, 255, 255)));
        texture.Apply(false);
        definition.sprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 2f), new Vector2(0.5f, 0.5f), 8f);

        HabitData habit = MakeHabit(16, 0);

        Sprite first = BurnedPaintingCache.SpriteFor(definition, habit);
        Assert.IsNotNull(first);
        Assert.AreNotSame(definition.sprite, first);
        Assert.AreSame(first, BurnedPaintingCache.SpriteFor(definition, habit), "ayni imza -> ayni kopya");

        // Dunya dikdortgeni aynen korunmali: PaintPixels kadraji bounds'tan
        // turetiyor (bkz. BurnedPaintingArt.Build).
        Assert.AreEqual(definition.sprite.bounds.size.x, first.bounds.size.x, 1e-4f);
        Assert.AreEqual(definition.sprite.bounds.size.y, first.bounds.size.y, 1e-4f);

        // Delik kumesi degisince yeniden pisiyor.
        habit.missedPixels.Add(1);
        Assert.AreNotSame(first, BurnedPaintingCache.SpriteFor(definition, habit));

        BurnedPaintingCache.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        BurnedPaintingCache.Clear();
    }

    // Kullanilmayan degisken uyarisi cikmasin diye: ustteki testler List<int>
    // uzerinden gidiyor, bu yardimci yalnizca okunabilirlik icin duruyor.
    private static List<int> Missed(params int[] indices)
    {
        return new List<int>(indices);
    }
}
