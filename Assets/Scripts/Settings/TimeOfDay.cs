using System.Globalization;

// "HH:mm" dogrulama/ayristirma -- HabitForm'un tarih dogrulamasiyla AYNI
// desen: saf, test edilebilir bir TryParse. HabitSaveSystem'in kayittaki
// minimal kontrolunun (RepairSettings) KALICI esdegeri; Ayarlar ekranindaki
// "Saat" alani (gercek bir saat secici olmadigi icin TMP_InputField'a
// yazilan metin) de buradan geciyor (bkz. NAVIGATION_SETTINGS_PLAN.md Faz 5).
public static class TimeOfDay
{
    public static bool TryParse(string text, out int hour, out int minute)
    {
        hour = 0;
        minute = 0;

        if (string.IsNullOrEmpty(text) || text.Length != 5 || text[2] != ':')
        {
            return false;
        }

        if (!int.TryParse(text.Substring(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out hour)
            || hour < 0 || hour > 23)
        {
            return false;
        }

        if (!int.TryParse(text.Substring(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out minute)
            || minute < 0 || minute > 59)
        {
            return false;
        }

        return true;
    }

    public static bool IsValid(string text)
    {
        return TryParse(text, out _, out _);
    }

    public static string Format(int hour, int minute)
    {
        return hour.ToString("00", CultureInfo.InvariantCulture) + ":" + minute.ToString("00", CultureInfo.InvariantCulture);
    }

    // Saat/dakika adimlayicisi icin iki yonlu sarma (24 -> 0, -1 -> 23). "%"
    // operatoru C#'ta negatif sayida negatif kalan dondurur (-1 % 24 == -1),
    // modulus'u bir kez daha ekleyip tekrar mod almak bunu duzeltiyor.
    public static int Wrap(int value, int modulus)
    {
        return ((value % modulus) + modulus) % modulus;
    }
}
