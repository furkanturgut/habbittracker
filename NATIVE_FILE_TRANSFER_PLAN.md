# Faz 7 -- Gercek dosya tasima yolu (Android + iOS "Kaydet / Dosya sec")

**Durum: KOD TARAFI TAMAMLANDI.** `dotnet build CompileCheck.csproj` 0 hata; Android,
iOS ve fallback `#if` dallari tek dosyalik ayri bir csproj ile ayri ayri derlendi, ucu
de 0 hata; EditMode kosusu **415/415 PASSED, 0 failed** (14 yeni `NativeFileBridgeTests`
dahil, 401 -> 415, regresyon yok). Hicbir `ProjectSettings` alani ve hicbir manifest
degismedi; **sahne rebuild gerekmiyor** (yeni `[SerializeField]` yok).

Geriye kalan, SENIN YAPMAN GEREKEN dogrulamalar: asagidaki **3** (Windows Play mode --
cihazsiz, tam round-trip), **4** (Android cihaz; on kosul Unity Hub > Android Build
Support), **5** (iOS C# dali), **6** (iOS cihaz; bulut Mac/CI).

---

## Baglam

`EXPORT_IMPORT_PLAN.md` Faz 1-6 bitti: kayit tek parca bir zarfa (`SaveTransferPackage`)
giriyor, dogrulaniyor, sanitize ediliyor ve `HabbitManager.TryImportSave` ile tamamen
geri yukleniyor. Ama **tasima yolu durustce yalnizca PANO** (ayni dosyanin kritik nokta
#11). Yazilan `.json` `Application.persistentDataPath` altinda duruyor; Android 11+'ta
`Android/data/<paket>/files` dosya yoneticilerinden **gorunmez**, iOS'ta uygulama
konteyneri erisilmez. Yani kullanici "Verileri disa aktar"a bastiginda ortaya cikan
dosyaya **ulasamiyor**; elinde yalnizca panoda 40-80 KB bir metin oluyor.

Bu teslim o bosluga sistem dosya seciciyi koyuyor: disa aktarmada **Kaydet-As**
(sistem dosya yoneticisi acilir, kullanici klasoru ve dosya adini secer), ice aktarmada
**Dosya sec**. Pano yolu silinmiyor -- secici yoksa (Editor, cok eski cihaz) ya da
kullanici seciciyi iptal ederse sessiz fallback olarak kaliyor.

**Alinan kararlar:** native kopruyu kendimiz yaziyoruz (ucuncu parti paket yok);
Android + iOS ayni teslimde yaziliyor (iOS **derlenemez**, asagiya bak); disa aktarma
yalnizca Kaydet-As -- `ACTION_SEND`/`UIActivityViewController` paylasim yolu
**yazilmiyor** (Android'de `FileProvider` + `provider_paths.xml` + ozel manifest
gerektiriyor; Kaydet-As hicbirini gerektirmiyor).

### Dogrulama sinirlari -- ONCE BUNU OKU

Elde **Android cihaz var**; Mac/Xcode **yok**. Buna gore uc kademeli dogrulama:

1. **Birincil dogrulama Windows'ta Play mode.** Editor dali gercek
   `EditorUtility.SaveFilePanel`/`OpenFilePanel` diyaloglarini aciyor, yani
   **Kaydet-As + Dosya sec round-trip'i hicbir cihaz olmadan tam olarak
   dogrulanabiliyor.** Plan bu yuzden Editor dalini bir "fallback" degil birinci
   sinif vatandas yapiyor.
2. **Android: gercek cihazda tam dogrulama.** Tek on kosul, Unity Hub'dan
   **Android Build Support + SDK/NDK + OpenJDK** kurmak -- su an
   `Editor/Data/PlaybackEngines/` altinda yalnizca `iOSSupport`, `WebGLSupport`,
   `windowsstandalonesupport` var, yani `.java` dosyasi bugun hic derlenmiyor.
   Modul kurulduktan sonra gercek SAF secici, iptal akislari ve cihazin dosya
   yoneticisinde dosyanin gorunurlugu **cihazda** dogrulanir.
3. **iOS Windows'ta derlenemez** ama **C# tarafi derlenebilir:** `iOSSupport` modulu
   kurulu, build target iOS'a alinirsa Unity `#if UNITY_IOS` dalini derler.
   `DllImport("__Internal")` derleme zamaninda cozulmez (link zamani, cihazda) --
   yani C# tarafi Windows'ta sorunsuz gecer. Derlenemeyen **yalnizca `.mm`'in
   Obj-C'si**; "yazildi, derlenmedi" olarak isaretlenir ve bulut Mac / CI
   adiminda dogrulanir. **iOS kodu bu teslimde tam olarak yaziliyor** -- iOS'a
   cikmak zorunlu, dolayisiyla eksik birakilmiyor.
4. **`#if UNITY_ANDROID && !UNITY_EDITOR` dali aktif build target Standalone iken
   derlenmez** (`PaintPixelsSandbox.cs:33` zaten bu tuzaga deginiyor). Yani
   `dotnet build CompileCheck.csproj` o kodu **gormez** -- hafif kontrol icin
   `UNITY_ANDROID` tanimli ikinci bir CompileCheck kopyasi, asil kontrol target
   degistirip Unity'ye derletmek.

---

## Tasarim

### Tek fikir: native taraf yalnizca DOSYA KOPYALAR

Native koprunun yuzeyi **iki dosya yolu**, hicbir zaman 80 KB'lik bir JSON degil:

- **Disa aktarma:** C# zaten `SaveExportService.Export()` icinde kurtarma dosyasini
  yaziyor. Secici o dosyanin **yolunu** aliyor; Java/Obj-C tarafi
  `ContentResolver.openOutputStream(uri)` ile dosyayi kullanicinin sectigi yere
  **kopyaliyor**. Yeni serialize yok.
- **Ice aktarma:** secici secilen URI'yi `Application.temporaryCachePath/import.json`
  hedefine kopyaliyor ve o yolu C#'a dondururuyor. C# `File.ReadAllText` +
  **mevcut** `SaveTransfer.TryRead` + `HabbitManager.TryImportSave` yolundan geciyor.

Boylece JNI uzerinden buyuk string gecmiyor, kodlama surprizi olmuyor ve Faz 1-6'nin
dogrulama/sanitize zinciri **tek bir satir degismeden** yeniden kullaniliyor.

### Geri cagri kanali

`UnityPlayer.UnitySendMessage("NativeFileBridge", "OnNativeFileResult", payload)` --
iki platformda ayni. Nedeni: `AndroidJavaProxy` geri cagrisi **Java thread'inde**
gelir, Unity API'si cagirilamaz; `UnitySendMessage` ana thread garantili.

Mesaji alan `GameObject`'in adi **tam olarak `NativeFileBridge`** olmali ve
**`DontDestroyOnLoad`** olmali: secici on plandayken uygulama `OnApplicationPause`
yiyor, Ayarlar paneli kapanabilir. Bu, `HabbitManager.RequestReminderPermission`'in
coroutine'i kendi uzerinde kosturma gerekcesinin (`REMINDER_PLAN.md` kritik duzeltme #2)
aynisi. Nesne `NativeFileBridge.Ensure()` ile tembel kuruluyor -- `PainterManager`'in
`DontDestroyOnLoad` singleton kalibi.

Payload formati `"<outcome>|<path>|<displayName>|<error>"`, `TryParseResult` ile
okunuyor -- **saf, testli** (asagi bak).

### Neden manifest / izin / ProjectSettings degisikligi YOK

- `ACTION_CREATE_DOCUMENT` ve `ACTION_OPEN_DOCUMENT` (API 19+, `minSdk` 23 yeterli)
  **hicbir izin istemiyor** ve `FileProvider` gerektirmiyor -- secici URI'yi kendisi
  veriyor.
- `startActivityForResult` sonucunu almak icin `android.app.Fragment` kullaniyoruz
  (AndroidX bagimliligi yok, `UnityPlayerActivity` duz bir `Activity`); fragment
  **manifeste yazilmaz**, calisma aninda ekleniyor.
- iOS `UIDocumentPickerViewController` `UIFileSharingEnabled` /
  `LSSupportsOpeningDocumentsInPlace` istemiyor.
- `androidResizeableActivity: 0` ve `defaultScreenOrientation: 0` **ELLENMEZ**
  (CLAUDE.md'nin acik uyarisi).

---

## Degisecek / eklenecek dosyalar

### 1. YENI `Assets/Plugins/Android/NativeFileBridge.java`

Unity, `Assets/Plugins/Android/` altindaki `.java` dosyalarini Gradle'in
`unityLibrary` modulune ekleyip derler. Sinif adi `com.habittracker.NativeFileBridge`.

- `public static void saveFile(Activity activity, String sourcePath, String fileName)`
  -> `ACTION_CREATE_DOCUMENT`, `setType("application/json")`,
  `EXTRA_TITLE = fileName`, `CATEGORY_OPENABLE`.
- `public static void pickFile(Activity activity, String destPath)`
  -> `ACTION_OPEN_DOCUMENT`, `setType("*/*")` +
  `EXTRA_MIME_TYPES {"application/json","text/plain","application/octet-stream"}`.
  **`*/*` bilincli:** bircok dosya yoneticisi kati `application/json` altinda `.json`
  dosyasini gostermiyor. `ActivityNotFoundException` halinde `ACTION_GET_CONTENT`
  fallback'i.
- Iki cagri da gorunmez bir `Fragment` ekleyip `startActivityForResult` cagiriyor;
  `onActivityResult` akislari kopyaliyor, fragment'i kaldiriyor ve
  `UnitySendMessage` ile sonucu yolluyor. Her sey try/catch, her cikisa tek bir
  mesaj dusuyor (iptal dahil -- `RESULT_CANCELED` -> `cancel`).
- Gorunen ad: `ContentResolver.query` + `OpenableColumns.DISPLAY_NAME`
  (AndroidX `DocumentFile` yok).

### 2. YENI `Assets/Plugins/iOS/NativeFileBridge.mm`

`UIDocumentPickerViewController` + bir delegate singleton:

- `_HabitNativeSaveFile(const char* sourcePath, const char* fileName)` --
  iOS 14+ `initForExportingURLs:asCopy:YES`, altinda
  `initWithURL:inMode:UIDocumentPickerModeExportToService`. Dosya once
  `NSTemporaryDirectory()` altina `fileName` adiyla kopyalanir (secicide gorunen ad
  dosya adi oldugu icin).
- `_HabitNativePickFile(const char* destPath)` -- iOS 14+
  `initForOpeningContentTypes:` (`UTTypeJSON`, `UTTypeText`), altinda
  `initWithDocumentTypes:@[@"public.json", @"public.text"] inMode:Import`.
- `didPickDocumentsAtURLs:` kopyalayip `UnitySendMessage(...)`;
  `documentPickerWasCancelled:` -> `cancel`.
- `extern "C" void UnitySendMessage(const char*, const char*, const char*);`

### 3. YENI `Assets/Scripts/Settings/NativeFileBridge.cs` (~230 satir)

Tek C# yuzeyi. `Scripts/` altinda, cunku test assembly'si Editor assembly'sini
gormuyor -- saf parcalar burada olmak zorunda.

```csharp
public enum NativeFileOutcome { Ok, Cancelled, Unavailable, Error }
public struct NativeFileResult { Outcome; Path; DisplayName; Error; }

public static bool IsAvailable { get; }
public static bool IsBusy { get; }
public static void SaveFile(string sourcePath, string suggestedName, Action<NativeFileResult> done);
public static void PickFile(string destPath, Action<NativeFileResult> done);

// SAF cekirdek -- testlerin dokundugu kisim:
public static bool TryParseResult(string payload, out NativeFileResult result);
public static string SanitizeFileName(string name);
```

Kurallar:

- **Tek bekleyen cagri.** Ikinci bir `SaveFile`/`PickFile` bekleyen varken
  `Unavailable` ile reddedilir -- `SettingsScreen.reminderPermissionPending`'in
  "OS diyalogu acikken ikinci akis baslamaz" kuralinin aynisi.
- **Geri cagri tam olarak bir kez.** Alan **cagirmadan once** temizlenir.
- Platform dallari: `#if UNITY_ANDROID && !UNITY_EDITOR` (AndroidJavaClass) /
  `#elif UNITY_IOS && !UNITY_EDITOR` (`DllImport("__Internal")`) /
  `#elif UNITY_EDITOR` (`EditorUtility.SaveFilePanel` / `OpenFilePanel` + `File.Copy`,
  ayni geri cagriyla **senkron** tamamlanir) / `#else` -> `Unavailable`.
  `#if UNITY_EDITOR` runtime assembly'de zaten kullanilan bir kalip
  (`UI/MaxHeightFitter.cs:59`, `UI/CanvasScalerDriver.cs:24`).
- Mesaj alici `MonoBehaviour` ayni dosyada `internal` bir sinif
  (`NativeFileBridgeReceiver`), `OnNativeFileResult(string payload)`.

### 4. `Assets/Scripts/Settings/SaveExportService.cs` -- GENISLETILIR, bozulmaz

Mevcut `Export` / `ReadClipboard` / `TryReadNewestBackup` **aynen kaliyor** (rezerve
public API kurali + `DevSaveTransferTools` onlari cagiriyor). Eklenenler:

- `ExportResult`'a `public string FilePath;` -- `TryWriteBackupFile` zaten tam yolu
  biliyor, yalnizca disa vermiyor. Kaydet-As bu yolu seciciye veriyor.
- `public static string ImportStagingPath()` --
  `Path.Combine(Application.temporaryCachePath, "import.json")`.
  `persistentDataPath` DEGIL: gecici, prune'lanan yedek klasorune karismiyor.
- `public static bool TryReadFile(string path, out string json, out string error)` --
  `TryReadNewestBackup`'in tek-dosya kardesi, ayni try/catch + Turkce hata kalibi.

### 5. YENI `Assets/Scripts/UI/Settings/SettingsScreen.Data.cs` (partial)

`SettingsScreen.cs` bugun **668 satir** (500 butcesinin uzerinde). `HandleExport`,
`HandleImport`, `HandleImportConfirmed` ana dosyadan **tasiniyor** ve secici akislari
buraya ekleniyor -- `HabbitManager.Rotus.cs` / `.Archive.cs` / `.Import.cs` ile ayni
partial gerekcesi.

- `HandleExport()` -- premium kapisi **aynen kalir**. `SaveExportService.Export(manager)`
  (pano + kurtarma kopyasi, degismedi) -> `NativeFileBridge.IsAvailable && FilePath != null`
  ise `SaveFile(FilePath, FileName, HandleSaveFileResult)`, degilse **bugunun kutusu
  aynen**.
- `HandleSaveFileResult(result)`:
  - `Ok` -> `"Yedek kaydedildi"` + secilen dosya adi + boyut
  - `Cancelled` -> `"Yedek panoya kopyalandı"` (pano yazimi gercekten oldu --
    **basarisizlik degil**, metin de oyle demiyor)
  - `Unavailable`/`Error` -> bugunun "cihazda bir kurtarma kopyasi saklandi" metni
- `HandleImport()` -- `NativeFileBridge.IsAvailable` ise
  `PickFile(ImportStagingPath(), HandlePickFileResult)`; degilse **bugunun pano yolu
  aynen**.
- `HandlePickFileResult(result)`: `Ok` -> `TryReadFile` -> `SaveTransfer.TryRead` ->
  `pendingImportJson` -> `ConfirmSheet.Show("Yedeği geri yükle", Describe(...) + ...,
  "Geri yükle", HandleImportConfirmed)`. **Bu zincirin tamami bugunku `HandleImport`'un
  govdesi** -- yalnizca JSON'un kaynagi pano yerine dosya.
  `Cancelled` -> sessiz. `Error` -> bilgi kutusu.
- `pendingImportJson`'un anlami degismiyor: **onaylanan sey okunan sey olmali.**
- Yeni `private bool nativePickerPending` -- `reminderPermissionPending` ile ayni rol.
- Her iki geri cagri `if (this == null || manager == null) return;` ile basliyor;
  ayrica panel geri cagri gelmeden kapanmissa (`!gameObject.activeInHierarchy`)
  **ConfirmSheet acilmaz, islem sessizce iptal edilir** -- import onay oncesi
  iptal edildiginde hicbir sey yazilmamis olur, zararsiz.
- `confirmSheet == null` fallback'i degismiyor: **import onaysiz calistirilmaz.**

### 6. `Assets/Scripts/Dev/DevSaveTransferTools.cs` + `Assets/Editor/Dev/DevToolsEditor.Rotus.cs` kalibinda yeni bir bolum

Iki komut: `ExportViaPicker(manager)`, `ImportViaPicker(manager, done)`. **Dev katmani
kural sahibi degil** -- ikisi de `SaveExportService` / `NativeFileBridge` /
`manager.TryImportSave` uretim yolundan geciyor. Editor'de bunlar gercek Windows dosya
diyaloglarini acar, yani cihazsiz round-trip buradan test edilir.

### 7. YENI `Assets/Tests/NativeFileBridgeTests.cs`

Native/`MonoBehaviour` kismi test edilemez (`HabbitManager` ile ayni sinir), **saf
cekirdek** edilir -- "pure core, thin wrapper" kurali:

- `TryParseResult_Ok_ParsesPathAndDisplayName`
- `TryParseResult_Cancel_YieldsCancelled`
- `TryParseResult_Error_CarriesMessage`
- `TryParseResult_Garbage_IsRejected` (`null`, `""`, `"ok"`, ayrac fazlasi/eksigi)
- `TryParseResult_PathWithSeparatorChar_IsNotTruncated` (yol icinde `|` -- ayrac
  sayisi sabit, son alan ham birakilir)
- `SanitizeFileName_StripsIllegalCharacters`, `SanitizeFileName_EmptyFallsBackToDefault`
- `SanitizeFileName_KeepsJsonExtension`

### 8. `CLAUDE.md` + `EXPORT_IMPORT_PLAN.md`

- `UI/Settings/*` bolumune `NativeFileBridge.cs`; `SaveExportService` artik pano+dosya
  degil **pano + kurtarma kopyasi + sistem secici**.
- Yeni invariant blogu: native yuzey yalnizca dosya yolu tasir (buyuk string JNI'den
  gecmez); geri cagri `UnitySendMessage` ile ana thread'e gelir ve alici
  `DontDestroyOnLoad` olmak **zorundadir** (secici on plandayken panel kapanabilir);
  tek bekleyen cagri kurali; `ACTION_CREATE_DOCUMENT`/`ACTION_OPEN_DOCUMENT` bilincli
  secim -- manifest/izin/`FileProvider` gerektirmiyor, paylasim (`ACTION_SEND`)
  gerektirirdi; `*/*` mime'i bilincli.
- Assemblies bolumu: `Assets/Plugins/` hicbir asmdef'e ait degil (native eklentiler);
  `.java`/`.mm` **`dotnet build CompileCheck.csproj` tarafindan derlenmez**.
- Test dosyasi listesi 17 -> 18.
- `EXPORT_IMPORT_PLAN.md` Faz 7 "ayri teslim" notu -> bu dosyaya referans.

---

## Riskler ve tuzaklar

- **`UnitySendMessage` nesne adina gore bulur.** `GameObject`'in adi
  `"NativeFileBridge"` olmazsa mesaj **sessizce** kaybolur ve geri cagri hic gelmez
  (secici kapanir, hicbir sey olmaz). Ad hem Java hem `.mm` hem C# tarafinda sabit
  bir `const`.
- **Android dali Standalone target'ta derlenmez.** `dotnet build CompileCheck.csproj`
  o kodu gormuyor. Hafif kontrol: `CompileCheck.csproj`'un bir kopyasina
  `<DefineConstants>UNITY_ANDROID</DefineConstants>` ekleyip bir kez derle; asil
  dogrulama Android modulu kurulduktan sonra target'i Android'e alip Unity'nin
  derlemesi.
- **`.java` dosyasi yalnizca Android build sirasinda derlenir** -- bir yazim hatasi
  Editor'de hic gorunmez, Gradle hatasi olarak cikar.
- **iOS'un Obj-C'si bu makinede derlenemez.** C# dali derlenir (target iOS), `.mm`
  derlenmez. Bulut Mac / CI'daki ilk derlemede Obj-C duzeltmesi gerekebilir --
  bu beklenen bir adim, bir aksilik degil. `.mm` dosyasinin `.meta`'si **yalnizca
  iOS platformunda dahil** olacak sekilde ayarlanmali, yoksa baska platformlarin
  derlemesine girmeye kalkar.
- **OEM dosya yoneticisi farkliliklari.** Bazi Samsung/Xiaomi kabuklari SAF
  secicide kati mime tiplerini farkli yorumluyor -- `*/*` + `EXTRA_MIME_TYPES`
  secimi tam olarak bunu hedefliyor. Elde olan cihazda dogrulanir; baska bir
  kabukta davranis farkliysa duzeltme noktasi tek: Java'daki mime satiri.
- **Secici acikken gun degisebilir.** Dondukten once `OnApplicationPause(false)` ->
  `RefreshDayIfNeeded()` kosar, **sonra** geri cagri gelir. Import bundan sonra
  `ReplaceWith` yaptigi icin cakisma yok; sira dogru. (Yeni kod gerekmiyor, yalniz
  yoruma yazilir.)
- **Kullanici seciciyi iptal ederse export basarisiz sayilmamali** -- pano + kurtarma
  kopyasi gerceklesti. Metin bunu dogru anlatmak zorunda.
- **Gorunen ad guvenilmez.** Secicinin dondurdugu `DISPLAY_NAME` null/garip olabilir;
  `SanitizeFileName` tek gecis noktasi, bos ise `HabitTracker_yedek.json`.
- **UTF-8 BOM.** Kullaniciya gorunen diakritikli metinler
  (`"Yedek kaydedildi"`, `"İçe aktarılamadı"`) -> `SettingsScreen.Data.cs`,
  `SaveExportService.cs` **BOM'lu UTF-8**, **PowerShell ile DUZENLENMEZ**
  (Edit/Write kullan). Yorumlar ASCII-only Turkce.
- **Secilen dosya bir HabitTracker yedegi olmayabilir** -- secici `*/*` gosteriyor.
  Faz 1'in `TryRead`'i tam olarak bunu yakaliyor, yeni kod gerekmiyor.
- **Bekleyen cagri takilirsa oturum boyunca kilitlenir** (hem
  `NativeFileBridge.IsBusy` hem `nativePickerPending` acik kalir). Java tarafi her
  cikis yolunda (ok/iptal/hata, `try/finally`) tam bir mesaj yolluyor; geri kalan tek
  delik Activity'nin onActivityResult'a hic ulasmadan yeniden kurulmasi. Surec olumunde
  C#'in statigi da sifirlandigi icin bekleyen cagri kalmiyor; surec olmeden yeniden
  kurulma ise portre kilidi + `androidResizeableActivity: 0` ile pratikte olusmuyor
  (olusursa yeniden kurulan parca `intent == null` gorup hata yolluyor).
  **Kabul edilen sinir** -- spekulatif bir zaman asimi makinesi eklenmedi.

---

## Uygulama sirasi

1. `NativeFileBridge.cs` -- saf cekirdek (`TryParseResult`, `SanitizeFileName`) +
   Editor dali + `#if` iskeleti. `NativeFileBridgeTests.cs`. **Burada her sey
   cihazsiz test edilebilir.**
2. `SaveExportService.cs` genislemesi (`FilePath`, `ImportStagingPath`, `TryReadFile`).
3. `SettingsScreen.Data.cs` partial'i -- tasima + secici akislari.
4. `DevSaveTransferTools` + `DevToolsEditor` bolumu.
5. `NativeFileBridge.java`.
6. `NativeFileBridge.mm`.
7. `CLAUDE.md` + `EXPORT_IMPORT_PLAN.md`.

Sahne rebuild **gerekmiyor** -- yeni `[SerializeField]` yok, yeni UI satiri yok
(mevcut iki satirin davranisi degisiyor). `SettingsPanelBuilder` dokunulmuyor.

---

## Dogrulama

**1. Derleme (her adimdan sonra)**
```powershell
dotnet build CompileCheck.csproj
```
0 hata. (Android/iOS dallarini **gormez** -- yukaridaki risk.)

**2. EditMode testleri** (Editor kapali, `Logs/results.xml` once silinir -- bayat kalabiliyor)
```powershell
Remove-Item .\Logs\results.xml -ErrorAction SilentlyContinue
& "C:\Program Files\Unity\Hub\Editor\6000.0.65f1\Editor\Unity.exe" -runTests -batchmode `
  -projectPath . -testPlatform EditMode -testResults .\Logs\results.xml
```
Beklenen: 401 + yeni `NativeFileBridgeTests` hepsi yesil, regresyon yok.

**3. Play mode, Windows -- TAM ROUND-TRIP, cihaz gerekmez**
Editor dali gercek Windows dosya diyaloglarini aciyor:
- Ayarlar > Premium'u ac > **Verileri disa aktar** -> **Windows "Kaydet" diyalogu
  acilir** -> Masaustune kaydet -> kutuda "Yedek kaydedildi" + dosya adi.
  Dosyayi bir metin editorunde ac: `"format":"habittracker.save"` govdesi gorunmeli.
- Ayni diyalogda **Iptal** -> kutu "Yedek panoya kopyalandı" demeli, hata **dememeli**.
- Dev > **Ilerlemeyi sifirla**.
- **Verileri ice aktar** -> **Windows "Ac" diyalogu** -> masaustundeki dosyayi sec ->
  ozet kutusu -> "Geri yükle" -> aliskanliklar, seriler, tablo ilerlemeleri, galeri,
  delikler, rotus, ayarlar, ressam modu **geri gelir**; premium **kapanmamis** olur.
- Iptal -> hicbir sey olmaz, hicbir kutu acilmaz.
- Yabanci bir `.json` (ornegin `Packages/manifest.json`) sec -> **"İçe aktarılamadı"**
  kutusu, mevcut veri **bozulmamis**.

**4. Android cihaz (On kosul: Unity Hub > Android Build Support + SDK/NDK + OpenJDK)**
- Build target'i Android'e al; Unity'nin `#if UNITY_ANDROID` dalini derlemesini bekle
  (konsol 0 hata).
- APK kur. Disa aktar -> **sistem "Dosya kaydet" ekrani** acilir (Indirilenler/Drive
  secilebilir) -> kaydet -> cihazin dosya yoneticisinde dosya **gorunur**.
- Sifirla -> Ice aktar -> **sistem dosya secici** -> ayni dosya -> veri geri gelir.
- Secici acikken uygulamayi arka plana al/geri don -> geri cagri **kayboluyor mu**
  diye bak (adb logcat'te `NativeFileBridge` satirlari).
- Her iki akista iptal -> uygulama takilmamali, ikinci deneme calismali.

**5. iOS C# dali (Windows'ta yapilabilir)**
Build target'i iOS'a al, Unity'nin script derlemesini bekle -> konsol 0 hata.
`DllImport("__Internal")` link zamaninda cozuldugu icin bu adim gecer; **`.mm`'in
Obj-C'si derlenmez.** `.mm`'in `.meta`'sinda yalnizca iOS platformu isaretli olmali.

**6. iOS cihaz** -- bulut Mac / macOS CI uzerinden; bu teslimin dogrulama kapsaminda
**degil**, ayri bir adim (secenekler konusmada anlatildi). Kod bu teslimde tam
yazilir, Obj-C duzeltmesi ilk gercek derlemede yapilir.
