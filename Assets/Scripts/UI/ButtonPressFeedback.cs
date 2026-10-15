using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Bir Button'a basinca kisa bir kuculme geri bildirimi ekler.
//
// Builder prefableri sifirdan kuruyor (bkz. CLAUDE.md, "Build Painter Habits
// UI"), yani bu bilesen prefaba elle eklenemez -- bir sonraki calistirmada
// silinir. Bunun yerine her Button sarmalayicisi (ScreenNavButton,
// OptionButton, HabitFilterButton, HabitCard, HabitSheet) kendi Awake'inde
// EnsureOn ile bu bileseni calisma anında kendine takiyor; builder'a hic
// dokunmadan calisir ve her Play'de yeniden kurulur.
[DisallowMultipleComponent]
public class ButtonPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private float pressedScale = 0.96f;
    [SerializeField] private float seconds = 0.08f;

    private RectTransform rect;
    private Vector3 restScale = Vector3.one;
    private Coroutine routine;
    private bool pressed;

    public static void EnsureOn(Button button)
    {
        EnsureOn(button, 0.96f);
    }

    // Galeri'nin bolum kartlari .985, eser karoları .97 istiyor -- varsayilan
    // disinda bir kuculme gereken her yer bunu kullanir. pressedScale alanina
    // AYNI SINIFTAN eriliyor (private ama static metot Instance alanina
    // erisebiliyor); Inspector'da gorunmesine gerek yok, cagiran deger verir.
    public static void EnsureOn(Button button, float pressedScale)
    {
        if (button == null)
        {
            return;
        }

        ButtonPressFeedback feedback = button.GetComponent<ButtonPressFeedback>();
        if (feedback == null)
        {
            feedback = button.gameObject.AddComponent<ButtonPressFeedback>();
        }

        feedback.pressedScale = pressedScale;
    }

    private void Awake()
    {
        rect = transform as RectTransform;

        if (rect != null)
        {
            restScale = rect.localScale;
        }
    }

    private void OnDisable()
    {
        // Coroutine GameObject kapaninca kendiliginden duruyor; referansi
        // birakmazsak bir sonraki basinca yasamayan bir coroutine'i
        // durdurmaya calisirdi (ToastView'daki ayni notla ayni sebep).
        routine = null;
        pressed = false;

        if (rect != null)
        {
            rect.localScale = restScale;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        AnimateTo(restScale * pressedScale);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
        AnimateTo(restScale);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Parmak/imlec butonu birakmadan disari cikarsa da geri donmeli --
        // yoksa buton birakilmamis gibi kucuk kalir.
        if (pressed)
        {
            pressed = false;
            AnimateTo(restScale);
        }
    }

    private void AnimateTo(Vector3 target)
    {
        if (rect == null)
        {
            return;
        }

        if (routine != null)
        {
            StopCoroutine(routine);
        }

        routine = StartCoroutine(Animate(target));
    }

    private IEnumerator Animate(Vector3 target)
    {
        Vector3 start = rect.localScale;

        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = Easing.OutCubic(Mathf.Clamp01(t / seconds));
            rect.localScale = Vector3.Lerp(start, target, k);
            yield return null;
        }

        rect.localScale = target;
        routine = null;
    }
}
