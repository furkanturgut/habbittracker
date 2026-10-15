using System.Collections.Generic;

// Ceviri okuma yuzeyi. Tabloyu Dictionary<string,string[]>'e BIR KEZ
// indeksler (O(1) lookup, hot-path kurallariyla uyumlu -- bkz. CLAUDE.md
// "Hot paths avoid allocation and LINQ"). AppSettings.Language'i CANLI okur:
// tablo yeniden Init edilmeden dil degisince Tr aninda farkli deger doner.
//
// Dusme sirasi: secili dil -> Turkce -> anahtarin kendisi. Son basamak
// BILEREK sessiz bos donmuyor -- kirik/eksik bir anahtar ekranda cikinti
// gibi gorunup hemen fark edilsin, sessizce kaybolmasin.
public static class Loc
{
    private static readonly Dictionary<string, string[]> table =
        new Dictionary<string, string[]>(256, System.StringComparer.Ordinal);

    private static bool initialized;

    private static readonly int TurkishSlot = AppLanguageNames.IndexOf(AppLanguage.Turkish);

    // Idempotent: hem oyun-ici HabbitManager.Awake'te hem Editor-build
    // komutlarinin basinda guvenle tekrar cagrilabilir (bkz.
    // LocalizationTableBuilder.Build, UiFactory.LocalizedText).
    public static void Init(LocalizationTable source)
    {
        table.Clear();
        initialized = source != null;

        if (source == null)
        {
            return;
        }

        IReadOnlyList<LocalizationEntry> entries = source.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            LocalizationEntry entry = entries[i];
            if (entry == null || string.IsNullOrEmpty(entry.key) || table.ContainsKey(entry.key))
            {
                continue;
            }

            table.Add(entry.key, entry.values);
        }
    }

    public static string Tr(string key)
    {
        if (!initialized || string.IsNullOrEmpty(key) || !table.TryGetValue(key, out string[] values))
        {
            return key;
        }

        int slot = AppLanguageNames.IndexOf(AppSettings.Language);
        if (slot >= 0 && slot < values.Length && !string.IsNullOrEmpty(values[slot]))
        {
            return values[slot];
        }

        if (TurkishSlot >= 0 && TurkishSlot < values.Length && !string.IsNullOrEmpty(values[TurkishSlot]))
        {
            return values[TurkishSlot];
        }

        return key;
    }
}
