using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Assets/Localization/Strings.tsv (anahtar + AppLanguageNames.Selectable
// sirasinda 8 dil sutunu, tab-ayracli) -> LocalizationTable.asset.
// PaintingLibraryBuilder'in "kaynak dosya -> Editor build adimi -> SO asset"
// ritminin aynisi. TSV elle/Excel'de duzenlenebilir ve versiyon kontrollu --
// cevirmen/kullanici kodu acmadan metinleri duzenleyebilsin diye.
//
// "Build UI Assets" akisina eklendi (bkz. PainterHabitsBuilder.BuildAssets/
// BuildAll). Loc.Init'i de kendi icinde cagirir: ayni calistirmada sonraki
// adimlar (UiFactory.LocalizedText) Loc.Tr'i hemen kullanabilsin.
public static class LocalizationTableBuilder
{
    public const string AssetFolder = "Assets/UI";
    public const string AssetPath = AssetFolder + "/LocalizationTable.asset";
    public const string SourcePath = "Assets/Localization/Strings.tsv";

    public static LocalizationTable Build()
    {
        EditorAssetUtil.EnsureFolder("Assets", "UI");

        LocalizationTable table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(AssetPath);
        if (table == null)
        {
            table = ScriptableObject.CreateInstance<LocalizationTable>();
            AssetDatabase.CreateAsset(table, AssetPath);
        }

        List<LocalizationEntry> entries = ReadSource();
        table.ReplaceAll(entries);
        EditorUtility.SetDirty(table);

        Loc.Init(table);

        Debug.Log(
            "LocalizationTableBuilder: " + entries.Count + " anahtar yuklendi -> " + AssetPath,
            table);

        return table;
    }

    // Basligin ilk sutunu "key", sonraki AppLanguageNames.Selectable.Length
    // sutun O DIZININ sirasiyla AYNI (Turkish..Spanish) olmali.
    private static List<LocalizationEntry> ReadSource()
    {
        List<LocalizationEntry> entries = new List<LocalizationEntry>();

        if (!File.Exists(SourcePath))
        {
            // Faz 1 tamamlanana kadar (ceviri verisi henuz yazilmadiysa)
            // beklenen durum -- bos tablo, Loc.Tr her anahtari oldugu gibi
            // dondurur, uygulama Turkce metinlerle calismaya devam eder
            // (butun UI'in kaynagi hala literal string'lerse).
            Debug.LogWarning(
                "LocalizationTableBuilder: " + SourcePath + " yok, bos tablo uretildi.");
            return entries;
        }

        string[] lines = File.ReadAllLines(SourcePath);
        int expectedColumns = AppLanguageNames.Selectable.Length + 1;
        HashSet<string> seenKeys = new HashSet<string>(System.StringComparer.Ordinal);

        for (int i = 1; i < lines.Length; i++) // 0. satir baslik
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] columns = line.Split('\t');
            if (columns.Length < expectedColumns)
            {
                Debug.LogError(
                    "LocalizationTableBuilder: " + SourcePath + " satir " + (i + 1) +
                    " icin " + expectedColumns + " sutun bekleniyordu, " + columns.Length +
                    " bulundu. Atlaniyor.");
                continue;
            }

            string key = columns[0].Trim();
            if (key.Length == 0)
            {
                continue;
            }

            if (!seenKeys.Add(key))
            {
                Debug.LogError(
                    "LocalizationTableBuilder: '" + key + "' anahtari " + SourcePath +
                    " icinde birden fazla kez tanimli, ilk tekrar atlaniyor.");
                continue;
            }

            LocalizationEntry entry = new LocalizationEntry();
            entry.key = key;
            entry.values = new string[AppLanguageNames.Selectable.Length];
            for (int c = 0; c < entry.values.Length; c++)
            {
                entry.values[c] = columns[c + 1];
            }

            entries.Add(entry);
        }

        return entries;
    }
}
