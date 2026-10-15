using System.Collections.Generic;

// Kategori adlarinin tek kaynagi.
//
// Kayitta HabitData.categoryGroup serbest bir string; bu sinif yalnizca formun
// sundugu SECENEKLERI ve listenin siralamasini tanimliyor. Kayitta duran bir
// degeri asla yeniden yazmiyoruz: eski ya da elle girilmis bir kategori adi
// listede kendi basligiyla gorunmeye devam etmeli.
public static class HabitCategories
{
    public const string Health = "Sağlık";
    public const string Nutrition = "Beslenme";
    public const string Finance = "Finans";
    public const string Fitness = "Fitness";
    public const string Art = "Sanat";
    public const string Social = "Sosyal";
    public const string Study = "Çalışma";
    public const string Work = "İş";
    public const string Other = "Diğer";
    public const string Morning = "Sabah";
    public const string Day = "Gün";
    public const string Evening = "Akşam";

    // Eskiden formda sunulan, artik All icinde OLMAYAN kategoriler. Silinmiyorlar:
    // bu aliskanliklari olan eski kayitlar kendi basligiyla gorunmeye devam etmeli
    // (bkz. yukaridaki sinif yorumu). IndexOf ikisi icin de -1 doner.
    public const string Mind = "Zihin";
    public const string Productivity = "Üretkenlik";

    public const int MaxCustom = 12;
    public const int MaxNameLength = 16;

    // Hem form pillerinin hem liste gruplarinin sirasi.
    public static readonly string[] All =
    {
        Nutrition, Finance, Fitness, Art, Health, Social, Study, Work, Other,
        Morning, Day, Evening
    };

    public static string Default => Health;

    // Gorunumluk karsilik: bilinen yerlesikleri (Mind/Productivity DAHIL --
    // eski kayitlar icin hala taniniyorlar) Loc.Tr anahtarina esler.
    // Taninmayan (kullanicinin kendi yazdigi) bir kategori ASLA yeniden
    // yazilmiyor -- CLAUDE.md'nin "kayitli bir kategori asla yeniden
    // yazilmaz" kuraliyla cakismamak icin STORAGE'a (categoryGroup alanina)
    // hic dokunulmuyor, sadece burada GORUNUM icin donusturuluyor.
    public static string DisplayName(string category)
    {
        switch (category)
        {
            case Nutrition: return Loc.Tr("category.nutrition");
            case Finance: return Loc.Tr("category.finance");
            case Fitness: return Loc.Tr("category.fitness");
            case Art: return Loc.Tr("category.art");
            case Health: return Loc.Tr("category.health");
            case Social: return Loc.Tr("category.social");
            case Study: return Loc.Tr("category.study");
            case Work: return Loc.Tr("category.work");
            case Other: return Loc.Tr("category.other");
            case Morning: return Loc.Tr("category.morning");
            case Day: return Loc.Tr("category.day");
            case Evening: return Loc.Tr("category.evening");
            case Mind: return Loc.Tr("category.mind");
            case Productivity: return Loc.Tr("category.productivity");
            default: return category;
        }
    }

    // Gorunum sirasi icin: bilinmeyen kategori -1 doner ve listede sona alinir.
    public static int IndexOf(string category)
    {
        if (string.IsNullOrEmpty(category))
        {
            return -1;
        }

        for (int i = 0; i < All.Length; i++)
        {
            if (string.Equals(All[i], category, System.StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    // Ozel kategoriler yerleskilerden SONRA sirlanir -- IndexOf'un tek
    // parametreli surumu yalnizca yerleskilere bakiyor, bu asiri yukleme
    // kategori secici gibi ozel listeyi de bilen cagiricilar icin.
    public static int IndexOf(string category, IReadOnlyList<string> custom)
    {
        int builtIn = IndexOf(category);
        if (builtIn >= 0)
        {
            return builtIn;
        }

        if (custom == null || string.IsNullOrEmpty(category))
        {
            return -1;
        }

        for (int i = 0; i < custom.Count; i++)
        {
            if (string.Equals(custom[i], category, System.StringComparison.Ordinal))
            {
                return All.Length + i;
            }
        }

        return -1;
    }

    // Yerleskiler + ozel kategoriler, gorunum sirasiyla. buffer yeniden
    // kullanilan bir tampon -- cagrici her karede cagirabilir (PaintingChoices.
    // Collect ile ayni imza tarzi).
    public static void Collect(IReadOnlyList<string> custom, List<string> buffer)
    {
        buffer.Clear();

        for (int i = 0; i < All.Length; i++)
        {
            buffer.Add(All[i]);
        }

        if (custom == null)
        {
            return;
        }

        for (int i = 0; i < custom.Count; i++)
        {
            string category = custom[i];
            if (string.IsNullOrEmpty(category) || ListLookup.ContainsOrdinal(buffer, category))
            {
                continue;
            }

            buffer.Add(category);
        }
    }

    // Kullanicinin elle girdigi ozel kategori adini temizler: trim, uzunluk
    // kirpma, yerleskilerle ya da var olan ozel adlarla (kulturden bagimsiz
    // buyuk/kucuk harf duyarsiz) cakisma kontrolu. Gecersizse "" doner --
    // HabbitManager.NormalizePainterName ile ayni "savunma hatti" kalibi:
    // UI'dan gecmeyen cagirici (Dev araclari) da ayni kurali yemeli.
    public static string NormalizeCustom(string raw, IReadOnlyList<string> existingCustom)
    {
        string trimmed = raw == null ? string.Empty : raw.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        if (trimmed.Length > MaxNameLength)
        {
            trimmed = trimmed.Substring(0, MaxNameLength);
        }

        for (int i = 0; i < All.Length; i++)
        {
            if (string.Equals(All[i], trimmed, System.StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }
        }

        if (existingCustom != null)
        {
            for (int i = 0; i < existingCustom.Count; i++)
            {
                if (string.Equals(existingCustom[i], trimmed, System.StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }
            }
        }

        return trimmed;
    }
}
