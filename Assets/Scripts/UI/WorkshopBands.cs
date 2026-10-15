using UnityEngine;

// 01 Atolye ekraninin DIKEY bestesi -- saf aritmetik.
//
// Bu hesap eskiden WorkshopLayout.Apply() icinde Screen.* okumalariyla ic ice
// duruyordu ve test edilemiyordu. Tam da burada kirilan bir degismez vardi:
// tablette izgaraya yarim kutu satiri bile kalmiyordu. Artik cikartilmis
// durumda, Tests/ResponsiveLayoutTests bir cihaz matrisi uzerinde
// GridHeight >= MinGridHeight'i dogruluyor.
//
// Iki farkli dikey uzay var ve karistirmak sahneyi centik kadar kaydirir:
//   - "ekran derinligi": ekranin tepesinden olculuyor, studyo burada yasiyor.
//   - "panel koordinati": guvenli alanin tepesinden olculuyor, chrome burada.
// Ikisi arasindaki ceviri safeTop.
//
// Serit kisaldiginda studyo KUCULTULMEZ, YUKARI KAYDIRILIR (ShiftUp): ressam,
// sovale ve golge birbirine gore tasarimdaki yerinde kalir, yalnizca duvarin
// tepesi kirpilir. Olcegi degistirmek piksel-art'i bozardi.
public static class WorkshopBands
{
    // Teslim dosyasindaki olculer (tasarim pikseli).
    public const float SceneHeight = 452f;
    public const float FilterOverlap = 58f;
    public const float FilterHeight = 92f;
    public const float SkirtHeight = 34f;
    public const float HeaderTop = 56f;
    public const float HeaderMinTop = 10f;

    // Basligin iki yanindaki bosluk ve kose rozetlerinin olcusu (ayar tusu
    // ile gunluk sayac). Tek sabit: hem rozetleri yerlestiren builder hem de
    // tam genisligi kullanan mod satiri buradan okuyor.
    public const float HeaderSideMargin = 18f;
    public const float HeaderBadgeSize = 38f;

    // Baslik artik IKI satir (Faz 3, AltNavigasyon+Ayarlar teslimi): ust
    // satirda "Atolye / Vincent" yazi blogu (ve iki kose rozeti), altinda mod
    // satiri. TitleBlockHeight 02/03'teki ayni TitleBlock ile birebir (14pt
    // alt baslik + 28pt ad).
    public const float TitleBlockHeight = 46f;

    // Yazi blogu ile mod satiri arasi. Eskiden "MoodRowBadgeGap" idi (rozetle
    // satir arasi YATAY bosluk, mod satiri rozetlerin ARASINDAYDI); mod
    // satiri artik basligin ALTINDA kendi bandinda, yani anlam DIKEY bosluga
    // donuyor -- tek sabit, tek anlam.
    public const float MoodRowGap = 6f;

    // HeaderHeight TURETILMIS: TitleBlockHeight + MoodRowGap + MoodCircleSize.
    // 46 diye sabit yazmiyoruz cunku iki satirin toplami otomatik hesaba
    // katilsin -- MaxMoodPoint'in MoodTable.Length'ten turetilmesiyle AYNI
    // gerekce (bkz. CLAUDE.md). = 82.
    public const float HeaderHeight = TitleBlockHeight + MoodRowGap + MoodCircleSize;

    // Ressam sprite kutusunun TASARIMDAKI tepesi (WorkshopSceneBuilder.
    // PainterTop ile AYNI sayi, saf tarafta tekrarlandi cunku Editor assembly'si
    // Runtime'a referans veriyor, tersi degil).
    public const float PainterTopDesign = 202f;

    // Basligin altiyla ressamin kafasi arasinda birakilacak en az bosluk.
    public const float PainterHeaderClearance = 4f;

    // Mod gostergesi artik basligin TAMAMINI kullaniyor (kose rozetlerinin
    // ARASINI degil): kendi bandinda, rozetler ustteki satirda.
    //
    // 6 daire x 30 + 5 x 6 = 210 birim, 330 birimlik baslik genisliginin
    // icine 60 birim paylasimli bosluk payla oturuyor.
    public const float MoodCircleSize = 30f;
    public const float MoodCircleGap = 6f;

    // Atolye izgarasindaki bir tile'in KARE gorselinin ALTINDAKI metin bandi:
    // baslik + seri cipi. Bu bandin yuksekligi iki ayri yerde lazim --
    // UiPrefabBuilder.BuildHabitTile prefabin "Info" rect'ini bununla kuruyor,
    // WorkshopSceneBuilder.BuildTileGrid ayni sayiyi GridCellFitter.extraHeight
    // olarak veriyor. Iki yerde elle yazilsalardi hucre ile icerik sessizce
    // ayrisir ve cip tasardi; bu yuzden tek kaynak burasi.
    //
    // Seri alevi teslimiyle 40 -> 56 bueyudu: eskiden burada tek satir metin
    // ("12 gun seri", 14 birim) vardi, simdi 24'luk alevi tasiyan 28 birimlik
    // bir cip var. Hesap: 7 (ust dolgu) + 17 (baslik) + 3 (bosluk) + 28 (cip)
    // = 55, bire yuvarlanarak 56.
    //
    // Bu sayi DIKEY BANT COZUMUNUN girdisi DEGIL (Solve'a hic girmiyor) --
    // yalnizca kaydirilan izgaranin hucre yuksekligi, yani ResponsiveLayoutTests
    // matrisini etkilemiyor; etkisi "kisa ekranda ayni anda kac satir goruluyor".
    public const float TileInfoHeight = 56f;

    // Satirin basligin icinde kullanabilecegi genislik: basligin TAMAMI
    // (kose rozetleri ustteki satirda, bu satiri daraltmiyorlar).
    public static float MoodRowAvailableWidth(float contentWidth)
    {
        return contentWidth - 2f * HeaderSideMargin;
    }

    // Satirin kaplayacagi genislik: daireler + aralari.
    public static float MoodRowWidth(int circleCount)
    {
        if (circleCount <= 0)
        {
            return 0f;
        }

        return circleCount * MoodCircleSize + (circleCount - 1) * MoodCircleGap;
    }

    // Sahne serit yuksekliginin alt siniri: bundan asagisinda ressamin kafasi
    // basligin altina girer. TURETILMIS (Faz 3): baslik 46'dan 82'ye cikinca
    // elle yazilmis 372 gecersiz olurdu -- ressamin kafasi basligin 18 birim
    // icine girerdi. = 452 - 202 + 56 + 82 + 4 = 392.
    public const float MinSceneHeight = SceneHeight - PainterTopDesign + HeaderTop + HeaderHeight + PainterHeaderClearance;

    // Izgaraya birakilan en az yer: bir tile satirinin gorunur parcasi.
    // ELLE yazilir (TURETILMEZ) -- MinSceneHeight'ten turetilseydi
    // Tests/ResponsiveLayoutTests'teki dogrulama totoloji olurdu. 134 = 650 -
    // 392 - 34 (SkirtHeight) - 90 (BottomNavBands.DesignHeight); 12 cihazlik
    // matriste gorulen en kotu durum.
    public const float MinGridHeight = 134f;

    // Hicbir kosulda bunun altina inilmiyor -- MinSceneHeight'i bile ezer.
    public const float HardMinGridHeight = 40f;

    // Cozulmus bantlar. readonly struct: cagiran taraf her yerlesim
    // gecirgeninde bunu uretiyor, tahsis olmamali.
    public readonly struct Result
    {
        // Sahne seridinin ekran derinligindeki bitisi.
        public readonly float SceneBottomScreen;

        // Studyonun ekranin USTUNE kaydirilacagi miktar.
        public readonly float ShiftUp;

        // Sahne seridinin panel koordinatindaki bitisi.
        public readonly float SceneBottom;

        // Panel koordinatinda chrome ofsetleri.
        public readonly float HeaderTop;
        public readonly float FilterTop;
        public readonly float SkirtTop;
        public readonly float GridTop;

        // Izgaraya kalan yukseklik. Degismez: >= MinGridHeight.
        public readonly float GridHeight;

        public Result(
            float sceneBottomScreen,
            float shiftUp,
            float sceneBottom,
            float headerTop,
            float filterTop,
            float skirtTop,
            float gridTop,
            float gridHeight)
        {
            SceneBottomScreen = sceneBottomScreen;
            ShiftUp = shiftUp;
            SceneBottom = sceneBottom;
            HeaderTop = headerTop;
            FilterTop = filterTop;
            SkirtTop = skirtTop;
            GridTop = gridTop;
            GridHeight = gridHeight;
        }
    }

    // available    : panelin (guvenli alanin) yuksekligi, tasarim birimi.
    // safeTop      : panelin tepesi ekranin tepesinden ne kadar asagida.
    // bottomReserve: panelin dibinde chrome disi bir bilesene (alt
    //                navigasyon cubugu) ayrilan pay -- izgaranin GUVENLI
    //                ALAN ustundeki kendi payi, Screen.safeArea'nin zaten
    //                temizledigi inset'le KARISTIRILMAMALI (bkz.
    //                NAVIGATION_SETTINGS_PLAN.md Faz 2, "Kesiften cikan
    //                gercekler" 4. madde). Izgara bunu dogrudan kaybediyor;
    //                sahne seridi/kaydirma hesabi bundan HABERSIZ -- studyo
    //                cubugun altina gecmiyor, yalnizca ustunde kaliyor.
    public static Result Solve(float available, float safeTop, float bottomReserve)
    {
        // Sahne seridinin ekrandaki bitis derinligi. Izgaraya en az
        // MinGridHeight kalsin; tasarim yuksekligini asmasin; ressami basligin
        // altina sokmasin.
        float sceneBottomScreen = Mathf.Clamp(
            safeTop + available - bottomReserve - SkirtHeight - MinGridHeight,
            MinSceneHeight,
            SceneHeight);

        // Cok kisa bir ekranda MinSceneHeight izgaraya hic yer birakmayabilir.
        // Orada alt sinir degil izgara kazaniyor: aksi halde kaydirma alani
        // TERS yukseklikle cizilirdi. LayoutMetrics kanvasi 650 birimin altina
        // dusurmedigi icin bu dal artik yalnizca patolojik olculerde calisiyor,
        // ama guvenlik agi olarak duruyor.
        sceneBottomScreen = Mathf.Max(0f, Mathf.Min(
            sceneBottomScreen,
            safeTop + available - bottomReserve - SkirtHeight - HardMinGridHeight));

        // Studyoyu tam bu kadar yukari kaydiriyoruz: tasarimin 452. satiri her
        // zaman seridin bitisine oturuyor, yani filtre satiri her ekranda ayni
        // yeri (394-452) ortuyor ve golge acikta kaliyor.
        float shiftUp = SceneHeight - sceneBottomScreen;

        // Panel koordinatina cevir: panelin tepesi safeTop derinliginde.
        float sceneBottom = sceneBottomScreen - safeTop;

        // Tasarimda baslik ekranin tepesinden 56 px asagida; centik zaten o
        // boslugun bir kismini yiyor, ustune bir de 56 eklemek basligi
        // gereksiz yere asagi itiyordu.
        float headerTop = Mathf.Max(HeaderMinTop, HeaderTop - safeTop);

        float gridTop = sceneBottom + SkirtHeight;

        // Mod satiri icin burada hesap YOK: satir basligin cocugu, yani
        // basligin rect'i nereye giderse onunla gidiyor. Guvenli alan isi zaten
        // headerTop'ta cozuldu.

        return new Result(
            sceneBottomScreen,
            shiftUp,
            sceneBottom,
            headerTop,
            sceneBottom - FilterOverlap,
            sceneBottom,
            gridTop,
            available - bottomReserve - gridTop);
    }
}
