using System;
using System.Collections.Generic;

// Kaydin tamami. JsonUtility ile tek bir string'e serialize edilip PlayerPrefs'e
// yaziliyor; acilista tek okumayla geri geliyor.
[Serializable]
public class PlayerSaveData
{
    public const int CurrentSaveVersion = 1;

    // Sonradan eklenen bir alan JsonUtility'de varsayilaniyla gelir, ama var olan
    // bir alanin ANLAMI degisirse bunu bilmenin baska yolu yok.
    public int saveVersion = CurrentSaveVersion;

    // "yyyy-MM-dd", YEREL tarih: reset kullanicinin gece yarisinda olmali, UTC'de degil.
    public string lastLoginDate;

    public List<HabitData> habits = new List<HabitData>();

    // Ressamin mood puani. PainterManager bunu bellekte tutuyor ama uygulama
    // kapaninca kaybolmamali: gunlerce suren bir dususun anlami yok.
    public int painterMoodPoint = PainterManager.DefaultMoodPoint;

    // Mood puaninin ISLENDIGI en son gun. Ayni gun icin iki kez puan verilmesini
    // engelliyor: gun icinde hepsi tiklenince odul verilip gun damgalanir, gece
    // yarisindaki gun sonu kontrolu damgayi gorup tekrar ceza/odul vermez.
    public string lastMoodSettledDate;

    // Rotus sarf malzemesi stogu (global, aliskanliga ozel degil). Bir rotus, bitmis
    // bir galeri tablosundaki bir deligi onarir (bkz. HabitGallery.RestoreOne).
    // saveVersion BILEREK artmiyor -- settings/missedPixels ile ayni gerekce: eski
    // kayitta bu alan yok, JsonUtility varsayilanini (0) birakiyor ve bu zaten
    // dogru okuma ("hic rotus kazanilmamis").
    public int rotusCount;

    // Kullanicinin kalici ayarlari (AltNavigasyon+Ayarlar teslimi). saveVersion
    // BILEREK artmiyor -- galleryHoles/missedPixels ile ayni gerekce: eski bir
    // kayitta bu alan yok, JsonUtility varsayilanini (null) birakiyor ve
    // HabitSaveSystem.RepairHabits o varsayilani "ayar yapilmamis, varsayilanlari
    // kullan" olarak okuyor. Bu zaten dogru davranis, ayri bir migrasyona gerek yok.
    public AppSettingsData settings = new AppSettingsData();
}

// Painter adi, hatirlatma, ses/titresim, hafta baslangici ve premium durumu.
// soundOn=true, hapticsOn=true, weekStart=Monday UYGULAMANIN BUGUNKU
// davranisiyla ayni -- bir ayar hic acilmadan kaydedilse bile hicbir sey
// degismis gibi gorunmemeli. reminderOn ISTISNA: varsayilani KAPALI (false).
// Izin yalnizca kullanici toggle'i ACTIGI anda istenecek (bkz. REMINDER_PLAN.md);
// toggle true doguyorsa kullanicinin ilk hareketi onu kapatmak olur, izin hic
// istenmez ve Ayarlar "acik" yazar ama OS hicbir bildirim teslim etmez.
[Serializable]
public class AppSettingsData
{
    public const string DefaultPainterName = "Vincent";
    public const string DefaultReminderTime = "21:00";

    public string painterName = DefaultPainterName;
    public bool reminderOn;
    public string reminderTime = DefaultReminderTime;
    public bool soundOn = true;
    public bool hapticsOn = true;
    public WeekStart weekStart = WeekStart.Monday;

    // Uygulama dili. Varsayilan Unset == "hic secilmemis" -- WeekStart'in
    // aksine burada "bugunku davranis" diye bir varsayilan YOK: ilk acilista
    // HabbitManager.ApplyLoadedSave icinde SystemLanguageMap.Detect ile
    // doldurulur (bkz. LOCALIZATION_PLAN.md). Sonradan eklenen alan: ayni
    // "saveVersion artmaz" gerekcesi (yukaridaki sinif yorumu) -- eski bir
    // kayitta bu alan yok, JsonUtility varsayilanini (0 == Unset) birakir ve
    // bu zaten dogru okuma ("hic secilmemis, tespit calissin").
    public AppLanguage language = AppLanguage.Unset;

    // Uretimde magaza makbuzundan turetilmeli (teslim dosyasinin kendi notu) --
    // PremiumService.Purchase su an icin bir hook, bkz. NAVIGATION_SETTINGS_PLAN.md Faz 5.
    public bool isPremium;

    // Kullanicinin kendi ekledigi kategori adlari. Ayni "sonradan eklenen alan"
    // gerekcesiyle saveVersion ARTMADI (bkz. yukaridaki sinif yorumu) -- eski
    // kayitta bu alan hic yok, JsonUtility null birakir, RepairSettings bunu
    // "hic ozel kategori yok" olarak okur.
    public List<string> customCategories = new List<string>();

    // Ana sayfa filtre satirinin gosterdigi token listesi (bkz. HabitFilters.
    // Token/TryParse). Ucretsiz bir ozellik -- PremiumService'e hic dokunmuyor.
    // Bos liste MESRU bir durum degil: Tumu her zaman var olmali, bu yuzden
    // RepairSettings bos/null geleni HabitFilters.DefaultTokens'a dusurur.
    public List<string> filterTokens = new List<string>();
}

[Serializable]
public class HabitData
{
    public const int DefaultCanvasSize = 32;

    // Kimlik ve kurallar
    public string id;
    public string title;
    public string categoryGroup;
    public string habitType; // HabitTypes.Build / HabitTypes.Quit
    public List<DayOfWeek> frequency = new List<DayOfWeek>();
    public bool hasEndDate;
    public string startDate;
    public string endDate;

    // Takip verileri
    public bool isCompletedToday;
    public int currentStreak;
    public int longestStreak;

    // Streak'in kirilip kirilmadigini bilmek icin sart: kullanici uygulamayi iki
    // gun hic acmazsa, "son tamamlanan gun" olmadan kacirilan planli gun tespit
    // edilemez ve streak yanlis devam ederdi. "yyyy-MM-dd".
    public string lastCompletedDate;

    // --- Seri alevi kutlamasi ---
    //
    // En son KUTLANAN alev basamagi ve kutlamanin yapildigi gun. Ikisi birlikte
    // tek bir isi yapiyor: ayni gun icinde tiki geri alip tekrar atmak kutlamayi
    // IKINCI kez acmasin. Yalnizca basamak saklansaydi, seri kirilip sifirdan
    // yeniden ayni basamaga tirmanmak bir daha hic kutlanmazdi -- tarih,
    // bastirmayi o gune sinirliyor (bkz. StreakFlame.ShouldCelebrate).
    //
    // saveVersion BILEREK artmiyor: missedPixels/galleryHoles/settings ile ayni
    // gerekce -- eski bir kayitta bu alanlar yok, JsonUtility varsayilanlarini
    // (0 / null) birakiyor ve ikisi de zaten "henuz hic kutlanmadi" demek.
    // RepairHabits'e de dokunmaya gerek yok: bunlar List<> degil.
    public int lastCelebratedTier;
    public string lastCelebratedDate;

    // --- Rotus dongusu ---
    //
    // Bu aliskanligin 7 tiklik rotus dongusunde kactik: 0..RotusCycle.CycleLength-1.
    // Dongu doldugunda (7. tik) PlayerSaveData.rotusCount bir artar ve bu sayac
    // 0'a doner (bkz. RotusCycle.Advance). Tik geri alinirsa sayac da geri alinir
    // (bkz. RotusCycle.RollBack) -- lastCelebratedTier/Date'in "geri alinabilir
    // olmali" dersinin aynisi, yoksa tik/geri-al ciftligi bedava rotus basar.
    //
    // saveVersion BILEREK artmiyor: lastCelebratedTier ile ayni gerekce, bu bir
    // List<> degil, eski kayitta 0 zaten dogru okuma ("donguye hic girilmemis").
    public int rotusProgress;

    // --- Arsiv ---
    //
    // Kullanici bu aliskanligi gecici olarak rafa kaldirdi. Arsivdeyken
    // HabitScheduling.IsDueOn false doner: mood cezasi yazilmaz, tablo parcasi
    // yanmaz, seri DailyResetService.BreakMissedStreaks tarafindan atlanir
    // (bkz. HabitArchive.cs). Tarih ya da aralik tutulmuyor -- arsivleme
    // yalnizca acik bir oturumda yapilabilir ve o oturumun Load() cagrisi
    // zaten butun bosluklari arsivlemeden ONCE kapatmis oluyor, yani bir
    // tarih hicbir hesaba dogruluk katmaz, sadece gosterim dizesi olurdu.
    //
    // saveVersion BILEREK artmiyor: rotusProgress ile ayni gerekce, bu bir
    // List<> degil, eski kayitta false zaten dogru okuma ("hic arsivlenmemis").
    public bool isArchived;

    // --- Gunluk tur sayisi ---
    //
    // Bu aliskanligin bir gunu kapanmasi icin kac tik gerekiyor (1 == bugunku
    // davranis, "kediyle oyna 2 tur" icin 2). Tek okuma kapisi HabitReps.Target --
    // elle bozulmus/eski bir kayitta 0 gelirse orada 1 olarak okunur.
    //
    // saveVersion BILEREK artmiyor: rotusProgress/isArchived ile ayni gerekce --
    // eski kayitta bu anahtar hic yok, JsonUtility alan baslaticisini (1)
    // birakiyor ve bu zaten dogru okuma ("gunde tek tik"). canvasSize ile ayni
    // kalip; List<> olmadigi icin RepairHabits'in null-onarimina girmiyor.
    public int targetPerDay = 1;

    // Bugun atilan tik sayisi. OTORITE DEGIL: gunun kapandigini hala yalnizca
    // isCompletedToday soyluyor (mood, streak, sovale sayaci, filtreler hepsi
    // oraya bakiyor). Degismeyen soz (bkz. HabitReps):
    //   isCompletedToday == true  ->  todayReps == hedef
    //   isCompletedToday == false ->  todayReps <  hedef
    // Gun donusunde isCompletedToday ile AYNI dongude sifirlaniyor
    // (DailyResetService.ApplyDailyReset).
    public int todayReps;

    // --- Tablo siparisi ve ilerlemesi ---
    public string activePaintingId;

    // DIKKAT -- bu PARCA SAYISIDIR, kenar uzunlugu degil. PaintPixels izgarayi
    // sabit 8 sutunla kuruyor (16 -> 8x2, 32 -> 8x4, 64 -> 8x8), yani
    // TotalPieces == canvasSize == PaintPixels.OverlayResolution.
    public int canvasSize = DefaultCanvasSize;

    // Sirayla (Sequential) acilan tablolar icin toplam sayi.
    public int totalPixelsPainted;

    // Rastgele (Random) acilan tablolar icin indeks listesi.
    public List<int> randomOpenedPixels = new List<int>();

    // Bu aliskanliktan bitirilip koleksiyona eklenen eserler.
    public List<string> habitGallery = new List<string>();

    // Aktif tablo bitti ve galeriye girdi, ama yerine YENISI henuz atanmadi:
    // siradakini kullanici seciyor (bir sonraki tik girisiminde acilan secim
    // kutusundan). Bayrak acikken activePaintingId hala BITEN tabloyu gosteriyor ve
    // ilerleme dolu kaliyor -- tablo ekranda tam haliyle kalsin diye.
    public bool awaitingPaintingChoice;

    // Planli oldugu halde yapilmayan gunlerde KALICI kapanan parcalar. acik
    // parcalardan AYRI tutuluyor: bir daha acilamazlar ve tabloda farkli bir
    // tonda cizilirler (bkz. PaintingPieceState, PaintPixels "Locked").
    //
    // MissedPieceService disinda hicbir yer buraya yazmaz.
    public List<int> missedPixels = new List<int>();

    // Bitirilmis tablolarin delikleri. SEYREK: yalnizca deligi OLAN tablo icin
    // girdi var. Galeri UYELIGI habitGallery'nin isi -- burada bir id bulunmasi
    // uyelik anlamina gelmez, yalnizca o id'nin delikli bittigini kaydeder.
    public List<PaintingHoles> galleryHoles = new List<PaintingHoles>();

    // Aktif tabloda su ana kadar acilmis parca sayisi. Hangi alanin gecerli
    // oldugunu tablonun reveal sirasi belirliyor (bkz. PaintingDefinition):
    // Sequential tek sayiyla, Random indeks listesiyle saklaniyor.
    //
    // Bu kuralin yazili oldugu TEK yer burasi; PaintingProgress.GetOpenedCount
    // de buraya gelir.
    public int OpenedPieceCount(bool sequential)
    {
        return sequential ? totalPixelsPainted : randomOpenedPixels.Count;
    }

    // Acik + yakilmis. "Tablo bitti" esigi artik buna bakiyor: delikli bir
    // tablo da canvasSize'a ulasinca biter.
    public int ConsumedPieceCount(bool sequential)
    {
        return OpenedPieceCount(sequential) + missedPixels.Count;
    }
}

// Bitmis bir tablonun delikleri. paintingId habitGallery'deki id ile ayni --
// galeri uyeligi orada, delik listesi burada; ikisi ayri tutuluyor cunku
// deliksiz bitmis tablolarin (coklugu) icin bu liste hic buyumuyor.
[Serializable]
public class PaintingHoles
{
    public string paintingId;
    public List<int> pieces = new List<int>();
}

// habitType string olarak saklaniyor (verilen model boyle), ama kodun icinde
// string karsilastirmasi dolasmasin diye tek gecis noktasi burasi.
public static class HabitTypes
{
    public const string Build = "Build";
    public const string Quit = "Quit";

    public static bool IsQuit(HabitData habit)
    {
        return string.Equals(habit.habitType, Quit, StringComparison.Ordinal);
    }
}
