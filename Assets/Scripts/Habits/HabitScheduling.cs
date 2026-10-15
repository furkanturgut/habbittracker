using System;
using System.Globalization;

// Tarih ve program (frequency / startDate / endDate) hesaplari.
//
// Hepsi static ve saf: sahne, MonoBehaviour ya da zaman kaynagi bagimliligi yok,
// tarih disaridan veriliyor. Gunluk reset ve streak mantigi bu sayede sahne
// kurmadan test edilebiliyor.
//
// Tarihler YEREL: aliskanlik gunu kullanicinin gece yarisinda doner, UTC'de degil.
public static class HabitScheduling
{
    public const string DateFormat = "yyyy-MM-dd";

    // Cihaz saati ileri kaymissa gun dongusu sonsuza kadar donmesin diye ust sinir.
    public const int MaxCatchUpDays = 400;

    public static string ToKey(DateTime date)
    {
        return date.ToString(DateFormat, CultureInfo.InvariantCulture);
    }

    public static bool TryParse(string dateKey, out DateTime date)
    {
        if (string.IsNullOrEmpty(dateKey))
        {
            date = default;
            return false;
        }

        return DateTime.TryParseExact(
            dateKey,
            DateFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    // Aliskanlik o gun planli mi? frequency bos birakilmissa "her gun" demektir --
    // kullanicidan gun secmesini zorunlu kilmamak icin.
    public static bool IsScheduledOn(HabitData habit, DateTime date)
    {
        if (habit.frequency == null || habit.frequency.Count == 0)
        {
            return true;
        }

        DayOfWeek day = date.DayOfWeek;
        for (int i = 0; i < habit.frequency.Count; i++)
        {
            if (habit.frequency[i] == day)
            {
                return true;
            }
        }

        return false;
    }

    // Aliskanlik o tarihte hala yururlukte mi? (baslangic gelmis, bitis gecmemis)
    public static bool IsActiveOn(HabitData habit, DateTime date)
    {
        if (TryParse(habit.startDate, out DateTime start) && date.Date < start.Date)
        {
            return false;
        }

        if (habit.hasEndDate && TryParse(habit.endDate, out DateTime end) && date.Date > end.Date)
        {
            return false;
        }

        return true;
    }

    // Hem planli hem yururlukte mi? Tik atilabilir gun tanimi bu.
    //
    // isArchived burada, IsActiveOn'da DEGIL: IsActiveOn saf bir tarih sorusu
    // ("baslangic gelmis, bitis gecmemis"), arsiv ise tarihten bagimsiz bir
    // durum. Bu tek satir MoodService/DailyProgress/MissedPieceService/
    // StreakFlame.IsPending/PaintingChoicePrompt dahil IsDueOn'un butun
    // tuketicilerini otomatik duzeltiyor (bkz. HabitArchive.cs).
    public static bool IsDueOn(HabitData habit, DateTime date)
    {
        return !habit.isArchived && IsActiveOn(habit, date) && IsScheduledOn(habit, date);
    }

    // Verilen gunden ONCEKI en yakin planli gun. Streak kirilmasi buna bakiyor:
    // "kullanici son tamamlamadan bu yana planli bir gun kacirdi mi?" Gecmiste hic
    // planli gun yoksa false doner.
    public static bool TryGetPreviousDueDay(HabitData habit, DateTime before, out DateTime previous)
    {
        // Sinir tarihleri dongu DISINDA bir kez cozuluyor: iceride cozmek ayni
        // string'i 400 kez parse etmek demekti.
        bool hasStart = TryParse(habit.startDate, out DateTime start);

        // Bitis tarihi yoksa "sonsuz": boylece asagidaki karsilastirma tek kollu
        // kaliyor. (Kosullu atamada end kesin atanmis sayilmadigi icin de gerekli.)
        DateTime end = DateTime.MaxValue;
        if (habit.hasEndDate && TryParse(habit.endDate, out DateTime parsedEnd))
        {
            end = parsedEnd;
        }

        DateTime cursor = before.Date.AddDays(-1);

        for (int i = 0; i < MaxCatchUpDays; i++)
        {
            if (hasStart && cursor.Date < start.Date)
            {
                break;
            }

            if (cursor.Date <= end.Date && IsScheduledOn(habit, cursor))
            {
                previous = cursor;
                return true;
            }

            cursor = cursor.AddDays(-1);
        }

        previous = default;
        return false;
    }
}
