using System.IO;
using UnityEditor;
using UnityEngine;

// Atolye sahnesinin iki yeni gorselini tasarim teslim klasorunden projeye alir:
// studio_bg.png (92x114) ve easel_painting.png (24x26).
//
// Teslim klasorundeki RESSAM sprite'lari ve tablo kirpiklari BILEREK alinmiyor:
// projenin kendi Assets/Sprites/PainterSprites ve Assets/Sprites/Paintings
// varliklari gecerli.
//
// pixelsPerUnit = 1 ve pivot = sol alt: boylece sahnedeki localScale dogrudan
// tasarimin buyutme carpani oluyor (studio_bg 4x, easel 4x) ve StudioPixelSprite
// tasarim pikseliyle konumlandirma yapabiliyor.
public static class StudioSpriteImporter
{
    public const string TargetFolder = "Assets/Sprites/Studio";

    // Proje kokunun yanindaki teslim klasoru. Assets disinda oldugu icin Unity
    // onu gormuyor; dosyalari elle kopyaliyoruz.
    private const string HandoffFolder =
        "Painter Habits Mobile App/design_handoff_painter_habits/sprites";

    private static readonly string[] Files = { "studio_bg.png", "easel_painting.png" };

    public static string BackgroundPath => TargetFolder + "/studio_bg.png";
    public static string EaselPath => TargetFolder + "/easel_painting.png";

    // Kopyalanan dosya sayisini doner. Hedefte zaten varsa DOKUNULMAZ: gorsel
    // elle retuslanmis olabilir ve builder onu ezmemeli.
    public static int Import()
    {
        EditorAssetUtil.EnsureFolder("Assets/Sprites", "Studio");

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string source = Path.Combine(projectRoot, HandoffFolder.Replace('/', Path.DirectorySeparatorChar));

        int copied = 0;

        for (int i = 0; i < Files.Length; i++)
        {
            string target = TargetFolder + "/" + Files[i];

            if (!File.Exists(target))
            {
                string from = Path.Combine(source, Files[i]);
                if (!File.Exists(from))
                {
                    Debug.LogError(
                        "StudioSpriteImporter: " + Files[i] + " teslim klasorunde bulunamadi (" + source + "). " +
                        "Dosyayi elle " + TargetFolder + " altina koyun.");
                    continue;
                }

                File.Copy(from, target);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
                copied++;
            }

            ApplyImportSettings(target);
        }

        return copied;
    }

    // Import ayarlari dosya kopyalanmasa da her calistirmada dogrulaniyor.
    private static void ApplyImportSettings(string path)
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
            || !Mathf.Approximately(importer.spritePixelsPerUnit, 1f)
            || settings.spriteAlignment != (int)SpriteAlignment.BottomLeft;

        if (!changed)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        // Piksel sanati: bilinear filtre 4x buyutmede gorseli bulaniklastirirdi.
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        // 1 piksel = 1 dunya birimi; olcekleme StudioPixelSprite'in isi.
        importer.spritePixelsPerUnit = 1f;

        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomLeft;
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();
    }
}
