using System.Collections;
using UnityEngine;

// Painter'a vurulunca dokunulan noktada beliren kisa "ofke" isareti.
//
// env_emote_anger.png bir ANIMASYON SERIDI DEGIL: Sprite Editor'de onceden
// dilimlenmis, siki paketlenmis (tightly trimmed) bir atlas -- her alt-sprite
// KENDI sinirlarina gore ortalaniyor (alignment Center). Halka motifinin
// yanindaki "pirilti" aksanlari (orn. _1/_2/_4) o yuzden kendi kucuk
// kutularinin merkezine atlayip halkanin merkezinden FARKLI bir noktada
// beliriyordu -- butun 20 kareyi sirayla oynatmak (ilk deneme) bu yuzden
// "gidip geliyormus gibi" bir glitch uretiyordu. Kare hizini degistirmek bunu
// COZMEZ, cunku sorun zamanlama degil, FARKLI PIVOT'LARDA sabitlenmis
// parcalari tek bir noktada gostermekti.
//
// Cozum, "hic kare degistirme" DEGIL: sorun kare degistirmekte degil, YANLIS
// karelerde (kendi pivotuna gore farkli konumlanan, izole pirilti kareleri)
// idi. env_emote_anger_0 (sade halka) ile env_emote_anger_1 (halka + iki
// pirilti, TEK gorselde birlikte) .meta'daki rect'lere gore AYNI dikey
// merkeze sahip ve piriltilar halkanin etrafinda simetrik -- yani ikisi
// arasinda gecis yapmak herhangi bir zipla(t)ma yaratmiyor. Pop anindaki
// "parlama" bu ikisi arasinda gecisle geri geliyor; prosedurel pop+fade
// egrisi (olcek + alfa) AYNEN kaliyor.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class AngerBurstVfx : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("Sakin hal: sade halka (env_emote_anger_0). Settle/Hold/Fade boyunca gosterilir.")]
    [SerializeField] private Sprite baseSprite;

    [Tooltip("Parlama ani: halka + iki pirilti (env_emote_anger_1). Pop/Settle boyunca gosterilir. Bos birakilirsa baseSprite kullanilir.")]
    [SerializeField] private Sprite peakSprite;

    // Pop: 0 -> hafif asiri buyume (overshoot) -> nihai olcege oturma.
    // Hold: nihai olcekte sabit kalma. Fade: alfa 1 -> 0. Toplam omur ~0.55s.
    private const float PopSeconds = 0.12f;
    private const float OvershootScale = 1.2f;
    private const float SettleSeconds = 0.08f;
    private const float HoldSeconds = 0.15f;
    private const float FadeSeconds = 0.2f;

    // Builder'in prefaba yazdigi nihai "tasarim pikseli" olcegi -- Awake'te
    // (OnEnable sifirlamadan ONCE) saklaniyor, pop/settle bu hedefe dogru
    // oynuyor.
    private Vector3 targetScale;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        targetScale = transform.localScale;
    }

    private void OnEnable()
    {
        if (baseSprite == null || spriteRenderer == null)
        {
            Destroy(gameObject);
            return;
        }

        spriteRenderer.sprite = peakSprite != null ? peakSprite : baseSprite;
        transform.localScale = Vector3.zero;
        SetAlpha(1f);

        StartCoroutine(Play());
    }

    public static void SpawnAt(AngerBurstVfx prefab, Vector3 worldPosition, Transform parent)
    {
        if (prefab == null)
        {
            return;
        }

        Instantiate(prefab, worldPosition, Quaternion.identity, parent);
    }

    private IEnumerator Play()
    {
        yield return Pop();

        // Parlama (pirilti) ani burada biter: olcek nihai degerine oturdu,
        // sade halkaya gecip Hold/Fade'i o anlatiyor -- surekli pirilti
        // gostermek "yapiskan" durur, kisa bir flama daha dogru okunuyor.
        spriteRenderer.sprite = baseSprite;

        yield return new WaitForSecondsRealtime(HoldSeconds);
        yield return Fade();

        Destroy(gameObject);
    }

    // unscaledTime: FlameSprite/SpriteFrameLoop'un ayni karari -- oyun
    // durdurulsa bile tepki oynamaya devam etmeli.
    private IEnumerator Pop()
    {
        Vector3 overshoot = targetScale * OvershootScale;
        float elapsed = 0f;

        while (elapsed < PopSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / PopSeconds), 3f);
            transform.localScale = Vector3.LerpUnclamped(Vector3.zero, overshoot, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < SettleSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / SettleSeconds);
            transform.localScale = Vector3.Lerp(overshoot, targetScale, t);
            yield return null;
        }

        transform.localScale = targetScale;
    }

    private IEnumerator Fade()
    {
        float elapsed = 0f;

        while (elapsed < FadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(1f - Mathf.Clamp01(elapsed / FadeSeconds));
            yield return null;
        }

        SetAlpha(0f);
    }

    private void SetAlpha(float alpha)
    {
        Color color = spriteRenderer.color;
        color.a = alpha;
        spriteRenderer.color = color;
    }
}
