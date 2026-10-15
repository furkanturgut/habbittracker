# Hatirlatma sistemi: gercek yerel bildirim + saat secici UI

**Durum:** Faz 8 TAMAMLANDI (otomatiklesebilen kisim) -- `dotnet build` 0 hata, 300/300 EditMode test
yesil. Geriye yalnizca gercek Android/iOS cihaz testi kaliyor (SENIN YAPMAN GEREKEN) -- bu tamamlanip
onaylandiginda proje tamamen biter.

## Baglam

Ayarlar ekranindaki "Hatirlatma" blogu bugun tamamen bir iskelet. Veri katmani
(`AppSettingsData.reminderOn` / `reminderTime`), dogrulama (`TimeOfDay`), kayit onarimi
(`HabitSaveSystem.RepairSettings`) ve UI (toggle + saat alani) calisiyor; ama
`ReminderService.Schedule` yalnizca `Debug.Log` atiyor (hicbir bildirim altyapisi yok) ve
"Saat" satiri ham bir `TMP_InputField` (kullanici "21:00"i elle yaziyor).

Bu is iki seyi bitirir: bildirim **gercekten** gonderilir, saat **secilerek** girilir.

## Alinan kararlar (sabit)

| Karar | Secim |
|---|---|
| Paket | `com.unity.mobile.notifications` **2.5.0** (Unity 6 hedefli surum) |
| Zamanlama | **Basit gunluk tekrar** -- tek bir `NotificationRepeatInterval.Daily` bildirimi, "o gun tik atilmamis aliskanlik varsa" kosulu yok |
| Izin | Yalnizca kullanici toggle'i **actigi anda** istenir; reddedilirse toggle geri kapanir |
| Saat secici | **Kapsama dahil** -- alttan acilan sheet, saat/dakika adimlayicilari |
| Bildirim metni | Ressamin agzindan: `"<ad> fircayi bekliyor"` + sabit govde (ad `AppSettings.PainterName`'den) |
| Alt etiket | `"Tik atilmamis aliskanlik varsa"` -> `"Her gun ayni saatte"` (basit-gunluk-tekrar ile artik dogru olan, kisa) |

### Ikinci tur incelemede bulunan kritik duzeltmeler (ilk taslaga gore)

1. **`reminderOn` varsayilani `true` -> `false` OLMALI.** Izin "yalnizca toggle acilinca" istenecek
   ama toggle `true` doguyorsa kullanicinin ilk hareketi onu KAPATMAK olur -- izin hic istenmez,
   Ayarlar "acik" yazar ama Android 13+/iOS hicbir bildirim teslim etmez. Sessiz UX hatasi. Gerileme
   riski sifir: bu alan `true`'yken de bugune kadar hic bildirim gitmiyordu (`Schedule` yalnizca log
   atiyordu).
2. **Izin isteme coroutine'ini `HabbitManager` kosturmali, `SettingsScreen` degil.** Panel kapanirken
   `gameObject.SetActive(false)` oluyor, bu kendi coroutine'lerini oldurur -- OS izin diyalogu
   acikken panel kapatilsa geri cagri hic calismaz, toggle izinsiz ACIK kalir. `HabitSystem` nesnesi
   hic deaktif olmuyor.
3. **Android "Reschedule on Device Restart" KAPALI kalmali.** Acmak manifeste
   `RECEIVE_BOOT_COMPLETED` izni ekler; `HabbitManager.Load()`'un HER ACILISTA yeniden planlamasi
   (cihaz yeniden baslatma sonrasi ilk acilista dahil) ayni isi ekstra izin istemeden yapiyor.
4. **iOS "Request Authorization on App Launch" acikca KAPALI yapilmali.** Paketin varsayilani ACIK
   olabilir; acik kalirsa uygulama ilk acilista izin ister ve "izin yalnizca toggle'dan" karari
   kodda hicbir iz birakmadan ihlal edilir.
5. **`DeleteAllData()` da yeniden planlama/iptal cagirmali** -- ayarlar varsayilana donuyor ama
   bugun o metot `ApplySettingsChange()`'i cagirmiyor; sifirlama sonrasi eski saatli alarm asili
   kalirdi.

---

## Faz 1 -- Paket, proje ayarlari, asmdef

- [x] `Packages/manifest.json`'a `"com.unity.mobile.notifications": "2.5.0"` ekle (alfabetik: `inputsystem` ile `multiplayer.center` arasi)
- [x] Unity'nin paketi cozmesini bekle (`Library/PackageCache/com.unity.mobile.notifications@...` klasoru belirmeli) -- `com.unity.mobile.notifications@6e5da11136c3` olusup `Packages/packages-lock.json`'a islendi
- [x] **Dogrulama (tahmin degil, dosyadan oku):**
  - [x] `Library/PackageCache/com.unity.mobile.notifications@6e5da11136c3/Runtime/Unified/Unity.Notifications.Unified.asmdef` -- assembly adi dogrulandi: `Unity.Notifications.Unified`, `includePlatforms: [Android, Editor, iOS]`
  - [x] `NotificationsPermissionRequest` (ayni klasor) `CustomYieldInstruction`den turuyor, `keepWaiting`i override ediyor -- polling gercekten `yield return request;` tek satira iniyor; sonra `request.Status` (`NotificationsPermissionStatus`: RequestPending/Granted/Denied) okunur. Ayrica `NotificationCenter.RequestPermission()` (Editor/Unified) bu nesneyi dondurur. "IsFinished" adinda public bir uye YOK -- bu platforma ozel ic siniflarin (Android `PermissionRequest`, iOS `AuthorizationRequest`) alani, plandaki tahmin kismen yanlisti.
  - [x] Paketin kendi `AndroidNotificationPostProcessor.cs`'i `POST_NOTIFICATIONS`'i build sirasinda manifestte kendisi ekliyor -- elle manifest degisikligi gerekmiyor.
  - [x] (Ekstra) `Notification.Identifier` nullable `int` olarak disaridan yazilabiliyor -- Faz 3'teki sabit `NotificationId = 1001` karari dogrulandi (`ScheduleNotification` id'yi uretmek yerine bu alani okuyor).
- [x] **Edit > Project Settings > Mobile Notifications** (GUI'den elle gezmek yerine kod yolu secildi -- `NotificationSettingsManager`in disk formati `EditorJsonUtility` + ozel `NotificationSettingsCollection`, elle YAML/JSON yazmak kirilgan olurdu):
  - [x] Android > *Reschedule Notifications on Device Restart*: paket varsayilani zaten **KAPALI** (`NotificationSettingsManager.cs:161`, `GetOrAddNotificationSettingValue(..., false, true)`) -- degisiklik gerekmiyor ama asagidaki menu komutu yine de aciklik icin yaziyor
  - [x] Android > *Schedule at Exact Time*: paket varsayilani zaten **KAPALI** (`(AndroidExactSchedulingOption)0`, ayni dosya satir 166) -- ayni sebep
  - [x] iOS > *Request Authorization on App Launch*: paket varsayilani **ACIK** (`NotificationSettingsManager.cs:108`, `..., true, false)`) -- kritik duzeltme #4 burada gercekten gerekli, menu komutu KAPATIYOR
  - [x] Android > Notification Icons: bos birakildi (takip isi, risklere bak)
  - [x] YENI `Assets/Editor/HabitTrackerMenu.cs` -> `[MenuItem("HabitTracker/Configure Mobile Notification Settings")]` (`#if HABIT_NOTIFICATIONS` arkasinda): ucunu de `NotificationSettings.AndroidSettings.RescheduleOnDeviceRestart/ExactSchedulingOption` ve `NotificationSettings.iOSSettings.RequestAuthorizationOnAppLaunch` uzerinden yaziyor
  - [ ] **SENIN YAPMAN GEREKEN:** Editor zaten acik -- asmdef degisikligini derleyip bitirsin, sonra `HabitTracker > Configure Mobile Notification Settings` menusunu BIR KERE calistir (log'da "KAPALI... KAPALI... KAPALI" satirini gormelisin). Bu adim `ProjectSettings/NotificationsSettings.asset` dosyasini olusturur -- **Plastic'e eklenmesi** gerekiyor.
- [x] `Assets/Scripts/HabitTracker.Runtime.asmdef`: `references`'a `Unity.Notifications.Unified` + `versionDefines`'a
      `{ "name": "com.unity.mobile.notifications", "expression": "2.3.0", "define": "HABIT_NOTIFICATIONS" }` eklendi
- [x] `Assets/Editor/HabitTracker.Editor.asmdef`: ayni `versionDefines` + `references`'a Editor-only ayarlar sinifi icin `Unity.Notifications` eklendi (plandaki asil madde Runtime icindi ama `Configure Mobile Notification Settings` menu komutu Editor assembly'sinde yasiyor ve `NotificationSettings` o Editor-only pakette)
- [ ] CLAUDE.md'deki hizli derleme-kontrolu `.csproj`'una da **iki paralel kontrol** icin hazirlik -- bilerek Faz 3/4 sonuna birakildi (plandaki parantez: "Asil dotnet build kosullari Faz 3/4 sonunda"); fallback dal (`CompileCheck.csproj`, HABIT_NOTIFICATIONS tanimsiz) bu fazin sonunda 0 hata ile derlendi, dogrulandi

**Neden versionDefines (duzeltilmis gerekce):** Asil sebep "dotnet build paketi gormez" degil --
paket assembly'leri de `Library/ScriptAssemblies/` altina derlenir, `Unity.InputSystem.dll` gibi
`HintPath` ile baglanabilir. Gercek sebepler: (a) iki dali da ayri ayri derleyip test etmek, "hangisi
sessizce cokmus" durumunu yakalayan tek yol; (b) paket cozulmemisken (cevrimdisi checkout,
registry hatasi) proje yine derlenir -- Plastic'te `Library/` versiyonlanmiyor; (c) paket tek bir
dosyadan (`ReminderService.cs`) gorunur kalir -- silinirse o dosyayi eski davranisina dondurmek
disinda hicbir sey gerekmez.

---

## Faz 2 -- Saf mantik + kritik varsayilan duzeltmesi -- TAMAMLANDI

- [x] `Assets/Scripts/Data/PlayerSaveData.cs`: `public bool reminderOn = true;` -> **`public bool reminderOn;`** (varsayilan kapali, kritik duzeltme #1) + aciklayici yorum guncellendi
- [x] YENI `Assets/Scripts/Settings/ReminderSchedule.cs` -- `TryNextFireTime(DateTime now, string hhmm, out DateTime fireTime)`: `TimeOfDay.TryParse` + `now.Date.Add(new TimeSpan(...))` (Local korunuyor) + esitlik->YARIN karari
- [x] `Assets/Scripts/Settings/TimeOfDay.cs`: `public static int Wrap(int value, int modulus)` eklendi
- [x] **Testler yazildi ve KOSULDU** (Editor kapatilip `-runTests` ile): `Logs/results.xml` -- **300/300 test PASSED, 0 failed, 0 inconclusive, 0 skipped**. Eklenen 13 `ReminderSchedule_*` testi (biri 5, biri 7 TestCase'li) + `TimeOfDay_Wrap_WrapsBothDirections` (5 TestCase) + guncellenen `AppSettingsData_DefaultsMatchProductionBehavior` hepsi yesil; `SettingsLogicTests` fixture'i tek basina 56/56.
- [x] `dotnet build` (fallback dal) -- 0 Hata, 0 Uyari

---

## Faz 3 -- ReminderService + HabbitManager kancalari -- TAMAMLANDI

- [x] `Assets/Scripts/Settings/ReminderService.cs` yeniden yazildi, `Schedule(bool on, string time)` imzasi KORUNDU + ic yuzden `Schedule(bool on, string time, DateTime now)` saf-asiri-yuklemesine yonleniyor. **UTF-8 BOM ILE kaydedildi** (`printf`+`cat` ile BOM byte'lari eklendi, Turkce diakritikler dogrulandi: fırçayı/Hatırlatıcılar/Günlük/aynı/dokunuş). Icerik:
  - `NotificationId = 1001`, `Cancel()`, `ClearDelivered()` (-> `NotificationCenter.CancelAllDeliveredNotifications()`)
  - `IEnumerator RequestPermission(Action<bool> done)` -- donus tipinde paket tipi yok
  - Tembel idempotent `Initialize()`: Editor'de `Application.isEditor` -> false (paket hic cagrilmaz); `RequestPermission` bu false'u `done(true)`'ya ceviriyor (Editor'de mutlu yol testi)
  - Tum paket cagrilari `#if HABIT_NOTIFICATIONS` arkasinda -- ekstra platform kisitlamasi GEREKMEDI, Faz 1'de dogrulanan `Unity.Notifications.Unified.asmdef` zaten `includePlatforms: [Android, Editor, iOS]`
  - Bildirim: baslik `AppSettings.PainterName + " fırçayı bekliyor"`, govde sabit, `ShowInForeground = false`, `NotificationDateTimeSchedule(fireTime, NotificationRepeatInterval.Daily)`
- [x] `Assets/Scripts/HabbitManager.cs`:
  - `ApplySettingsChange()` sonuna `ReminderService.Schedule(...)` eklendi
  - `Load()` icinde `AppSettings.Apply(...)`'dan hemen sonra ayni cagri (3-parametreli saf asiri yukleme, zaten elde olan `now` ile)
  - `DeleteAllData()` icine ayni cagri eklendi (kritik duzeltme #5)
  - YENI `public void RequestReminderPermission(Action<bool> done)` -- kritik duzeltme #2
  - `OnApplicationPause(false)` / `OnApplicationFocus(true)` icine `ReminderService.ClearDelivered()` eklendi
  - `SetReminder`'in erken-donus satirina DOKUNULMADI
- [x] **Dogrulama, plandaki "iki paralel kontrol"dan daha gucluyle yapildi:** fallback dal `dotnet build CompileCheck.csproj` -> 0 hata; gercek dal icin Editor kapatilip `-runTests` ile TAM Unity derlemesi kosuldu (HABIT_NOTIFICATIONS gercekten tanimli, `Unity.Notifications.Unified`/`Unity.Notifications` assembly'leri `Library/ScriptAssemblies/`e derlendi) -> **300/300 test PASSED, 0 failed, 0 hata**. Bu, `ReminderService.cs`'in gercek paket API'siyle VE `HabitTrackerMenu.cs`'teki `ConfigureNotificationSettings`'in ikisinin de Unity'nin kendi derleyicisinde hatasiz derlendigini kanitliyor.

---

## Faz 4 -- UI: saat secici (kapsama dahil edilen karar) -- TAMAMLANDI

- [x] YENI `Assets/Scripts/UI/Settings/TimePickerSheet.cs` -- `ConfirmSheet`in kardesi, ayni scrim/kayma sablonu (kodu PAYLASMIYOR): saat/dakika adimlayicilari (dakika adimi 5, `TimeOfDay.Wrap` ile sarma), `public event Action<int, int> Picked`, `public void Show(string hhmm)`, `HandleConfirm`'de "once kapat sonra cagir" sirasi
- [x] `Assets/Editor/UI/SettingsRowBuilder.cs`: `ValueRow(RectTransform row, RectTransform trailing, UiTheme theme, out TMP_Text valueLabel)` eklendi (`NavRow`'un ok yerine deger gosteren ikizi)
- [x] `Assets/Editor/UI/SettingsPanelBuilder.cs` -- `BuildReminderBlock`:
  - Alt etiket `"Tik atılmamış alışkanlık varsa"` -> `"Her gün aynı saatte"`
  - `TMP_InputField` `SettingsRowBuilder.ValueRow(...)` ile degistirildi
  - `ConfirmSheet`ten sonra `TimePickerSheetBuilder.Build(root, theme)` cagriliyor (en ustte)
  - Wiring: `"timeButton"`, `"timeValueLabel"`, `"timePicker"` eklendi; `"timeInput"` cikti
- [x] YENI `Assets/Editor/UI/TimePickerSheetBuilder.cs` (ayri dosya -- `BuildConfirmSheet` sablonu, saat/dakika sutunlari icin `BuildStepperColumn`/`StepperButton` yardimcilari)
- [x] `Assets/Scripts/UI/Settings/SettingsScreen.cs`:
  - `timeInput` -> `timeButton`/`timeValueLabel`/`timePicker`
  - YENI `reminderPermissionPending` alani
  - `HandleReminderToggled`: kapanista direkt `SetReminder(false,...)`; acilista `reminderPermissionPending` kontrolu + `manager.RequestReminderPermission(HandleReminderPermission)`
  - `HandleReminderPermission(bool granted)`: `this == null || manager == null` korumasi; reddedilirse toggle+satir kapatiliyor; kabulde `SetReminder(true,...)`
  - `HandleTimeRowTapped`/`HandleTimePicked` eklendi; `RefreshAll` `timeValueLabel.text` kullaniyor; `Close()` `timePicker.Close()` da cagiriyor
  - Eski iki `ReminderService.Schedule(...)` cagrisi KALDIRILDI (planlama artik tamamen `HabbitManager` uzerinden)
- [x] `dotnet build CompileCheck.csproj` -- 0 Hata, 0 Uyari (Faz4 kodu `HABIT_NOTIFICATIONS` define'ina bagli degil, tek dal yeterli ve dogrulandi)

---

## Faz 5 -- Testler (Faz 2'de yazilanlarin disinda, varsa) -- TAMAMLANDI (ek is gerekmedi)

- [x] Faz 3 sonrasi tam Unity test kosusu zaten 300/300 yesil vermisti; Faz 4 hicbir EditMode testinin dokundugu siniflari (UI/Editor-only) degistirmedi, yan etki riski yoktu -- duzeltilecek bir sey cikmadi
- [x] `TimeOfDay.Wrap` testleri Faz 2'de yazilip kosuldu (`TimeOfDay_Wrap_WrapsBothDirections`, 5 TestCase, hepsi yesil) -- Faz 4'un saat secicisi bu fonksiyona dayaniyor

---

## Faz 6 -- Unity Editor icinde (SENIN YAPMAN GEREKEN) -- TAMAMLANDI (kullanici onayladi)

Bunlar Unity Editor'un GUI'sinde calisan adimlar -- buradan tetiklenemiyordu, kullanici yapti.

- [x] Sahneyi kaydet
- [x] **HabitTracker > Build Painter Habits UI** menu komutu kosuldu (Saat satiri + TimePickerSheet sahneye girdi)
- [x] Sahneyi tekrar kaydet
- [x] Play mode dogrulamasi yapildi

---

## Faz 7 -- CLAUDE.md guncelleme -- TAMAMLANDI

- [x] Commands > derleme-kontrolu tarifine `Unity.Notifications.Unified`/`Unity.Notifications` referansi + `HABIT_NOTIFICATIONS` define'i ekleyen not
- [x] Assemblies: `HabitTracker.Runtime` + `HabitTracker.Editor`'in yeni referanslari + paylasilan `versionDefines` girdisinin ucuncu gerekcesi
- [x] Architecture > `UI/Settings/*`: `ReminderService` artik "hook" degil (kardesleri `PremiumService`/`SaveExportService` hala hook); `ReminderSchedule.cs`, `TimePickerSheet.cs`, `TimePickerSheetBuilder.cs` ve `SettingsRowBuilder.ValueRow` eklendi
- [x] Test dosyasi listesine `SettingsLogicTests.cs`'in yeni kapsami eklendi (sayi on uc kaldi)
- [x] `AppClock.Now` invariantina ucuncu uretici olarak `ReminderService` eklendi + `NotificationRepeatInterval.Daily`'nin dev-saat-kaymasiyla catismama gerekcesi
- [x] YENI invariant eklendi: sabit kimlik / kesinlikle-gelecek kurali / `Load()`'un her acilista yeniden planlamasi / iznin yalnizca toggle'dan istenmesi ve coroutine'in HabbitManager'da yasamasi / iki proje-ayarinin kodda iz birakmadigi icin kritik oldugu
- [x] ASCII-yorum / diakritikli-kullanici-metni paragrafina `ReminderService.cs`'i eklendi (artik BOM'lu UTF-8)

---

## Faz 8 -- Son dogrulama

- [x] `dotnet build` -- fallback dal (`CompileCheck.csproj`, `HABIT_NOTIFICATIONS` tanimsiz): **0 Hata**
- [x] EditMode testleri -- Editor kapatilip `-runTests` ile kosuldu (gercek dal: `HABIT_NOTIFICATIONS` tanimli, paket assembly'leri `Library/ScriptAssemblies/`e derlendi). Tek bir `TestResults.xml` once silindi (bayat kalmadi) -> **300/300 test PASSED, 0 failed, 0 hata.** Faz 6'nin sahne degisikligi (builder kosusu) dahil hicbir regresyon yok.
- [ ] **SENIN YAPMAN GEREKEN -- gercek Android cihaz** (Android 13+ sart, izin akisi icin): kur -> toggle kapali -> ac -> OS izin diyalogu -> REDDET -> toggle kendiliginden kapanir -> tekrar ac -> IZIN VER -> `adb shell dumpsys alarm` kaydi gorunur -> saati 2dk sonraya ayarla, arka plana al -> bildirim gelir (ressam adi basliktaki) -> on plana al -> bildirim golgesi temizlenir -> cihazi yeniden baslat, uygulamayi ac -> alarm tekrar kayitli -> "Ilerlemeyi sifirla" -> alarm kaybolur
- [ ] **SENIN YAPMAN GEREKEN -- gercek iOS cihaz** (varsa): ilk acilista izin diyalogu **CIKMAMALI**; toggle'dan izin; kilit ekraninda teslim; on plandayken banner gelmemeli

---

## Riskler ve tuzaklar

- **Assembly referans adi tahmin degil, Faz 1'de dosyadan dogrulanacak.**
- **`NotificationsPermissionRequest` uye adlari** (`IsFinished`/`Status`) Faz 1'de dogrulanacak; `CustomYieldInstruction`'dan turuyorsa polling `yield return request;`'e iner.
- **`ReminderService`'in public imzalarinda paket tipi OLMAMALI** (yalnizca `bool`/`string`/`int`/`DateTime`/`Action<bool>`/`IEnumerator`) -- aksi halde `HabitTracker.Tests` paketi referans etmek zorunda kalir.
- **UTF-8 BOM** -- `ReminderService.cs`, `TimePickerSheetBuilder.cs`, degisen `SettingsPanelBuilder.cs` BOM'lu UTF-8; PowerShell ile DUZENLENMEYECEK (Edit/Write kullan, `Set-Content` ANSI yazar ve Turkce karakterleri bozar).
- **Builder kosulmazsa Saat satiri olu kalir** -- Faz 6 atlanamaz.
- **Her gun atesleme** -- kabul edilen takas: tum aliskanliklar tamamlanmis/planli aliskanlik olmayan gunlerde de bildirim gelir.
- **OEM pil optimizasyonu (Xiaomi/Huawei/Samsung) ve Doze** bildirimi geciktirebilir/dusurebilir -- kabul edilen sinir, `SCHEDULE_EXACT_ALARM` bilerek istenmiyor.
- **DST gecisleri** -- var olmayan bir yerel saatte (orn. 02:30) o gun atlanabilir/kayabilir -- kabul edilen uc durum.
- **Dev-modu `AppClock` kaydirmasi OS alarmini KAYDIRMAZ** -- istenen davranis (bir gelistirici anahtari kullanicinin telefon alarmini oynatmamali); bildirim teslimi test edilecekse CIHAZ SAATI kaydirilir.
- **Android ≤ 12'de calisma zamani izni yok** -- `RequestPermission` aninda `Granted` doner, reddetme yolu o cihazlarda hic tetiklenmez (Android 13+ cihazda dogrulanmali).
- **Android small icon ayarlanmadi** -- genel uygulama ikonuna duser, paket uyari loglar; takip isi (48x48 beyaz-seffaf PNG).
- **Izin diyalogu acikken `SettingsScreen` kapatilir/yok edilirse** -- `this == null || manager == null` korumasi bu durumu zararsiz kilar.
- **`timeInput` kaldirildi, serialize alan degisiyor** -- builder her kosuda sifirdan kurdugu icin `[FormerlySerializedAs]` gerekmez ama Faz 6 atlanmamali.

---

## Uygulama sirasi (bagimliliklar)

1. Faz 1 (paket + proje ayarlari + asmdef) -- Faz 1'deki dogrulama adimi sonraki fazlari etkileyebilir, durdurucu
2. Faz 2 (saf mantik + kritik varsayilan + testler) -- paket/UI karismadan saf mantik yesil olsun
3. Faz 3 (servis + manager kancalari) -- iki dal `dotnet build`
4. Faz 4 (UI saat secici) -- `dotnet build`
5. Faz 5 (kalan test duzeltmeleri, varsa)
6. Faz 6 (Editor icinde: builder + playtest) -- SENIN YAPMAN GEREKEN
7. Faz 7 (CLAUDE.md)
8. Faz 8 (EditMode testleri + gercek cihaz) -- SENIN YAPMAN GEREKEN kisimlari var
