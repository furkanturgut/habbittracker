using UnityEngine;
using UnityEngine.UI;

// Canvas Scaler'i LayoutMetrics'ten yazar.
//
// Olcek kurali uc yerde birden gecerli olmali: Canvas Scaler (chrome),
// SafeAreaFitter (icerik kolonu) ve StudioStage (dunya uzayindaki studyo).
// Ucu de LayoutMetrics'i okuyor; bu bilesen scaler'in Inspector'dan elle
// bozulup digerlerinden ayrismasini imkansiz kiliyor. Ayni gerekce UiRect'in
// Editor ile Runtime arasinda tek kopya olmasindaki gerekce.
//
// [ExecuteAlways]: Editor'de de gecerli olsun ki Game view / Device Simulator
// onizlemesi calisma anindaki yerlesimin aynisini gostersin.
[ExecuteAlways]
[RequireComponent(typeof(CanvasScaler))]
[DisallowMultipleComponent]
public class CanvasScalerDriver : MonoBehaviour
{
    private void OnEnable()
    {
        Apply();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        Apply();
    }
#endif

    public void Apply()
    {
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            return;
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(
            LayoutMetrics.DesignWidth,
            LayoutMetrics.MinDesignHeight);

        // Expand == min(w / refW, h / refH): kanvas hicbir eksende referanstan
        // kucuk kalmaz. Genislige uyum (match = 0) bunun yerine gectiginde
        // tablette kanvas 488 birime dusuyordu.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        // Uretilen UI sprite'lari 100 PPU; 1 kanvas birimi == 1 tasarim pikseli.
        scaler.referencePixelsPerUnit = 100f;
    }
}
