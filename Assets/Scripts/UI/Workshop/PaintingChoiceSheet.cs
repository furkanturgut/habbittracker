using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Biten bir tablonun yerine SIRADAKINI secen alttan acilan kutu.
//
// HabitSheet'in kalibini izliyor (scrim + CanvasGroup + alttan kayma, pasif
// kurulan kutu icin Awake degil EnsureWired, kapanista navigator kilidini en
// SONDA acma) ama kaydirma kodu BILEREK paylasilmiyor: HabitSheet'in
// anchoredPosition'i MaxHeightFitter ile ortak bir regresyon gecmisine sahip
// (bkz. CLAUDE.md), o dosyaya dokunmamak bilincli -- DayChipRow /
// PaintingPickerRow / MoodRow'un izledigi "ayni kalip, ayri uygulama" notunun
// bu dosyadaki karsiligi.
[DisallowMultipleComponent]
public class PaintingChoiceSheet : MonoBehaviour
{
    [SerializeField] private HabbitManager manager;

    [Tooltip("Acikken ressamin dokunmasi kapatiliyor.")]
    [SerializeField] private ScreenNavigator navigator;

    [Header("Kutu")]
    [SerializeField] private CanvasGroup scrimGroup;
    [SerializeField] private Button scrimButton;
    [SerializeField] private RectTransform panel;
    [SerializeField] private float slideSeconds = 0.28f;

    [Header("Ust bar")]
    [SerializeField] private TMP_Text sheetTitle;
    [SerializeField] private TMP_Text subtitle;
    [SerializeField] private Button cancelButton;

    [Header("Secim")]
    [SerializeField] private PaintingPickerRow paintingRow;

    [Header("Alt bar")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Image confirmBackground;

    [SerializeField] private UiTheme theme;

    private readonly List<PaintingDefinition> candidates = new List<PaintingDefinition>(32);

    private string habitId;
    private int canvasSize;
    private string selectedId;
    private bool hasSelection;

    private Coroutine slideRoutine;
    private Vector2 openPosition;
    private bool openPositionCached;
    private bool closing;
    private bool wired;

    // habitId, secilen tablonun id'si ("" = Rastgele).
    public event Action<string, string> PaintingChosen;

    public void Open(HabitData habit)
    {
        if (habit == null)
        {
            return;
        }

        EnsureWired();

        habitId = habit.id;
        canvasSize = habit.canvasSize;
        selectedId = null;
        hasSelection = false;

        if (manager != null)
        {
            PaintingChoices.Collect(manager.Library, habit, candidates);
        }
        else
        {
            candidates.Clear();
        }

        if (paintingRow != null)
        {
            paintingRow.Show(candidates, canvasSize, selectedId, instant: true);
        }

        if (sheetTitle != null)
        {
            sheetTitle.text = Loc.Tr("painting_choice.title");
        }

        if (subtitle != null)
        {
            subtitle.text = habit.title;
        }

        RefreshConfirmButton();

        if (!openPositionCached)
        {
            openPosition = panel.anchoredPosition;
            openPositionCached = true;
        }

        closing = false;
        gameObject.SetActive(true);

        if (navigator != null)
        {
            navigator.SetModalOpen(true);
        }

        StartSlide();
    }

    // Close()'in aynasi HabitSheet.Close ile ayni gerekceyle: kilit, animasyon
    // BITIMINDE aciliyor, SIRASINDA degil.
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

        if (paintingRow != null)
        {
            paintingRow.PaintingSelected += HandlePaintingSelected;
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

    private void HandlePaintingSelected(string paintingId)
    {
        selectedId = paintingId;
        hasSelection = true;

        if (paintingRow != null)
        {
            paintingRow.Show(candidates, canvasSize, selectedId, instant: false);
        }

        RefreshConfirmButton();
    }

    // "Rastgele" karesinin id'si "" ve bu GECERLI bir secim -- selectedId == null
    // (hic secim yok) ile ayirmak icin ayri bir bayrak gerekiyor.
    private void RefreshConfirmButton()
    {
        if (confirmButton != null)
        {
            confirmButton.interactable = hasSelection;
        }

        if (confirmBackground != null)
        {
            confirmBackground.color = hasSelection ? Color.white : theme.goldDisabled;
        }
    }

    private void Confirm()
    {
        if (!hasSelection)
        {
            return;
        }

        PaintingChosen?.Invoke(habitId, selectedId);
        Close();
    }

    // -----------------------------------------------------------------------
    // Animasyon -- HabitSheet.Slide/CloseSlide ile ayni maths, bilerek ayri kopya.
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

        if (navigator != null)
        {
            navigator.SetModalOpen(false);
        }

        gameObject.SetActive(false);
    }
}
