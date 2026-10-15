using System.IO;
using UnityEditor;
using UnityEngine;

// Arayuzun ihtiyac duydugu sprite'lari uretir: yuvarlak koseli 9-slice kutular,
// bir daire, tek piksel beyaz ve ressamin ayak golgesi.
//
// Projede hic UI sprite'i yoktu (butun spriteBorder'lar 0) ve tasarim yariçapi
// 5 ile 30 arasinda degisen kutular istiyor. Disaridan varlik indirmek yerine
// uretiyoruz: kaynak kodda duran birkac satir matematik, ikili dosya bagimliligi
// ve lisans sorusu birakmiyor.
//
// Yeniden calistirilabilir: PNG icerigi aynıysa dosyaya dokunulmuyor, ama import
// ayarlari her seferinde dogrulaniyor.
public static class UiSpriteGenerator
{
    public const string Folder = "Assets/Sprites/UI";

    // Tasarimdaki yariçaplar (radius: 5, 6, 12, 14, 18, 20, 22, 30).
    public static readonly int[] Radii = { 5, 6, 12, 14, 18, 20, 22, 30 };

    public const string CircleName = "ui_circle";
    public const string WhiteName = "ui_white";
    public const string ShadowName = "studio_shadow";
    public const string CheckName = "ui_check";
    public const string SoftShadowName = "ui_shadow_box";
    public const string HatchName = "ui_hatch";
    public const int HatchSize = 4;

    // Seri kutlamasinin iki parcasi. Ikisi de BEYAZ uretiliyor ve rengi
    // Image.color'dan aliyor -- basamak basina dokuz ayri doku uretmek yerine
    // tek doku, tema renginin dokuz satiri (bkz. UiTheme.FlameTier).
    public const string RadialGlowName = "ui_radial_glow";
    public const string RayRingName = "ui_ray_ring";
    private const int CelebrationSpriteSize = 256;

    public static string RoundedName(int radius)
    {
        return "ui_round_" + radius;
    }

    public static string PathFor(string spriteName)
    {
        return Folder + "/" + spriteName + ".png";
    }

    // Uretimin tamami. Kac dosyanin gercekten degistigini doner.
    public static int Build()
    {
        EditorAssetUtil.EnsureFolder("Assets/Sprites", "UI");

        int written = 0;

        for (int i = 0; i < Radii.Length; i++)
        {
            int radius = Radii[i];
            // Kenarlar yariçap kadar, ortada 2 piksellik esneyen bir alan kaliyor.
            int size = radius * 2 + 2;
            written += WriteSprite(RoundedName(radius), RoundedRect(size, radius), size, size, radius, FilterMode.Bilinear);
        }

        written += WriteSprite(CircleName, RoundedRect(64, 32), 64, 64, 0, FilterMode.Bilinear);
        written += WriteSprite(WhiteName, Solid(4, Color.white), 4, 4, 0, FilterMode.Bilinear);
        written += WriteSprite(ShadowName, RadialFalloff(175, 18), 175, 18, 0, FilterMode.Bilinear);
        written += WriteSprite(CheckName, Checkmark(48), 48, 48, 0, FilterMode.Bilinear);

        // 9-slice kenari = golgenin tasma payi. Tek kaynak GalleryBands: cizim
        // tarafi (GalleryPrefabBuilder) golge rect'ini ayni sayiyla tasiriyor,
        // ikisi ayrismasin.
        int softShadowBorder = (int)GalleryBands.PaintingShadowSpread;
        int softShadowSize = softShadowBorder * 2 + 2;
        written += WriteSprite(
            SoftShadowName,
            SoftShadow(softShadowSize, softShadowBorder),
            softShadowSize,
            softShadowSize,
            softShadowBorder,
            FilterMode.Bilinear);

        // Point filtering: tarama piksel sanati gibi sert kalsin, komsu
        // dokularda oldugu gibi (Rounded/circle) yumusatma istenmiyor.
        written += WriteSprite(HatchName, Hatch(HatchSize), HatchSize, HatchSize, 0, FilterMode.Point);

        // Kutlama ekraninin zemini ve isin halkasi. Bilinear: ikisi de buyuk,
        // yumusak gradyanlar -- alevin piksel sanati Point kalirken BUNLARIN
        // Point olmasi bantlanma yapardi.
        written += WriteSprite(
            RadialGlowName, RadialGlow(CelebrationSpriteSize),
            CelebrationSpriteSize, CelebrationSpriteSize, 0, FilterMode.Bilinear);

        written += WriteSprite(
            RayRingName, RayRing(CelebrationSpriteSize),
            CelebrationSpriteSize, CelebrationSpriteSize, 0, FilterMode.Bilinear);

        return written;
    }

    // -----------------------------------------------------------------------
    // Piksel uretimi
    // -----------------------------------------------------------------------

    // Yuvarlak koseli kare. Renk her zaman beyaz; renklendirme Image.color ile
    // yapiliyor, boylece tek sprite butun tonlarda kullanilabiliyor.
    //
    // Kenar yumusatmasi isaretli mesafe fonksiyonundan geliyor: piksel merkezinin
    // sekil sinirina uzakligi alfaya cevriliyor. Supersampling'e gerek yok.
    private static Color32[] RoundedRect(int size, int radius)
    {
        Color32[] pixels = new Color32[size * size];
        float half = size * 0.5f;
        float inner = half - radius;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Abs(x + 0.5f - half) - inner;
                float py = Mathf.Abs(y + 0.5f - half) - inner;

                float qx = Mathf.Max(px, 0f);
                float qy = Mathf.Max(py, 0f);
                float distance = Mathf.Sqrt(qx * qx + qy * qy) - radius;

                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - distance) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        return pixels;
    }

    private static Color32[] Solid(int size, Color color)
    {
        Color32[] pixels = new Color32[size * size];
        Color32 value = color;

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = value;
        }

        return pixels;
    }

    // Ressamin ayaginin altindaki elips golge. Tasarimdaki radial-gradient'in
    // karsiligi: merkezde opak, kenarda tamamen seffaf.
    private static Color32[] RadialFalloff(int width, int height)
    {
        Color32[] pixels = new Color32[width * height];
        float cx = width * 0.5f;
        float cy = height * 0.5f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = (x + 0.5f - cx) / cx;
                float dy = (y + 0.5f - cy) / cy;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                // Kareli dusus, CSS'teki closest-side radial-gradient'e yakin duruyor.
                float alpha = Mathf.Clamp01(1f - distance);
                alpha *= alpha;

                pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        return pixels;
    }

    // Kutlama ekraninin zemini: ortada opak, %70 yaricapta tamamen seffaf.
    // Teslim dosyasinin "radial-gradient(90% 70% at 50% 38%, {glow} 0%, #2b1b12
    // 70%)" ifadesinin alfa karsiligi -- ikinci renk (#2b1b12) burada DEGIL,
    // altta duran duz zeminde, cunku tek bir sprite iki rengi tasiyamaz ve
    // zemin rengi zaten temada var (theme.background).
    //
    // ShadowName'in RadialFalloff'u ile ayni fikir ama ayri bir fonksiyon:
    // orasi 175x18'lik bir ELIPS (ressamin ayak golgesi) ve dususu kenarda
    // bitiyor; burasi kare, ve dusus %70'te bitip kalan kenar BOSLUK kaliyor.
    private static Color32[] RadialGlow(int size)
    {
        Color32[] pixels = new Color32[size * size];
        float center = size * 0.5f;

        // Parlama sprite'in kenarina DEGMEDEN sonmeli: koselerde ani bir
        // kesilme olursa kutlama zemininde gorunur bir kare kenari olusur.
        const float edge = 0.7f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - center) / center;
                float dy = (y + 0.5f - center) / center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                float t = Mathf.Clamp01(1f - distance / edge);

                // Kareli dusus (RadialFalloff ile ayni egri): merkeze yakin
                // genis ve dolu, disa dogru hizla soniyor.
                float alpha = t * t;

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        return pixels;
    }

    // Kutlamanin donen isin halkasi. Teslim dosyasi:
    // "repeating-conic-gradient({num}33 0 12deg, transparent 12deg 30deg)"
    // bir "radial-gradient(closest-side, #000 40%, transparent)" maskesiyle.
    //
    // Iki carpan: acisal dilim (12 derece dolu, 18 derece bos -> 30 derecelik
    // periyot, 12 dilim) ve yaricapsal maske (merkezden %40'a kadar tam opak,
    // oradan kenara kadar soluyor). %20 alfa sprite'ta DEGIL Image.color'da --
    // renk zaten oradan geliyor ve ikisini carpmak gereksiz bant yaratirdi.
    private static Color32[] RayRing(int size)
    {
        Color32[] pixels = new Color32[size * size];
        float center = size * 0.5f;

        const float periodDegrees = 30f;
        const float wedgeDegrees = 12f;
        const float solidRadius = 0.4f;

        // Dilim kenarindaki merdiven basamaklarini silen yumusatma payi
        // (derece). Point degil Bilinear filtre kullaniyoruz ama 256 pikselde
        // bir dilim kenari yine de sert durur.
        const float wedgeFeather = 1.2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - center) / center;
                float dy = (y + 0.5f - center) / center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                // Yaricapsal maske: %40'a kadar 1, kenarda 0.
                float radial = distance <= solidRadius
                    ? 1f
                    : Mathf.Clamp01(1f - (distance - solidRadius) / (1f - solidRadius));

                // Acisal dilim. Atan2 [-180,180] veriyor; periyoda indirgemek
                // icin once pozitife tasiniyor.
                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 180f;
                float phase = angle - Mathf.Floor(angle / periodDegrees) * periodDegrees;

                float wedge = Mathf.Clamp01(phase / wedgeFeather)
                    * Mathf.Clamp01((wedgeDegrees - phase) / wedgeFeather);

                float alpha = radial * radial * wedge;

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        return pixels;
    }

    // Tablolarin altindaki YUMUSAK kutu golgesi. Tasarimda yumusak golge TEK
    // yerde var ("soft shadows only under paintings": 0 6px 10px rgba(0,0,0,.45))
    // ve projedeki tek golge sprite'i (studio_shadow) ressamin ayagi icin cizilmis
    // bir ELIPS -- dikdortgen bir tablonun altinda kullanilamaz.
    //
    // Gecis sprite'in KENDI kenarindan iceri dogru "border" piksel boyunca
    // 0 -> 1 kosuyor, yani dolu olan tam ORTASI. Cizen taraf merkezi
    // fillCenter=false ile ATIYOR ve geriye yalnizca halka kaliyor: CSS'te de
    // dis golge kutunun icine cizilmez, tablo PNG'lerinin kose pikselleri
    // seffaf oldugu icin dolu bir merkez oralardan koyu leke olarak sizardi.
    private static Color32[] SoftShadow(int size, int border)
    {
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            float fy = EdgeFalloff(y, size, border);

            for (int x = 0; x < size; x++)
            {
                // Tepe deger 0.5, 1 degil: bir kutu golgesinin bulanikligi tam
                // kutu kenarinda kapamayi YARILAR (kenarin yarisi kutu, yarisi
                // dis). Halkanin ic kenari da tam o cizgide durdugu icin dogru
                // baslangic 0.5 -- 1 olsa golge tasarimdakinin iki kati koyu
                // gorunurdu.
                float alpha = 0.5f * fy * EdgeFalloff(x, size, border);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        return pixels;
    }

    // Tek eksende kenardan iceri 0 -> 1 yumusak gecis. Iki eksenin CARPIMI
    // alinıyor (SoftShadow'da) cunku bir kutunun ayrilabilir gauss bulanikligi
    // tam olarak bunu verir -- koseler ayrica hesaplanmiyor, kendiliginden dogru.
    private static float EdgeFalloff(int index, int size, int border)
    {
        float center = index + 0.5f;
        float distance = Mathf.Min(center, size - center);
        float t = Mathf.Clamp01(distance / border);
        return t * t * (3f - 2f * t);
    }

    // Delik cipindeki 45-derece caprazlama (Galeri Seviye 3). Teslim dosyasi:
    // "repeating-linear-gradient(45deg, rgba(243,230,204,.35) 0 1.5px,
    // #5b4a3c 1.5px 4px)". size=4 TAM bir periyot oldugu icin Tiled bir
    // Image bunu kenarsiz tekrarlar -- boyut baska bir deger olursa dikis
    // gorunur.
    private static Color32[] Hatch(int size)
    {
        Color32 baseColor = new Color32(0x5B, 0x4A, 0x3C, 0xFF);
        Color32 lineColor = Blend(baseColor, new Color32(0xF3, 0xE6, 0xCC, 0xFF), 0.35f);

        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float diagonal = PositiveMod(x + y, size);
                pixels[y * size + x] = diagonal < 1.5f ? lineColor : baseColor;
            }
        }

        return pixels;
    }

    private static int PositiveMod(int value, int size)
    {
        int m = value % size;
        return m < 0 ? m + size : m;
    }

    // CSS'teki yari-saydam cizginin OPAK alttaki renkle onceden karistirilmis
    // hali -- sprite'in kendisi hep opak, Tiled gosterimde alfa karismasina
    // gerek yok.
    private static Color32 Blend(Color32 under, Color32 over, float alpha)
    {
        return new Color32(
            (byte)Mathf.RoundToInt(Mathf.Lerp(under.r, over.r, alpha)),
            (byte)Mathf.RoundToInt(Mathf.Lerp(under.g, over.g, alpha)),
            (byte)Mathf.RoundToInt(Mathf.Lerp(under.b, over.b, alpha)),
            255);
    }

    // "Bitti" rozetinin tik isareti. Once TMP metni olarak "✓" cizildi ama
    // Fredoka-Bold SDF (ve fallback'leri) bu karakteri icermiyordu -- eksik
    // glyph TextMeshPro'nun her karede atlas/duzen yeniden kurmaya calismasina
    // yol aciyordu (Canvas.ForceUpdateCanvases donguye giriyor, konsol
    // saniyede yuzlerce stack-trace ile doluyor, Editor donmus gibi kaliyor).
    // Font'a bagimli olmayan bir sprite CIZEREK bu sinifi tamamen atlatiyoruz.
    private static Color32[] Checkmark(int size)
    {
        Color32[] pixels = new Color32[size * size];
        float thickness = size * 0.14f;

        Vector2 a = new Vector2(size * 0.20f, size * 0.52f);
        Vector2 b = new Vector2(size * 0.42f, size * 0.26f);
        Vector2 c = new Vector2(size * 0.82f, size * 0.74f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float distance = Mathf.Min(
                    DistanceToSegment(p, a, b),
                    DistanceToSegment(p, b, c)) - thickness * 0.5f;

                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - distance) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        return pixels;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float denom = Vector2.Dot(ab, ab);
        float t = denom > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / denom) : 0f;
        Vector2 closest = a + ab * t;
        return Vector2.Distance(p, closest);
    }

    // -----------------------------------------------------------------------
    // Disk
    // -----------------------------------------------------------------------

    // Boyut her zaman ACIKCA veriliyor. Daha once piksel sayisinin karekokunden
    // turetilen bir kisa yol vardi ve kare olmayan golge elipsinde (175x18)
    // sessizce 56x56'lik bir texture kurup SetPixels32'yi patlatiyordu.
    private static int WriteSprite(
        string spriteName,
        Color32[] pixels,
        int width,
        int height,
        int border,
        FilterMode filterMode)
    {
        string path = PathFor(spriteName);

        if (pixels.Length != width * height)
        {
            Debug.LogError(
                "UiSpriteGenerator: " + spriteName + " icin " + width + "x" + height + " istendi ama " +
                pixels.Length + " piksel uretildi.");
            return 0;
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply();

        byte[] png = texture.EncodeToPNG();
        Object.DestroyImmediate(texture);

        // Icerik aynıysa dosyaya dokunmuyoruz: her calistirmada surum kontrolunde
        // degismis gorunen bir dosya yigini birakmasin.
        bool changed = !File.Exists(path) || !SameBytes(File.ReadAllBytes(path), png);
        if (changed)
        {
            File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        ApplyImportSettings(path, border, filterMode);
        return changed ? 1 : 0;
    }

    private static bool SameBytes(byte[] left, byte[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }

    // Import ayarlari dosya degismese bile dogrulaniyor: 9-slice kenarlari
    // .meta'da duruyor ve elle bozulabilir.
    private static void ApplyImportSettings(string path, int border, FilterMode filterMode)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("UiSpriteGenerator: " + path + " icin TextureImporter yok.");
            return;
        }

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        Vector4 wanted = new Vector4(border, border, border, border);

        bool changed =
            importer.textureType != TextureImporterType.Sprite
            || importer.spriteImportMode != SpriteImportMode.Single
            || importer.filterMode != filterMode
            || importer.textureCompression != TextureImporterCompression.Uncompressed
            || importer.mipmapEnabled
            || !importer.alphaIsTransparency
            || !Mathf.Approximately(importer.spritePixelsPerUnit, 100f)
            || settings.spriteMeshType != SpriteMeshType.FullRect
            || settings.spriteBorder != wanted;

        if (!changed)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = filterMode;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritePixelsPerUnit = 100f;

        importer.ReadTextureSettings(settings);
        settings.spriteBorder = wanted;
        // FullRect: ui_hatch Image.Type.Tiled ile kullaniliyor, Tiled "Tight"
        // meshte dikisli/yanlis tekrarlar -- digerleri (Sliced/Simple) icin
        // mesh tipi zaten onemsiz, hepsine aym ayar zarar vermiyor.
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();
    }
}
