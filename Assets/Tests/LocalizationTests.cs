using NUnit.Framework;
using UnityEngine;

// Yerellestirme altyapisinin EditMode testleri: SystemLanguageMap.Map, Loc.Tr
// dusme sirasi, AppLanguageNames.DisplayName kapsami. Sahne/MonoBehaviour
// gerektirmeyen saf siniflar (bkz. LOCALIZATION_PLAN.md Faz 0).
public class LocalizationTests
{
    [TearDown]
    public void TearDown()
    {
        // Baska testlerin varsayilan duzene guvenebilmesi icin aynayi geri
        // al -- SettingsLogicTests'teki AYNI gerekce (AppSettings statik).
        AppSettings.Apply(new AppSettingsData());
        Loc.Init(null);
    }

    // -------------------------------------------------------------------
    // SystemLanguageMap.Map
    // -------------------------------------------------------------------

    [TestCase(SystemLanguage.Turkish, AppLanguage.Turkish)]
    [TestCase(SystemLanguage.English, AppLanguage.English)]
    [TestCase(SystemLanguage.French, AppLanguage.French)]
    [TestCase(SystemLanguage.German, AppLanguage.German)]
    [TestCase(SystemLanguage.Italian, AppLanguage.Italian)]
    [TestCase(SystemLanguage.Portuguese, AppLanguage.PortugueseBR)]
    [TestCase(SystemLanguage.Russian, AppLanguage.Russian)]
    [TestCase(SystemLanguage.Spanish, AppLanguage.Spanish)]
    public void Map_SupportedSystemLanguage_ReturnsMatchingAppLanguage(
        SystemLanguage systemLanguage, AppLanguage expected)
    {
        Assert.AreEqual(expected, SystemLanguageMap.Map(systemLanguage));
    }

    [TestCase(SystemLanguage.Japanese)]
    [TestCase(SystemLanguage.Chinese)]
    [TestCase(SystemLanguage.Arabic)]
    [TestCase(SystemLanguage.Unknown)]
    public void Map_UnsupportedSystemLanguage_FallsBackToEnglish(SystemLanguage systemLanguage)
    {
        Assert.AreEqual(AppLanguage.English, SystemLanguageMap.Map(systemLanguage));
    }

    // -------------------------------------------------------------------
    // AppLanguageNames
    // -------------------------------------------------------------------

    [Test]
    public void Selectable_HasEightUniqueLanguagesExcludingUnset()
    {
        Assert.AreEqual(8, AppLanguageNames.Selectable.Length);

        for (int i = 0; i < AppLanguageNames.Selectable.Length; i++)
        {
            Assert.AreNotEqual(AppLanguage.Unset, AppLanguageNames.Selectable[i]);
        }
    }

    [Test]
    public void DisplayName_EveryLanguageHasNonEmptyDistinctName()
    {
        System.Collections.Generic.HashSet<string> seen =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);

        for (int i = 0; i < AppLanguageNames.Selectable.Length; i++)
        {
            string name = AppLanguageNames.DisplayName(AppLanguageNames.Selectable[i]);
            Assert.IsFalse(string.IsNullOrEmpty(name));
            Assert.IsTrue(seen.Add(name), "yinelenen ad: " + name);
        }
    }

    [Test]
    public void IndexOf_UnsetReturnsMinusOne()
    {
        Assert.AreEqual(-1, AppLanguageNames.IndexOf(AppLanguage.Unset));
    }

    // -------------------------------------------------------------------
    // Loc.Tr dusme sirasi: secili dil -> Turkce -> anahtarin kendisi
    // -------------------------------------------------------------------

    private static LocalizationTable BuildTable()
    {
        LocalizationTable table = ScriptableObject.CreateInstance<LocalizationTable>();

        LocalizationEntry entry = new LocalizationEntry { key = "greeting" };
        entry.values[AppLanguageNames.IndexOf(AppLanguage.Turkish)] = "Merhaba";
        entry.values[AppLanguageNames.IndexOf(AppLanguage.English)] = "Hello";
        // Almanca sutunu BOS birakildi -- dusme sirasini test etmek icin.

        table.ReplaceAll(new System.Collections.Generic.List<LocalizationEntry> { entry });
        return table;
    }

    [Test]
    public void Tr_SelectedLanguageHasValue_ReturnsThatValue()
    {
        Loc.Init(BuildTable());
        AppSettings.Apply(new AppSettingsData { language = AppLanguage.English });

        Assert.AreEqual("Hello", Loc.Tr("greeting"));
    }

    [Test]
    public void Tr_SelectedLanguageMissingValue_FallsBackToTurkish()
    {
        Loc.Init(BuildTable());
        AppSettings.Apply(new AppSettingsData { language = AppLanguage.German });

        Assert.AreEqual("Merhaba", Loc.Tr("greeting"));
    }

    [Test]
    public void Tr_UnknownKey_ReturnsKeyItself()
    {
        Loc.Init(BuildTable());
        AppSettings.Apply(new AppSettingsData { language = AppLanguage.English });

        Assert.AreEqual("no.such.key", Loc.Tr("no.such.key"));
    }

    [Test]
    public void Tr_NotInitialized_ReturnsKeyItself()
    {
        Loc.Init(null);

        Assert.AreEqual("greeting", Loc.Tr("greeting"));
    }

    // -------------------------------------------------------------------
    // AppSettings aynasi
    // -------------------------------------------------------------------

    [Test]
    public void AppSettings_Apply_MirrorsLanguage()
    {
        AppSettings.Apply(new AppSettingsData { language = AppLanguage.Russian });
        Assert.AreEqual(AppLanguage.Russian, AppSettings.Language);
    }

    [Test]
    public void AppSettings_Apply_NullData_FallsBackToUnset()
    {
        AppSettings.Apply(null);
        Assert.AreEqual(AppLanguage.Unset, AppSettings.Language);
    }

    // -------------------------------------------------------------------
    // Faz 5 -- veri kaynakli metinler (HabitCategories.DisplayName,
    // WeekDays.ShortLabel/Initial). BuildTable'in genel surumu: TEK Turkce
    // deger tasiyan anahtarlar, Loc.Tr'in "secili -> Turkce -> anahtar"
    // dusme sirasinin Turkce basamagini test eder (bkz. yukaridaki
    // Tr_SelectedLanguageMissingValue_FallsBackToTurkish).
    // -------------------------------------------------------------------

    private static LocalizationTable BuildTable(params (string key, string trValue)[] entries)
    {
        LocalizationTable table = ScriptableObject.CreateInstance<LocalizationTable>();
        System.Collections.Generic.List<LocalizationEntry> list = new System.Collections.Generic.List<LocalizationEntry>(entries.Length);
        int turkishSlot = AppLanguageNames.IndexOf(AppLanguage.Turkish);

        for (int i = 0; i < entries.Length; i++)
        {
            LocalizationEntry entry = new LocalizationEntry { key = entries[i].key };
            entry.values[turkishSlot] = entries[i].trValue;
            list.Add(entry);
        }

        table.ReplaceAll(list);
        return table;
    }

    [Test]
    public void HabitCategories_DisplayName_BuiltIn_ReturnsTranslatedValue()
    {
        Loc.Init(BuildTable(("category.health", "Sağlık"), ("category.mind", "Zihin")));

        Assert.AreEqual("Sağlık", HabitCategories.DisplayName(HabitCategories.Health));
        // Legacy (artik All icinde olmayan) kategori hala taniniyor.
        Assert.AreEqual("Zihin", HabitCategories.DisplayName(HabitCategories.Mind));
    }

    [Test]
    public void HabitCategories_DisplayName_CustomCategory_ReturnsUnchanged()
    {
        Loc.Init(BuildTable(("category.health", "Sağlık")));

        Assert.AreEqual("Bahçem", HabitCategories.DisplayName("Bahçem"));
    }

    [Test]
    public void WeekDays_ShortLabelAndInitial_ResolveThroughLoc()
    {
        Loc.Init(BuildTable(("weekday.0.short", "Pt"), ("weekday.0.initial", "P")));

        Assert.AreEqual("Pt", WeekDays.ShortLabel(0));
        Assert.AreEqual("P", WeekDays.Initial(0));
    }

    [Test]
    public void WeekDays_ShortLabel_UnknownIndex_FallsBackToKeyItself()
    {
        Loc.Init(BuildTable(("weekday.0.short", "Pt")));

        Assert.AreEqual("weekday.9.short", WeekDays.ShortLabel(9));
    }
}
