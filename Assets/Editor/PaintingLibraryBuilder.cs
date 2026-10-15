using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Assets/Sprites/Paintings altindaki gercek tablo PNG'lerini tarayip her biri
// icin bir PaintingDefinition ve hepsini toplayan bir PaintingLibrary uretir.
// Prosedurel placeholder ureten eski komutun yerini alir:
// bu yalnizca var olan gorselleri bagliyor.
//
// Komut tekrar tekrar calistirilabilir. Yeni PNG eklendiginde bir daha
// calistirmak yeterli; ONCEDEN URETILMIS tanimlarin Inspector'da elle
// degistirilen alanlari (parca sayisi, acilis sirasi, isim) KORUNUR. Aksi
// halde her calistirma elle yapilan ayari ezerdi -- revealOrder'i degistirmek
// kayitli ilerlemeyi gecersiz kildigi icin bu sessiz bir veri kaybi olurdu.
public static class PaintingLibraryBuilder
{
    private const string SourceFolder = "Assets/Sprites/Paintings";
    private const string RootFolder = "Assets/Paintings";
    private const string DefinitionFolder = RootFolder + "/Definitions";
    private const string LibraryPath = RootFolder + "/PaintingLibrary.asset";

    // Ilk kesifte uygulanan varsayilanlar. Dosya adina bagli cunku parca sayisi
    // gorselin detayina gore secildi: kalabalik tablolar 64 parca (8x8, kare
    // parcalar), sade olanlar 16 parca (8x2, genis bantlar).
    private struct Defaults
    {
        public string DisplayName;
        public PaintPixels.OverlayResolution Resolution;
        public PaintPixels.RevealOrder Order;
    }

    private static readonly Dictionary<string, Defaults> DefaultsByFile =
        new Dictionary<string, Defaults>(16, System.StringComparer.OrdinalIgnoreCase)
        {
            { "Painting (19)",  Make("Kedi Portresi", PaintPixels.OverlayResolution.Size32, PaintPixels.RevealOrder.Sequential) },
            { "Painting (20)",  Make("Sovalye",       PaintPixels.OverlayResolution.Size32, PaintPixels.RevealOrder.Sequential) },
            { "Painting (36)",  Make("Sonbahar Yolu", PaintPixels.OverlayResolution.Size64, PaintPixels.RevealOrder.Sequential) },
            { "Painting (48)",  Make("Naturmort",     PaintPixels.OverlayResolution.Size64, PaintPixels.RevealOrder.Sequential) },
            { "Painting (62)",  Make("Simsek",        PaintPixels.OverlayResolution.Size64, PaintPixels.RevealOrder.Sequential) },
            { "Painting (108)", Make("Ocak Basi",     PaintPixels.OverlayResolution.Size16, PaintPixels.RevealOrder.Sequential) },
            { "Painting (126)", Make("Somine",        PaintPixels.OverlayResolution.Size32, PaintPixels.RevealOrder.Sequential) },
            { "Painting (133)", Make("Yesil Salon",   PaintPixels.OverlayResolution.Size64, PaintPixels.RevealOrder.Sequential) },
            { "Painting (214)", Make("Tilsim",        PaintPixels.OverlayResolution.Size16, PaintPixels.RevealOrder.Sequential) },
            { "Painting (216)", Make("Oyun Kolu",     PaintPixels.OverlayResolution.Size16, PaintPixels.RevealOrder.Sequential) },
        };

    private static Defaults Make(
        string displayName,
        PaintPixels.OverlayResolution resolution,
        PaintPixels.RevealOrder order)
    {
        return new Defaults { DisplayName = displayName, Resolution = resolution, Order = order };
    }

    [MenuItem("HabitTracker/Build Painting Library From Sprites")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(SourceFolder))
        {
            Debug.LogError("PaintingLibraryBuilder: " + SourceFolder + " klasoru yok.");
            return;
        }

        EnsureFolder("Assets", "Paintings");
        EnsureFolder(RootFolder, "Definitions");

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SourceFolder });
        if (guids.Length == 0)
        {
            Debug.LogError("PaintingLibraryBuilder: " + SourceFolder + " altinda hic gorsel yok.");
            return;
        }

        // FindAssets sirasi garanti degil; kutuphanedeki sira calistirmadan
        // calistirmaya oynamasin diye yola gore siraliyoruz.
        System.Array.Sort(guids, (left, right) => string.CompareOrdinal(
            AssetDatabase.GUIDToAssetPath(left),
            AssetDatabase.GUIDToAssetPath(right)));

        List<PaintingDefinition> definitions = new List<PaintingDefinition>(guids.Length);
        int createdCount = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string spritePath = AssetDatabase.GUIDToAssetPath(guids[i]);
            PaintingDefinition definition = BuildOne(spritePath, i, out bool created);
            if (definition == null)
            {
                continue;
            }

            definitions.Add(definition);
            if (created)
            {
                createdCount++;
            }
        }

        PaintingLibrary library = WriteLibrary(definitions);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int assigned = AssignToSceneManagers(library);

        Selection.activeObject = library;
        EditorGUIUtility.PingObject(library);

        Debug.Log(
            "PaintingLibraryBuilder: " + definitions.Count + " tablo baglandi (" +
            createdCount + " yeni tanim). Kutuphane: " + LibraryPath + "\n" +
            (assigned > 0
                ? assigned + " HabbitManager'a otomatik atandi (sahneyi kaydetmeyi unutmayin)."
                : "Acik sahnede atanacak HabbitManager bulunamadi; alani elle atayin."),
            library);
    }

    // Tek bir gorseli tanima cevirir. created, tanimin bu calistirmada
    // yaratildigini soyler -- var olan tanimin ayarlarina dokunmuyoruz.
    private static PaintingDefinition BuildOne(string spritePath, int index, out bool created)
    {
        created = false;

        string fileName = System.IO.Path.GetFileNameWithoutExtension(spritePath);
        string id = MakeId(fileName);

        if (!ApplyImportSettings(spritePath))
        {
            return null;
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null)
        {
            Debug.LogError("PaintingLibraryBuilder: " + spritePath + " sprite olarak yuklenemedi.");
            return null;
        }

        string definitionPath = DefinitionFolder + "/" + id + ".asset";
        PaintingDefinition definition = AssetDatabase.LoadAssetAtPath<PaintingDefinition>(definitionPath);

        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<PaintingDefinition>();
            AssetDatabase.CreateAsset(definition, definitionPath);
            created = true;

            Defaults defaults = ResolveDefaults(fileName, index);
            definition.displayName = defaults.DisplayName;
            definition.resolution = defaults.Resolution;
            definition.revealOrder = defaults.Order;
            definition.revealMode = PaintPixels.RevealMode.Fade;
        }

        // id ve sprite her zaman tazeleniyor: id kayit formatinin parcasi ama
        // dosya adindan turedigi icin sabit kaliyor; sprite referansi ise
        // gorsel tasinirsa kopabilir.
        definition.id = id;
        definition.sprite = sprite;

        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static Defaults ResolveDefaults(string fileName, int index)
    {
        if (DefaultsByFile.TryGetValue(fileName, out Defaults known))
        {
            return known;
        }

        // Tabloda olmayan yeni gorsel: uc kovaya sirayla dagitiliyor ki
        // PickNext her canvasSize icin bir sey bulabilsin. Acilis sirasi her
        // zaman Sequential: tablolar ilk parcadan baslayip sirayla ilerlemeli,
        // Random bilerek varsayilan olarak kullanilmiyor.
        PaintPixels.OverlayResolution[] cycle =
        {
            PaintPixels.OverlayResolution.Size16,
            PaintPixels.OverlayResolution.Size32,
            PaintPixels.OverlayResolution.Size64
        };

        return new Defaults
        {
            DisplayName = fileName,
            Resolution = cycle[index % cycle.Length],
            Order = PaintPixels.RevealOrder.Sequential
        };
    }

    // Piksel sanati import ayarlari. Point filtre olmadan tablolar
    // RenderTexture'a olceklenirken bulaniklasir; sikistirma ise bu kadar
    // kucuk gorsellerde gozle gorulur blok artefakti birakir.
    private static bool ApplyImportSettings(string spritePath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("PaintingLibraryBuilder: " + spritePath + " icin TextureImporter yok.");
            return false;
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(spritePath);
        // pixelsPerUnit = uzun kenar: tablolar farkli en-boy oraninda olsa da
        // hepsi 1 birimlik kutuya sigiyor. PaintPixels zaten kameraya gore
        // yeniden olcekliyor ama ayni tabandan baslamalari sahnede oynarken
        // isleri ongorulebilir kiliyor.
        float pixelsPerUnit = texture != null ? Mathf.Max(texture.width, texture.height) : 32f;

        bool changed =
            importer.textureType != TextureImporterType.Sprite
            || importer.spriteImportMode != SpriteImportMode.Single
            || importer.filterMode != FilterMode.Point
            || importer.textureCompression != TextureImporterCompression.Uncompressed
            || importer.mipmapEnabled
            || !importer.alphaIsTransparency
            || !importer.isReadable
            || !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit);

        if (!changed)
        {
            return true;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        // isReadable: BurnedPaintingArt calisma aninda tablonun piksellerini
        // okuyup yakilan parcalari soldurarak yeni bir kopya pisiriyor. Bu bayrak
        // olmadan GetPixels32 patlar; o durumda yakilan parca sessizce eski duz
        // griye duser (bkz. BurnedPaintingArt.Build). CPU'da tutulan kopya bu
        // boyutta gorsellerde ~8 KB.
        importer.isReadable = true;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.SaveAndReimport();
        return true;
    }

    private static PaintingLibrary WriteLibrary(List<PaintingDefinition> definitions)
    {
        PaintingLibrary library = AssetDatabase.LoadAssetAtPath<PaintingLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<PaintingLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        // paintings alani private [SerializeField]; alani public yapmak yerine
        // SerializedObject uzerinden yaziyoruz.
        SerializedObject serialized = new SerializedObject(library);
        SerializedProperty list = serialized.FindProperty("paintings");
        list.ClearArray();
        for (int i = 0; i < definitions.Count; i++)
        {
            list.InsertArrayElementAtIndex(i);
            list.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return library;
    }

    // Acik sahnedeki HabbitManager'lara kutuphaneyi atar; elle surukleme
    // adimini atlamak icin. Sahneyi kirli isaretliyoruz ama KAYDETMIYORUZ,
    // kaydetmek kullanicinin karari.
    private static int AssignToSceneManagers(PaintingLibrary library)
    {
        HabbitManager[] managers = Object.FindObjectsByType<HabbitManager>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        int assigned = 0;
        for (int i = 0; i < managers.Length; i++)
        {
            SerializedObject serialized = new SerializedObject(managers[i]);
            SerializedProperty property = serialized.FindProperty("paintingLibrary");
            if (property == null || property.objectReferenceValue == library)
            {
                continue;
            }

            property.objectReferenceValue = library;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(managers[i].gameObject.scene);
            assigned++;
        }

        return assigned;
    }

    // "Painting (19)" -> "painting_19". id kayit formatinin parcasi: ayni dosya
    // adi her zaman ayni id'yi vermeli, yoksa galeriler sahipsiz kalir.
    private static string MakeId(string fileName)
    {
        StringBuilder builder = new StringBuilder(fileName.Length);
        bool lastWasSeparator = false;

        for (int i = 0; i < fileName.Length; i++)
        {
            char c = char.ToLowerInvariant(fileName[i]);
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
            {
                builder.Append(c);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator && builder.Length > 0)
            {
                builder.Append('_');
                lastWasSeparator = true;
            }
        }

        while (builder.Length > 0 && builder[builder.Length - 1] == '_')
        {
            builder.Length--;
        }

        return builder.Length > 0 ? builder.ToString() : "painting";
    }

    private static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
