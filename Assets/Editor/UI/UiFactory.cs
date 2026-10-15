using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Builder'larin ortak sozlugu.
//
// Bu olmadan her builder yuzlerce satir anchor aritmetigi olur ve tasarimla
// karsilastirilamaz hale gelirdi. Buradaki yerlestirme yardimcilari TASARIM
// KOORDINATIYLA calisiyor: orijin sol UST kose, "top" asagi dogru artiyor --
// yani teslim dosyasindaki sayilar oldugu gibi yaziliyor.
//
// Canvas Scaler referansi 366x820 ve sprite'lar 100 PPU oldugu icin
// 1 canvas birimi == 1 tasarim pikseli == 1 sprite pikseli.
public static class UiFactory
{
    public static RectTransform Rect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        return rect;
    }

    public static Image Image(string name, Transform parent, Sprite sprite, Color color, bool sliced = true)
    {
        RectTransform rect = Rect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = sliced && sprite != null ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        // Varsayilan olarak dekoratif: dokunmayi yutmasin. Tiklanabilir olanlar
        // bunu acikca geri aciyor (bkz. Button).
        image.raycastTarget = false;
        return image;
    }

    public static TMP_Text Text(
        string name,
        Transform parent,
        string content,
        TMP_FontAsset font,
        float size,
        Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        RectTransform rect = Rect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    // Text()'in yerellestirilmis karsiligi: Editor-onizleme icin Loc.Tr(key)
    // ile icerigi doldurur, sonra LocalizedText bilesenini ekleyip anahtari
    // baglar -- oyun ici dil degisince etiket kendi kendini yeniler. Builder'in
    // Loc.Tr'dan dogru metni alabilmesi icin cagrilmadan once Loc.Init
    // calismis olmali (bkz. LocalizationTableBuilder.Build, PainterHabitsBuilder).
    public static TMP_Text LocalizedText(
        string name,
        Transform parent,
        string key,
        TMP_FontAsset font,
        float size,
        Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left,
        bool uppercase = false)
    {
        string content = Loc.Tr(key);
        TMP_Text text = Text(name, parent, uppercase ? content.ToUpperInvariant() : content, font, size, color, alignment);
        LocalizedText localized = text.gameObject.AddComponent<LocalizedText>();
        localized.Bind(text, key, uppercase);
        return text;
    }

    // Tiklanabilir bir zemin: Image hem gorsel hem hedef.
    public static Button Button(Image target)
    {
        target.raycastTarget = true;
        Button button = target.gameObject.AddComponent<Button>();
        button.targetGraphic = target;
        button.transition = Selectable.Transition.None;
        return button;
    }

    public static UiGradient Gradient(GameObject host, Color top, Color bottom, float angle = 180f)
    {
        UiGradient gradient = host.AddComponent<UiGradient>();
        gradient.Set(top, bottom, angle);
        return gradient;
    }

    // Tasarimin sert golgesi: bulaniklik yok, yalnizca kaydirma.
    public static Shadow HardShadow(GameObject host, float down, Color color)
    {
        Shadow shadow = host.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(0f, -down);
        shadow.effectColor = color;
        shadow.useGraphicAlpha = true;
        return shadow;
    }

    // -----------------------------------------------------------------------
    // Yerlestirme (tasarim koordinati: orijin sol ust, top asagi artar)
    // -----------------------------------------------------------------------

    // Aritmetik UiRect'te (Runtime assembly): ayni hesabi calisma aninda
    // WorkshopLayout da yapiyor ve iki kopya sessizce ayrisirdi.

    public static RectTransform TopLeft(RectTransform rect, float left, float top, float width, float height)
    {
        return UiRect.TopLeft(rect, left, top, width, height);
    }

    // Ustten sabit, yanlardan esneyen serit.
    public static RectTransform StretchTop(RectTransform rect, float top, float height, float left, float right)
    {
        return UiRect.StretchTop(rect, top, height, left, right);
    }

    // Alttan sabit, yanlardan esneyen serit.
    public static RectTransform StretchBottom(RectTransform rect, float bottom, float height, float left, float right)
    {
        return UiRect.StretchBottom(rect, bottom, height, left, right);
    }

    // Ebeveyni doldurur; kenar bosluklari tasarim pikseliyle.
    public static RectTransform Fill(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
    {
        return UiRect.Fill(rect, left, top, right, bottom);
    }

    // Ustten hizali, yatayda ortali.
    public static RectTransform TopCenter(RectTransform rect, float top, float width, float height)
    {
        return UiRect.TopCenter(rect, top, width, height);
    }


    // -----------------------------------------------------------------------
    // Yerlesim bilesenleri
    // -----------------------------------------------------------------------

    public static HorizontalLayoutGroup Row(RectTransform rect, float spacing, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        HorizontalLayoutGroup group = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        group.spacing = spacing;
        group.childAlignment = alignment;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = false;
        group.childControlWidth = true;
        group.childControlHeight = true;
        return group;
    }

    public static VerticalLayoutGroup Column(RectTransform rect, float spacing, TextAnchor alignment = TextAnchor.UpperLeft)
    {
        VerticalLayoutGroup group = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        group.spacing = spacing;
        group.childAlignment = alignment;
        group.childForceExpandWidth = true;
        group.childForceExpandHeight = false;
        group.childControlWidth = true;
        group.childControlHeight = true;
        return group;
    }

    public static LayoutElement Size(GameObject host, float width = -1f, float height = -1f)
    {
        LayoutElement element = host.AddComponent<LayoutElement>();
        element.preferredWidth = width;
        element.preferredHeight = height;
        element.minWidth = width;
        element.minHeight = height;
        return element;
    }

    public static ContentSizeFitter FitVertical(GameObject host)
    {
        ContentSizeFitter fitter = host.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return fitter;
    }

    // FitVertical'in yatay ikizi -- Galeri Seviye 3'un kucuk resim seridi
    // (tasma varsa yana kayan) icin: genislik icerige gore buyur, yukseklik
    // viewport'a sabit kalsin.
    public static ContentSizeFitter FitHorizontal(GameObject host)
    {
        ContentSizeFitter fitter = host.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        return fitter;
    }

    // Kaydirma alani. Viewport ve content'i kurup content'i doner.
    // horizontal=false (varsayilan): dikey liste, content yatayda viewport'a
    // gerilir (HabitsPanelBuilder/GalleryPanelBuilder/GalleryDetailBuilder
    // ile ayni, davranis DEGISMEDI). horizontal=true: Seviye 3'un kucuk
    // resim seridi -- content SOLA sabit, dikeyde viewport'a gerilir ve
    // genisligi FitHorizontal ile kendi icerigine gore buyur.
    public static RectTransform ScrollView(
        RectTransform root,
        out ScrollRect scrollRect,
        out RectTransform viewport,
        bool horizontal = false)
    {
        scrollRect = root.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = horizontal;
        scrollRect.vertical = !horizontal;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.scrollSensitivity = 20f;

        viewport = Fill(Rect("Viewport", root));
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = Rect("Content", viewport);

        if (horizontal)
        {
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
        }
        else
        {
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
        }

        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        scrollRect.viewport = viewport;
        scrollRect.content = content;
        return content;
    }
}
