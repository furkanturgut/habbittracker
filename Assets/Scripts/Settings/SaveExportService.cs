using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// Kaydi disa/ice aktarmanin tasima katmani: pano + cihazdaki bir kurtarma
// kopyasi. Runtime assembly'sinin ILK System.IO kullanimi -- asmdef degisikligi
// gerekmiyor, System.IO/GUIUtility BCL/UnityEngine.CoreModule'un parcasi.
//
// BU KATMAN DOSYAYI KULLANICIYA ULASTIRMAZ. Yazdigi dosya Android 11+'ta dosya
// yoneticilerinden GORUNMEZ (uygulama konteyneri), iOS'ta da erisilmez: burada
// "cihaza kaydedildi" bir KURTARMA kopyasi. Dosyayi kullanicinin sectigi yere
// goturen kati NativeFileBridge (bkz. NATIVE_FILE_TRANSFER_PLAN.md) -- ve
// seciciye verdigi sey tam olarak ExportResult.FilePath, yani burada yazilan
// dosya. Ikinci bir serialize yok.
public static class SaveExportService
{
    private const string BackupFolderName = "Yedekler";
    private const string FileNamePrefix = "HabitTracker_yedek_";
    private const string FileNameDateFormat = "yyyy-MM-dd_HHmm";
    private const int MaxBackupsToKeep = 5;

    public struct ExportResult
    {
        public bool Ok;
        public string FileName;

        // Kurtarma kopyasinin TAM yolu. Kaydet-As bunu dogrudan seciciye
        // veriyor; yazim basarisizsa bos kalir ve cagiran pano yoluna duser.
        public string FilePath;

        public int ByteCount;
        public int HabitCount;
        public string Error;
    }

    // Kaydi panoya VE cihazdaki bir kurtarma dosyasina yazar. Ikisi bagimsiz:
    // biri basarisiz olsa da digeri denenir, ikisi birden basarisizsa Ok=false.
    public static ExportResult Export(HabbitManager manager)
    {
        // Diskteki en guncel haliyle disa aktar -- debounce'da bekleyen bir
        // yazim varsa once onu bitiriyoruz.
        manager.FlushNow();

        string json = SaveTransfer.Build(manager.Data, AppClock.Now, Application.version);

        ExportResult result = new ExportResult
        {
            HabitCount = manager.Data.habits.Count,
            ByteCount = Encoding.UTF8.GetByteCount(json)
        };

        bool clipboardOk = TryWriteClipboard(json);
        bool fileOk = TryWriteBackupFile(json, out string fileName, out string filePath);

        result.Ok = clipboardOk || fileOk;
        result.FileName = fileName;
        result.FilePath = filePath;

        if (!result.Ok)
        {
            result.Error = Loc.Tr("save_export.error.write_failed");
        }

        return result;
    }

    // Ice aktarmanin tek okuma noktasi: hicbir yerde render edilmez, panodan
    // DOGRUDAN okunur (80 KB'lik bir metni bir TMP_InputField'a basmak hem
    // projenin SingleLine+characterLimit kalibini bozar hem gorunur bir
    // takilma uretirdi).
    public static string ReadClipboard()
    {
        try
        {
            return GUIUtility.systemCopyBuffer;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"SaveExportService: pano okunamadi. {exception.Message}");
            return null;
        }
    }

    // Ayarlar ekrani bunu cagirmiyor; DevSaveTransferTools.ImportNewestFile
    // ve adb ile cihaz testi kullaniyor -- secici olmadan round-trip denemenin
    // en kisa yolu.
    public static bool TryReadNewestBackup(out string json, out string error)
    {
        json = null;
        error = null;

        try
        {
            string folder = BackupFolderPath();
            if (!Directory.Exists(folder))
            {
                error = Loc.Tr("save_export.error.no_backup_on_device");
                return false;
            }

            string[] files = Directory.GetFiles(folder, FileNamePrefix + "*.json");
            if (files.Length == 0)
            {
                error = Loc.Tr("save_export.error.no_backup_on_device");
                return false;
            }

            // Dosya adi tarih-saat ile basliyor, ordinal siralama en yeniyi sona koyar.
            Array.Sort(files, StringComparer.Ordinal);
            json = File.ReadAllText(files[files.Length - 1], Encoding.UTF8);
            return true;
        }
        catch (Exception exception)
        {
            error = Loc.Tr("save_export.error.backup_file_unreadable");
            Debug.LogWarning($"SaveExportService: {exception.Message}");
            return false;
        }
    }

    // Dosya secicinin sectigi icerigi kopyalayacagi YEREL hedef.
    // persistentDataPath DEGIL: gecici bir ara dosya, prune'lanan "Yedekler"
    // klasorune karismamasi gerekiyor (orada en yeni 5 dosya kurali var ve
    // ice aktarilan yabanci bir dosya oraya girmemeli).
    public static string ImportStagingPath()
    {
        return Path.Combine(Application.temporaryCachePath, "import.json");
    }

    // TryReadNewestBackup'in tek-dosya kardesi: secici bir dosyayi kopyaladi,
    // iceriginin SaveTransfer.TryRead'e verilmesi gerekiyor. Dosyanin gercekten
    // bir yedek olup olmadigina BURADA bakilmiyor -- o kontrol TryRead'in isi.
    public static bool TryReadFile(string path, out string json, out string error)
    {
        json = null;
        error = null;

        if (string.IsNullOrEmpty(path))
        {
            error = Loc.Tr("save_export.error.empty_path");
            return false;
        }

        try
        {
            if (!File.Exists(path))
            {
                error = Loc.Tr("native_file.error.read_failed");
                return false;
            }

            json = File.ReadAllText(path, Encoding.UTF8);
            return true;
        }
        catch (Exception exception)
        {
            error = Loc.Tr("native_file.error.read_failed");
            Debug.LogWarning($"SaveExportService: {exception.Message}");
            return false;
        }
    }

    private static bool TryWriteClipboard(string json)
    {
        try
        {
            GUIUtility.systemCopyBuffer = json;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"SaveExportService: pano yazilamadi. {exception.Message}");
            return false;
        }
    }

    private static bool TryWriteBackupFile(string json, out string fileName, out string filePath)
    {
        fileName = null;
        filePath = null;

        try
        {
            string folder = BackupFolderPath();
            Directory.CreateDirectory(folder);

            fileName = FileNamePrefix + AppClock.Now.ToString(FileNameDateFormat, CultureInfo.InvariantCulture) + ".json";
            filePath = Path.Combine(folder, fileName);
            File.WriteAllText(filePath, json, Encoding.UTF8);

            // Budama YAZIMDAN SONRA: en yeni dosya biz oldugumuz icin
            // kendimizi silmiyoruz, ama Kaydet-As bu yolu seciciye verecegi
            // icin sira onemli -- once var olsun, sonra eskiler gitsin.
            PruneOldBackups(folder);
            return true;
        }
        catch (Exception exception)
        {
            fileName = null;
            filePath = null;
            Debug.LogWarning($"SaveExportService: dosyaya yazilamadi. {exception.Message}");
            return false;
        }
    }

    // Klasor sonsuza kadar buyumesin -- en yeni MaxBackupsToKeep disinda
    // kalanlari siler.
    private static void PruneOldBackups(string folder)
    {
        string[] files = Directory.GetFiles(folder, FileNamePrefix + "*.json");
        if (files.Length <= MaxBackupsToKeep)
        {
            return;
        }

        Array.Sort(files, StringComparer.Ordinal);

        int removeCount = files.Length - MaxBackupsToKeep;
        for (int i = 0; i < removeCount; i++)
        {
            File.Delete(files[i]);
        }
    }

    private static string BackupFolderPath()
    {
        return Path.Combine(Application.persistentDataPath, BackupFolderName);
    }
}
