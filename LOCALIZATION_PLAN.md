**Durum:** Faz 0-5'in TUMU kod/veri tarafi bitti (altyapi, 218 anahtar x 8 dil,
Ayarlar dil secimi UI'si, Editor builder statik metinleri, runtime dinamik
metinler, veri kaynakli metinler). Hepsi CompileCheck'ten gecti; her Loc.Tr
anahtari Strings.tsv'ye karsi dogrulandi (bos kalan yok). Sira Faz 6'da --
ama o faz TAMAMEN Editor'de yapiliyor (Build UI Assets + Build Painter
Habits UI + EditMode test suite + 8 dilde goz ile QA), Claude'un bu oturumda
yapamadigi tek sey. Kullanicinin Editor'u acip o adimlari calistirmasi
gerekiyor -- asagidaki "Faz 6" kontrol listesi sirayla ne yapilacagini
anlatiyor.

# Coklu dil (localization) destegi

## Baglam

Uygulama su an butunuyle Turkce: ~54 dosyada (19 Editor builder + 35 runtime
script) ~250-300 kullanici-gorunur string literal olarak gomulu, ustune 12
kategori adi (depolanan deger == goruntulenen Turkce metin) ve 7 gun kisaltmasi
var. Kullanici 7 yeni dil istiyor: Ingilizce, Fransizca, Almanca, Italyanca,
Portekizce (Brezilya), Rusca, Ispanyolca -- Turkce'yle birlikte toplam 8.
("Isvicre" bir dil degil; kullaniciyla konusulup Fransizca/Almanca/Italyanca'nin
zaten Isvicre'yi kapsadigina, ayri bir girdi gerekmedigine karar verildi.)

Istenen ikinci kisim: ilk acilista `Application.systemLanguage` okunup
desteklenen bir dile eslenmeli (desteklenmiyorsa Ingilizce'ye dusmeli), ve
Ayarlar'dan elle degistirilebilmeli. Amac: hardcoded olmayan, temiz, projenin
var olan "ayna" mimarisine (AppSettings/AppClock/RotusState) ve "Build X"
Editor-araci desenine (UiThemeBuilder, Painting Library builder) uyan bir
cozum.

## Mimari karar

- **Depolama:** `AppSettingsData.language` (yeni `AppLanguage` enum alani,
  varsayilan `Unset = 0`) -- `weekStart` ile AYNI "sonradan eklenen alan,
  saveVersion artmaz" deseni (`PlayerSaveData.cs:62` civari). `Unset` ==
  "hic secilmemis" ayni `WeekStart`/`customCategories` null-okunusu gibi.
- **Ayna:** `AppSettings.Language` (yeni property + `Apply()` icine satir),
  `AppSettings.cs`'teki mevcut 4 alanla AYNI kalip -- yeni bir event sistemi
  YOK, var olan `AppSettings.Changed` kullanilir.
- **Ceviri tablosu:** `LocalizationTable` (yeni ScriptableObject,
  `Assets/UI/LocalizationTable.asset`) -- projede `Resources.Load` hic
  kullanilmiyor (survey'de dogrulandi), bu yuzden UiTheme/PaintingLibrary
  gibi dogrudan `[SerializeField]` referans. Icerik `Assets/Localization/
  Strings.tsv` (tab-separated, elle/Excel'de duzenlenebilir, versiyon
  kontrollu) adli bir kaynak dosyadan yeni bir Editor menu item
  ("Build Localization Table", `UiThemeBuilder`'in yaninda cagrilir --
  "Build UI Assets" adimina eklenir) ile uretilir. Ayni "kaynak dosya ->
  Editor build adimi -> SO asset" ritmi (Build Painting Library From
  Sprites).
- **Okuma:** `Loc.Tr(string key)` (yeni static sinif, `Localization/Loc.cs`)
  -- tabloyu bir `Dictionary<string,string[]>`'e bir kez indeksler (O(1)
  lookup, hot-path kurallariyla uyumlu), `AppSettings.Language`'i canli okur,
  dusme sirasi: secili dil -> Turkce -> anahtarin kendisi (sessizce bos
  birakmak yerine kirik ceviriyi goz onune sokar).
- **Ilk kurulum tespiti:** `SystemLanguageMap.Map(SystemLanguage)` (pure,
  test edilebilir) + `Detect()` sarmalayici. `HabbitManager.ApplyLoadedSave`
  icinde, `AppSettings.Apply(...)`'den ONCE (satir ~252), `language ==
  Unset` ise tespit edilip yazilir -- `DeleteAllData`'da da (satir ~308)
  ayni kontrol, cunku `DeleteAll()` ayarlari sifirliyor ve bu da "yeni kurulum"
  hissi vermeli.
- **Degistirme:** `HabbitManager.SetLanguage(AppLanguage)` -- `SetWeekStart`
  ile birebir ayni kalip (satir 391-400).
- **UI guncellemesi:** `LocalizedText` (yeni, leaf `MonoBehaviour`) --
  `NavMoodBadge`/`RotusStockPill` ile AYNI "dogrudan statik aynaya abone ol"
  deseni: `OnEnable`'da `AppSettings.Changed`'e abone olup `Refresh()`
  (`text.text = Loc.Tr(key)`), `OnDisable`'da abonelikten cik. Merkezi bir
  "tum ekranlari yenile" orkestrasyonu YOK -- her etiket kendi kendini
  yonetiyor, ekranlarin coklugu (3 panel + sheet'ler) bu sayede sorun
  olmuyor.
- **Editor-build-time metinler:** `UiFactory.LocalizedText(name, parent, key,
  font, size, color, align)` (yeni overload) -- `Text()`'i Editor-onizleme
  icin `Loc.Tr(key)` ile cagirir, sonra `LocalizedText` bilesenini ekleyip
  `key`'i baglar. Builder'lar `Loc.Tr` kullanabilsin diye her Editor-build
  calistirmasinin basinda `Loc.Init(tablo)` cagrilir (idempotent, tekrar
  cagrilabilir) -- oyun-disi context'te de tablo hazir olur.
- **Kategori adlari (kritik nokta):** `HabitCategories.Health = "Sağlık"` gibi
  const'lar HEM depolanan deger HEM goruntulenen metin. CLAUDE.md'nin
  "kayitli bir kategori asla yeniden yazilmaz" kuraliyla cakismamak icin
  STORAGE'A DOKUNULMUYOR -- yeni `HabitCategories.DisplayName(string
  category)` sadece GORUNUM icin bilinen built-in const'lari (`Health`,
  `Mind`, `Productivity` dahil -- hala tanindiklari icin) `Loc.Tr("category.
  health")` gibi anahtarlara eşler, taninmayan (ozel/eski) kategori
  degismeden (`category`) doner. Hicbir save migration'i gerekmiyor.
- **WeekDays/DateLabels:** `WeekDays.ShortLabels`/`Initials` sabit dizi
  yerine `ShortLabel(int mondayIndex)`/`Initial(int mondayIndex)` metoduna
  donusur (sabit anahtar switch + `Loc.Tr`) -- "Monday-first indeks" kurali
  AYNEN kaliyor, sadece etiket kaynagi degisiyor. `DateLabels.cs`'teki 2
  literal de `Loc.Tr`'a gecer.
- **Import/export guvenligi:** `SaveTransfer.Sanitize` gecersiz/aralik-disi
  bir `language` degerini `Unset`'e dusurur (bozuk/gelecek-surum ice
  aktarimda cihaz kendi dilini yeniden tespit eder, cokme olmaz).

## Riskler / bilinen sinirlar

- **Metin uzunlugu:** Almanca/Rusca/Portekizce Turkce/Ingilizce'den %30-50
  daha uzun olabilir; butonlar/satirlar Turkce'ye gore dar tasarlandi. Bu
  plan genislikleri yeniden tasarlamiyor -- Faz 6'da gorsel QA ile tespit
  edilip gerekirse (autosize/kirpma) ayri bir takip isi olarak not edilecek.
- **Kapsam:** ~54 dosya, ~300 string -- coklu oturum gerektirebilir. Her
  faz kendi basina derlenebilir/calisir durumda biter, bu yuzden ara
  noktalarda durulabilir.
- Ceviri metinlerini ben (Claude) yazacagim; anadili konusanla gozden
  gecirme onerilir ama kapsam disinda tutuldu.

## Fazlar

- [x] **Faz 0 -- Cekirdek altyapi** (kod tarafi bitti; Editor'de "Build UI Assets" ile
      LocalizationTable.asset uretilip HabbitManager'in yeni "Yerellestirme" slotuna
      ELLE atanmali -- diger PaintingLibrary/viewPool/sfxLibrary alanlariyla ayni,
      builder HabitSystem'e dokunmuyor. Sonra EditMode testleri Editor kapaliyken
      calistirilip dogrulanmali.)
  - `Localization/AppLanguage.cs` (enum: Unset, Turkish, English, French,
    German, Italian, PortugueseBR, Russian, Spanish)
  - `Localization/AppLanguageNames.cs` (native ad donusturucu, WeekDays/
    MoodScale ile ayni "tek donusturucu" sekli)
  - `Localization/SystemLanguageMap.cs` (`Map`/`Detect`)
  - `Localization/LocalizationTable.cs` (ScriptableObject + `LocalizationEntry`)
  - `Localization/Loc.cs` (`Init`, `Tr`, dusme sirasi)
  - `Editor/LocalizationTableBuilder.cs` (TSV -> asset) + "Build UI Assets"
    akisina ekleme
  - `UI/Localization/LocalizedText.cs` (runtime leaf bileseni)
  - `Editor/UI/UiFactory.cs`'e `LocalizedText(...)` overload'u
  - `PlayerSaveData.cs`: `AppSettingsData.language`
  - `AppSettings.cs`: `Language` + `Apply()` satiri
  - `HabbitManager.cs`: `SetLanguage`, `ApplyLoadedSave`/`DeleteAllData`
    icinde ilk-kurulum tespiti
  - `Settings/SaveTransfer.cs`: `Sanitize` icinde `language` araligi kontrolu
  - `Tests/LocalizationTests.cs`: `SystemLanguageMap.Map`, `Loc.Tr` dusme
    sirasi, `AppLanguageNames.DisplayName` kapsama testleri
  - Dogrulama: CLAUDE.md'deki CompileCheck tarifiyle derleme + EditMode
    testleri (Editor kapali)

- [x] **Faz 1 -- Ceviri verisi** -- `Assets/Localization/Strings.tsv` yazildi: 208
  anahtar, 8 dilde (satir basi 9 sutun: key + Turkish/English/French/German/
  Italian/PortugueseBR/Russian/Spanish, AppLanguageNames.Selectable sirasiyla
  AYNI). Anahtar listesi iki arka-plan taramasindan (Editor builder statik
  metinleri + runtime toast/confirm/hata metinleri) ve elle cikarilan veri
  kaynakli metinlerden (12 kategori + 14 haftanin-gunu + 12 ay kisaltmasi)
  derlendi. Editor tarama sonucundaki "[PLACEHOLDER?]" isaretli ~28 girdi
  (prefab varsayilanlari, Bind()/Refresh() tarafindan hemen ezilen) plan
  kuraliyla ("placeholder/sayi metinleri DOKUNULMAZ") dislandi; ayni
  kavramin runtime karsiligi varsa (ornegin habit_tile'daki seri etiketi)
  onun anahtari kullanildi. Iki taramanin da kacirdigi "YAP"/"BIRAK" tip
  rozetleri (Turkce harf isareti icermedikleri icin fark edilmedi) elle
  eklendi: `habit_type.build_pill`/`habit_type.quit_pill`. Dogrulandi:
  her satir tam 9 sutun (`awk -F'\t' '{print NF}' | sort -u` -> sadece 9),
  208 satir, yinelenen anahtar yok, bos satir yok, UTF-8.
  - "Build Localization Table" henuz calistirilmadi -- Editor acildiginda
    "Build UI Assets" ile birlikte calisacak (Faz 0'da zaten wire edildi).

- [x] **Faz 2 -- Ayarlar ekraninda dil secimi** -- kod tarafi bitti, derleniyor
  (CompileCheck gecti):
  - `Scripts/UI/Habits/LanguageChipRow.cs` -- CategoryChipRow'un create-row'suz
    esdegeri, sabit `AppLanguageNames.Selectable` (8) + `DisplayName` ile.
  - `Scripts/UI/Settings/LanguagePickerSheet.cs` -- CategoryPickerSheet kalibi,
    ama navigator kilidi YOK (CategorySettingsSheet/TimePickerSheet gibi,
    panelin KENDI icinde aciliyor) ve hasSelection/disabled-confirm YOK
    (secim her zaman gecerli bir dille acilir).
  - `Editor/UI/LanguagePickerSheetBuilder.cs` -- CategoryPickerSheetBuilder
    sablonu, 2 sutunlu cip izgarasi (dil adlari kategori adlarindan uzun
    olabildigi icin 3 degil 2 sutun), `prefabs.CategoryPill` cipini yeniden
    kullaniyor (ikinci bir prefaba gerek yok).
  - `SettingsPanelBuilder.cs`: "Dil" satiri -- `SettingsRowBuilder.ValueRow`
    ("Saat" satiriyla AYNI sekil: deger sagda gosterilip dokunulabilir),
    Takvim ile Kategoriler bloklari arasina eklendi.
  - `SettingsScreen.cs`: `HandleWeekStartPicked` kalibinda wiring +
    `manager.SetLanguage(...)`, `RefreshAll`'da `languageValueLabel.text =
    AppLanguageNames.DisplayName(settings.language)`.
  - Yeni UI metinleri Strings.tsv'ye eklendi (`settings_section.language`,
    `language_picker.title`, `language_picker.confirm_button`); Faz 3'un ayni
    oturumda tamamlanmasiyla bu satirlar da LocalizedText'e gecti (asagiya bkz.).
  - Bekleyen: "Build Painter Habits UI" ile sahne/prefab yeniden uretimi +
    Ayarlar'da dil degistirip canli guncellemeyi goz ile dogrulama (Editor
    acilmadan yapilamiyor).

- [x] **Faz 3 -- Editor builder'lardaki statik metinler** -- kod tarafi bitti,
  CompileCheck gecti, her LocalizedText/Section/Row cagrisindaki anahtar
  Strings.tsv'de dogrulandi (bos kalan yok):
  - 15 dosyada (ArchiveSheetBuilder, BottomNavBuilder, CategoryPickerSheetBuilder,
    CategorySettingsSheetBuilder, GalleryDetailBuilder, GalleryPanelBuilder,
    HabitsPanelBuilder, LanguagePickerSheetBuilder, PaintingChoiceSheetBuilder,
    RotusRestoreSheetBuilder, SettingsPanelBuilder, SettingsRowBuilder,
    StreakCelebrationBuilder, TimePickerSheetBuilder, WorkshopSceneBuilder)
    genuinely-static literalleri `UiFactory.LocalizedText`'e tasindi; ortak
    satir/bolum fabrikalari (`SettingsRowBuilder.Section/Row/SubLabel`,
    `HabitsPanelBuilder.Section/BuildInput/SpawnOption`) KEY alan parametrelere
    donusturuldu ki her cagiran otomatik kazansin.
  - **Yeni bulgu:** `SettingsRowBuilder.Section`/`HabitsPanelBuilder.Section`
    baslikları eskiden Editor-build-time'da TEK SEFERLIK `.ToUpperInvariant()`
    uyguluyordu; LocalizedText'in deger her dil degisiminde YENIDEN
    cozuldugu icin bu donusum artik `LocalizedText.uppercase` alaninda
    (ve `UiFactory.LocalizedText`'in `uppercase` parametresinde) REACTIVE
    olarak tekrarlaniyor -- yoksa dil degisince baslik kucuk harfe donerdi.
    "FİLTRELER"/"PLAN"/"RÖTUŞ" gibi zaten-buyuk-harf literalli anahtarlar
    TSV'de HAZIR buyuk harfle yazili oldugu icin `uppercase` bayragini
    almiyor (cift donusum olmasin).
  - **Placeholder/toggle-driven kuralinin genisletilmesi:** plan'in
    "placeholder/sayi metinleri DOKUNULMAZ" kuralini, Editor varsayilani
    runtime kodu tarafindan HER ZAMAN DURUM BAZLI ezilen etiketlere
    (habit_sheet.title_new/edit, common.archive_button/unarchive_button,
    common.add_button/save_button, rotus_restore.primary_*,
    streak_celebration.title format-string'i) de uyguladik -- bunlari Faz 3'te
    LocalizedText'e cevirmek, Faz 4 ayni runtime dosyayi (HabitSheet.cs,
    RotusRestoreSheet.cs, StreakCelebration.cs) ELE ALMADAN hicbir ise
    yaramazdi (runtime .text= atamasi LocalizedText'i her zaman ezer).
  - Bekleyen: "Build Painter Habits UI" ile tum prefab/sahne kokleri yeniden
    uretilir, goz ile dogrulama (Editor acilmadan yapilamiyor).

- [x] **Faz 4 -- Runtime dinamik metinler** -- kod tarafi bitti, CompileCheck
  gecti, her `Loc.Tr` cagrisindaki anahtar Strings.tsv'de dogrulandi:
  - Donusturulen dosyalar: `MoodPresentation.cs`, `CanvasSizeOption.cs`,
    `CanvasSizes.cs`, `HabitCard.cs`, `UiTheme.cs` (`TypeLabel`),
    `HabitFilters.cs` (`Label`), `CategorySettingsSheet.cs`,
    `GalleryDetailView.cs`, `GalleryHabitCard.cs`, `GalleryWorkTile.cs`,
    `GalleryWallItem.cs`, `GalleryPaintingViewer.cs`, `RotusRestoreSheet.cs`,
    `StreakCelebration.cs`, `WorkshopScreen.cs`, `CategoryPickerSheet.cs`,
    `CategoryChipRow.cs`, `HabitSheet.cs`, `PaintingOption.cs`,
    `PaintingChoiceSheet.cs`, `SaveTransfer.cs`, `NativeFileBridge.cs`/
    `.Payload.cs`, `SaveExportService.cs`, `ReminderService.cs`,
    `SettingsScreen.Data.cs`, `SettingsScreen.cs`, `HabitsScreen.cs`,
    `ArchiveRow.cs`, `HabitForm.cs`.
  - **Yeni bulgulari:** diyakritik-tabanli ilk taramanin kacirdigi, Turkce
    diyakritigi OLMAYAN literaller ayrica elle bulundu ve eklendi --
    `UiTheme.TypeLabel` ("YAP"/"BIRAK" -> `habit_type.build_pill`/
    `quit_pill`), `WorkshopScreen.cs`'in 2 toast'u, `HabitForm.DaysHint`
    ("{0} gün seçili" -> `habit_sheet.days_hint_selected`),
    `ArchiveRow.cs` ("seri {0}" -> `archive_sheet.row_streak`),
    `HabitsScreen.cs` ("{0} aktif" -> `habits.active_count_label`).
    Toplam 7 yeni anahtar bu faz sirasinda eklendi (simdiye kadar 218).
  - `HabitFilters.Label`/`CategorySettingsSheet.RefreshFilterChips`
    arasindaki "Yap"/"Bırak" ikilemesi (fork2'nin flagladigi) `habit_type.
    build`/`quit` ile birlesti -- iki kopya degil, tek kaynak.
  - `NativeFileBridge.cs`'in `#if UNITY_EDITOR` dalindaki Windows dosya
    diyalog basliklari ("Yedegi kaydet"/"Yedek dosyasi sec") BILEREK
    Turkce kaldi -- bu dal hic bir gercek kullaniciya ulasmiyor, yalnizca
    Editor'de cihazsiz dogrulama icin (ayni sinif Debug.Log'lar gibi
    kapsam disi).

- [x] **Faz 5 -- Veri kaynakli metinler** -- kod tarafi bitti, CompileCheck
  gecti:
  - `HabitCategories.DisplayName(string)` eklendi; bilinen yerlesikleri
    (Mind/Productivity DAHIL) `category.*` anahtarlarina esler, taninmayan
    (ozel) kategoriyi degismeden doner. STORAGE'a (`categoryGroup`) hic
    dokunulmuyor -- CLAUDE.md'nin "kayitli bir kategori asla yeniden
    yazilmaz" kurali korunuyor.
  - Cagiran taraflar guncellendi: `CategoryChipRow.Show` (cip etiketi),
    `CategorySettingsSheet.RefreshFilterChips` (filtre etiketi),
    `HabitSheet.categoryValueLabel`, `GalleryDetailView.subLabel`,
    `HabitFilters.Label` (Kind.Category durumu) -- hepsi DisplayName
    uzerinden GORUNUYOR, secim/karsilastirma/kayit hala raw string.
  - **Kritik duzeltme:** `HabitCategorySection.cs`'teki Turkce'ye-ozgu
    dotted-İ duzeltmesi (`category.Replace('i','İ').ToUpperInvariant()`)
    simdi `AppSettings.Language == AppLanguage.Turkish` ile KAPILANDI --
    aksi halde Ingilizce "Finance" gibi bir ceviri "FİNANCE" olarak
    bozulurdu (Turkce disi metne Turkce harf donusumu uygulanirdi).
  - `WeekDays.ShortLabels`/`Initials` sabit dizileri `ShortLabel(int)`/
    `Initial(int)` metotlarina donustu (`Loc.Tr("weekday." + mondayIndex +
    ".short"/".initial")`) -- `LabelForSlot`/`InitialForSlot` bunlari
    cagiriyor, "Monday-first indeks" kurali AYNEN kaliyor. `Editor/Dev/
    DevToolsEditor.cs`'in Inspector-ici (sahnesiz, Play modu disi) gun
    etiketi BILEREK kendi Turkce-sabit kucuk dizisini kullanmaya devam
    ediyor -- Loc o baglamda initialized olmayabilir ve bu arac kapsam
    disi (yalnizca gelistiricinin kendi makinesinde).
  - `DateLabels.Format`'taki 12 ay kisaltmasi `Loc.Tr("month." + ay)`'a
    gecti -- gun-ay-yil SIRASI butun dillerde ayni kaliyor.
  - **Test uyarlamalari:** `SettingsLogicTests.WeekDays_Sunday_SlotZeroIsPazar`
    artik literal "Pz" beklemiyor (Loc init'siz "Pz" donmez) -- mondayIndex
    == 6 + ShortLabel/LabelForSlot tutarliligini kontrol ediyor.
    `GalleryLogicTests.cs`'teki 3 `DateLabels` testi kendi gecici Turkce
    ay tablosunu kurup `[TearDown]`'da birakiyor (Tests/LocalizationTests.cs
    ile AYNI kalip). `Tests/LocalizationTests.cs`'e `HabitCategories.
    DisplayName`/`WeekDays.ShortLabel`/`Initial` icin 4 yeni test eklendi.

- [ ] **Faz 6 -- Dogrulama**
  - CompileCheck derlemesi + tum EditMode test suite'i (Editor kapali)
  - Editor'de 8 dilin hepsinde: Workshop/Habits/Gallery/Settings panelleri
    + acilan sheet'ler gozle kontrol (kirpilma/tasma icin)
  - Herhangi bir layout sabiti degismesi gerekiyorsa once
    `ResponsiveLayoutTests.cs` matrisine eklenir (CLAUDE.md kurali)
  - Yeni kullanici: dil secilmemisken ilk acilista sistem dili testi
    (Dev Tools ile ya da Inspector'dan `AppSettingsData.language`'i
    `Unset` yapip calistirarak)
