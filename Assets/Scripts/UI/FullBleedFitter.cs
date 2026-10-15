using UnityEngine;
using UnityEngine.EventSystems;

// Bir tam sayfa zemini, atalarindaki yatay kelepceyi asip kanvasin TAM
// genisligine yayar.
//
// SafeAreaFitter genis ekranlarda UI Root'u 366 birimlik bir kolona kisitliyor
// (bkz. LayoutMetrics). Chrome icin dogru olan bu, ama zeminin de kolonda
// kalmasi tablette kenarda bir ton farki birakiyordu: kolonun icinde gradyan,
// disinda kamera zemin rengi. Bu bilesen rect'in yatay ofsetlerini negatife
// itip zemini kenardan kenara goturuyor.
//
// Telefonda kolon zaten kanvasin tamami: tasma 0, hicbir sey degismiyor.
//
// OLCUM KENDI OFSETLERIMIZI OKUMAZ -- ve bu bir ayrinti degil, bilesenin
// dogru calismasinin sarti. offsetMin/offsetMax yazmak rect'in boyutunu
// degistirir, Unity de bunu ayni cagri yigininda OnRectTransformDimensionsChange
// ile geri bildirir. Olcum kendi ofsetlerine bakiyorsa geri bildirim yeni bir
// sonuc uretir (once tabana don -> 0, sonra tasmayi yaz -> -tasma, tekrar
// tabana don...) ve ozyineleme hicbir zaman yakinsamaz: Editor Play moduna
// girerken kilitlenir. Bu yuzden tasma yalnizca EBEVEYNIN cipalardan gelen
// taban kenarlarindan ve kanvastan hesaplaniyor; ikisi de bizim yazdigimizdan
// etkilenmedigi icin sonuc cagri sayisindan bagimsiz (idempotent) ve ikinci
// yazma ayni degeri tasiyor, yani Unity'nin degisiklik kontrolu zinciri
// kendiliginden kesiyor. Yine de asagidaki applying bayragi ozyinelemeyi ilk
// adimda durduruyor.
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class FullBleedFitter : UIBehaviour
{
    // Varsayilan false: mevcut uc cagiranin (Panel_Habits/Background,
    // TileScroll/Backdrop, GalleryPanelBuilder) davranisi BIREBIR AYNI kalir.
    // Acildiginda zeminin ALT kenari da guvenli alanin disina, ekranin
    // dibine kadar iner -- alt navigasyon cubugunun zemini bunu kullanir
    // (bkz. NAVIGATION_SETTINGS_PLAN.md Faz 2).
    [SerializeField] private bool bleedBottom;

    private RectTransform rect;
    private RectTransform canvasRect;

    // Yazma sirasinda gelen yeniden-olculendirme geri bildirimini yutar.
    private bool applying;

    // GetWorldCorners her cagrida dizi istiyor; alan olarak tutuyoruz.
    private readonly Vector3[] cornerScratch = new Vector3[4];

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

    public void Apply()
    {
        if (applying)
        {
            return;
        }

        if (rect == null)
        {
            rect = (RectTransform)transform;
        }

        RectTransform parent = rect.parent as RectTransform;
        if (parent == null || parent.rect.width <= 0f)
        {
            return;
        }

        if (canvasRect == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            canvasRect = (RectTransform)canvas.rootCanvas.transform;
        }

        if (canvasRect.rect.width <= 0f)
        {
            return;
        }

        // Ebeveynin yatay VE dikey sinirlari, kok kanvasin uzayinda.
        // GetWorldCorners sirasi: 0 sol-alt, 1 sol-ust, 2 sag-ust, 3 sag-alt.
        parent.GetWorldCorners(cornerScratch);
        float parentLeft = canvasRect.InverseTransformPoint(cornerScratch[0]).x;
        float parentRight = canvasRect.InverseTransformPoint(cornerScratch[2]).x;
        float parentBottom = canvasRect.InverseTransformPoint(cornerScratch[0]).y;
        float parentTop = canvasRect.InverseTransformPoint(cornerScratch[1]).y;

        // Ebeveyn pikselinden kanvas pikseline olcek. Ayni kanvasta ic ice
        // olcek yoksa 1; yine de hesaba katiyoruz ki ofsetler (ebeveyn uzayinda
        // olculuyor) dogru birimde ciksin.
        float scale = (parentRight - parentLeft) / parent.rect.width;
        if (scale <= 0f)
        {
            return;
        }

        // Ofsetler sifir olsaydi kenarlarimiz nerede olurdu: cipa kesirlerinin
        // ebeveyn kenarlari arasindaki karsiligi.
        float baseLeft = Mathf.Lerp(parentLeft, parentRight, rect.anchorMin.x);
        float baseRight = Mathf.Lerp(parentLeft, parentRight, rect.anchorMax.x);

        float leftOverflow = Mathf.Max(0f, (baseLeft - canvasRect.rect.xMin) / scale);
        float rightOverflow = Mathf.Max(0f, (canvasRect.rect.xMax - baseRight) / scale);

        Vector2 offsetMin = rect.offsetMin;
        Vector2 offsetMax = rect.offsetMax;

        // Alt tasma da AYNI aritmetikle: ebeveynin alt kenari (cipa
        // kesirlerinden) ile kanvasin alt kenari arasindaki fark. bleedBottom
        // kapaliyken offsetMin.y'ye hic dokunulmuyor -- yatay kural ile ayni
        // idempotentlik: olcum kendi ofsetine bakmiyor, yalnizca ebeveyn ve
        // kanvastan geliyor.
        float targetOffsetMinY = offsetMin.y;
        if (bleedBottom)
        {
            float baseBottom = Mathf.Lerp(parentBottom, parentTop, rect.anchorMin.y);
            float bottomOverflow = Mathf.Max(0f, (baseBottom - canvasRect.rect.yMin) / scale);
            targetOffsetMinY = -bottomOverflow;
        }

        // Tasma yoksa sifira donuyoruz: cihaz tabletten telefona (ya da Game
        // view genis pencereden dar pencereye) gectiginde eski itme kalmasin.
        if (Mathf.Approximately(offsetMin.x, -leftOverflow)
            && Mathf.Approximately(offsetMax.x, rightOverflow)
            && Mathf.Approximately(offsetMin.y, targetOffsetMinY))
        {
            return;
        }

        applying = true;
        try
        {
            rect.offsetMin = new Vector2(-leftOverflow, targetOffsetMinY);
            rect.offsetMax = new Vector2(rightOverflow, offsetMax.y);
        }
        finally
        {
            applying = false;
        }
    }
}
