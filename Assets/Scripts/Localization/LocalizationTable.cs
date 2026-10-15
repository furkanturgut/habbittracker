using System;
using System.Collections.Generic;
using UnityEngine;

// Ceviri verisinin SO karsiligi. Icerik Assets/Localization/Strings.tsv'den
// "Build Localization Table" (Editor/LocalizationTableBuilder.cs) ile
// uretilir -- PaintingLibrary'nin kaynak PNG'lerden uretilmesiyle AYNI ritim
// (kaynak dosya -> Editor build adimi -> SO asset). Elle duzenlenmez; kaynak
// TSV duzenlenip builder yeniden calistirilir.
[CreateAssetMenu(fileName = "LocalizationTable", menuName = "HabitTracker/Localization Table")]
public class LocalizationTable : ScriptableObject
{
    [SerializeField] private List<LocalizationEntry> entries = new List<LocalizationEntry>();

    public IReadOnlyList<LocalizationEntry> Entries => entries;

    // Builder'in tek yazma yolu: tabloyu TAMAMEN degistirir. Kismi guncelleme
    // yok -- TSV her zaman tum anahtarlarin dogru kaynagi.
    public void ReplaceAll(List<LocalizationEntry> newEntries)
    {
        entries.Clear();
        if (newEntries != null)
        {
            entries.AddRange(newEntries);
        }
    }
}

// Bir anahtarin AppLanguageNames.Selectable sirasindaki (Turkish..Spanish,
// Unset HARIC) karsiliklari. values.Length AppLanguageNames.Selectable.Length
// ile AYNI olmali -- Loc.Tr sinir disi okumaya karsi kendi kontrolunu yapar.
[Serializable]
public class LocalizationEntry
{
    public string key;
    public string[] values = new string[AppLanguageNames.Selectable.Length];
}
