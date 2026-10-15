using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Galeri'nin 1. seviyesindeki iki prefab: GalleryWallItem (duvardaki tek
// yuva) ve GalleryHabitCard (bir aliskanligin bolumu). Ayri dosya --
// UiPrefabBuilder zaten 500 satir sinirinin cok ustunde
// (PaintingChoiceSheetBuilder'in WorkshopSceneBuilder'dan ayrilma gerekcesiyle
// ayni). 2. seviyenin kendi iki prefab'i (WorkTile/StatTile) ayni sebeple
// GalleryDetailPrefabBuilder'da -- bu dosya onlarla birlikte 500'u asardi;
// Prefabs sonucu tek yapida kalsin diye yine BURADA, yalnizca INSASI orada.
public static class GalleryPrefabBuilder
{
    public class Prefabs
    {
        public GameObject WallItem;
        public GameObject HabitCard;
        public GameObject WorkTile;
        public GameObject StatTile;
        public GameObject StreakStatTile;
        public GameObject Thumb;
    }

    public static Prefabs Build(UiTheme theme)
    {
        EditorAssetUtil.EnsureFolderPath(UiPrefabBuilder.Folder);

        GameObject wallItem = Save(BuildWallItem(theme), "GalleryWallItem");
        GameObject habitCard = Save(BuildHabitCard(theme, wallItem), "GalleryHabitCard");
        GameObject workTile = Save(GalleryDetailPrefabBuilder.BuildWorkTile(theme), "GalleryWorkTile");
        GameObject statTile = Save(GalleryDetailPrefabBuilder.BuildStatTile(theme), "GalleryStatTile");
        GameObject streakStatTile = Save(GalleryDetailPrefabBuilder.BuildStreakStatTile(theme), "GalleryStreakStatTile");
        // GalleryViewerBuilder.cs'de (Seviye 3'un kendi dosyasi) -- ayni
        // "insasi tuketicinin dosyasinda, Prefabs sonucu burada" ayrimi.
        GameObject thumb = Save(GalleryViewerBuilder.BuildThumb(theme), "GalleryThumb");

        return new Prefabs
        {
            WallItem = wallItem,
            HabitCard = habitCard,
            WorkTile = workTile,
            StatTile = statTile,
            StreakStatTile = streakStatTile,
            Thumb = thumb
        };
    }

    private static GameObject Save(GameObject instance, string name)
    {
        instance.name = name;
        string path = UiPrefabBuilder.Folder + "/" + name + ".prefab";

        GameObject asset = PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        return asset;
    }

    private static RectTransform Root(string name, float width, float height)
    {
        RectTransform rect = UiFactory.Rect(name, null);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    // Delik rozeti + rotus parilti gorseli. UC yerde (burada, GalleryDetail
    // PrefabBuilder.BuildWorkTile, GalleryViewerBuilder.BuildPaintingSlot)
    // BIREBIR AYNI kod tekrar ediyordu; rotus'un parilti+dugme eklemesiyle
    // DORDUNCU bir kopyaya cikmak yerine TEK fabrika (bkz. CLAUDE.md'nin
    // "bir kurali iki yerde yazarsan ayrisir" temasi).
    public static void BuildHoleBadge(
        RectTransform paintingRect,
        UiTheme theme,
        out GameObject badgeObject,
        out Image badgeBorder,
        out Image badgeFill,
        out TMP_Text badgeLabel,
        out Button badgeButton,
        out Image glow)
    {
        // Parilti rozetin ARKASINDA: paintingRect'in cocugu olarak rozetten
        // ONCE eklenir (kardes sirasi = cizim sirasi). Boyutu AKTIF rozet
        // boyutunun 1.8 kati; merkezi aktif rozetle AYNI noktada kalsin diye
        // konum o farka gore kaydiriliyor.
        float activeSize = GalleryBands.HoleBadgeActiveSize;
        float glowSize = activeSize * 1.8f;
        float centerShift = (glowSize - activeSize) * 0.5f;

        glow = UiFactory.Image("HoleGlow", paintingRect, theme.radialGlow, theme.rotusGlow, false);
        glow.rectTransform.anchorMin = new Vector2(1f, 1f);
        glow.rectTransform.anchorMax = new Vector2(1f, 1f);
        glow.rectTransform.pivot = new Vector2(1f, 1f);
        glow.rectTransform.anchoredPosition = new Vector2(
            GalleryBands.HoleBadgeOffset + centerShift,
            GalleryBands.HoleBadgeOffset + centerShift);
        glow.rectTransform.sizeDelta = new Vector2(glowSize, glowSize);
        glow.gameObject.SetActive(false);

        // Delik rozeti: UiPrefabBuilder.BuildHabitTile'daki DoneBadge ile AYNI
        // kalip (anchor/pivot ust-sag, offset 8/8) -- paintingRect'in cocugu,
        // boylece tablonun GERCEK kosesini (degisken boyuta gore) otomatik takip eder.
        RectTransform badge = UiFactory.Rect("HoleBadge", paintingRect);
        badge.anchorMin = new Vector2(1f, 1f);
        badge.anchorMax = new Vector2(1f, 1f);
        badge.pivot = new Vector2(1f, 1f);
        badge.anchoredPosition = new Vector2(GalleryBands.HoleBadgeOffset, GalleryBands.HoleBadgeOffset);
        badge.sizeDelta = new Vector2(GalleryBands.HoleBadgeSize, GalleryBands.HoleBadgeSize);

        // Border-alti-fill kalibi (UiPrefabBuilder.BuildRoundIconButton ile ayni):
        // DIS katman (badge'in KENDI Image'i) kenarlik rengi tam boyutta, IC
        // katman ("Fill" cocugu) zemin rengi 2px icerden -- tersi sirada fill
        // kenarligi tamamen ortrdu.
        badgeBorder = badge.gameObject.AddComponent<Image>();
        badgeBorder.sprite = theme.circle;
        badgeBorder.color = theme.galleryHoleBadgeBorder;
        badgeBorder.raycastTarget = false;

        badgeFill = UiFactory.Image("Fill", badge, theme.circle, theme.background, false);
        UiFactory.Fill(badgeFill.rectTransform, 2f, 2f, 2f, 2f);

        badgeLabel = UiFactory.Text("Label", badge, "0", theme.semiBold, 11f,
            theme.textPrimary, TextAlignmentOptions.Center);
        UiFactory.Fill(badgeLabel.rectTransform);

        // Rotus'un tiklama hedefi -- varsayilan KAPALI (enabled=false).
        // GalleryWorkFrame.ApplyBadgeStyle rotusActive=true oldugunda aciyor
        // ve raycastTarget'i AYRICA acar (Button.enabled tek basina pointer
        // olayini yutmaya devam ederdi, bkz. o dosyadaki not).
        badgeButton = badge.gameObject.AddComponent<Button>();
        badgeButton.targetGraphic = badgeBorder;
        badgeButton.transition = Selectable.Transition.None;
        badgeButton.enabled = false;

        badge.gameObject.SetActive(false);
        badgeObject = badge.gameObject;
    }

    // -----------------------------------------------------------------------
    // GalleryWallItem: resim + ortu + delik rozeti + plaka
    // -----------------------------------------------------------------------

    private static GameObject BuildWallItem(UiTheme theme)
    {
        RectTransform root = Root("GalleryWallItem", GalleryBands.SlotMinWidth, GalleryBands.ItemHeight);

        LayoutElement layoutElement = root.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = GalleryBands.SlotMinWidth;
        layoutElement.minWidth = GalleryBands.SlotMinWidth;
        layoutElement.preferredHeight = GalleryBands.ItemHeight;
        layoutElement.minHeight = GalleryBands.ItemHeight;

        // Slot: sol ust (0,1)-(1,1), sabit yukseklik, item'in tam genisligine
        // yatayda GERIYOR -- Bind her bindte root.sizeDelta.x degistiginde
        // bu anchor sayesinde yeniden hesaba gerek kalmadan takip ediyor.
        RectTransform slot = UiFactory.Rect("Slot", root);
        slot.anchorMin = new Vector2(0f, 1f);
        slot.anchorMax = new Vector2(1f, 1f);
        slot.pivot = new Vector2(0.5f, 1f);
        slot.anchoredPosition = Vector2.zero;
        slot.sizeDelta = new Vector2(0f, GalleryBands.SlotHeight);

        RectTransform paintingRect = UiFactory.Rect("Painting", slot);
        paintingRect.anchorMin = new Vector2(0.5f, 0.5f);
        paintingRect.anchorMax = new Vector2(0.5f, 0.5f);
        paintingRect.pivot = new Vector2(0.5f, 0.5f);
        paintingRect.anchoredPosition = Vector2.zero;
        paintingRect.sizeDelta = new Vector2(GalleryBands.SlotMinWidth, GalleryBands.SlotHeight);

        // Yumusak golge ILK cocuk: kardes sirasi cizim sirasi, tablonun ALTINDA
        // kalmasi gerekiyor. Rect her yandan Spread kadar TASIYOR ve Drop kadar
        // asagi kayiyor; Fill'in dolgulari tasarim koordinatinda oldugu icin
        // tasma negatif deger (bkz. UiRect.Fill ve GalleryBands.PaintingShadow*).
        //
        // fillCenter=false: 9-slice'in yalnizca halkasi ciziliyor, yani golge
        // tam olarak tablonun DISINDAKI Spread'lik seritte kaliyor. Sprite
        // kenari da Spread oldugu icin halka kusursuz oturuyor
        // (bkz. UiSpriteGenerator.SoftShadow). Dolu bir merkez, tablo
        // PNG'lerinin seffaf kose pikselleri altindan koyu leke olarak gorunurdu.
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

        BuildHoleBadge(paintingRect, theme, out GameObject badgeObject, out Image badgeBorder,
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

        // Plaka: ustten ortali, SlotHeight + ItemColumnGap kadar asagida.
        // Genisligi kendi icerigine gore Bind'da yeniden yazilir.
        RectTransform plaqueRect = UiFactory.TopCenter(
            UiFactory.Rect("Plaque", root),
            GalleryBands.SlotHeight + GalleryBands.ItemColumnGap,
            GalleryBands.SlotMinWidth,
            GalleryBands.PlaqueHeight);

        Image border = plaqueRect.gameObject.AddComponent<Image>();
        border.sprite = theme.Rounded(5);
        border.type = Image.Type.Sliced;
        border.color = theme.galleryPlaqueBorder;
        UiFactory.HardShadow(plaqueRect.gameObject, GalleryBands.PlaqueShadowDrop, theme.hardShadow);

        Image background = UiFactory.Image("Fill", plaqueRect, theme.Rounded(5), theme.galleryPlaqueBackground);
        UiFactory.Fill(background.rectTransform, 1f, 1f, 1f, 1f);

        TMP_Text label = UiFactory.Text("Label", background.rectTransform, "Sürüyor · 0/16",
            theme.medium, 11f, theme.gold, TextAlignmentOptions.Center);
        UiFactory.Fill(label.rectTransform);
        label.textWrappingMode = TextWrappingModes.NoWrap;

        GalleryWallItem item = root.gameObject.AddComponent<GalleryWallItem>();
        UiWiring.Set(item, "rect", root);
        UiWiring.Set(item, "layoutElement", layoutElement);
        UiWiring.Set(item, "frame", frame);
        UiWiring.Set(item, "plaqueRect", plaqueRect);
        UiWiring.Set(item, "plaqueBorder", border);
        UiWiring.Set(item, "plaqueBackground", background);
        UiWiring.Set(item, "plaqueLabel", label);
        UiWiring.Set(item, "theme", theme);

        return root.gameObject;
    }

    // -----------------------------------------------------------------------
    // GalleryHabitCard: baslik satiri + kart (ray + duvar + lambri)
    // -----------------------------------------------------------------------

    private static GameObject BuildHabitCard(UiTheme theme, GameObject wallItemPrefab)
    {
        RectTransform root = Root("GalleryHabitCard", 326f, 200f);

        // Bolumun TAMAMI bir dokunma hedefi: Image+Button doğrudan KOKUN
        // KENDISINDE -- kokun kendi VerticalLayoutGroup'u yalnizca COCUKLARIN
        // yerlesimini kontrol eder, kendi Image/Button bilesenlerini etkilemez.
        Image hit = root.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        Button button = UiFactory.Button(hit);

        UiFactory.Column(root, GalleryBands.HeaderRowGap);
        UiFactory.FitVertical(root.gameObject);

        BuildHeaderRow(root, theme, out HeaderRow header);
        BuildCard(root, theme, out Transform wallRoot);

        GalleryHabitCard component = root.gameObject.AddComponent<GalleryHabitCard>();
        UiWiring.Set(component, "button", button);
        UiWiring.Set(component, "title", header.Title);
        UiWiring.Set(component, "typeLabel", header.TypeLabel);
        UiWiring.Set(component, "typePillElement", header.TypePillElement);
        UiWiring.Set(component, "countLabel", header.CountLabel);
        UiWiring.Set(component, "countLabelElement", header.CountLabelElement);
        UiWiring.Set(component, "wallItemPrefab", wallItemPrefab.GetComponent<GalleryWallItem>());
        UiWiring.Set(component, "wallRoot", wallRoot);
        UiWiring.Set(component, "theme", theme);

        return root.gameObject;
    }

    // Baslik satirinin builder'dan GalleryHabitCard'a tasinan parcalari. Dort
    // ayri `out` parametresi yerine tek bir tasiyici: iki tanesi (pil ve sayac
    // genisligi) metne gore Bind'da yeniden yazilan LayoutElement'ler.
    private struct HeaderRow
    {
        public TMP_Text Title;
        public TMP_Text TypeLabel;
        public LayoutElement TypePillElement;
        public TMP_Text CountLabel;
        public LayoutElement CountLabelElement;
    }

    private static void BuildHeaderRow(RectTransform parent, UiTheme theme, out HeaderRow result)
    {
        RectTransform header = UiFactory.Rect("HeaderRow", parent);
        HorizontalLayoutGroup row = UiFactory.Row(header, 10f, TextAnchor.MiddleLeft);
        UiFactory.Size(header.gameObject, -1f, 24f);

        // Teslim dosyasi: baslik satiri "padding:0 24", kart "margin:0 20".
        // Bolum kokü kart genisliginde oldugu icin (bkz. GalleryPanelBuilder)
        // fark burada dolgu olarak duruyor.
        int inset = (int)GalleryBands.HeaderRowSideInset;
        row.padding = new RectOffset(inset, inset, 0, 0);

        RectTransform titleGroup = UiFactory.Rect("TitleGroup", header);
        UiFactory.Row(titleGroup, 8f, TextAnchor.MiddleLeft);
        LayoutElement titleGroupElement = titleGroup.gameObject.AddComponent<LayoutElement>();
        titleGroupElement.flexibleWidth = 1f;

        result.Title = UiFactory.Text("Title", titleGroup, "Alışkanlık", theme.semiBold, 17f,
            theme.textPrimary, TextAlignmentOptions.Left);
        result.Title.overflowMode = TextOverflowModes.Ellipsis;
        UiFactory.Size(result.Title.gameObject, -1f, 22f);

        // Pil METNINE gore daralip genisliyor (teslim dosyasi: "padding:2px 8px",
        // sabit genislik YOK -- "YAP" ve "BIRAK" ayni kutuda duramaz). Genislik
        // Bind'da yaziliyor; burasi yalnizca acilis degerini ve yuksekligi
        // koyuyor. Yuksekligi LayoutElement tasiyor, genisligi -1 kalarak satirin
        // kendi olcumune birakilmiyor: GalleryHabitCard.Bind ikisini de yaziyor.
        RectTransform pillRect = UiFactory.Rect("TypePill", titleGroup);
        LayoutElement pillElement = UiFactory.Size(
            pillRect.gameObject, GalleryBands.TypePillMinWidth, GalleryBands.TypePillHeight);
        result.TypePillElement = pillElement;

        Image typePill = pillRect.gameObject.AddComponent<Image>();
        typePill.sprite = theme.Rounded(6);
        typePill.type = Image.Type.Sliced;
        typePill.color = theme.inputBackground;

        result.TypeLabel = UiFactory.Text("Label", pillRect, "YAP", theme.semiBold, 10f,
            theme.buildColor, TextAlignmentOptions.Center);
        UiFactory.Fill(result.TypeLabel.rectTransform);
        // letter-spacing:.05em -- TMP'nin characterSpacing'i em'in yuzdesi.
        result.TypeLabel.characterSpacing = 5f;

        RectTransform countGroup = UiFactory.Rect("CountGroup", header);
        UiFactory.Row(countGroup, 6f, TextAnchor.MiddleRight);
        UiFactory.Size(countGroup.gameObject, -1f, 20f);

        result.CountLabel = UiFactory.Text("Count", countGroup, "0 eser", theme.regular, 12f,
            theme.textMuted, TextAlignmentOptions.Right);
        result.CountLabel.textWrappingMode = TextWrappingModes.NoWrap;
        result.CountLabelElement = UiFactory.Size(result.CountLabel.gameObject, -1f, 16f);

        TMP_Text chevron = UiFactory.Text("Chevron", countGroup, "›", theme.semiBold, 16f,
            theme.gold, TextAlignmentOptions.Center);
        UiFactory.Size(chevron.gameObject, 12f, 16f);
    }

    private static void BuildCard(RectTransform parent, UiTheme theme, out Transform wallRoot)
    {
        RectTransform border = UiFactory.Rect("Card", parent);
        Image borderImage = border.gameObject.AddComponent<Image>();
        borderImage.sprite = theme.Rounded((int)GalleryBands.CardRadius);
        borderImage.type = Image.Type.Sliced;
        borderImage.color = theme.borderFaint;
        UiFactory.HardShadow(border.gameObject, 4f, theme.hardShadow);

        UiFactory.Column(border, 0f);
        UiFactory.FitVertical(border.gameObject);

        Image fill = UiFactory.Image("Fill", border, theme.Rounded((int)GalleryBands.CardRadius - 2), Color.white);
        UiFactory.Fill(fill.rectTransform, 2f, 2f, 2f, 2f);
        UiFactory.Gradient(fill.gameObject, theme.cardTop, theme.cardBottom, 160f);
        LayoutElement fillElement = fill.gameObject.AddComponent<LayoutElement>();
        fillElement.ignoreLayout = true;

        BuildRail(border, theme);
        wallRoot = BuildWall(border);
        BuildWainscot(border, theme);
    }

    private static void BuildRail(RectTransform parent, UiTheme theme)
    {
        RectTransform rail = UiFactory.Rect("Rail", parent);
        UiFactory.Size(rail.gameObject, -1f, GalleryBands.RailHeight);

        Image background = UiFactory.Image("Background", rail, theme.white, theme.surface, false);
        UiFactory.Fill(background.rectTransform);

        RectTransform borderLine = UiFactory.StretchBottom(UiFactory.Rect("Border", rail), 0f, 1f, 0f, 0f);
        Image borderImage = UiFactory.Image("Image", borderLine, theme.white, theme.galleryRailBorder, false);
        UiFactory.Fill(borderImage.rectTransform);
    }

    // Duvar ogeleri burada ONCEDEN YARATILMIYOR -- GalleryHabitCard calisma
    // aninda kendi havuzunu Instantiate ile kurar (bkz. GalleryHabitCard.ItemAt),
    // bu fonksiyon yalnizca bos FlowLayoutGroup kabini hazirlar.
    private static RectTransform BuildWall(RectTransform parent)
    {
        RectTransform wall = UiFactory.Rect("Wall", parent);

        FlowLayoutGroup flow = wall.gameObject.AddComponent<FlowLayoutGroup>();
        flow.padding = new RectOffset(
            (int)GalleryBands.CardSidePadding,
            (int)GalleryBands.CardSidePadding,
            (int)GalleryBands.CardTopPadding,
            (int)GalleryBands.CardBottomPadding);
        UiWiring.SetFloat(flow, "spacingX", GalleryBands.WallGapX);
        UiWiring.SetFloat(flow, "spacingY", GalleryBands.WallGapY);
        UiWiring.SetFloat(flow, "itemHeight", GalleryBands.ItemHeight);

        return wall;
    }

    private static void BuildWainscot(RectTransform parent, UiTheme theme)
    {
        RectTransform wainscot = UiFactory.Rect("Wainscot", parent);
        UiFactory.Size(wainscot.gameObject, -1f, GalleryBands.WainscotHeight);

        Image background = UiFactory.Image("Background", wainscot, theme.white, Color.white, false);
        UiFactory.Fill(background.rectTransform);
        // Lambri DUZ renk degil, kendi dikey gradyani var (teslim dosyasi:
        // "linear-gradient(180deg,#3e2a1c,#2b1b12)") -- kartin alt kenarini
        // ekranin zeminine bagliyor.
        UiFactory.Gradient(background.gameObject, theme.cardBottom, theme.background);

        RectTransform borderLine = UiFactory.StretchTop(UiFactory.Rect("Border", wainscot), 0f, 2f, 0f, 0f);
        Image borderImage = UiFactory.Image("Image", borderLine, theme.white, theme.galleryWainscotBorder, false);
        UiFactory.Fill(borderImage.rectTransform);
    }
}
