using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 02 Aliskanliklar listesinin sonundaki "Arsiv (N)" satirinin actigi alttan
// kayan sayfa: arsivdeki her aliskanlik icin bir satir, "Geri al" butonu.
//
// CategoryPickerSheet'in kalibini izliyor (scrim + CanvasGroup + alttan
// kayma, pasif kurulan kutu icin Awake degil EnsureWired, kapaniste navigator
// kilidini en SONDA acma) ama kaydirma kodu BILEREK paylasilmiyor -- projenin
// "ayni kalip, ayri uygulama" notunun bu dosyadaki karsiligi.
//
// Silme bu sayfanin isi DEGIL: ArchiveRow.Clicked -> EditRequested ->
// HabitsScreen.HandleCardClicked -> HabitSheet.OpenForEdit. HabitSheet'in
// alt bari zaten [Sil][Geri al][Kaydet] -- silme oradan geciyor, ikinci bir
// onay sayfasi eklemiyoruz (HabitSheet.Delete() de onay sormuyor).
[DisallowMultipleComponent]
public class ArchiveSheet : MonoBehaviour
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
    [SerializeField] private Button closeButton;

    [Header("Liste")]
    [SerializeField] private ArchiveRow rowPrefab;
    [SerializeField] private Transform rowRoot;
    [SerializeField] private GameObject emptyHint;

    private readonly List<ArchiveRow> rows = new List<ArchiveRow>(8);
    private readonly List<HabitData> archivedBuffer = new List<HabitData>(8);

    private Coroutine slideRoutine;
    private Vector2 openPosition;
    private bool openPositionCached;
    private bool closing;
    private bool wired;

    // HabitsScreen hem "Geri al" hem de bir satirin govdesine dokunulmasini
    // buradan dinler; ikisi de duzenleme sayfasini acar (ayni HabitSheet,
    // orada [Sil][Geri al][Kaydet] zaten var).
    public event Action<string> EditRequested;

    public void Open()
    {
        EnsureWired();
        RefreshRows();

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

    // Close()'in aynasi: kilit animasyon BITIMINDE aciliyor, SIRASINDA degil.
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

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
            ButtonPressFeedback.EnsureOn(closeButton);
        }

        if (scrimButton != null)
        {
            scrimButton.onClick.AddListener(Close);
        }
    }

    private void OnEnable()
    {
        if (manager != null)
        {
            manager.HabitListChanged += HandleHabitListChanged;
        }
    }

    private void OnDisable()
    {
        if (manager != null)
        {
            manager.HabitListChanged -= HandleHabitListChanged;
        }

        slideRoutine = null;
        closing = false;
    }

    // Sayfa acikken baska bir yoldan (ornegin kendi satirindan "Geri al")
    // arsiv listesi degisirse buradan tazelenir.
    private void HandleHabitListChanged()
    {
        RefreshRows();
    }

    private void RefreshRows()
    {
        archivedBuffer.Clear();

        if (manager != null)
        {
            IReadOnlyList<HabitData> habits = manager.Habits;
            for (int i = 0; i < habits.Count; i++)
            {
                if (habits[i].isArchived)
                {
                    archivedBuffer.Add(habits[i]);
                }
            }
        }

        for (int i = 0; i < archivedBuffer.Count; i++)
        {
            ArchiveRow row = RowAt(i);
            row.Show(archivedBuffer[i]);
            row.gameObject.SetActive(true);
        }

        for (int i = archivedBuffer.Count; i < rows.Count; i++)
        {
            rows[i].gameObject.SetActive(false);
        }

        if (emptyHint != null)
        {
            emptyHint.SetActive(archivedBuffer.Count == 0);
        }

        // Liste bosaldi (son aliskanlik geri alindi/silindi): sayfa acik
        // kalmasin.
        if (archivedBuffer.Count == 0 && gameObject.activeSelf && !closing)
        {
            Close();
        }
    }

    private ArchiveRow RowAt(int index)
    {
        while (rows.Count <= index)
        {
            ArchiveRow created = Instantiate(rowPrefab, rowRoot);
            created.Clicked += HandleRowClicked;
            created.UnarchiveRequested += HandleUnarchiveRequested;
            rows.Add(created);
        }

        return rows[index];
    }

    private void HandleRowClicked(string habitId)
    {
        EditRequested?.Invoke(habitId);
    }

    private void HandleUnarchiveRequested(string habitId)
    {
        if (manager != null)
        {
            manager.SetArchived(habitId, false);
        }
    }

    // -----------------------------------------------------------------------
    // Animasyon -- CategoryPickerSheet.Slide/CloseSlide ile ayni maths,
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
