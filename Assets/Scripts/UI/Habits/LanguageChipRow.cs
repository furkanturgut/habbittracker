using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Dil secici alt sayfasinin cip izgarasi. CategoryChipRow'un AYNI kalibi,
// ama olcek SABIT: AppLanguageNames.Selectable 8 elemanli ve degismez --
// "+ olustur" satiri YOK (kullanici yeni dil ekleyemez, CreateRequested
// karsiligi bu sinifta hic yok).
[DisallowMultipleComponent]
public class LanguageChipRow : MonoBehaviour
{
    [SerializeField] private OptionButton chipPrefab;
    [SerializeField] private Transform root;
    [SerializeField] private float transitionSeconds = 0.16f;

    private readonly List<OptionButton> chips = new List<OptionButton>(8);

    private Color[] fromBackground;
    private Color[] fromBorder;
    private Color[] fromLabel;

    private Coroutine transition;

    public event Action<AppLanguage> LanguageSelected;

    private void OnDisable()
    {
        StopTransition();
    }

    public void Show(AppLanguage selected, bool instant)
    {
        AppLanguage[] options = AppLanguageNames.Selectable;
        EnsureCapacity(options.Length);

        for (int i = 0; i < options.Length; i++)
        {
            OptionButton chip = ChipAt(i);
            chip.Label.text = AppLanguageNames.DisplayName(options[i]);
            chip.SetSelected(options[i] == selected, instant);
            chip.gameObject.SetActive(true);
        }

        for (int i = options.Length; i < chips.Count; i++)
        {
            chips[i].gameObject.SetActive(false);
        }

        if (instant || !isActiveAndEnabled || transitionSeconds <= 0f)
        {
            StopTransition();
            return;
        }

        // Yeni gecis eskisini KESIYOR: CategoryChipRow.Show'daki AYNI gerekce.
        StopTransition();
        transition = StartCoroutine(Animate(options.Length));
    }

    private OptionButton ChipAt(int index)
    {
        while (chips.Count <= index)
        {
            OptionButton created = Instantiate(chipPrefab, root);
            int capturedIndex = chips.Count;
            created.Button.onClick.AddListener(() => RaiseClicked(capturedIndex));
            chips.Add(created);
        }

        return chips[index];
    }

    private void RaiseClicked(int index)
    {
        AppLanguage[] options = AppLanguageNames.Selectable;
        if (index < 0 || index >= options.Length)
        {
            return;
        }

        LanguageSelected?.Invoke(options[index]);
    }

    private void EnsureCapacity(int count)
    {
        if (fromBackground != null && fromBackground.Length >= count)
        {
            return;
        }

        fromBackground = new Color[count];
        fromBorder = new Color[count];
        fromLabel = new Color[count];
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

    private IEnumerator Animate(int count)
    {
        for (int i = 0; i < count; i++)
        {
            OptionButton chip = chips[i];
            fromBackground[i] = chip.CurrentBackground;
            fromBorder[i] = chip.CurrentBorder;
            fromLabel[i] = chip.CurrentLabelColor;
        }

        float elapsed = 0f;

        while (elapsed < transitionSeconds)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / transitionSeconds);
            float eased = Easing.OutCubic(t);

            for (int i = 0; i < count; i++)
            {
                OptionButton chip = chips[i];
                chip.ApplyVisual(
                    Color.Lerp(fromBackground[i], chip.TargetBackground, eased),
                    Color.Lerp(fromBorder[i], chip.TargetBorder, eased),
                    Color.Lerp(fromLabel[i], chip.TargetLabelColor, eased));
            }

            yield return null;
        }

        // Son kare tam hedefte kapansin: Lerp'in yuvarlamasi kalmasin.
        for (int i = 0; i < count; i++)
        {
            OptionButton chip = chips[i];
            chip.ApplyVisual(chip.TargetBackground, chip.TargetBorder, chip.TargetLabelColor);
        }

        transition = null;
    }
}
