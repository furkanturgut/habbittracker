using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Kategori secici alt sayfasinin sarmalanan cip izgarasi.
//
// PaintingPickerRow'un havuzlama kaligi: cip sayisi kategori sayisina gore
// DEGISIYOR (12 yerlesik + kullanicinin ozel kategorileri), GameObject
// yaratip yok etmek yerine var olanlar yeniden baglanir. Son cip HER ZAMAN
// "+ Kategori olustur" -- secilemez, kendi CreateRequested olayini firlatir.
[DisallowMultipleComponent]
public class CategoryChipRow : MonoBehaviour
{
    [SerializeField] private OptionButton chipPrefab;
    [SerializeField] private Transform root;
    [SerializeField] private float transitionSeconds = 0.16f;

    // Son eleman HER ZAMAN "olustur" cipi, bu yuzden null isaretle -- hangi
    // indeksin bir kategori hangisinin "olustur" oldugunu RaiseClicked'da
    // yeniden hesaplamak yerine burada bir kez kaydediliyor.
    private readonly List<OptionButton> chips = new List<OptionButton>(16);
    private readonly List<string> shown = new List<string>(16);

    private Color[] fromBackground;
    private Color[] fromBorder;
    private Color[] fromLabel;

    private Coroutine transition;

    public event Action<string> CategorySelected;
    public event Action CreateRequested;

    private void OnDisable()
    {
        StopTransition();
    }

    public void Show(IReadOnlyList<string> categories, string selectedId, bool instant)
    {
        int categoryCount = categories != null ? categories.Count : 0;
        int count = categoryCount + 1;
        EnsureCapacity(count);

        shown.Clear();

        for (int i = 0; i < categoryCount; i++)
        {
            string category = categories[i];
            shown.Add(category);

            OptionButton chip = ChipAt(i);
            chip.Label.text = HabitCategories.DisplayName(category);
            chip.SetSelected(string.Equals(category, selectedId, StringComparison.Ordinal), instant);
            chip.gameObject.SetActive(true);
        }

        OptionButton create = ChipAt(categoryCount);
        shown.Add(null);
        create.Label.text = Loc.Tr("category_picker.create_chip_label");
        create.SetSelected(false, instant);
        create.gameObject.SetActive(true);

        for (int i = count; i < chips.Count; i++)
        {
            chips[i].gameObject.SetActive(false);
        }

        if (instant || !isActiveAndEnabled || transitionSeconds <= 0f)
        {
            StopTransition();
            return;
        }

        // Yeni gecis eskisini KESIYOR: yarim kalmis bir yumusatmanin ustune
        // binmek ciplerin arada bir yerde kalmasina yol acardi.
        StopTransition();
        transition = StartCoroutine(Animate(count));
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
        if (index < 0 || index >= shown.Count)
        {
            return;
        }

        string category = shown[index];
        if (category == null)
        {
            CreateRequested?.Invoke();
        }
        else
        {
            CategorySelected?.Invoke(category);
        }
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
