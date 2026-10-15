using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Alt navigasyon cubugunun tek sekmesi: ikon + etiket + altin elmas
// gostergesi. Ikon/etiket ICERIGI builder tarafindan bir kez yazilir
// (hangi sekme oldugu degismez); burasi yalnizca SECILI/SECILI DEGIL
// gorselini tasir.
//
// Elmas yalnizca ALFA ile beliriyor (DayChipRow/MoodRow kalibindaki ara
// kare suruculugu BottomNavBar'da tek bir coroutine'de toplanir, burasi
// MoodCircle'daki Current/Target ciftinin ayni gerekcesiyle sadece hedefi
// tutar): olcek/konum degismiyor, cubuktaki uc sekme ayni anda gecis
// yapmasi gerektiginde (ScreenNavigator.ScreenChanged) tek surucunun
// hepsini ayni karede ilerletebilmesi icin.
[DisallowMultipleComponent]
public class NavTab : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;
    [SerializeField] private CanvasGroup diamondGroup;
    [SerializeField] private UiTheme theme;

    private float targetDiamondAlpha;

    public float TargetDiamondAlpha => targetDiamondAlpha;

    public float CurrentDiamondAlpha => diamondGroup != null ? diamondGroup.alpha : 0f;

    // Semantik durum SENKRON yazilir (renk); elmasin alfasi yalnizca HEDEF
    // olarak guncellenir, gercek deger instant=false'ta disaridaki
    // coroutine'den gelir.
    public void SetSelected(bool selected, bool instant)
    {
        targetDiamondAlpha = selected ? 1f : 0f;

        Color tint = theme != null
            ? (selected ? theme.gold : theme.navLabelInactive)
            : Color.white;

        if (icon != null)
        {
            icon.color = tint;
        }

        if (label != null)
        {
            label.color = tint;
        }

        if (instant)
        {
            ApplyVisual(targetDiamondAlpha);
        }
    }

    // Ara kareyi disaridan (BottomNavBar'in tek coroutine'i) suruyoruz: sekme
    // basina bir coroutine acmak MoodRow'un kacindigi ayni gereksiz isti.
    public void ApplyVisual(float diamondAlpha)
    {
        if (diamondGroup != null)
        {
            diamondGroup.alpha = diamondAlpha;
        }
    }
}
