using UnityEngine;

// RectTransform'u cihazin guvenli alanina oturtur (centik, kose yuvarlamasi,
// alttaki home cubugu) ve icerigi 366 birimlik kolonda ortalar.
//
// Ekran boyutu ya da guvenli alan degismedikce hicbir sey yapmiyor: her kare
// iki karsilastirma, sifir tahsis. Cihaz dondurulunce ya da Editor'de Game view
// boyutu degisince kendiliginden tazeleniyor.
//
// Genislik kelepcesi neden burada: UI Root'un rect'inin tek yazari bu bilesen.
// Ayri bir fitter'a koymak sira sorunu dogururdu -- asagidaki
// "offsetMin = offsetMax = zero" satiri kelepceyi her gecirgende silerdi.
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class SafeAreaFitter : MonoBehaviour
{
    [Tooltip("Icerik kolonunun en fazla genisligi (tasarim pikseli). Daha genis "
        + "bir kanvasta icerik ortalanir, yanlar bos kalir.")]
    [SerializeField] private float maxWidth = LayoutMetrics.DesignWidth;

    private RectTransform rect;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;

    private void Awake()
    {
        rect = (RectTransform)transform;
    }

    private void OnEnable()
    {
        Apply();
    }

    private void Update()
    {
        if (Screen.safeArea != lastSafeArea
            || Screen.width != lastScreenSize.x
            || Screen.height != lastScreenSize.y)
        {
            Apply();
        }
    }

    private void Apply()
    {
        lastSafeArea = Screen.safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        if (lastScreenSize.x <= 0 || lastScreenSize.y <= 0)
        {
            return;
        }

        // Anchor'lari kesir olarak yaziyoruz: Canvas Scaler'in olcegi ne olursa
        // olsun dogru yere oturuyor ve piksel cevirisi gerekmiyor.
        Vector2 min = lastSafeArea.position;
        Vector2 max = min + lastSafeArea.size;

        min.x /= lastScreenSize.x;
        min.y /= lastScreenSize.y;
        max.x /= lastScreenSize.x;
        max.y /= lastScreenSize.y;

        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        if (maxWidth <= 0f)
        {
            return;
        }

        // Tasarim 366 birim genis. Daha genis bir kanvasta (tablet, kare ya da
        // yatay pencere) icerigi ortalayip yanlari bos birakiyoruz: her yatay
        // teslim sayisi birebir gecerli kaliyor ve hicbir sey yeniden akmak
        // zorunda kalmiyor. Telefonda kanvas zaten 366 birim, tasma 0.
        float canvasWidth = LayoutMetrics.CanvasWidth(lastScreenSize.x, lastScreenSize.y);
        float columnWidth = canvasWidth * (max.x - min.x);
        float excess = columnWidth - maxWidth;

        if (excess > 0f)
        {
            float half = excess * 0.5f;
            rect.offsetMin = new Vector2(half, 0f);
            rect.offsetMax = new Vector2(-half, 0f);
        }
    }
}
