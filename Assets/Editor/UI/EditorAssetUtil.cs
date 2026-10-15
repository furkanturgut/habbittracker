using UnityEditor;

// Builder'larin paylastigi kucuk varlik yardimcilari.
public static class EditorAssetUtil
{
    // "Assets/Sprites" + "UI" -> yoksa olusturur. AssetDatabase.CreateFolder var
    // olan bir klasorde uyari basip bos GUID donduruyor, bu yuzden once soruluyor.
    public static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    // "Assets/A/B/C" yolunun tamamini olusturur.
    public static void EnsureFolderPath(string assetFolderPath)
    {
        string[] parts = assetFolderPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            EnsureFolder(current, parts[i]);
            current += "/" + parts[i];
        }
    }
}
