using UnityEngine;
using UnityEngine.EventSystems;

// 01 Atolye ekraninin DIKEY bestesini ekrana uydurur.
//
// Aritmetigin tamami WorkshopBands'te (saf statik, test edilebilir); burasi
// yalnizca olculeri okuyup sonucu rect'lere yaziyor. Ikisini ayirmanin sebebi:
// hesap Screen.* okumalariyla ic ice durdugu surece test edilemiyordu ve
// tablette izgaraya yarim kutu satiri bile kalmadigi fark edilmiyordu.
//
// Yatayda is LayoutMetrics'e ait: kanvas 366 birimden genis oldugunda
// SafeAreaFitter icerigi ortalanmis bir kolona kelepceliyor, StudioStage da
// studyoyu ayni kolona hizaliyor. Bu yuzden burada yatay bir hesap yok.
//
// Guvenli alan iki farkli uzayi ayiriyor: chrome (baslik, filtre, izgara)
// panelin -- yani guvenli alanin -- icinde kalmali, studyo ise ekranin
// tepesinden tasmali. WorkshopBands.Solve ikisi arasindaki ceviriyi yapiyor.
[DisallowMultipleComponent]
public class WorkshopLayout : UIBehaviour
{
    // Builder bu sabiti acilis degerleri icin okuyor. Tek kaynak WorkshopBands.
    public const float DesignSceneHeight = WorkshopBands.SceneHeight;

    [Tooltip("Studyoya hizali katman: alt fade ve toast burada.")]
    [SerializeField] private RectTransform sceneOverlay;

    [Tooltip("Guvenli alanin tepesine yapisan baslik.")]
    [SerializeField] private RectTransform header;

    [Tooltip("Sahne ile izgara arasindaki duz serit.")]
    [SerializeField] private RectTransform sceneSkirt;

    [SerializeField] private RectTransform filterBar;
    [SerializeField] private RectTransform tileScroll;

    [Tooltip("Dunya uzayindaki studyo koku; kaydirma miktarini buraya veriyoruz.")]
    [SerializeField] private StudioStage stage;

    private RectTransform panel;
    private Canvas canvas;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;
    private float lastScaleFactor;

    protected override void OnEnable()
    {
        base.OnEnable();
        Apply();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        Apply();
    }

    // Guvenli alan panelin yuksekligini degistirmeden de kayabiliyor (cihaz
    // donusu, Editor'de Simulator degisimi). scaleFactor'u de izliyoruz: ilk
    // karede CanvasScaler henuz calismamis olabilir ve o karede hesaplanan
    // safeTop yanlis kalirdi. Birkac karsilastirma, sifir tahsis.
    private void Update()
    {
        if (Screen.safeArea != lastSafeArea
            || Screen.width != lastScreenSize.x
            || Screen.height != lastScreenSize.y
            || (canvas != null && !Mathf.Approximately(canvas.scaleFactor, lastScaleFactor)))
        {
            Apply();
        }
    }

    public void Apply()
    {
        if (panel == null)
        {
            panel = (RectTransform)transform;
        }

        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (canvas == null || canvas.scaleFactor <= 0f)
        {
            return;
        }

        float available = panel.rect.height;
        if (available <= 0f)
        {
            // Olculer henuz gecerli degil; onbellege yazmiyoruz ki sonraki kare
            // tekrar denesin.
            return;
        }

        Rect safeArea = Screen.safeArea;

        // Panelin tepesi ekranin tepesinden ne kadar asagida (tasarim pikseli).
        float safeTop = (Screen.height - safeArea.yMax) / canvas.scaleFactor;

        // BottomNavBar'in AYNI ham girdilerden (Screen.safeArea + scaleFactor)
        // okudugu deger -- ikisi boylece ayrisamiyor (bkz. BottomNavBar.ApplyHeight).
        float safeBottom = safeArea.yMin / canvas.scaleFactor;
        float bottomReserve = BottomNavBands.BarHeight(safeBottom);

        WorkshopBands.Result bands = WorkshopBands.Solve(available, safeTop, bottomReserve);

        if (sceneOverlay != null)
        {
            // Ekranin tepesinden baslayip studyo ile birlikte kayan katman.
            sceneOverlay.anchorMin = new Vector2(0f, 1f);
            sceneOverlay.anchorMax = new Vector2(1f, 1f);
            sceneOverlay.pivot = new Vector2(0.5f, 1f);
            sceneOverlay.offsetMax = new Vector2(0f, safeTop + bands.ShiftUp);
            sceneOverlay.offsetMin = new Vector2(
                0f,
                safeTop + bands.ShiftUp - WorkshopBands.SceneHeight);
        }

        // Mod satiri basligin cocugu: baslik yerine oturunca o da oturuyor,
        // burada ayri bir yazma yok.
        if (header != null)
        {
            UiRect.StretchTop(
                header,
                bands.HeaderTop,
                WorkshopBands.HeaderHeight,
                WorkshopBands.HeaderSideMargin,
                WorkshopBands.HeaderSideMargin);
        }

        if (sceneSkirt != null)
        {
            UiRect.StretchTop(sceneSkirt, bands.SkirtTop, WorkshopBands.SkirtHeight, 0f, 0f);
        }

        if (filterBar != null)
        {
            UiRect.StretchTop(filterBar, bands.FilterTop, WorkshopBands.FilterHeight, 0f, 0f);
        }

        if (tileScroll != null)
        {
            UiRect.Fill(tileScroll, 0f, bands.GridTop, 0f, bottomReserve);
        }

        if (stage != null)
        {
            stage.SetTopShift(bands.ShiftUp);
        }

        lastSafeArea = safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        lastScaleFactor = canvas.scaleFactor;
    }
}
