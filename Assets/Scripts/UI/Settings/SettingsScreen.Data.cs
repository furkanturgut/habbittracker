using UnityEngine;
using UnityEngine.UI;

// Ayarlar > "Veri" kartinin akislari: disa aktar, ice aktar.
//
// NEDEN AYRI PARTIAL: SettingsScreen.cs zaten butce disi -- HabbitManager'in
// Rotus/Archive/Import parcalariyla ayni gerekce ("partial parca, sinif icinden
// private uyelere eris"): manager, panel, confirmSheet hepsi private kaliyor.
//
// IKI KAYNAK, TEK ONAY YOLU: JSON ister sistem dosya secicisinden ister panodan
// gelsin, BeginImport'tan geciyor. Dogrulama (SaveTransfer.TryRead), ozet
// (Describe) ve onay kutusu tek yerde -- kaynak degisince ikinci bir kopya
// dogmasin.
public partial class SettingsScreen
{
    // Ice aktarmanin okudugu JSON, kullanici onay kutusunu kapatana kadar
    // degisebilir (pano kullanicinin elinde, gecici dosya yeniden yazilabilir)
    // -- onaylanan sey O AN okunan sey olmali.
    private string pendingImportJson;

    // OS secicisi acikken satira ikinci bir dokunus gelmesin --
    // reminderPermissionPending ile ayni rol. NativeFileBridge kendi icinde de
    // reddediyor; buradaki kopya, secici acilmadan ONCE yapilan is (pano
    // yazimi, kurtarma kopyasi) ikinci kez kosmasin diye.
    private bool nativePickerPending;

    // Kaydet-As geri cagrisi, seciciden ONCE uretilen ozeti (aliskanlik
    // sayisi, boyut, kurtarma kopyasinin adi) gostermek zorunda -- secici
    // dondugunde o bilgiyi bir daha hesaplayamayiz.
    private SaveExportService.ExportResult pendingExport;

    private void HandleExport()
    {
        if (!manager.Settings.isPremium)
        {
            // Teslim dosyasi: premium degilken bu satir Premium kartina
            // kaydiriyor -- gercek bir ScrollRect.ScrollTo API'si yok, en
            // basit karsiligi panelin basina donmek.
            if (panel != null)
            {
                ScrollRect scroll = panel.GetComponentInChildren<ScrollRect>();
                if (scroll != null)
                {
                    scroll.verticalNormalizedPosition = 1f;
                }
            }

            return;
        }

        if (nativePickerPending)
        {
            return;
        }

        SaveExportService.ExportResult result = SaveExportService.Export(manager);

        if (!result.Ok)
        {
            ShowInfo(Loc.Tr("data.export_failed_title"), result.Error);
            return;
        }

        // Kaydet-As yeni bir serialize YAPMIYOR: Export'un diske yazdigi
        // kurtarma kopyasinin yolunu seciciye veriyor.
        if (NativeFileBridge.IsAvailable && !string.IsNullOrEmpty(result.FilePath))
        {
            pendingExport = result;
            nativePickerPending = true;
            NativeFileBridge.SaveFile(result.FilePath, result.FileName, HandleSaveFileResult);
            return;
        }

        ShowInfo(Loc.Tr("data.backup_ready_title"), ClipboardBody(result));
    }

    // Panel geri cagri gelmeden kapanmis olabilir (secici on plandayken
    // uygulama OnApplicationPause yiyor). NativeFileBridge'in alicisi
    // DontDestroyOnLoad oldugu icin geri cagri HER ZAMAN geliyor; gosterecek
    // bir kutu yoksa sessizce birakiyoruz.
    private void HandleSaveFileResult(NativeFileResult result)
    {
        nativePickerPending = false;

        SaveExportService.ExportResult export = pendingExport;
        pendingExport = default;

        if (this == null || !gameObject.activeInHierarchy)
        {
            return;
        }

        if (result.Outcome == NativeFileOutcome.Ok)
        {
            string shown = string.IsNullOrEmpty(result.DisplayName) ? export.FileName : result.DisplayName;

            ShowInfo(
                Loc.Tr("data.backup_saved_title"),
                string.Format(Loc.Tr("data.backup_saved_body"), export.HabitCount, KiloBytes(export.ByteCount), shown));
            return;
        }

        if (result.Outcome == NativeFileOutcome.Cancelled)
        {
            // IPTAL BASARISIZLIK DEGIL: pano yazimi ve kurtarma kopyasi
            // secici acilmadan once gerceklesti, veri gercekten disa cikti.
            ShowInfo(Loc.Tr("data.backup_copied_title"), ClipboardBody(export)
                + "\n\n" + Loc.Tr("data.save_cancelled_suffix"));
            return;
        }

        // Secici hic acilamadi (Unavailable/Error) -- pano yolu yine de
        // tamamlandigi icin bugunun metni dogru kaliyor.
        ShowInfo(Loc.Tr("data.backup_ready_title"), ClipboardBody(export));
    }

    // Ice aktarma UCRETSIZ -- export'un aksine premium kapisi yok. Kurtarma
    // yolunu odeme arkasina koymak, makbuzu henuz geri yuklenmemis bir
    // kullaniciyi kendi verisinden kilitlerdi (bkz. EXPORT_IMPORT_PLAN.md).
    private void HandleImport()
    {
        if (nativePickerPending)
        {
            return;
        }

        if (NativeFileBridge.IsAvailable)
        {
            nativePickerPending = true;
            NativeFileBridge.PickFile(SaveExportService.ImportStagingPath(), HandlePickFileResult);
            return;
        }

        BeginImport(SaveExportService.ReadClipboard());
    }

    private void HandlePickFileResult(NativeFileResult result)
    {
        nativePickerPending = false;

        if (this == null || !gameObject.activeInHierarchy)
        {
            return;
        }

        if (result.Outcome == NativeFileOutcome.Cancelled)
        {
            // Kullanici vazgecti -- hicbir kutu acilmaz.
            return;
        }

        if (result.Outcome == NativeFileOutcome.Unavailable)
        {
            // Secici bu cihazda yok ya da mesgul: panoya DUSMEK dogru, yoksa
            // kullanicinin geri yukleme yolu hic kalmaz.
            BeginImport(SaveExportService.ReadClipboard());
            return;
        }

        if (result.Outcome != NativeFileOutcome.Ok)
        {
            ShowInfo(Loc.Tr("data.import_failed_title"), result.Error);
            return;
        }

        if (!SaveExportService.TryReadFile(result.Path, out string json, out string error))
        {
            ShowInfo(Loc.Tr("data.import_failed_title"), error);
            return;
        }

        BeginImport(json);
    }

    // Pano ve dosya yollarinin BIRLESTIGI yer: dogrulama, ozet ve onay burada.
    private void BeginImport(string json)
    {
        if (!SaveTransfer.TryRead(json, out SaveTransferPackage package, out string error))
        {
            ShowInfo(Loc.Tr("data.import_failed_title"), error);
            return;
        }

        pendingImportJson = json;

        if (confirmSheet == null)
        {
            // Onay kutusu yoksa ice aktarma ONAYSIZ CALISTIRILMAZ -- bu,
            // HandleResetRequested'in aksine (o fallback'te dogrudan siliyor),
            // cunku bu islemin verisi disaridan geliyor.
            Debug.LogWarning("SettingsScreen: confirmSheet yok, ice aktarma onaylanamadi.");
            pendingImportJson = null;
            return;
        }

        string body = SaveTransfer.Describe(package, manager.Library)
            + "\n\n" + Loc.Tr("data.restore_confirm_suffix");

        confirmSheet.Show(Loc.Tr("data.restore_confirm_title"), body, Loc.Tr("common.restore_button"), HandleImportConfirmed);
    }

    private void HandleImportConfirmed()
    {
        string json = pendingImportJson;
        pendingImportJson = null;

        if (!manager.TryImportSave(json, out string error))
        {
            ShowInfo(Loc.Tr("data.import_failed_title"), error);
            return;
        }

        RefreshAll();
    }

    private void ShowInfo(string title, string body)
    {
        if (confirmSheet == null)
        {
            return;
        }

        confirmSheet.Show(title, body, Loc.Tr("common.ok_button"), null);
    }

    private static int KiloBytes(int byteCount)
    {
        return Mathf.CeilToInt(byteCount / 1024f);
    }

    private string ClipboardBody(SaveExportService.ExportResult result)
    {
        string body = string.Format(Loc.Tr("data.clipboard_copied_body"), result.HabitCount, KiloBytes(result.ByteCount));

        if (!string.IsNullOrEmpty(result.FileName))
        {
            body += "\n\n" + string.Format(Loc.Tr("data.recovery_copy_suffix"), result.FileName);
        }

        return body;
    }
}
