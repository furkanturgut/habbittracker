using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Galeri basligindaki rotus stok pili. GalleryPanelBuilder (Seviye 1) ve
// GalleryDetailBuilder (Seviye 2) HER IKISI de bu fabrikayi cagirir --
// detay gorunumu panelin uzerine tam ekran cizildigi icin dibindeki sag
// bos yuvanin (eskiden "RightSpacer") yerini burada da almasi gerekiyor,
// yoksa pill tek ekranda "yerinde durmus" gibi gorunmez (bkz. design_
// handoff_rotus/README.md).
public static class RotusPillBuilder
{
    private const float Height = 38f;
    private const float Radius = 19f;
    private const float BorderWidth = 2f;
    private const float PaddingLeft = 2f;
    private const float IconSize = 32f;

    // header'in sag-ust kosesine, eski "RightSpacer" ile AYNI ankraja
    // (anchor/pivot 1,1, anchoredPosition sifir) oturtulur.
    public static RotusStockPill Build(RectTransform header, UiTheme theme)
    {
        RectTransform root = UiFactory.Rect("RotusPill", header);
        root.anchorMin = new Vector2(1f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(1f, 1f);
        root.anchoredPosition = Vector2.zero;
        // Gercek genislik RotusStockPill.RefreshLayout tarafindan yazilir
        // (icerige gore buyur); burasi yalnizca ilk kare icin bir baslangic.
        root.sizeDelta = new Vector2(Height, Height);

        Image border = UiFactory.Image("Border", root, theme.Rounded((int)Radius), theme.rotusPillBorder);
        UiFactory.Fill(border.rectTransform);
        UiFactory.HardShadow(root.gameObject, 3f, theme.hardShadow);

        Image fill = UiFactory.Image("Fill", root, theme.Rounded((int)(Radius - BorderWidth)), theme.rotusPillBg);
        UiFactory.Fill(fill.rectTransform, BorderWidth, BorderWidth, BorderWidth, BorderWidth);

        Image icon = UiFactory.Image("Icon", root, null, Color.white, false);
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.sizeDelta = new Vector2(IconSize, IconSize);
        iconRect.anchoredPosition = new Vector2(PaddingLeft, 0f);
        SpriteFrameLoop iconLoop = icon.gameObject.AddComponent<SpriteFrameLoop>();
        UiWiring.Set(iconLoop, "image", icon);

        TMP_Text countLabel = UiFactory.Text(
            "Count", root, "0", theme.semiBold, 16f, theme.textPrimary, TextAlignmentOptions.Left);
        RectTransform labelRect = countLabel.rectTransform;
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);

        RotusStockPill pill = root.gameObject.AddComponent<RotusStockPill>();
        UiWiring.Set(pill, "rect", root);
        UiWiring.Set(pill, "iconRect", iconRect);
        UiWiring.Set(pill, "iconLoop", iconLoop);
        UiWiring.Set(pill, "countLabel", countLabel);
        UiWiring.Set(pill, "theme", theme);

        return pill;
    }
}
