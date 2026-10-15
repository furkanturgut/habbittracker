using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Atolye ekranindaki elmas filtresinin secim durumu ve sayilari.
//
// Hangi filtrelerin oldugu ve neyin esledigi HabitFilters'ta (saf ve test
// edilebilir); burasi yalnizca gorsel durum ve dokunma. Elmas SAYISI artik
// SABIT degil -- kullanici Ayarlar'dan hangi filtrelerin gorunecegini
// seciyor (bkz. AppSettings.FilterTokens) -- bu yuzden PaintingPickerRow'un
// havuzlama kaligi kullaniliyor: GameObject yaratip yok etmek yerine var
// olanlar yeniden baglaniyor.
//
// MoodRow ile ayni kalip: "hedef + instant bayragi + disaridan surulen tek
// coroutine". `Refresh()` hem ilk acilista hem sadece sayilar degistiginde
// (secim ayni kalirken) hem de kullanici bir elmasa basinca cagriliyor --
// ucunu de ayirt etmek yerine MoodRow'daki gibi bir `shown` bayragi kullanildi:
// ilk gosterim aninda, sonrasi hep yumusak (hedef degismiyorsa zaten gorsel
// fark etmiyor).
[DisallowMultipleComponent]
public class HabitFilterBar : MonoBehaviour
{
    [SerializeField] private HabbitManager manager;
    [SerializeField] private HabitFilterButton buttonPrefab;
    [SerializeField] private Transform root;

    [Tooltip("Elmas gecis suresi (sn). Yalnizca kozmetik.")]
    [SerializeField] private float transitionSeconds = 0.16f;

    private readonly List<HabitFilterButton> buttons = new List<HabitFilterButton>(HabitFilters.MaxSlots);
    private readonly List<HabitFilters.HabitFilter> filters = new List<HabitFilters.HabitFilter>(HabitFilters.MaxSlots);

    private int selected;

    // Gecisin baslangic degerleri: coroutine icinde yerel dizi acmak yerine
    // alan, satir bir kez kuruluyor.
    private float[] fromSize;
    private float[] fromTop;
    private Color[] fromBorder;
    private Color[] fromGradientTop;
    private Color[] fromGradientBottom;
    private Color[] fromCountColor;
    private Color[] fromNameColor;

    private Coroutine transition;

    // Ilk tazeleme animasyonsuz olmali: acilista elmaslar sifirdan hedefe
    // yurumez, dogru durumda belirir.
    private bool shown;

    // Secilen filtrenin KENDISI -- indeks degil. Rebuild listeyi her cagrida
    // bastan doldurdugu icin eski secimi "eski liste + eski indeks" ile
    // yeniden bulmaya calismak, liste zaten ezildikten SONRA okunursa yanlis
    // elemani isaret ederdi (bkz. Rebuild'in bunu NEDEN boyle tuttugu yorumu).
    private HabitFilters.HabitFilter selectedFilter = HabitFilters.HabitFilter.All;

    public event Action<HabitFilters.HabitFilter> FilterChanged;

    public HabitFilters.HabitFilter Selected => selectedFilter;

    private void OnEnable()
    {
        AppSettings.Changed += HandleSettingsChanged;
    }

    private void OnDisable()
    {
        AppSettings.Changed -= HandleSettingsChanged;

        StopTransition();

        // Panel tekrar acildiginda animasyonsuz dogru durumla gelsin.
        shown = false;
    }

    // Hem Ayarlar'dan secim degistiginde hem de liste/tik degistiginde
    // cagrilir -- ikisini ayirt etmeye gerek yok, Rebuild zaten yalnizca
    // filtre KUMESI degistiyse elmas sayisini degistiriyor.
    //
    // Rebuild secili filtreyi kumeden dusurebilir (silinen bir ozel kategori
    // gibi) ve bu durumda selectedFilter kendiligi'nden Tumu'ye duser --
    // bu, Select()'in disinda (Ayarlar'dan tetiklenen HandleSettingsChanged
    // yolunda) olan bir degisiklik oldugu icin FilterChanged'i BURADA
    // fırlatiyoruz; Select() kendi cagrisinda zaten fırlatiyor, cift
    // fırlatmayi onlemek icin oncesi/sonrasi KARSILASTIRILIYOR.
    public void Refresh()
    {
        HabitFilters.HabitFilter beforeRebuild = selectedFilter;
        Rebuild();

        if (manager == null)
        {
            return;
        }

        IReadOnlyList<HabitData> habits = manager.Habits;

        bool instant = !shown || !isActiveAndEnabled || transitionSeconds <= 0f;

        // buttons.Count havuzun TAVAN boyu -- Rebuild bir filtre dustugunde
        // kuculmez, yalnizca fazla butonlari SetActive(false) yapar. Burada
        // filters.Count'a gore donmek gerekir, yoksa kisalmis listenin
        // disina tasip ArgumentOutOfRangeException firlar.
        for (int i = 0; i < filters.Count; i++)
        {
            buttons[i].SetCount(HabitFilters.Count(filters[i], habits));
            buttons[i].SetSelected(i == selected, instant);
        }

        shown = true;

        if (!selectedFilter.Equals(beforeRebuild))
        {
            FilterChanged?.Invoke(selectedFilter);
        }

        if (instant)
        {
            StopTransition();
            return;
        }

        // Yeni gecis eskisini KESIYOR: yarim kalmis bir yumusatmanin ustune
        // binmek elmaslari arada bir yerde birakirdi.
        StopTransition();
        transition = StartCoroutine(Animate());
    }

    private void HandleSettingsChanged()
    {
        Refresh();
    }

    // Filtre kumesini AppSettings.FilterTokens'tan cozer ve elmas havuzunu
    // o kumeye gore buyutur/kucultur. selectedFilter (indeks DEGIL, deger)
    // yeni kumede hala varsa onu, yoksa Tumu'yu isaretler -- liste HER
    // cagrida bastan dolduruldugu icin eski secimi "eski liste + eski
    // indeks" ile aramak (liste zaten ezildikten SONRA) yanlis elemani
    // isaret ederdi; bu yuzden karsilastirma deger uzerinden.
    private void Rebuild()
    {
        HabitFilters.Collect(AppSettings.FilterTokens, filters);
        EnsureCapacity(filters.Count);

        for (int i = 0; i < filters.Count; i++)
        {
            HabitFilterButton button = ButtonAt(i);
            button.SetName(HabitFilters.Label(filters[i]));
            button.gameObject.SetActive(true);
        }

        for (int i = filters.Count; i < buttons.Count; i++)
        {
            buttons[i].gameObject.SetActive(false);
        }

        selected = 0;
        for (int i = 0; i < filters.Count; i++)
        {
            if (filters[i].Equals(selectedFilter))
            {
                selected = i;
                break;
            }
        }

        selectedFilter = filters.Count > 0 ? filters[selected] : HabitFilters.HabitFilter.All;
    }

    private HabitFilterButton ButtonAt(int index)
    {
        while (buttons.Count <= index)
        {
            HabitFilterButton button = Instantiate(buttonPrefab, root);

            // Dongu degiskenini yakalamak yerine yerel kopya: C# 5'te gecerli
            // olan tuzak surumden bagimsiz okunur kalsin.
            int capturedIndex = buttons.Count;
            button.Button.onClick.AddListener(() => Select(capturedIndex));

            buttons.Add(button);
        }

        return buttons[index];
    }

    private void Select(int index)
    {
        if (index == selected || index >= filters.Count)
        {
            return;
        }

        selected = index;
        selectedFilter = filters[index];
        Refresh();
        FilterChanged?.Invoke(Selected);
    }

    private void EnsureCapacity(int count)
    {
        if (fromSize != null && fromSize.Length >= count)
        {
            return;
        }

        fromSize = new float[count];
        fromTop = new float[count];
        fromBorder = new Color[count];
        fromGradientTop = new Color[count];
        fromGradientBottom = new Color[count];
        fromCountColor = new Color[count];
        fromNameColor = new Color[count];
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
        int count = filters.Count;

        // Baslangic degerleri: gecis ortasinda yeni bir Refresh gelirse yurume
        // bulundugu yerden devam etsin, basa sarmasin.
        for (int i = 0; i < count; i++)
        {
            HabitFilterButton button = buttons[i];
            fromSize[i] = button.CurrentSize;
            fromTop[i] = button.CurrentTop;
            fromBorder[i] = button.CurrentBorder;
            fromGradientTop[i] = button.CurrentGradientTop;
            fromGradientBottom[i] = button.CurrentGradientBottom;
            fromCountColor[i] = button.CurrentCountColor;
            fromNameColor[i] = button.CurrentNameColor;
        }

        float elapsed = 0f;

        while (elapsed < transitionSeconds)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / transitionSeconds);
            float eased = Easing.OutCubic(t);

            for (int i = 0; i < count; i++)
            {
                HabitFilterButton button = buttons[i];
                button.ApplyVisual(
                    Mathf.Lerp(fromSize[i], button.TargetSize, eased),
                    Mathf.Lerp(fromTop[i], button.TargetTop, eased),
                    Color.Lerp(fromBorder[i], button.TargetBorder, eased),
                    Color.Lerp(fromGradientTop[i], button.TargetGradientTop, eased),
                    Color.Lerp(fromGradientBottom[i], button.TargetGradientBottom, eased),
                    Color.Lerp(fromCountColor[i], button.TargetCountColor, eased),
                    Color.Lerp(fromNameColor[i], button.TargetNameColor, eased));
            }

            yield return null;
        }

        // Son kare tam hedefte kapansin: Lerp'in yuvarlamasi kalmasin.
        for (int i = 0; i < count; i++)
        {
            HabitFilterButton button = buttons[i];
            button.ApplyVisual(
                button.TargetSize,
                button.TargetTop,
                button.TargetBorder,
                button.TargetGradientTop,
                button.TargetGradientBottom,
                button.TargetCountColor,
                button.TargetNameColor);
        }

        transition = null;
    }
}
