using System.IO;
using NUnit.Framework;

// NativeFileBridge'in SAF cekirdeginin EditMode testleri: native taraftan
// gelen payload'un okunmasi ve secicinin dondurdugu dosya adinin
// temizlenmesi.
//
// Koprunun kendisi (AndroidJavaClass / DllImport / UnitySendMessage alicisi)
// test edilmiyor -- HabbitManager ile ayni sinir: platform API'si ve
// MonoBehaviour hicbir testte kurulmuyor. "Pure core, thin wrapper" kurali
// geregi karar veren her sey bu iki statik metodda.
//
// Calistirmak icin: Window > General > Test Runner > EditMode > Run All.
public class NativeFileBridgeTests
{
    // -----------------------------------------------------------------------
    // TryParseResult
    // -----------------------------------------------------------------------

    [Test]
    public void TryParseResult_Ok_ParsesPathAndDisplayName()
    {
        bool read = NativeFileBridge.TryParseResult(
            "ok|yedek.json||/storage/emulated/0/Download/yedek.json",
            out NativeFileResult result);

        Assert.IsTrue(read);
        Assert.AreEqual(NativeFileOutcome.Ok, result.Outcome);
        Assert.AreEqual("yedek.json", result.DisplayName);
        Assert.AreEqual("/storage/emulated/0/Download/yedek.json", result.Path);
        Assert.AreEqual(string.Empty, result.Error);
    }

    [Test]
    public void TryParseResult_Cancel_YieldsCancelled()
    {
        bool read = NativeFileBridge.TryParseResult("cancel|||", out NativeFileResult result);

        Assert.IsTrue(read);
        Assert.AreEqual(NativeFileOutcome.Cancelled, result.Outcome);
        Assert.AreEqual(string.Empty, result.Path);
    }

    [Test]
    public void TryParseResult_Error_CarriesMessage()
    {
        bool read = NativeFileBridge.TryParseResult(
            "error||Dosya yazilamadi.|", out NativeFileResult result);

        Assert.IsTrue(read);
        Assert.AreEqual(NativeFileOutcome.Error, result.Outcome);
        Assert.AreEqual("Dosya yazilamadi.", result.Error);
    }

    [Test]
    public void TryParseResult_Garbage_IsRejected()
    {
        Assert.IsFalse(NativeFileBridge.TryParseResult(null, out _));
        Assert.IsFalse(NativeFileBridge.TryParseResult(string.Empty, out _));

        // Ayrac eksik -- tek alan.
        Assert.IsFalse(NativeFileBridge.TryParseResult("ok", out _));

        // Alan sayisi yetersiz.
        Assert.IsFalse(NativeFileBridge.TryParseResult("ok|ad|", out _));

        // Taninmayan sonuc jetonu.
        Assert.IsFalse(NativeFileBridge.TryParseResult("maybe|||/tmp/x.json", out _));

        // Yabanci bir metin.
        Assert.IsFalse(NativeFileBridge.TryParseResult("merhaba", out _));
    }

    [Test]
    public void TryParseResult_OkWithoutPath_IsRejected()
    {
        // Yolsuz bir "ok" cagirana hicbir sey veremez -- bozuk payload sayilir,
        // yoksa SettingsScreen bos bir yolu dosya sanip okumaya kalkardi.
        Assert.IsFalse(NativeFileBridge.TryParseResult("ok|yedek.json||", out _));
    }

    [Test]
    public void TryParseResult_PathWithSeparatorChar_IsNotTruncated()
    {
        // Yol EN SON alan ve Split'e alan sayisi veriliyor: icinde ayrac olsa
        // bile ham kaliyor. (Linux/Android'de '|' yasal bir dosya adi karakteri.)
        bool read = NativeFileBridge.TryParseResult(
            "ok|ad.json||/sdcard/a|b/yedek.json", out NativeFileResult result);

        Assert.IsTrue(read);
        Assert.AreEqual("/sdcard/a|b/yedek.json", result.Path);
    }

    // -----------------------------------------------------------------------
    // TranslateError
    // -----------------------------------------------------------------------

    [Test]
    public void TranslateError_NativeTokens_BecomeTurkishSentences()
    {
        // Native taraf (Java/Obj-C) kisa ASCII jetonu yolluyor; kullaniciya
        // gorunen Turkce cumle C#'ta uretiliyor.
        Assert.AreEqual("Yedek seçilen yere yazılamadı.", NativeFileBridge.TranslateError("write"));
        Assert.AreEqual("Seçilen dosya okunamadı.", NativeFileBridge.TranslateError("read"));
        Assert.AreEqual("Bu cihazda dosya seçici bulunamadı.", NativeFileBridge.TranslateError("nopicker"));
    }

    [Test]
    public void TranslateError_UnknownText_PassesThrough()
    {
        // C#'in kendi hata cagrilari zaten tam cumle yolluyor -- ikinci bir
        // cevirmeye girmemeleri gerekiyor.
        Assert.AreEqual("Dosya kaydetme penceresi açılamadı.",
            NativeFileBridge.TranslateError("Dosya kaydetme penceresi açılamadı."));
    }

    [Test]
    public void TranslateError_Empty_FallsBackToGenericSentence()
    {
        Assert.AreEqual("Dosya işlemi tamamlanamadı.", NativeFileBridge.TranslateError(null));
        Assert.AreEqual("Dosya işlemi tamamlanamadı.", NativeFileBridge.TranslateError(string.Empty));
    }

    // -----------------------------------------------------------------------
    // SanitizeFileName
    // -----------------------------------------------------------------------

    [Test]
    public void SanitizeFileName_EmptyFallsBackToDefault()
    {
        Assert.AreEqual(NativeFileBridge.DefaultFileName, NativeFileBridge.SanitizeFileName(null));
        Assert.AreEqual(NativeFileBridge.DefaultFileName, NativeFileBridge.SanitizeFileName(string.Empty));
        Assert.AreEqual(NativeFileBridge.DefaultFileName, NativeFileBridge.SanitizeFileName("   "));

        // Temizlikten sonra govde bos kalirsa da varsayilana duser.
        Assert.AreEqual(NativeFileBridge.DefaultFileName, NativeFileBridge.SanitizeFileName(".json"));
    }

    [Test]
    public void SanitizeFileName_StripsIllegalCharacters()
    {
        string name = NativeFileBridge.SanitizeFileName("ye|dek\n/kopya.json");

        Assert.AreEqual("yedekkopya.json", name);

        // Yol ayraci hicbir platformda icinde kalmamali.
        Assert.AreEqual(-1, name.IndexOf(Path.DirectorySeparatorChar));
        Assert.AreEqual(-1, name.IndexOf('/'));
        Assert.AreEqual(-1, name.IndexOf('|'));
    }

    [Test]
    public void SanitizeFileName_KeepsSingleJsonExtension()
    {
        Assert.AreEqual("yedek.json", NativeFileBridge.SanitizeFileName("yedek.json"));

        // Uzantisizsa eklenir.
        Assert.AreEqual("yedek.json", NativeFileBridge.SanitizeFileName("yedek"));

        // Buyuk/kucuk harf farki ikinci bir uzanti dogurmamali.
        Assert.AreEqual("yedek.json", NativeFileBridge.SanitizeFileName("yedek.JSON"));
    }

    [Test]
    public void SanitizeFileName_LongName_IsClampedButKeepsExtension()
    {
        string name = NativeFileBridge.SanitizeFileName(new string('a', 400) + ".json");

        Assert.IsTrue(name.EndsWith(".json"));
        Assert.Less(name.Length, 200);
    }

    [Test]
    public void SanitizeFileName_KeepsTurkishCharacters()
    {
        // Kullaniciya gorunen ad -- diakritikler atilmamali (ASCII-only kurali
        // YORUMLAR icin, kullanici metni icin degil).
        Assert.AreEqual("alışkanlık.json", NativeFileBridge.SanitizeFileName("alışkanlık"));
    }
}
