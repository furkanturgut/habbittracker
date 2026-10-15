using System.Collections;
using TMPro;
using UnityEngine;

// Kisa bildirim balonu: "Hepsi tamam! Ruh hali +1", "Tablo bitti! Galeriye eklendi".
//
// Ust uste gelen mesajlarda sonuncusu kazaniyor -- bekleyen kuyruk yok. Iki olay
// ayni karede tetiklenebiliyor (son parca hem tabloyu bitirir hem gunu tamamlar)
// ve kullaniciya arka arkaya iki balon gostermek yerine en taze olani birakmak
// daha az gurultulu.
[DisallowMultipleComponent]
public class ToastView : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform panel;
    [SerializeField] private TMP_Text label;

    [SerializeField] private float popSeconds = 0.3f;
    [SerializeField] private float holdSeconds = 2.2f;
    [SerializeField] private float popOffset = 10f;

    private Coroutine routine;

    public void Show(string message)
    {
        if (label == null || group == null)
        {
            return;
        }

        label.text = message;
        gameObject.SetActive(true);

        if (routine != null)
        {
            StopCoroutine(routine);
        }

        routine = StartCoroutine(Play());
    }

    public void Hide()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        group.alpha = 0f;
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        // Coroutine GameObject kapaninca oluyor; referansi birakmazsak bir sonraki
        // Show yasamayan bir coroutine'i durdurmaya calisirdi.
        routine = null;
    }

    private IEnumerator Play()
    {
        Vector2 target = panel != null ? panel.anchoredPosition : Vector2.zero;
        Vector2 start = target + new Vector2(0f, popOffset);

        for (float t = 0f; t < popSeconds; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / popSeconds);
            group.alpha = k;

            if (panel != null)
            {
                panel.anchoredPosition = Vector2.Lerp(start, target, k);
            }

            yield return null;
        }

        group.alpha = 1f;
        if (panel != null)
        {
            panel.anchoredPosition = target;
        }

        yield return new WaitForSeconds(holdSeconds);

        for (float t = 0f; t < popSeconds; t += Time.deltaTime)
        {
            group.alpha = 1f - Mathf.Clamp01(t / popSeconds);
            yield return null;
        }

        routine = null;
        Hide();
    }
}
