using UnityEngine;
using UnityEngine.InputSystem;

// PaintPixels'i tek basina denemek icin: tabloya dokun, bir parca acilsin.
//
// ANA UYGULAMADA KULLANILMAZ. Orada parcalar dokunusla degil komutla aciliyor
// ve hangi parcanin acilacagina HabbitManager karar veriyor -- bu bilesen bir
// goruntuye takilirsa acilan parca kayitla ayrisir. Modulu bir sahneye elle
// koyup gozle denemek icin var; bu yuzden PaintPixels'in icinde degil, ayri.
[RequireComponent(typeof(PaintPixels))]
public class PaintPixelsSandbox : MonoBehaviour
{
    [Tooltip("Bos birakilirsa Camera.main kullanilir.")]
    [SerializeField] private Camera inputCamera;

    private PaintPixels module;

    private void Awake()
    {
        module = GetComponent<PaintPixels>();
    }

    private void Update()
    {
        if (!module.IsBuilt)
        {
            return;
        }

        // Device Simulator acikken Input System native mouse'u devre disi birakip
        // yerine simule bir Touchscreen ekliyor, bu yuzden once dokunmaya bakiyoruz;
        // fare yalnizca editor Game view'i icin yedek. Platform makrosu
        // kullanmiyoruz: aktif build target Standalone oldugu icin UNITY_IOS
        // Simulator'da false kalir ve o kod hic calismazdi.
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
        {
            RevealAtScreenPoint(touchscreen.primaryTouch.position.ReadValue());
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            RevealAtScreenPoint(mouse.position.ReadValue());
        }
    }

    // Tiklama perdenin uzerine denk geldiyse bir parca acar. Hangi parcanin
    // acildigini tiklanan nokta BELIRLEMIYOR -- karar modulun reveal sirasinin;
    // buradaki hit-test sadece "sahnenin bos yerine tiklandi mi" sorusunu eliyor.
    private void RevealAtScreenPoint(Vector2 screenPosition)
    {
        Camera cam = inputCamera != null ? inputCamera : Camera.main;
        if (cam == null)
        {
            return;
        }

        if (module.ContainsWorldPoint(cam.ScreenToWorldPoint(screenPosition)))
        {
            module.RevealNext();
        }
    }
}
