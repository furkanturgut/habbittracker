using UnityEngine;

// Tasarim koordinatiyla RectTransform yerlestirme: orijin sol UST kose, "top"
// asagi dogru artar. Teslim dosyasindaki sayilar donusumsuz yazilabiliyor.
//
// Ayni yardimcilar hem Editor builder'inda (UiFactory bunlari cagiriyor) hem de
// calisma aninda (WorkshopLayout ekrani yeniden dagitirken) gerekiyor. Iki ayri
// kopya olsaydi biri duzeltilip digeri unutulurdu; tek tanim burada duruyor ve
// Editor tarafi buna delege ediyor.
public static class UiRect
{
    public static RectTransform TopLeft(RectTransform rect, float left, float top, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    // Ustten sabit, yanlardan esneyen serit.
    public static RectTransform StretchTop(RectTransform rect, float top, float height, float left, float right)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, -(top + height));
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    // Alttan sabit, yanlardan esneyen serit.
    public static RectTransform StretchBottom(RectTransform rect, float bottom, float height, float left, float right)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, bottom + height);
        return rect;
    }

    // Ebeveyni doldurur; kenar bosluklari tasarim pikseliyle.
    public static RectTransform Fill(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    // Ustten hizali, yatayda ortali. Pivot ustte: ContentSizeFitter boyutu
    // buyutunce kutu asagi dogru acilir ve "top" degeri sabit kalir.
    public static RectTransform TopCenter(RectTransform rect, float top, float width, float height)
    {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -top);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }
}
