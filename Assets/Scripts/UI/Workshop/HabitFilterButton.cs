using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Filtre satirindaki tek elmas.
//
// Elmas, 45 derece dondurulmus yuvarlak koseli bir kare; icindeki sayi TERS
// dondurulerek duz duruyor. Secili olan hem buyuyor hem yukari kayiyor, bu
// yuzden boyut ve ust bosluk da durumun parcasi.
//
// MoodCircle ile ayni kalip: hedefler `SetSelected` ile SENKRON yaziliyor,
// ara kareyi disaridaki sahiplik noktasi (HabitFilterBar'in tek surucu
// coroutine'i) `ApplyVisual` ile suruyor.
[DisallowMultipleComponent]
public class HabitFilterButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private RectTransform diamond;
    [SerializeField] private Image diamondBackground;
    [SerializeField] private Image diamondBorder;
    [SerializeField] private UiGradient diamondGradient;
    [SerializeField] private TMP_Text countLabel;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private UiTheme theme;

    [Header("Olculer")]
    [SerializeField] private float selectedSize = 50f;
    [SerializeField] private float unselectedSize = 40f;
    [SerializeField] private float selectedTop = 4f;
    [SerializeField] private float unselectedTop = 10f;

    // Elmas, MERKEZ pivotlu (0.5, 0.5) bir karenin 45 derece dondurulmus hali
    // (bkz. UiPrefabBuilder.BuildFilterDiamond). Pivot merkezde oldugu icin
    // donus gorsel merkezi hic oynatmiyor -- yatay ortalama artik bedava.
    //
    // Bu sabit yalnizca DIKEY gorunumu eskisi gibi birakmak icin: pivot
    // ust-ortadayken donus merkezi kenar/(2*kok2) kadar ASAGI da cekiyordu,
    // ve "top" degerleri (selectedTop/unselectedTop) o goruntuye gore
    // ayarlanmisti. Ayni ofseti burada acikca ekleyip elmasin dikeyde
    // oynamasini engelliyoruz.
    public const float RotatedCenterOffset = 0.35355339f; // 1 / (2 * kok2)

    private float targetSize = 40f;
    private float targetTop = 10f;
    private Color targetBorder;
    private Color targetGradientTop;
    private Color targetGradientBottom;
    private Color targetCountColor;
    private Color targetNameColor;

    public Button Button => button;

    public float TargetSize => targetSize;
    public float TargetTop => targetTop;
    public Color TargetBorder => targetBorder;
    public Color TargetGradientTop => targetGradientTop;
    public Color TargetGradientBottom => targetGradientBottom;
    public Color TargetCountColor => targetCountColor;
    public Color TargetNameColor => targetNameColor;

    public float CurrentSize => diamond != null ? diamond.sizeDelta.x : targetSize;

    // ApplyVisual'daki y formulunun TERSI: ikisi birlikte degismeli, yoksa
    // gecisin baslangic degeri hedefle ayni birimde olmaz ve elmas her
    // secimde zipllar.
    public float CurrentTop => diamond != null
        ? -diamond.anchoredPosition.y - diamond.sizeDelta.x * RotatedCenterOffset
        : targetTop;
    public Color CurrentBorder => diamondBorder != null ? diamondBorder.color : targetBorder;
    public Color CurrentGradientTop => diamondGradient != null ? diamondGradient.TopColor : targetGradientTop;
    public Color CurrentGradientBottom => diamondGradient != null ? diamondGradient.BottomColor : targetGradientBottom;
    public Color CurrentCountColor => countLabel != null ? countLabel.color : targetCountColor;
    public Color CurrentNameColor => nameLabel != null ? nameLabel.color : targetNameColor;

    private void Awake()
    {
        ButtonPressFeedback.EnsureOn(button);
    }

    public void SetName(string value)
    {
        nameLabel.text = value;
    }

    public void SetCount(int value)
    {
        countLabel.text = value.ToString();
    }

    // Semantik durumu SENKRON yaziyor. instant=false ise yalniz hedefler
    // guncellenir, gorsel aynen kalir -- disaridaki tek surucu coroutine
    // CurrentXxx'ten buraya yurur.
    public void SetSelected(bool selected, bool instant)
    {
        targetSize = selected ? selectedSize : unselectedSize;
        targetTop = selected ? selectedTop : unselectedTop;
        targetBorder = selected ? theme.gold : theme.borderSoft;
        targetGradientTop = selected ? theme.burgundyLight : theme.cardTop;
        targetGradientBottom = selected ? theme.burgundy : theme.cardBottom;
        targetCountColor = selected ? theme.textPrimary : theme.textMuted;
        targetNameColor = selected ? theme.gold : theme.textMuted;

        diamondBackground.color = Color.white;

        if (instant)
        {
            ApplyVisual(targetSize, targetTop, targetBorder, targetGradientTop, targetGradientBottom, targetCountColor, targetNameColor);
        }
    }

    // Ara kareyi disaridan (HabitFilterBar'in tek coroutine'i) suruyoruz:
    // elmas basina bir coroutine acmak satir basina bes kat is demekti.
    public void ApplyVisual(
        float size,
        float top,
        Color borderColor,
        Color gradientTop,
        Color gradientBottom,
        Color countColor,
        Color nameColor)
    {
        diamond.sizeDelta = new Vector2(size, size);

        // x her zaman 0: merkez pivotla elmas kendi sutununun tam ortasinda.
        // y'deki ofset icin bkz. RotatedCenterOffset -- CurrentTop bunun
        // tersini aliyor.
        diamond.anchoredPosition = new Vector2(0f, -(top + size * RotatedCenterOffset));

        if (diamondGradient != null)
        {
            diamondGradient.Set(gradientTop, gradientBottom, 160f);
        }

        diamondBorder.color = borderColor;
        countLabel.color = countColor;
        nameLabel.color = nameColor;
    }
}
