using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Ressamin tepesindeki mod gostergesi: her moda bir daire, anlik mod yanik.
//
// Daire sayisi MoodScale.Count'tan TURETILIYOR, 6 diye sabit yazilmiyor: mod
// enum'u degisirse satir da kendiliginden degisiyor (MoodPill'in segment
// sayisini MaxMoodPoint'ten turetmesiyle ayni gerekce).
//
// Hangi dairenin hangi moda ait oldugu MoodScale'de (saf, test edilebilir);
// burasi yalnizca gorsel durum. HabbitManager'a DEGIL PainterManager'a abone:
// mod puani orada yasiyor ve sahneler arasi ayakta kaliyor.
[DisallowMultipleComponent]
public class MoodRow : MonoBehaviour
{
    [SerializeField] private MoodCircle circlePrefab;
    [SerializeField] private Transform root;
    [SerializeField] private UiTheme theme;

    [Tooltip("Vurgu kaymasinin suresi (sn). Yalnizca kozmetik.")]
    [SerializeField] private float transitionDuration = 0.18f;

    private readonly List<MoodCircle> circles = new List<MoodCircle>(8);

    // Gecisin baslangic degerleri. Coroutine icinde yerel dizi acmak yerine alan:
    // satir bir kez kuruluyor, her mod degisiminde iki dizi cop uretmesin.
    private float[] fromScale;
    private float[] fromAlpha;

    private bool subscribed;

    // Tek coroutine, daire basina bir tane degil: alti ayri coroutine her karede
    // alti kat is demekti ve hepsini ayri ayri kesmek gerekirdi.
    private Coroutine transition;

    // Ilk tazeleme animasyonsuz olmali: acilista daireler sifirdan hedefe
    // yurumez, dogru durumda belirir.
    private bool shown;

    private void OnEnable()
    {
        TrySubscribe();
        Refresh();
    }

    // OnEnable'da PainterManager.Instance henuz kurulmamis olabilir: Unity
    // nesneler arasi Awake sirasini garanti etmiyor. Start'ta kesin hazir.
    private void Start()
    {
        TrySubscribe();
        Refresh();
    }

    private void OnDisable()
    {
        StopTransition();

        if (!subscribed)
        {
            return;
        }

        PainterManager painter = PainterManager.Instance;
        if (painter != null)
        {
            painter.MoodChanged -= HandleMoodChanged;
        }

        subscribed = false;

        // Panel tekrar acildiginda animasyonsuz dogru durumla gelsin.
        shown = false;
    }

    public void Refresh()
    {
        Build();

        PainterManager painter = PainterManager.Instance;

        // Manager yoksa (PaintersScene, ya da ileride mood tasimayan bir sahne)
        // en notr mod gosteriliyor -- MoodPill de boyle davraniyor.
        PainterManager.PainterMoods mood = painter != null
            ? painter.CurrentMood
            : PainterManager.PainterMoods.Normal;

        int active = MoodScale.SlotForMood(mood);

        // Ilk gosterimde ya da bilesen kapaliyken: aninda. Aksi halde hedefler
        // yazilir ve tek coroutine gorseli oraya yurutur.
        bool instant = !shown || !isActiveAndEnabled || transitionDuration <= 0f;

        for (int i = 0; i < circles.Count; i++)
        {
            circles[i].SetIcon(theme != null ? theme.IconFor(MoodScale.MoodForSlot(i)) : null);
            circles[i].SetSelected(i == active, instant);
        }

        shown = true;

        if (instant)
        {
            StopTransition();
            return;
        }

        // Yeni gecis eskisini KESIYOR. Mod tek cagrida birkac basamak
        // atlayabiliyor (kapanan gunlerin cezasi) ve yarim kalmis bir
        // yumusatmanin ustune binmek daireleri arada bir yerde birakirdi.
        StopTransition();
        transition = StartCoroutine(Animate());
    }

    private void HandleMoodChanged(int _)
    {
        Refresh();
    }

    private void TrySubscribe()
    {
        if (subscribed)
        {
            return;
        }

        PainterManager painter = PainterManager.Instance;
        if (painter == null)
        {
            return;
        }

        painter.MoodChanged += HandleMoodChanged;
        subscribed = true;
    }

    private void Build()
    {
        if (circlePrefab == null || root == null || circles.Count > 0)
        {
            return;
        }

        for (int i = 0; i < MoodScale.Count; i++)
        {
            MoodCircle circle = Instantiate(circlePrefab, root);
            circle.name = "MoodCircle " + i;
            circles.Add(circle);
        }

        fromScale = new float[circles.Count];
        fromAlpha = new float[circles.Count];
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

    // Bastaki degerden hedefe yumusak gecis. Yalnizca localScale ve CanvasGroup
    // alpha'si degisiyor -- rect olculeri degismedigi icin yerlesim yeniden
    // hesaplanmiyor ve satir akmiyor.
    private IEnumerator Animate()
    {
        int count = circles.Count;

        // Baslangic degerleri: gecis ortasinda yeni bir Refresh gelirse yurume
        // bulundugu yerden devam etsin, basa sarmasin.
        for (int i = 0; i < count; i++)
        {
            fromScale[i] = circles[i].CurrentScale;
            fromAlpha[i] = circles[i].CurrentAlpha;
        }

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            // Yavaslayarak varis: hareket hedefe otururken sakinlesiyor.
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            t = 1f - (1f - t) * (1f - t);

            for (int i = 0; i < count; i++)
            {
                circles[i].ApplyVisual(
                    Mathf.Lerp(fromScale[i], circles[i].TargetScale, t),
                    Mathf.Lerp(fromAlpha[i], circles[i].TargetAlpha, t));
            }

            yield return null;
        }

        // Son kare tam hedefte kapansin: Lerp'in yuvarlamasi kalmasin.
        for (int i = 0; i < count; i++)
        {
            circles[i].ApplyVisual(circles[i].TargetScale, circles[i].TargetAlpha);
        }

        transition = null;
    }
}
