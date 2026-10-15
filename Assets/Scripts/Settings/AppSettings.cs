using System;
using System.Collections.Generic;

// Ayar kaydinin salt-okunur aynasi. AppClock ile AYNI gerekce: yapraga inen
// bilesenler (DayChipRow gibi) HabbitManager'i gormeden -- sahne baglami
// olmadan -- ayarlari okuyabilsin. Yazan TEK yer HabbitManager; buradaki
// Apply disinda hicbir yerden yazilmaz.
//
// Varsayilanlar UYGULAMANIN BUGUNKU davranisiyla ayni (Monday, true, true,
// "Vincent"): ayna hic doldurulmasa (Apply hic cagrilmasa -- testte, ya da
// sahnesiz bir arayic baglamda) bile hicbir sey bozulmaz.
public static class AppSettings
{
    private static WeekStart weekStart = WeekStart.Monday;
    private static bool soundOn = true;
    private static bool hapticsOn = true;
    private static string painterName = AppSettingsData.DefaultPainterName;
    private static AppLanguage language = AppLanguage.Unset;
    private static readonly List<string> customCategories = new List<string>();
    private static readonly List<string> filterTokens = new List<string>(HabitFilters.DefaultTokens);

    public static WeekStart WeekStart => weekStart;
    public static bool SoundOn => soundOn;
    public static bool HapticsOn => hapticsOn;
    public static string PainterName => painterName;
    public static AppLanguage Language => language;

    // Ikisi de KOPYA liste dondurur -- cagrici kayit nesnesinin kendisini
    // degil, aynanin kendi tamponunu okuyor; disaridan degistirilemez.
    public static IReadOnlyList<string> CustomCategories => customCategories;
    public static IReadOnlyList<string> FilterTokens => filterTokens;

    // AppSettings'i dogrudan dinleyen sahnesiz bilesenler icin (DayChipRow,
    // CategoryChipRow, HabitFilterBar). HabbitManager.SettingsChanged ile
    // AYNI anda tetiklenir -- oradaki manager-abonesi ekranlar icin, burasi
    // sahne baglami olmayanlar icin.
    public static event Action Changed;

    public static void Apply(AppSettingsData data)
    {
        weekStart = data != null ? data.weekStart : WeekStart.Monday;
        soundOn = data == null || data.soundOn;
        hapticsOn = data == null || data.hapticsOn;
        painterName = data != null && !string.IsNullOrEmpty(data.painterName)
            ? data.painterName
            : AppSettingsData.DefaultPainterName;
        language = data != null ? data.language : AppLanguage.Unset;

        customCategories.Clear();
        if (data != null && data.customCategories != null)
        {
            customCategories.AddRange(data.customCategories);
        }

        filterTokens.Clear();
        if (data != null && data.filterTokens != null && data.filterTokens.Count > 0)
        {
            filterTokens.AddRange(data.filterTokens);
        }
        else
        {
            filterTokens.AddRange(HabitFilters.DefaultTokens);
        }

        Changed?.Invoke();
    }
}
