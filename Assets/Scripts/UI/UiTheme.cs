using System;
using TMPro;
using UnityEngine;

// Tasarimin butun renkleri, sprite'lari ve fontlari tek bir varlikta.
//
// UI hiyerarsisi Editor builder'i tarafindan SIFIRDAN uretiliyor; uretilen
// prefab'lari elle duzenlemek bir sonraki calistirmada kayboluyor. Gorunusu
// degistirmenin dogru yolu bu varligi duzenleyip builder'i tekrar calistirmak --
// yani burasi elle duzenlenebilen TEK yuzey.
[CreateAssetMenu(fileName = "UiTheme", menuName = "HabitTracker/UI Theme")]
public class UiTheme : ScriptableObject
{
    // Yuvarlak kose sprite'lari yariçapa gore araniyor. Dizi, alan basina bir
    // Sprite tutmaktan iyi: yeni bir yariçap eklemek builder'a bir satir.
    [Serializable]
    public struct RoundedSprite
    {
        public int radius;
        public Sprite sprite;
    }

    // Mod basina bir gorsel. Dizi + mod alani, duz Sprite[] degil: Inspector'da
    // "Element 3" yerine hangi satirin hangi moda ait oldugu yaziyor, yani
    // sprite yanlis yuvaya dusmuyor. RoundedSprite ile ayni desen.
    [Serializable]
    public struct MoodIcon
    {
        public PainterManager.PainterMoods mood;
        public Sprite sprite;
    }

    // Seri alevinin bir basamagi: uc animasyon sayfasi (her biri 4 kare) + iki
    // renk. MoodIcon/RoundedSprite ile ayni desen -- "Element 3" yerine hangi
    // satirin hangi basamak oldugu Inspector'da okunuyor.
    //
    // Sprite dizileri BUILDER tarafindan yaziliyor (teslim klasorunden gelen
    // hazir gorseller, moodIcons gibi kullanicinin kendi varliklari degil);
    // RENKLER ise yalnizca asagidaki alan baslaticisindan ve kullanicinin elle
    // duzenlemesinden geliyor -- builder onlara hic dokunmuyor (bkz.
    // UiThemeBuilder'in bas yorumu: "renklere asla dokunulmuyor").
    [Serializable]
    public struct FlameTier
    {
        [Tooltip("0..8. Yalnizca Inspector'da satiri tanimak icin; kod diziyi indeksle okuyor.")]
        public int tier;

        public Sprite[] frames;   // yanan alev, 4 kare
        public Sprite[] pending;  // gri bekleyen alev, 4 kare (T0'da bos)
        public Sprite[] large;    // kutlama ekraninin 32x32 sayfasi, 4 kare

        public Color number;      // seri sayisinin rengi
        public Color glow;        // kutlama ekraninin radyal parlamasi

        // Alevin KARE ICINDEKI optik kaymasini duzelten kaldirma, kare
        // yuksekliginin ORANI olarak (0..1). Gorselin kendi boyutundan
        // bagimsiz: 24 birimlik cipte de 160 birimlik kutlamada da ayni sayi
        // calisiyor, tuketen taraf kendi yuksekligiyle carpiyor.
        //
        // NEDEN GEREKLI: butun alevler karenin TABANINA yasli cizilmis (her
        // basamakta son dolu satir ayni), yukari dogru buyuyorlar. Yani kucuk
        // bir alevin gorsel agirligi karenin ortasinin epey ALTINDA kaliyor --
        // yaninda ortalanmis bir sayi varken "alev asagida, sayi yukarida"
        // gorunuyor. Kaldirma, sanatin SINIR KUTUSUNU karenin ortasina
        // getiriyor; kutlamada da alevi isin halkasinin merkezine oturtuyor.
        //
        // Degerler SANATTAN OLCULDU (dort karenin ortalamasi, alfa sinir
        // kutusunun merkezi). Gorseller degisirse yeniden olculmeli:
        // her kare icin alfasi > 16 olan ilk/son satiri bul, ortalamalarini al,
        // (kare-1)/2 ile farkini kare yuksekligine bol. Tema zaten arayuzun
        // elle duzenlenebilen tek yuzeyi -- gerekirse Inspector'dan da
        // ayarlanabilir.
        public float iconLift;    // 16x16 ikon sayfasi
        public float largeLift;   // 32x32 kutlama sayfasi
    }

    [Header("Zeminler")]
    public Color background = new Color32(0x2B, 0x1B, 0x12, 0xFF);
    public Color surface = new Color32(0x3B, 0x26, 0x18, 0xFF);
    public Color cardTop = new Color32(0x5A, 0x3D, 0x28, 0xFF);
    public Color cardBottom = new Color32(0x3E, 0x2A, 0x1C, 0xFF);
    public Color scrim = new Color32(0x0F, 0x08, 0x04, 0x99);

    [Header("Bordo")]
    public Color burgundy = new Color32(0x7A, 0x1F, 0x22, 0xFF);
    public Color burgundyLight = new Color32(0x9E, 0x2B, 0x2C, 0xFF);
    public Color burgundyShadow = new Color32(0x4A, 0x10, 0x12, 0xFF);

    [Header("Altin")]
    public Color gold = new Color32(0xE2, 0xB0, 0x4A, 0xFF);
    public Color goldButtonTop = new Color32(0xE9, 0xBD, 0x5C, 0xFF);
    public Color goldButtonBottom = new Color32(0xD3, 0x9D, 0x38, 0xFF);
    public Color goldButtonShadow = new Color32(0x8A, 0x5F, 0x1C, 0xFF);
    public Color goldDisabled = new Color32(0xE2, 0xB0, 0x4A, 0x59);
    public Color goldSoft = new Color32(0xE2, 0xB0, 0x4A, 0x38);

    [Header("Metin")]
    public Color textPrimary = new Color32(0xF3, 0xE6, 0xCC, 0xFF);
    public Color textMuted = new Color32(0xF3, 0xE6, 0xCC, 0x9E);
    public Color textFaint = new Color32(0xF3, 0xE6, 0xCC, 0x4D);
    public Color textOnGold = new Color32(0x2B, 0x1B, 0x12, 0xFF);

    [Header("Aliskanlik turu")]
    public Color buildColor = new Color32(0xB5, 0xCC, 0x86, 0xFF);
    public Color quitColor = new Color32(0xEB, 0x8F, 0x6E, 0xFF);

    [Header("Tablo ortusu")]
    public Color coverClosed = new Color32(0x22, 0x15, 0x0D, 0xE6);
    // Planli oldugu halde yapilmayan gunde kalici kapanan parca. coverClosed'tan
    // AYIRT EDILEBILIR olmali -- kullanici hangi parcayi kaybettigini gormeli.
    public Color coverMissed = new Color32(0x94, 0x94, 0x94, 0xC8);
    public Color easelCover = new Color32(0xE9, 0xDC, 0xBF, 0xFF);
    public Color moodSegmentEmpty = new Color32(0xF3, 0xE6, 0xCC, 0x2E);

    // Galeri'nin kendi tasarim degerleri. Cogu renk zaten var olan alanlarin
    // aynisi (ekran/kart gradyani, borderFaint, inputBackground, easelCover,
    // gold, textOnGold, textMuted, background, textPrimary -- bkz. GalleryWallItem
    // ve GalleryHabitCard) ve burada TEKRAR EDILMIYOR; yalnizca mevcut alanlarin
    // hicbirine TAM denk gelmeyen degerler eklendi.
    [Header("Galeri")]
    // rgba(226,176,74,.35) -- duvarin ust "resim rayi" alt kenarligi.
    public Color galleryRailBorder = new Color32(0xE2, 0xB0, 0x4A, 0x59);
    // rgba(226,176,74,.25) -- duvarin alt "lambri" ust kenarligi.
    public Color galleryWainscotBorder = new Color32(0xE2, 0xB0, 0x4A, 0x40);
    // rgba(20,10,4,.45) -- suren tablonun plaka zemini.
    public Color galleryPlaqueBackground = new Color32(0x14, 0x0A, 0x04, 0x73);
    // rgba(226,176,74,.55) -- suren tablonun plaka kenarligi.
    public Color galleryPlaqueBorder = new Color32(0xE2, 0xB0, 0x4A, 0x8C);
    // rgba(243,230,204,.45) -- delik rozetinin kenarligi.
    public Color galleryHoleBadgeBorder = new Color32(0xF3, 0xE6, 0xCC, 0x73);

    // Seviye 3'un (Tablo gorunumu) onceki/sonraki dugmeleri RoundIconButton'in
    // bordo/altin ikilisini PAYLASMIYOR -- teslim dosyasi onlara ayri bir
    // renk cifti veriyor. rgba(20,10,4,.55) / rgba(226,176,74,.45).
    public Color galleryViewerNavBackground = new Color32(0x14, 0x0A, 0x04, 0x8C);
    public Color galleryViewerNavBorder = new Color32(0xE2, 0xB0, 0x4A, 0x73);
    // Delik cipinin kenarligi/metni: rgba(243,230,204,.25) / .85 -- mevcut
    // textMuted(.62)/textFaint(.30) alanlarinin HICBIRINE denk gelmiyor.
    public Color galleryHolesChipBorder = new Color32(0xF3, 0xE6, 0xCC, 0x40);
    public Color galleryHolesChipText = new Color32(0xF3, 0xE6, 0xCC, 0xD9);

    // AltNavigasyon + Ayarlar teslim dosyasinin kendi degerleri. Ayni Galeri
    // kurali: yalnizca mevcut hicbir alana TAM denk gelmeyen degerler eklendi.
    [Header("Navigasyon / Ayarlar")]
    // rgba(226,176,74,.28) -- cubugun ust kenarligi.
    public Color navTopBorder = new Color32(0xE2, 0xB0, 0x4A, 0x47);
    // rgba(243,230,204,.62) -- secili olmayan sekme etiketi.
    public Color navLabelInactive = new Color32(0xF3, 0xE6, 0xCC, 0x9E);
    // rgba(243,230,204,.12) -- sekmeler arasi ince ayirici.
    public Color navSeparator = new Color32(0xF3, 0xE6, 0xCC, 0x1F);
    // rgba(226,176,74,.55) -- Premium kartinin kenarligi (notPremium durumu).
    public Color premiumBorder = new Color32(0xE2, 0xB0, 0x4A, 0x8C);
    // rgba(226,176,74,.35) -- Premium seridinin kenarligi (isPremium durumu).
    public Color premiumActiveBorder = new Color32(0xE2, 0xB0, 0x4A, 0x59);
    // rgba(20,10,4,.25) -- ayarlar satirinin basili zemini.
    public Color rowPressed = new Color32(0x14, 0x0A, 0x04, 0x40);
    // rgba(20,10,4,.35) -- hafta baslangici segmentinin ray zemini.
    public Color segmentTrack = new Color32(0x14, 0x0A, 0x04, 0x59);
    // rgba(243,230,204,.85) -- Premium kartinin madde listesi metni.
    public Color premiumListText = new Color32(0xF3, 0xE6, 0xCC, 0xD9);

    // Seri alevi teslim dosyasinin kendi degerleri. Ayni Galeri/Navigasyon
    // kurali: yalnizca mevcut hicbir alana TAM denk gelmeyen degerler eklendi.
    [Header("Seri alevi")]
    // rgba(20,10,4,.42) -- tile'in altindaki alev+sayi cipinin zemini.
    // pillBackground (#1E1008B8) YAKIN ama ayni degil.
    public Color flameChipBackground = new Color32(0x14, 0x0A, 0x04, 0x6B);
    // #8F7D70 -- galeri detayindaki ilerleme cubugu BEKLERKEN. Basamak rengi
    // yerine bu geciyor, cunku gri alev "bugun henuz yapilmadi" demek.
    public Color flameBarPending = new Color32(0x8F, 0x7D, 0x70, 0xFF);

    // Dokuz basamak, T0..T8. Sprite yuvalarini builder aciyor/dolduruyor;
    // buradaki RENKLER teslim dosyasinin tablosu ve builder onlara dokunmuyor.
    // Alan baslaticisi, var olan UiTheme.asset'te bu alan hic bulunmadigi icin
    // oldugu gibi uygulaniyor (sonradan eklenen her alanda oldugu gibi).
    public FlameTier[] flameTiers =
    {
        new FlameTier { tier = 0, number = new Color32(0xA8, 0x98, 0x88, 0xFF), glow = new Color32(0x4F, 0x40, 0x38, 0xFF), iconLift = 0.2422f, largeLift = 0.2891f },
        new FlameTier { tier = 1, number = new Color32(0xF7, 0xA1, 0x2E, 0xFF), glow = new Color32(0x8A, 0x3A, 0x12, 0xFF), iconLift = 0.1953f, largeLift = 0.2422f },
        new FlameTier { tier = 2, number = new Color32(0xF7, 0xA1, 0x2E, 0xFF), glow = new Color32(0x8A, 0x3A, 0x12, 0xFF), iconLift = 0.1328f, largeLift = 0.1992f },
        new FlameTier { tier = 3, number = new Color32(0xF7, 0xA1, 0x2E, 0xFF), glow = new Color32(0x9A, 0x3D, 0x10, 0xFF), iconLift = 0.0859f, largeLift = 0.1562f },
        new FlameTier { tier = 4, number = new Color32(0xF5, 0x8A, 0x26, 0xFF), glow = new Color32(0x9A, 0x2A, 0x0E, 0xFF), iconLift = 0.0547f, largeLift = 0.1250f },
        new FlameTier { tier = 5, number = new Color32(0xF2, 0x6A, 0x1F, 0xFF), glow = new Color32(0x8A, 0x14, 0x10, 0xFF), iconLift = 0.0391f, largeLift = 0.0938f },
        new FlameTier { tier = 6, number = new Color32(0x5F, 0xB6, 0xFF, 0xFF), glow = new Color32(0x1F, 0x4F, 0xAE, 0xFF), iconLift = 0.0391f, largeLift = 0.0781f },
        new FlameTier { tier = 7, number = new Color32(0xB6, 0x85, 0xFF, 0xFF), glow = new Color32(0x5A, 0x2A, 0xA6, 0xFF), iconLift = 0.0391f, largeLift = 0.0625f },
        new FlameTier { tier = 8, number = new Color32(0xFF, 0xD3, 0x4D, 0xFF), glow = new Color32(0xB0, 0x74, 0x14, 0xFF), iconLift = 0.0391f, largeLift = 0.0391f },
    };

    // Rotus (yanik parca onarimi) teslim dosyasinin kendi degerleri. Ayni
    // Galeri/Navigasyon/Seri-alevi kurali: yalnizca mevcut hicbir alana TAM
    // denk gelmeyen degerler eklendi.
    [Header("Rötuş")]
    // #7A1F22 -- stok pilinin zemini. burgundy'ye YAKIN ama ayni deger degil,
    // teslim dosyasi kendi tonunu veriyor.
    public Color rotusPillBg = new Color32(0x7A, 0x1F, 0x22, 0xFF);
    // rgba(226,176,74,.6) -- stok pilinin kenarligi.
    public Color rotusPillBorder = new Color32(0xE2, 0xB0, 0x4A, 0x99);
    // #2C4FB8 -- yanik rozetinin/cipinin dolgu mavisi.
    public Color rotusBlue = new Color32(0x2C, 0x4F, 0xB8, 0xFF);
    // #A9C2FF -- rozetin/cipin acik mavi vurgusu ve "· Onar" metni.
    public Color rotusBlueLight = new Color32(0xA9, 0xC2, 0xFF, 0xFF);
    // #7EA2F2 -- parilti nabzi ve onarim flas'inin rengi.
    public Color rotusGlow = new Color32(0x7E, 0xA2, 0xF2, 0xFF);

    [Header("Kenarlik ve golge")]
    public Color borderFaint = new Color32(0xF3, 0xE6, 0xCC, 0x1A);
    public Color borderSoft = new Color32(0xF3, 0xE6, 0xCC, 0x24);
    public Color borderGold = new Color32(0xE2, 0xB0, 0x4A, 0x99);
    public Color hardShadow = new Color32(0x00, 0x00, 0x00, 0x4D);
    // rgba(0,0,0,.45) -- tasarimdaki TEK yumusak golge, tablolarin altinda
    // (bkz. shadowBox sprite'i). Arayuzun geri kalani hardShadow kullaniyor.
    public Color softShadow = new Color32(0x00, 0x00, 0x00, 0x73);
    public Color pillBackground = new Color32(0x1E, 0x10, 0x08, 0xB8);
    public Color inputBackground = new Color32(0x14, 0x0A, 0x04, 0x59);

    [Header("Sprite'lar (builder doldurur)")]
    public RoundedSprite[] roundedSprites = Array.Empty<RoundedSprite>();
    public Sprite circle;
    public Sprite white;
    public Sprite checkmark;
    public Sprite shadowEllipse;
    // Yumusak 9-slice kutu golgesi. shadowEllipse'ten AYRI: o ressamin ayaginin
    // altindaki elips, bu tablolarin altindaki dikdortgen golge.
    public Sprite shadowBox;
    public Sprite studioBackground;
    public Sprite easelPainting;
    // Delik cipindeki 45-derece tarama suslemesi (Seviye 3). Image.Type.Tiled
    // ile kullanilir -- 4x4 TAM bir periyot, kenarsiz tekrarlar.
    public Sprite hatch;
    // Seri kutlamasinin zemini ve donen isin halkasi (bkz. StreakCelebration).
    // Ikisi de beyaz uretiliyor, rengi basamagin glow/number renginden geliyor.
    public Sprite radialGlow;
    public Sprite rayRing;
    // Alt navigasyon cubugunun uc sekme ikonu + ayar tusunun disli ikonu.
    public Sprite navTabAtolye;
    public Sprite navTabHabits;
    public Sprite navTabGallery;
    public Sprite settingsGear;
    // Rotus ikonunun uc sayfasi: Rotus.png'den kesilen 4 kare (stok > 0,
    // canli), Rotus_Static.png (hareketsiz kullanim, isteğe bagli) ve
    // Rotus_Empty.png (stok = 0, gri). RotusFrames(bool) ikisi arasinda secer.
    public Sprite[] rotusFrames = Array.Empty<Sprite>();
    public Sprite rotusStatic;
    public Sprite rotusEmpty;

    // Builder bu diziye yalnizca BOS YUVA aciyor (her mod icin bir satir);
    // sprite alanini asla yazmiyor. Yani uretilen prefab'larin aksine buraya
    // elle atanan gorseller komut tekrar calistirilinca KORUNUYOR -- tema zaten
    // arayuzun elle duzenlenebilen tek yuzeyi.
    [Header("Mod ikonlari (ELLE atanir; builder ezmez)")]
    public MoodIcon[] moodIcons = Array.Empty<MoodIcon>();

    [Header("Fontlar (builder doldurur; yoksa LiberationSans)")]
    public TMP_FontAsset regular;
    public TMP_FontAsset medium;
    public TMP_FontAsset semiBold;
    public TMP_FontAsset bold;

    // Istenen yariçapta sprite yoksa en yakinina dusuyoruz: eksik bir varlik
    // yuzunden kutunun kosesi tamamen kare kalmasin.
    public Sprite Rounded(int radius)
    {
        Sprite closest = null;
        int closestDistance = int.MaxValue;

        for (int i = 0; i < roundedSprites.Length; i++)
        {
            if (roundedSprites[i].sprite == null)
            {
                continue;
            }

            int distance = Mathf.Abs(roundedSprites[i].radius - radius);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = roundedSprites[i].sprite;
            }
        }

        return closest;
    }

    // Moda atanmis gorsel; atanmamissa null doner ve daire sade kalir.
    // Tahsissiz dogrusal arama: dizi mod sayisi kadar kisa.
    public Sprite IconFor(PainterManager.PainterMoods mood)
    {
        for (int i = 0; i < moodIcons.Length; i++)
        {
            if (moodIcons[i].mood == mood)
            {
                return moodIcons[i].sprite;
            }
        }

        return null;
    }

    // Bir basamagin animasyon kareleri. pending istenip o basamakta gri
    // varyant yoksa (T0) yanan kareler doner -- cagiran taraf ikinci bir
    // kontrol yapmak zorunda kalmasin.
    //
    // large ise pending BILEREK yok sayiliyor: kutlama ekrani yalnizca yeni
    // yakilan bir alevle aciliyor, gri bir kutlama diye bir sey yok.
    //
    // Sinir disi bir basamak ya da eksik/bos dizi null doner: FlameSprite bunu
    // "gorseli kapat" olarak okuyor (MoodCircle'in atanmamis mod ikonunu
    // sessizce gizlemesiyle ayni tolerans).
    public Sprite[] FlameFrames(int tier, bool pending, bool large)
    {
        if (!HasFlameTier(tier))
        {
            return null;
        }

        FlameTier row = flameTiers[tier];

        Sprite[] frames = large
            ? row.large
            : pending && NotEmpty(row.pending) ? row.pending : row.frames;

        return NotEmpty(frames) ? frames : null;
    }

    // Alevin kutu icinde yukari kaldirilma orani (bkz. FlameTier.iconLift).
    public float FlameLift(int tier, bool large)
    {
        if (!HasFlameTier(tier))
        {
            return 0f;
        }

        return large ? flameTiers[tier].largeLift : flameTiers[tier].iconLift;
    }

    public Color FlameNumber(int tier)
    {
        return HasFlameTier(tier) ? flameTiers[tier].number : textMuted;
    }

    public Color FlameGlow(int tier)
    {
        return HasFlameTier(tier) ? flameTiers[tier].glow : background;
    }

    // Stok > 0 ise 4 kareli canli sayfa, degilse tek kareli (gri) dizi. Tek
    // kaynak: pill de, onarim sayfasi da bunu cagirir, ikisi ayrismaz.
    public Sprite[] RotusFrames(bool hasStock)
    {
        if (hasStock)
        {
            return NotEmpty(rotusFrames) ? rotusFrames : Array.Empty<Sprite>();
        }

        return rotusEmpty != null ? new[] { rotusEmpty } : Array.Empty<Sprite>();
    }

    private bool HasFlameTier(int tier)
    {
        return flameTiers != null && tier >= 0 && tier < flameTiers.Length;
    }

    private static bool NotEmpty(Sprite[] frames)
    {
        return frames != null && frames.Length > 0;
    }

    public Color TypeColor(HabitData habit)
    {
        return HabitTypes.IsQuit(habit) ? quitColor : buildColor;
    }

    public static string TypeLabel(HabitData habit)
    {
        return Loc.Tr(HabitTypes.IsQuit(habit) ? "habit_type.quit_pill" : "habit_type.build_pill");
    }
}
