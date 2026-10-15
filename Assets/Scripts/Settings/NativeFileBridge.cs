using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

// Sistem dosya secicisinin TEK C# yuzeyi: disa aktarmada "Kaydet-As"
// (kullanici klasoru ve adi secer), ice aktarmada "Dosya sec".
//
// TEMEL FIKIR: native taraf yalnizca DOSYA KOPYALAR, asla JSON tasimaz.
// Disa aktarmada SaveExportService'in zaten yazdigi kurtarma dosyasinin YOLU
// seciciye verilir; ice aktarmada secilen URI bir gecici dosyaya kopyalanir ve
// C# onu okur. Boylece 40-80 KB'lik bir metin JNI'den gecmez ve Faz 1-6'nin
// dogrulama/sanitize zinciri (SaveTransfer.TryRead -> Sanitize ->
// HabbitManager.TryImportSave) TEK SATIR DEGISMEDEN yeniden kullanilir.
//
// GERI CAGRI KANALI: iki platformda da UnitySendMessage. Nedeni,
// AndroidJavaProxy geri cagrisinin Java thread'inde gelmesi -- orada hicbir
// Unity API'si cagirilamaz. UnitySendMessage ana thread garantili.
//
// ALICI NESNE DontDestroyOnLoad OLMAK ZORUNDA: secici on plandayken uygulama
// OnApplicationPause yiyor ve Ayarlar paneli kapanabilir. Bu,
// HabbitManager.RequestReminderPermission'in coroutine'i KENDI uzerinde
// kosturma gerekcesinin (REMINDER_PLAN.md kritik duzeltme #2) aynisi.
//
// SAF CEKIRDEK AYRI PARCADA: payload okuma, hata jetonu cevirisi ve dosya
// adi temizligi NativeFileBridge.Payload.cs'de -- testlerin dokundugu tek
// yuzey orada, burada kalan her sey platform API'si ya da MonoBehaviour.
//
// #if SIRASI ONEMLI: UNITY_EDITOR EN BASTA. Editor'de UNITY_ANDROID/UNITY_IOS
// de tanimli olabilir (aktif build target'a gore), ama Editor'de her zaman
// EditorUtility diyaloglarini istiyoruz -- boylece tum round-trip Windows'ta
// Play mode'da cihazsiz dogrulanabiliyor.
public enum NativeFileOutcome
{
    Ok,
    Cancelled,
    Unavailable,
    Error
}

public struct NativeFileResult
{
    public NativeFileOutcome Outcome;

    // Ok'ta dolu. ICE AKTARMADA okunabilir bir YEREL dosya yolu (icerik oraya
    // kopyalandi). DISA AKTARMADA yalnizca "nereye gitti" bilgisi: Android'de
    // bir content:// URI, yani File ile OKUNAMAZ -- disa aktarma akisi onu
    // zaten okumuyor, kullaniciya DisplayName gosteriliyor.
    public string Path;

    // Ok'ta dolu olabilir: kullaniciya gosterilecek dosya adi. GUVENILMEZ --
    // secicinin DISPLAY_NAME'i null ya da garip gelebilir, SanitizeFileName
    // tek gecis noktasi.
    public string DisplayName;

    // Yalnizca Error/Unavailable'da dolu, Turkce ve kullaniciya gosterilebilir.
    public string Error;
}

public static partial class NativeFileBridge
{
    // UnitySendMessage nesneyi ADINA gore buluyor: bu ad Java ve Obj-C
    // tarafinda da ayni sabit. Degisirse mesaj SESSIZCE kaybolur ve geri cagri
    // hic gelmez (secici kapanir, hicbir sey olmaz).
    public const string ReceiverObjectName = "NativeFileBridge";
    public const string ResultMethodName = "OnNativeFileResult";

    public const string DefaultFileName = "HabitTracker_yedek.json";
    private const string JsonExtension = ".json";
    private const int MaxFileNameLength = 96;

    // Payload: "<outcome>|<displayName>|<error>|<path>"
    //
    // YOL EN SONDA, bilerek: Split'e alan sayisi verildigi icin son alan
    // ayracli da olsa HAM kaliyor. Native taraf Ok'ta yalnizca yol+ad,
    // Error'da yalnizca hata yolluyor -- ikisi ayni mesajda hic bulunmuyor,
    // yani hata metnindeki bir ayrac yolu yiyemez.
    private const char FieldSeparator = '|';
    private const int FieldCount = 4;

    private const string TokenOk = "ok";
    private const string TokenCancel = "cancel";
    private const string TokenError = "error";

    // Native taraf TURKCE CUMLE YOLLAMAZ, kisa ASCII jetonlari yollar:
    // kullaniciya gorunen her metin C#'ta kaliyor (projenin kalibi) ve
    // .java/.mm dosyalarinin karakter kodlamasi hic sorun olmuyor.
    private const string ErrorWrite = "write";
    private const string ErrorRead = "read";
    private const string ErrorNoPicker = "nopicker";

    // AYNI ANDA TEK BEKLEYEN CAGRI. Ikincisi Unavailable ile reddedilir --
    // SettingsScreen.reminderPermissionPending'in "OS diyalogu acikken ikinci
    // akis baslamaz" kuralinin aynisi.
    private static Action<NativeFileResult> pending;
    private static NativeFileBridgeReceiver receiver;

#if UNITY_EDITOR
    private static readonly bool supported = true;
#elif UNITY_ANDROID || UNITY_IOS
    private static readonly bool supported = true;
#else
    private static readonly bool supported = false;
#endif

    public static bool IsAvailable
    {
        get { return supported; }
    }

    public static bool IsBusy
    {
        get { return pending != null; }
    }

    // sourcePath DISKTE VAR OLMALI -- bu metod yeni bir dosya uretmiyor,
    // SaveExportService'in yazdigini kullanicinin sectigi yere kopyalatiyor.
    public static void SaveFile(string sourcePath, string suggestedName, Action<NativeFileResult> done)
    {
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
        {
            Invoke(done, Failure(NativeFileOutcome.Error, Loc.Tr("native_file.error.backup_not_found")));
            return;
        }

        if (!TryBegin(done))
        {
            return;
        }

        string fileName = SanitizeFileName(suggestedName);

        try
        {
            NativeSaveFile(sourcePath, fileName);
        }
        catch (Exception exception)
        {
            // Java sinifi hic derlenmemisse (Android Build Support kurulu
            // degilken) tam olarak buraya dusuyoruz.
            Debug.LogWarning("NativeFileBridge: kaydetme baslatilamadi. " + exception.Message);
            Fail(Loc.Tr("native_file.error.save_dialog_failed"));
        }
    }

    // destPath secilen icerigin kopyalanacagi YEREL hedef (bkz.
    // SaveExportService.ImportStagingPath). Secici kendi URI'sini veriyor;
    // uygulama hicbir depolama izni istemiyor.
    public static void PickFile(string destPath, Action<NativeFileResult> done)
    {
        if (string.IsNullOrEmpty(destPath))
        {
            Invoke(done, Failure(NativeFileOutcome.Error, Loc.Tr("native_file.error.temp_path_failed")));
            return;
        }

        if (!TryBegin(done))
        {
            return;
        }

        try
        {
            // Hedef klasor yoksa native taraf yazamaz; onu BURADA aciyoruz
            // (Java/Obj-C tarafini mumkun oldugunca aptal tutuyoruz).
            string folder = System.IO.Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            NativePickFile(destPath);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("NativeFileBridge: dosya secici baslatilamadi. " + exception.Message);
            Fail(Loc.Tr("native_file.error.pick_dialog_failed"));
        }
    }

    // -----------------------------------------------------------------------
    // Bekleyen cagri yonetimi
    // -----------------------------------------------------------------------

    private static bool TryBegin(Action<NativeFileResult> done)
    {
        if (!supported)
        {
            Invoke(done, Failure(NativeFileOutcome.Unavailable, Loc.Tr("native_file.error.no_picker_on_device")));
            return false;
        }

        if (pending != null)
        {
            Invoke(done, Failure(NativeFileOutcome.Unavailable, Loc.Tr("native_file.error.operation_in_progress")));
            return false;
        }

        pending = done;
        EnsureReceiver();
        return true;
    }

    // Native taraftan gelen TEK giris noktasi. Receiver bunu cagiriyor.
    internal static void Deliver(string payload)
    {
        // Geri cagri alani cagirmadan ONCE temizleniyor: geri cagrinin kendisi
        // yeni bir SaveFile/PickFile baslatabilir.
        Action<NativeFileResult> callback = pending;
        pending = null;

        if (callback == null)
        {
            Debug.LogWarning("NativeFileBridge: bekleyen cagri yokken sonuc geldi. " + payload);
            return;
        }

        if (!TryParseResult(payload, out NativeFileResult result))
        {
            Debug.LogWarning("NativeFileBridge: sonuc okunamadi. " + payload);
            result = Failure(NativeFileOutcome.Error, Loc.Tr("native_file.error.result_unreadable"));
        }
        else if (result.Outcome == NativeFileOutcome.Error)
        {
            // Native jetonu burada Turkce cumleye donuyor -- cagiran
            // (SettingsScreen) Error'u dogrudan kutuya basabilsin.
            result.Error = TranslateError(result.Error);
        }

        callback(result);
    }

    private static void Fail(string message)
    {
        Deliver(TokenError + FieldSeparator + FieldSeparator + message + FieldSeparator);
    }

    private static NativeFileResult Failure(NativeFileOutcome outcome, string message)
    {
        return new NativeFileResult
        {
            Outcome = outcome,
            Path = string.Empty,
            DisplayName = string.Empty,
            Error = message
        };
    }

    private static void Invoke(Action<NativeFileResult> done, NativeFileResult result)
    {
        if (done != null)
        {
            done(result);
        }
    }

    private static void EnsureReceiver()
    {
        // Editor'de Play mode disinda cagrilirsa sahneye bir nesne sizdirmayalim;
        // cihazda isPlaying her zaman true.
        if (receiver != null || !Application.isPlaying)
        {
            return;
        }

        GameObject host = new GameObject(ReceiverObjectName);
        UnityEngine.Object.DontDestroyOnLoad(host);
        receiver = host.AddComponent<NativeFileBridgeReceiver>();
    }

    // -----------------------------------------------------------------------
    // Platform dallari
    // -----------------------------------------------------------------------

#if UNITY_EDITOR

    // Editor dali bir "fallback" DEGIL, birincil dogrulama yolu: gercek
    // Windows diyaloglarini acar, yani Kaydet-As + Dosya sec round-trip'i
    // cihazsiz tam olarak denenebilir. Senkron tamamlaniyor -- Deliver
    // dogrudan cagriliyor.
    private static void NativeSaveFile(string sourcePath, string fileName)
    {
        string target = UnityEditor.EditorUtility.SaveFilePanel(
            "Yedegi kaydet", string.Empty, fileName, "json");

        if (string.IsNullOrEmpty(target))
        {
            Deliver(TokenCancel + FieldSeparator + FieldSeparator + FieldSeparator);
            return;
        }

        File.Copy(sourcePath, target, true);
        Deliver(TokenOk + FieldSeparator + System.IO.Path.GetFileName(target)
            + FieldSeparator + FieldSeparator + target);
    }

    private static void NativePickFile(string destPath)
    {
        string picked = UnityEditor.EditorUtility.OpenFilePanel("Yedek dosyasi sec", string.Empty, "json");

        if (string.IsNullOrEmpty(picked))
        {
            Deliver(TokenCancel + FieldSeparator + FieldSeparator + FieldSeparator);
            return;
        }

        File.Copy(picked, destPath, true);
        Deliver(TokenOk + FieldSeparator + System.IO.Path.GetFileName(picked)
            + FieldSeparator + FieldSeparator + destPath);
    }

#elif UNITY_ANDROID

    // ACTION_CREATE_DOCUMENT / ACTION_OPEN_DOCUMENT: hicbir izin ve
    // FileProvider gerektirmiyor, secici URI'yi kendisi veriyor. Sonuc
    // onActivityResult'tan geldigi icin Java tarafinda gorunmez bir Fragment
    // var -- saf JNI ile sonuc alinamaz.
    private const string JavaBridgeClass = "com.habittracker.NativeFileBridge";

    private static void NativeSaveFile(string sourcePath, string fileName)
    {
        using (AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
        using (AndroidJavaClass bridge = new AndroidJavaClass(JavaBridgeClass))
        {
            bridge.CallStatic("saveFile", activity, sourcePath, fileName);
        }
    }

    private static void NativePickFile(string destPath)
    {
        using (AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
        using (AndroidJavaClass bridge = new AndroidJavaClass(JavaBridgeClass))
        {
            bridge.CallStatic("pickFile", activity, destPath);
        }
    }

#elif UNITY_IOS

    // UIDocumentPickerViewController. DllImport("__Internal") DERLEME
    // zamaninda cozulmez (link zamani, cihazda) -- yani bu dal Windows'ta da
    // derlenir, yalnizca .mm'in Obj-C'si derlenmez.
    [DllImport("__Internal")]
    private static extern void HabitNativeSaveFile(string sourcePath, string fileName);

    [DllImport("__Internal")]
    private static extern void HabitNativePickFile(string destPath);

    private static void NativeSaveFile(string sourcePath, string fileName)
    {
        HabitNativeSaveFile(sourcePath, fileName);
    }

    private static void NativePickFile(string destPath)
    {
        HabitNativePickFile(destPath);
    }

#else

    // supported == false oldugu icin bu govdelere hic girilmiyor; yalnizca
    // derleyici icin var.
    private static void NativeSaveFile(string sourcePath, string fileName)
    {
    }

    private static void NativePickFile(string destPath)
    {
    }

#endif
}

// UnitySendMessage'in hedefi. Nesne adi ReceiverObjectName, metod adi
// ResultMethodName olmak ZORUNDA -- Java ve Obj-C tarafi bu iki ismi string
// olarak tasiyor.
[DisallowMultipleComponent]
internal class NativeFileBridgeReceiver : MonoBehaviour
{
    // Imza native tarafin bekledigi hali: tek string parametre.
    public void OnNativeFileResult(string payload)
    {
        NativeFileBridge.Deliver(payload);
    }
}
