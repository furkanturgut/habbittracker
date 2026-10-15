using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Pazartesi / Pazar" ikili segmenti. OptionButton'i KULLANMIYOR -- o
// kart/pil gorunumu (zemin + kenarlik + etiket, N secenek), bu pill-ICINDE-
// pill (bir ray zemininin uzerinde iki dar sekme, kenarliksiz). Gorsel desen
// farkli ama secili/secilmeyen renk gecisi AYNI 0.2 sn kalibinda.
[DisallowMultipleComponent]
public class SettingsSegmentRow : MonoBehaviour
{
    [SerializeField] private Button mondayButton;
    [SerializeField] private Image mondayBackground;
    [SerializeField] private TMP_Text mondayLabel;
    [SerializeField] private Button sundayButton;
    [SerializeField] private Image sundayBackground;
    [SerializeField] private TMP_Text sundayLabel;
    [SerializeField] private UiTheme theme;

    [SerializeField] private float transitionSeconds = 0.2f;

    private WeekStart current;
    private Coroutine transition;

    // Gecisin baslangic degerleri: coroutine icinde yerel degisken acmak
    // yerine alan, DayChipRow/MoodRow'daki ayni "bir kez kurulur" kalibi.
    private Color fromMondayBg, fromMondayLabel, fromSundayBg, fromSundayLabel;

    public event Action<WeekStart> Picked;

    private void Awake()
    {
        if (mondayButton != null)
        {
            mondayButton.onClick.AddListener(PickMonday);
        }

        if (sundayButton != null)
        {
            sundayButton.onClick.AddListener(PickSunday);
        }
    }

    private void OnDestroy()
    {
        if (mondayButton != null)
        {
            mondayButton.onClick.RemoveListener(PickMonday);
        }

        if (sundayButton != null)
        {
            sundayButton.onClick.RemoveListener(PickSunday);
        }

        StopTransition();
    }

    private void PickMonday()
    {
        Pick(WeekStart.Monday);
    }

    private void PickSunday()
    {
        Pick(WeekStart.Sunday);
    }

    private void Pick(WeekStart value)
    {
        if (value == current)
        {
            return;
        }

        SetValue(value, false);
        Picked?.Invoke(value);
    }

    public void SetValue(WeekStart value, bool instant)
    {
        current = value;

        if (instant || !isActiveAndEnabled || transitionSeconds <= 0f)
        {
            StopTransition();
            ApplyVisual(TargetMondayBg, TargetMondayLabel, TargetSundayBg, TargetSundayLabel);
            return;
        }

        StopTransition();
        transition = StartCoroutine(Animate());
    }

    private Color TargetMondayBg => current == WeekStart.Monday && theme != null ? theme.gold : Color.clear;
    private Color TargetSundayBg => current == WeekStart.Sunday && theme != null ? theme.gold : Color.clear;

    private Color TargetMondayLabel => current == WeekStart.Monday && theme != null ? theme.textOnGold : TextMutedOrWhite();
    private Color TargetSundayLabel => current == WeekStart.Sunday && theme != null ? theme.textOnGold : TextMutedOrWhite();

    // rgba(243,230,204,.75) secili olmayan etiket -- mevcut alanlarin
    // hicbirine TAM denk gelmiyor (textMuted .62, premiumListText .85);
    // Faz 0 bu ekran icin yeni bir alan eklemedigi icin en yakinini
    // (textMuted) kullaniyoruz.
    private Color TextMutedOrWhite()
    {
        return theme != null ? theme.textMuted : Color.white;
    }

    private void ApplyVisual(Color mondayBg, Color mondayLabelColor, Color sundayBg, Color sundayLabelColor)
    {
        if (mondayBackground != null)
        {
            mondayBackground.color = mondayBg;
        }

        if (mondayLabel != null)
        {
            mondayLabel.color = mondayLabelColor;
        }

        if (sundayBackground != null)
        {
            sundayBackground.color = sundayBg;
        }

        if (sundayLabel != null)
        {
            sundayLabel.color = sundayLabelColor;
        }
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

    private IEnumerator Animate()
    {
        fromMondayBg = mondayBackground != null ? mondayBackground.color : Color.clear;
        fromMondayLabel = mondayLabel != null ? mondayLabel.color : Color.white;
        fromSundayBg = sundayBackground != null ? sundayBackground.color : Color.clear;
        fromSundayLabel = sundayLabel != null ? sundayLabel.color : Color.white;

        float elapsed = 0f;

        while (elapsed < transitionSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Easing.OutCubic(Mathf.Clamp01(elapsed / transitionSeconds));

            ApplyVisual(
                Color.Lerp(fromMondayBg, TargetMondayBg, t),
                Color.Lerp(fromMondayLabel, TargetMondayLabel, t),
                Color.Lerp(fromSundayBg, TargetSundayBg, t),
                Color.Lerp(fromSundayLabel, TargetSundayLabel, t));

            yield return null;
        }

        ApplyVisual(TargetMondayBg, TargetMondayLabel, TargetSundayBg, TargetSundayLabel);
        transition = null;
    }
}
