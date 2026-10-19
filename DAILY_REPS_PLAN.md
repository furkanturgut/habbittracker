# Gunluk tur sayisi: bir aliskanlik gunde birden fazla tikle tamamlanir

**Durum:** Faz 1-5 TAMAMLANDI -- `dotnet build CompileCheck.csproj` 0 hata, 0 uyari.
EditMode kosusu Unity Editor kapatilmasini bekliyor (tekil ornek kilidi). Geriye Faz 6
(Editor yeniden kurulumu + elle cihaz testi) kaliyor -- SENIN YAPMAN GEREKEN.

## Istek

"Kediyle oyna: 2 tur" gibi, gunde birden fazla kez tiklanmasi gereken aliskanlik turu.
Ikinci tik atildiginda aliskanlik **o gun icin tamamlanmis** sayilir: streak ilerler,
tablodan parca acilir, mood odulu/rotus islenir. Birinci tik yalnizca sayaci ilerletir.

## Baglam

Bugun bir aliskanligin gunu tek bir `bool` ile kapaniyor: `HabitData.isCompletedToday`.
Butun alt sistemler (mood, streak, sovale sayaci, galeri, filtreler, yakilan parca) bu tek
alani okuyor. Yani isin tamami **"o bool'u kim ve ne zaman true yapar"** sorusunun etrafinda
donuyor; alt sistemlerin hicbirine dokunmaya gerek yok.

## Alinan kararlar (sabit)

| Karar | Secim |
|---|---|
| Yeni alanlar | `HabitData.targetPerDay` (hedef, varsayilan 1) + `HabitData.todayReps` (bugunku tik sayisi) |
| `saveVersion` | **ARTMIYOR** -- `rotusProgress`/`isArchived` ile ayni gerekce (asagida) |
| Otorite | `isCompletedToday` **tek otorite** kalir; `todayReps` yalnizca "hedefe kac kaldi" sayaci |
| Degismeyen soz | `isCompletedToday == true` ise `todayReps == target`; `false` ise `todayReps < target` |
| Ust sinir | `HabitReps.MaxTarget = 10` (form adimlayicisinin tasarlandigi aralik; `HabitFilters.MaxSlots` gibi bilincli bir tavan) |
| Dokunma dongusu | Tik ilerletir; **hedefe ulasmis** bir tile'a dokunmak gunu TAMAMEN geri alir (`todayReps = 0`) |
| Hedef duzenlemesi | Hedefi degistirmek bugunku tamamlanma durumunu DEGISTIRMEZ (yalnizca sayac kirpilir) |
| Saf mantik | Yeni `Assets/Scripts/Habits/HabitReps.cs` -- `DateTime.Now` okumaz, test edilebilir |
| Form kontrolu | `-` / deger / `+` adimlayici satiri (`TimePickerSheetBuilder` gorsel kalibi) |
| Gosterim | Tile (01) ve kart (02) uzerinde `1/2` pili; hedef 1 ise pil hic gorunmez |
| Ses | Yeni ses ipucu YOK -- kismi tikte zaten bugun de ses cikmiyor |

### Neden `saveVersion` artmiyor

Eski kayitta bu iki anahtar hic yok. `JsonUtility` eksik anahtari C# alan
baslangic degerinde birakiyor: `targetPerDay = 1` (alan baslaticisi, `canvasSize`
ile ayni kalip) ve `todayReps = 0`. Ikisi birlikte zaten "bu ozellik hic
kullanilmamis" demek -- migrasyon degil. `List<>` olmadiklari icin
`RepairHabits`'in null-onarimina da girmiyorlar; yine de elle duzenlenmis bir
JSON icin kirpma korumasi ekleniyor (asagida Faz 1.4).

### Neden "hedefe ulasmis tile'a dokunmak gunu sifirlar"

Bugunku davranis zaten bu (`WorkshopScreen.HandleTileClicked`: tikliyse
`UncompleteHabit`). Hedef 1 icin davranis **birebir ayni** kaliyor. Hedef 2 icin
dongu `0/2 -> 1/2 -> 2/2 (tamam) -> 0/2`. Boylece sifira donusun her zaman bir
yolu var ve geri alma **atomik**: streak/parca/mood/rotus tek seferde ve tam
olarak geri sariliyor (bugunku `UncompleteHabit` govdesinin aynisi).

Kabul edilen kusur: `1/2`'de yapilan yanlis bir dokunus tek dokunusla geri
alinamaz -- once tamamlanip sonra sifirlanir. Bedeli yok, cunku kismi tik hicbir
sey odullendirmiyor (parca acilmiyor, streak ilerlemiyor, mood islenmiyor);
yalnizca sayac gorunur.

### Neden hedef duzenlemesi bugunu tamamlamiyor

`UpdateHabit` icinde "hedef dustu, demek ki gun artik tamamlandi" demek, tamamlama
kuyrugunu (streak + parca + mood + rotus + kutlama) **ikinci bir yerden** cagirmak
olurdu; `frequency`/`endDate` duzenlemelerinin bugune dokunmamasiyla da celisirdi
(bkz. CLAUDE.md -- arsivleme "kabul edilen kusur" notu). Bunun yerine saf kural:

* Hedef **yukselirse** ve gun zaten tamamlanmissa: tamamlanmis KALIR, `todayReps = yeniHedef`.
  (Arsivleme kusuruyla ayni sinif: bir duzenleme gecmisi geri almaz.)
* Hedef **duserse** ve gun tamamlanmamissa: `todayReps = min(todayReps, yeniHedef - 1)`
  -- yani fazla tiklar affedilir, gunu kapatmak icin bir dokunus kalir.

Boylece "`!isCompletedToday` ise `todayReps < target`" sozu her yolda ayakta kaliyor.

---

## Faz 1 -- Veri + saf mantik

- [x] **1.1** `Scripts/Data/PlayerSaveData.cs` / `HabitData`: `public int targetPerDay = 1;` ve
      `public int todayReps;` alanlari + neden `saveVersion` artmadigini anlatan Turkce (ASCII)
      yorum. `HabitTypes` gibi mevcut yorum duzenine uyacak.
- [x] **1.2** YENI `Scripts/Habits/HabitReps.cs` -- bu ozelligin butun kurallarinin tek yeri:
      - `MinTarget = 1`, `MaxTarget = 10`, `ClampTarget(int)`
      - `Target(HabitData)` -- `<= 0` gelen (eski/elle bozulmus) degeri 1 okur; **tek okuma kapisi**
      - `IsMulti(HabitData)` -- hedef > 1 (UI'nin pili gosterip gostermeme karari)
      - `DoneCount(HabitData)` -- ekranda gosterilecek sayi (tamamlandiysa hedefin kendisi)
      - `Advance(HabitData)` -- sayaci ilerletir, **bu tik gunu kapatiyorsa** `true` doner
      - `Reset(HabitData)` -- sayaci sifirlar (gun donusu + geri alma)
      - `ApplyTarget(HabitData, int)` -- yukaridaki hedef-duzenleme kurali
- [x] **1.3** `Habits/DailyResetService.cs` -- `ApplyDailyReset`: `isCompletedToday = false` ile
      ayni dongude `HabitReps.Reset(habit)`.
- [x] **1.4** `Save/HabitSaveSystem.cs` -- `RepairHabits`: `targetPerDay` kirpilir, `todayReps`
      `[0, target]` araligina cekilir ve degismeyen soz zorlanir (`isCompletedToday` ise hedefe
      esitlenir, degilse en fazla `hedef - 1`).
- [x] **1.5** `Settings/SaveTransfer.cs` -- `SanitizeHabit`: ayni kirpma (disaridan gelen kayit
      icin; `rotusProgress`/`canvasSize` satirlarinin yanina).

## Faz 2 -- Yonetici (tek yazma yuzeyi)

- [x] **2.1** `HabbitManager.CompleteHabit`: mevcut kapilardan (`isCompletedToday`,
      `awaitingPaintingChoice`, `isArchived`, `IsDueOn`) sonra `HabitReps.Advance`. Gun
      kapanmiyorsa **kismi tik**: `MarkDirtyDeferred` + `HabitChanged` + `true` doner; streak,
      parca, mood, rotus ve kutlamanin hicbirine dokunulmaz. Kapaniyorsa bugunku govde
      oldugu gibi isler.
- [x] **2.2** `HabbitManager.UncompleteHabit`: govdeye `HabitReps.Reset(habit)` eklenir
      (geri alma gunu tamamen sifirlar). Giris kosulu DEGISMEZ -- yalnizca tamamlanmis bir
      gun geri alinabilir.
- [x] **2.3** `CreateHabit`: sonuna `int targetPerDay = 1` istege bagli parametre (varsayilan
      deger `DevSampleHabits`'in mevcut cagrisini kirmasin diye sonda) + `ClampTarget`.
- [x] **2.4** `UpdateHabit`: yeni `int targetPerDay` parametresi -> `HabitReps.ApplyTarget`.

## Faz 3 -- Form (ekleme/duzenleme kutusu)

- [x] **3.1** `Habits/HabitForm.cs`: `TargetPerDay` alani + `Reset`/`LoadFrom` + `StepTarget`
      (kirpmali).
- [x] **3.2** `UI/Habits/HabitSheet.cs`: `repsMinusButton` / `repsPlusButton` / `repsValueLabel`
      alanlari, `WireOptions` baglantilari, `RefreshAll`'da deger + dugmelerin `interactable`
      durumu (1'de `-`, 10'da `+` kapali), `Save`'de yeni parametrenin gecilmesi.
- [x] **3.3** `Editor/UI/HabitsPanelBuilder.cs`: "Gunluk tur" bolumu -- Gunler ile Tuval
      arasina, `TimePickerSheetBuilder.StepperButton` gorsel kalibiyla yatay adimlayici;
      uc yeni alanin `UiWiring` ile baglanmasi.

## Faz 4 -- Gosterim (01 ve 02 ekranlari)

- [x] **4.1** `UI/Workshop/HabitTile.cs`: `repsLabel` -- kart cercevesinin SOL UST kosesinde
      kucuk pil (`doneBadge` sag ustte, carpismiyor). `HabitReps.IsMulti` false ise pasif.
      Mutlak konumlu, `Info` kolonunun layout grubuna girmiyor.
- [x] **4.2** `UI/Habits/HabitCard.cs`: `repsLabel` -- baslik satirindaki tur pilinin yanina
      `1/2`. Yine hedef 1 ise pasif.
- [x] **4.3** `Editor/UI/UiPrefabBuilder.cs`: iki pilin `BuildHabitTile` / `BuildHabitCard`
      icinde kurulmasi + `UiWiring`.
- [x] **4.4** `Assets/Localization/Strings.tsv`: yeni anahtarlar, sekiz dilde --
      `habit_sheet.section.reps` ("Gunluk tur"), `habit_sheet.reps_hint` ("Gunde kac tik"),
      `habit.reps_progress` ("{0}/{1}"). Ardindan **Build Localization Table**.

## Faz 5 -- Gelistirici araclari + testler

- [x] **5.1** `Dev/DevHabitTools.CompleteAllDue`: hedefi 1'den buyuk olan aliskanlik icin
      `CompleteHabit` tek cagriyla gunu kapatmiyor -- her aliskanlik icin hedef kadar
      (sinirli) dongu. Uretim yolundan gecme kurali korunur.
- [x] **5.2** `Dev/DevSampleHabits`: ornek listedeki bir aliskanlik hedef 2 ile uretilir
      (elle test icin hazir veri).
- [x] **5.3** YENI `Assets/Tests/HabitRepsTests.cs`: `ClampTarget`, `Target` (eski kayit 0 ->
      1), `Advance` (hedef 1'de ilk tik kapatir; hedef 3'te ucuncu tik kapatir), `Reset`,
      `ApplyTarget` (dort yon: yukari/asagi x tamamlanmis/tamamlanmamis), degismeyen sozun
      her durumda tutmasi.
- [x] **5.4** Mevcut testlere ekleme: `HabitDataLogicTests` (gun donusu sayaci sifirlar;
      JSON gidis-donusunde iki alan tasinir), `SaveTransferTests` (`Sanitize` bozuk
      hedef/sayaci onarir).
- [ ] **5.5** `dotnet build CompileCheck.csproj` + Editor'de `-runTests` (Editor kapali).

## Faz 6 -- Editor yeniden kurulumu ve elle test (SENIN YAPMAN GEREKEN)

- [ ] **6.1** Unity'de **HabitTracker > Build Painter Habits UI** -- TEK komut yeter:
      ilk adimi zaten `LocalizationTableBuilder.Build()` cagiriyor (ayri bir "Build
      Localization Table" menu ogesi YOK, bkz. PainterHabitsBuilder.BuildAll), ardindan
      prefablari ve sahne koklerini sifirdan kuruyor -- generated prefablari elle duzenleme.
- [ ] **6.2** Elle test: hedef 2 ile aliskanlik olustur -> birinci tik `1/2` gosterir, parca
      ACILMAZ, streak ilerlemez -> ikinci tik gunu kapatir (rozet, parca, alev, mood) ->
      ucuncu dokunus `0/2`'ye doner -> gece yarisi/dev saat atlamasi sayaci sifirlar ->
      hedefi 3'e cikar, 2'ye dusur (yukaridaki kural) -> kayit kapat/ac (sayac korunur).

---

## Dokunulmayan yerler (bilincli)

`MoodService`, `DailyProgress`, `HabitFilters`, `GalleryWorks`, `StreakFlame`,
`MissedPieceService`, `PaintingProgress`, `PaintingAssignment`, `HabitArchive`,
`ReminderService`: hepsi `isCompletedToday` / `IsDueOn` okuyor ve otorite orada
kaldigi icin tek satir degisiklige ihtiyac duymuyorlar. Sovale sayaci da
aliskanlik sayar, tik sayar degil -- "2 turluk aliskanlik gunun yarisi" diye bir
sey yok.
