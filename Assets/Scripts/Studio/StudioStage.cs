using UnityEngine;

// Atolye sahnesinin dunya uzayindaki koku.
//
// Ressamin animasyonlari SpriteRenderer.sprite'i suruyor, yani ressam bir uGUI
// Image OLAMAZ. Bu yuzden arka plan, ressam ve sovale dunya uzayinda kaliyor,
// arayuz chrome'u ustlerinde Overlay Canvas'ta duruyor.
//
// Bu bilesen kokun YEREL UZAYINI tasarim pikseline cevirir: olcek kameranin
// gorunur genisligi / KANVASIN tasarim birimi cinsinden genisligi, konum da
// icerik kolonunun sol UST kosesi. Boylece bir cocuk "top: 202" icin y = -202
// yaziyor ve teslim dosyasindaki butun ofsetler her en-boy oraninda oldugu gibi
// gecerli kaliyor.
//
// Olcek neden 366'dan degil kanvastan turetiliyor: genis bir ekranda (tablet,
// kare ya da yatay pencere) kanvas 366 birimden genis olur ve studyo arayuzle
// AYNI olcegi paylasmak zorundadir, yoksa ikisi ayrisir. Fazla genislik iki
// yana bosluk olarak dagiliyor; studyo o boslugun degil, ortalanmis kolonun
// icinde duruyor. Bkz. LayoutMetrics.
[ExecuteAlways]
[DisallowMultipleComponent]
public class StudioStage : MonoBehaviour
{
    [Tooltip("Bos birakilirsa Camera.main kullanilir.")]
    [SerializeField] private Camera stageCamera;

    [Tooltip("Icerik kolonunun genisligi (tasarim px). Kokun yerel birimi budur; "
        + "kanvas bundan genisse fark iki yana bosluk olarak dagilir.")]
    [SerializeField] private float designWidth = LayoutMetrics.DesignWidth;

    private Camera cachedCamera;
    private Vector2Int lastScreenSize;
    private float lastOrthographicSize;
    private Vector3 lastCameraPosition;

    // Ekran tasarimdan kisaysa studyo KUCULMEZ, yukari kayar: ressam, sovale ve
    // golge birbirine gore yerinde kalir, yalnizca duvarin tepesi kirpilir.
    // Olcegi degistirmek piksel-art'i bozar, yanlarda bosluk birakir ve ayaklari
    // golgeden kaydirirdi. Degeri WorkshopLayout hesapliyor.
    private float topShift;

    // Bir tasarim pikselinin dunya birimi karsiligi. Disaridan yerlestirme
    // yapmak isteyen taraf icin.
    public float DesignPixel { get; private set; } = 1f;

    private void OnEnable()
    {
        Apply();
    }

    // Studyoyu tasarim pikseli kadar yukari kaydirir. Yeniden hizalamayi ancak
    // deger degistiyse tetikliyoruz: cagiran taraf bunu her yerlesim
    // gecirgeninde cagirabilsin.
    public void SetTopShift(float designPixels)
    {
        designPixels = Mathf.Max(0f, designPixels);

        if (Mathf.Approximately(topShift, designPixels))
        {
            return;
        }

        topShift = designPixels;
        Apply();
    }

    // LateUpdate: kamera bu karede hareket ettiyse onun ardindan hizalanalim.
    // Uc karsilastirma disinda hicbir sey yapmiyor.
    private void LateUpdate()
    {
        Camera camera = ResolveCamera();
        if (camera == null)
        {
            return;
        }

        if (Screen.width == lastScreenSize.x
            && Screen.height == lastScreenSize.y
            && Mathf.Approximately(camera.orthographicSize, lastOrthographicSize)
            && camera.transform.position == lastCameraPosition)
        {
            return;
        }

        Apply();
    }

    public void Apply()
    {
        Camera camera = ResolveCamera();
        if (camera == null || !camera.orthographic || designWidth <= 0f)
        {
            return;
        }

        float visibleWidth = 2f * camera.orthographicSize * camera.aspect;

        // Kanvasin tasarim birimi cinsinden genisligi. Telefonda tam 366, yani
        // bu hesap eski davranisin birebir aynisini veriyor.
        float canvasWidth = LayoutMetrics.CanvasWidth(Screen.width, Screen.height);
        if (canvasWidth <= 0f)
        {
            return;
        }

        DesignPixel = visibleWidth / canvasWidth;

        transform.localScale = new Vector3(DesignPixel, DesignPixel, 1f);

        // Tasarimin x = 0'i ekranin degil, ORTALANMIS KOLONUN sol kenari.
        // Telefonda bosluk 0 ve koken yine ekranin sol kosesine oturuyor.
        float sideMargin = Mathf.Max(0f, (canvasWidth - designWidth) * 0.5f);

        // Kokun orijini kolonun sol UST kosesi; cocuklar "top" degerini negatif
        // y olarak yaziyor. topShift orijini ekranin USTUNE tasiyor, yani butun
        // studyo o kadar yukari kayiyor.
        Vector3 cameraPosition = camera.transform.position;
        transform.position = new Vector3(
            cameraPosition.x - visibleWidth * 0.5f + sideMargin * DesignPixel,
            cameraPosition.y + camera.orthographicSize + topShift * DesignPixel,
            0f);

        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        lastOrthographicSize = camera.orthographicSize;
        lastCameraPosition = cameraPosition;
    }

    private Camera ResolveCamera()
    {
        if (stageCamera != null)
        {
            return stageCamera;
        }

        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }

        return cachedCamera;
    }
}
