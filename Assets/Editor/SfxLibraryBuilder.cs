using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Assets/Sounds altindaki 6 sabit ses dosyasini SfxLibrary.asset'e baglar.
// PaintingLibraryBuilder'in "var olan gorselleri bagliyor, tekrar
// calistirilabilir" deseniyle ayni, ama tarama yok: PaintingLibrary'nin
// aksine liste acik uclu degil, kullanicinin belirledigi 6 sabit dosya adi --
// bu yuzden dogrudan yol-bazli yukleme yeterli.
public static class SfxLibraryBuilder
{
    private const string SoundsFolder = "Assets/Sounds";
    private const string LibraryPath = "Assets/Audio/SfxLibrary.asset";

    [MenuItem("HabitTracker/Build Sfx Library")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(SoundsFolder))
        {
            Debug.LogError("SfxLibraryBuilder: " + SoundsFolder + " klasoru yok.");
            return;
        }

        EditorAssetUtil.EnsureFolderPath("Assets/Audio");

        SfxLibrary library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<SfxLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        int missing = 0;
        library.dayCompleted = Load(SoundsFolder + "/guntamamlandi.wav", ref missing);
        library.rotusApplied = Load(SoundsFolder + "/rotusUygulandi.wav", ref missing);
        library.rotusEarned = Load(SoundsFolder + "/rotus eklendi.mp3", ref missing);
        library.newPainting = Load(SoundsFolder + "/yeni tablo.mp3", ref missing);
        library.slap = Load(SoundsFolder + "/slap.wav", ref missing);
        library.slapReaction = Load(SoundsFolder + "/slap2.wav", ref missing);

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int assigned = AssignToSceneManagers(library);

        Selection.activeObject = library;
        EditorGUIUtility.PingObject(library);

        Debug.Log(
            "SfxLibraryBuilder: " + (6 - missing) + "/6 ses baglandi. Kutuphane: " + LibraryPath + "\n" +
            (missing > 0
                ? missing + " dosya " + SoundsFolder + " altinda bulunamadi (yukaridaki hatalara bakin)."
                : "Hepsi tam.") + "\n" +
            (assigned > 0
                ? assigned + " HabbitManager'a otomatik atandi (sahneyi kaydetmeyi unutmayin)."
                : "Acik sahnede atanacak HabbitManager bulunamadi; alani elle atayin."),
            library);
    }

    private static AudioClip Load(string path, ref int missing)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null)
        {
            Debug.LogError("SfxLibraryBuilder: " + path + " bulunamadi ya da AudioClip olarak yuklenemedi.");
            missing++;
        }

        return clip;
    }

    // Acik sahnedeki HabbitManager'lara kutuphaneyi atar; elle surukleme
    // adimini atlamak icin. Sahneyi kirli isaretliyoruz ama KAYDETMIYORUZ,
    // kaydetmek kullanicinin karari (PaintingLibraryBuilder ile ayni kalip).
    private static int AssignToSceneManagers(SfxLibrary library)
    {
        HabbitManager[] managers = Object.FindObjectsByType<HabbitManager>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        int assigned = 0;
        for (int i = 0; i < managers.Length; i++)
        {
            SerializedObject serialized = new SerializedObject(managers[i]);
            SerializedProperty property = serialized.FindProperty("sfxLibrary");
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
}
