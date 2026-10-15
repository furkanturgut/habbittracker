using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Bir Graphic'in alfasini VE olcegini birlikte "nefes aldirir" -- rotus
// parilti nabzi (yanik rozet/cip) icin.
//
// AttentionPulse ile AYNI "builder prefabi sifirdan kuruyor, calisma aninda
// EnsureOn ile takilir" deseni, ama AttentionPulse yalnizca RENK suruyor ve
// idempotentlik kilidi parametre listesine bagli -- alfa+olcek kanali eklemek
// onu her Refresh()'te yeniden baslatma riskine sokardi. Bu yuzden ayri
// bilesen.
//
// Olcek rect'in localScale'ini yazar, OFSETINI DEGIL -- ButtonPressFeedback
// ile ayni guvenli kanal (CLAUDE.md'nin OnRectTransformDimensionsChange
// tuzagi burada gecerli degil, cunku localScale bir rect boyutu degismiyor).
[DisallowMultipleComponent]
public class GlowPulse : MonoBehaviour
{
    private Graphic target;
    private RectTransform rect;

    private Color restColor;
    private Vector3 restScale = Vector3.one;

    private float minAlpha;
    private float maxAlpha;
    private float minScale;
    private float maxScale;
    private float period;

    private Coroutine routine;
    private bool playing;

    public static GlowPulse EnsureOn(Graphic target)
    {
        if (target == null)
        {
            return null;
        }

        GlowPulse pulse = target.GetComponent<GlowPulse>();
        if (pulse == null)
        {
            pulse = target.gameObject.AddComponent<GlowPulse>();
        }

        pulse.target = target;
        pulse.rect = target.transform as RectTransform;
        pulse.restColor = target.color;

        if (pulse.rect != null)
        {
            pulse.restScale = pulse.rect.localScale;
        }

        return pulse;
    }

    private void OnDisable()
    {
        Stop();
    }

    // Idempotent: ayni parametrelerle zaten calisiyorsa yeniden baslatmaz --
    // AttentionPulse.Play'in ayni gerekcesi.
    public void Play(float newMinAlpha, float newMaxAlpha, float newMinScale, float newMaxScale, float periodSeconds)
    {
        if (playing
            && Mathf.Approximately(minAlpha, newMinAlpha)
            && Mathf.Approximately(maxAlpha, newMaxAlpha)
            && Mathf.Approximately(minScale, newMinScale)
            && Mathf.Approximately(maxScale, newMaxScale)
            && Mathf.Approximately(period, periodSeconds))
        {
            return;
        }

        minAlpha = newMinAlpha;
        maxAlpha = newMaxAlpha;
        minScale = newMinScale;
        maxScale = newMaxScale;
        period = periodSeconds;
        playing = true;

        if (routine != null)
        {
            StopCoroutine(routine);
        }

        routine = StartCoroutine(Animate());
    }

    // Alfayi/olcegi dinlenme degerine dondurur -- EnsureOn aninda yakalanan
    // orijinal renk/olcek.
    public void Stop()
    {
        playing = false;

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (target != null)
        {
            target.color = restColor;
        }

        if (rect != null)
        {
            rect.localScale = restScale;
        }
    }

    private IEnumerator Animate()
    {
        float elapsed = 0f;

        while (true)
        {
            float k = Easing.Breathe(elapsed, period);

            if (target != null)
            {
                Color color = restColor;
                color.a = Mathf.Lerp(minAlpha, maxAlpha, k);
                target.color = color;
            }

            if (rect != null)
            {
                float scale = Mathf.Lerp(minScale, maxScale, k);
                rect.localScale = restScale * scale;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}
