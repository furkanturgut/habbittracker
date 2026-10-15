using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Ayarlar'daki "Dil" satirini acan alttan kayan secici.
//
// CategoryPickerSheet'in AYNI kalibi (scrim + CanvasGroup + alttan kayma,
// pasif kurulan kutu icin Awake degil EnsureWired) ama iki fark var: "+
// olustur" satiri YOK (AppLanguageNames.Selectable sabit 8 elemanli,
// kullanici yeni dil ekleyemez) ve navigator kilidi YOK -- CategorySettingsSheet/
// TimePickerSheet ile AYNI gerekce: bu sheet Ayarlar panelinin KENDI
// icinde aciliyor, panel zaten acildiginda ressamin dokunmasini kilitlemis
// oluyor, ikinci bir kilit gereksiz. Secim her zaman gecerli bir baslangic
// degeriyle acilir (HabbitManager ilk acilista Unset'i zaten tespit edip
// yazmis olur), bu yuzden CategoryPickerSheet'teki hasSelection/disabled-confirm
// durumu burada yok -- onay dugmesi hep aktif.
[DisallowMultipleComponent]
public class LanguagePickerSheet : MonoBehaviour
{
    [Header("Kutu")]
    [SerializeField] private CanvasGroup scrimGroup;
    [SerializeField] private Button scrimButton;
    [SerializeField] private RectTransform panel;
    [SerializeField] private float slideSeconds = 0.28f;

    [Header("Ust bar")]
    [SerializeField] private Button cancelButton;

    [Header("Secim")]
    [SerializeField] private LanguageChipRow chipRow;

    [Header("Alt bar")]
    [SerializeField] private Button confirmButton;

    private AppLanguage selectedLanguage;

    private Coroutine slideRoutine;
    private Vector2 openPosition;
    private bool openPositionCached;
    private bool closing;
    private bool wired;

    public event Action<AppLanguage> LanguageChosen;

    public void Open(AppLanguage current)
    {
        EnsureWired();

        selectedLanguage = current;

        if (chipRow != null)
        {
            chipRow.Show(selectedLanguage, instant: true);
        }

        if (!openPositionCached)
        {
            openPosition = panel.anchoredPosition;
            openPositionCached = true;
        }

        closing = false;
        gameObject.SetActive(true);

        StartSlide();
    }

    // Close()'in aynasi CategoryPickerSheet.Close ile ayni gerekceyle: kilit,
    // animasyon BITIMINDE aciliyor, SIRASINDA degil.
    public void Close()
    {
        if (closing || !gameObject.activeSelf)
        {
            return;
        }

        closing = true;
        StopSlide();
        slideRoutine = StartCoroutine(CloseSlide());
    }

    private void EnsureWired()
    {
        if (wired)
        {
            return;
        }

        wired = true;

        if (chipRow != null)
        {
            chipRow.LanguageSelected += HandleLanguageSelected;
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(Close);
            ButtonPressFeedback.EnsureOn(cancelButton);
        }

        if (scrimButton != null)
        {
            scrimButton.onClick.AddListener(Close);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(Confirm);
            ButtonPressFeedback.EnsureOn(confirmButton);
        }
    }

    private void OnDisable()
    {
        slideRoutine = null;
        closing = false;
    }

    private void HandleLanguageSelected(AppLanguage language)
    {
        selectedLanguage = language;

        if (chipRow != null)
        {
            chipRow.Show(selectedLanguage, instant: false);
        }
    }

    private void Confirm()
    {
        LanguageChosen?.Invoke(selectedLanguage);
        Close();
    }

    // -----------------------------------------------------------------------
    // Animasyon -- CategoryPickerSheet.Slide/CloseSlide ile ayni maths,
    // bilerek ayri kopya (CLAUDE.md'deki "ayni kalip, ayri uygulama" notu).
    // -----------------------------------------------------------------------

    private void StartSlide()
    {
        StopSlide();
        slideRoutine = StartCoroutine(Slide());
    }

    private void StopSlide()
    {
        if (slideRoutine != null)
        {
            StopCoroutine(slideRoutine);
            slideRoutine = null;
        }
    }

    private IEnumerator Slide()
    {
        float height = panel.rect.height;
        Vector2 target = openPosition;
        Vector2 start = new Vector2(target.x, target.y - height);

        for (float t = 0f; t < slideSeconds; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / slideSeconds);
            float eased = Easing.OutCubic(k);

            panel.anchoredPosition = Vector2.Lerp(start, target, eased);
            scrimGroup.alpha = k;

            yield return null;
        }

        panel.anchoredPosition = target;
        scrimGroup.alpha = 1f;
        slideRoutine = null;
    }

    private IEnumerator CloseSlide()
    {
        float height = panel.rect.height;
        Vector2 start = panel.anchoredPosition;
        Vector2 target = new Vector2(openPosition.x, openPosition.y - height);
        float startAlpha = scrimGroup.alpha;

        for (float t = 0f; t < slideSeconds; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / slideSeconds);
            float eased = Easing.OutCubic(k);

            panel.anchoredPosition = Vector2.Lerp(start, target, eased);
            scrimGroup.alpha = Mathf.Lerp(startAlpha, 0f, eased);

            yield return null;
        }

        panel.anchoredPosition = target;
        scrimGroup.alpha = 0f;
        slideRoutine = null;
        closing = false;

        gameObject.SetActive(false);
    }
}
