using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Bir Image uzerinde seri alevinin 4 karesini donguye alan kucuk oynatici.
//
// AttentionPulse ile ayni sinif bir bilesen (tek is, tek coroutine, kendi
// rengini/gorselini surer) ama BIR FARKLA: bu builder tarafindan prefaba
// konuyor ve alanlari UiWiring ile baglaniyor, calisma aninda EnsureOn ile
// takilmiyor -- cunku hangi Image'in alev oldugu bir yerlesim karari, bir
// durum karari degil.
//
// Kareleri temadan cozuyor (UiTheme.FlameFrames); cagiran taraf yalnizca
// "hangi basamak, bekliyor mu, buyuk mu" soyluyor.
[DisallowMultipleComponent]
public class FlameSprite : MonoBehaviour
{
    // Teslim dosyasinin zamanlamasi: yanan alev 0.6 s/tur (4 kare x 0.15),
    // bekleyen gri alev YARIM hizda 1.2 s/tur. Hiz farki kasitli -- gri alev
    // "uyuyor" gibi durmali, yanan alev canli.
    private const float LitFrameSeconds = 0.15f;
    private const float PendingFrameSeconds = 0.3f;

    [SerializeField] private Image image;
    [SerializeField] private UiTheme theme;

    private Sprite[] frames;
    private Coroutine routine;

    private int currentTier = -1;
    private bool currentPending;
    private bool currentLarge;
    private bool hasState;

    // Kare suresi: SetTier bunu bir kez cozuyor, coroutine her karede
    // yeniden sormuyor.
    private float frameSeconds = LitFrameSeconds;

    private void Awake()
    {
        if (image == null)
        {
            image = GetComponent<Image>();
        }
    }

    private void OnEnable()
    {
        // Kapaninca coroutine olur, acilinca MEVCUT kareden devam etmeli --
        // basa sarmak, listeyi kaydirip geri gelen her tile'in alevini ayni
        // kareye hizalardi ve izgara topluca "yanip sonerdi".
        StartLoop();
    }

    private void OnDisable()
    {
        StopLoop();
    }

    // Tile'in/karonun Bind'i her HabitChanged'de cagiriyor. Durum GERCEKTEN
    // degismediyse hicbir sey yapmiyor: aksi halde her tazeleme alevi kare
    // 0'a sardirir ve donmus gibi gorunurdu (AttentionPulse.Play'in idempotent
    // olma gerekcesinin aynisi).
    public void SetTier(int tier, bool pending, bool large)
    {
        if (hasState && currentTier == tier && currentPending == pending && currentLarge == large)
        {
            return;
        }

        currentTier = tier;
        currentPending = pending;
        currentLarge = large;
        hasState = true;

        frames = theme != null ? theme.FlameFrames(tier, pending, large) : null;
        frameSeconds = pending ? PendingFrameSeconds : LitFrameSeconds;

        ApplyLift();

        if (image != null)
        {
            // Kareler cozulemediyse (tema bos, sayfa dilimlenmemis) gorsel
            // sessizce kapaniyor -- MoodCircle'in atanmamis mod ikonunu
            // gizlemesiyle ayni tolerans, patlamak yerine bos birakmak.
            bool ready = frames != null && frames.Length > 0;
            image.enabled = ready;

            if (ready)
            {
                image.sprite = frames[0];
            }
        }

        StartLoop();
    }

    // Alevi kutusunun icinde yukari kaydirir, boylece SANATIN ortasi kutunun
    // ortasina gelir (bkz. UiTheme.FlameTier.iconLift). Yanindaki sayiyla
    // hizalanmasini saglayan sey bu: butun alevler karenin tabanina yasli
    // cizildigi icin kaydirmasiz birakilinca alev asagida, sayi yukarida
    // duruyor -- ve sapma basamaga gore degisiyor, yani tek bir sabitle
    // duzelmiyor.
    //
    // Kaydirma gorunur pikselleri kutunun DISINA tasirmiyor: kaldirma miktari
    // tam olarak sanatin karede birakti bosluk kadar, yani hizalanmis sanat
    // her basamakta ayni 24 (ya da 160) birimlik ayak izinin icinde kaliyor.
    //
    // Bu rect bir layout grubunun DOGRUDAN cocugu OLMAMALI -- HorizontalLayout
    // Group cocuklarinin anchoredPosition'ini kendisi yazar ve kaydirmayi bir
    // sonraki duzen gecisinde geri alirdi. Builder bu yuzden her iki yerde de
    // layout'un gordugu bir "yuva" ile icindeki bu gorseli ayirdi.
    private void ApplyLift()
    {
        if (image == null)
        {
            return;
        }

        RectTransform rect = image.rectTransform;
        float lift = theme != null ? theme.FlameLift(currentTier, currentLarge) : 0f;

        Vector2 position = rect.anchoredPosition;
        float wanted = lift * rect.rect.height;

        // IDEMPOTENT: zaten dogruysa YAZMIYORUZ. Kaydirma rect'in kendi
        // yuksekliginden turedigi icin OnRectTransformDimensionsChange'ten de
        // cagriliyor; kosulsuz yazmak o geri cagrinin kendini tetikleyip
        // tetiklemedigine bagli bir donguye acik birakirdi (bkz. CLAUDE.md --
        // FullBleedFitter / MaxHeightFitter'in ayni tuzagi).
        if (Mathf.Approximately(position.y, wanted))
        {
            return;
        }

        position.y = wanted;
        rect.anchoredPosition = position;
    }

    // Yuksekligi duzen belirledigi icin SetTier ilk cagrildiginda rect henuz
    // olculmemis olabilir (ozellikle prefab yeni ornendigi karede). Duzen
    // oturdugunda kaydirmayi tazeliyoruz.
    private void OnRectTransformDimensionsChange()
    {
        if (hasState)
        {
            ApplyLift();
        }
    }

    private void StartLoop()
    {
        StopLoop();

        if (!isActiveAndEnabled || frames == null || frames.Length <= 1 || image == null)
        {
            return;
        }

        routine = StartCoroutine(Animate());
    }

    private void StopLoop()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    // unscaledDeltaTime: alev Time.timeScale'den bagimsiz -- kutlama ekrani
    // acikken oyun durdurulsa bile alev yanmaya devam etmeli (projedeki
    // butun UI animasyonlarinin ayni karari).
    private IEnumerator Animate()
    {
        float elapsed = 0f;
        int frame = 0;

        while (true)
        {
            elapsed += Time.unscaledDeltaTime;

            if (elapsed >= frameSeconds)
            {
                // Dongu, "elapsed = 0" DEGIL: dusuk kare hizinda bir karede
                // birden fazla kare suresi gecebilir ve sifirlamak alevi
                // yavaslatirdi.
                elapsed -= frameSeconds * Mathf.Floor(elapsed / frameSeconds);

                frame = (frame + 1) % frames.Length;
                image.sprite = frames[frame];
            }

            yield return null;
        }
    }
}
