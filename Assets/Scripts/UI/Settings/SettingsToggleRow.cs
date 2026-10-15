using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Ayarlar ekraninin acik/kapali satiri: 48x28 ray + 22 topuz, sol 3 <-> 23.
//
// HabitsPanelBuilder.BuildEndDateToggle'in gorsel ikizi ama orasi HabitSheet'in
// form durumuna bagli (tek kullanimlik); burasi BAGIMSIZ bir satir bileseni --
// Ayarlar'da uc kez (hatirlatma, ses, titresim) kullanilacak.
//
// BUTUN SATIR dokunma hedefi: teslim dosyasinda satirin tamami onClick tasiyor,
// yalnizca ray degil.
[DisallowMultipleComponent]
public class SettingsToggleRow : MonoBehaviour
{
    [SerializeField] private Button hitArea;
    [SerializeField] private Image track;
    [SerializeField] private RectTransform knob;
    [SerializeField] private UiTheme theme;

    [Tooltip("Topuz gecisinin suresi (sn). Yalnizca kozmetik.")]
    [SerializeField] private float transitionSeconds = 0.2f;

    private const float KnobOffX = 3f;
    private const float KnobOnX = 23f;

    private bool on;
    private Coroutine transition;

    public event Action<bool> Toggled;

    public bool IsOn => on;

    private void Awake()
    {
        if (hitArea != null)
        {
            hitArea.onClick.AddListener(HandleClick);
        }
    }

    private void OnDestroy()
    {
        if (hitArea != null)
        {
            hitArea.onClick.RemoveListener(HandleClick);
        }

        StopTransition();
    }

    private void HandleClick()
    {
        SetOn(!on, false);
        Toggled?.Invoke(on);
    }

    // Semantik durum SENKRON yazilir (ray rengi); topuzun konumu instant=false
    // ise tek kendi coroutine'inden yumusak gecer -- SettingsScreen bu satiri
    // tek basina kullaniyor, DayChipRow/MoodRow'daki paylasimli surucuye
    // gerek yok.
    public void SetOn(bool value, bool instant)
    {
        on = value;

        if (track != null && theme != null)
        {
            // rgba(243,230,204,.18) kapali ray -- moodSegmentEmpty (.18) ile
            // AYNI deger, yeni bir alan gerekmedi.
            track.color = on ? theme.gold : theme.moodSegmentEmpty;
        }

        float target = on ? KnobOnX : KnobOffX;

        if (instant || !isActiveAndEnabled || transitionSeconds <= 0f)
        {
            StopTransition();
            ApplyKnob(target);
            return;
        }

        StopTransition();
        transition = StartCoroutine(Animate(target));
    }

    private void ApplyKnob(float x)
    {
        if (knob != null)
        {
            knob.anchoredPosition = new Vector2(x, knob.anchoredPosition.y);
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

    private IEnumerator Animate(float target)
    {
        float from = knob != null ? knob.anchoredPosition.x : target;
        float elapsed = 0f;

        while (elapsed < transitionSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Easing.OutCubic(Mathf.Clamp01(elapsed / transitionSeconds));
            ApplyKnob(Mathf.Lerp(from, target, t));
            yield return null;
        }

        ApplyKnob(target);
        transition = null;
    }
}
