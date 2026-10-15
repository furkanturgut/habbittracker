using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Formdaki "Kategori" satirini acan alttan kayan secici.
//
// PaintingChoiceSheet'in kalibini izliyor (scrim + CanvasGroup + alttan kayma,
// pasif kurulan kutu icin Awake degil EnsureWired, kapanista navigator
// kilidini en SONDA acma) ama kaydirma kodu BILEREK paylasilmiyor -- DayChipRow
// / PaintingPickerRow / TimePickerSheet'in izledigi "ayni kalip, ayri
// uygulama" notunun bu dosyadaki karsiligi (bkz. CLAUDE.md).
[DisallowMultipleComponent]
public class CategoryPickerSheet : MonoBehaviour
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
    [SerializeField] private Button cancelButton;

    [Header("Secim")]
    [SerializeField] private CategoryChipRow chipRow;

    [Header("Yeni kategori")]
    [SerializeField] private GameObject createRow;
    [SerializeField] private TMP_InputField createInput;
    [SerializeField] private Button createConfirmButton;
    [SerializeField] private TMP_Text createHint;

    [Header("Alt bar")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Image confirmBackground;

    [SerializeField] private UiTheme theme;

    private readonly List<string> candidates = new List<string>(16);

    private string selectedId;
    private bool hasSelection;

    private Coroutine slideRoutine;
    private Vector2 openPosition;
    private bool openPositionCached;
    private bool closing;
    private bool wired;

    public event Action<string> CategoryChosen;

    public void Open(string currentCategory)
    {
        EnsureWired();

        selectedId = currentCategory;
        hasSelection = !string.IsNullOrEmpty(currentCategory);

        RefreshCandidates(instant: true);
        CloseCreateRow();
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

    // Close()'in aynasi PaintingChoiceSheet.Close ile ayni gerekceyle: kilit,
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
            chipRow.CategorySelected += HandleCategorySelected;
            chipRow.CreateRequested += HandleCreateRequested;
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

        if (createConfirmButton != null)
        {
            createConfirmButton.onClick.AddListener(HandleCreateConfirmed);
            ButtonPressFeedback.EnsureOn(createConfirmButton);
        }

        if (createInput != null)
        {
            createInput.onSubmit.AddListener(HandleCreateSubmit);
        }
    }

    private void OnDisable()
    {
        slideRoutine = null;
        closing = false;
    }

    private void RefreshCandidates(bool instant)
    {
        if (manager != null)
        {
            HabitCategories.Collect(manager.Settings.customCategories, candidates);
        }
        else
        {
            candidates.Clear();
        }

        if (chipRow != null)
        {
            chipRow.Show(candidates, selectedId, instant);
        }
    }

    private void HandleCategorySelected(string category)
    {
        selectedId = category;
        hasSelection = true;

        RefreshCandidates(instant: false);
        RefreshConfirmButton();
    }

    private void HandleCreateRequested()
    {
        if (createRow != null)
        {
            createRow.SetActive(true);
        }

        if (createInput != null)
        {
            createInput.SetTextWithoutNotify(string.Empty);
            createInput.ActivateInputField();
        }

        if (createHint != null)
        {
            createHint.text = string.Empty;
        }
    }

    private void CloseCreateRow()
    {
        if (createRow != null)
        {
            createRow.SetActive(false);
        }
    }

    private void HandleCreateSubmit(string value)
    {
        HandleCreateConfirmed();
    }

    // Ikinci bir ic ice sheet acilmiyor: "+ Kategori olustur" cipi ayni
    // kutunun icinde gizli bir satiri acip kapatiyor.
    private void HandleCreateConfirmed()
    {
        if (manager == null || createInput == null)
        {
            return;
        }

        string created = manager.AddCustomCategory(createInput.text);
        if (created.Length == 0)
        {
            if (createHint != null)
            {
                createHint.text = Loc.Tr("category_picker.name_unavailable");
            }

            return;
        }

        CloseCreateRow();
        selectedId = created;
        hasSelection = true;

        RefreshCandidates(instant: false);
        RefreshConfirmButton();
    }

    // "Rastgele" ile ayni gerekce yok burada ama hasSelection yine ayri bir
    // bayrak: PaintingChoiceSheet'teki ile ayni sekilcilik, iki kutunun
    // RefreshConfirmButton'u birbirine benzesin.
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

        CategoryChosen?.Invoke(selectedId);
        Close();
    }

    // -----------------------------------------------------------------------
    // Animasyon -- PaintingChoiceSheet.Slide/CloseSlide ile ayni maths,
    // bilerek ayri kopya.
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
