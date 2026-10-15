using UnityEngine;
using UnityEngine.UI;

// Bir Graphic'in koselerini iki renk arasinda boyar.
//
// Tasarim kartlarda, butonlarda ve sahne gecislerinde dogrusal gradyan istiyor;
// uGUI Image'inda gradyan YOK. Her ton icin ayri bir doku uretmek yerine vertex
// renklerini degistiriyoruz: tek bir sprite butun gradyanlarda kullanilabiliyor
// ve 9-slice'in dokuz parcasi da ayni eksende dogru boyaniyor.
//
// Renk mevcut vertex rengiyle CARPILIYOR, uzerine yazilmiyor: Graphic'in kendi
// alfasi (CanvasGroup, fade) korunuyor. Saf gradyan icin Image.color beyaz olmali.
[AddComponentMenu("UI/Effects/Ui Gradient")]
[DisallowMultipleComponent]
public class UiGradient : BaseMeshEffect
{
    [SerializeField] private Color topColor = Color.white;
    [SerializeField] private Color bottomColor = Color.gray;

    [Tooltip("CSS ile ayni olcek: 0 = asagidan yukari, 180 = yukaridan asagiya.")]
    [SerializeField] private float angleDegrees = 180f;

    public Color TopColor => topColor;
    public Color BottomColor => bottomColor;

    public void Set(Color top, Color bottom, float angle = 180f)
    {
        topColor = top;
        bottomColor = bottom;
        angleDegrees = angle;

        // BaseMeshEffect'in kendi SetVerticesDirty'si yok; yeniden cizimi
        // bagli oldugu Graphic isteyecek.
        if (graphic != null)
        {
            graphic.SetVerticesDirty();
        }
    }

    public override void ModifyMesh(VertexHelper helper)
    {
        if (!IsActive() || helper.currentVertCount == 0)
        {
            return;
        }

        // CSS'te 180deg gradyan cizgisi asagi bakar ve ILK renk ustte kalir.
        // uGUI'de +Y yukari oldugu icin yon (sin, cos) ile ayni sonucu veriyor.
        float radians = angleDegrees * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));

        UIVertex vertex = default;
        float min = float.MaxValue;
        float max = float.MinValue;

        for (int i = 0; i < helper.currentVertCount; i++)
        {
            helper.PopulateUIVertex(ref vertex, i);
            float projection = vertex.position.x * direction.x + vertex.position.y * direction.y;

            if (projection < min)
            {
                min = projection;
            }

            if (projection > max)
            {
                max = projection;
            }
        }

        float range = max - min;
        if (range <= Mathf.Epsilon)
        {
            return;
        }

        for (int i = 0; i < helper.currentVertCount; i++)
        {
            helper.PopulateUIVertex(ref vertex, i);

            float projection = vertex.position.x * direction.x + vertex.position.y * direction.y;
            Color tint = Color.Lerp(topColor, bottomColor, (projection - min) / range);

            vertex.color = (Color)vertex.color * tint;
            helper.SetUIVertex(vertex, i);
        }
    }
}
