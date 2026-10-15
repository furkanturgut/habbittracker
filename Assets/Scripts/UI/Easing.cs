using UnityEngine;

// Animasyon yumusatma egrileri. Saf statik fonksiyonlar -- MonoBehaviour yok,
// Time.deltaTime yok -- boylece sahne kurmadan test edilebiliyor. Coroutine'ler
// (ButtonPressFeedback, ileride ToastView/HabitSheet) yalnizca k'yi [0,1]
// araliginda ilerletip burayi cagirir.
public static class Easing
{
    public static float OutCubic(float k)
    {
        float inv = 1f - k;
        return 1f - inv * inv * inv;
    }

    // Ileri-geri "nefes" egrisi: period saniyede bir tam tur (0 -> 1 -> 0).
    // Lineer testere disi yerine smoothstep: uclarda yavaslar, yani "yanip sonuyor"
    // degil "nefes aliyor" gibi durur. Saf -- elapsed'i cagiran biriktirir.
    public static float Breathe(float elapsed, float period)
    {
        if (period <= 0f)
        {
            return 0f;
        }

        float phase = elapsed / period;
        phase -= Mathf.Floor(phase);

        float triangle = phase < 0.5f ? phase * 2f : (1f - phase) * 2f;
        return triangle * triangle * (3f - 2f * triangle);
    }

    // Galeri 2. seviyenin kart->tam-ekran genislemesi: teslim dosyasinin
    // "cubic-bezier(.2,.8,.2,1)" egrisinin KENDISI, bir yaklasiklik degil --
    // k zamanin oraninda x(t)=k'yi Newton-Raphson ile cozup y(t)'yi donduruyor.
    // x kontrol noktalari (.2,.2) tek-kollu monoton oldugu icin 6 yineleme
    // ondalik hassasiyete kadar yakinsiyor (bkz. UiEasingTests).
    public static float OutEmphasized(float k)
    {
        const float x1 = 0.2f, y1 = 0.8f, x2 = 0.2f, y2 = 1f;
        k = Mathf.Clamp01(k);

        float t = k;
        for (int i = 0; i < 6; i++)
        {
            float dx = BezierDerivative(t, x1, x2);
            if (Mathf.Abs(dx) < 1e-6f)
            {
                break;
            }

            t -= (BezierComponent(t, x1, x2) - k) / dx;
            t = Mathf.Clamp01(t);
        }

        return BezierComponent(t, y1, y2);
    }

    private static float BezierComponent(float t, float p1, float p2)
    {
        float inv = 1f - t;
        return 3f * inv * inv * t * p1 + 3f * inv * t * t * p2 + t * t * t;
    }

    private static float BezierDerivative(float t, float p1, float p2)
    {
        float inv = 1f - t;
        return 3f * inv * inv * p1 + 6f * inv * t * (p2 - p1) + 3f * t * t * (1f - p2);
    }
}
