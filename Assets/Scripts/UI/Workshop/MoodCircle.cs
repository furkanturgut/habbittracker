using UnityEngine;
using UnityEngine.UI;

// Mod satirindaki tek daire.
//
// Durum UC kanaldan birden okunuyor -- renk, OLCEK ve opaklik. Yalnizca renk
// kullanmak erisilebilirlik acisindan yetersiz (renk korlugu, gunes altinda
// ekran); ustelik daireler tugla duvarin uzerinde duruyor ve tek basina ton
// farki zayif kaliyor.
//
// Olcek icerideki scaleRoot'a uygulaniyor, kokun sizeDelta'sina DEGIL: kok
// HorizontalLayoutGroup'un icinde sabit 44x44 bir LayoutElement ve buyuyup
// kuculurse butun satir her mod degisiminde akar.
[DisallowMultipleComponent]
public class MoodCircle : MonoBehaviour
{
    [SerializeField] private RectTransform scaleRoot;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image border;
    [SerializeField] private Image fill;
    [SerializeField] private Image icon;
    [SerializeField] private UiTheme theme;

    [Header("Durum olculeri")]
    [SerializeField] private float selectedScale = 1f;
    [SerializeField] private float unselectedScale = 0.86f;
    [SerializeField] private float selectedAlpha = 1f;
    [SerializeField] private float unselectedAlpha = 0.45f;

    // Hedef degerler: animasyon bunlara dogru yurur, dogruluk bunlarda.
    private float targetScale = 1f;
    private float targetAlpha = 1f;

    public float TargetScale => targetScale;
    public float TargetAlpha => targetAlpha;

    // Sprite yoksa Image kapatiliyor: sade daire, bos kare degil.
    public void SetIcon(Sprite sprite)
    {
        if (icon == null)
        {
            return;
        }

        icon.sprite = sprite;
        icon.enabled = sprite != null;
    }

    // Semantik durumu SENKRON yaziyor. Yumusatma (varsa) yalnizca kozmetik --
    // mod birden fazla basamak atlayabiliyor (kapanan gunlerin cezasi tek
    // cagrida geliyor) ve o sirada dogru dairenin yanmasi animasyonun bitmesine
    // birakilamaz.
    public void SetSelected(bool selected, bool instant)
    {
        targetScale = selected ? selectedScale : unselectedScale;
        targetAlpha = selected ? selectedAlpha : unselectedAlpha;

        if (border != null)
        {
            border.color = selected ? theme.gold : theme.borderSoft;
        }

        if (fill != null)
        {
            fill.color = selected ? theme.burgundy : theme.cardBottom;
        }

        if (instant)
        {
            ApplyVisual(targetScale, targetAlpha);
        }
    }

    // Ara kareyi disaridan (MoodRow'un tek coroutine'i) suruyoruz: daire basina
    // bir coroutine acmak altmis kare boyunca alti kat is demekti.
    public void ApplyVisual(float scale, float alpha)
    {
        if (scaleRoot != null)
        {
            scaleRoot.localScale = new Vector3(scale, scale, 1f);
        }

        if (group != null)
        {
            group.alpha = alpha;
        }
    }

    public float CurrentScale => scaleRoot != null ? scaleRoot.localScale.x : 1f;

    public float CurrentAlpha => group != null ? group.alpha : 1f;
}
