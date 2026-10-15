using UnityEngine;

// 03 Galeri ekraninin 1. seviyesindeki (duvar) butun sayilari tek yerde --
// WorkshopBands'in galeri tarafindaki karsiligi. Hesaplari FlowLayoutGroup,
// GalleryWallItem ve Editor builder'i paylasiyor; iki ayri kopya sessizce
// ayrismasin diye burada.
public static class GalleryBands
{
    // Teslim dosyasindaki olculer (tasarim pikseli).
    public const float SlotHeight = 112f;
    public const float SlotMinWidth = 84f;
    public const float PaintingScale = 2f;

    public const float WallGapX = 16f;
    public const float WallGapY = 12f;

    // Yuva (resim + plaka) kendi icinde "column, gap 8".
    public const float ItemColumnGap = 8f;

    // Plaka satirinin tuttugu sabit yukseklik (11pt metin + 2/8 dolgu + 1pt
    // kenarlik). Teslim dosyasi tam sayi vermiyor; FlowLayoutGroup'un sabit
    // itemHeight'i gerektirdigi icin burada tek bir sabit olarak tutuluyor.
    public const float PlaqueHeight = 20f;

    // FlowLayoutGroup'a bildirilen SABIT yuva yuksekligi.
    public const float ItemHeight = SlotHeight + ItemColumnGap + PlaqueHeight;

    // Tablonun altindaki yumusak golge: "0 6px 10px rgba(0,0,0,.45)". Golge
    // grafigi tablonun rect'inden her yandan Spread kadar tasirilir (blur/2,
    // yani CSS'te golgenin kutunun disinda gorunen bandi) ve Drop kadar asagi
    // kaydirilir. Spread AYNI ZAMANDA golge sprite'inin 9-slice kenari --
    // UiSpriteGenerator.Build bu sabiti okuyor, iki yerde yazilmiyor.
    public const float PaintingShadowSpread = 5f;
    public const float PaintingShadowDrop = 6f;

    // Plakanin sert golgesi: "0 2px 0 rgba(0,0,0,.3)".
    public const float PlaqueShadowDrop = 2f;

    public const float RailHeight = 6f;
    public const float WainscotHeight = 12f;
    public const float CardRadius = 20f;
    public const float CardSideMargin = 20f;
    public const float CardTopPadding = 16f;
    public const float CardSidePadding = 12f;
    public const float CardBottomPadding = 14f;

    // Baslik satiri kartin kendisinden 4 birim DAHA icerden basliyor: teslim
    // dosyasinda kartin yan boslugu 20 ("margin:0 20"), baslik satirinin dolgusu
    // 24 ("padding:0 24"). Bolum kokunun genisligi kart genisligine (366-2*20)
    // kelepceli oldugu icin baslik satiri farki kendi dolgusu olarak tasiyor.
    public const float HeaderRowSideInset = 4f;

    // Baslik satirindaki tur pili: "padding:2px 8px", 10/600. Genisligi SABIT
    // DEGIL -- GalleryHabitCard.Bind metnin olcusune gore yaziyor (plakanin
    // GalleryWallItem'daki kurali ile ayni). MinWidth yalnizca prefab'in acilis
    // degeri; "YAP" bile ondan genis cikarsa metin kazanir.
    public const float TypePillHeight = 18f;
    public const float TypePillMinWidth = 38f;
    public const float TypePillPaddingX = 16f;

    // Baslik satiri ile kartin arasi.
    public const float HeaderRowGap = 10f;

    // Bolumler (aliskanliklar) arasi.
    public const float SectionGap = 26f;

    public const float HoleBadgeSize = 22f;
    public const float HoleBadgeOffset = 8f;

    // Rotus stogu varken rozet buyur (teslim dosyasi: 22 -> 26) -- dikkat
    // cekmesi gereken bir sey artik TIKLANABILIR de oldugu icin.
    public const float HoleBadgeActiveSize = 26f;

    // Parilti nabzinin periyodu ve alfa/olcek araligi -- rozet de, Seviye 3
    // cipi de AYNI sayilari kullanir (bkz. design_handoff_rotus/README.md).
    public const float GlowPeriodSeconds = 1.6f;
    public const float GlowMinAlpha = 0.55f;
    public const float GlowMaxAlpha = 0.95f;
    public const float GlowMinScale = 1f;
    public const float GlowMaxScale = 1.15f;

    // Bir yuvanin genisligi: tablonun 2x native genisligi, plakanin kendi
    // genisligi ve tasarimdaki asgari (84) -- hangisi en genisse o.
    public static float SlotWidth(float nativeWidthScaled, float plaqueWidth)
    {
        return Mathf.Max(SlotMinWidth, Mathf.Max(nativeWidthScaled, plaqueWidth));
    }

    // -----------------------------------------------------------------------
    // Seviye 3 (Tablo gorunumu)
    // -----------------------------------------------------------------------

    public const float ViewerMaxWidth = 260f;
    public const float ViewerMaxHeight = 290f;
    public const float ThumbFitSize = 36f;

    // Onceki/sonraki gecisi: icerik bu kadar birim kenardan kayip solarak
    // yerine oturuyor (bkz. GalleryPaintingViewer.AnimateTo).
    public const float NavTransitionSeconds = 0.22f;
    public const float NavSlideDistance = 36f;

    // Tablonun TAM SAYI buyutme kati: "k = max(1, floor(min(260/w, 290/h)))".
    // Tam sayi -- piksel sanati yari piksellerde bulaniklasir. w/h gecersizse
    // (cozulemeyen sprite) 1 donuyor, cagiran taraf boylece sifira bolmekten
    // korunuyor.
    public static int ViewerScale(float nativeWidth, float nativeHeight)
    {
        if (nativeWidth <= 0f || nativeHeight <= 0f)
        {
            return 1;
        }

        return Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(ViewerMaxWidth / nativeWidth, ViewerMaxHeight / nativeHeight)));
    }

    // Kucuk resim seridindeki tek kare: tabloyu 36x36 kutuya en-boy orani
    // bozulmadan sigdirir (GalleryThumb.Bind'in tek cagirani).
    public static Vector2 ThumbFit(float nativeWidth, float nativeHeight)
    {
        if (nativeWidth <= 0f || nativeHeight <= 0f)
        {
            return Vector2.zero;
        }

        float scale = Mathf.Min(ThumbFitSize / nativeWidth, ThumbFitSize / nativeHeight);
        return new Vector2(nativeWidth * scale, nativeHeight * scale);
    }
}
