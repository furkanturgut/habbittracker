using UnityEngine;

// Alt navigasyon cubugunun DIKEY VE YATAY bestesi -- saf aritmetik.
//
// WorkshopBands/GalleryBands'in ucuncu kardesi: cubugun kendi rect'ini
// yazan BottomNavBar da, onu barindiran ekranlarin WorkshopBands.Solve'u da
// buradan okuyor, aralarinda sayi tekrari yok.
public static class BottomNavBands
{
    public const float TopPadding = 10f;
    public const float SidePadding = 12f;
    public const float TabGap = 2f;

    public const float IconSize = 24f;
    public const float IconLabelGap = 4f;
    public const float LabelHeight = 13f;

    // 10 ust dolgu + 54 icerik (ikon + bosluk + etiket + alt pay).
    public const float ContentHeight = 64f;

    // Teslim dosyasinin home-indicator payi.
    public const float DesignBottomPadding = 26f;

    public const float DesignHeight = ContentHeight + DesignBottomPadding;

    public const float SeparatorWidth = 1f;
    public const float SeparatorMargin = 6f;

    public const float MoodColumnWidth = 62f;
    public const float MoodBadgeSize = 36f;
    public const float MoodBadgeRing = 2f;

    public const float DiamondSize = 8f;
    public const float TopBorderHeight = 2f;

    // Cubugun ekranin dibinden olculen GORUNUR yuksekligi: guvenli alan
    // zaten DesignBottomPadding'in bir kismini (ya da tamamini) temizliyor,
    // ustune tekrar eklemek cubugu gereksiz yere uzatirdi (bkz.
    // NAVIGATION_SETTINGS_PLAN.md Faz 2, "Kesiften cikan gercekler" 4.
    // madde). Sonuc her cihazda ~90 birimde kaliyor: SE 64+26=90,
    // iPhone 14 64 + 32 inset = 96, Pixel 7 65.6 + 24.4 = 90.
    public static float BarHeight(float safeBottom)
    {
        return ContentHeight + Mathf.Max(0f, DesignBottomPadding - safeBottom);
    }

    // Cubugun guvenli alanin ALTINA dogru tasmasi gereken miktar
    // (FullBleedFitter.bleedBottom bunu okur).
    public static float BottomPadding(float safeBottom)
    {
        return Mathf.Max(0f, DesignBottomPadding - safeBottom);
    }

    // Uc sekmenin her birinin genisligi: yan dolgular, aralari, ayirici ve
    // mod sutunu dusulduktan sonra kalan alanin ucte biri.
    public static float TabWidth(float contentWidth)
    {
        float remaining = contentWidth
            - 2f * SidePadding
            - 4f * TabGap
            - (SeparatorWidth + 2f * SeparatorMargin)
            - MoodColumnWidth;

        return remaining / 3f;
    }
}
