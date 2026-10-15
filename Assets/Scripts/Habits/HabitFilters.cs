using System;
using System.Collections.Generic;

// Atolye ekranindaki elmas filtresi.
//
// Saf ve static: elmaslarin sayilari ile listenin suzulmesi ayni kuraldan gecsin
// diye. Hot path degil ama tik atildikca yeniden hesaplandigi icin LINQ yok.
//
// MaxSlots BUGUNKU elmas sayisina kilitli (Tumu + 4). HabitFilterBar'in
// yerlesimi (WorkshopSceneBuilder.BuildFilterBar) 5 elmasa gore olculmus --
// 62*5 + 10*4 = 350, 366 birimlik kolona tam sigiyor. Kategori sayisi
// 4'ten 12+'ya cikinca kullanici hepsini filtre olarak secemesin diye bu
// sinir var; Collect TEK kapi, baska hicbir yer bu sinira kendi basina
// uymamali.
public static class HabitFilters
{
    public enum Kind
    {
        All,
        Build,
        Quit,
        Category
    }

    public const int MaxSlots = 5;

    // Ekranda ilk acilista gorunen varsayilan secim -- bugunku 5 elmasin
    // (Tumu/Yap/Birak/Saglik/Fitness) ayni karsiligi.
    public static readonly string[] DefaultTokens =
    {
        "@all", "@build", "@quit", HabitCategories.Health, HabitCategories.Fitness
    };

    public readonly struct HabitFilter : IEquatable<HabitFilter>
    {
        public readonly Kind Kind;
        public readonly string Category;

        public HabitFilter(Kind kind, string category = null)
        {
            Kind = kind;
            Category = kind == Kind.Category ? category : null;
        }

        public static readonly HabitFilter All = new HabitFilter(Kind.All);
        public static readonly HabitFilter Build = new HabitFilter(Kind.Build);
        public static readonly HabitFilter Quit = new HabitFilter(Kind.Quit);

        public bool Equals(HabitFilter other)
        {
            return Kind == other.Kind
                && (Kind != Kind.Category || string.Equals(Category, other.Category, StringComparison.Ordinal));
        }

        public override bool Equals(object obj)
        {
            return obj is HabitFilter other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Kind == Kind.Category && Category != null
                ? Category.GetHashCode()
                : (int)Kind;
        }
    }

    // Token <-> filtre donusumunun TEK yeri -- WeekDays/MoodScale'in "tek
    // donusturucu" kaliplariyla ayni gerekce: iki el yazimi kopya birbirinden
    // kayardi ve hata sessiz kalirdi (yanlis elmasin yanmasi gibi).
    public static string Token(in HabitFilter filter)
    {
        switch (filter.Kind)
        {
            case Kind.Build: return "@build";
            case Kind.Quit: return "@quit";
            case Kind.Category: return filter.Category ?? string.Empty;
            default: return "@all";
        }
    }

    public static bool TryParse(string token, out HabitFilter filter)
    {
        if (string.Equals(token, "@all", StringComparison.Ordinal))
        {
            filter = HabitFilter.All;
            return true;
        }

        if (string.Equals(token, "@build", StringComparison.Ordinal))
        {
            filter = HabitFilter.Build;
            return true;
        }

        if (string.Equals(token, "@quit", StringComparison.Ordinal))
        {
            filter = HabitFilter.Quit;
            return true;
        }

        // "@" ile baslayan baska her sey bilinmeyen bir yerlesik -- gelecekte
        // yeni bir yerlesik eklenirse eski kayittaki bu token kategori gibi
        // yanlis yorumlanmasin.
        if (!string.IsNullOrEmpty(token) && token[0] != '@')
        {
            filter = new HabitFilter(Kind.Category, token);
            return true;
        }

        filter = default;
        return false;
    }

    // tokens'i filtreye cozer: gecersiz/yinelenen dusurulur, Tumu YOKSA basa
    // eklenir, MaxSlots'a kirpilir. Kaydedilmis bir token listesi ne olursa
    // olsun sonuc asla MaxSlots'u gecemez.
    public static void Collect(IReadOnlyList<string> tokens, List<HabitFilter> buffer)
    {
        buffer.Clear();
        buffer.Add(HabitFilter.All);

        if (tokens == null)
        {
            return;
        }

        for (int i = 0; i < tokens.Count && buffer.Count < MaxSlots; i++)
        {
            if (!TryParse(tokens[i], out HabitFilter filter) || filter.Kind == Kind.All)
            {
                continue;
            }

            if (ContainsFilter(buffer, filter))
            {
                continue;
            }

            buffer.Add(filter);
        }
    }

    public static string Label(in HabitFilter filter)
    {
        switch (filter.Kind)
        {
            case Kind.Build: return Loc.Tr("habit_type.build");
            case Kind.Quit: return Loc.Tr("habit_type.quit");
            case Kind.Category: return HabitCategories.DisplayName(filter.Category);
            default: return Loc.Tr("filter.all_label");
        }
    }

    // Arsivdeki aliskanlik hicbir elmasa (Tumu dahil) girmez -- Atolye karo
    // izgarasi ve her elmasin sayaci AYNI bu kapidan geciyor, tek yerde durmasi
    // ikisinin birbirinden kaymasini onluyor.
    public static bool Matches(in HabitFilter filter, HabitData habit)
    {
        if (habit.isArchived)
        {
            return false;
        }

        switch (filter.Kind)
        {
            case Kind.Build: return !HabitTypes.IsQuit(habit);
            case Kind.Quit: return HabitTypes.IsQuit(habit);
            case Kind.Category: return IsCategory(habit, filter.Category);
            default: return true;
        }
    }

    public static int Count(in HabitFilter filter, IReadOnlyList<HabitData> habits)
    {
        if (habits == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < habits.Count; i++)
        {
            if (Matches(filter, habits[i]))
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsCategory(HabitData habit, string category)
    {
        return string.Equals(habit.categoryGroup, category, StringComparison.Ordinal);
    }

    private static bool ContainsFilter(List<HabitFilter> values, in HabitFilter filter)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (values[i].Equals(filter))
            {
                return true;
            }
        }

        return false;
    }
}
