using System;
using System.Collections;
using UnityEngine;

// Formdaki tablo izgarasi: bir "Rastgele" karesi + secili tuval boyutundaki
// butun tablolar.
//
// DayChipRow kalibinin aynisi: tile'lar bu bilesene ozel oldugu icin
// HabitSheet'in paylasimli AnimateOptions'ina katilamazlar (bkz. DayChipRow.cs
// basindaki gerekce) -- ustelik burada sayi kitaplik boyutuna ve tuval
// filtresine gore DEGISIYOR, yani sabit boyutlu bir diziye katilmalari zaten
// mumkun degil. Bu yuzden kendi "hedef + instant bayragi + tek coroutine"
// dongusunu tasiyor.
[DisallowMultipleComponent]
public class PaintingPickerRow : MonoBehaviour
{
    [SerializeField] private PaintingOption optionPrefab;
    [SerializeField] private Transform optionRoot;
    [SerializeField] private float transitionSeconds = 0.16f;

    // Ilk eleman hep Rastgele; sonrasi secili boyuttaki tablolar. Liste
    // HabitCategorySection.CardAt kalibiyla havuzlaniyor: sayi her Show'da
    // degisebildigi icin GameObject yaratip yok etmek yerine var olanlar
    // yeniden baglaniyor.
    private readonly System.Collections.Generic.List<PaintingOption> options =
        new System.Collections.Generic.List<PaintingOption>(8);

    private Color[] fromBackground;
    private Color[] fromBorder;
    private Color[] fromLabel;

    private Coroutine transition;

    // "" = Rastgele.
    public event Action<string> PaintingSelected;

    private void OnDisable()
    {
        StopTransition();
    }

    public void Show(PaintingLibrary library, int canvasSize, string selectedId, bool instant)
    {
        System.Collections.Generic.IReadOnlyList<PaintingDefinition> paintings =
            library != null ? library.ForSize(canvasSize) : null;

        Show(paintings, canvasSize, selectedId, instant);
    }

    // Onceden filtrelenmis bir liste cizer (ornegin galeride olmayanlar) --
    // "hangi tablolar" karari artik bu satirin isi degil, yalnizca verilen
    // listeyi gosteriyor. Bkz. PaintingChoices.Collect.
    public void Show(
        System.Collections.Generic.IReadOnlyList<PaintingDefinition> paintings,
        int canvasSize,
        string selectedId,
        bool instant)
    {
        int count = 1 + (paintings != null ? paintings.Count : 0);
        EnsureCapacity(count);

        PaintingOption random = OptionAt(0);
        random.ShowRandom(canvasSize);
        random.gameObject.SetActive(true);

        if (paintings != null)
        {
            for (int i = 0; i < paintings.Count; i++)
            {
                PaintingOption option = OptionAt(1 + i);
                option.ShowPainting(paintings[i]);
                option.gameObject.SetActive(true);
            }
        }

        for (int i = count; i < options.Count; i++)
        {
            options[i].gameObject.SetActive(false);
        }

        for (int i = 0; i < count; i++)
        {
            PaintingOption option = options[i];
            option.SetSelected(string.Equals(option.PaintingId, selectedId, StringComparison.Ordinal), instant);
        }

        if (instant || !isActiveAndEnabled || transitionSeconds <= 0f)
        {
            StopTransition();
            return;
        }

        // Yeni gecis eskisini KESIYOR: yarim kalmis bir yumusatmanin ustune
        // binmek karelerin arada bir yerde kalmasina yol acardi.
        StopTransition();
        transition = StartCoroutine(Animate(count));
    }

    private PaintingOption OptionAt(int index)
    {
        while (options.Count <= index)
        {
            PaintingOption created = Instantiate(optionPrefab, optionRoot);
            int capturedIndex = options.Count;
            created.Option.Button.onClick.AddListener(() => RaiseSelected(capturedIndex));
            options.Add(created);
        }

        return options[index];
    }

    private void RaiseSelected(int index)
    {
        if (index < 0 || index >= options.Count)
        {
            return;
        }

        PaintingSelected?.Invoke(options[index].PaintingId);
    }

    // from* dizileri tile sayisi ARTTIGINDA yeniden ayriliyor; kuculdugunde
    // dokunulmuyor cunku Animate zaten sadece gecerli count kadarini okuyor.
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
            OptionButton option = options[i].Option;
            fromBackground[i] = option.CurrentBackground;
            fromBorder[i] = option.CurrentBorder;
            fromLabel[i] = option.CurrentLabelColor;
        }

        float elapsed = 0f;

        while (elapsed < transitionSeconds)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / transitionSeconds);
            float eased = Easing.OutCubic(t);

            for (int i = 0; i < count; i++)
            {
                OptionButton option = options[i].Option;
                option.ApplyVisual(
                    Color.Lerp(fromBackground[i], option.TargetBackground, eased),
                    Color.Lerp(fromBorder[i], option.TargetBorder, eased),
                    Color.Lerp(fromLabel[i], option.TargetLabelColor, eased));
            }

            yield return null;
        }

        // Son kare tam hedefte kapansin: Lerp'in yuvarlamasi kalmasin.
        for (int i = 0; i < count; i++)
        {
            OptionButton option = options[i].Option;
            option.ApplyVisual(option.TargetBackground, option.TargetBorder, option.TargetLabelColor);
        }

        transition = null;
    }
}
