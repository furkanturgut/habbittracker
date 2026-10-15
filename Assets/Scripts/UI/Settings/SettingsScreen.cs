using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Sagdan kayarak acilan tam ekran Ayarlar paneli.
//
// `UI Root`'un UC PANELDEN SONRAKI cocugu (hepsinin USTUNE ciziliyor);
// `ScreenNavigator.screens` dizisine GIRMIYOR -- bir sekme degil, ucu
// ekrandan de acilabilen bagimsiz bir katman. `navigator.SetModalOpen`
// acilista HEMEN, kapanista animasyon BITIMINDE cagrilir -- HabitSheet/
// PaintingChoiceSheet'in "bir kez kilitle" kuraliyla AYNI.
//
// HabitSheet'in kayma/MaxHeightFitter kodunu PAYLASMIYOR (PaintingChoiceSheet
// icin verilen ayni karar -- gorsel desen benziyor ama eksen ve sahiplik
// farkli: bu panel YATAYDA kayiyor, DIKEY yuksekligi her zaman tam ekran).
// Disa/ice aktarma akislari SettingsScreen.Data.cs'de -- ayni dosya butcesi
// gerekcesi (HabbitManager.Rotus.cs / .Archive.cs / .Import.cs kardesligi).
[DisallowMultipleComponent]
public partial class SettingsScreen : MonoBehaviour
{
    [SerializeField] private HabbitManager manager;
    [SerializeField] private ScreenNavigator navigator;
    [SerializeField] private RectTransform panel;
    [SerializeField] private Button closeButton;

    [SerializeField] private PremiumCard premiumCard;

    [SerializeField] private TMP_InputField nameInput;

    [SerializeField] private SettingsToggleRow reminderToggle;
    [SerializeField] private GameObject timeRow;
    [SerializeField] private Button timeButton;
    [SerializeField] private TMP_Text timeValueLabel;
    [SerializeField] private TimePickerSheet timePicker;

    [SerializeField] private SettingsToggleRow soundToggle;
    [SerializeField] private SettingsToggleRow hapticToggle;

    [SerializeField] private SettingsSegmentRow weekStartRow;

    [SerializeField] private Button languageButton;
    [SerializeField] private TMP_Text languageValueLabel;
    [SerializeField] private LanguagePickerSheet languagePicker;

    [SerializeField] private Button categoriesButton;
    [SerializeField] private CategorySettingsSheet categorySheet;

    [SerializeField] private Button exportButton;
    [SerializeField] private GameObject exportPremiumBadge;
    [SerializeField] private Button importButton;
    [SerializeField] private Button resetButton;

    [SerializeField] private ConfirmSheet confirmSheet;
    [SerializeField] private TMP_Text versionLabel;

    [Tooltip("Kayma suresi (sn). Egri Easing.OutEmphasized -- teslim dosyasinin\ncubic-bezier(.2,.8,.2,1) EGRISININ KENDISI, OutCubic yaklasikligi degil.")]
    [SerializeField] private float slideSeconds = 0.28f;

    private Coroutine transition;
    private bool opening;

    // Izin diyalogu acikken toggle'a ikinci bir dokunus gelmesin -- OS
    // diyalogu kapanana kadar RequestReminderPermission'in geri cagrisi
    // gelmez, bu arada toggle tekrar tetiklenirse iki akis cakisir.
    private bool reminderPermissionPending;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }

        if (nameInput != null)
        {
            nameInput.onEndEdit.AddListener(HandleNameChanged);
        }

        if (reminderToggle != null)
        {
            reminderToggle.Toggled += HandleReminderToggled;
        }

        if (timeButton != null)
        {
            timeButton.onClick.AddListener(HandleTimeRowTapped);
        }

        if (timePicker != null)
        {
            timePicker.Picked += HandleTimePicked;
        }

        if (soundToggle != null)
        {
            soundToggle.Toggled += HandleSoundToggled;
        }

        if (hapticToggle != null)
        {
            hapticToggle.Toggled += HandleHapticToggled;
        }

        if (weekStartRow != null)
        {
            weekStartRow.Picked += HandleWeekStartPicked;
        }

        if (languageButton != null)
        {
            languageButton.onClick.AddListener(HandleLanguageRowTapped);
        }

        if (languagePicker != null)
        {
            languagePicker.LanguageChosen += HandleLanguageChosen;
        }

        if (categoriesButton != null)
        {
            categoriesButton.onClick.AddListener(HandleCategoriesTapped);
        }

        if (categorySheet != null)
        {
            categorySheet.DeleteRequested += HandleCategoryDeleteRequested;
        }

        if (premiumCard != null)
        {
            premiumCard.Purchase += HandlePurchase;
        }

        if (exportButton != null)
        {
            exportButton.onClick.AddListener(HandleExport);
        }

        if (importButton != null)
        {
            importButton.onClick.AddListener(HandleImport);
        }

        if (resetButton != null)
        {
            resetButton.onClick.AddListener(HandleResetRequested);
        }

        if (versionLabel != null)
        {
            versionLabel.text = string.Format(Loc.Tr("settings.version_label"), Application.version);
        }

        if (panel != null)
        {
            // Acilista ekranin disinda, sagda -- Open() ilk cagrildiginda
            // Slide() ayni konumdan baslayabilsin.
            panel.anchoredPosition = new Vector2(LayoutMetrics.DesignWidth, 0f);
        }
    }

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }

        if (nameInput != null)
        {
            nameInput.onEndEdit.RemoveListener(HandleNameChanged);
        }

        if (reminderToggle != null)
        {
            reminderToggle.Toggled -= HandleReminderToggled;
        }

        if (timeButton != null)
        {
            timeButton.onClick.RemoveListener(HandleTimeRowTapped);
        }

        if (timePicker != null)
        {
            timePicker.Picked -= HandleTimePicked;
        }

        if (soundToggle != null)
        {
            soundToggle.Toggled -= HandleSoundToggled;
        }

        if (hapticToggle != null)
        {
            hapticToggle.Toggled -= HandleHapticToggled;
        }

        if (weekStartRow != null)
        {
            weekStartRow.Picked -= HandleWeekStartPicked;
        }

        if (languageButton != null)
        {
            languageButton.onClick.RemoveListener(HandleLanguageRowTapped);
        }

        if (languagePicker != null)
        {
            languagePicker.LanguageChosen -= HandleLanguageChosen;
        }

        if (categoriesButton != null)
        {
            categoriesButton.onClick.RemoveListener(HandleCategoriesTapped);
        }

        if (categorySheet != null)
        {
            categorySheet.DeleteRequested -= HandleCategoryDeleteRequested;
        }

        if (premiumCard != null)
        {
            premiumCard.Purchase -= HandlePurchase;
        }

        if (exportButton != null)
        {
            exportButton.onClick.RemoveListener(HandleExport);
        }

        if (importButton != null)
        {
            importButton.onClick.RemoveListener(HandleImport);
        }

        if (resetButton != null)
        {
            resetButton.onClick.RemoveListener(HandleResetRequested);
        }

        StopTransition();
    }

    public void Open()
    {
        if (manager == null)
        {
            return;
        }

        gameObject.SetActive(true);

        // Hemen kilitle: HabitSheet.Open ile AYNI zamanlama -- acik oldugu
        // surece ressam dokunmaya tepki vermemeli.
        if (navigator != null)
        {
            navigator.SetModalOpen(true);
        }

        RefreshAll();

        opening = true;
        StopTransition();
        transition = StartCoroutine(Slide());
    }

    public void Close()
    {
        if (!gameObject.activeSelf || !opening)
        {
            return;
        }

        if (categorySheet != null)
        {
            categorySheet.Close();
        }

        if (confirmSheet != null)
        {
            confirmSheet.Close();
        }

        if (timePicker != null)
        {
            timePicker.Close();
        }

        opening = false;
        StopTransition();
        transition = StartCoroutine(CloseSlide());
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

    private IEnumerator Slide()
    {
        float elapsed = 0f;

        while (elapsed < slideSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float eased = Easing.OutEmphasized(Mathf.Clamp01(elapsed / slideSeconds));

            if (panel != null)
            {
                panel.anchoredPosition = new Vector2(Mathf.Lerp(LayoutMetrics.DesignWidth, 0f, eased), 0f);
            }

            yield return null;
        }

        if (panel != null)
        {
            panel.anchoredPosition = Vector2.zero;
        }

        transition = null;
    }

    private IEnumerator CloseSlide()
    {
        float startX = panel != null ? panel.anchoredPosition.x : 0f;
        float elapsed = 0f;

        while (elapsed < slideSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float eased = Easing.OutEmphasized(Mathf.Clamp01(elapsed / slideSeconds));

            if (panel != null)
            {
                panel.anchoredPosition = new Vector2(Mathf.Lerp(startX, LayoutMetrics.DesignWidth, eased), 0f);
            }

            yield return null;
        }

        if (panel != null)
        {
            panel.anchoredPosition = new Vector2(LayoutMetrics.DesignWidth, 0f);
        }

        // Kapanis animasyonu BITTIKTEN sonra kilidi birak -- HabitSheet.
        // CloseSlide ile AYNI sira.
        if (navigator != null)
        {
            navigator.SetModalOpen(false);
        }

        gameObject.SetActive(false);
        transition = null;
    }

    // -----------------------------------------------------------------------
    // Tazeleme
    // -----------------------------------------------------------------------

    private void RefreshAll()
    {
        AppSettingsData settings = manager.Settings;

        if (premiumCard != null)
        {
            premiumCard.SetPremium(settings.isPremium);
        }

        if (exportPremiumBadge != null)
        {
            exportPremiumBadge.SetActive(!settings.isPremium);
        }

        if (nameInput != null)
        {
            nameInput.SetTextWithoutNotify(settings.painterName);
        }

        if (reminderToggle != null)
        {
            reminderToggle.SetOn(settings.reminderOn, true);
        }

        if (timeRow != null)
        {
            timeRow.SetActive(settings.reminderOn);
        }

        if (timeValueLabel != null)
        {
            timeValueLabel.text = settings.reminderTime;
        }

        if (soundToggle != null)
        {
            soundToggle.SetOn(settings.soundOn, true);
        }

        if (hapticToggle != null)
        {
            hapticToggle.SetOn(settings.hapticsOn, true);
        }

        if (weekStartRow != null)
        {
            weekStartRow.SetValue(settings.weekStart, true);
        }

        if (languageValueLabel != null)
        {
            languageValueLabel.text = AppLanguageNames.DisplayName(settings.language);
        }
    }

    // -----------------------------------------------------------------------
    // Olaylar -- her biri TEK bir HabbitManager.Setxxx cagirir, kayit her
    // zaman tek yerden degisir.
    // -----------------------------------------------------------------------

    private void HandleNameChanged(string value)
    {
        manager.SetPainterName(value);

        // Kirpma/varsayilan HabbitManager icinde oluyor; kullaniciya o
        // sonucu GERI yaziyoruz ki alan kendi yazdigindan farkli bir sey
        // gosteriyor olmasin (orn. 14 karakteri asan bir yapistirma).
        if (nameInput != null)
        {
            nameInput.SetTextWithoutNotify(manager.Settings.painterName);
        }
    }

    // Izin yalnizca toggle ACILDIGINDA istenir (REMINDER_PLAN.md karari).
    // Kapanista izin akisi yok: direkt SetReminder(false, ...) -> ortak
    // kuyruk (ApplySettingsChange) zaten ReminderService.Schedule'i cagirip
    // Cancel()'a esdeger hale getiriyor.
    private void HandleReminderToggled(bool on)
    {
        if (!on)
        {
            manager.SetReminder(false, manager.Settings.reminderTime);

            if (timeRow != null)
            {
                timeRow.SetActive(false);
            }

            return;
        }

        if (reminderPermissionPending)
        {
            return;
        }

        if (timeRow != null)
        {
            timeRow.SetActive(true);
        }

        reminderPermissionPending = true;
        manager.RequestReminderPermission(HandleReminderPermission);
    }

    // Panel kapanirken bu nesne SetActive(false) oluyor ama YOK EDILMIYOR --
    // yine de nesne asenkron geri cagri gelmeden once yok edilirse diye
    // korumali: HabbitManager.RequestReminderPermission coroutine'i KENDI
    // uzerinde kosturdugu icin (REMINDER_PLAN.md kritik duzeltme #2) bu
    // nesne yok olsa bile geri cagri calisir.
    private void HandleReminderPermission(bool granted)
    {
        reminderPermissionPending = false;

        if (this == null || manager == null)
        {
            return;
        }

        if (!granted)
        {
            if (reminderToggle != null)
            {
                reminderToggle.SetOn(false, false);
            }

            if (timeRow != null)
            {
                timeRow.SetActive(false);
            }

            manager.SetReminder(false, manager.Settings.reminderTime);
            return;
        }

        manager.SetReminder(true, manager.Settings.reminderTime);
    }

    private void HandleTimeRowTapped()
    {
        if (timePicker != null)
        {
            timePicker.Show(manager.Settings.reminderTime);
        }
    }

    private void HandleTimePicked(int hour, int minute)
    {
        manager.SetReminder(manager.Settings.reminderOn, TimeOfDay.Format(hour, minute));

        if (timeValueLabel != null)
        {
            timeValueLabel.text = manager.Settings.reminderTime;
        }
    }

    private void HandleSoundToggled(bool on)
    {
        manager.SetSoundOn(on);
    }

    private void HandleHapticToggled(bool on)
    {
        manager.SetHapticsOn(on);
    }

    private void HandleWeekStartPicked(WeekStart start)
    {
        manager.SetWeekStart(start);
    }

    private void HandleLanguageRowTapped()
    {
        if (languagePicker != null)
        {
            languagePicker.Open(manager.Settings.language);
        }
    }

    private void HandleLanguageChosen(AppLanguage language)
    {
        manager.SetLanguage(language);

        if (languageValueLabel != null)
        {
            languageValueLabel.text = AppLanguageNames.DisplayName(manager.Settings.language);
        }
    }

    private void HandleCategoriesTapped()
    {
        if (categorySheet != null)
        {
            categorySheet.Open();
        }
    }

    // Silme YIKICI bir eylem -- CategorySettingsSheet bunu kendi basina
    // yapmiyor, bu sayfanin ConfirmSheet'ini (zaten "Ilerlemeyi sifirla" icin
    // var olan ayni ornegi) kullanarak burada onayliyoruz.
    private void HandleCategoryDeleteRequested(string category)
    {
        if (confirmSheet == null)
        {
            manager.RemoveCustomCategory(category);
            return;
        }

        confirmSheet.Show(
            Loc.Tr("settings.delete_category_confirm_title"),
            string.Format(Loc.Tr("settings.delete_category_confirm_body"), category, HabitCategories.DisplayName(HabitCategories.Other)),
            Loc.Tr("common.delete_button"),
            () => manager.RemoveCustomCategory(category));
    }

    private void HandlePurchase()
    {
        PremiumService.Purchase(manager);
        RefreshAll();
    }

    private void HandleResetRequested()
    {
        if (confirmSheet == null)
        {
            manager.DeleteAllData();
            RefreshAll();
            return;
        }

        confirmSheet.Show(
            Loc.Tr("settings.reset_progress_title"),
            Loc.Tr("settings.reset_progress_confirm_body"),
            Loc.Tr("common.reset_button"),
            HandleResetConfirmed);
    }

    private void HandleResetConfirmed()
    {
        manager.DeleteAllData();
        RefreshAll();
    }
}
