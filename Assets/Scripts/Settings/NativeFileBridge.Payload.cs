using System;
using System.Text;

// NativeFileBridge'in SAF CEKIRDEGI: native tarafin yolladigi payload'un
// okunmasi, hata jetonunun Turkceye cevrilmesi, secicinin dondurdugu dosya
// adinin temizlenmesi.
//
// NEDEN AYRI PARTIAL: ana dosya platform dallariyla birlikte 500 satir
// butcesini asiyor (HabbitManager.Rotus.cs / SettingsScreen.Data.cs ile ayni
// gerekce). Ayirma cizgisi tesaduf degil: BURADAKI HER SEY SAF ve
// Tests/NativeFileBridgeTests.cs'in dokundugu tek yuzey; ana dosyada kalan
// her sey ya platform API'si ya MonoBehaviour, yani test edilemeyen kisim
// ("pure core, thin wrapper").
//
// Ayraclar/jetonlar ana dosyada tanimli -- partial parcalar ayni sinif.
public static partial class NativeFileBridge
{
    // -----------------------------------------------------------------------
    // Saf cekirdek -- testlerin dokundugu kisim (Tests/NativeFileBridgeTests.cs)
    // -----------------------------------------------------------------------

    public static bool TryParseResult(string payload, out NativeFileResult result)
    {
        result = default;

        if (string.IsNullOrEmpty(payload))
        {
            return false;
        }

        // Alan sayisi verilerek bolunuyor: son alan (yol) ayrac icerse bile
        // kirpilmiyor.
        string[] parts = payload.Split(new[] { FieldSeparator }, FieldCount);
        if (parts.Length != FieldCount)
        {
            return false;
        }

        if (string.Equals(parts[0], TokenOk, StringComparison.Ordinal))
        {
            result.Outcome = NativeFileOutcome.Ok;
        }
        else if (string.Equals(parts[0], TokenCancel, StringComparison.Ordinal))
        {
            result.Outcome = NativeFileOutcome.Cancelled;
        }
        else if (string.Equals(parts[0], TokenError, StringComparison.Ordinal))
        {
            result.Outcome = NativeFileOutcome.Error;
        }
        else
        {
            return false;
        }

        result.DisplayName = parts[1];
        result.Error = parts[2];
        result.Path = parts[3];

        // Yolsuz bir "ok" anlamsiz -- cagirani bos bir yolla ugrastirmak
        // yerine bozuk payload sayiyoruz.
        if (result.Outcome == NativeFileOutcome.Ok && string.IsNullOrEmpty(result.Path))
        {
            result = default;
            return false;
        }

        return true;
    }

    // Native tarafin jetonunu kullaniciya gosterilecek Turkce cumleye cevirir.
    // TANINMAYAN METIN AYNEN GECER: C#'in kendi Fail() cagrilari zaten tam
    // cumle yolluyor, yalnizca native jetonlar cevriliyor.
    public static string TranslateError(string error)
    {
        if (string.IsNullOrEmpty(error))
        {
            return Loc.Tr("native_file.error.operation_failed_generic");
        }

        if (string.Equals(error, ErrorWrite, StringComparison.Ordinal))
        {
            return Loc.Tr("native_file.error.write_failed");
        }

        if (string.Equals(error, ErrorRead, StringComparison.Ordinal))
        {
            return Loc.Tr("native_file.error.read_failed");
        }

        if (string.Equals(error, ErrorNoPicker, StringComparison.Ordinal))
        {
            return Loc.Tr("native_file.error.no_picker_found");
        }

        return error;
    }

    // Secicinin dondurdugu ad GUVENILMEZ (null, bos, yol ayraci, kontrol
    // karakteri gelebilir). Ayrica '|' ayraci payload'u bozacagi icin Windows
    // disinda yasal olsa da atiliyor.
    public static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return DefaultFileName;
        }

        char[] illegal = System.IO.Path.GetInvalidFileNameChars();
        StringBuilder builder = new StringBuilder(name.Length);

        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];

            if (c == FieldSeparator || c < ' ' || c == System.IO.Path.DirectorySeparatorChar
                || c == System.IO.Path.AltDirectorySeparatorChar)
            {
                continue;
            }

            // Soguk yol (tek dosya adi) -- ListLookup'in List<int>/List<string>
            // sicak-yol asiri yuklemelerinden biri burada ise yaramiyor.
            if (Array.IndexOf(illegal, c) >= 0)
            {
                continue;
            }

            builder.Append(c);
        }

        string cleaned = builder.ToString().Trim();
        if (cleaned.Length == 0)
        {
            return DefaultFileName;
        }

        // Uzantiyi kirpmadan once govdeyi kisalt -- ".json" her zaman sonda
        // kalsin.
        if (cleaned.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned.Substring(0, cleaned.Length - JsonExtension.Length);
        }

        cleaned = cleaned.Trim();
        if (cleaned.Length == 0)
        {
            return DefaultFileName;
        }

        if (cleaned.Length > MaxFileNameLength)
        {
            cleaned = cleaned.Substring(0, MaxFileNameLength);
        }

        return cleaned + JsonExtension;
    }

}
