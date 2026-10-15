using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Ayarlar > "Kategoriler ve filtreler": ana sayfanin filtre satirinda hangi
// filtrelerin gorunecegini secme + kullanicinin kendi kategorilerini
// yonetme (yeniden adlandirma, silme) -- UCRETSIZ bir ozellik, PremiumService'e
// hic dokunmuyor.
//
// ConfirmSheet/TimePickerSheet'in KARDESI (ayni scrim/kayma gorsel sablonu,
// ayni "Awake'te kur, panelHeight'i bir kez yakala" kalibi) ama kodu
// PAYLASMIYOR -- projenin "ayni kalip, ayri uygulama" notunun bu dosyadaki
// karsiligi. Silme YIKICI bir eylem oldugu icin burasi bunu kendi basina
// YAPMAZ: DeleteRequested'i firlatir, SettingsScreen kendi ConfirmSheet'ini
// acip onaylanirsa HabbitManager.RemoveCustomCategory'yi cagirir. O cagri
// SettingsChanged'i tetikler ve bu sayfa kendi aboneligiyle (HandleSettingsChanged)
// otomatik tazelenir -- geri bildirim icin elle bir "refresh" cagrisina
// gerek kalmiyor.
[DisallowMultipleComponent]
public class CategorySettingsSheet : MonoBehaviour
{
    [SerializeField] private HabbitManager manager;

    [Header("Kutu")]
    [SerializeField] private CanvasGroup scrimGroup;
    [SerializeField] private Button scrimButton;
    [SerializeField] private RectTransform panel;
    [SerializeField] private Button closeButton;
    [SerializeField] private float slideSeconds = 0.22f;

    [Header("Filtreler")]
    [SerializeField] private TMP_Text selectionCountLabel;
    [SerializeField] private OptionButton filterChipPrefab;
    [SerializeField] private Transform filterChipRoot;

    [Header("Ozel kategoriler")]
    [SerializeField] private CategoryCustomRow customRowPrefab;
    [SerializeField] private Transform customRowRoot;
    [SerializeField] private GameObject customEmptyHint;

    private readonly List<OptionButton> filterChips = new List<OptionButton>(16);
    private readonly List<string> filterCandidates = new List<string>(16);
    private readonly List<string> filterLabels = new List<string>(16);
    private readonly List<string> selectedTokens = new List<string>(HabitFilters.MaxSlots - 1);
    private readonly List<HabitFilters.HabitFilter> resolvedBuffer = new List<HabitFilters.HabitFilter>(HabitFilters.MaxSlots);
    private readonly List<string> categoryBuffer = new List<string>(16);

    private readonly List<CategoryCustomRow> customRows = new List<CategoryCustomRow>(HabitCategories.MaxCustom);

    private float panelHeight;
    private Coroutine routine;

    // Silme onayi bu sayfanin isi degil -- bkz. sinif basi yorum.
    public event Action<string> DeleteRequested;

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

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
            ButtonPressFeedback.EnsureOn(closeButton);
        }

        if (manager != null)
        {
            manager.SettingsChanged += HandleSettingsChanged;
        }
    }

    private void OnDestroy()
    {
        if (scrimButton != null)
        {
            scrimButton.onClick.RemoveListener(Close);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }

        if (manager != null)
        {
            manager.SettingsChanged -= HandleSettingsChanged;
        }

        StopRoutine();
    }

    public void Open()
    {
        if (manager == null)
        {
            return;
        }

        RefreshAll();

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

    // Bu sayfa acikken baska bir yoldan (kendi rename cagrisi, ya da
    // SettingsScreen'in onaylanmis silme cagrisi) ozel kategoriler/filtreler
    // degisirse buradan otomatik tazelenir.
    private void HandleSettingsChanged()
    {
        if (!gameObject.activeSelf)
        {
            return;
        }

        RefreshAll();
    }

    // Bu sayfadaki cipler HabitSheet/DayChipRow gibi ayri bir "surucu"
    // coroutine'e sahip degil -- OptionButton.SetSelected(..., instant:false)
    // sadece hedef rengi hesaplar, ApplyVisual'i cagirmaz, bu yuzden boyle bir
    // surucu olmadan secim GORSEL olarak hic guncellenmezdi (veri dogru
    // kaydedilse bile). Bu yuzden burada hep aninda uygulanir.
    private void RefreshAll()
    {
        LoadSelection();
        RefreshFilterChips();
        RefreshCustomRows();
    }

    private void LoadSelection()
    {
        HabitFilters.Collect(manager.Settings.filterTokens, resolvedBuffer);

        selectedTokens.Clear();
        for (int i = 0; i < resolvedBuffer.Count; i++)
        {
            if (resolvedBuffer[i].Kind == HabitFilters.Kind.All)
            {
                continue;
            }

            selectedTokens.Add(HabitFilters.Token(resolvedBuffer[i]));
        }
    }

    private void RefreshFilterChips()
    {
        filterCandidates.Clear();
        filterLabels.Clear();

        filterCandidates.Add("@build");
        filterLabels.Add(Loc.Tr("habit_type.build"));
        filterCandidates.Add("@quit");
        filterLabels.Add(Loc.Tr("habit_type.quit"));

        HabitCategories.Collect(manager.Settings.customCategories, categoryBuffer);
        for (int i = 0; i < categoryBuffer.Count; i++)
        {
            filterCandidates.Add(categoryBuffer[i]);
            filterLabels.Add(HabitCategories.DisplayName(categoryBuffer[i]));
        }

        // Secili sayisi tavana (MaxSlots - 1, Tumu haric) ulastiginda secili
        // OLMAYAN cipler pasiflesir -- kullanicinin "fazla secilmesin"
        // kisitini kaydetmeden ONCE, burada gorunur kiliyoruz.
        bool atCap = selectedTokens.Count >= HabitFilters.MaxSlots - 1;

        for (int i = 0; i < filterCandidates.Count; i++)
        {
            OptionButton chip = FilterChipAt(i);
            chip.Label.text = filterLabels[i];

            bool selected = ListLookup.ContainsOrdinal(selectedTokens, filterCandidates[i]);
            chip.SetSelected(selected, instant: true);
            chip.Button.interactable = selected || !atCap;
            chip.gameObject.SetActive(true);
        }

        for (int i = filterCandidates.Count; i < filterChips.Count; i++)
        {
            filterChips[i].gameObject.SetActive(false);
        }

        if (selectionCountLabel != null)
        {
            selectionCountLabel.text = selectedTokens.Count + "/" + (HabitFilters.MaxSlots - 1);
        }
    }

    private OptionButton FilterChipAt(int index)
    {
        while (filterChips.Count <= index)
        {
            OptionButton created = Instantiate(filterChipPrefab, filterChipRoot);

            // Dongu degiskenini yakalamak yerine yerel kopya: C# 5'te gecerli
            // olan tuzak surumden bagimsiz okunur kalsin. capturedIndex havuz
            // SIRASI -- RefreshFilterChips her cagrida ayni sirayla yazdigi
            // icin filterCandidates'teki ayni indeksle daima eslesir.
            int capturedIndex = filterChips.Count;
            created.Button.onClick.AddListener(() => ToggleFilter(capturedIndex));
            filterChips.Add(created);
        }

        return filterChips[index];
    }

    private void ToggleFilter(int index)
    {
        if (index < 0 || index >= filterCandidates.Count)
        {
            return;
        }

        string token = filterCandidates[index];
        int existing = selectedTokens.FindIndex(value => string.Equals(value, token, StringComparison.Ordinal));

        if (existing >= 0)
        {
            selectedTokens.RemoveAt(existing);
        }
        else
        {
            if (selectedTokens.Count >= HabitFilters.MaxSlots - 1)
            {
                return;
            }

            selectedTokens.Add(token);
        }

        PushFilterTokens();
        RefreshFilterChips();
    }

    private void PushFilterTokens()
    {
        List<string> tokens = new List<string>(selectedTokens.Count + 1) { "@all" };
        tokens.AddRange(selectedTokens);
        manager.SetFilterTokens(tokens);
    }

    private void RefreshCustomRows()
    {
        List<string> custom = manager.Settings.customCategories;

        for (int i = 0; i < custom.Count; i++)
        {
            CategoryCustomRow row = CustomRowAt(i);
            row.Show(custom[i]);
            row.gameObject.SetActive(true);
        }

        for (int i = custom.Count; i < customRows.Count; i++)
        {
            customRows[i].gameObject.SetActive(false);
        }

        if (customEmptyHint != null)
        {
            customEmptyHint.SetActive(custom.Count == 0);
        }
    }

    private CategoryCustomRow CustomRowAt(int index)
    {
        while (customRows.Count <= index)
        {
            CategoryCustomRow created = Instantiate(customRowPrefab, customRowRoot);
            created.Renamed += HandleCustomRenamed;
            created.DeleteRequested += HandleCustomDeleteRequested;
            customRows.Add(created);
        }

        return customRows[index];
    }

    private void HandleCustomRenamed(string oldName, string newName)
    {
        manager.RenameCustomCategory(oldName, newName);

        // Basarisiz (gecersiz/cakisan ad) olsa da satir eski adiyla geri
        // gelir -- RefreshAll zaten kayittaki GERCEK listeyi okuyor.
        RefreshAll();
    }

    private void HandleCustomDeleteRequested(string category)
    {
        DeleteRequested?.Invoke(category);
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

    // -----------------------------------------------------------------------
    // Animasyon -- ConfirmSheet.Slide/CloseSlide ile ayni maths, bilerek ayri
    // kopya.
    // -----------------------------------------------------------------------

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
