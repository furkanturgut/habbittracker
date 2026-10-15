using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Galeri'nin 2. seviyesindeki iki prefab: GalleryWorkTile ("Eserler"
// izgarasinin kendi kartli karesi) ve GalleryStatTile (sabit uclu istatistik
// karosu). Ayri dosya -- GalleryPrefabBuilder bunlarla birlikte 500 satir
// sinirini asardi (PaintingChoiceSheetBuilder'in WorkshopSceneBuilder'dan
// ayrilma gerekcesiyle ayni). GalleryPrefabBuilder.Build bu iki fonksiyonu
// cagirip sonucu KENDI Prefabs yapisina koyuyor -- iki ayri "Prefabs" tipi
// yok, insaat burada, sonuc orada.
public static class GalleryDetailPrefabBuilder
{
    // Hucre boyutunu GridCellFitter yaziyor -- kare gorsel + altta sabit metin
    // bandi, UiPrefabBuilder.BuildHabitTile'daki kalibin aynisi.
    public const float WorkTileExtraHeight = 42f;

    // Istatistik karosunun yuksekligi. 70 -> 92: 0. karo (Su anki seri) artik
    // alevin yani sira bir ilerleme cubugu (4) ve bir alt yazi (13) tasiyor.
    // Alti karo TEK bir satirda hizalandigi icin yukseklik hepsinde ayni olmak
    // zorunda -- 1-5 arasi karolarin icerigi zaten MiddleCenter, fazla alanda
    // ortalanmis kaliyorlar.
    public const float StatTileHeight = 92f;

    // Seri karosunun kendi olculeri.
    private const float StreakFlameSize = 24f;
    private const float StreakBarHeight = 4f;
    private const float StreakBarWidthRatio = 0.7f;

    private static RectTransform Root(string name, float width, float height)
    {
        RectTransform rect = UiFactory.Rect(name, null);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    // BuildHabitTile'daki "Card" + AspectRatioFitter kalibi: hucrenin genisligi
    // GridCellFitter'dan geliyor, resim bolgesi o genislige gore KARE kaliyor,
    // WorkTileExtraHeight kadarlik sabit bir bant ise altta metne ayriliyor --
    // boylece "side" degerini burada tekrar hesaplamaya gerek kalmiyor.
    public static GameObject BuildWorkTile(UiTheme theme)
    {
        RectTransform root = Root("GalleryWorkTile", 160f, 186f);

        Image border = root.gameObject.AddComponent<Image>();
        border.sprite = theme.Rounded(18);
        border.type = Image.Type.Sliced;
        border.color = theme.borderFaint;
        UiFactory.HardShadow(root.gameObject, 4f, theme.hardShadow);
        Button button = UiFactory.Button(border);

        Image fill = UiFactory.Image("Fill", root, theme.Rounded(16), Color.white);
        UiFactory.Fill(fill.rectTransform, 2f, 2f, 2f, 2f);
        UiFactory.Gradient(fill.gameObject, theme.cardTop, theme.cardBottom, 160f);

        RectTransform imageSlot = UiFactory.Rect("ImageSlot", fill.rectTransform);
        imageSlot.anchorMin = new Vector2(0f, 1f);
        imageSlot.anchorMax = new Vector2(1f, 1f);
        imageSlot.pivot = new Vector2(0.5f, 1f);
        imageSlot.offsetMin = new Vector2(10f, -100f);
        imageSlot.offsetMax = new Vector2(-10f, -8f);
        AspectRatioFitter aspect = imageSlot.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
        aspect.aspectRatio = 1f;

        RectTransform paintingRect = UiFactory.Rect("Painting", imageSlot);
        paintingRect.anchorMin = new Vector2(0.5f, 0.5f);
        paintingRect.anchorMax = new Vector2(0.5f, 0.5f);
        paintingRect.pivot = new Vector2(0.5f, 0.5f);
        paintingRect.anchoredPosition = Vector2.zero;
        paintingRect.sizeDelta = new Vector2(84f, 84f);

        // Golge, tablo ve ortu: GalleryPrefabBuilder.BuildWallItem'daki
        // ucluyle AYNI kalip -- burada da GalleryWorkFrame ikisini paylasiyor.
        Image shadow = UiFactory.Image("Shadow", paintingRect, theme.shadowBox, theme.softShadow);
        shadow.fillCenter = false;
        UiFactory.Fill(
            shadow.rectTransform,
            -GalleryBands.PaintingShadowSpread,
            -GalleryBands.PaintingShadowSpread + GalleryBands.PaintingShadowDrop,
            -GalleryBands.PaintingShadowSpread,
            -GalleryBands.PaintingShadowSpread - GalleryBands.PaintingShadowDrop);

        Image painting = UiFactory.Image("Image", paintingRect, null, Color.white, false);
        UiFactory.Fill(painting.rectTransform);

        RectTransform coverRoot = UiFactory.Fill(UiFactory.Rect("Cover", paintingRect));
        PaintingCoverGrid cover = coverRoot.gameObject.AddComponent<PaintingCoverGrid>();
        UiWiring.Set(cover, "cellSprite", theme.white);
        UiWiring.SetColor(cover, "closedColor", theme.coverClosed);
        UiWiring.SetColor(cover, "missedColor", theme.coverMissed);

        GalleryPrefabBuilder.BuildHoleBadge(paintingRect, theme, out GameObject badgeObject, out Image badgeBorder,
            out Image badgeFill, out TMP_Text badgeLabel, out Button badgeButton, out Image glow);

        GalleryWorkFrame frame = root.gameObject.AddComponent<GalleryWorkFrame>();
        UiWiring.Set(frame, "paintingRect", paintingRect);
        UiWiring.Set(frame, "painting", painting);
        UiWiring.Set(frame, "shadow", shadow);
        UiWiring.Set(frame, "cover", cover);
        UiWiring.Set(frame, "holeBadge", badgeObject);
        UiWiring.Set(frame, "holeBadgeBorder", badgeBorder);
        UiWiring.Set(frame, "holeBadgeFill", badgeFill);
        UiWiring.Set(frame, "holeBadgeLabel", badgeLabel);
        UiWiring.Set(frame, "holeBadgeButton", badgeButton);
        UiWiring.Set(frame, "holeGlow", glow);
        UiWiring.Set(frame, "theme", theme);

        // Ad + alt satir: HabitTile'in "Info" bandiyla ayni kalip, sabit
        // WorkTileExtraHeight yuksekliginde, altta.
        RectTransform info = UiFactory.StretchBottom(UiFactory.Rect("Info", fill.rectTransform), 6f, WorkTileExtraHeight - 6f, 8f, 8f);
        VerticalLayoutGroup infoColumn = UiFactory.Column(info, 1f, TextAnchor.UpperCenter);
        infoColumn.padding = new RectOffset(0, 0, 2, 0);

        TMP_Text name = UiFactory.Text("Name", info, "Tablo", theme.medium, 14f,
            theme.textPrimary, TextAlignmentOptions.Center);
        name.overflowMode = TextOverflowModes.Ellipsis;
        UiFactory.Size(name.gameObject, -1f, 17f);

        TMP_Text sub = UiFactory.Text("Sub", info, "16 parça", theme.regular, 11f,
            theme.gold, TextAlignmentOptions.Center);
        sub.overflowMode = TextOverflowModes.Ellipsis;
        UiFactory.Size(sub.gameObject, -1f, 14f);

        GalleryWorkTile tile = root.gameObject.AddComponent<GalleryWorkTile>();
        UiWiring.Set(tile, "button", button);
        UiWiring.Set(tile, "frame", frame);
        UiWiring.Set(tile, "nameLabel", name);
        UiWiring.Set(tile, "subLabel", sub);
        UiWiring.Set(tile, "theme", theme);

        return root.gameObject;
    }

    // Sayisi hep 6 (iki satir, satir basina uc) oldugu icin ayri bir bilesen
    // YOK -- GalleryDetailBuilder alti kopyayi dogrudan kurup "Value"/"Label"
    // cocuklarini adiyla buluyor.
    public static GameObject BuildStatTile(UiTheme theme)
    {
        RectTransform body = BuildStatShell(theme, "GalleryStatTile", out _);

        TMP_Text value = UiFactory.Text("Value", body, "0", theme.bold, 22f,
            theme.textPrimary, TextAlignmentOptions.Center);
        UiFactory.Size(value.gameObject, -1f, 26f);

        BuildStatLabel(theme, body, "Seri");

        return body.root.gameObject;
    }

    // 0. karo ("Su anki seri") AYRI bir prefab, BuildStatTile'in bir varyanti
    // degil: GalleryDetailBuilder karo iclerine "Fill/Body/Value" yolu ile
    // uzaniyor, yani ortak bir prefabin ornegini sonradan yeniden ebeveynleyip
    // araya satir sokmak hem PrefabUtility ile tatsiz hem de o yolu kirardi.
    // Iskeleti paylasiyorlar (BuildStatShell), icerigi paylasmiyorlar.
    //
    // Fark uc sey: sayinin SOLUNDA alev, etiketin ALTINDA bir sonraki aleve
    // ilerleme cubugu ve "Yeni alev: N gun" alt yazisi.
    public static GameObject BuildStreakStatTile(UiTheme theme)
    {
        RectTransform body = BuildStatShell(theme, "GalleryStreakStatTile", out _);

        // Alev + sayi tek satirda. Kok karonun kendi genisligi esnek oldugu
        // icin satir da esnek: icerige gore ortalaniyor.
        RectTransform row = UiFactory.Rect("ValueRow", body);
        UiFactory.Row(row, 4f, TextAnchor.MiddleCenter);
        UiFactory.Size(row.gameObject, -1f, 26f);

        // Iki katman, UiPrefabBuilder'daki cip ile ayni gerekce: disaridaki
        // yuvayi duzen yerlestiriyor, icerideki gorseli FlameSprite optik
        // hizalama icin kaydiriyor.
        RectTransform flameSlot = UiFactory.Rect("FlameSlot", row);
        UiFactory.Size(flameSlot.gameObject, StreakFlameSize, StreakFlameSize);

        Image flameImage = UiFactory.Image("Image", flameSlot, null, Color.white, false);
        UiFactory.Fill(flameImage.rectTransform);
        flameImage.preserveAspect = true;
        flameImage.raycastTarget = false;
        FlameSprite flame = flameImage.gameObject.AddComponent<FlameSprite>();
        UiWiring.Set(flame, "image", flameImage);
        UiWiring.Set(flame, "theme", theme);

        // Adi "Value" KALIYOR: GalleryDetailBuilder bu karoyu da diger besi
        // gibi statValues dizisine baglayabilsin diye (yol bir seviye derin,
        // ama ad ayni).
        TMP_Text value = UiFactory.Text("Value", row, "0", theme.bold, 22f,
            theme.textPrimary, TextAlignmentOptions.Center);

        // Yer tutucu metin; gercek etiketi GalleryDetailView.Bind yaziyor
        // (BuildStatTile'daki "Seri" yer tutucusuyla ayni).
        BuildStatLabel(theme, body, "Seri");

        // Ilerleme cubugu. Genislik karonun %70'i ve bu ORAN, sabit bir sayi
        // degil: karo genisligi flexibleWidth ile paylasildigi icin cihazdan
        // cihaza degisiyor. Oran ANCHOR ile veriliyor (0.15 .. 0.85), bir
        // LayoutElement.flexibleWidth ile DEGIL -- dikey bir kolonda
        // flexibleWidth bir yuzde anlamina gelmez, childForceExpandWidth zaten
        // cocugu tam genislige yayar ve cubuk karoyu bastan basa gecerdi.
        //
        // Bu yuzden ara bir "BarRow" var: kolonun satiri O (yuksekligi
        // LayoutElement'ten), cubugun kendisi onun icinde oransal duruyor.
        RectTransform barRow = UiFactory.Rect("BarRow", body);
        UiFactory.Size(barRow.gameObject, -1f, StreakBarHeight);

        float inset = (1f - StreakBarWidthRatio) * 0.5f;
        RectTransform track = UiFactory.Rect("BarTrack", barRow);
        track.anchorMin = new Vector2(inset, 0f);
        track.anchorMax = new Vector2(1f - inset, 1f);
        track.pivot = new Vector2(0.5f, 0.5f);
        track.offsetMin = Vector2.zero;
        track.offsetMax = Vector2.zero;

        Image trackImage = track.gameObject.AddComponent<Image>();
        trackImage.sprite = theme.Rounded(5);
        trackImage.type = Image.Type.Sliced;
        trackImage.color = theme.galleryPlaqueBackground;
        trackImage.raycastTarget = false;

        Image bar = UiFactory.Image("BarFill", track, theme.Rounded(5), theme.gold, false);
        UiFactory.Fill(bar.rectTransform);
        bar.type = Image.Type.Filled;
        bar.fillMethod = Image.FillMethod.Horizontal;
        bar.fillOrigin = (int)Image.OriginHorizontal.Left;
        bar.fillAmount = 0f;

        TMP_Text next = UiFactory.Text("Next", body, string.Empty, theme.regular, 10f,
            theme.textFaint, TextAlignmentOptions.Center);
        next.overflowMode = TextOverflowModes.Ellipsis;
        UiFactory.Size(next.gameObject, -1f, 13f);

        return body.root.gameObject;
    }

    // Iki istatistik karosunun ortak kabugu: kenarlik + gradyan dolgu + dikey
    // govde. "Body" rect'ini doner, kok nesneye body.root ile ulasiliyor.
    private static RectTransform BuildStatShell(UiTheme theme, string name, out Image fill)
    {
        RectTransform root = Root(name, 100f, StatTileHeight);
        LayoutElement element = root.gameObject.AddComponent<LayoutElement>();
        element.flexibleWidth = 1f;
        element.preferredHeight = StatTileHeight;
        element.minHeight = StatTileHeight;

        Image border = root.gameObject.AddComponent<Image>();
        border.sprite = theme.Rounded(16);
        border.type = Image.Type.Sliced;
        border.color = theme.borderFaint;
        UiFactory.HardShadow(root.gameObject, 4f, theme.hardShadow);

        fill = UiFactory.Image("Fill", root, theme.Rounded(14), Color.white);
        UiFactory.Fill(fill.rectTransform, 2f, 2f, 2f, 2f);
        UiFactory.Gradient(fill.gameObject, theme.cardTop, theme.cardBottom, 160f);

        RectTransform body = UiFactory.Fill(UiFactory.Rect("Body", fill.rectTransform), 6f, 10f, 6f, 10f);
        UiFactory.Column(body, 2f, TextAnchor.MiddleCenter);

        return body;
    }

    private static TMP_Text BuildStatLabel(UiTheme theme, RectTransform body, string text)
    {
        TMP_Text label = UiFactory.Text("Label", body, text, theme.regular, 11f,
            theme.textMuted, TextAlignmentOptions.Center);
        label.overflowMode = TextOverflowModes.Ellipsis;
        UiFactory.Size(label.gameObject, -1f, 14f);

        return label;
    }
}
