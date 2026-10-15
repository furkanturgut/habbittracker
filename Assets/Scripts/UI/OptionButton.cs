using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Secili / secili degil iki durumu olan kucuk bir secim dugmesi.
//
// Formdaki dort ayri secici (Tur karti, Kategori pili, Gun dugmesi, Tuval karti)
// gorsel olarak farkli ama DAVRANISI ayni. Dordu icin dort bilesen yazmak yerine
// tek bilesen + dort prefab: secim kurali tek yerde duruyor.
//
// Durum MoodCircle ile ayni kalipta: hedef renkler SENKRON yaziliyor
// (`SetSelected`), ara kareyi disaridaki sahiplik noktasi (HabitSheet'in tek
// surucu coroutine'i) `ApplyVisual` ile suruyor. `instant=true` -- ornegin tek
// basina cagrilan DayChipRow/CanvasSizeOption gibi disaridan surulmeyen
// cagiranlar icin -- hedefi aninda uygular, kendi coroutine'ine gerek kalmaz.
[RequireComponent(typeof(Button))]
[DisallowMultipleComponent]
public class OptionButton : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private Image border;
    [SerializeField] private TMP_Text label;

    [Header("Renkler")]
    [SerializeField] private Color selectedBackground;
    [SerializeField] private Color unselectedBackground;
    [SerializeField] private Color selectedBorder;
    [SerializeField] private Color unselectedBorder;
    [SerializeField] private Color selectedLabel;
    [SerializeField] private Color unselectedLabel;

    private Button button;

    private Color targetBackground;
    private Color targetBorder;
    private Color targetLabelColor;

    public Button Button
    {
        get
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            return button;
        }
    }

    public bool IsSelected { get; private set; }

    public TMP_Text Label => label;

    // Hedef degerler: disaridaki surucu coroutine CurrentXxx'ten buraya yurur.
    public Color TargetBackground => targetBackground;
    public Color TargetBorder => targetBorder;
    public Color TargetLabelColor => targetLabelColor;

    public Color CurrentBackground => background != null ? background.color : targetBackground;
    public Color CurrentBorder => border != null ? border.color : targetBorder;
    public Color CurrentLabelColor => label != null ? label.color : targetLabelColor;

    private void Awake()
    {
        ButtonPressFeedback.EnsureOn(Button);
    }

    // Tur karti secilince baslik "yap" yesiline / "birak" turuncusuna donuyor,
    // digerlerinde altin kaliyor. Renkleri disaridan yazabilmek bu yuzden gerekli.
    public void Configure(Color labelSelected, Color labelUnselected)
    {
        selectedLabel = labelSelected;
        unselectedLabel = labelUnselected;
        ComputeTargets();
        ApplyVisual(targetBackground, targetBorder, targetLabelColor);
    }

    // Disaridan surulmeyen cagiranlar icin kisayol (DayChipRow, CanvasSizeOption
    // gibi tek basina kullananlar): hedefi aninda uygular.
    public void SetSelected(bool value)
    {
        SetSelected(value, instant: true);
    }

    public void SetSelected(bool value, bool instant)
    {
        IsSelected = value;
        ComputeTargets();

        if (instant)
        {
            ApplyVisual(targetBackground, targetBorder, targetLabelColor);
        }
    }

    private void ComputeTargets()
    {
        targetBackground = IsSelected ? selectedBackground : unselectedBackground;
        targetBorder = IsSelected ? selectedBorder : unselectedBorder;
        targetLabelColor = IsSelected ? selectedLabel : unselectedLabel;
    }

    // Ara kareyi disaridan (sahiplik noktasinin tek coroutine'i) suruyoruz --
    // MoodRow'un MoodCircle'i her karede guncellemesiyle ayni kalip.
    public void ApplyVisual(Color backgroundColor, Color borderColor, Color labelColor)
    {
        if (background != null)
        {
            background.color = backgroundColor;
        }

        if (border != null)
        {
            border.color = borderColor;
        }

        if (label != null)
        {
            label.color = labelColor;
        }
    }
}
