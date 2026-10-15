using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

// Uc ekranin altina sabitlenen sekme cubugu. Panel basina bir ornek (bkz.
// BottomNavBuilder) ama ucu de AYNI global durumu gosterir: hangi ekran
// aktifse o sekme yanar, bu bar'in hangi panelde yasadigina bakilmaksizin.
// Bu yuzden gecis yarim kalip iki panel/iki cubuk ust uste binince ikisi
// AYNI gorunur -- kusur degil (bkz. NAVIGATION_SETTINGS_PLAN.md Faz 2).
//
// tabs[i]'nin targetIndex'i HER ZAMAN i: dizinin sirasi Atolye/Aliskanliklar/
// Galeri, yani ScreenNavigator.screens sirasiyla birebir.
[DisallowMultipleComponent]
public class BottomNavBar : UIBehaviour
{
    [SerializeField] private NavTab[] tabs;
    [SerializeField] private ScreenNavigator navigator;

    [Tooltip("Elmas gecisinin suresi (sn). Yalnizca kozmetik.")]
    [SerializeField] private float transitionSeconds = 0.2f;

    private RectTransform rect;
    private Canvas canvas;

    // Kendi yazdigimiz sonucu geri bildirimle tekrar girdi almayalim diye --
    // FullBleedFitter/MaxHeightFitter'daki ayni "applying" kaligi.
    private bool applying;
    private float lastBarHeight = -1f;

    private bool subscribed;
    private bool shown;

    private float[] fromAlpha;
    private Coroutine transition;

    public event Action HeightChanged;

    public float CurrentHeight => rect != null ? rect.rect.height : 0f;

    protected override void OnEnable()
    {
        base.OnEnable();

        if (rect == null)
        {
            rect = (RectTransform)transform;
        }

        ApplyHeight();
        TrySubscribe();
        Refresh(!shown);
    }

    // Guvenli alan degisebilir (cihaz donusu) diye WorkshopLayout'taki ayni
    // kontrol dongusu; sifir tahsisli karsilastirma.
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;
    private float lastScaleFactor;

    private void Update()
    {
        if (Screen.safeArea != lastSafeArea
            || Screen.width != lastScreenSize.x
            || Screen.height != lastScreenSize.y
            || (canvas != null && !Mathf.Approximately(canvas.scaleFactor, lastScaleFactor)))
        {
            ApplyHeight();
        }
    }

    // OnEnable'da ScreenNavigator henuz Start'ini calistirmamis olabilir
    // (Unity nesneler arasi Awake/Start sirasini garanti etmiyor) --
    // MoodRow'un PainterManager icin yaptiginin ayni ikizi: Start'ta tekrar
    // dene, ustune olaya degil dogrudan okumaya da guven.
    protected override void Start()
    {
        base.Start();
        TrySubscribe();
        Refresh(!shown);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        StopTransition();

        if (subscribed && navigator != null)
        {
            navigator.ScreenChanged -= HandleScreenChanged;
        }

        subscribed = false;
        shown = false;
    }

    private void TrySubscribe()
    {
        if (subscribed || navigator == null)
        {
            return;
        }

        navigator.ScreenChanged += HandleScreenChanged;
        subscribed = true;
    }

    private void HandleScreenChanged(int _)
    {
        Refresh(false);
    }

    private void Refresh(bool instant)
    {
        if (tabs == null || navigator == null)
        {
            return;
        }

        int current = navigator.Current;

        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i] != null)
            {
                tabs[i].SetSelected(i == current, instant);
            }
        }

        shown = true;

        if (instant || !isActiveAndEnabled || transitionSeconds <= 0f)
        {
            StopTransition();
            ApplyAllVisual();
            return;
        }

        // Yeni gecis eskisini KESIYOR: DayChipRow/MoodRow'daki ayni kural --
        // yarim kalmis bir yumusatmanin ustune binmek elmaslari arada bir
        // alfada birakirdi.
        StopTransition();
        transition = StartCoroutine(Animate());
    }

    private void ApplyAllVisual()
    {
        if (tabs == null)
        {
            return;
        }

        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i] != null)
            {
                tabs[i].ApplyVisual(tabs[i].TargetDiamondAlpha);
            }
        }
    }

    private void StopTransition()
    {
        if (transition == null)
        {
            return;
        }

        StopCoroutine(transition);
        transition = null;
    }

    // Tek surucu coroutine: uc sekmenin elmasini birlikte yumusatiyor,
    // sekme basina bir coroutine acmak MoodRow'un kacindigi ayni gereksiz
    // iki/uc kat is olurdu.
    private IEnumerator Animate()
    {
        int count = tabs.Length;

        if (fromAlpha == null || fromAlpha.Length != count)
        {
            fromAlpha = new float[count];
        }

        for (int i = 0; i < count; i++)
        {
            fromAlpha[i] = tabs[i] != null ? tabs[i].CurrentDiamondAlpha : 0f;
        }

        float elapsed = 0f;

        while (elapsed < transitionSeconds)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / transitionSeconds);
            float eased = Easing.OutCubic(t);

            for (int i = 0; i < count; i++)
            {
                if (tabs[i] != null)
                {
                    tabs[i].ApplyVisual(Mathf.Lerp(fromAlpha[i], tabs[i].TargetDiamondAlpha, eased));
                }
            }

            yield return null;
        }

        ApplyAllVisual();
        transition = null;
    }

    // Kendi rect'inin yuksekligini kendi yaziyor ama olcumu kendi
    // rect'inden DEGIL Screen.safeArea + canvas.scaleFactor'dan aliyor --
    // MaxHeightFitter'in SolveHeight'e gecis gerekcesinin aynisi. Yalnizca
    // sizeDelta.y yaziyoruz, UiRect.* CAGIRMIYORUZ (bu da tek owned alan
    // kuralinin aynisi).
    private void ApplyHeight()
    {
        if (applying)
        {
            return;
        }

        if (rect == null)
        {
            rect = (RectTransform)transform;
        }

        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (canvas == null || canvas.scaleFactor <= 0f)
        {
            return;
        }

        Rect safeArea = Screen.safeArea;
        float safeBottom = safeArea.yMin / canvas.scaleFactor;

        float barHeight = BottomNavBands.BarHeight(safeBottom);

        lastSafeArea = safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        lastScaleFactor = canvas.scaleFactor;

        if (Mathf.Approximately(barHeight, lastBarHeight))
        {
            return;
        }

        float delta = barHeight - rect.rect.height;

        applying = true;
        try
        {
            rect.sizeDelta += new Vector2(0f, delta);
        }
        finally
        {
            applying = false;
        }

        lastBarHeight = barHeight;
        HeightChanged?.Invoke();
    }
}
