using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Alttan acilan saat secici -- ConfirmSheet'in KARDESI (ayni scrim/kayma
// gorsel sablonu) ama kodu PAYLASMIYOR, aynen PaintingChoiceSheet'in de
// HabitSheet ile paylasmadigi gibi (bkz. CLAUDE.md > PaintingChoiceSheet
// notu): gorsel desen benziyor ama eksen/sahiplik farkli, burada icerik de
// (metin yerine saat/dakika adimlayicilari) tamamen baska.
//
// Dakika adimi 5 -- teslim karari (REMINDER_PLAN.md Faz 4). Sarma
// TimeOfDay.Wrap uzerinden: 23+1 -> 0, 0-1 -> 23 gibi.
[DisallowMultipleComponent]
public class TimePickerSheet : MonoBehaviour
{
    private const int MinuteStep = 5;

    [SerializeField] private CanvasGroup scrimGroup;
    [SerializeField] private Button scrimButton;
    [SerializeField] private RectTransform panel;
    [SerializeField] private TMP_Text hourLabel;
    [SerializeField] private TMP_Text minuteLabel;
    [SerializeField] private Button hourUpButton;
    [SerializeField] private Button hourDownButton;
    [SerializeField] private Button minuteUpButton;
    [SerializeField] private Button minuteDownButton;
    [SerializeField] private Button confirmButton;

    [SerializeField] private float slideSeconds = 0.22f;

    private float panelHeight;
    private int hour;
    private int minute;
    private Coroutine routine;

    public event Action<int, int> Picked;

    private void Awake()
    {
        if (panel != null)
        {
            panelHeight = panel.rect.height;
        }

        if (scrimButton != null)
        {
            scrimButton.onClick.AddListener(Close);
        }

        if (hourUpButton != null)
        {
            hourUpButton.onClick.AddListener(HandleHourUp);
        }

        if (hourDownButton != null)
        {
            hourDownButton.onClick.AddListener(HandleHourDown);
        }

        if (minuteUpButton != null)
        {
            minuteUpButton.onClick.AddListener(HandleMinuteUp);
        }

        if (minuteDownButton != null)
        {
            minuteDownButton.onClick.AddListener(HandleMinuteDown);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(HandleConfirm);
        }
    }

    private void OnDestroy()
    {
        if (scrimButton != null)
        {
            scrimButton.onClick.RemoveListener(Close);
        }

        if (hourUpButton != null)
        {
            hourUpButton.onClick.RemoveListener(HandleHourUp);
        }

        if (hourDownButton != null)
        {
            hourDownButton.onClick.RemoveListener(HandleHourDown);
        }

        if (minuteUpButton != null)
        {
            minuteUpButton.onClick.RemoveListener(HandleMinuteUp);
        }

        if (minuteDownButton != null)
        {
            minuteDownButton.onClick.RemoveListener(HandleMinuteDown);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(HandleConfirm);
        }

        StopRoutine();
    }

    public void Show(string hhmm)
    {
        if (!TimeOfDay.TryParse(hhmm, out hour, out minute))
        {
            hour = 0;
            minute = 0;
        }

        RefreshLabels();

        gameObject.SetActive(true);
        StopRoutine();
        routine = StartCoroutine(Slide());
    }

    public void Close()
    {
        if (!gameObject.activeSelf)
        {
            return;
        }

        StopRoutine();
        routine = StartCoroutine(CloseSlide());
    }

    private void HandleHourUp()
    {
        hour = TimeOfDay.Wrap(hour + 1, 24);
        RefreshLabels();
    }

    private void HandleHourDown()
    {
        hour = TimeOfDay.Wrap(hour - 1, 24);
        RefreshLabels();
    }

    private void HandleMinuteUp()
    {
        minute = TimeOfDay.Wrap(minute + MinuteStep, 60);
        RefreshLabels();
    }

    private void HandleMinuteDown()
    {
        minute = TimeOfDay.Wrap(minute - MinuteStep, 60);
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        if (hourLabel != null)
        {
            hourLabel.text = hour.ToString("00", CultureInfo.InvariantCulture);
        }

        if (minuteLabel != null)
        {
            minuteLabel.text = minute.ToString("00", CultureInfo.InvariantCulture);
        }
    }

    // Once kapat, SONRA cagir -- ConfirmSheet.HandleConfirm ile AYNI sira:
    // Picked bir ust ekranin kendi kapanisini tetiklerse (tetiklemiyor ama
    // kural tutarli olsun diye) bu nesnenin kapanisi zaten tamamlanmis olmali.
    private void HandleConfirm()
    {
        int h = hour;
        int m = minute;
        Close();
        Picked?.Invoke(h, m);
    }

    private void StopRoutine()
    {
        if (routine == null)
        {
            return;
        }

        StopCoroutine(routine);
        routine = null;
    }

    private IEnumerator Slide()
    {
        if (panel != null)
        {
            panel.anchoredPosition = new Vector2(0f, -panelHeight);
        }

        if (scrimGroup != null)
        {
            scrimGroup.alpha = 0f;
            scrimGroup.blocksRaycasts = true;
            scrimGroup.interactable = true;
        }

        float elapsed = 0f;

        while (elapsed < slideSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float eased = Easing.OutCubic(Mathf.Clamp01(elapsed / slideSeconds));

            if (scrimGroup != null)
            {
                scrimGroup.alpha = eased;
            }

            if (panel != null)
            {
                panel.anchoredPosition = new Vector2(0f, Mathf.Lerp(-panelHeight, 0f, eased));
            }

            yield return null;
        }

        if (scrimGroup != null)
        {
            scrimGroup.alpha = 1f;
        }

        if (panel != null)
        {
            panel.anchoredPosition = Vector2.zero;
        }

        routine = null;
    }

    private IEnumerator CloseSlide()
    {
        float startAlpha = scrimGroup != null ? scrimGroup.alpha : 1f;
        float startY = panel != null ? panel.anchoredPosition.y : 0f;
        float elapsed = 0f;

        while (elapsed < slideSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float eased = Easing.OutCubic(Mathf.Clamp01(elapsed / slideSeconds));

            if (scrimGroup != null)
            {
                scrimGroup.alpha = Mathf.Lerp(startAlpha, 0f, eased);
            }

            if (panel != null)
            {
                panel.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, -panelHeight, eased));
            }

            yield return null;
        }

        if (scrimGroup != null)
        {
            scrimGroup.alpha = 0f;
            scrimGroup.blocksRaycasts = false;
            scrimGroup.interactable = false;
        }

        gameObject.SetActive(false);
        routine = null;
    }
}
