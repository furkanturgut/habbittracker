using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Iki ekrani tek tikla kuran komut.
//
// Yeniden calistirilabilirligin uc kurali:
//   A. Gorunus icin elle duzenlenebilen TEK yuzey Assets/UI/UiTheme.asset.
//      Prefab'lar her calistirmada sifirdan kuruluyor -- temayi degistirip
//      komutu tekrar calistirin, prefab'lara elle dokunmayin.
//   B. Builder yalnizca uc sahne kokunun sahibi: Studio, Panel_Workshop,
//      Panel_Habits (+ UI Root). Main Camera, HabitSystem, EventSystem,
//      Global Light 2D ve SceneLoader'a DOKUNMAZ.
//   C. Referanslar SerializedObject ile ve "farkliysa yaz" bicimde baglanir.
//
// Sahne kirli isaretleniyor ama KAYDEDILMIYOR: kaydetmek kullanicinin karari.
public static class PainterHabitsBuilder
{
    private const string UiRootName = "UI Root";

    [MenuItem("HabitTracker/Build UI Assets")]
    public static void BuildAssets()
    {
        int sprites = UiSpriteGenerator.Build();
        int studio = StudioSpriteImporter.Import();
        int icons = UiIconImporter.Import();
        int flames = FlameSpriteImporter.Import();
        int rotus = RotusSpriteImporter.Import();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Dilimleme dogrulamasi tema YAZILMADAN once: bozuk bir sayfa varsa
        // konsolda once o hata duruyor, sonra temanin o basamagi bos geliyor --
        // ters sirada "alev neden cizilmiyor" sorusunun cevabi log'un dibinde
        // kalirdi (ValidateNav/ValidateSettings ile ayni gerekce).
        int brokenFlames = FlameSpriteImporter.Validate();
        int brokenRotus = RotusSpriteImporter.Validate();

        UiTheme theme = UiThemeBuilder.Build();

        // Tabloyu BURADA uretmek Loc.Init'i de calistirir -- bu komuttan sonra
        // calisan her sey (Faz 3+'taki builder'lar, UiFactory.LocalizedText)
        // Loc.Tr'dan dogru metni alir.
        LocalizationTable localization = LocalizationTableBuilder.Build();

        AssetDatabase.SaveAssets();

        Selection.activeObject = theme;
        EditorGUIUtility.PingObject(theme);

        Debug.Log(
            "PainterHabitsBuilder: varliklar hazir. " + sprites + " UI sprite'i yazildi, " +
            studio + " studyo gorseli kopyalandi, " + icons + " ikon kopyalandi, " +
            flames + " alev sayfasi kopyalandi" +
            (brokenFlames > 0 ? " (" + brokenFlames + " tanesi dilimlenemedi!)" : "") + ", " +
            rotus + " rotus gorseli kopyalandi" +
            (brokenRotus > 0 ? " (" + brokenRotus + " tanesi dilimlenemedi!)" : "") + ", " +
            localization.Entries.Count + " ceviri anahtari yuklendi. Tema: " +
            UiThemeBuilder.AssetPath,
            theme);
    }

    [MenuItem("HabitTracker/Build Painter Habits UI")]
    public static void BuildAll()
    {
        Scene scene = SceneManager.GetActiveScene();

        // Pasif nesneler de taransin: HabitSystem elle kapatilmis olabilir ve
        // "yok" demek yerine bulup kullanmak daha yardimci.
        HabbitManager manager = Object.FindFirstObjectByType<HabbitManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            Debug.LogError(
                "PainterHabitsBuilder: acik sahnede ('" + scene.name + "') HabbitManager yok. " +
                "Bu komut uygulama sahnesinde calisir: Assets/Scenes/SampleScene.unity dosyasini " +
                "acip tekrar deneyin.");
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("PainterHabitsBuilder: sahnede MainCamera etiketli bir kamera yok.");
            return;
        }

        UiSpriteGenerator.Build();
        StudioSpriteImporter.Import();
        UiIconImporter.Import();
        FlameSpriteImporter.Import();
        RotusSpriteImporter.Import();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        FlameSpriteImporter.Validate();
        RotusSpriteImporter.Validate();

        UiTheme theme = UiThemeBuilder.Build();
        LocalizationTableBuilder.Build();
        UiPrefabBuilder.Prefabs prefabs = UiPrefabBuilder.Build(theme);
        GalleryPrefabBuilder.Prefabs galleryPrefabs = GalleryPrefabBuilder.Build(theme);
        AssetDatabase.SaveAssets();

        StringBuilder report = new StringBuilder();

        RemoveExisting(WorkshopSceneBuilder.StudioRootName);
        RemoveExisting(WorkshopSceneBuilder.PanelName);
        RemoveExisting(HabitsPanelBuilder.PanelName);
        RemoveExisting(GalleryPanelBuilder.PanelName);
        RemoveExisting(SettingsPanelBuilder.PanelName);
        RemoveExisting(UiRootName);

        GameObject studio = WorkshopSceneBuilder.BuildStudio(
            theme, mainCamera, out EaselCover easel, out StudioStage stage);
        report.AppendLine("- Studyo kuruldu: " + studio.name);

        // Kamera temizleme rengi = kolonun yanindaki bosluk. Telefonda hicbir
        // yerde gorunmuyor (0-456 studyo sprite'i, altini SceneSkirt ve
        // TileScroll kapatiyor); tablette 366 birimlik kolonun iki yanindaki
        // seritler bu renk oluyor. Kamera ayarlarindan dokundugumuz tek sey bu.
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = theme.background;

        Canvas canvas = EnsureCanvas(scene, report);
        RectTransform uiRoot = BuildUiRoot(canvas);

        ScreenNavigator navigator = uiRoot.gameObject.AddComponent<ScreenNavigator>();

        GameObject workshop = WorkshopSceneBuilder.BuildPanel(
            uiRoot, theme, prefabs, manager, easel, stage, navigator,
            out WorkshopScreen workshopScreen, out Button workshopSettingsButton);

        GameObject habits = HabitsPanelBuilder.Build(
            uiRoot, theme, prefabs, manager, navigator, out Button habitsSettingsButton);

        GameObject gallery = GalleryPanelBuilder.Build(
            uiRoot, theme, prefabs, galleryPrefabs, manager, navigator, out Button gallerySettingsButton);

        UiWiring.SetArray(navigator, "screens", new Object[] { workshop, habits, gallery });
        UiWiring.SetInt(navigator, "painterScreenIndex", 0);
        UiWiring.Set(navigator, "painter", Object.FindFirstObjectByType<Painter>());

        report.AppendLine("- Paneller kuruldu: " + workshop.name + ", " + habits.name + ", " + gallery.name);

        // Ucu de ayni ekrani acar: SettingsPanelBuilder paneli kurup uc
        // SettingsButton'u da ona bagliyor (bkz. o dosyadaki yorum).
        SettingsScreen settingsScreen = SettingsPanelBuilder.Build(uiRoot, theme, prefabs, manager);
        SettingsPanelBuilder.BindButton(workshopSettingsButton, settingsScreen);
        SettingsPanelBuilder.BindButton(habitsSettingsButton, settingsScreen);
        SettingsPanelBuilder.BindButton(gallerySettingsButton, settingsScreen);

        MakeSettingsIcon(workshopSettingsButton, theme);
        MakeSettingsIcon(habitsSettingsButton, theme);
        MakeSettingsIcon(gallerySettingsButton, theme);

        report.AppendLine("- Ayarlar paneli kuruldu, uc ayar tusu baglandi.");

        Validate(mainCamera, manager, report);
        RetireLegacyButton(canvas, report);

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeObject = workshopScreen.gameObject;

        Debug.Log("PainterHabitsBuilder: kurulum tamam.\n" + report + "Sahneyi kaydetmeyi unutmayin.");
    }

    // -----------------------------------------------------------------------
    // Sahne parcalari
    // -----------------------------------------------------------------------

    private static void RemoveExisting(string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }
    }

    private static Canvas EnsureCanvas(Scene scene, StringBuilder report)
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            GameObject go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = LayerMask.NameToLayer("UI");
            canvas = go.GetComponent<Canvas>();
            report.AppendLine("- Canvas yaratildi.");
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Olcek ayarlarini elle yazmiyoruz: CanvasScalerDriver onlari
        // LayoutMetrics'ten okuyor. Ayni kural SafeAreaFitter (icerik kolonu) ve
        // StudioStage (dunya uzayindaki studyo) icin de gecerli; uc tarafin da
        // tek kaynaktan okumasi ikisinin sessizce ayrismasini engelliyor.
        if (canvas.GetComponent<CanvasScaler>() == null)
        {
            canvas.gameObject.AddComponent<CanvasScaler>();
        }

        CanvasScalerDriver driver = canvas.GetComponent<CanvasScalerDriver>();
        if (driver == null)
        {
            driver = canvas.gameObject.AddComponent<CanvasScalerDriver>();
        }

        driver.Apply();

        if (canvas.GetComponent<GraphicRaycaster>() == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        return canvas;
    }

    private static RectTransform BuildUiRoot(Canvas canvas)
    {
        RectTransform root = UiFactory.Fill(UiFactory.Rect(UiRootName, canvas.transform));

        // Guvenli alan + icerik kolonu kelepcesi. Kanvas 366 birimden genis
        // oldugunda (tablet, kare/yatay pencere) chrome ortalanmis bir kolonda
        // kaliyor, yanlar bos.
        SafeAreaFitter fitter = root.gameObject.AddComponent<SafeAreaFitter>();
        UiWiring.SetFloat(fitter, "maxWidth", LayoutMetrics.DesignWidth);

        return root;
    }

    // Atolye'nin sol ustundeki dugme eskiden "liste" (menu) anlamina
    // geliyordu. REZERVE: Faz 4'te butun kose rozetleri ayar tusuna
    // (MakeSettingsIcon) donustugu icin artik cagrilmiyor, ama rezerve public
    // API silinmez kuraliyla (bkz. MakeGalleryIcon) tutuluyor. Dort kose
    // rozetini ScreenNavButton'a baglayan eski BindNav yardimcisi de Faz 2'de
    // kalkti (bkz. BottomNavBuilder) ama o yalnizca ic yardimciydi, rezerve
    // tutulmadi.
    private static void MakeMenuIcon(Button button, UiTheme theme)
    {
        TMPro.TMP_Text label = button.GetComponentInChildren<TMPro.TMP_Text>();
        if (label != null)
        {
            label.gameObject.SetActive(false);
        }

        RectTransform parent = (RectTransform)button.transform;

        for (int i = 0; i < 3; i++)
        {
            Image bar = UiFactory.Image("Bar " + i, parent, theme.Rounded(5), theme.textPrimary);
            RectTransform rect = bar.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(16f, 2f);
            rect.anchoredPosition = new Vector2(0f, 5f - i * 5f);
        }
    }

    // Galeri'ye giris dugmesinin glifi: kucuk bir tablo cercevesi, TMP karakteri
    // DEGIL -- MakeMenuIcon'daki ayni gerekce (eksik glyph riski).
    private static void MakeGalleryIcon(Button button, UiTheme theme)
    {
        TMPro.TMP_Text label = button.GetComponentInChildren<TMPro.TMP_Text>();
        if (label != null)
        {
            label.gameObject.SetActive(false);
        }

        RectTransform parent = (RectTransform)button.transform;

        Image frame = UiFactory.Image("Frame", parent, theme.Rounded(5), theme.textPrimary);
        RectTransform frameRect = frame.rectTransform;
        frameRect.anchorMin = new Vector2(0.5f, 0.5f);
        frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        frameRect.pivot = new Vector2(0.5f, 0.5f);
        frameRect.sizeDelta = new Vector2(18f, 14f);
        frameRect.anchoredPosition = Vector2.zero;

        Image inner = UiFactory.Image("Dot", frame.rectTransform, theme.circle, theme.burgundy);
        RectTransform innerRect = inner.rectTransform;
        innerRect.anchorMin = new Vector2(0f, 0f);
        innerRect.anchorMax = new Vector2(0f, 0f);
        innerRect.pivot = new Vector2(0f, 0f);
        innerRect.sizeDelta = new Vector2(6f, 6f);
        innerRect.anchoredPosition = new Vector2(2f, 2f);
    }

    // Uc panelin kose rozetinin YENI glifi (Faz 4): disli ikonu. RoundIconButton
    // prefab'ina DOKUNMUYORUZ -- MakeMenuIcon/MakeGalleryIcon kaliginin aynisi,
    // TMP glifi yerine 20x20 gorsel (eksik glyph riski yok).
    private static void MakeSettingsIcon(Button button, UiTheme theme)
    {
        TMPro.TMP_Text label = button.GetComponentInChildren<TMPro.TMP_Text>();
        if (label != null)
        {
            label.gameObject.SetActive(false);
        }

        Image icon = UiFactory.Image("Icon", (RectTransform)button.transform, theme.settingsGear, theme.textPrimary, false);
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(20f, 20f);
        iconRect.anchoredPosition = Vector2.zero;
    }

    // Eski demo nav butonu Constant Pixel Size'a gore konumlanmisti; yeni
    // scaler'la yanlis yere duser. SILMIYORUZ, yalnizca kapatiyoruz.
    private static void RetireLegacyButton(Canvas canvas, StringBuilder report)
    {
        Button[] buttons = canvas.GetComponentsInChildren<Button>(true);

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].GetComponentInParent<ScreenNavigator>() != null)
            {
                continue;
            }

            if (buttons[i].gameObject.activeSelf)
            {
                buttons[i].gameObject.SetActive(false);
                report.AppendLine("- Eski demo butonu kapatildi: " + buttons[i].name + " (silinmedi).");
            }
        }
    }

    // -----------------------------------------------------------------------
    // Dogrulama
    // -----------------------------------------------------------------------

    // Uc panel x uc sekme = 9 ScreenNavButton (bkz. BottomNavBuilder --
    // eski dort kose-rozeti baglantisinin yerini aldi). Baglanti bir gun
    // tekrar Editor-ani AddListener'a dusurulurse hata yine sessiz olurdu:
    // ekran hicbir sey goturmez ama ne derleme ne de bu komut sikayet ederdi.
    // Panel_Habits/Panel_Gallery pasif kuruldugu icin tarama Include ile
    // yapiliyor.
    private const int ExpectedNavButtons = 9;

    private static void ValidateNav(StringBuilder report)
    {
        ScreenNavButton[] navButtons = Object.FindObjectsByType<ScreenNavButton>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (navButtons.Length != ExpectedNavButtons)
        {
            report.AppendLine(
                "! Sahnede " + ExpectedNavButtons + " ScreenNavButton bekleniyordu, " +
                navButtons.Length + " bulundu. Ekranlar arasi gecis calismayabilir.");
        }
    }

    // Uc kose rozeti = uc SettingsButton.Ayni sessiz-hata sinifi: baglanti
    // kaybolursa ayar tusu hicbir sey yapmaz ama derleme sikayet etmez.
    private const int ExpectedSettingsButtons = 3;

    private static void ValidateSettings(StringBuilder report)
    {
        SettingsButton[] settingsButtons = Object.FindObjectsByType<SettingsButton>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (settingsButtons.Length != ExpectedSettingsButtons)
        {
            report.AppendLine(
                "! Sahnede " + ExpectedSettingsButtons + " SettingsButton bekleniyordu, " +
                settingsButtons.Length + " bulundu. Ayarlar paneli bazi ekranlardan acilmayabilir.");
        }
    }

    // Iki rotus pili (Galeri Seviye 1 + Seviye 2 basligi) + bir onarim
    // sayfasi -- ayni sessiz-hata sinifi: RightSpacer'in yerine pill
    // oturtulmazsa stok hic gorunmez ama derleme sikayet etmez.
    private const int ExpectedRotusStockPills = 2;
    private const int ExpectedRotusRestoreSheets = 1;

    private static void ValidateRotus(StringBuilder report)
    {
        RotusStockPill[] pills = Object.FindObjectsByType<RotusStockPill>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (pills.Length != ExpectedRotusStockPills)
        {
            report.AppendLine(
                "! Sahnede " + ExpectedRotusStockPills + " RotusStockPill bekleniyordu, " +
                pills.Length + " bulundu. Galeri basliginda rotus stogu gorunmeyebilir.");
        }

        RotusRestoreSheet[] sheets = Object.FindObjectsByType<RotusRestoreSheet>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (sheets.Length != ExpectedRotusRestoreSheets)
        {
            report.AppendLine(
                "! Sahnede " + ExpectedRotusRestoreSheets + " RotusRestoreSheet bekleniyordu, " +
                sheets.Length + " bulundu. Yanan parca rozetine dokununca onarim sayfasi acilmayabilir.");
        }
    }

    // Bir arsiv sayfasi + bir "Arsiv (N)" satiri -- ayni sessiz-hata sinifi:
    // baglanti kaybolursa arsivleme hicbir sey yapmaz ama derleme sikayet
    // etmez.
    private const int ExpectedArchiveSheets = 1;
    private const int ExpectedArchiveEntryRows = 1;

    private static void ValidateArchive(StringBuilder report)
    {
        ArchiveSheet[] sheets = Object.FindObjectsByType<ArchiveSheet>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (sheets.Length != ExpectedArchiveSheets)
        {
            report.AppendLine(
                "! Sahnede " + ExpectedArchiveSheets + " ArchiveSheet bekleniyordu, " +
                sheets.Length + " bulundu. Arsiv satirina dokununca sayfa acilmayabilir.");
        }

        ArchiveRow[] rows = Object.FindObjectsByType<ArchiveRow>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (rows.Length != ExpectedArchiveEntryRows)
        {
            report.AppendLine(
                "! Sahnede " + ExpectedArchiveEntryRows + " ArchiveRow sablonu bekleniyordu, " +
                rows.Length + " bulundu. Arsiv sayfasindaki satir havuzu bozuk olabilir.");
        }
    }

    private static void Validate(Camera mainCamera, HabbitManager manager, StringBuilder report)
    {
        ValidateNav(report);
        ValidateSettings(report);
        ValidateRotus(report);
        ValidateArchive(report);

        int paintingLayer = LayerMask.NameToLayer("Paintings");

        if (paintingLayer < 0)
        {
            report.AppendLine("! 'Paintings' layer'i tanimli degil (Project Settings > Tags and Layers).");
        }
        else if ((mainCamera.cullingMask & (1 << paintingLayer)) != 0)
        {
            report.AppendLine(
                "! Main Camera'nin culling mask'i 'Paintings' layer'ini iceriyor. " +
                "Cikarilmazsa tablolar ana sahnede de gorunur.");
        }

        PaintingViewPool pool = Object.FindFirstObjectByType<PaintingViewPool>();
        if (pool == null)
        {
            report.AppendLine("! Sahnede PaintingViewPool yok; tile'lar animasyonsuz statik yolda kalir.");
            return;
        }

        SerializedObject serialized = new SerializedObject(pool);
        SerializedProperty size = serialized.FindProperty("poolSize");

        // Uc sutunlu izgarada ayni anda ucten fazla satir gorunebiliyor; binder
        // butcesi 8, havuzun ondan buyuk olmasi gerek.
        if (size != null && size.intValue < 12)
        {
            size.intValue = 12;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            report.AppendLine("- PaintingViewPool.poolSize 12'ye yukseltildi.");
        }

        if (manager.Library == null)
        {
            report.AppendLine(
                "! HabbitManager'da Painting Library atanmamis. " +
                "HabitTracker > Build Painting Library From Sprites komutunu calistirin.");
        }
    }
}
