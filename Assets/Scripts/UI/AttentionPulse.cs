using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Bir Graphic'in rengini iki renk arasinda surekli "nefes aldirir" -- siradaki
// tablo secimini bekleyen tile'in cerceve rengine binen dikkat cekici.
//
// ButtonPressFeedback.EnsureOn kaliginin aynisi: builder prefabi sifirdan
// kurdugu icin bu bilesen elle eklenemez, calisma aninda EnsureOn ile takilir.
[DisallowMultipleComponent]
public class AttentionPulse : MonoBehaviour
{
    private Graphic target;
    private Color calm;
    private Color alert;
    private float period;
    private Coroutine routine;
    private bool playing;

    public static AttentionPulse EnsureOn(Graphic target)
    {
        if (target == null)
        {
            return null;
        }

        AttentionPulse pulse = target.GetComponent<AttentionPulse>();
        if (pulse == null)
        {
            pulse = target.gameObject.AddComponent<AttentionPulse>();
        }

        pulse.target = target;
        return pulse;
    }

    private void OnDisable()
    {
        Stop();
    }

    // Idempotent: ayni renklerle zaten calisiyorsa yeniden baslatmaz -- yoksa
    // her HabitChanged tazelemesi nabzi basa sardirirdi.
    public void Play(Color calmColor, Color alertColor, float periodSeconds)
    {
        if (playing && calm.Equals(calmColor) && alert.Equals(alertColor) && Mathf.Approximately(period, periodSeconds))
        {
            return;
        }

        calm = calmColor;
        alert = alertColor;
        period = periodSeconds;
        playing = true;

        if (routine != null)
        {
            StopCoroutine(routine);
        }

        routine = StartCoroutine(Animate());
    }

    public void Stop()
    {
        playing = false;

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    private IEnumerator Animate()
    {
        float elapsed = 0f;

        while (true)
        {
            if (target != null)
            {
                target.color = Color.Lerp(calm, alert, Easing.Breathe(elapsed, period));
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}
