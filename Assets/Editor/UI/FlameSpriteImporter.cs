using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Seri alevi teslim klasorundeki sprite sayfalarini projeye alir.
//
// UiIconImporter'in birebir kalibi (kopyala, hedefte varsa DOKUNMA, import
// ayarlarini her calistirmada dogrula) -- TEK fark dilimleme: her PNG tek bir
// sprite degil, yan yana 4 KARELIK bir animasyon sayfasi. Bu yuzden
// spriteImportMode Multiple ve kareler "Grid by Cell Size" ile aciliyor:
//   FlameIcon_*  64x16  -> 4 x (16x16)   tile cipi + galeri karosu
//   Flame_*     128x32  -> 4 x (32x32)   kutlama ekrani
//
// Kare adlari "<dosya>_0".."<dosya>_3": UiThemeBuilder temayi bu ada gore
// dolduruyor, yani adlandirma kaydin degil TEMANIN bir parcasi -- bir kareyi
// yeniden adlandirmak yalnizca bir sonraki tema yapimini bozar, kullanici
// verisini degil (PaintingDefinition.id'nin aksine).
public static class FlameSpriteImporter
{
    public const string TargetFolder = "Assets/Sprites/UI/Flame";

    // Proje kokunun yanindaki teslim klasoru. Assets disinda oldugu icin Unity
    // onu gormuyor; dosyalari elle kopyaliyoruz.
    private const string HandoffFolder =
        "Painter Habits Mobile App seri takibi/design_handoff_streak_flame/sprites/flame";

    // Bir sayfadaki kare sayisi. Dilimlemenin de dogrulamanin da tek kaynagi.
    public const int FrameCount = 4;

    public const int IconFrameSize = 16;
    public const int LargeFrameSize = 32;

    private const string IconPrefix = "FlameIcon_T";
    private const string LargePrefix = "Flame_T";
    private const string PendingSuffix = "_Pending";

    // T0'in "_Pending" varyanti YOK: seri sifirsa alev zaten sonuk, bekleyen
    // diye bir hali olamaz (bkz. StreakFlame.IsPending -- currentStreak > 0).
    private const int FirstPendingTier = 1;

    public static string IconName(int tier, bool pending)
    {
        return IconPrefix + tier + (pending ? PendingSuffix : string.Empty);
    }

    public static string LargeName(int tier, bool pending)
    {
        return LargePrefix + tier + (pending ? PendingSuffix : string.Empty);
    }

    public static string PathFor(string spriteName)
    {
        return TargetFolder + "/" + spriteName + ".png";
    }

    // Tek bir karenin alt-sprite adi. UiThemeBuilder bu adla ariyor.
    public static string FrameName(string spriteName, int frame)
    {
        return spriteName + "_" + frame;
    }

    // Kopyalanan dosya sayisini doner. Hedefte zaten varsa DOKUNULMAZ; import
    // ayarlari (ve dilimleme) yine de her calistirmada dogrulanir.
    public static int Import()
    {
        EditorAssetUtil.EnsureFolderPath(TargetFolder);

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string source = Path.Combine(projectRoot, HandoffFolder.Replace('/', Path.DirectorySeparatorChar));

        int copied = 0;

        for (int tier = 0; tier < StreakFlame.TierCount; tier++)
        {
            copied += ImportOne(source, IconName(tier, false), IconFrameSize);
            copied += ImportOne(source, LargeName(tier, false), LargeFrameSize);

            if (tier >= FirstPendingTier)
            {
                copied += ImportOne(source, IconName(tier, true), IconFrameSize);
                copied += ImportOne(source, LargeName(tier, true), LargeFrameSize);
            }
        }

        return copied;
    }

    private static int ImportOne(string sourceFolder, string spriteName, int frameSize)
    {
        string target = PathFor(spriteName);
        string file = spriteName + ".png";
        int copied = 0;

        if (!File.Exists(target))
        {
            string from = Path.Combine(sourceFolder, file);
            if (!File.Exists(from))
            {
                Debug.LogError(
                    "FlameSpriteImporter: " + file + " teslim klasorunde bulunamadi (" + sourceFolder + "). " +
                    "Dosyayi elle " + TargetFolder + " altina koyun.");
                return 0;
            }

            File.Copy(from, target);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
            copied = 1;
        }

        ApplyImportSettings(target, spriteName, frameSize);
        return copied;
    }

    // Import ayarlari + dilimleme. UiIconImporter'daki "degisti mi" kontrolunun
    // aynisi: hicbir sey degismediyse SaveAndReimport cagrilmiyor, yoksa her
    // komut calistirmasi 34 dokuyu bosuna yeniden import ederdi.
    //
    // Dilimleme ISpriteEditorDataProvider ile yapiliyor, eski
    // TextureImporter.spritesheet ile DEGIL: Unity 6'da o ozelligin destegi
    // KALDIRILDI (derleyici CS0618 ile "support ... has been removed" diyor),
    // yani yazmak sessizce etkisiz kalirdi ve 34 doku tek kare olarak gelirdi.
    private static void ApplyImportSettings(string path, string spriteName, int frameSize)
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
            || CountFrames(spriteName) != FrameCount;

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

        Slice(importer, spriteName, frameSize);

        importer.SaveAndReimport();
    }

    // Soldan saga 4 kare. Texture2D'nin y ekseni asagidan yukari oldugu icin
    // tek satirlik bir sayfada y her zaman 0 -- PaintPixels.TextureRowForPiece'in
    // satir cevirme ihtiyaci burada yok, cunku SATIR tek.
    private static void Slice(TextureImporter importer, string spriteName, int frameSize)
    {
        SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
        factories.Init();

        ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null)
        {
            Debug.LogError(
                "FlameSpriteImporter: " + spriteName + " icin sprite veri saglayicisi alinamadi. " +
                "com.unity.2d.sprite paketi kurulu mu?");
            return;
        }

        provider.InitSpriteEditorDataProvider();

        SpriteRect[] rects = new SpriteRect[FrameCount];
        List<SpriteNameFileIdPair> pairs = new List<SpriteNameFileIdPair>(FrameCount);

        for (int i = 0; i < FrameCount; i++)
        {
            string name = FrameName(spriteName, i);
            GUID id = GUID.Generate();

            rects[i] = new SpriteRect
            {
                name = name,
                spriteID = id,
                rect = new Rect(i * frameSize, 0f, frameSize, frameSize),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            };

            pairs.Add(new SpriteNameFileIdPair(name, id));
        }

        provider.SetSpriteRects(rects);

        // Ad <-> kimlik eslemesi 2021.2'den beri AYRI bir saglayicida. Yazilmazsa
        // kareler olusur ama her yeniden import'ta yeni kimlik alir: temaya
        // bagladigimiz 4 karenin referansi bir sonraki calistirmada kopardi.
        ISpriteNameFileIdDataProvider names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (names != null)
        {
            names.SetNameFileIdPairs(pairs);
        }

        provider.Apply();
    }

    // Dilimleme gercekten tuttu mu? "spritesheet" yazmak sessizce etkisiz
    // kalabilecek bir API; sonucu saymak, temanin yarim dolmasini sessiz bir
    // hata olmaktan cikariyor (PainterHabitsBuilder.Validate ile ayni fikir).
    // Eksik sayfalarin sayisini doner; 0 ise her sey yerinde.
    public static int Validate()
    {
        int broken = 0;

        for (int tier = 0; tier < StreakFlame.TierCount; tier++)
        {
            broken += ValidateOne(IconName(tier, false));
            broken += ValidateOne(LargeName(tier, false));

            if (tier >= FirstPendingTier)
            {
                broken += ValidateOne(IconName(tier, true));
                broken += ValidateOne(LargeName(tier, true));
            }
        }

        return broken;
    }

    private static int ValidateOne(string spriteName)
    {
        int frames = CountFrames(spriteName);

        if (frames == FrameCount)
        {
            return 0;
        }

        Debug.LogError(
            "FlameSpriteImporter: " + spriteName + " " + FrameCount + " kareye dilimlenemedi (" +
            frames + " bulundu). Alev bu basamakta cizilmeyecek. Dokuyu Sprite Editor'de " +
            "Grid by Cell Size ile elle dilimleyin.");

        return 1;
    }

    private static int CountFrames(string spriteName)
    {
        Object[] all = AssetDatabase.LoadAllAssetRepresentationsAtPath(PathFor(spriteName));
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
