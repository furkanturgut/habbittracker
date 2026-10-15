using UnityEngine;

// Application.systemLanguage -> desteklenen AppLanguage. Yalnizca ILK
// KURULUMDA kullanilir (bkz. HabbitManager.ApplyLoadedSave/DeleteAllData) --
// kullanici Ayarlar'dan elle sectikten sonra bir daha hic cagrilmaz.
//
// Saf: UnityEngine.SystemLanguage disinda hicbir sahne/singleton bagimliligi
// yok, Tests/LocalizationTests Map'i dogrudan cagirir (Detect Application.
// systemLanguage okudugu icin test edilemez, bu yuzden sarmalayici olarak
// ayrildi).
public static class SystemLanguageMap
{
    public static AppLanguage Map(SystemLanguage systemLanguage)
    {
        switch (systemLanguage)
        {
            case SystemLanguage.Turkish: return AppLanguage.Turkish;
            case SystemLanguage.English: return AppLanguage.English;
            case SystemLanguage.French: return AppLanguage.French;
            case SystemLanguage.German: return AppLanguage.German;
            case SystemLanguage.Italian: return AppLanguage.Italian;
            case SystemLanguage.Portuguese: return AppLanguage.PortugueseBR;
            case SystemLanguage.Russian: return AppLanguage.Russian;
            case SystemLanguage.Spanish: return AppLanguage.Spanish;
            // Desteklenmeyen her sistem dili (Japonca, Cince, Arapca, vb.)
            // Ingilizce'ye duser -- plan bu secimi acikca Turkce'ye degil
            // Ingilizce'ye yapiyor (LOCALIZATION_PLAN.md).
            default: return AppLanguage.English;
        }
    }

    public static AppLanguage Detect()
    {
        return Map(Application.systemLanguage);
    }
}
