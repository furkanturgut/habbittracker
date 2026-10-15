using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 01 Atolye ekranini kurar: dunya uzayindaki studyo koku + Overlay Canvas paneli.
public static class WorkshopSceneBuilder
{
    public const string StudioRootName = "Studio";
    public const string PanelName = "Panel_Workshop";

    private const string PaintersScenePath = "Assets/Scenes/PaintersScene.unity";

    // Teslim dosyasindaki olculer (tasarim pikseli, orijin sol ust).
    //
    // DIKKAT: bunlar yalnizca ACILIS degerleri. Dikey bestenin son sozu calisma
    // anindaki WorkshopLayout'ta (aritmetigi WorkshopBands'te): ekran tasarimdan
    // kisaysa sahne seridi kisalir, filtre satiri ve izgara onunla birlikte
    // yukari gelir. Buradaki sayilari degistirirken WorkshopBands'teki esleriyle
    // birlikte degistirin.
    private const float SceneHeight = WorkshopBands.SceneHeight;
    private const float FilterTop = 394f;
    private const float FilterHeight = 92f;
    private const float GridTop = FilterTop + FilterHeight;

    // Ressamin sol-ust kosesi (tasarim px). Teslim dosyasi left=36 diyor; orada
    // ressam kolonun sol kenarina yapisik kaliyor ve uzattigi firca bos duvari
    // gosteriyordu. 95'te sovalenin yanina gecip tabloyu boyuyormus gibi
    // duruyor. Golge de ayni kadar kaydi (govde ile ayni merkezde kalmali).
    //
    // SINIR: ressam govdesi 95 + 25*5 = 220'de bitiyor, tablo 231'de basliyor.
    // Daha saga almayin -- tablonun uzerindeki EaselCover izgarasi ilerlemeyi
    // gosteriyor, ressam oraya tasarsa acilan parcalar gorunmez olur.
    private const float PainterLeft = 95f;
    private const float PainterTop = 202f;

    // Sprite'larin IMPORT pivotu. StudioPixelSprite gercek degeri sprite'tan
    // okuyor; bunlar yalnizca sprite cozulemezse devreye giren yedekler, ama
    // dogru yazilmalari sart -- ikisini birden (0,0) yazmak ressami yarim kare
    // sola ve asagi kaydiriyordu.
    //
    // studio_bg / easel_painting: StudioSpriteImporter alignment 6 (BottomLeft).
    // studio_shadow: UiSpriteGenerator alignment 0 (Center).
    // Ressam sheet'leri: .meta'da alignment 0 (Center) -- alt sprite'lardaki
    // "pivot: {x: 0, y: 0}" alani Unity'de yalnizca alignment 9 (Custom) iken
    // okunur, bu yuzden gercek pivot merkez.
    private static readonly Vector2 BottomLeftPivot = Vector2.zero;
    private static readonly Vector2 CenterPivot = new Vector2(0.5f, 0.5f);

    // -----------------------------------------------------------------------
    // Dunya: studyo
    // -----------------------------------------------------------------------

    public static GameObject BuildStudio(
        UiTheme theme,
        Camera mainCamera,
        out EaselCover easel,
        out StudioStage stage)
    {
        GameObject root = new GameObject(StudioRootName);
        stage = root.AddComponent<StudioStage>();
        UiWiring.Set(stage, "stageCamera", mainCamera);
        UiWiring.SetFloat(stage, "designWidth", 366f);

        // Teslim dosyasindaki dort yerlesim; ressam ve golgesi PainterLeft ile
        // saga alindi (bkz. yukarisi). Ressam karesi 33x34 (36 degil):
        // dogru deger .meta'dan geliyor ve ayaklarin golgeye oturmasini saglayan
        // sayi bu.
        PlaceSprite(root.transform, "Background", theme.studioBackground, 0,
            new Vector2(-1f, 0f), new Vector2(92f, 114f), 4f, 1f, BottomLeftPivot);

        PlaceSprite(root.transform, "Shadow", theme.shadowEllipse, 5,
            new Vector2(PainterLeft - 6f, 370f), new Vector2(175f, 18f), 1f, 100f, CenterPivot)
            .color = new Color32(0x1E, 0x0C, 0x04, 0x80);

        SpriteRenderer easelRenderer = PlaceSprite(root.transform, "Easel", theme.easelPainting, 10,
            new Vector2(231f, 200f), new Vector2(24f, 26f), 4f, 1f, BottomLeftPivot);
        easel = BuildEaselCover(easelRenderer, theme);

        ImportPainter(root.transform, mainCamera);

        // Layer 8 (Paintings) ana kameranin culling mask'inin DISINDA; oraya dusen
        // bir nesne ana sahnede gorunmez, ustelik dokuz tablo kamerasinin hepsinde
        // gorunurdu. Studyonun tamamini layer 0'a sabitliyoruz.
        SetLayerRecursively(root, 0);
        return root;
    }

    private static SpriteRenderer PlaceSprite(
        Transform parent,
        string name,
        Sprite sprite,
        int sortingOrder,
        Vector2 topLeft,
        Vector2 sourceSize,
        float zoom,
        float sourcePixelsPerUnit,
        Vector2 pivot)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;

        StudioPixelSprite placement = go.AddComponent<StudioPixelSprite>();
        UiWiring.SetVector2(placement, "topLeft", topLeft);
        UiWiring.SetVector2(placement, "sourceSize", sourceSize);
        UiWiring.SetFloat(placement, "zoom", zoom);
        UiWiring.SetFloat(placement, "sourcePixelsPerUnit", sourcePixelsPerUnit);
        UiWiring.SetVector2(placement, "pivot", pivot);
        placement.Apply();

        return renderer;
    }

    // Sovalenin 6x6 ortusu. Hucreler burada uretiliyor (calisma aninda degil):
    // sahnede gorunur kaliyorlar ve layer'lari garanti ediliyor.
    private static EaselCover BuildEaselCover(SpriteRenderer easelRenderer, UiTheme theme)
    {
        GameObject coverRoot = new GameObject("Cover");
        coverRoot.transform.SetParent(easelRenderer.transform, false);

        // Sovale sprite'i 24x26, pivot sol alt: yerel kutusu x[0,24] y[0,26].
        const float width = 24f;
        const float height = 26f;
        float cellWidth = width / EaselCover.Columns;
        float cellHeight = height / EaselCover.Rows;

        // Hucre sprite'inin kendi dunya boyutundan olcek turetiyoruz; sprite'in
        // pixelsPerUnit degeri degisse de hucreler dogru boyutta kaliyor.
        Vector2 unit = theme.white != null ? (Vector2)theme.white.bounds.size : Vector2.one;
        SpriteRenderer[] cells = new SpriteRenderer[EaselCover.CellCount];

        for (int row = 0; row < EaselCover.Rows; row++)
        {
            for (int column = 0; column < EaselCover.Columns; column++)
            {
                int index = row * EaselCover.Columns + column;

                GameObject cell = new GameObject("Cell " + row + "_" + column);
                cell.transform.SetParent(coverRoot.transform, false);
                // Sol ustten saga, satir satir -- EaselCover'in bekledigi sira.
                cell.transform.localPosition = new Vector3(
                    (column + 0.5f) * cellWidth,
                    height - (row + 0.5f) * cellHeight,
                    0f);
                cell.transform.localScale = new Vector3(cellWidth / unit.x, cellHeight / unit.y, 1f);

                SpriteRenderer renderer = cell.AddComponent<SpriteRenderer>();
                renderer.sprite = theme.white;
                renderer.color = theme.easelCover;
                renderer.sortingOrder = easelRenderer.sortingOrder + 1;

                cells[index] = renderer;
            }
        }

        EaselCover cover = easelRenderer.gameObject.AddComponent<EaselCover>();
        UiWiring.SetArray(cover, "cells", cells);
        UiWiring.SetColor(cover, "coverColor", theme.easelCover);
        return cover;
    }

    // Ressami PaintersScene'den KOPYALIYORUZ, sifirdan kurmuyoruz.
    //
    // Painter'in alti mood controller'i [FormerlySerializedAs] ile eski adlara
    // bagli ve collider'i elle ayarlanmis. Instantiate serialize edilmis
    // referanslari birebir tasidigi icin o soru hic dogmuyor.
    private static void ImportPainter(Transform studioRoot, Camera mainCamera)
    {
        Scene active = SceneManager.GetActiveScene();

        bool needsPainter = Object.FindFirstObjectByType<Painter>() == null;
        bool needsManager = Object.FindFirstObjectByType<PainterManager>() == null;

        if (!needsPainter && !needsManager)
        {
            ReparentExistingPainter(studioRoot, mainCamera);
            return;
        }

        Scene painterScene = EditorSceneManager.OpenScene(PaintersScenePath, OpenSceneMode.Additive);
        if (!painterScene.IsValid())
        {
            Debug.LogError(
                "WorkshopSceneBuilder: " + PaintersScenePath + " acilamadi. Ressami elle ekleyip " +
                StudioRootName + " altina koyun.");
            return;
        }

        GameObject[] roots = painterScene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            bool isPainter = roots[i].GetComponent<Painter>() != null;
            bool isManager = roots[i].GetComponent<PainterManager>() != null;

            if ((!isPainter || !needsPainter) && (!isManager || !needsManager))
            {
                continue;
            }

            GameObject copy = Object.Instantiate(roots[i]);
            copy.name = roots[i].name;
            SceneManager.MoveGameObjectToScene(copy, active);

            if (isPainter)
            {
                copy.transform.SetParent(studioRoot, false);
                ApplyPainterPlacement(copy, mainCamera);
            }
        }

        // PaintersScene'i KAYDETMEDEN kapatiyoruz: oradaki kurulum dokunulmadan
        // kalmali, iki ekran arasinda gidip gelmek hala calisiyor.
        EditorSceneManager.CloseScene(painterScene, true);
    }

    private static void ReparentExistingPainter(Transform studioRoot, Camera mainCamera)
    {
        Painter painter = Object.FindFirstObjectByType<Painter>();
        if (painter == null)
        {
            return;
        }

        painter.transform.SetParent(studioRoot, false);
        ApplyPainterPlacement(painter.gameObject, mainCamera);
    }

    private static void ApplyPainterPlacement(GameObject painter, Camera mainCamera)
    {
        StudioPixelSprite placement = painter.GetComponent<StudioPixelSprite>();
        if (placement == null)
        {
            placement = painter.AddComponent<StudioPixelSprite>();
        }

        UiWiring.SetVector2(placement, "topLeft", new Vector2(PainterLeft, PainterTop));
        // Idle karesi 33x34 (teslim dosyasi 36 diyor ama .meta 34 gosteriyor;
        // 34 ile ayaklar golgenin uzerine oturuyor).
        UiWiring.SetVector2(placement, "sourceSize", new Vector2(33f, 34f));
        UiWiring.SetFloat(placement, "zoom", 5f);
        UiWiring.SetFloat(placement, "sourcePixelsPerUnit", 32f);
        UiWiring.SetVector2(placement, "pivot", CenterPivot);
        placement.Apply();

        SpriteRenderer renderer = painter.GetComponentInChildren<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 20;
        }

        // Sahnede artik dokuz PaintingView kamerasi var; Camera.main cozumlemesini
        // sansa birakmiyoruz.
        Painter component = painter.GetComponent<Painter>();
        if (component != null)
        {
            UiWiring.Set(component, "inputCamera", mainCamera);
        }
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;

        for (int i = 0; i < go.transform.childCount; i++)
        {
            SetLayerRecursively(go.transform.GetChild(i).gameObject, layer);
        }
    }

    // -----------------------------------------------------------------------
    // Arayuz paneli
    // -----------------------------------------------------------------------

    public static GameObject BuildPanel(
        RectTransform canvas,
        UiTheme theme,
        UiPrefabBuilder.Prefabs prefabs,
        HabbitManager manager,
        EaselCover easel,
        StudioStage stage,
        ScreenNavigator navigator,
        out WorkshopScreen screen,
        out Button backButton)
    {
        // Panelin kendisinde Graphic YOK: dunyadaki studyo gorunmeli ve
        // EventSystem.IsPointerOverGameObject ressamin dokunmasini yutmamali.
        RectTransform panel = UiFactory.Fill(UiFactory.Rect(PanelName, canvas));

        // SceneOverlay guvenli alana DEGIL studyoya hizali: ekranin tepesinden
        // baslar ve studyo yukari kaydiginda onunla birlikte kayar. Boylece
        // alt fade zeminden ayrilmiyor. Son konumu WorkshopLayout yaziyor.
        RectTransform overlay = UiFactory.StretchTop(
            UiFactory.Rect("SceneOverlay", panel), 0f, SceneHeight, 0f, 0f);

        BuildBottomFade(overlay, theme);
        ToastView toast = BuildToast(overlay, theme);

        // Baslik guvenli alanda kaliyor, bu yuzden overlay'in degil panelin
        // cocugu -- centikli bir telefonda studyo ile birlikte kaymamali.
        // Mod gostergesi basligin altinda, kendi bandinda (bkz. BuildHeader).
        RectTransform header = BuildHeader(
            panel, theme, prefabs, out TMP_Text counter, out TMP_Text painterName, out backButton);

        // Filtre satiri sahneyi -58 px ortuyor; arada kalan serit duz zemin.
        Image skirt = UiFactory.Image("SceneSkirt", panel, theme.white, theme.background, false);
        UiFactory.StretchTop(skirt.rectTransform, SceneHeight, GridTop - SceneHeight, 0f, 0f);

        HabitFilterBar filterBar = BuildFilterBar(panel, prefabs, manager);
        HabitTileGrid tileGrid = BuildTileGrid(panel, theme, prefabs, manager, out RectTransform tileScroll);

        // TileScroll'dan SONRA, PaintingChoiceSheet'ten ONCE: alt sayfa acilinca
        // cubugu ORTMELI (bkz. NAVIGATION_SETTINGS_PLAN.md Faz 2, "panel basina
        // bir ornek" gerekcesi).
        BottomNavBuilder.Build(panel, theme, navigator, 0);

        // En ustte cizilsin diye TileGrid'den sonra, son cocuk olarak eklendi.
        PaintingChoiceSheet choiceSheet = PaintingChoiceSheetBuilder.Build(panel, theme, prefabs, manager, navigator);

        // Secim kutusundan da SONRA: kutlama tam ekran ve panelin icindeki
        // her seyi ortmeli (bkz. StreakCelebrationBuilder).
        StreakCelebration celebration = StreakCelebrationBuilder.Build(panel, theme, navigator);

        // Dikey besteyi ekrana uyduran bilesen. Yukaridaki sabitler acilis
        // degeri; gercek yerlesimi bu yaziyor (OnEnable + her olcu degisiminde).
        WorkshopLayout layout = panel.gameObject.AddComponent<WorkshopLayout>();
        UiWiring.Set(layout, "sceneOverlay", overlay);
        UiWiring.Set(layout, "header", header);
        UiWiring.Set(layout, "sceneSkirt", skirt.rectTransform);
        UiWiring.Set(layout, "filterBar", (RectTransform)filterBar.transform);
        UiWiring.Set(layout, "tileScroll", tileScroll);
        UiWiring.Set(layout, "stage", stage);

        screen = panel.gameObject.AddComponent<WorkshopScreen>();
        UiWiring.Set(screen, "manager", manager);
        UiWiring.Set(screen, "counterLabel", counter);
        UiWiring.Set(screen, "painterNameLabel", painterName);
        UiWiring.Set(screen, "easel", easel);
        UiWiring.Set(screen, "toast", toast);
        UiWiring.Set(screen, "filterBar", filterBar);
        UiWiring.Set(screen, "tileGrid", tileGrid);
        UiWiring.Set(screen, "choiceSheet", choiceSheet);
        UiWiring.Set(screen, "celebration", celebration);

        return panel.gameObject;
    }

    private static void BuildBottomFade(RectTransform parent, UiTheme theme)
    {
        Image fade = UiFactory.Image("BottomFade", parent, theme.white, Color.white, false);
        UiFactory.StretchTop(fade.rectTransform, SceneHeight - 70f, 70f, 0f, 0f);

        Color transparent = theme.background;
        transparent.a = 0f;
        UiFactory.Gradient(fade.gameObject, transparent, theme.background);
    }

    // Baslik artik IKI satir (Faz 3): ustte 02/03'teki TitleBlock deseninin
    // aynisi ("Atolye" alt baslik + ressam adi) iki kose rozetinin arasinda,
    // altinda kendi bandinda mod satiri.
    private static RectTransform BuildHeader(
        RectTransform parent,
        UiTheme theme,
        UiPrefabBuilder.Prefabs prefabs,
        out TMP_Text counter,
        out TMP_Text painterName,
        out Button backButton)
    {
        float badge = WorkshopBands.HeaderBadgeSize;

        RectTransform header = UiFactory.StretchTop(
            UiFactory.Rect("Header", parent),
            WorkshopBands.HeaderTop,
            WorkshopBands.HeaderHeight,
            WorkshopBands.HeaderSideMargin,
            WorkshopBands.HeaderSideMargin);

        GameObject back = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.RoundIconButton, header);
        back.name = "BackButton";
        UiFactory.TopLeft((RectTransform)back.transform, 0f, 0f, badge, badge);
        backButton = back.GetComponentInChildren<Button>();

        GameObject counterButton = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.RoundIconButton, header);
        counterButton.name = "Counter";
        RectTransform counterRect = (RectTransform)counterButton.transform;
        counterRect.anchorMin = new Vector2(1f, 1f);
        counterRect.anchorMax = new Vector2(1f, 1f);
        counterRect.pivot = new Vector2(1f, 1f);
        counterRect.anchoredPosition = Vector2.zero;
        counterRect.sizeDelta = new Vector2(badge, badge);

        counter = counterButton.GetComponentInChildren<TMP_Text>();
        counter.text = "0/0";
        counter.fontSize = 13f;

        // Sayac bir buton degil, yalnizca rozet.
        Button counterAsButton = counterButton.GetComponentInChildren<Button>();
        if (counterAsButton != null)
        {
            counterAsButton.interactable = false;
        }

        // Yazi blogu: 02/03'teki TitleBlock'un AYNISI, kose rozetleriyle
        // AYNI ust bantta (yukseklik WorkshopBands.TitleBlockHeight).
        RectTransform titleBlock = UiFactory.Rect("TitleBlock", header);
        titleBlock.anchorMin = new Vector2(0.5f, 1f);
        titleBlock.anchorMax = new Vector2(0.5f, 1f);
        titleBlock.pivot = new Vector2(0.5f, 1f);
        titleBlock.anchoredPosition = Vector2.zero;
        titleBlock.sizeDelta = new Vector2(240f, WorkshopBands.TitleBlockHeight);
        UiFactory.Column(titleBlock, 2f, TextAnchor.UpperCenter);

        TMP_Text subtitle = UiFactory.LocalizedText("Subtitle", titleBlock, "nav.tab.workshop", theme.medium, 14f,
            theme.textMuted, TextAlignmentOptions.Center);
        UiFactory.Size(subtitle.gameObject, -1f, 16f);

        painterName = UiFactory.Text("PainterName", titleBlock, AppSettingsData.DefaultPainterName,
            theme.bold, 28f, theme.textPrimary, TextAlignmentOptions.Center);
        UiFactory.Size(painterName.gameObject, -1f, 32f);

        BuildMoodRow(header, theme, prefabs);

        return header;
    }

    // Basligin ALTINDAKI mod satiri: moda bir daire, anlik mod yanik.
    //
    // Kendi bandinda (StretchBottom: basligin tam dibine oturur,
    // yuksekligi MoodCircleSize) -- eskiden kose rozetlerinin ARASINDAYDI,
    // artik basligin TAMAMINI (kose rozetleri ustteki satirda) kullaniyor.
    //
    // Daireleri burada uretmiyoruz -- sayilari MoodScale.Count'tan geliyor ve
    // onlari MoodRow calisma aninda kuruyor. Builder yalnizca serit ile
    // prefab'i bagliyor.
    private static RectTransform BuildMoodRow(
        RectTransform parent,
        UiTheme theme,
        UiPrefabBuilder.Prefabs prefabs)
    {
        RectTransform row = UiFactory.StretchBottom(
            UiFactory.Rect("MoodRow", parent), 0f, WorkshopBands.MoodCircleSize, 0f, 0f);

        UiFactory.Row(row, WorkshopBands.MoodCircleGap, TextAnchor.MiddleCenter);

        MoodRow component = row.gameObject.AddComponent<MoodRow>();
        UiWiring.Set(component, "circlePrefab", prefabs.MoodCircle.GetComponent<MoodCircle>());
        UiWiring.Set(component, "root", row);
        UiWiring.Set(component, "theme", theme);

        return row;
    }

    private static ToastView BuildToast(RectTransform parent, UiTheme theme)
    {
        RectTransform root = UiFactory.TopCenter(UiFactory.Rect("Toast", parent), 168f, 300f, 34f);
        CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        RectTransform panel = UiFactory.Rect("Panel", root);
        panel.anchorMin = new Vector2(0.5f, 1f);
        panel.anchorMax = new Vector2(0.5f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.anchoredPosition = Vector2.zero;

        Image background = panel.gameObject.AddComponent<Image>();
        background.sprite = theme.Rounded(14);
        background.type = Image.Type.Sliced;
        background.color = theme.gold;
        background.raycastTarget = false;

        HorizontalLayoutGroup row = UiFactory.Row(panel, 0f, TextAnchor.MiddleCenter);
        row.padding = new RectOffset(16, 16, 9, 9);
        ContentSizeFitter fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TMP_Text label = UiFactory.Text("Label", panel, "", theme.semiBold, 13f,
            theme.textOnGold, TextAlignmentOptions.Center);
        UiFactory.Size(label.gameObject, -1f, 16f);

        ToastView toast = root.gameObject.AddComponent<ToastView>();
        UiWiring.Set(toast, "group", group);
        UiWiring.Set(toast, "panel", panel);
        UiWiring.Set(toast, "label", label);

        root.gameObject.SetActive(false);
        return toast;
    }

    private static HabitFilterBar BuildFilterBar(
        RectTransform parent,
        UiPrefabBuilder.Prefabs prefabs,
        HabbitManager manager)
    {
        RectTransform bar = UiFactory.StretchTop(UiFactory.Rect("FilterBar", parent),
            FilterTop, FilterHeight, 0f, 0f);

        UiFactory.Row(bar, 10f, TextAnchor.UpperCenter);

        HabitFilterBar component = bar.gameObject.AddComponent<HabitFilterBar>();
        UiWiring.Set(component, "manager", manager);
        UiWiring.Set(component, "buttonPrefab", prefabs.FilterDiamond.GetComponent<HabitFilterButton>());
        UiWiring.Set(component, "root", bar);

        return component;
    }

    private static HabitTileGrid BuildTileGrid(
        RectTransform parent,
        UiTheme theme,
        UiPrefabBuilder.Prefabs prefabs,
        HabbitManager manager,
        out RectTransform scrollRoot)
    {
        RectTransform scroll = UiFactory.Fill(UiFactory.Rect("TileScroll", parent), 0f, GridTop, 0f, 0f);
        scrollRoot = scroll;

        // TileScroll'un kendi Image'i yalnizca raycast hedefi: bos alana dokunup
        // kaydirabilmek icin. Gorunen zemin asagidaki Backdrop'ta.
        Image hit = scroll.gameObject.AddComponent<Image>();
        hit.sprite = theme.white;
        hit.color = Color.clear;
        hit.raycastTarget = true;

        // Gradyan zemin ayri bir cocuk: FullBleedFitter ile kanvasin tam
        // genisligine yayiliyor. Kaydirma alaninin kendisi 366 birimlik kolonda
        // kaliyor (yoksa GridCellFitter hucreleri genisletirdi), ama zemin
        // tablette kenardan kenara gidiyor ve yan bosluklarla arasinda ton
        // farki kalmiyor.
        Image background = UiFactory.Image("Backdrop", scroll, theme.white, Color.white, false);
        UiFactory.Fill(background.rectTransform);
        UiFactory.Gradient(background.gameObject, theme.background, theme.surface);
        background.gameObject.AddComponent<FullBleedFitter>();

        RectTransform content = UiFactory.ScrollView(scroll, out ScrollRect scrollRect, out RectTransform viewport);

        GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(20, 20, 8, 40);
        grid.spacing = new Vector2(14f, 16f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        UiFactory.FitVertical(content.gameObject);

        GridCellFitter fitter = content.gameObject.AddComponent<GridCellFitter>();
        UiWiring.SetInt(fitter, "columns", 3);
        // Kare gorselin altindaki baslik + seri satiri.
        UiWiring.SetFloat(fitter, "extraHeight", WorkshopBands.TileInfoHeight);

        PaintingViewBinder binder = content.gameObject.AddComponent<PaintingViewBinder>();
        UiWiring.Set(binder, "manager", manager);
        UiWiring.Set(binder, "scrollRect", scrollRect);
        UiWiring.Set(binder, "viewport", viewport);

        HabitTileGrid tileGrid = content.gameObject.AddComponent<HabitTileGrid>();
        UiWiring.Set(tileGrid, "manager", manager);
        UiWiring.Set(tileGrid, "tilePrefab", prefabs.HabitTile.GetComponent<HabitTile>());
        UiWiring.Set(tileGrid, "content", content);
        UiWiring.Set(tileGrid, "binder", binder);

        return tileGrid;
    }
}
