using UnityEngine;

// StudioStage kokunun altindaki tek bir piksel-art nesnesini tasarim
// koordinatina yerlestirir.
//
// Teslim dosyasi her seyi "left / top / zoom" ile anlatiyor (ornegin ressam:
// left 36, top 202, 5x). Kaynak gorsellerin pixelsPerUnit degerleri ise farkli
// (stüdyo varliklari 1, ressam sheet'leri 32) -- olcegi elle yazmak yerine
// ikisinden TURETIYORUZ, boylece bir import ayari degisirse konum da dogru kalir.
[ExecuteAlways]
[DisallowMultipleComponent]
public class StudioPixelSprite : MonoBehaviour
{
    [Tooltip("Tasarimdaki sol-ust konum (px), sahne kokune gore.")]
    [SerializeField] private Vector2 topLeft;

    [Tooltip("Kaynak gorselin piksel boyutu (ornegin ressam karesi 33x34).")]
    [SerializeField] private Vector2 sourceSize = Vector2.one;

    [Tooltip("Tasarimdaki buyutme carpani: studio_bg 4x, ressam 5x.")]
    [SerializeField] private float zoom = 1f;

    [Tooltip("Kaynak gorselin pixelsPerUnit degeri: studio varliklari 1, ressam 32.")]
    [SerializeField] private float sourcePixelsPerUnit = 1f;

    [Tooltip("Gorselin pivotu. GERCEK deger sprite'in import ayarindan okunur; "
        + "bu alan yalnizca sprite cozulemezse kullanilan yedek.")]
    [SerializeField] private Vector2 pivot = Vector2.zero;

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
        // Kok zaten tasarim pikselini dunya birimine ceviriyor; burada yalnizca
        // "kaynak pikseli -> tasarim pikseli" carpani kaliyor.
        transform.localScale = Vector3.one * (zoom * sourcePixelsPerUnit);

        float width = sourceSize.x * zoom;
        float height = sourceSize.y * zoom;

        Vector2 spritePivot = ResolvePivot();

        // Kokun y ekseni asagi dogru negatif: "top" degeri eksi isaretle giriyor.
        transform.localPosition = new Vector3(
            topLeft.x + spritePivot.x * width,
            -(topLeft.y + (1f - spritePivot.y) * height),
            0f);
    }

    // Pivotu SPRITE'IN KENDISINDEN turetiyoruz, tipki olcegi pixelsPerUnit'ten
    // turettigimiz gibi. Elle yazilan pivot sessizce yaniltiyordu: studyo
    // varliklari alignment 6 (BottomLeft) ile iceri aliniyor ama ressam
    // sheet'leri ve uretilen golge alignment 0 (Center) -- builder ikisine de
    // (0,0) yaziyordu, bu yuzden ressam yarim kare (82 px) sola ve 85 px asagi
    // kayip kosede kaliyordu. Sprite'tan okununca bir import ayari degisse bile
    // konum dogru kalir.
    private Vector2 ResolvePivot()
    {
        SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>(true);
        Sprite sprite = renderer != null ? renderer.sprite : null;

        if (sprite == null)
        {
            return pivot;
        }

        Rect rect = sprite.rect;
        if (rect.width <= 0f || rect.height <= 0f)
        {
            return pivot;
        }

        // Sprite.pivot rect'in sol altina gore PIKSEL cinsinden; 0..1'e ceviriyoruz.
        return new Vector2(sprite.pivot.x / rect.width, sprite.pivot.y / rect.height);
    }
}
