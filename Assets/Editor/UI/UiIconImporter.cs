using System.IO;
using UnityEditor;
using UnityEngine;

// Alt navigasyon + ayarlar teslim klasorundeki ikonlari projeye alir:
// tab_atolye.png, tab_aliskanliklar.png, tab_galeri.png, settings_gear.png.
// StudioSpriteImporter'in birebir kalibi -- ayni gerekce, farkli klasor/ayarlar.
//
// PNG'ler 96x96 beyaz; renk Image.color ile veriliyor, bu yuzden pivot Center
// ve pixelsPerUnit 100 (StudioSpriteImporter'in 1 px = 1 world unit kuralinin
// aksine, bunlar uGUI Image icinde dogrudan boyutlandiriliyor -- Center/100
// tek rakamla duzgun olceklenmesi icin yeter, ozel bir dunya-birimi anlami yok).
public static class UiIconImporter
{
    public const string TargetFolder = "Assets/Sprites/UI/Icons";

    // Proje kokunun yanindaki teslim klasoru. Assets disinda oldugu icin Unity
    // onu gormuyor; dosyalari elle kopyaliyoruz.
    private const string HandoffFolder =
        "Painter Habits Mobile App Navigasyon+Ayarlar/design_handoff_navigation_settings/icons";

    private static readonly string[] Files =
    {
        "tab_atolye.png",
        "tab_aliskanliklar.png",
        "tab_galeri.png",
        "settings_gear.png",
    };

    public static string TabAtolyePath => TargetFolder + "/tab_atolye.png";
    public static string TabHabitsPath => TargetFolder + "/tab_aliskanliklar.png";
    public static string TabGalleryPath => TargetFolder + "/tab_galeri.png";
    public static string SettingsGearPath => TargetFolder + "/settings_gear.png";

    // Kopyalanan dosya sayisini doner. Hedefte zaten varsa DOKUNULMAZ.
    public static int Import()
    {
        EditorAssetUtil.EnsureFolderPath(TargetFolder);

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
                        "UiIconImporter: " + Files[i] + " teslim klasorunde bulunamadi (" + source + "). " +
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
}
