using UnityEngine;
using UnityEngine.EventSystems;

// Alttan acilan kutunun yuksekligini ekrana sigdirir.
//
// Form kutusu tasarimda 700 tasarim pikseli; Canvas Scaler genislige uydugu icin
// canvas'in dikey birim yuksekligi cihazdan cihaza degisiyor (16:9 bir telefonda
// ~651, guvenli alan dusulunce daha da az). Sabit 700 yazildiginda kutunun UST
// BARI -- "Vazgec" ve baslik -- ekranin disinda kaliyordu.
//
// Ebeveynin yuksekliginden topMargin dusuluyor, sonuc tasarim yuksekligini asla
// asmiyor: uzun ekranlarda tasarim neyse o, kisa ekranlarda kirpilmis hali.
// Alanlar zaten ScrollView icinde, yani kisalan kutu icerik kaybettirmiyor.
//
// SADECE sizeDelta.y yaziyoruz, anchoredPosition'a HIC dokunmuyoruz --
// anchoredPosition.y'nin sahibi HabitSheet'in kaydirma animasyonu (Slide/
// CloseSlide). UiRect.StretchBottom kullanmiyoruz cunku o offsetMin/offsetMax
// yazar ve Unity'nin RectTransform setter'lari o ikisini ayri ayri isler:
// offsetMin yazilirken OnRectTransformDimensionsChange bildirimi sizeDelta
// guncellenir guncellenmez, anchoredPosition'daki telafi satiri calismadan
// ATESLENIYOR. Panelin pivot.y'si 0 oldugu icin (StretchBottom'un kendi
// pivot atamasi) ic cagri AYNI duzeltmeyi bulup sizeDelta'yi tekrar degistirir
// -- ozyineleme hicbir zaman yakinsamaz. Bu tam olarak Faz 2 (HabitSheet
// kapanis animasyonu) panel'i ekran disina (anchoredPosition.y != 0) park
// edip SetActive(false) yapmaya basladiginda ortaya cikan StackOverflowException
// idi: bir sonraki Open() -> SetActive(true) -> OnEnable -> Apply() artik
// anchoredPosition.y == 0 degilken calisiyordu. Yalnizca sizeDelta.y yazmak
// bu ikinci alani hic etkilemedigi icin bildirim zinciri ilk adimda biter.
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class MaxHeightFitter : UIBehaviour
{
    [Tooltip("Tasarimdaki yukseklik; asla bunun uzerine cikilmiyor.")]
    [SerializeField] private float designHeight = 700f;

    [Tooltip("Ebeveynin tepesinde birakilacak en az bosluk.")]
    [SerializeField] private float topMargin = 40f;

    private RectTransform rect;

    // Ayni cagri yiginina geri donen OnRectTransformDimensionsChange
    // bildirimini yutar. SolveHeight idempotent oldugu icin normalde hic
    // tetiklenmiyor; yine de ilk adimda kesmek icin emniyet kemeri olarak
    // kaliyor (FullBleedFitter'daki "applying" kaliginin ayni uygulamasi).
    private bool applying;

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

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        // OnValidate icinden transform yazmak "SendMessage cannot be called
        // during Awake, CheckConsistency, or OnValidate" uyarisi uretir
        // (sahne Internal_RestoreLastOpenedScenes ile acilirken gorulur).
        // Bir kare erteleyip yok-edilmis bileseni (domain reload, sahne
        // kapanisi) kontrol ederek guvenli hale getiriyoruz.
        UnityEditor.EditorApplication.delayCall += ApplyIfAlive;
    }

    private void ApplyIfAlive()
    {
        UnityEditor.EditorApplication.delayCall -= ApplyIfAlive;
        if (this == null)
        {
            return;
        }

        Apply();
    }
#endif

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
        if (parent == null)
        {
            return;
        }

        float target = SolveHeight(parent.rect.height, topMargin, designHeight);
        if (target < 0f)
        {
            return;
        }

        float delta = target - rect.rect.height;
        if (Mathf.Abs(delta) < 0.01f)
        {
            return;
        }

        applying = true;
        try
        {
            rect.sizeDelta += new Vector2(0f, delta);
        }
        finally
        {
            applying = false;
        }
    }

    // Saf statik olcum: WorkshopBands/LayoutMetrics kalibi, EditMode'da test
    // edilebilir. Ebeveyn henuz olculmemisse (ilk kare, kapali panel) yapacak
    // is yok demek icin -1 doner.
    public static float SolveHeight(float parentHeight, float topMargin, float designHeight)
    {
        float available = parentHeight - topMargin;
        if (available <= 0f)
        {
            return -1f;
        }

        return Mathf.Min(designHeight, available);
    }
}
