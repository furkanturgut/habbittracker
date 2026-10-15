using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// Disa/ice aktarma zarfi ve onun dogrulama/onarim mantigi. KAYIT FORMATININ
// PARCASI DEGIL -- PlayerSaveData kendi basina hala tek kaynak; bu sinif onu
// bir ZARFA sarip geri cozuyor.
//
// Saf ve static: sahne, MonoBehaviour ya da zaman kaynagi gerekmiyor, boylece
// yabanci/bozuk JSON senaryolari sahne kurmadan test edilebiliyor.
[Serializable]
public class SaveTransferPackage
{
    // Zarfi yabanci JSON'dan ayirt eden sabit. Alanin kendisinde BILEREK
    // varsayilan deger YOK: JsonUtility.FromJson bir alan JSON'da yoksa alani
    // tipin varsayilanina (burada null) birakiyor. Bir varsayilan verilseydi
    // (= FormatId), "{}" ya da yabanci bir JSON bile bu kontrolu GECERDI --
    // zarfin kendi degeri hep Build() icinde ACIKCA yaziliyor.
    public const string FormatId = "habittracker.save";

    // ZARFIN surumu -- data.saveVersion'dan BAGIMSIZ bir kapi. Ikisi ayri
    // hareket edebilir: zarf hic degismeden kayit formati surum atlayabilir,
    // ya da tam tersi.
    public const int CurrentFormatVersion = 1;

    public string format;
    public int formatVersion;
    public string exportedAt;
    public string appVersion;

    // data.habits.Count ile ayni olmasi beklenir -- panonun ortasindan kesilmis
    // bir yapistirmayi (habitCount yazili ama habits kesik) yakalamanin ucuncu
    // kontrolu.
    public int habitCount;

    public PlayerSaveData data;
}

public static class SaveTransfer
{
    private const string ExportedAtFormat = "yyyy-MM-dd HH:mm";

    // Kaynagi ASLA degistirmez: derin kopya alinip ANCAK kopya uzerinde
    // redakte edilir. data.settings.isPremium = false dogrudan yazilsaydi,
    // disa aktarma kullanicinin kendi premium durumunu silerdi.
    public static string Build(PlayerSaveData data, DateTime now, string appVersion)
    {
        PlayerSaveData copy = JsonUtility.FromJson<PlayerSaveData>(JsonUtility.ToJson(data));

        if (copy.settings == null)
        {
            copy.settings = new AppSettingsData();
        }

        // Gercek IAP geldiginde paylasilan bir yedek dosyasi bedava-premium
        // dagitim yoluna donerdi -- premium makbuzdan geri yuklenir, yedekten
        // degil (bkz. EXPORT_IMPORT_PLAN.md).
        copy.settings.isPremium = false;

        if (copy.habits == null)
        {
            copy.habits = new List<HabitData>();
        }

        SaveTransferPackage package = new SaveTransferPackage
        {
            format = SaveTransferPackage.FormatId,
            formatVersion = SaveTransferPackage.CurrentFormatVersion,
            exportedAt = now.ToString(ExportedAtFormat, CultureInfo.InvariantCulture),
            appVersion = appVersion ?? string.Empty,
            habitCount = copy.habits.Count,
            data = copy
        };

        return JsonUtility.ToJson(package);
    }

    // Depoya hicbir sey yazilmadan ONCE cagrilmak zorunda. JsonUtility.
    // FromJsonOverwrite bozuk JSON'u yakalar ama YABANCI/eksik JSON'u yakalamaz --
    // "{}" gecerli bir nesnedir, hicbir alani tanimadigi icin hicbir sey
    // yazmaz ve sonuc "bos ama gecerli bir kayit" olur. Zarf kontrolleri tam
    // olarak bu sessiz veri kaybini onlemek icin var.
    public static bool TryRead(string json, out SaveTransferPackage package, out string error)
    {
        package = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = Loc.Tr("save_transfer.error.clipboard_empty");
            return false;
        }

        SaveTransferPackage parsed;
        try
        {
            parsed = JsonUtility.FromJson<SaveTransferPackage>(json);
        }
        catch (Exception)
        {
            error = Loc.Tr("save_transfer.error.not_habittracker_backup");
            return false;
        }

        if (parsed == null || string.IsNullOrEmpty(parsed.format)
            || !string.Equals(parsed.format, SaveTransferPackage.FormatId, StringComparison.Ordinal))
        {
            error = Loc.Tr("save_transfer.error.not_habittracker_backup");
            return false;
        }

        if (parsed.formatVersion < 1)
        {
            error = Loc.Tr("save_transfer.error.backup_corrupt");
            return false;
        }

        if (parsed.formatVersion > SaveTransferPackage.CurrentFormatVersion)
        {
            error = Loc.Tr("save_transfer.error.backup_too_new");
            return false;
        }

        if (parsed.data == null)
        {
            error = Loc.Tr("save_transfer.error.backup_incomplete");
            return false;
        }

        if (parsed.data.saveVersion > PlayerSaveData.CurrentSaveVersion)
        {
            error = Loc.Tr("save_transfer.error.backup_too_new");
            return false;
        }

        int actualHabitCount = parsed.data.habits != null ? parsed.data.habits.Count : 0;
        if (parsed.habitCount != actualHabitCount)
        {
            error = Loc.Tr("save_transfer.error.backup_incomplete");
            return false;
        }

        package = parsed;
        return true;
    }

    // RepairHabits'in YAPMADIKLARI: parca indeksi tutarliligi, tuketilen
    // toplamin canvasSize'i asmasi, gelecek tarihler, negatif/tutarsiz
    // streakler, gecersiz gun degerleri. RepairHabits'e TASINMIYOR -- o her
    // acilista calisan sicak yol, bu ise yalnizca ice aktarmanin (guvenilmez
    // kaynakli veri) sert pass'i.
    public static void Sanitize(PlayerSaveData data, DateTime now)
    {
        if (data == null)
        {
            return;
        }

        DateTime today = now.Date;
        data.lastLoginDate = ClampDateKey(data.lastLoginDate, today);
        data.lastMoodSettledDate = ClampDateKey(data.lastMoodSettledDate, today);

        if (data.rotusCount < 0)
        {
            data.rotusCount = 0;
        }

        // Bozuk/gelecek-surum bir ice aktarimda (bilinmeyen int degeri)
        // Unset'e dusuluyor -- cihaz kendi dilini yeniden tespit eder
        // (HabbitManager.ApplyLoadedSave), cokme olmaz.
        if (data.settings != null)
        {
            int language = (int)data.settings.language;
            if (language < (int)AppLanguage.Unset || language > (int)AppLanguage.Spanish)
            {
                data.settings.language = AppLanguage.Unset;
            }
        }

        if (data.habits == null)
        {
            return;
        }

        for (int i = 0; i < data.habits.Count; i++)
        {
            SanitizeHabit(data.habits[i], today);
        }
    }

    // Onay kutusunda gosterilecek Turkce ozet. library verilirse cozulemeyen
    // tablo id'leri icin ikinci bir uyari satiri ekler -- iki kurulumun sanat
    // seti farkliysa RepairAll zaten bunlari onaracak, ama kullanici bunu
    // ONCEDEN bilmeli.
    public static string Describe(SaveTransferPackage package, PaintingLibrary library)
    {
        if (package == null || package.data == null)
        {
            return string.Empty;
        }

        List<HabitData> habits = package.data.habits;
        int habitCount = habits != null ? habits.Count : 0;
        int finishedCount = 0;
        int unresolved = 0;

        if (habits != null)
        {
            for (int i = 0; i < habits.Count; i++)
            {
                HabitData habit = habits[i];
                if (habit == null)
                {
                    continue;
                }

                if (habit.habitGallery != null)
                {
                    finishedCount += habit.habitGallery.Count;

                    if (library != null)
                    {
                        for (int g = 0; g < habit.habitGallery.Count; g++)
                        {
                            if (!library.TryGet(habit.habitGallery[g], out _))
                            {
                                unresolved++;
                            }
                        }
                    }
                }

                if (library != null && !string.IsNullOrEmpty(habit.activePaintingId)
                    && !library.TryGet(habit.activePaintingId, out _))
                {
                    unresolved++;
                }
            }
        }

        string dateLabel = package.exportedAt;
        if (DateTime.TryParseExact(
            package.exportedAt, ExportedAtFormat, CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out DateTime exportedAt))
        {
            dateLabel = DateLabels.Format(exportedAt);
        }

        StringBuilder builder = new StringBuilder();
        builder.Append(string.Format(Loc.Tr("save_transfer.describe.summary"),
            habitCount, finishedCount, package.data.rotusCount, dateLabel));

        if (unresolved > 0)
        {
            builder.Append("\n\n");
            builder.Append(string.Format(Loc.Tr("save_transfer.describe.library_mismatch"), unresolved));
        }

        return builder.ToString();
    }

    private static string ClampDateKey(string dateKey, DateTime today)
    {
        if (!HabitScheduling.TryParse(dateKey, out DateTime parsed))
        {
            return dateKey;
        }

        return parsed.Date > today ? HabitScheduling.ToKey(today) : dateKey;
    }

    private static void SanitizeHabit(HabitData habit, DateTime today)
    {
        if (habit == null)
        {
            return;
        }

        habit.lastCompletedDate = ClampDateKey(habit.lastCompletedDate, today);
        habit.lastCelebratedDate = ClampDateKey(habit.lastCelebratedDate, today);

        if (habit.currentStreak < 0)
        {
            habit.currentStreak = 0;
        }

        if (habit.longestStreak < habit.currentStreak)
        {
            habit.longestStreak = habit.currentStreak;
        }

        if (habit.rotusProgress < 0)
        {
            habit.rotusProgress = 0;
        }

        if (habit.frequency != null)
        {
            for (int i = habit.frequency.Count - 1; i >= 0; i--)
            {
                int value = (int)habit.frequency[i];
                if (value < 0 || value > 6)
                {
                    habit.frequency.RemoveAt(i);
                }
            }
        }

        int canvasSize = habit.canvasSize > 0 ? habit.canvasSize : HabitData.DefaultCanvasSize;

        if (habit.randomOpenedPixels != null)
        {
            DedupeAndClampRange(habit.randomOpenedPixels, canvasSize);
        }

        if (habit.missedPixels != null)
        {
            DedupeAndClampRange(habit.missedPixels, canvasSize);
        }

        // Ayni indeks hem acik hem yanik ise ACIK kazanir -- PaintingAssignment.
        // Resize'daki "kullanicinin boyadigi pikseller delikler icin feda
        // edilmez" kuralinin aynisi.
        if (habit.randomOpenedPixels != null && habit.missedPixels != null)
        {
            for (int i = habit.missedPixels.Count - 1; i >= 0; i--)
            {
                if (ListLookup.Contains(habit.randomOpenedPixels, habit.missedPixels[i]))
                {
                    habit.missedPixels.RemoveAt(i);
                }
            }
        }

        if (habit.totalPixelsPainted < 0)
        {
            habit.totalPixelsPainted = 0;
        }

        int missedCount = habit.missedPixels != null ? habit.missedPixels.Count : 0;
        int consumed = habit.totalPixelsPainted + missedCount;

        if (consumed > canvasSize)
        {
            // Rastgele modda bu dal hic tetiklenmez: indeksler zaten [0,canvasSize)
            // icinde ve birbirinden ayrik, yani Count toplami otomatik olarak
            // canvasSize'i asamaz. Burasi yalnizca Sequential'in SAYI tabanli
            // totalPixelsPainted'inin yakilan parcalarla birlikte tasmasini
            // kirpiyor -- acik parcalar ONCELIKLI (once yakilandan kirpilir).
            int overflow = consumed - canvasSize;

            if (habit.missedPixels != null && habit.missedPixels.Count > 0)
            {
                int trim = Mathf.Min(overflow, habit.missedPixels.Count);
                habit.missedPixels.RemoveRange(habit.missedPixels.Count - trim, trim);
                overflow -= trim;
            }

            if (overflow > 0)
            {
                habit.totalPixelsPainted = Mathf.Max(0, habit.totalPixelsPainted - overflow);
            }
        }
    }

    private static void DedupeAndClampRange(List<int> values, int canvasSize)
    {
        for (int i = values.Count - 1; i >= 0; i--)
        {
            int value = values[i];
            if (value < 0 || value >= canvasSize)
            {
                values.RemoveAt(i);
                continue;
            }

            bool duplicate = false;
            for (int j = 0; j < i; j++)
            {
                if (values[j] == value)
                {
                    duplicate = true;
                    break;
                }
            }

            if (duplicate)
            {
                values.RemoveAt(i);
            }
        }
    }
}
