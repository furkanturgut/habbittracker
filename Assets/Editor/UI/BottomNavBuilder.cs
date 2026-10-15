using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Alt navigasyon cubugunu kurar. Ayri dosya: WorkshopSceneBuilder/
// HabitsPanelBuilder zaten 500 satir butcesinin sinirinda (bkz.
// PaintingChoiceSheetBuilder ile ayni gerekce).
//
// PANEL BASINA BIR ORNEK cagriliyor (toplam 3) -- paylasimli tek cubuk degil.
// Sebebi NAVIGATION_SETTINGS_PLAN.md Faz 2'de: cubuk Aliskanliklar'in alt
// sayfasi ve Galeri'nin tablo goruntuleyicisi tarafindan ORTULMELI; o ikisi
// panellerin ICINDE yasadigi icin paylasimli bir cubuk ya onlarin altinda
// kalirdi ya da ustunde (alt sayfa ortulemez). Her panel kendi cizim sirasina
// gore cubugu dogru yere ekliyor (bkz. cagiran taraf).
public static class BottomNavBuilder
{
    public const string RootName = "BottomNavBar";

    private static readonly string[] TabLabelKeys = { "nav.tab.workshop", "nav.tab.habits", "nav.tab.gallery" };

    public static BottomNavBar Build(RectTransform panel, UiTheme theme, ScreenNavigator navigator, int activeIndex)
    {
        RectTransform root = UiFactory.StretchBottom(
            UiFactory.Rect(RootName, panel), 0f, BottomNavBands.DesignHeight, 0f, 0f);

        BuildBackground(root, theme);

        float rowHeight = BottomNavBands.ContentHeight - BottomNavBands.TopPadding;
        RectTransform content = UiFactory.StretchTop(
            UiFactory.Rect("Content", root),
            BottomNavBands.TopPadding,
            rowHeight,
            BottomNavBands.SidePadding,
            BottomNavBands.SidePadding);

        float tabWidth = BottomNavBands.TabWidth(LayoutMetrics.DesignWidth);
        Sprite[] icons = { theme.navTabAtolye, theme.navTabHabits, theme.navTabGallery };

        NavTab[] tabs = new NavTab[3];
        float x = 0f;

        for (int i = 0; i < 3; i++)
        {
            tabs[i] = BuildTab(content, theme, navigator, i, x, tabWidth, rowHeight, icons[i], TabLabelKeys[i]);
            x += tabWidth + BottomNavBands.TabGap;
        }

        BuildSeparator(content, theme, x, rowHeight);
        x += BottomNavBands.SeparatorWidth + 2f * BottomNavBands.SeparatorMargin + BottomNavBands.TabGap;

        BuildMoodColumn(content, theme, x, rowHeight);

        BottomNavBar bar = root.gameObject.AddComponent<BottomNavBar>();
        UiWiring.SetArray(bar, "tabs", tabs);
        UiWiring.Set(bar, "navigator", navigator);

        // Calisma ani Start()'i gercek navigator.Current'i okuyup bunu zaten
        // dogru deger ile yeniden yazacak -- bu yalnizca sahne dosyasinda
        // (Editor'de, Play moduna girmeden) mantikli bir acilis durumu birakir.
        for (int i = 0; i < tabs.Length; i++)
        {
            tabs[i].SetSelected(i == activeIndex, true);
        }

        return bar;
    }

    // Zemin: ust kenarlik + gradyan, guvenli alanin ALTINA (ekranin dibine)
    // kadar bleedBottom ile taniyor. Icerik (sekmeler) bundan AYRI -- guvenli
    // alanda sabit kalan Content rect'inde (bkz. Build).
    private static void BuildBackground(RectTransform root, UiTheme theme)
    {
        Image background = UiFactory.Image("Background", root, theme.white, Color.white, false);
        UiFactory.Fill(background.rectTransform);
        // CSS linear-gradient(180deg,#3e2a1c,#2b1b12) -- iki ucu da var olan
        // alanlar (cardBottom ustte, background altta), yeni renk gerekmedi.
        UiFactory.Gradient(background.gameObject, theme.cardBottom, theme.background);
        FullBleedFitter fitter = background.gameObject.AddComponent<FullBleedFitter>();
        UiWiring.SetBool(fitter, "bleedBottom", true);

        Image topBorder = UiFactory.Image("TopBorder", root, theme.white, theme.navTopBorder, false);
        UiFactory.StretchTop(topBorder.rectTransform, 0f, BottomNavBands.TopBorderHeight, 0f, 0f);
    }

    // x: sekmenin Content icindeki sol kenari (tasarim pikseli).
    private static NavTab BuildTab(
        RectTransform parent,
        UiTheme theme,
        ScreenNavigator navigator,
        int index,
        float x,
        float width,
        float height,
        Sprite icon,
        string labelKey)
    {
        RectTransform tabRect = UiFactory.TopLeft(UiFactory.Rect("Tab " + index, parent), x, 0f, width, height);

        // Dokunma hedefi: PaintingViewPool'un TileScroll "hit" kalibi ile ayni
        // -- gorunmez, yalnizca raycast.
        Image hit = UiFactory.Image("Hit", tabRect, theme.white, Color.clear, false);
        UiFactory.Fill(hit.rectTransform);
        Button button = UiFactory.Button(hit);

        float stackHeight = BottomNavBands.IconSize + BottomNavBands.IconLabelGap + BottomNavBands.LabelHeight;
        float stackTop = (height - stackHeight) * 0.5f;

        Image iconImage = UiFactory.Image("Icon", tabRect, icon, theme.navLabelInactive, false);
        RectTransform iconRect = iconImage.rectTransform;
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.sizeDelta = new Vector2(BottomNavBands.IconSize, BottomNavBands.IconSize);
        iconRect.anchoredPosition = new Vector2(0f, -stackTop);

        TMP_Text labelText = UiFactory.LocalizedText(
            "Label", tabRect, labelKey, theme.medium, 11f, theme.navLabelInactive, TextAlignmentOptions.Center);
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.sizeDelta = new Vector2(0f, BottomNavBands.LabelHeight);
        labelRect.anchoredPosition = new Vector2(
            0f, -(stackTop + BottomNavBands.IconSize + BottomNavBands.IconLabelGap));

        // Elmas gostergesi kaldirildi (kullanici talebi): NavTab'in
        // diamondGroup alani null birakiliyor, zaten her okuma null-check'li
        // (bkz. NavTab.CurrentDiamondAlpha/ApplyVisual) -- aktif sekme artik
        // yalnizca ikon/etiket rengiyle (altin) belli oluyor.
        NavTab tab = tabRect.gameObject.AddComponent<NavTab>();
        UiWiring.Set(tab, "icon", iconImage);
        UiWiring.Set(tab, "label", labelText);
        UiWiring.Set(tab, "theme", theme);

        // ScreenNavButton Awake'inde ButtonPressFeedback.EnsureOn'u kendisi
        // cagiriyor -- burada ayrica cagirmaya gerek yok.
        ScreenNavButton navButton = tabRect.gameObject.AddComponent<ScreenNavButton>();
        UiWiring.Set(navButton, "button", button);
        UiWiring.Set(navButton, "navigator", navigator);
        UiWiring.SetInt(navButton, "targetIndex", index);

        return tab;
    }

    // flexStart: ayiricinin KENDI flex-ogesi (kendi kenar bosluklari dahil)
    // nerede basliyor. CSS margin:6px 6px 2px -- ust/yan 6 (SeparatorMargin),
    // alt 2 farkli (asimetrik, adlandirilmis sabit yok, teslim dosyasindan
    // birebir).
    private static void BuildSeparator(RectTransform parent, UiTheme theme, float flexStart, float rowHeight)
    {
        const float bottomMargin = 2f;

        float lineX = flexStart + BottomNavBands.SeparatorMargin;
        float lineHeight = rowHeight - BottomNavBands.SeparatorMargin - bottomMargin;

        Image line = UiFactory.Image("Separator", parent, theme.white, theme.navSeparator, false);
        UiRect.TopLeft(line.rectTransform, lineX, BottomNavBands.SeparatorMargin, BottomNavBands.SeparatorWidth, lineHeight);
    }

    private static void BuildMoodColumn(RectTransform parent, UiTheme theme, float x, float rowHeight)
    {
        const float moodGap = 3f;
        const float moodLabelHeight = 12f;

        RectTransform column = UiRect.TopLeft(
            UiFactory.Rect("MoodColumn", parent), x, 0f, BottomNavBands.MoodColumnWidth, rowHeight);

        float stackHeight = BottomNavBands.MoodBadgeSize + moodGap + moodLabelHeight;
        float badgeTop = (rowHeight - stackHeight) * 0.5f;
        float badgeLeft = (BottomNavBands.MoodColumnWidth - BottomNavBands.MoodBadgeSize) * 0.5f;

        Image ring = UiFactory.Image("Badge", column, theme.circle, theme.gold);
        UiRect.TopLeft(ring.rectTransform, badgeLeft, badgeTop, BottomNavBands.MoodBadgeSize, BottomNavBands.MoodBadgeSize);

        float innerSize = BottomNavBands.MoodBadgeSize - 2f * BottomNavBands.MoodBadgeRing;
        Image inner = UiFactory.Image("Fill", ring.rectTransform, theme.circle, theme.burgundy);
        UiRect.TopLeft(inner.rectTransform, BottomNavBands.MoodBadgeRing, BottomNavBands.MoodBadgeRing, innerSize, innerSize);

        // Sprite NavMoodBadge tarafindan atanana kadar kapali -- MoodCircle'daki
        // "sprite yoksa Image kapat" kuralinin aynisi.
        Image icon = UiFactory.Image("Icon", inner.rectTransform, null, Color.white, false);
        UiFactory.Fill(icon.rectTransform);
        icon.enabled = false;

        TMP_Text label = UiFactory.Text("Label", column, "", theme.semiBold, 10f, theme.gold, TextAlignmentOptions.Center);
        // Etiket rect'i MoodColumnWidth'ten (62) GENIS ve ortali: "COK ZENGIN"
        // 10pt'de 62'ye sigmiyor -- kirpmak yerine simetrik tasiyoruz.
        label.overflowMode = TextOverflowModes.Overflow;
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0.5f, 1f);
        labelRect.anchorMax = new Vector2(0.5f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.sizeDelta = new Vector2(120f, moodLabelHeight);
        labelRect.anchoredPosition = new Vector2(
            0f, -(badgeTop + BottomNavBands.MoodBadgeSize + moodGap));

        NavMoodBadge badge = column.gameObject.AddComponent<NavMoodBadge>();
        UiWiring.Set(badge, "icon", icon);
        UiWiring.Set(badge, "label", label);
        UiWiring.Set(badge, "theme", theme);
    }
}
