using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 03 Galeri ekraninin 2. seviyesi: GalleryPanelBuilder tarafindan EN SON
// cagrilir, panelin SON cocugu olarak eklenir ki kart listesinin USTUNE
// cizilsin (bkz. GalleryDetailView'in kart->tam-ekran rect morph'u).
//
// GalleryHabitCardBuilder'in "Card" iskeleti gibi degil -- burada kose
// yaricapi hic animasyonlanmiyor (bkz. GalleryDetailView basindaki not),
// bu yuzden hem "Background" (sayfa gorunumu) hem "Skin" (kart gorunumu)
// katmanlari DUZ dikdortgen Image, Rounded() hic kullanilmiyor.
public static class GalleryDetailBuilder
{
    private const float HeaderHeight = 50f;

    public static GalleryDetailView Build(
        RectTransform parent,
        UiTheme theme,
        UiPrefabBuilder.Prefabs prefabs,
        GalleryPrefabBuilder.Prefabs galleryPrefabs)
    {
        RectTransform root = UiFactory.Rect("GalleryDetail", parent);
        root.sizeDelta = new Vector2(326f, 600f);

        Image background = UiFactory.Image("Background", root, theme.white, Color.white, false);
        UiFactory.Fill(background.rectTransform);
        UiFactory.Gradient(background.gameObject, theme.surface, theme.background);

        Image skin = UiFactory.Image("Skin", root, theme.white, Color.white, false);
        UiFactory.Fill(skin.rectTransform);
        UiFactory.Gradient(skin.gameObject, theme.cardTop, theme.cardBottom, 160f);
        CanvasGroup skinGroup = skin.gameObject.AddComponent<CanvasGroup>();

        RectTransform content = UiFactory.Fill(UiFactory.Rect("Content", root));
        CanvasGroup contentGroup = content.gameObject.AddComponent<CanvasGroup>();

        BuildHeader(content, theme, prefabs, out TMP_Text sub, out TMP_Text title, out Button back);

        RectTransform body = BuildBody(content);

        BuildHero(body, theme, out TMP_Text heroTotal, out TMP_Text heroLabel, out TMP_Text heroNote);
        BuildStats(body, theme, galleryPrefabs, out TMP_Text[] statValues, out TMP_Text[] statLabels,
            out FlameSprite streakFlame, out Image streakBar, out TMP_Text streakNext);
        BuildPlan(body, theme, prefabs, out TMP_Text planLabel, out DayChipRow planDays);
        RectTransform tileRoot = BuildWorks(body, theme, galleryPrefabs, out TMP_Text worksLabel);

        GalleryDetailView view = root.gameObject.AddComponent<GalleryDetailView>();
        UiWiring.Set(view, "panel", root);
        UiWiring.Set(view, "skinGroup", skinGroup);
        UiWiring.Set(view, "contentGroup", contentGroup);
        UiWiring.Set(view, "backButton", back);
        UiWiring.Set(view, "subLabel", sub);
        UiWiring.Set(view, "titleLabel", title);
        UiWiring.Set(view, "heroTotal", heroTotal);
        UiWiring.Set(view, "heroLabel", heroLabel);
        UiWiring.Set(view, "heroNote", heroNote);
        UiWiring.SetArray(view, "statValues", statValues);
        UiWiring.SetArray(view, "statLabels", statLabels);
        UiWiring.Set(view, "streakFlame", streakFlame);
        UiWiring.Set(view, "streakBar", streakBar);
        UiWiring.Set(view, "streakNext", streakNext);
        UiWiring.Set(view, "theme", theme);
        UiWiring.Set(view, "planLabel", planLabel);
        UiWiring.Set(view, "planDays", planDays);
        UiWiring.Set(view, "worksLabel", worksLabel);
        UiWiring.Set(view, "tilePrefab", galleryPrefabs.WorkTile.GetComponent<GalleryWorkTile>());
        UiWiring.Set(view, "tileRoot", tileRoot);

        root.gameObject.SetActive(false);
        return view;
    }

    // GalleryPanelBuilder.BuildHeader'in aynisi (geri tusu + ortali baslik
    // bloğu + sag rotus pili), tek fark alt satirin burada tur/kategori
    // ("sub") gostermesi -- teslim dosyasinin x.sub/x.title ikilisi.
    private static void BuildHeader(
        RectTransform parent,
        UiTheme theme,
        UiPrefabBuilder.Prefabs prefabs,
        out TMP_Text sub,
        out TMP_Text title,
        out Button backButton)
    {
        RectTransform header = UiFactory.StretchTop(UiFactory.Rect("Header", parent), 6f, HeaderHeight, 18f, 18f);

        GameObject back = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.RoundIconButton, header);
        back.name = "BackButton";
        UiFactory.TopLeft((RectTransform)back.transform, 0f, 0f, 38f, 38f);
        backButton = back.GetComponentInChildren<Button>();

        RectTransform titleBlock = UiFactory.Rect("TitleBlock", header);
        titleBlock.anchorMin = new Vector2(0.5f, 1f);
        titleBlock.anchorMax = new Vector2(0.5f, 1f);
        titleBlock.pivot = new Vector2(0.5f, 1f);
        titleBlock.anchoredPosition = Vector2.zero;
        titleBlock.sizeDelta = new Vector2(260f, 46f);
        UiFactory.Column(titleBlock, 2f, TextAnchor.UpperCenter);

        sub = UiFactory.Text("Sub", titleBlock, "Sağlık · Yap", theme.medium, 14f,
            theme.textMuted, TextAlignmentOptions.Center);
        UiFactory.Size(sub.gameObject, -1f, 16f);

        title = UiFactory.Text("Title", titleBlock, "Alışkanlık", theme.bold, 28f,
            theme.textPrimary, TextAlignmentOptions.Center);
        title.overflowMode = TextOverflowModes.Ellipsis;
        UiFactory.Size(title.gameObject, -1f, 32f);

        // GalleryPanelBuilder.BuildHeader'in AYNI nedeni: settings/back dugmesi
        // geri tusuna donuse bile pill ayni ankrajda "yerinde durmus" gorunmeli.
        RotusPillBuilder.Build(header, theme);
    }

    // Kaydirmali govde: teslim dosyasi "padding:24px 20px 44px", bolum
    // araligi 24 (CLAUDE.md'deki GalleryBands.SectionGap=26'dan KASITLI
    // FARKLI -- bu seviyenin kendi teslim dosyasi 24 diyor, 1. seviyenin
    // duvar araligi degil).
    private static RectTransform BuildBody(RectTransform parent)
    {
        RectTransform scroll = UiFactory.Fill(UiFactory.Rect("Body", parent), 0f, HeaderHeight + 8f, 0f, 0f);

        // Bos alana dokunup kaydirabilmek icin gorunmez bir raycast hedefi
        // (GalleryPanelBuilder.BuildList ile ayni kalip).
        Image hit = UiFactory.Image("Hit", scroll, null, Color.clear, false);
        UiFactory.Fill(hit.rectTransform);
        hit.raycastTarget = true;

        RectTransform content = UiFactory.ScrollView(scroll, out _, out _);

        // 44 -> 132: detay gorunumu TAM EKRAN cizildigi icin (bkz. dosya
        // basi notu) alt navigasyon cubugunu kendisi kapliyor; son satir
        // cubuk kadar + pay ile dibe oturuyor (teslim dosyasi, bkz.
        // NAVIGATION_SETTINGS_PLAN.md Faz 2).
        VerticalLayoutGroup column = UiFactory.Column(content, 24f);
        column.padding = new RectOffset(20, 20, 24, 132);
        UiFactory.FitVertical(content.gameObject);

        return content;
    }

    private static void BuildHero(
        RectTransform parent,
        UiTheme theme,
        out TMP_Text total,
        out TMP_Text label,
        out TMP_Text note)
    {
        RectTransform hero = UiFactory.Rect("Hero", parent);
        UiFactory.Column(hero, 4f, TextAnchor.UpperCenter);
        UiFactory.FitVertical(hero.gameObject);

        total = UiFactory.Text("Total", hero, "0", theme.bold, 64f,
            theme.gold, TextAlignmentOptions.Center);
        UiFactory.HardShadow(total.gameObject, 4f, theme.hardShadow);
        UiFactory.Size(total.gameObject, -1f, 70f);

        label = UiFactory.Text("Label", hero, "gün yapıldı", theme.medium, 16f,
            theme.textPrimary, TextAlignmentOptions.Center);
        UiFactory.Size(label.gameObject, -1f, 20f);

        note = UiFactory.Text("Note", hero, string.Empty, theme.regular, 12f,
            theme.textMuted, TextAlignmentOptions.Center);
        note.textWrappingMode = TextWrappingModes.Normal;
        UiFactory.Size(note.gameObject, -1f, 16f);
    }

    // Iki satir, her satirda uc ES GENISLIKTE karo: GridLayoutGroup/GridCellFitter
    // degil, cunku sayisi hic degismiyor ve karolar KARE olmak zorunda degil --
    // duz bir Row + her karonun kendi flexibleWidth'i (bkz.
    // GalleryPrefabBuilder.BuildStatTile). 1. satir seri/en iyi seri/eser,
    // 2. satir GalleryWorks.ConsistencyStats'in uc gostergesi
    // (GalleryDetailView.Bind sirayi 0..5 olarak dolduruyor).
    private static void BuildStats(
        RectTransform parent,
        UiTheme theme,
        GalleryPrefabBuilder.Prefabs galleryPrefabs,
        out TMP_Text[] values,
        out TMP_Text[] labels,
        out FlameSprite streakFlame,
        out Image streakBar,
        out TMP_Text streakNext)
    {
        RectTransform container = UiFactory.Rect("Stats", parent);
        UiFactory.Column(container, 10f);
        UiFactory.FitVertical(container.gameObject);

        values = new TMP_Text[6];
        labels = new TMP_Text[6];

        BuildStatRow(container, theme, galleryPrefabs, "Row1", values, labels, 0,
            out streakFlame, out streakBar, out streakNext);
        BuildStatRow(container, theme, galleryPrefabs, "Row2", values, labels, 3,
            out _, out _, out _);
    }

    // 0. karo "Su anki seri" ve kendi prefabi var (alev + ilerleme cubugu +
    // alt yazi); kalan bes karo paylasilan StatTile. Seri karosunun uc fazla
    // parcasi yalnizca ILK satirda doluyor, ikinci satirda null kaliyor.
    private static void BuildStatRow(
        RectTransform parent,
        UiTheme theme,
        GalleryPrefabBuilder.Prefabs galleryPrefabs,
        string name,
        TMP_Text[] values,
        TMP_Text[] labels,
        int offset,
        out FlameSprite streakFlame,
        out Image streakBar,
        out TMP_Text streakNext)
    {
        streakFlame = null;
        streakBar = null;
        streakNext = null;

        RectTransform row = UiFactory.Rect(name, parent);
        UiFactory.Row(row, 10f);
        UiFactory.Size(row.gameObject, -1f, GalleryDetailPrefabBuilder.StatTileHeight);

        for (int i = 0; i < 3; i++)
        {
            bool streakCell = offset == 0 && i == 0;

            GameObject source = streakCell ? galleryPrefabs.StreakStatTile : galleryPrefabs.StatTile;
            GameObject tile = (GameObject)PrefabUtility.InstantiatePrefab(source, row);

            // Seri karosunda "Value" bir satir daha derinde (alevin yanina
            // alindigi icin), etiket ikisinde de ayni yerde.
            values[offset + i] = tile.transform
                .Find(streakCell ? "Fill/Body/ValueRow/Value" : "Fill/Body/Value")
                .GetComponent<TMP_Text>();
            labels[offset + i] = tile.transform.Find("Fill/Body/Label").GetComponent<TMP_Text>();

            if (!streakCell)
            {
                continue;
            }

            streakFlame = tile.transform.Find("Fill/Body/ValueRow/FlameSlot/Image").GetComponent<FlameSprite>();
            streakBar = tile.transform.Find("Fill/Body/BarRow/BarTrack/BarFill").GetComponent<Image>();
            streakNext = tile.transform.Find("Fill/Body/Next").GetComponent<TMP_Text>();
        }
    }

    // Plan baslik satiri + gun rozetleri. DayChipRow burada interactable=false,
    // useInitials=false ile kullaniliyor -- kartin salt-okunur rozetleriyle
    // AYNI "yonetim ekrani degil, sadece gosterim" karari (bkz. DayChipRow.cs).
    private static void BuildPlan(
        RectTransform parent,
        UiTheme theme,
        UiPrefabBuilder.Prefabs prefabs,
        out TMP_Text planLabel,
        out DayChipRow days)
    {
        RectTransform section = SectionHeader(parent, theme, "gallery_detail.plan_caption", out planLabel);

        RectTransform row = UiFactory.Rect("Days", section);
        UiFactory.Row(row, 6f);
        UiFactory.Size(row.gameObject, -1f, 36f);

        days = row.gameObject.AddComponent<DayChipRow>();
        UiWiring.Set(days, "chipPrefab", prefabs.PlanDayChip.GetComponent<OptionButton>());
        UiWiring.Set(days, "root", row);
        UiWiring.SetBool(days, "useInitials", false);
        UiWiring.SetBool(days, "interactable", false);
    }

    private static RectTransform BuildWorks(
        RectTransform parent,
        UiTheme theme,
        GalleryPrefabBuilder.Prefabs galleryPrefabs,
        out TMP_Text worksLabel)
    {
        RectTransform section = SectionHeader(parent, theme, "gallery_detail.works_caption", out worksLabel);

        RectTransform grid = UiFactory.Rect("Grid", section);
        GridLayoutGroup gridGroup = grid.gameObject.AddComponent<GridLayoutGroup>();
        gridGroup.spacing = new Vector2(12f, 12f);

        // GridCellFitter spacing'i Apply()'da okuyor; spacing'i ONDAN ONCE
        // yazmak gerekiyor (bkz. PaintingChoiceSheetBuilder.BuildOptions).
        GridCellFitter fitter = grid.gameObject.AddComponent<GridCellFitter>();
        UiWiring.SetInt(fitter, "columns", 2);
        UiWiring.SetFloat(fitter, "extraHeight", GalleryDetailPrefabBuilder.WorkTileExtraHeight);

        UiFactory.FitVertical(grid.gameObject);

        return grid;
    }

    // Sol ustte altin, buyuk harfli baslik + sag yaslı sonuc etiketi --
    // GalleryHabitCard'in tur pili ile ayni "letter-spacing:.08em" karari
    // (teslim dosyasi, bkz. Galeri.dc.html plan/eserler baslik satiri).
    private static RectTransform SectionHeader(RectTransform parent, UiTheme theme, string captionKey, out TMP_Text rightLabel)
    {
        RectTransform section = UiFactory.Rect(captionKey, parent);
        UiFactory.Column(section, 10f);
        UiFactory.FitVertical(section.gameObject);

        RectTransform header = UiFactory.Rect("Header", section);
        UiFactory.Row(header, 8f);
        UiFactory.Size(header.gameObject, -1f, 16f);

        TMP_Text left = UiFactory.LocalizedText("Caption", header, captionKey, theme.semiBold, 12f,
            theme.gold, TextAlignmentOptions.Left);
        left.characterSpacing = 8f;
        LayoutElement leftElement = left.gameObject.AddComponent<LayoutElement>();
        leftElement.flexibleWidth = 1f;

        rightLabel = UiFactory.Text("Value", header, string.Empty, theme.regular, 12f,
            theme.textMuted, TextAlignmentOptions.Right);

        return section;
    }
}
