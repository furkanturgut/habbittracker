using System;
using System.Globalization;

// Galeri'nin 2. seviyesindeki tarih satiri icin ay kisaltmalari.
//
// HabitScheduling.TryParse uzerinden calisir -- "yyyy-MM-dd" disinda bir
// deger hic cagrilmaz, ikinci bir parse kuralina gerek yok. Ay adlari Loc.Tr
// uzerinden geliyor (LOCALIZATION_PLAN.md Faz 5, "month.1".."month.12") --
// gun-ay-yil SIRASI butun dillerde AYNI kaliyor, sadece ay kisaltmasi degisiyor.
public static class DateLabels
{
    public static string Format(DateTime date)
    {
        return date.Day.ToString(CultureInfo.InvariantCulture)
            + " " + Loc.Tr("month." + date.Month)
            + " " + date.Year.ToString(CultureInfo.InvariantCulture);
    }

    public static bool TryFormat(string dateKey, out string label)
    {
        if (!HabitScheduling.TryParse(dateKey, out DateTime date))
        {
            label = null;
            return false;
        }

        label = Format(date);
        return true;
    }
}
