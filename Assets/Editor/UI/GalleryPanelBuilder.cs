using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 03 Galeri ekraninin 1. seviyesi: baslik + alt alta bolumler (duvarlar).
// HabitsPanelBuilder.Build'in iskeletinin aynisi (bkz. o dosyadaki yorum) --
// geri tusu 02'ye donuyor, giris 02'nin basligindaki yeni rozetten geliyor
// (bkz. HabitsPanelBuilder.BuildHeader).
public static class GalleryPanelBuilder
{
    public const string PanelName = "Panel_Gallery";

    public static GameObject Build(
        RectTransform canvas,
        UiTheme theme,
        UiPrefabBuilder.Prefabs prefabs,
        GalleryPrefabBuilder.Prefabs galleryPrefabs,
        HabbitManager manager,
        ScreenNavigator navigator,
        out Button backButton)
    {
        RectTransform panel = UiFactory.Fill(UiFactory.Rect(PanelName, canvas));

        Image background = UiFactory.Image("Background", panel, theme.white, Color.white, false);
        UiFactory.Fill(background.rectTransform);
        UiFactory.Gradient(background.gameObject, theme.surface, theme.background);
        background.gameObject.AddComponent<FullBleedFitter>();

        BuildHeader(panel, theme, prefabs, out TMP_Text totalLabel, out backButton);

        RectTransform content = BuildList(panel);

        // EN SON eklenen cocuk: kart listesinin USTUNDE cizilmesi gerekiyor
        // (bkz. GalleryDetailBuilder basindaki not).
        GalleryDetailView detailView = GalleryDetailBuilder.Build(panel, theme, prefabs, galleryPrefabs);

        // GalleryDetail'den SONRA, goruntuleyiciden ONCE: teslim dosyasi
        // "detay gorunumunde cubuk gorunur kalir" diyor (detayin kendi zemini
        // tam ekran, cubugu KAPATMAZ); goruntuleyici cubuktan sonra eklenerek
        // onu ORTER (bkz. NAVIGATION_SETTINGS_PLAN.md Faz 2).
        BottomNavBuilder.Build(panel, theme, navigator, 2);

        // Seviye 3, Seviye 2'den de SONRA ekleniyor: ikisi birden acikken
        // goruntuleyici en ustte cizilmeli (bkz. GalleryPaintingViewer
        // basindaki "Sahiplik" notu).
        GalleryPaintingViewer viewer = GalleryViewerBuilder.Build(panel, theme, prefabs, galleryPrefabs);

        // Rotus onarim sayfasi EN SON cocuk: goruntuleyicinin de ustunde
        // cizilmeli (bkz. RotusRestoreSheetBuilder basindaki not).
        RotusRestoreSheet restoreSheet = RotusRestoreSheetBuilder.Build(panel, theme, manager, navigator);

        GalleryScreen screen = panel.gameObject.AddComponent<GalleryScreen>();
        UiWiring.Set(screen, "manager", manager);
        UiWiring.Set(screen, "totalLabel", totalLabel);
        UiWiring.Set(screen, "cardPrefab", galleryPrefabs.HabitCard.GetComponent<GalleryHabitCard>());
        UiWiring.Set(screen, "content", content);
        UiWiring.Set(screen, "detailView", detailView);
        UiWiring.Set(screen, "viewer", viewer);
        UiWiring.Set(screen, "restoreSheet", restoreSheet);

        panel.gameObject.SetActive(false);
        return panel.gameObject;
    }

    private static void BuildHeader(
        RectTransform parent,
        UiTheme theme,
        UiPrefabBuilder.Prefabs prefabs,
        out TMP_Text totalLabel,
        out Button backButton)
    {
        RectTransform header = UiFactory.StretchTop(UiFactory.Rect("Header", parent), 6f, 46f, 18f, 18f);

        GameObject back = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.RoundIconButton, header);
        back.name = "BackButton";
        UiFactory.TopLeft((RectTransform)back.transform, 0f, 0f, 38f, 38f);
        backButton = back.GetComponentInChildren<Button>();

        RectTransform titleBlock = UiFactory.Rect("TitleBlock", header);
        titleBlock.anchorMin = new Vector2(0.5f, 1f);
        titleBlock.anchorMax = new Vector2(0.5f, 1f);
        titleBlock.pivot = new Vector2(0.5f, 1f);
        titleBlock.anchoredPosition = Vector2.zero;
        titleBlock.sizeDelta = new Vector2(240f, 46f);
        UiFactory.Column(titleBlock, 2f, TextAnchor.UpperCenter);

        totalLabel = UiFactory.Text("TotalLabel", titleBlock, "0 eser", theme.medium, 14f,
            theme.textMuted, TextAlignmentOptions.Center);
        UiFactory.Size(totalLabel.gameObject, -1f, 16f);

        TMP_Text title = UiFactory.LocalizedText("Title", titleBlock, "gallery_panel.title", theme.bold, 28f,
            theme.textPrimary, TextAlignmentOptions.Center);
        UiFactory.Size(title.gameObject, -1f, 32f);

        // Sagda eskiden bos bir yuva vardi ("empty spacer") -- rotus teslimi
        // bu yuvaya stok pilini oturtuyor (bkz. RotusPillBuilder basindaki not).
        RotusPillBuilder.Build(header, theme);
    }

    private static RectTransform BuildList(RectTransform parent)
    {
        RectTransform scroll = UiFactory.Fill(UiFactory.Rect("ListScroll", parent), 0f, 58f, 0f, 0f);

        // Bos alana dokunup kaydirabilmek icin gorunmez bir raycast hedefi
        // (HabitsPanelBuilder.BuildList ile ayni kalip).
        Image hit = UiFactory.Image("Hit", scroll, null, Color.clear, false);
        UiFactory.Fill(hit.rectTransform);
        hit.raycastTarget = true;

        RectTransform content = UiFactory.ScrollView(scroll, out _, out _);

        VerticalLayoutGroup column = UiFactory.Column(content, GalleryBands.SectionGap);

        // Yatay dolgu = kartin yan boslugu (teslim dosyasi: "margin:0 20").
        // Column childForceExpandWidth ile cocugu kolonun TAMAMINA gerdigi icin
        // bu dolgu yoksa bolum kokü 366 birim olur: kart tam kenara dayanir ve
        // baslik ekranin soluna yapisir. HabitsPanelBuilder.BuildList ile ayni
        // kalip -- oradaki kart genisligi de (326) buradan geliyor.
        // 44 -> 110: alt navigasyon cubugu artik ekranin dibini kapliyor,
        // son satir kendi dibine tam oturmak icin cubuk kadar + pay istiyor
        // (HabitsPanelBuilder.BuildList ile ayni sayi -- iki ekranin listesi
        // ayni desende, bkz. NAVIGATION_SETTINGS_PLAN.md Faz 2).
        int side = (int)GalleryBands.CardSideMargin;
        column.padding = new RectOffset(side, side, 20, 110);
        UiFactory.FitVertical(content.gameObject);

        return content;
    }
}
