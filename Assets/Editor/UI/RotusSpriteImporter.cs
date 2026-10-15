using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Rotus (yanik parca onarimi) teslim klasorundeki sprite'lari projeye alir.
//
// FlameSpriteImporter'in birebir kalibi (kopyala, hedefte varsa DOKUNMA, import
// ayarlarini her calistirmada dogrula) -- kendi sabitleriyle, alev sabitlerini
// ODUNC ALMADAN: FrameCount/FrameSize burada kendi basina tanimli.
//
//   Rotus.png        128x32 -> 4 x (32x32)  Multiple, stok > 0 canli sayfa
//   Rotus_Static.png  32x32 -> Single       hareketsiz kullanim (isteğe bagli)
//   Rotus_Empty.png   32x32 -> Single       stok = 0, gri
//
// Kare adlari "Rotus_0".."Rotus_3": UiThemeBuilder temayi bu ada gore
// dolduruyor (bkz. FlameSpriteImporter.FrameName'in ayni gerekcesi).
public static class RotusSpriteImporter
{
    public const string TargetFolder = "Assets/Sprites/UI/Rotus";

    // Proje kokunun yanindaki teslim klasoru. Assets disinda oldugu icin Unity
    // onu gormuyor; dosyalari elle kopyaliyoruz.
    private const string HandoffFolder =
        "Painter Habits Mobile App - Rotus/design_handoff_rotus/sprites/rotus";

    public const int FrameCount = 4;
    public const int FrameSize = 32;

    private const string SheetFile = "Rotus.png";
    private const string StaticFile = "Rotus_Static.png";
    private const string EmptyFile = "Rotus_Empty.png";
    private const string SheetName = "Rotus";

    public static string SheetPath => TargetFolder + "/" + SheetFile;
    public static string StaticPath => TargetFolder + "/" + StaticFile;
    public static string EmptyPath => TargetFolder + "/" + EmptyFile;

    // Tek bir karenin alt-sprite adi. UiThemeBuilder bu adla ariyor.
    public static string FrameName(int frame)
    {
        return SheetName + "_" + frame;
    }

    // Kopyalanan dosya sayisini doner. Hedefte zaten varsa DOKUNULMAZ; import
    // ayarlari (ve dilimleme) yine de her calistirmada dogrulanir.
    public static int Import()
    {
        EditorAssetUtil.EnsureFolderPath(TargetFolder);

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string source = Path.Combine(projectRoot, HandoffFolder.Replace('/', Path.DirectorySeparatorChar));

        int copied = 0;
        copied += ImportOne(source, SheetFile, true);
        copied += ImportOne(source, StaticFile, false);
        copied += ImportOne(source, EmptyFile, false);
        return copied;
    }

    private static int ImportOne(string sourceFolder, string file, bool sheet)
    {
        string target = TargetFolder + "/" + file;
        int copied = 0;

        if (!File.Exists(target))
        {
            string from = Path.Combine(sourceFolder, file);
            if (!File.Exists(from))
            {
                Debug.LogError(
                    "RotusSpriteImporter: " + file + " teslim klasorunde bulunamadi (" + sourceFolder + "). " +
                    "Dosyayi elle " + TargetFolder + " altina koyun.");
                return 0;
            }

            File.Copy(from, target);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
            copied = 1;
        }

        if (sheet)
        {
            ApplySheetImportSettings(target);
        }
        else
        {
            ApplySingleImportSettings(target);
        }

        return copied;
    }

    // Dilimleme ISpriteEditorDataProvider ile yapiliyor, eski
    // TextureImporter.spritesheet ile DEGIL -- Unity 6'da o ozelligin destegi
    // KALDIRILDI (bkz. FlameSpriteImporter'daki ayni not).
    private static void ApplySheetImportSettings(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        bool changed =
            importer.textureType != TextureImporterType.Sprite
            || importer.spriteImportMode != SpriteImportMode.Multiple
            || importer.filterMode != FilterMode.Point
            || importer.textureCompression != TextureImporterCompression.Uncompressed
            || importer.mipmapEnabled
            || !importer.alphaIsTransparency
            || !Mathf.Approximately(importer.spritePixelsPerUnit, 100f)
            || settings.spriteAlignment != (int)SpriteAlignment.Center
            || CountFrames() != FrameCount;

        if (!changed)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritePixelsPerUnit = 100f;

        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);

        Slice(importer);

        importer.SaveAndReimport();
    }

    private static void ApplySingleImportSettings(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        bool changed =
            importer.textureType != TextureImporterType.Sprite
            || importer.spriteImportMode != SpriteImportMode.Single
            || importer.filterMode != FilterMode.Point
            || importer.textureCompression != TextureImporterCompression.Uncompressed
            || importer.mipmapEnabled
            || !importer.alphaIsTransparency
            || !Mathf.Approximately(importer.spritePixelsPerUnit, 100f)
            || settings.spriteAlignment != (int)SpriteAlignment.Center;

        if (!changed)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritePixelsPerUnit = 100f;

        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();
    }

    // Soldan saga 4 kare, tek satir -- FlameSpriteImporter.Slice'in aynisi,
    // tek sayfaya indirgenmis.
    private static void Slice(TextureImporter importer)
    {
        SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
        factories.Init();

        ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null)
        {
            Debug.LogError(
                "RotusSpriteImporter: Rotus.png icin sprite veri saglayicisi alinamadi. " +
                "com.unity.2d.sprite paketi kurulu mu?");
            return;
        }

        provider.InitSpriteEditorDataProvider();

        SpriteRect[] rects = new SpriteRect[FrameCount];
        List<SpriteNameFileIdPair> pairs = new List<SpriteNameFileIdPair>(FrameCount);

        for (int i = 0; i < FrameCount; i++)
        {
            string name = FrameName(i);
            GUID id = GUID.Generate();

            rects[i] = new SpriteRect
            {
                name = name,
                spriteID = id,
                rect = new Rect(i * FrameSize, 0f, FrameSize, FrameSize),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            };

            pairs.Add(new SpriteNameFileIdPair(name, id));
        }

        provider.SetSpriteRects(rects);

        ISpriteNameFileIdDataProvider names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (names != null)
        {
            names.SetNameFileIdPairs(pairs);
        }

        provider.Apply();
    }

    // Dilimleme gercekten tuttu mu? Eksikse 1, yerindeyse 0 doner --
    // PainterHabitsBuilder.Validate ile ayni fikir.
    public static int Validate()
    {
        int frames = CountFrames();
        if (frames == FrameCount)
        {
            return 0;
        }

        Debug.LogError(
            "RotusSpriteImporter: Rotus.png " + FrameCount + " kareye dilimlenemedi (" +
            frames + " bulundu). Rotus ikonu animasyonsuz kalacak. Dokuyu Sprite Editor'de " +
            "Grid by Cell Size ile elle dilimleyin.");

        return 1;
    }

    private static int CountFrames()
    {
        Object[] all = AssetDatabase.LoadAllAssetRepresentationsAtPath(SheetPath);
        if (all == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] is Sprite)
            {
                count++;
            }
        }

        return count;
    }
}
