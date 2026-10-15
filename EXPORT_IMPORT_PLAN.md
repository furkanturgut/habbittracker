# Verileri disa/ice aktarma (yedekle & geri yukle)

**Durum:** Faz 1-6 kod tarafi + otomatik dogrulama TAMAMLANDI -- `dotnet build CompileCheck.csproj`
her fazdan sonra 0 hata, EditMode test kosusu **401/401 PASSED, 0 failed** (19 yeni
`SaveTransferTests` dahil). Geriye yalnizca Faz 6'nin Editor-ici adimlari kaliyor (sahne rebuild +
Play mode dogrulamasi -- SENIN YAPMAN GEREKEN). Faz 7 (gercek dosya secici yolu) ayri bir teslim
olarak YAZILDI -- bkz. `NATIVE_FILE_TRANSFER_PLAN.md` ve asagidaki Faz 7 bolumu.

---

## Baglam

Ayarlar > "Veri" kartindaki **"Verileri disa aktar"** satiri bugun tamamen bir iskelet:
`SaveExportService.Export()` 10 satirlik bir stub, yalnizca `Debug.Log` atiyor
(`Assets/Scripts/Settings/SaveExportService.cs:6`). Ice aktarmanin ise hic karsiligi yok --
ne servis, ne satir, ne veri yolu. Kullanicinin aylarca birikmis serisi, galerisi ve
tablolari **tek bir PlayerPrefs anahtarinda** (`habit_save_v1`) duruyor; telefon
degistirmek ya da uygulamayi silmek her seyi goturuyor.

Bu is iki soruyu bitiriyor: veri gercekten **disa cikiyor** ve gercekten **geri
yukleniyor**.

### Altyapi bu ise uygun mu? -- Evet, bir kac istisna disinda hazir

| Gereken | Durum |
|---|---|
| Kaydin tamamini tek parca serialize etmek | **Hazir.** Tum kayit tek `PlayerSaveData` -> `JsonUtility.ToJson` (`HabitSaveSystem.FlushIfDirty:82`). Export icin ayri bir DTO gerekmiyor. |
| Depoyu soyutlamak | **Hazir.** `IHabitStorage` (`Load/Write/Flush/Delete`); dosya basindaki yorum bu genislemeyi zaten ongormus. |
| Guvenilmez JSON'a dayanikli yukleme | **Buyuk olcude hazir.** `HabitSaveSystem.Load()` try/catch + `RepairHabits()` (null listeler, `canvasSize<=0`, `rotusProgress` clamp) + `RepairSettings()` (bos ad, gecersiz saat, ozel kategoriler, filtre token'lari). Yorumlari acikca "elle duzenlenmis kayit"i hedefliyor. |
| Bilinmeyen tablo id'si onarimi | **Hazir.** `PaintingAssignment.RepairAll` cozulmeyen `activePaintingId`'ye yenisini atiyor; `GalleryWorks.Collect` cozulmeyen galeri id'lerini atliyor. |
| Yinelenen/bos alaskanlik id'si | **Hazir.** `RebuildHabitIndex` (`HabbitManager.cs:594`) bos ya da yinelenen id'ye yeni `Guid` yaziyor. |
| Yikici islemin onayi | **Hazir.** `ConfirmSheet.Show(title, body, confirmText, Action)` -- CLAUDE.md zaten "bilerek yeniden kullanilabilir" diyor. |
| Yikici islemden sonra oturumu tazeleme | **Hazir ornek var.** `DeleteAllData()` (`HabbitManager.cs:261-297`) tam olarak import'un ihtiyaci olan sirayi uyguluyor. |
| Saf mantigin test edilebilirligi | **Hazir.** `HabitTracker.Tests` Editor-only, `MemoryStorage : IHabitStorage` kalibi her test dosyasinda var, repair testleri ham JSON literal'leriyle yazilmis. |

**Eksik olan tam olarak su dort sey:**

1. **Runtime'da hic dosya I/O'su yok.** `Assets/Scripts` + `Assets/Tests` altinda tek bir
   `System.IO` / `File.` / `Application.persistentDataPath` / `AndroidJavaObject` /
   `GUIUtility.systemCopyBuffer` kullanimi yok -- yalnizca `IHabitStorage.cs:2`'de bir
   yorum. `System.IO` sadece Editor kodunda (sprite importer'lar). Yani tasima katmani
   sifirdan yazilacak.
2. **Pakette dosya secici / paylasim paketi yok.** `Packages/manifest.json`'da
   NativeShare benzeri hicbir sey yok; `com.unity.modules.androidjni` var (JNI mumkun)
   ama bu Faz 7'ye biraktigimiz is.
3. **`HabbitManager.saveSystem` private ve `Awake`'te sabit kurulu** (`:34`, `:114`) --
   storage disaridan enjekte edilemiyor. Import'un yeni bir public giris noktasina
   ihtiyaci var.
4. **`Load()` hicbir olay yaymiyor ve hicbir `PaintingView` birakmiyor** -- `Awake`'ten
   cagrildigi icin aboneler henuz yok. Oturum ortasinda calisan bir import bunlari
   kendisi yapmak zorunda.

---

## Alinan kararlar (sabit)

| Karar | Secim |
|---|---|
| Tasima yolu (Faz 1) | **Pano (clipboard) + yerel kurtarma dosyasi.** Native paylasim / dosya secici Faz 7'ye planlandi, Faz 1'de yazilmiyor. |
| Ice aktarma semantigi | **Tamamen degistir.** Mevcut kayit silinir, paket aynen yazilir. Birlestirme yok. |
| Paket kapsami | **Tum kayit, `isPremium` HARIC.** |
| Zarf (envelope) | `PlayerSaveData`'yi saran kendi surumlu basligi olan bir ust nesne (`SaveTransferPackage`). |
| Export premium kapisi | **Degismiyor** -- bugunku `HandleExport` premium kontrolu aynen kaliyor. |
| Import premium kapisi | **YOK, ucretsiz.** Gerekce asagida. |

### Neden `isPremium` pakete girmiyor

Bugun `PremiumService.Purchase` bir hook; `isPremium` zaten elle duzenlenebilir bir
kayit alani. Ama gercek IAP geldigi an **paylasilan bir yedek dosyasi bedava-premium
dagitim yoluna doner** -- yedek dosyasi tanim geregi kullanicidan kullaniciya gider.
Premium makbuzdan geri yuklenir, yedekten degil. Dolayisiyla:

- **Export:** paketi uretirken alan silinir.
- **Import:** pakettekine bakilmaz, **cihazdaki mevcut deger korunur** (`false`'a
  zorlanmaz da -- odeme yapmis bir kullanici kendi yedegini geri yukleyince premium'unu
  kaybetmemeli).

### Neden import ucretsiz, export premium

Yedek **uretmeyi** paralama yaygin ve mesru bir kalip. Yedek **geri yuklemeyi** paralamak
ise kilitleme uretir: kullanici odedi, export aldi, telefonu degistirdi, makbuz henuz
geri yuklenmedi -> kendi verisine erisemez. Kurtarma yolu hicbir zaman odeme arkasinda
olmamali. (Tersini istersen tek satir: `HandleImport`'un basina `HandleExport`'un
`isPremium` kontrolunun aynisi + builder'da `NavRow(..., premiumBadge: true)`.)

### Ikinci tur incelemede bulunan kritik noktalar

1. **`JsonUtility.FromJsonOverwrite` YANLIS JSON'a karsi korumasiz.** `Load()`'un
   try/catch'i yalnizca **bozuk** JSON'u yakalar. `"{}"` ya da baska bir uygulamanin
   JSON'u gecerli bir nesnedir: hicbir alan tanimadigi icin hicbir sey yazmaz, sonuc
   **bos ama gecerli bir kayit** olur -- yani sessiz veri kaybi. Zarftaki
   `format`/`formatVersion` alanlari tam olarak bunun icin var ve dogrulama
   **depoya hicbir sey yazilmadan once** calismak zorunda.
2. **Export, elle yazilmis bir alan listesi OLMAMALI.** `SaveTransfer.Build`
   `JsonUtility.FromJson<PlayerSaveData>(JsonUtility.ToJson(data))` ile derin kopya
   alip yalnizca `isPremium`'u siler. Alan alan kopyalayan bir kod, kayda eklenen her
   yeni alanda sessizce yedeklenmeyi birakir -- bu projede son bir yilda `missedPixels`,
   `galleryHoles`, `settings`, `lastCelebratedTier/Date`, `rotusProgress`, `isArchived`
   tam olarak boyle eklendi.
3. **Derin kopya SART, yerinde redaksiyon degil.** `data.settings.isPremium = false`
   dogrudan yazilirsa **export almak kullanicinin premium'unu silerdi.** Testle
   kazikliyoruz (`Build_DoesNotMutateSource`).
4. **`ReplaceWith` once `ResetToEmpty()` cagirmali.** `JsonUtility.ToJson` **bos
   listeleri yazmaz**; gelen pakette `habits` bossa anahtar hic olmaz ve
   `FromJsonOverwrite` mevcut `habits` listesine dokunmaz -> **5 aliskanligi olan bir
   cihaza bos bir yedek yuklemek hicbir seyi silmez.** Ayni tuzak `customCategories`,
   `filterTokens`, `missedPixels`, `galleryHoles`, `randomOpenedPixels`,
   `habitGallery`, `frequency` icin de gecerli.
5. **`Load()`'un gun hattı ikinci bir kopyaya cikmamali.** CLAUDE.md: "Mood must be
   settled before the daily reset... Swapping the two silently turns every closed day
   into a penalty." Import ayni sirayi (settle mood -> settle missed pieces -> daily
   reset -> RepairAll) tekrar yazarsa o degismez iki yerde yasamaya baslar. Cozum:
   `Load()`'un `saveSystem.Load()` SONRASI govdesi `ApplyLoadedSave(now)` olarak
   ayriliyor, iki yol da onu cagiriyor.
6. **`pendingMoodSteps` import'ta ANINDA harcanmali.** Acilista `Start()` harciyor
   (`:134-156`); oturum ortasinda `Start()` bir daha calismaz, yani yazilan ceza
   sonsuza kadar askida kalir. Import `RestoreMoodPoint`'ten hemen sonra
   `ApplyMood(-pendingMoodSteps)` + sifirlama yapiyor.
7. **`BurnedPaintingCache.Clear()` ZORUNLU.** Onbellek `habitId`'ye gore anahtarli ve
   gecersizlemesi yalnizca `missedPixels` imzasina bakiyor (`SignatureOf:225`). Import
   ayni id'lerle farkli bir icerik getirebilir -> bayat yanmis sprite. Bugun `Clear()`
   yalnizca `OnDestroy`'dan cagriliyor; `DeleteAllData` bile cagirmiyor.
8. **`PaintingView`'lar KAYITTAN ONCE birakilmali.** `DeleteAllData`'nin kendi yorumu
   (`:258-260`) bunu soyluyor: birakilmazsa artik var olmayan aliskanliklara bagli
   kalip 12'lik havuzu tuketirler.
9. **Parca indeksi aralik kontrolu hicbir yerde yok.** `RepairHabits` null listeleri ve
   iki sayiyi onariyor ama `randomOpenedPixels` / `missedPixels` icindeki
   `canvasSize` disi indeksleri, iki listede ayni anda gecen indeksi ya da
   `totalPixelsPainted > canvasSize` durumunu kontrol etmiyor. Bugun bu bir sorun
   degil (kaydi yalnizca uygulama yaziyor); import'tan sonra guvenilmez veri bir
   **ozellik** oldugu icin yeni bir `SaveTransfer.Sanitize` pass'i gerekiyor.
10. **Gelecek tarihli yedek.** Iki cihazin saat dilimi/saati farkli olabilir. Import
    edilen `lastLoginDate` bugunden ileri ise `DailyResetService`'in
    "saat geri alindi" korumasi devreye girer ve kayit **kalici olarak donar**
    (`isCompletedToday` hic temizlenmez). `Sanitize` gelecek tarihleri bugune
    kirpiyor -- o koruma asil isi (kullanicinin cihaz saatini oynatmasi) icin yerinde
    kaliyor.
11. **Faz 1'in tasima yolu durustce yalnizca PANO.** Android 11+'ta
    `Android/data/<paket>/files` dosya yoneticilerinden **gorulmez**; iOS'ta uygulama
    konteyneri `UIFileSharingEnabled` olmadan erisilmez. Yani yazilan `.json`
    **Faz 1'de kullaniciya ulasmaz** -- yerel bir kurtarma kopyasi ve Faz 7'nin
    paylasacagi dosyadir. UI bu yuzden dosya listesi gostermiyor; metin de dosyayi
    "cihazda saklandi" diye anlatiyor, "gonderebilirsin" diye degil.
12. **Panoya 80 KB metin TMP'ye BASILMAMALI.** Yapistirma alani yok: import panoyu
    `GUIUtility.systemCopyBuffer` ile **dogrudan okuyor**, hicbir yerde
    render etmiyor. Projedeki her `TMP_InputField` zaten `SingleLine` +
    `characterLimit` ile kurulu; 80.000 karakterlik bir alan hem o kalibi bozar hem
    gorunur bir takilma uretir.

---

## Tasarim

### Hangi bilgiler aktarilacak

Zarf -> `data` (`PlayerSaveData`), asagidaki **her sey**. Secilmis bir alt kume degil:
derin kopya + tek bir acik redaksiyon.

**`PlayerSaveData` (kok)**
- `saveVersion` -- tasinir (migrasyon capasi, import'ta surum kapisi olarak okunur)
- `lastLoginDate` -- tasinir (`Sanitize` gelecekse bugune kirpar)
- `habits[]` -- tamami, asagida
- `painterMoodPoint`, `lastMoodSettledDate` -- tasinir (ressamin ruh hali ve gun damgasi)
- `rotusCount` -- tasinir (global rotus stogu)
- `settings` -- `isPremium` HARIC tamami: `painterName`, `reminderOn`, `reminderTime`,
  `soundOn`, `hapticsOn`, `weekStart`, `customCategories`, `filterTokens`

**`HabitData` (her aliskanlik icin)**
- Kimlik/kural: `id`, `title`, `categoryGroup`, `habitType`, `frequency`, `hasEndDate`,
  `startDate`, `endDate`
- Takip: `isCompletedToday`, `currentStreak`, `longestStreak`, `lastCompletedDate`
- Kutlama damgasi: `lastCelebratedTier`, `lastCelebratedDate`
- Rotus dongusu: `rotusProgress`
- Arsiv: `isArchived`
- Tablo: `activePaintingId`, `canvasSize`, `totalPixelsPainted`, `randomOpenedPixels`,
  `habitGallery`, `awaitingPaintingChoice`, `missedPixels`, `galleryHoles[]`
  (`paintingId` + `pieces`)

**Tasinmayanlar ve gerekceleri**
- `settings.isPremium` -- yukaridaki karar.
- `AppClock` gun kaydirmasi -- kayitta degil, yalnizca dev modu; dogru davranis.
- `BurnedPaintingCache`, `PaintingViewPool`, `AppSettings`/`RotusState` aynalari --
  turetilmis runtime durumu, kayittan yeniden kuruluyor.
- Tablo **gorselleri** -- `PaintingDefinition.id` tasiniyor, PNG'ler tasinmiyor. Iki
  kurulumun sanat seti farkliysa: `PaintingAssignment.RepairAll` aktif tabloya yenisini
  atar (o tablonun ilerlemesi gider), `GalleryWorks.Collect` cozulmeyen galeri
  girdilerini atlar. **Kabul edilen sinir** ve `Describe` ozetinde uyari olarak
  gosterilecek; tek yan etkisi `GalleryWorks.FinishedCount`'in (ki `habitGallery.Count`
  okur) ekranda gorunen karo sayisindan fazla cikmasi.

### Zarf formati

```jsonc
{
  "format": "habittracker.save",   // yabanci JSON'u ayirt eden tek sey
  "formatVersion": 1,              // ZARFIN surumu
  "exportedAt": "2026-10-14 18:35",
  "appVersion": "0.1",
  "habitCount": 7,                 // insan tarafi + kesik-pano tespiti
  "data": { /* PlayerSaveData, isPremium'suz */ }
}
```

`formatVersion` ve `data.saveVersion` **bagimsiz** iki kapi: biri zarfin, oteki kaydin
surumu, ayri hareket edebilirler. Ikisi de "elimdekinden yeni" ise paket reddedilir.

---

## Faz 1 -- Saf mantik + testler (sahne yok, paket yok)

Hepsi `Assets/Scripts/` altinda, `MonoBehaviour`'dan bagimsiz -- test assembly'si
Editor assembly'sini gormuyor, mantik orada olmak zorunda.

- [x] YENI `Assets/Scripts/Settings/SaveTransfer.cs` (~250 satir, butce icinde):
  - `[Serializable] public class SaveTransferPackage` -- yukaridaki alanlar +
    `public const string FormatId`, `public const int CurrentFormatVersion = 1`.
    **Kayit formatinin parcasi DEGIL**, dosya yorumunda bu acikca yaziyor.
  - `public static string Build(PlayerSaveData data, DateTime now, string appVersion)`
    -- derin kopya (`FromJson<PlayerSaveData>(ToJson(data))`) -> `settings.isPremium`
    temizle -> zarfi doldur -> `JsonUtility.ToJson`. Kaynagi **asla** degistirmez.
  - `public static bool TryRead(string json, out SaveTransferPackage package, out string error)`
    -- null/bos; `FromJson<SaveTransferPackage>` try/catch; `package == null`;
    `format` esitligi; `formatVersion <= CurrentFormatVersion`; `data != null`;
    `data.saveVersion <= PlayerSaveData.CurrentSaveVersion`; `habitCount` ile
    `data.habits.Count` uyumu. Her hata **kullaniciya gosterilecek Turkce bir cumle**
    dondurur ("Bu metin bir HabitTracker yedegi degil.", "Yedek daha yeni bir
    surumden -- uygulamayi guncelle.", "Yedek eksik gorunuyor." ...).
  - `public static void Sanitize(PlayerSaveData data, DateTime now)` -- `RepairHabits`'in
    yapmadiklari: parca indekslerini `[0, canvasSize)` disinda ve yinelenen olanlari at;
    ayni indeks hem acik hem yanik ise **acik kazanir** (`PaintingAssignment.Resize`'in
    "kullanicinin boyadigi pikseller delikler icin feda edilmez" kuralinin aynisi);
    `totalPixelsPainted` ve tuketilen toplami `canvasSize`'a kirp; `currentStreak`/
    `longestStreak` negatiflerini sifirla ve `longestStreak >= currentStreak` yap;
    `frequency`'deki 0..6 disi degerleri at; `lastLoginDate` /
    `lastCompletedDate` / `lastCelebratedDate` gelecekteyse bugune kirp.
    **`RepairHabits`'e TASINMIYOR:** o her acilista calisan sicak yol ve bugun
    guvenilir bir kaynaktan besleniyor; import'un sert pass'i ayri durarak 300 yesil
    testin acilis davranisini hic degistirmiyor. (Ileride `RepairHabits`'e tasima
    adayi -- plan dosyasinda takip isi olarak kalir.)
  - `public static string Describe(SaveTransferPackage package, PaintingLibrary library)`
    -- onay kutusunda gosterilecek ozet: "7 aliskanlik, 12 eser, 1 rotus -- 14 Eki 2026
    tarihli yedek". Kutuphanede cozulemeyen tablo id'si varsa ikinci bir uyari satiri
    ekler. `DateLabels.TryFormat` yeniden kullaniliyor.
- [x] `Assets/Scripts/Save/HabitSaveSystem.cs`:
  - `Load()`'un govdesi bolunuyor: `private void ApplyJson(string json)` (mevcut
    try/catch + `habits` null kontrolu + `RepairHabits()` + `RepairSettings()`).
  - YENI `public void ReplaceWith(PlayerSaveData incoming)` --
    **`ResetToEmpty()` ONCE** (kritik nokta #4), sonra
    `JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(incoming), Data)`, sonra
    `RepairHabits()` + `RepairSettings()`, sonra `dirty = true`. `Data` nesne kimligi
    korunuyor (disaridaki referanslar gecerli kalsin -- sinifin kendi tasarim notu).
- [x] YENI `Assets/Tests/SaveTransferTests.cs` -- kendi nested `MemoryStorage` kopyasiyla
      (paylasilan helper yok, projenin kalibi bu):
  - `Build_RoundTrips_EveryHabitField` -- her alani varsayilan-disi doldur, Build ->
    TryRead -> alan alan karsilastir
  - `Build_StripsPremium` ve **`Build_DoesNotMutateSource`** (kritik nokta #3)
  - `TryRead_EmptyObjectJson_IsRejected` (`"{}"`), `TryRead_ForeignJson_IsRejected`,
    `TryRead_Garbage_IsRejected` (`null`, `""`, `"merhaba"`, kesik JSON)
  - `TryRead_NewerFormatVersion_IsRejected`, `TryRead_NewerSaveVersion_IsRejected`
  - `TryRead_HabitCountMismatch_IsRejected`
  - `Sanitize_OutOfRangeOrDuplicatePieceIndices_AreDropped`
  - `Sanitize_IndexInBothOpenedAndMissed_OpenedWins`
  - `Sanitize_TotalPixelsPaintedBeyondCanvas_IsClamped`
  - `Sanitize_FutureDates_AreClampedToToday`
  - `Sanitize_NegativeStreaks_AreClamped`, `Sanitize_LongestNeverBelowCurrent`
  - `Sanitize_IllegalFrequencyValues_AreDropped`
  - **`ReplaceWith_EmptyIncomingHabits_ClearsExistingHabits`** (kritik nokta #4)
  - `ReplaceWith_NullListsInIncoming_AreRepaired`
  - `ReplaceWith_PreservesDataInstanceIdentity`
- [x] `dotnet build CompileCheck.csproj` -- 0 hata
- [x] EditMode testleri (Editor kapatilip `-runTests`; `Logs/results.xml` once silinir --
      bayat kalabiliyor) -- **401/401 PASSED, 0 failed** (19 yeni `SaveTransferTests` testi dahil,
      hicbir regresyon yok)

## Faz 2 -- `HabbitManager` import yuzeyi

- [x] `Assets/Scripts/HabbitManager.cs`: `Load()`'un `saveSystem.Load()` SONRASI govdesi
      `private void ApplyLoadedSave(DateTime now)` olarak ayrilir (kritik nokta #5).
      Icerik aynen bugunku sira: `RebuildHabitIndex` -> `todayDate` ->
      `MoodService.SettleClosedDays` -> `MissedPieceService.SettleAll` ->
      `DailyResetService.ApplyDailyReset` -> `PaintingAssignment.RepairAll` ->
      `AppSettings.Apply` -> `RotusState.Apply` -> `ReminderService.Schedule` ->
      `changed` ise `MarkDirty` + `FlushNow`. `Load()` kucuk bir sarmalayiciya iner.
- [x] YENI `Assets/Scripts/HabbitManager.Import.cs` -- `HabbitManager.Rotus.cs` /
      `HabbitManager.Archive.cs` ile ayni `partial` gerekcesi (ana dosya zaten 1191 satir):
  ```
  public bool TryImportSave(string json, out string error)
  ```
  Sira (her adimin gerekcesi yukaridaki kritik noktalarda):
  1. `SaveTransfer.TryRead(...)` -- **basarisizsa hicbir sey dokunulmamis halde cik**
  2. `SaveTransfer.Sanitize(package.data, AppClock.Now)`
  3. `package.data.settings.isPremium = Settings.isPremium` (yerel deger kazanir)
  4. `CancelPendingFlush()`
  5. her mevcut aliskanlik icin `ReleaseViewsFor(habit.id)` -- **kayittan ONCE** (#8)
  6. `BurnedPaintingCache.Clear()` (#7)
  7. `saveSystem.ReplaceWith(package.data)`
  8. `ApplyLoadedSave(AppClock.Now)`
  9. `PainterManager.Instance?.RestoreMoodPoint(Data.painterMoodPoint)`, ardindan
     `if (pendingMoodSteps > 0) { ApplyMood(-pendingMoodSteps); pendingMoodSteps = 0; }` (#6)
  10. `saveSystem.SaveNow()` -- import debounce'a birakilmaz, aninda diske iner
  11. Olaylar, bu sirayla: `SettingsChanged` -> `RotusChanged` (mevcut imzasiyla) ->
      **`HabitListChanged` en son** (yapi en sonda degisir, ekranlar zaten tazelenmis
      ayar/aynalara karsi yeniden kurulsun -- CLAUDE.md'nin "HabitListChanged is
      structure" kurali)
- [x] (kucuk, ayni dosyada) `DeleteAllData()`'nin atladigi iki adim eklenir:
      `BurnedPaintingCache.Clear()` ve `RotusChanged` yayimi. Import'la ayni sinifta
      olan, bugun de var olan sessiz eksikler.
- [x] `dotnet build` -- 0 hata

## Faz 3 -- Tasima katmani (pano + yerel kurtarma dosyasi)

- [x] `Assets/Scripts/Settings/SaveExportService.cs` yeniden yazilir (ad korunuyor --
      mevcut hook ve "rezerve public API" kurali). Runtime assembly'sinin **ilk**
      `System.IO` kullanimi; asmdef degisikligi gerekmiyor (BCL):
  - `public struct ExportResult { bool Ok; string FileName; int ByteCount; int HabitCount; string Error; }`
  - `public static ExportResult Export(HabbitManager manager)` -- `manager.FlushNow()`
    (disa aktarilan, kayitli olanla birebir olsun) -> `SaveTransfer.Build` ->
    `GUIUtility.systemCopyBuffer = json` -> `File.WriteAllText` (UTF-8) ->
    `PruneOldBackups`. Her I/O try/catch; dosya yazimi basarisiz olsa bile pano
    yolu basarili sayilir (ve tersi).
  - Dosya adi: `HabitTracker_yedek_yyyy-MM-dd_HHmm.json`,
    `Application.persistentDataPath` altinda. `PruneOldBackups` en yeni **5** taneyi
    birakir (klasor sonsuza kadar buyumesin).
  - `public static string ReadClipboard()` -- `GUIUtility.systemCopyBuffer`, tek gecis
    noktasi (#12: hicbir yerde render edilmiyor).
  - `public static bool TryReadNewestBackup(out string json, out string error)` --
    Faz 7 ve adb ile cihaz testi icin; Faz 1 UI'si cagirmiyor.
- [x] `dotnet build` -- 0 hata

## Faz 4 -- UI (yeni bilesen YOK)

- [x] `Assets/Editor/UI/SettingsPanelBuilder.cs` -> `BuildDataBlock` (:394-412):
      export satiri ile "Ilerlemeyi sifirla" arasina `Divider` + yeni satir
      **"Verileri ice aktar"** (`SettingsRowBuilder.Row(..., 56f, ...)` +
      `NavRow(row, trailing, theme, theme.gold, premiumBadge: **false**)`).
      `UiWiring.Set(screen, "importButton", importButton)` eklenir.
- [x] `Assets/Scripts/UI/Settings/SettingsScreen.cs`:
  - `[SerializeField] private Button importButton;` + `Awake`/`OnDestroy` listener
    cifti (mevcut kalip)
  - `HandleExport()` -- premium kapisi **aynen kalir**; `SaveExportService.Export()`
    yerine `Export(manager)` + sonucu `confirmSheet.Show("Yedek hazır",
    "<N> alışkanlık panoya kopyalandı (<K> KB). Cihazda ayrıca bir kopya saklandı:
    <dosya adı>", "Tamam", null)` ile bildirir. Hata halinde ayni kutu hata metniyle.
  - YENI `HandleImport()` -- `SaveExportService.ReadClipboard()` ->
    `SaveTransfer.TryRead` -> **basarisiz:** `confirmSheet.Show("İçe aktarılamadı",
    error, "Tamam", null)`; **basarili:** `confirmSheet.Show("Yedeği geri yükle",
    Describe(...) + "\n\nMevcut bütün veriler silinip bu yedekle değiştirilecek.",
    "Geri yükle", HandleImportConfirmed)`
  - YENI `HandleImportConfirmed()` -- `manager.TryImportSave(json, out error)` ->
    `RefreshAll()`; hata halinde bilgilendirme kutusu. (`ConfirmSheet.HandleConfirm`
    "once kapat sonra cagir" sirasi sayesinde kutu kendini kapatmis olur.)
  - Panodan okunan JSON `HandleImport` ile `HandleImportConfirmed` arasinda tek bir
    alanda tasinir (pano arada degisebilir -- onaylanan sey okunan sey olmali).
- [x] `confirmSheet == null` fallback'i: import'ta **onaysiz calistirma YOK** --
      `HandleResetRequested`'in aksine (o, onay kutusu yoksa dogrudan siliyor) burada
      hicbir sey yapilmaz ve log atilir. Yikicilik ayni ama bu islemin verisi
      disaridan geliyor.
- [x] `SettingsScreen.Close()` -- yeni sheet eklenmedigi icin degisiklik gerekmiyor
      (`confirmSheet.Close()` zaten var).
- [x] `dotnet build` -- 0 hata

## Faz 5 -- Dev tarafi (Editor'de round-trip testi)

- [x] YENI `Assets/Scripts/Dev/DevSaveTransferTools.cs` + `DevTools`'a iki komut
      (`ExportToFile`, `ImportNewestFile`) -- **dev katmani kural sahibi degil**:
      ikisi de `SaveExportService` / `manager.TryImportSave` uretim yolundan geciyor.
      `DevToolsEditor` tarafina kendi `Section`'i (`DevToolsEditor.Rotus.cs`'in
      partial kalibi; ana dosya butcesi).
- [x] `dotnet build` -- 0 hata

## Faz 6 -- CLAUDE.md + Editor icinde dogrulama

- [x] CLAUDE.md:
  - Architecture > saf mantik listesine `Settings/SaveTransfer.cs`
  - `UI/Settings/*`: `SaveExportService` artik hook degil (`PremiumService` tek basina
    kaldi); `HabbitManager.Import.cs` partial'i `Rotus`/`Archive` kardesligine eklenir
  - "Save format" bolumune: zarf kayit formatinin parcasi **degil**, `isPremium`
    tasinmaz, `saveVersion` ve `formatVersion` iki bagimsiz kapi
  - Yeni invariant blogu: derin-kopya+redaksiyon, dogrulama-once-depo-sonra,
    `ResetToEmpty`-oncelikli `ReplaceWith`, `ApplyLoadedSave`'in tek sahipligi,
    import'ta `pendingMoodSteps`'in aninda harcanmasi, `BurnedPaintingCache.Clear()`
    zorunlulugu, import ucretsiz/export premium gerekcesi, Faz 1'de tasima yolunun
    gercekte yalnizca pano oldugu
  - Test dosyasi listesine `SaveTransferTests.cs` (sayi on altiya cikar)
- [ ] **SENIN YAPMAN GEREKEN (Unity Editor):** sahneyi kaydet ->
      **HabitTracker > Build Painter Habits UI** -> sahneyi tekrar kaydet.
      Builder kosulmazsa "Verileri ice aktar" satiri sahnede hic olmaz.
- [ ] **SENIN YAPMAN GEREKEN (Play mode):** Premium'u ac -> export -> kutuda ozet
      gorunur -> Dev > "Ilerlemeyi sifirla" -> import -> yedek geri gelir
      (aliskanliklar, seriler, tablo ilerlemeleri, galeri, delikler, rotus, ayarlar,
      ressam modu) -> premium **kapanmamis** olmali.
- [x] EditMode testleri -- tam kosu yesil (401/401, bkz. Faz 1)

## Faz 7 -- Gercek dosya tasima yolu --> **AYRI DOSYADA, KOD TARAFI BITTI**

Bu faz `NATIVE_FILE_TRANSFER_PLAN.md` olarak ayri planlandi ve kodu yazildi:
sistem dosya secicisi (disa aktarmada **Kaydet-As**, ice aktarmada **Dosya sec**),
`Settings/NativeFileBridge.cs` + `Plugins/Android/NativeFileBridge.java` +
`Plugins/iOS/NativeFileBridge.mm`. Pano yolu silinmedi -- secici yoksa ya da iptal
edilirse fallback olarak duruyor.

Secimler bu dosyanin tahmininden iki yerde ayrildi:

- **`ACTION_SEND` degil `ACTION_CREATE_DOCUMENT`.** Paylasim `FileProvider` +
  `provider_paths.xml` + ozel `AndroidManifest.xml` isterdi; Kaydet-As hicbirini
  istemiyor (secici URI'yi kendisi veriyor). Sonuc: bu teslim **tek bir
  ProjectSettings/manifest alani degistirmedi**.
- **Editor'de test EDILEBILIYOR.** `NativeFileBridge`'in Editor dali gercek
  `EditorUtility.SaveFilePanel`/`OpenFilePanel` diyaloglarini aciyor, yani tum
  round-trip Windows'ta Play mode'da cihazsiz dogrulanabiliyor. Gercek cihaz yalnizca
  SAF/UIDocumentPicker davranisi icin gerekiyor.

`TryReadNewestBackup` yerine kurtarma dosyalarini listeleyen bir `ImportSheet` fikri
**yazilmadi ve gerekmiyor**: sistem secicisi zaten kullanicinin kendi dosya
yoneticisi.

---

## Riskler ve tuzaklar

- **Builder kosulmazsa import satiri olu kalir** -- Faz 6 atlanamaz. Satir sayisi
  dogrulamasi (`ValidateNav`/`ValidateRotus` kalibi) bu satir icin yok; `SettingsScreen`
  alani bos kalirsa `Awake`'teki listener kurulumu sessizce atlar.
- **UTF-8 BOM.** Kullaniciya gorunen metinler diakritikli ("Yedeği geri yükle",
  "İçe aktarılamadı", "alışkanlık") -> `SaveTransfer.cs`, `SaveExportService.cs`,
  `SettingsScreen.cs`, `SettingsPanelBuilder.cs` **BOM'lu UTF-8** kaydedilir ve
  **PowerShell ile DUZENLENMEZ** (Edit/Write kullan; `Set-Content` ANSI yazar).
  Yorumlar ASCII-only Turkce kalir.
- **Pano boyutu.** ~1-3 KB/aliskanlik; 20 aliskanlik ~40 KB. Android panosu modern
  surumlerde ~1 MB; bazi OEM'lerde daha dusuk olabilir -- buyuk kayitlarda sessiz
  kirpilma ihtimali var, `habitCount` kontrolu tam olarak bunu yakalayan sey.
- **Import bir hile vektorudur** (elle duzenlenmis seri/galeri). Kabul edilen sinir:
  tek oyunculu bir uygulama ve dev araclari zaten ayni seyi yapabiliyor.
  `Sanitize` **tutarsiz** veriyi onariyor, "fazla iyi" veriyi reddetmiyor.
- **Farkli sanat seti** -> aktif tablo yeniden atanir (o tablonun ilerlemesi gider),
  cozulemeyen galeri girdileri cizilmez ama `FinishedCount` onlari saymaya devam eder
  (gorunen karo sayisi ile "Eser" sayisi ayrisir). Kabul edilen sinir, `Describe`
  uyariyor.
- **`isCompletedToday` + import ayni gun.** Yedek bugun alinmis ve bugun geri
  yuklenmisse tiklenmis aliskanliklar tikli gelir -- dogru davranis; mood damgasi
  (`lastMoodSettledDate`) de tasindigi icin cift odul olmaz.
- **`ReminderService`** `ApplyLoadedSave` icinden yeniden planlar; sabit
  `NotificationId = 1001` sayesinde eski alarm yiginlanmaz.
- **`HabbitManager.saveSystem` hala private** -- import testleri manager'i degil
  `HabitSaveSystem` + `SaveTransfer`'i test ediyor (projenin mevcut siniri;
  `HabbitManager` bir `MonoBehaviour` oldugu icin hicbir testte kurulmuyor).

---

## Uygulama sirasi

1. **Faz 1** -- saf mantik + testler (sahnesiz, en riskli kararlar burada kazikli)
2. **Faz 2** -- manager yuzeyi + `ApplyLoadedSave` ayrilmasi (`Load()` regresyonu
   riski var, Faz 1 yesilken yapilmali)
3. **Faz 3** -- pano/dosya katmani
4. **Faz 4** -- UI + builder
5. **Faz 5** -- dev araclari
6. **Faz 6** -- CLAUDE.md + Editor icinde dogrulama (SENIN YAPMAN GEREKEN kisimlari var)
7. **Faz 7** -- ayri teslim, simdi yazilmiyor
