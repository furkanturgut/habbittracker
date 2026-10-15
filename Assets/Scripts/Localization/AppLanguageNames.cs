// AppLanguage <-> ekranda gorunen ANA DIL adi. WeekDays/MoodScale ile AYNI
// "tek donusturucu" sekli: yeni bir dil eklendiginde tek bu dosyaya dokunulur.
//
// Adlar kullanicinin kendi dilinde yazilir (Ingilizce'de "German" degil,
// Almanca'nin kendi alfabesiyle "Deutsch") -- secim listesinde kullanici
// kendi dilini ararken Turkce/Ingilizce karsiligini degil kendi yazimini
// tanimali.
public static class AppLanguageNames
{
    // LocalizationEntry.values dizisinin sirasi da BUDUR (Loc.cs, Strings.tsv
    // sutun sirasi) -- Unset burada YOK, hicbir ceviri sutununa karsilik
    // gelmiyor.
    public static readonly AppLanguage[] Selectable =
    {
        AppLanguage.Turkish,
        AppLanguage.English,
        AppLanguage.French,
        AppLanguage.German,
        AppLanguage.Italian,
        AppLanguage.PortugueseBR,
        AppLanguage.Russian,
        AppLanguage.Spanish,
    };

    public static string DisplayName(AppLanguage language)
    {
        switch (language)
        {
            case AppLanguage.Turkish: return "Türkçe";
            case AppLanguage.English: return "English";
            case AppLanguage.French: return "Français";
            case AppLanguage.German: return "Deutsch";
            case AppLanguage.Italian: return "Italiano";
            case AppLanguage.PortugueseBR: return "Português (Brasil)";
            case AppLanguage.Russian: return "Русский";
            case AppLanguage.Spanish: return "Español";
            default: return "Türkçe";
        }
    }

    // values[] dizisindeki konum; bulunamazsa -1 (Unset ya da gecersiz deger).
    // Tahsissiz dogrusal arama: dizi sekiz elemanli, MoodScale.IndexOf ile
    // ayni gerekce.
    public static int IndexOf(AppLanguage language)
    {
        for (int i = 0; i < Selectable.Length; i++)
        {
            if (Selectable[i] == language)
            {
                return i;
            }
        }

        return -1;
    }
}
