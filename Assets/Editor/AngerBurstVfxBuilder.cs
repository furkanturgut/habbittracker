using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Assets/VFX/env_emote_anger.png (elle Multiple modda onceden dilimlenmis)
// icinden IKI temiz kareyi secip AngerBurstVfx prefabini kurar.
//
// Sayfanin tamamini bir animasyon seridi gibi sirayla oynatmak YANLISTI:
// bu bir flipbook degil, siki paketlenmis (tightly trimmed) bir atlas --
// her alt-sprite KENDI sinirlarina gore ortalaniyor. Izole pirilti kareleri
// (env_emote_anger_2/4 -- piriltinin TEK BASINA, kendi kucuk kutusuna
// kirpilmis hali) bu yuzden halkanin merkezinden FARKLI bir noktaya
// atlardi ("gidip geliyormus gibi" glitch). Ama env_emote_anger_0 (sade
// halka) ile env_emote_anger_1 (halka + IKI pirilti, TEK gorselde birlikte,
// simetrik) .meta'daki rect'lere gore AYNI dikey merkeze sahip -- bu ikisi
// arasinda gecis yapmak guvenli, hatta PNG'de goruntulenen piriltiyi geri
// katiyor. AngerBurstVfx pop anindaki "parlama" icin peakSprite'i (frame 1),
// settle/hold/fade icin baseSprite'i (frame 0) kullaniyor.
public static class AngerBurstVfxBuilder
{
    private const string SheetPath = "Assets/VFX/env_emote_anger.png";
    private const string Folder = "Assets/Prefabs/Vfx";

    // Public: WorkshopSceneBuilder her kurulumda ayni prefabi Painter'a geri
    // bagliyor (bkz. LoadPrefab).
    public const string PrefabPath = Folder + "/AngerBurstVfx.prefab";
    private const string BaseFrameName = "env_emote_anger_0";
    private const string PeakFrameName = "env_emote_anger_1";

    // Sprite'lar 100 PPU ile import edildi (spritePixelsToUnits); Painter'in
    // StudioPixelSprite ile kullandigi "zoom * sourcePixelsPerUnit" ayni
    // mantik -- localScale = 100 native pikseli birebir "tasarim pikseli"
    // olarak cizer. Zoom < 1: "cok buyuk olmasin" kisitlamasi.
    private const float SpritePixelsPerUnit = 100f;
    private const float Zoom = 0.6f;

    // Painter'in SpriteRenderer'i 20'de ciziliyor (bkz. WorkshopSceneBuilder.
    // ApplyPainterPlacement); patlama onun ONUNDE kalsin diye bir fazlasi.
    private const int SortingOrder = 21;

    [MenuItem("HabitTracker/Build Anger Burst Vfx")]
    public static void Build()
    {
        Sprite baseSprite = LoadSprite(BaseFrameName);
        Sprite peakSprite = LoadSprite(PeakFrameName);
        if (baseSprite == null || peakSprite == null)
        {
            Debug.LogError(
                "AngerBurstVfxBuilder: " + SheetPath + " icinde " + BaseFrameName + " / " + PeakFrameName +
                " adinda dilimlenmis kareler bulunamadi. Sayfa Sprite Editor'de Multiple modda " +
                "dilimlenmis mi?");
            return;
        }

        EditorAssetUtil.EnsureFolderPath(Folder);

        GameObject instance = new GameObject("AngerBurstVfx");
        instance.transform.localScale = Vector3.one * (SpritePixelsPerUnit * Zoom);

        SpriteRenderer renderer = instance.AddComponent<SpriteRenderer>();
        renderer.sprite = baseSprite;
        renderer.sortingOrder = SortingOrder;

        AngerBurstVfx vfx = instance.AddComponent<AngerBurstVfx>();
        SerializedObject serialized = new SerializedObject(vfx);
        serialized.FindProperty("spriteRenderer").objectReferenceValue = renderer;
        serialized.FindProperty("baseSprite").objectReferenceValue = baseSprite;
        serialized.FindProperty("peakSprite").objectReferenceValue = peakSprite;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject asset = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        UnityEngine.Object.DestroyImmediate(instance);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int assigned = AssignToScenePainters(asset.GetComponent<AngerBurstVfx>());

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);

        Debug.Log(
            "AngerBurstVfxBuilder: prefab guncellendi (" + BaseFrameName + " + " + PeakFrameName +
            "). Prefab: " + PrefabPath + "\n" +
            (assigned > 0
                ? assigned + " Painter'a otomatik atandi (sahneyi kaydetmeyi unutmayin)."
                : "Acik sahnede atanacak Painter bulunamadi; alani elle atayin."),
            asset);
    }

    // Kurulmus prefabi okur (yoksa null). Sahne kurucusu bunu kullaniyor:
    // prefabi yeniden URETMEK sahne kurulumunun isi degil, var olani
    // BAGLAMAK ise -- cunku bos kalan bir referans hicbir yerde uyari
    // uretmeden efekti tamamen yok ediyor.
    public static AngerBurstVfx LoadPrefab()
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        return asset != null ? asset.GetComponent<AngerBurstVfx>() : null;
    }

    private static Sprite LoadSprite(string frameName)
    {
        UnityEngine.Object[] representations = AssetDatabase.LoadAllAssetRepresentationsAtPath(SheetPath);
        if (representations == null)
        {
            return null;
        }

        for (int i = 0; i < representations.Length; i++)
        {
            if (representations[i] is Sprite sprite && sprite.name == frameName)
            {
                return sprite;
            }
        }

        return null;
    }

    // Acik sahnedeki Painter'lara prefabi atar -- PaintingLibraryBuilder'in
    // AssignToSceneManagers'iyla ayni kalip.
    private static int AssignToScenePainters(AngerBurstVfx prefab)
    {
        Painter[] painters = UnityEngine.Object.FindObjectsByType<Painter>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        int assigned = 0;
        for (int i = 0; i < painters.Length; i++)
        {
            SerializedObject serialized = new SerializedObject(painters[i]);
            SerializedProperty property = serialized.FindProperty("angerVfxPrefab");
            if (property == null || property.objectReferenceValue == prefab)
            {
                continue;
            }

            property.objectReferenceValue = prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(painters[i].gameObject.scene);
            assigned++;
        }

        return assigned;
    }
}
