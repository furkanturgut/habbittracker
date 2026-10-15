using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 03 Galeri ekraninin 3. seviyesi: GalleryPanelBuilder tarafindan
// GalleryDetailBuilder'dan SONRA cagrilir, panelin EN SON cocugu olarak
// eklenir ki Seviye 2'nin de USTUNE cizilsin (bkz. GalleryPaintingViewer
// basindaki "Sahiplik" notu).
//
// Onceki/sonraki dugmeleri RoundIconButton'i YENIDEN KULLANMIYOR -- teslim
// dosyasi onlara ayri bir renk cifti veriyor (rgba(20,10,4,.55) zemin,
// rgba(226,176,74,.45) kenarlik), RoundIconButton'in bordo/altin ikilisiyle
// AYNI degil. Kapatma dugmesi ise diger ekranlardaki geri tusuyle AYNI
// RoundIconButton, glif de dahil -- ayri bir "kapat" stili yok.
//
// Tablonun kendisi GalleryWorkFrame ile (WallItem/WorkTile'daki UCUNCU
// kopyasi, bkz. GalleryWorkFrame basindaki not) -- cover/golge/delik rozeti
// buradan bedava geliyor, yalnizca olcek runtime'da degisken (k).
public static class GalleryViewerBuilder
{
    private const float HeaderTop = 6f;
    private const float HeaderHeight = 46f;
    private const float ThumbBandHeight = 118f;
    private const float InfoBandHeight = 112f;
    private const float NavButtonSize = 40f;
    private const float NavButtonInset = 12f;
    private const float ChipHeight = 28f;
    private const float StatusChipWidth = 150f;
    private const float HolesChipWidth = 150f;
    private const float SwatchSize = 12f;
    // Rotus parilti gorseli cipin kenarlarindan bu kadar tasar (badge'in
    // "merkez ayni kalsin" fikrinin cip icin daha basit karsiligi).
    private const float HolesChipGlowSpread = 8f;
    private const float ThumbSize = 52f;

    public static GalleryPaintingViewer Build(
        RectTransform parent,
        UiTheme theme,
        UiPrefabBuilder.Prefabs prefabs,
        GalleryPrefabBuilder.Prefabs galleryPrefabs)
    {
        RectTransform root = UiFactory.Fill(UiFactory.Rect("GalleryViewer", parent));

        Image background = UiFactory.Image("Background", root, theme.white, Color.white, false);
        UiFactory.Fill(background.rectTransform);
        UiFactory.Gradient(background.gameObject, theme.surface, theme.background);

        CanvasGroup fadeGroup = root.gameObject.AddComponent<CanvasGroup>();

        BuildHeader(root, theme, prefabs, out TMP_Text habitLabel, out TMP_Text posLabel, out Button closeButton);

        // Onceki/sonraki ile kayip/solarak degisen tek alt agac -- bkz.
        // GalleryPaintingViewer.AnimateTo basindaki not. Thumbnails ve Header
        // bunun DISINDA kalir, sahnedeki yerleri degismedi.
        RectTransform content = UiFactory.Fill(UiFactory.Rect("Content", root));
        CanvasGroup contentGroup = content.gameObject.AddComponent<CanvasGroup>();

        RectTransform paintingArea = UiFactory.Fill(
            UiFactory.Rect("PaintingArea", content), 0f, HeaderTop + HeaderHeight, 0f, ThumbBandHeight + InfoBandHeight);

        GalleryWorkFrame frame = BuildPaintingSlot(paintingArea, theme);
        BuildNavButtons(
            paintingArea, theme,
            out Button prevButton, out CanvasGroup prevGroup,
            out Button nextButton, out CanvasGroup nextGroup);

        BuildInfo(
            content, theme,
            out TMP_Text nameLabel, out TMP_Text metaLabel,
            out Image statusFill, out TMP_Text statusLabel,
            out GameObject holesChip, out Image holesChipBorder, out Image holesChipFill,
            out TMP_Text holesLabel, out Button holesChipButton, out Image holesChipGlow);

        RectTransform thumbRoot = BuildThumbnails(root);

        GalleryPaintingViewer viewer = root.gameObject.AddComponent<GalleryPaintingViewer>();
        UiWiring.Set(viewer, "fadeGroup", fadeGroup);
        UiWiring.Set(viewer, "closeButton", closeButton);
        UiWiring.Set(viewer, "habitLabel", habitLabel);
        UiWiring.Set(viewer, "posLabel", posLabel);
        UiWiring.Set(viewer, "contentRect", content);
        UiWiring.Set(viewer, "contentGroup", contentGroup);
        UiWiring.Set(viewer, "frame", frame);
        UiWiring.Set(viewer, "prevButton", prevButton);
        UiWiring.Set(viewer, "prevGroup", prevGroup);
        UiWiring.Set(viewer, "nextButton", nextButton);
        UiWiring.Set(viewer, "nextGroup", nextGroup);
        UiWiring.Set(viewer, "nameLabel", nameLabel);
        UiWiring.Set(viewer, "metaLabel", metaLabel);
        UiWiring.Set(viewer, "statusFill", statusFill);
        UiWiring.Set(viewer, "statusLabel", statusLabel);
        UiWiring.Set(viewer, "holesChip", holesChip);
        UiWiring.Set(viewer, "holesChipBorder", holesChipBorder);
        UiWiring.Set(viewer, "holesChipFill", holesChipFill);
        UiWiring.Set(viewer, "holesLabel", holesLabel);
        UiWiring.Set(viewer, "holesChipButton", holesChipButton);
        UiWiring.Set(viewer, "holesChipGlow", holesChipGlow);
        UiWiring.Set(viewer, "thumbPrefab", galleryPrefabs.Thumb.GetComponent<GalleryThumb>());
        UiWiring.Set(viewer, "thumbRoot", thumbRoot);
        UiWiring.Set(viewer, "theme", theme);

        root.gameObject.SetActive(false);
        return viewer;
    }

    // GalleryDetailBuilder.BuildHeader'in aynisi (geri tusu + ortali baslik
    // blogu + sag bos yuva) -- sol dugme digerleriyle AYNI "‹" glifini
    // kullaniyor, tek fark baslik blogu iki satiri (aliskanlik adi +
    // "{i}/{n}") cok daha kucuk puntoda.
    private static void BuildHeader(
        RectTransform parent,
        UiTheme theme,
        UiPrefabBuilder.Prefabs prefabs,
        out TMP_Text habitLabel,
        out TMP_Text posLabel,
        out Button closeButton)
    {
        RectTransform header = UiFactory.StretchTop(UiFactory.Rect("Header", parent), HeaderTop, HeaderHeight, 18f, 18f);

        GameObject close = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.RoundIconButton, header);
        close.name = "CloseButton";
        UiFactory.TopLeft((RectTransform)close.transform, 0f, 0f, 38f, 38f);
        closeButton = close.GetComponentInChildren<Button>();

        RectTransform titleBlock = UiFactory.Rect("TitleBlock", header);
        titleBlock.anchorMin = new Vector2(0.5f, 1f);
        titleBlock.anchorMax = new Vector2(0.5f, 1f);
        titleBlock.pivot = new Vector2(0.5f, 1f);
        titleBlock.anchoredPosition = Vector2.zero;
        titleBlock.sizeDelta = new Vector2(220f, 36f);
        UiFactory.Column(titleBlock, 2f, TextAnchor.UpperCenter);

        habitLabel = UiFactory.Text("Habit", titleBlock, "Alışkanlık", theme.medium, 14f,
            theme.textMuted, TextAlignmentOptions.Center);
        habitLabel.overflowMode = TextOverflowModes.Ellipsis;
        UiFactory.Size(habitLabel.gameObject, -1f, 16f);

        posLabel = UiFactory.Text("Pos", titleBlock, "1 / 1", theme.semiBold, 15f,
            theme.textPrimary, TextAlignmentOptions.Center);
        UiFactory.Size(posLabel.gameObject, -1f, 18f);

        RectTransform spacer = UiFactory.Rect("RightSpacer", header);
        spacer.anchorMin = new Vector2(1f, 1f);
        spacer.anchorMax = new Vector2(1f, 1f);
        spacer.pivot = new Vector2(1f, 1f);
        spacer.anchoredPosition = Vector2.zero;
        spacer.sizeDelta = new Vector2(38f, 38f);
    }

    // GalleryWallItem/BuildWorkTile'daki golge+tablo+ortu+rozet dorteliinin
    // UCUNCU kopyasi -- GalleryWorkFrame zaten bu desenin paylasilan YARISI
    // (bkz. o dosyadaki not); kurulumun KENDISI (hangi cocuklarin hangi
    // sirada eklendigi) her cagiran yerde ayri kalmasi gerekiyor, cunku
    // ebeveyn anchor'lari (slot-ortali / ekran-ortali) farkli.
    private static GalleryWorkFrame BuildPaintingSlot(RectTransform parent, UiTheme theme)
    {
        RectTransform paintingRect = UiFactory.Rect("Painting", parent);
        paintingRect.anchorMin = new Vector2(0.5f, 0.5f);
        paintingRect.anchorMax = new Vector2(0.5f, 0.5f);
        paintingRect.pivot = new Vector2(0.5f, 0.5f);
        paintingRect.anchoredPosition = Vector2.zero;
        paintingRect.sizeDelta = new Vector2(GalleryBands.SlotMinWidth, GalleryBands.SlotHeight);

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

        GalleryWorkFrame frame = parent.gameObject.AddComponent<GalleryWorkFrame>();
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

        return frame;
    }

    private static void BuildNavButtons(
        RectTransform parent,
        UiTheme theme,
        out Button prevButton,
        out CanvasGroup prevGroup,
        out Button nextButton,
        out CanvasGroup nextGroup)
    {
        prevButton = BuildNavButton(parent, theme, "PrevButton", "‹", 0f, out prevGroup);
        nextButton = BuildNavButton(parent, theme, "NextButton", "›", 1f, out nextGroup);
    }

    // edgeAnchorX: 0 = sol kenar, 1 = sag kenar -- tek fonksiyonda hem onceki
    // hem sonraki, ayna simetrisiyle (sign).
    private static Button BuildNavButton(
        RectTransform parent, UiTheme theme, string name, string glyph, float edgeAnchorX, out CanvasGroup group)
    {
        RectTransform root = UiFactory.Rect(name, parent);
        root.anchorMin = new Vector2(edgeAnchorX, 0.5f);
        root.anchorMax = new Vector2(edgeAnchorX, 0.5f);
        root.pivot = new Vector2(edgeAnchorX, 0.5f);

        float sign = edgeAnchorX < 0.5f ? 1f : -1f;
        root.anchoredPosition = new Vector2(sign * NavButtonInset, 0f);
        root.sizeDelta = new Vector2(NavButtonSize, NavButtonSize);

        group = root.gameObject.AddComponent<CanvasGroup>();

        Image border = UiFactory.Image("Border", root, theme.circle, theme.galleryViewerNavBorder);
        UiFactory.Fill(border.rectTransform);
        Button button = UiFactory.Button(border);
        ButtonPressFeedback.EnsureOn(button);

        Image fill = UiFactory.Image("Fill", border.rectTransform, theme.circle, theme.galleryViewerNavBackground);
        UiFactory.Fill(fill.rectTransform, 2f, 2f, 2f, 2f);

        TMP_Text label = UiFactory.Text("Label", border.rectTransform, glyph, theme.semiBold, 20f,
            theme.textPrimary, TextAlignmentOptions.Center);
        UiFactory.Fill(label.rectTransform);

        return button;
    }

    private static void BuildInfo(
        RectTransform parent,
        UiTheme theme,
        out TMP_Text nameLabel,
        out TMP_Text metaLabel,
        out Image statusFill,
        out TMP_Text statusLabel,
        out GameObject holesChip,
        out Image holesChipBorder,
        out Image holesChipFill,
        out TMP_Text holesLabel,
        out Button holesChipButton,
        out Image holesChipGlow)
    {
        RectTransform info = UiFactory.StretchBottom(UiFactory.Rect("Info", parent), ThumbBandHeight, InfoBandHeight, 0f, 0f);
        VerticalLayoutGroup column = UiFactory.Column(info, 8f, TextAnchor.MiddleCenter);
        column.padding = new RectOffset(28, 28, 0, 0);

        nameLabel = UiFactory.Text("Name", info, "Tablo", theme.bold, 28f,
            theme.textPrimary, TextAlignmentOptions.Center);
        nameLabel.overflowMode = TextOverflowModes.Ellipsis;
        UiFactory.Size(nameLabel.gameObject, -1f, 32f);

        metaLabel = UiFactory.Text("Meta", info, "16 parça", theme.regular, 14f,
            theme.textMuted, TextAlignmentOptions.Center);
        UiFactory.Size(metaLabel.gameObject, -1f, 18f);

        RectTransform chips = UiFactory.Rect("Chips", info);
        UiFactory.Row(chips, 8f, TextAnchor.MiddleCenter);
        UiFactory.Size(chips.gameObject, -1f, ChipHeight);

        BuildStatusChip(chips, theme, out statusFill, out statusLabel);
        holesChip = BuildHolesChip(chips, theme, out holesChipBorder, out holesChipFill,
            out holesLabel, out holesChipButton, out holesChipGlow);
    }

    // Genislik SABIT (dinamik metin-olcusune gore degil) -- iki metin de
    // ("Tamamlandı" / "Sürüyor · 12/64") kisa, GalleryHabitCard/GalleryWallItem'in
    // dinamik FitToText'ine burada gerek yok.
    private static void BuildStatusChip(RectTransform parent, UiTheme theme, out Image fill, out TMP_Text label)
    {
        RectTransform root = UiFactory.Rect("StatusChip", parent);
        UiFactory.Size(root.gameObject, StatusChipWidth, ChipHeight);

        Image border = root.gameObject.AddComponent<Image>();
        border.sprite = theme.Rounded(14);
        border.type = Image.Type.Sliced;
        border.color = theme.gold;

        fill = UiFactory.Image("Fill", root, theme.Rounded(12), theme.gold);
        UiFactory.Fill(fill.rectTransform, 2f, 2f, 2f, 2f);

        label = UiFactory.Text("Label", root, "Tamamlandı", theme.semiBold, 12f,
            theme.textOnGold, TextAlignmentOptions.Center);
        UiFactory.Fill(label.rectTransform);
    }

    // Varsayilan KAPALI -- cogu bitmis tablonun deligi yok (bkz.
    // GalleryWorkFrame'in holeBadge'iyle ayni varsayilan).
    //
    // Fill katmani YENI: eskiden border tek basina hem kenarlik hem zemin
    // tonuydu (fill'siz). Rotus aktifken zemin ayrica mavi bir dolgu alir
    // (rgba(44,79,184,.35)), bu yuzden GalleryWorkFrame'in rozetindeki
    // border-alti-fill kalibi burada da gerekti.
    private static GameObject BuildHolesChip(
        RectTransform parent,
        UiTheme theme,
        out Image border,
        out Image fill,
        out TMP_Text label,
        out Button button,
        out Image glow)
    {
        RectTransform root = UiFactory.Rect("HolesChip", parent);
        UiFactory.Size(root.gameObject, HolesChipWidth, ChipHeight);

        // Parilti cipin ARKASINDA, kenarlardan HolesChipGlowSpread tasarak --
        // badge'in "merkezi sabit tut" aritmetigi yerine burada daha basit
        // bir negatif-Fill tasmasi yeterli (GalleryWallItem'in golge kalibi).
        glow = UiFactory.Image("Glow", root, theme.radialGlow, theme.rotusGlow, false);
        UiFactory.Fill(glow.rectTransform, -HolesChipGlowSpread, -HolesChipGlowSpread, -HolesChipGlowSpread, -HolesChipGlowSpread);
        glow.gameObject.SetActive(false);

        border = root.gameObject.AddComponent<Image>();
        border.sprite = theme.Rounded(14);
        border.type = Image.Type.Sliced;
        border.color = theme.galleryHolesChipBorder;

        fill = UiFactory.Image("Fill", root, theme.Rounded(12), Color.clear);
        UiFactory.Fill(fill.rectTransform, 2f, 2f, 2f, 2f);

        RectTransform row = UiFactory.Fill(UiFactory.Rect("Row", root), 10f, 0f, 10f, 0f);
        UiFactory.Row(row, 6f, TextAnchor.MiddleCenter);

        RectTransform swatchRect = UiFactory.Rect("Swatch", row);
        UiFactory.Size(swatchRect.gameObject, SwatchSize, SwatchSize);

        Image swatch = swatchRect.gameObject.AddComponent<Image>();
        swatch.sprite = theme.hatch;
        swatch.type = Image.Type.Tiled;
        swatch.raycastTarget = false;

        label = UiFactory.Text("Label", row, "0 parça yandı", theme.semiBold, 12f,
            theme.galleryHolesChipText, TextAlignmentOptions.Left);

        // Rotus'un tiklama hedefi -- varsayilan KAPALI, GalleryWorkFrame'in
        // rozet dugmesiyle ayni gerekce (bkz. GalleryPrefabBuilder.BuildHoleBadge).
        button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = border;
        button.transition = Selectable.Transition.None;
        button.enabled = false;

        root.gameObject.SetActive(false);
        return root.gameObject;
    }

    // Yatayda tasarsa yana kayan serit -- UiFactory.ScrollView'in yeni
    // horizontal=true kolu (bkz. o dosyadaki not).
    private static RectTransform BuildThumbnails(RectTransform parent)
    {
        RectTransform band = UiFactory.StretchBottom(UiFactory.Rect("Thumbnails", parent), 0f, ThumbBandHeight, 0f, 0f);
        RectTransform scrollRoot = UiFactory.Fill(UiFactory.Rect("Scroll", band), 20f, 26f, 20f, 40f);

        RectTransform content = UiFactory.ScrollView(scrollRoot, out _, out _, true);
        UiFactory.Row(content, 10f, TextAnchor.MiddleCenter);
        UiFactory.FitHorizontal(content.gameObject);

        return content;
    }

    // -----------------------------------------------------------------------
    // GalleryThumb prefab'i -- GalleryPrefabBuilder.Build'in cagirdigi,
    // GalleryDetailPrefabBuilder'daki WorkTile/StatTile ile AYNI "insasi
    // burada, Prefabs sonucu orada" ayrimi.
    // -----------------------------------------------------------------------

    public static GameObject BuildThumb(UiTheme theme)
    {
        RectTransform root = Root("GalleryThumb", ThumbSize, ThumbSize);
        UiFactory.Size(root.gameObject, ThumbSize, ThumbSize);

        Image border = root.gameObject.AddComponent<Image>();
        border.sprite = theme.Rounded(12);
        border.type = Image.Type.Sliced;
        border.color = theme.borderSoft;
        Button button = UiFactory.Button(border);

        Image fill = UiFactory.Image("Fill", root, theme.Rounded(10), theme.inputBackground);
        UiFactory.Fill(fill.rectTransform, 2f, 2f, 2f, 2f);

        RectTransform paintingRect = UiFactory.Rect("Painting", fill.rectTransform);
        paintingRect.anchorMin = new Vector2(0.5f, 0.5f);
        paintingRect.anchorMax = new Vector2(0.5f, 0.5f);
        paintingRect.pivot = new Vector2(0.5f, 0.5f);
        paintingRect.anchoredPosition = Vector2.zero;
        paintingRect.sizeDelta = new Vector2(36f, 36f);

        Image painting = UiFactory.Image("Image", paintingRect, null, Color.white, false);
        UiFactory.Fill(painting.rectTransform);

        GalleryThumb thumb = root.gameObject.AddComponent<GalleryThumb>();
        UiWiring.Set(thumb, "button", button);
        UiWiring.Set(thumb, "border", border);
        UiWiring.Set(thumb, "paintingRect", paintingRect);
        UiWiring.Set(thumb, "painting", painting);
        UiWiring.Set(thumb, "theme", theme);

        return root.gameObject;
    }

    private static RectTransform Root(string name, float width, float height)
    {
        RectTransform rect = UiFactory.Rect(name, null);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }
}
