using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Ekleme / duzenleme / silme icin alttan acilan form.
//
// Form durumu MonoBehaviour'da DEGIL, saf bir HabitForm nesnesinde: dogrulama ve
// gun donusumu sahne kurmadan test edilebilsin diye. Burasi yalnizca o durumu
// ekrana yansitip HabbitManager'a gonderiyor.
[DisallowMultipleComponent]
public class HabitSheet : MonoBehaviour
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
    [SerializeField] private Button cancelButton;

    [Header("Alanlar")]
    [SerializeField] private TMP_InputField titleInput;
    [SerializeField] private OptionButton buildOption;
    [SerializeField] private OptionButton quitOption;
    [SerializeField] private Button categoryButton;
    [SerializeField] private TMP_Text categoryValueLabel;
    [SerializeField] private CategoryPickerSheet categoryPicker;
    [SerializeField] private DayChipRow dayToggles;
    [SerializeField] private TMP_Text daysHint;
    [SerializeField] private CanvasSizeOption[] canvasOptions;
    [SerializeField] private GameObject paintingSectionRoot;
    [SerializeField] private PaintingPickerRow paintingRow;
    [SerializeField] private TMP_Text paintingHint;

    [Header("Bitis tarihi")]
    [SerializeField] private Button endDateToggle;
    [SerializeField] private Image endDateTrack;
    [SerializeField] private RectTransform endDateKnob;
    [SerializeField] private GameObject endDateRow;
    [SerializeField] private TMP_InputField endDateInput;
    [SerializeField] private float knobOnX = 23f;
    [SerializeField] private float knobOffX = 3f;
    [SerializeField] private float optionTransitionSeconds = 0.16f;

    [Header("Alt bar")]
    [SerializeField] private GameObject deleteRoot;
    [SerializeField] private Button deleteButton;
    [SerializeField] private GameObject archiveRoot;
    [SerializeField] private Button archiveButton;
    [SerializeField] private TMP_Text archiveLabel;
    [SerializeField] private Button saveButton;
    [SerializeField] private TMP_Text saveLabel;
    [SerializeField] private Image saveBackground;

    [SerializeField] private UiTheme theme;

    private readonly HabitForm form = new HabitForm();

    // Kayda gonderilen gun listesi: her kaydetmede yeni liste ayirmamak icin alan.
    private readonly List<DayOfWeek> frequencyBuffer = new List<DayOfWeek>(WeekDays.Count);

    private string editingId;

    // form.LoadFrom'un tasimadigi tek alan: arsivleme Save()'in bir parcasi
    // degil, Delete() gibi anlik bir eylem -- HabitForm'a yeni bir alan
    // eklemeye gerek yok.
    private bool editingArchived;
    private Coroutine slideRoutine;
    private Vector2 openPosition;
    private bool openPositionCached;
    private bool closing;

    // Tur/Kategori/Tuval kartlarinin hepsini TEK coroutine suruyor -- MoodRow
    // kaliginin ayni (daire basina degil satir basina bir coroutine). Dizi
    // EnsureWired'da bir kez kuruluyor, boyutu acilis boyunca sabit.
    private OptionButton[] animatedOptions;
    private Color[] optionFromBackground;
    private Color[] optionFromBorder;
    private Color[] optionFromLabel;
    private float endDateTargetKnobX;
    private Color endDateTargetTrackColor;
    private Coroutine optionTransition;

    // Kaydedildi ya da silindi; liste ekrani tazelensin.
    public event Action Submitted;

    private bool wired;

    public void OpenForNew()
    {
        editingId = null;
        editingArchived = false;
        form.Reset();
        Open();
    }

    public void OpenForEdit(HabitData habit)
    {
        editingId = habit.id;
        editingArchived = habit.isArchived;
        form.LoadFrom(habit);
        Open();
    }

    // Open()'in aynasi: panel asagi kayar, scrim alpha 0'a iner, animasyon
    // bitince navigator kilidi acilir ve SetActive(false) cagrilir. Kilidin
    // gecis SIRASINDA degil BITIMINDE acilmasi bilincli: kapanis surerken
    // ressam tiklamasi ekrani erken acmamali.
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

    // -----------------------------------------------------------------------
    // Baglantilar
    // -----------------------------------------------------------------------

    // Baglantilar Awake'te DEGIL burada kuruluyor: sheet sahnede PASIF basliyor
    // ve pasif bir nesnenin Awake'i hic calismaz. Ilk acilista bir kez kurulup
    // sonra atlaniyor.
    private void EnsureWired()
    {
        if (wired)
        {
            return;
        }

        wired = true;
        WireOptions();
        BuildAnimatedOptions();
    }

    // buildOption + quitOption + canvasOptions -- hepsi tek bir sahiplik
    // noktasindan (RefreshAll) degisiyor, o yuzden ara deger dizileri de
    // burada bir kez ayrilip her karede yeniden ayrilmiyor. Kategori artik bu
    // listede DEGIL: tek bir deger satiri (categoryValueLabel), alt popup
    // sheet'ten secilen tek bir metin, animasyonlu piller degil.
    private void BuildAnimatedOptions()
    {
        int count = 2 + canvasOptions.Length;
        animatedOptions = new OptionButton[count];

        int index = 0;
        animatedOptions[index++] = buildOption;
        animatedOptions[index++] = quitOption;

        for (int i = 0; i < canvasOptions.Length; i++)
        {
            animatedOptions[index++] = canvasOptions[i].Option;
        }

        optionFromBackground = new Color[count];
        optionFromBorder = new Color[count];
        optionFromLabel = new Color[count];
    }

    private void WireOptions()
    {
        if (titleInput != null)
        {
            titleInput.onValueChanged.AddListener(value =>
            {
                form.Title = value;
                RefreshSaveButton();
            });
        }

        if (endDateInput != null)
        {
            endDateInput.onValueChanged.AddListener(value =>
            {
                form.EndDate = value;
                RefreshSaveButton();
            });
        }

        Bind(buildOption, () => SetType(HabitTypes.Build));
        Bind(quitOption, () => SetType(HabitTypes.Quit));

        if (categoryButton != null)
        {
            categoryButton.onClick.AddListener(OpenCategoryPicker);
            ButtonPressFeedback.EnsureOn(categoryButton);
        }

        if (categoryPicker != null)
        {
            categoryPicker.CategoryChosen += SetCategory;
        }

        for (int i = 0; i < canvasOptions.Length && i < CanvasSizes.All.Length; i++)
        {
            int size = CanvasSizes.All[i];
            canvasOptions[i].Setup(size);
            Bind(canvasOptions[i].Option, () => SetCanvasSize(size));
        }

        if (paintingRow != null)
        {
            paintingRow.PaintingSelected += SetPainting;
        }

        if (dayToggles != null)
        {
            dayToggles.DayToggled += HandleDayToggled;
        }

        if (endDateToggle != null)
        {
            endDateToggle.onClick.AddListener(ToggleEndDate);
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

        if (saveButton != null)
        {
            saveButton.onClick.AddListener(Save);
            ButtonPressFeedback.EnsureOn(saveButton);
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.AddListener(Delete);
            ButtonPressFeedback.EnsureOn(deleteButton);
        }

        if (archiveButton != null)
        {
            archiveButton.onClick.AddListener(ToggleArchive);
            ButtonPressFeedback.EnsureOn(archiveButton);
        }

        // Tur kartlarinda secili baslik kendi renginde yaniyor; digerlerinde altin.
        buildOption.Configure(theme.buildColor, theme.textPrimary);
        quitOption.Configure(theme.quitColor, theme.textPrimary);
    }

    private static void Bind(OptionButton option, UnityEngine.Events.UnityAction action)
    {
        if (option != null)
        {
            option.Button.onClick.AddListener(action);
        }
    }

    // -----------------------------------------------------------------------
    // Durum -> ekran
    // -----------------------------------------------------------------------

    private void Open()
    {
        EnsureWired();

        // Ilk acilista panelin sahnedeki konumu zaten "acik" hedef konum --
        // bir kere onbelleklenip bundan sonra hep buradan okunuyor. Cunku
        // kapanis animasyonu panel.anchoredPosition'i ekran disina tasir;
        // onbellek olmasa bir sonraki Open() o ekran-disi konumu "hedef"
        // sanip paneli bir daha hic gostermezdi.
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

        bool editing = !string.IsNullOrEmpty(editingId);
        sheetTitle.text = Loc.Tr(editing ? "habit_sheet.title_edit" : "habit_sheet.title_new");
        saveLabel.text = Loc.Tr(editing ? "common.save_button" : "common.add_button");
        deleteRoot.SetActive(editing);

        if (archiveRoot != null)
        {
            archiveRoot.SetActive(editing);
        }

        if (archiveLabel != null)
        {
            archiveLabel.text = Loc.Tr(editingArchived ? "common.unarchive_button" : "common.archive_button");
        }

        // Tablo secimi yalnizca YENI aliskanlikta: duzenlemede tuval boyutu
        // degistirmek PaintingAssignment.Resize'in min(acilan, yeni) kuraliyla
        // ilerliyor, acik secim bu yolu bozardi.
        if (paintingSectionRoot != null)
        {
            paintingSectionRoot.SetActive(!editing);
        }

        // Metin alanlarini olay TETIKLEMEDEN dolduruyoruz: SetTextWithoutNotify
        // olmasa onValueChanged form.Title'i geri yazip dongu kurardi.
        titleInput.SetTextWithoutNotify(form.Title);
        endDateInput.SetTextWithoutNotify(form.EndDate);

        // Acilis her zaman aninda: sheet zaten ekran disindan kayarak geliyor,
        // kartlarin da o surede sifirdan renklenmesine gerek yok.
        RefreshAll(instant: true);
        StartSlide();
    }

    // instant=false -- yani kullanici formda bir secim degistirdiginde --
    // hedefler guncellenir ama gorsel degismez; tek surucu coroutine
    // (AnimateOptions) CurrentXxx'ten TargetXxx'e yumusak gecer. Open()
    // ise instant=true ile cagiriyor (bkz. yukarida).
    private void RefreshAll(bool instant)
    {
        buildOption.SetSelected(!IsQuitSelected(), instant);
        quitOption.SetSelected(IsQuitSelected(), instant);

        if (categoryValueLabel != null)
        {
            categoryValueLabel.text = HabitCategories.DisplayName(form.Category);
        }

        for (int i = 0; i < canvasOptions.Length; i++)
        {
            canvasOptions[i].SetSelected(canvasOptions[i].Size == form.CanvasSize, instant);
        }

        if (paintingRow != null)
        {
            paintingRow.Show(manager != null ? manager.Library : null, form.CanvasSize, form.PaintingId, instant);
        }

        if (paintingHint != null)
        {
            paintingHint.text = PaintingHint();
        }

        dayToggles.ShowForm(form, instant);
        daysHint.text = form.DaysHint();

        bool hasEndDate = form.HasEndDate;
        endDateTargetTrackColor = hasEndDate ? theme.gold : theme.borderSoft;
        endDateTargetKnobX = hasEndDate ? knobOnX : knobOffX;

        if (instant)
        {
            endDateTrack.color = endDateTargetTrackColor;
            Vector2 knob = endDateKnob.anchoredPosition;
            knob.x = endDateTargetKnobX;
            endDateKnob.anchoredPosition = knob;
        }

        endDateRow.SetActive(hasEndDate);

        RefreshSaveButton();

        if (instant)
        {
            StopOptionAnimation();
        }
        else
        {
            StartOptionAnimation();
        }
    }

    private void RefreshSaveButton()
    {
        bool valid = form.IsValid;
        saveButton.interactable = valid;
        saveBackground.color = valid ? Color.white : theme.goldDisabled;
    }

    private bool IsQuitSelected()
    {
        return string.Equals(form.HabitType, HabitTypes.Quit, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // Ekran -> durum
    // -----------------------------------------------------------------------

    private void SetType(string habitType)
    {
        form.HabitType = habitType;
        RefreshAll(instant: false);
    }

    private void SetCategory(string category)
    {
        form.Category = category;
        RefreshAll(instant: false);
    }

    private void OpenCategoryPicker()
    {
        if (categoryPicker != null)
        {
            categoryPicker.Open(form.Category);
        }
    }

    private void SetCanvasSize(int size)
    {
        form.SetCanvasSize(size);
        RefreshAll(instant: false);
    }

    private void SetPainting(string paintingId)
    {
        form.PaintingId = paintingId;
        RefreshAll(instant: false);
    }

    // daysHint kalibinin aynisi: bos secim "Rastgele" demek, dolu secim
    // kitaplikta hala cozulebiliyorsa tablonun adini gosterir.
    private string PaintingHint()
    {
        if (string.IsNullOrEmpty(form.PaintingId))
        {
            return Loc.Tr("painting.random_label");
        }

        if (manager != null && manager.Library != null
            && manager.Library.TryGet(form.PaintingId, out PaintingDefinition definition))
        {
            return definition.displayName;
        }

        return "Rastgele";
    }

    private void HandleDayToggled(int mondayIndex)
    {
        form.ToggleDay(mondayIndex);
        RefreshAll(instant: false);
    }

    private void ToggleEndDate()
    {
        form.HasEndDate = !form.HasEndDate;
        RefreshAll(instant: false);
    }

    // -----------------------------------------------------------------------
    // Gonderim
    // -----------------------------------------------------------------------

    private void Save()
    {
        if (manager == null || !form.IsValid)
        {
            return;
        }

        form.FillFrequency(frequencyBuffer);
        string endDate = form.HasEndDate ? form.EndDate : null;

        if (string.IsNullOrEmpty(editingId))
        {
            manager.CreateHabit(
                form.TrimmedTitle,
                form.Category,
                form.HabitType,
                frequencyBuffer,
                form.CanvasSize,
                form.HasEndDate,
                endDate,
                form.PaintingId);
        }
        else
        {
            manager.UpdateHabit(
                editingId,
                form.TrimmedTitle,
                form.Category,
                form.HabitType,
                frequencyBuffer,
                form.CanvasSize,
                form.HasEndDate,
                endDate);
        }

        Submitted?.Invoke();
        Close();
    }

    private void Delete()
    {
        if (manager != null && !string.IsNullOrEmpty(editingId))
        {
            manager.DeleteHabit(editingId);
        }

        Submitted?.Invoke();
        Close();
    }

    // Geri alinabilir bir eylem -- Delete() gibi onay sormuyor.
    private void ToggleArchive()
    {
        if (manager != null && !string.IsNullOrEmpty(editingId))
        {
            manager.SetArchived(editingId, !editingArchived);
        }

        Submitted?.Invoke();
        Close();
    }

    // -----------------------------------------------------------------------
    // Animasyon
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

    private void OnDisable()
    {
        slideRoutine = null;
        closing = false;
        optionTransition = null;
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

        // Kilit gecis BITIMINDE aciliyor: animasyon surerken ressam tiklamasi
        // ekrani erken acmamali (bkz. UI_ANIMATION_PLAN.md Faz 2).
        if (navigator != null)
        {
            navigator.SetModalOpen(false);
        }

        gameObject.SetActive(false);
    }

    private void StartOptionAnimation()
    {
        StopOptionAnimation();
        optionTransition = StartCoroutine(AnimateOptions());
    }

    private void StopOptionAnimation()
    {
        if (optionTransition != null)
        {
            StopCoroutine(optionTransition);
            optionTransition = null;
        }
    }

    // buildOption/quitOption/canvasOptions + endDate knob'u TEK coroutine'den
    // surer -- MoodRow'daki "daire basina degil satir basina bir coroutine"
    // kaliginin ayni. Kategori artik bu listede degil (bkz. BuildAnimatedOptions).
    // Yeni bir RefreshAll(false) gelirse StartOptionAnimation() bunu
    // StopOptionAnimation() ile kesip CurrentXxx'in BULUNDUGU yerden yeniden
    // baslatir, basa sarmaz.
    private IEnumerator AnimateOptions()
    {
        for (int i = 0; i < animatedOptions.Length; i++)
        {
            OptionButton option = animatedOptions[i];
            optionFromBackground[i] = option.CurrentBackground;
            optionFromBorder[i] = option.CurrentBorder;
            optionFromLabel[i] = option.CurrentLabelColor;
        }

        float fromKnobX = endDateKnob.anchoredPosition.x;
        Color fromTrackColor = endDateTrack.color;

        for (float t = 0f; t < optionTransitionSeconds; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / optionTransitionSeconds);
            float eased = Easing.OutCubic(k);

            for (int i = 0; i < animatedOptions.Length; i++)
            {
                OptionButton option = animatedOptions[i];
                option.ApplyVisual(
                    Color.Lerp(optionFromBackground[i], option.TargetBackground, eased),
                    Color.Lerp(optionFromBorder[i], option.TargetBorder, eased),
                    Color.Lerp(optionFromLabel[i], option.TargetLabelColor, eased));
            }

            Vector2 knob = endDateKnob.anchoredPosition;
            knob.x = Mathf.Lerp(fromKnobX, endDateTargetKnobX, eased);
            endDateKnob.anchoredPosition = knob;
            endDateTrack.color = Color.Lerp(fromTrackColor, endDateTargetTrackColor, eased);

            yield return null;
        }

        for (int i = 0; i < animatedOptions.Length; i++)
        {
            OptionButton option = animatedOptions[i];
            option.ApplyVisual(option.TargetBackground, option.TargetBorder, option.TargetLabelColor);
        }

        Vector2 finalKnob = endDateKnob.anchoredPosition;
        finalKnob.x = endDateTargetKnobX;
        endDateKnob.anchoredPosition = finalKnob;
        endDateTrack.color = endDateTargetTrackColor;

        optionTransition = null;
    }
}
