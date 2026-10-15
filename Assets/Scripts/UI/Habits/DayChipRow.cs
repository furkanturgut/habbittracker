using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Haftanin yedi gunu icin rozet sirasi. Kartta salt okunur (18x18, tek harf),
// formda tiklanabilir (44 yuksek, iki harf).
//
// Iki AYRI gosterim metodu var cunku "bos liste" iki yerde farkli anlama geliyor:
//   * Kayitta bos frequency = HER GUN planli (HabitScheduling sozlesmesi), yani
//     kartta yedi rozet de aktif gorunur.
//   * Formda bos secim = kullanici henuz gun secmedi; ipucu "Seçilmezse her gün"
//     diyor ve rozetlerin hicbiri isaretli olmamali.
// Tek metotla ikisini karistirmak, duzenleme ekraninda butun gunleri secilmis
// gosterip kaydedince anlami degistirirdi.
//
// Formdaki rozetler (ShowForm) MoodRow/HabitFilterBar ile ayni kalipta kendi
// surucu coroutine'ine sahip: chips[] bu bilesende ozel kaldigi icin HabitSheet'in
// paylasimli AnimateOptions'ina katilamazlar, bu yuzden kendi "hedef + instant
// bayragi + tek coroutine" dongusunu tasirlar (bkz. UI_ANIMATION_PLAN.md Faz 4
// notu). Kartin salt okunur rozetleri (ShowFrequency) bilincli olarak aninda
// kaliyor -- HabitCard.cs:11'deki "yonetim ekrani, animasyona gerek yok" karari.
[DisallowMultipleComponent]
public class DayChipRow : MonoBehaviour
{
    [SerializeField] private OptionButton chipPrefab;
    [SerializeField] private Transform root;

    [Tooltip("Acikken rozet tek harf gosterir (kart), kapaliyken iki harf (form).")]
    [SerializeField] private bool useInitials = true;

    [Tooltip("Acikken rozetlere dokunulabilir.")]
    [SerializeField] private bool interactable;

    [Tooltip("Form gecis suresi (sn). Yalnizca kozmetik, ShowFrequency'yi etkilemez.")]
    [SerializeField] private float transitionSeconds = 0.16f;

    private readonly List<OptionButton> chips = new List<OptionButton>(WeekDays.Count);

    // Gecisin baslangic degerleri: coroutine icinde yerel dizi acmak yerine
    // alan, satir bir kez kuruluyor.
    private Color[] fromBackground;
    private Color[] fromBorder;
    private Color[] fromLabel;

    private Coroutine transition;

    // Son ShowFrequency/ShowForm cagrisinin parametreleri: hafta baslangici
    // degisince (AppSettings.Changed) rozetleri YENIDEN URETMEDEN sadece
    // etiketlerini ve secim durumlarini tazelemek icin (bkz. Relabel).
    private List<DayOfWeek> lastFrequency;
    private HabitForm lastForm;
    private bool showingForm;

    public event Action<int> DayToggled;

    private void Awake()
    {
        Build();
    }

    private void OnEnable()
    {
        AppSettings.Changed += Relabel;
    }

    private void OnDisable()
    {
        AppSettings.Changed -= Relabel;
        StopTransition();
    }

    // Kayittaki programi gosterir: bos liste = her gun.
    public void ShowFrequency(List<DayOfWeek> frequency)
    {
        Build();

        lastFrequency = frequency;
        showingForm = false;

        for (int i = 0; i < chips.Count; i++)
        {
            int mondayIndex = WeekDays.MondayIndexForSlot(i, AppSettings.WeekStart);
            chips[i].SetSelected(WeekDays.IsSelected(frequency, mondayIndex));
        }
    }

    // Formdaki secimi gosterir: bos secim = hicbiri. instant=false -- yani
    // kullanici formda bir gun degistirdiginde -- hedefler guncellenir ama
    // gorsel degismez; tek surucu coroutine (Animate) CurrentXxx'ten
    // TargetXxx'e yumusak gecer. HabitSheet.Open() instant=true ile cagirir.
    public void ShowForm(HabitForm form, bool instant)
    {
        Build();

        lastForm = form;
        showingForm = true;

        for (int i = 0; i < chips.Count; i++)
        {
            int mondayIndex = WeekDays.MondayIndexForSlot(i, AppSettings.WeekStart);
            chips[i].SetSelected(form.IsDaySelected(mondayIndex), instant);
        }

        if (instant || !isActiveAndEnabled || transitionSeconds <= 0f)
        {
            StopTransition();
            return;
        }

        // Yeni gecis eskisini KESIYOR: yarim kalmis bir yumusatmanin ustune
        // binmek rozetleri arada bir yerde birakirdi.
        StopTransition();
        transition = StartCoroutine(Animate());
    }

    // Hafta baslangici degisince cagrilir: rozetler YENIDEN URETILMEZ, yalnizca
    // etiketleri (WeekDays.LabelForSlot/InitialForSlot) ve son gosterilen
    // secim/program aninda (instant) yeniden okunur -- kullanici ayari
    // degistirirken rozetlerin bir anlik yanlis gun gostermesi istenmiyor.
    private void Relabel()
    {
        if (chips.Count != WeekDays.Count)
        {
            return;
        }

        for (int i = 0; i < chips.Count; i++)
        {
            chips[i].Label.text = useInitials
                ? WeekDays.InitialForSlot(i, AppSettings.WeekStart)
                : WeekDays.LabelForSlot(i, AppSettings.WeekStart);
        }

        if (showingForm && lastForm != null)
        {
            ShowForm(lastForm, true);
        }
        else if (!showingForm)
        {
            ShowFrequency(lastFrequency);
        }
    }

    private void Build()
    {
        if (chipPrefab == null || root == null || chips.Count == WeekDays.Count)
        {
            return;
        }

        for (int i = chips.Count; i < WeekDays.Count; i++)
        {
            OptionButton chip = Instantiate(chipPrefab, root);
            chip.Label.text = useInitials
                ? WeekDays.InitialForSlot(i, AppSettings.WeekStart)
                : WeekDays.LabelForSlot(i, AppSettings.WeekStart);
            chip.Button.interactable = interactable;

            if (interactable)
            {
                int slot = i;
                chip.Button.onClick.AddListener(
                    () => DayToggled?.Invoke(WeekDays.MondayIndexForSlot(slot, AppSettings.WeekStart)));
            }

            chips.Add(chip);
        }

        int count = chips.Count;
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

    private IEnumerator Animate()
    {
        int count = chips.Count;

        // Baslangic degerleri: gecis ortasinda yeni bir ShowForm gelirse
        // yurume bulundugu yerden devam etsin, basa sarmasin.
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
