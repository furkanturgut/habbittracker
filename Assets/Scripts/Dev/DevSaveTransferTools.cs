using UnityEngine;

// Disa/ice aktarmanin dev katmani: Inspector'dan tek tikla round-trip
// denemek icin. KURAL SAHIBI DEGIL -- DevRotusTools/DevHabitTools ile ayni
// "dev katmani hicbir sey karar vermez" ilkesi: ikisi de URETIM yolundan
// (SaveExportService, HabbitManager.TryImportSave) geciyor, aynen gercek
// Ayarlar ekranindaki dugmeler gibi.
public static class DevSaveTransferTools
{
    // Normal "Verileri disa aktar" dugmesiyle AYNI cagri -- yalnizca
    // Inspector'dan tetiklenebilir.
    public static SaveExportService.ExportResult ExportToFile(HabbitManager manager)
    {
        return manager != null ? SaveExportService.Export(manager) : default;
    }

    // Cihazdaki EN YENI kurtarma dosyasini okuyup ayni TryImportSave
    // uzerinden geri yukler -- Editor'de gercek bir pano/dosya secici
    // olmadan round-trip denemek icin.
    public static bool ImportNewestFile(HabbitManager manager, out string error)
    {
        if (manager == null)
        {
            error = "Yonetici yok.";
            return false;
        }

        if (!SaveExportService.TryReadNewestBackup(out string json, out error))
        {
            return false;
        }

        return manager.TryImportSave(json, out error);
    }

    // -----------------------------------------------------------------------
    // Sistem dosya secici yolu
    // -----------------------------------------------------------------------
    //
    // Yukaridaki ikisi seciciyi ATLAYARAK round-trip deniyor; bunlar
    // SettingsScreen'in GERCEK akisini deniyor (Export -> NativeFileBridge ->
    // TryReadFile -> TryImportSave). Editor'de gercek Windows diyaloglari
    // acildigi icin Kaydet-As ve Dosya sec cihazsiz denenebiliyor.
    //
    // Geri cagri yerine LOG atiyorlar: Inspector dugmesi senkron, secici ise
    // (cihazda) degil -- sonucu bekleyen bir dugme yapmak DevToolsEditor'u
    // durum tutmaya zorlardi.

    public static void ExportViaPicker(HabbitManager manager)
    {
        if (manager == null)
        {
            return;
        }

        SaveExportService.ExportResult result = SaveExportService.Export(manager);

        if (!result.Ok || string.IsNullOrEmpty(result.FilePath))
        {
            Debug.LogWarning("DevSaveTransferTools: disa aktarma basarisiz. " + result.Error);
            return;
        }

        NativeFileBridge.SaveFile(result.FilePath, result.FileName, picked =>
        {
            if (picked.Outcome == NativeFileOutcome.Ok)
            {
                Debug.Log("DevSaveTransferTools: yedek kaydedildi -> " + picked.Path);
                return;
            }

            Debug.Log("DevSaveTransferTools: kaydetme tamamlanmadi (" + picked.Outcome + "). " + picked.Error);
        });
    }

    public static void ImportViaPicker(HabbitManager manager)
    {
        if (manager == null)
        {
            return;
        }

        NativeFileBridge.PickFile(SaveExportService.ImportStagingPath(), picked =>
        {
            if (picked.Outcome != NativeFileOutcome.Ok)
            {
                Debug.Log("DevSaveTransferTools: secim tamamlanmadi (" + picked.Outcome + "). " + picked.Error);
                return;
            }

            if (!SaveExportService.TryReadFile(picked.Path, out string json, out string readError))
            {
                Debug.LogWarning("DevSaveTransferTools: " + readError);
                return;
            }

            Debug.Log(manager.TryImportSave(json, out string importError)
                ? "DevSaveTransferTools: yedek geri yuklendi."
                : "DevSaveTransferTools: ice aktarma basarisiz. " + importError);
        });
    }
}
