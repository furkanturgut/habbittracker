using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Rotus harcayip bitmis bir tablonun yanan parcalarini onaran alttan acilan
// kutu. PaintingChoiceSheetBuilder'in uc-bantli sablonunu izliyor (grabber
// bandi + kaydirmali govde + sabit alt bar) -- govde kaydirmali cunku
// ikon+baslik+tablo kutusu+govde metni dizisi kisa ekranlarda tasabilir,
// PaintingChoiceSheet'in "Options" govdesiyle AYNI gerekce.
public static class RotusRestoreSheetBuilder
{
    private const float SheetHeight = 640f;
    private const float SheetTopMargin = 40f;
    private const float GrabberBandHeight = 24f;
    private const float FooterHeight = 122f;

    private const float PaintingBoxWidth = 228f;
    private const float PaintingBoxHeight = 198f;

    public static RotusRestoreSheet Build(
        RectTransform parent,
        UiTheme theme,
        HabbitManager manager,
        ScreenNavigator navigator)
    {
        RectTransform root = UiFactory.Fill(UiFactory.Rect("RotusRestoreSheet", parent));

        Image scrim = UiFactory.Image("Scrim", root, theme.white, theme.scrim, false);
        UiFactory.Fill(scrim.rectTransform);
        Button scrimButton = UiFactory.Button(scrim);
        CanvasGroup scrimGroup = scrim.gameObject.AddComponent<CanvasGroup>();

        Image sheet = UiFactory.Image("Sheet", root, theme.Rounded(28), theme.surface);
        RectTransform sheetRect = UiFactory.StretchBottom(sheet.rectTransform, 0f, SheetHeight, 0f, 0f);
        sheet.raycastTarget = true;
        UiFactory.Gradient(sheet.gameObject, theme.cardTop, theme.cardBottom);

        MaxHeightFitter fitter = sheet.gameObject.AddComponent<MaxHeightFitter>();
        UiWiring.SetFloat(fitter, "designHeight", SheetHeight);
        UiWiring.SetFloat(fitter, "topMargin", SheetTopMargin);

        Image topBorder = UiFactory.Image("TopBorder", sheetRect, theme.white, theme.borderGold, false);
        UiFactory.StretchTop(topBorder.rectTransform, 0f, 2f, 0f, 0f);

        BuildGrabber(sheetRect, theme);

        BuildBody(
            sheetRect, theme,
            out Image icon, out SpriteFrameLoop iconLoop, out TMP_Text subLabel,
            out Image shadow, out Image painting, out Image flash, out TMP_Text bodyLabel);

        BuildFooter(
            sheetRect, theme,
            out Button primaryButton, out UiGradient primaryGradient, out Shadow primaryShadow, out TMP_Text primaryLabel,
            out Button secondaryButton, out TMP_Text secondaryLabel);

        RotusRestoreSheet component = root.gameObject.AddComponent<RotusRestoreSheet>();
        UiWiring.Set(component, "manager", manager);
        UiWiring.Set(component, "navigator", navigator);
        UiWiring.Set(component, "scrimGroup", scrimGroup);
        UiWiring.Set(component, "scrimButton", scrimButton);
        UiWiring.Set(component, "panel", sheetRect);
        UiWiring.Set(component, "icon", icon);
        UiWiring.Set(component, "iconLoop", iconLoop);
        UiWiring.Set(component, "subLabel", subLabel);
        UiWiring.Set(component, "shadow", shadow);
        UiWiring.Set(component, "painting", painting);
        UiWiring.Set(component, "flash", flash);
        UiWiring.Set(component, "bodyLabel", bodyLabel);
        UiWiring.Set(component, "primaryButton", primaryButton);
        UiWiring.Set(component, "primaryGradient", primaryGradient);
        UiWiring.Set(component, "primaryShadow", primaryShadow);
        UiWiring.Set(component, "primaryLabel", primaryLabel);
        UiWiring.Set(component, "secondaryButton", secondaryButton);
        UiWiring.Set(component, "secondaryLabel", secondaryLabel);
        UiWiring.Set(component, "theme", theme);

        root.gameObject.SetActive(false);
        return component;
    }

    private static void BuildGrabber(RectTransform sheet, UiTheme theme)
    {
        RectTransform band = UiFactory.StretchTop(UiFactory.Rect("GrabberBand", sheet), 0f, GrabberBandHeight, 0f, 0f);

        Image grabber = UiFactory.Image("Grabber", band, theme.Rounded(5), theme.borderSoft);
        RectTransform grabberRect = grabber.rectTransform;
        grabberRect.anchorMin = new Vector2(0.5f, 0.5f);
        grabberRect.anchorMax = new Vector2(0.5f, 0.5f);
        grabberRect.pivot = new Vector2(0.5f, 0.5f);
        grabberRect.anchoredPosition = Vector2.zero;
        grabberRect.sizeDelta = new Vector2(40f, 5f);
    }

    // Ikon + eyebrow + baslik + alt satir + tablo kutusu + govde metni --
    // hepsi tek bir kaydirmali sutun, PaintingChoiceSheetBuilder.BuildOptions
    // ile ayni "Options" kalibi (ScrollView + FitVertical).
    private static void BuildBody(
        RectTransform sheet,
        UiTheme theme,
        out Image icon,
        out SpriteFrameLoop iconLoop,
        out TMP_Text subLabel,
        out Image shadow,
        out Image painting,
        out Image flash,
        out TMP_Text bodyLabel)
    {
        RectTransform scroll = UiFactory.Fill(UiFactory.Rect("Body", sheet), GrabberBandHeight, 0f, 0f, FooterHeight);
        RectTransform content = UiFactory.ScrollView(scroll, out _, out _);

        VerticalLayoutGroup column = UiFactory.Column(content, 18f, TextAnchor.UpperCenter);
        column.padding = new RectOffset(22, 22, 10, 24);
        UiFactory.FitVertical(content.gameObject);

        RectTransform iconRow = CenteredRow(content, "IconRow", 64f);
        icon = UiFactory.Image("Icon", iconRow, null, Color.white, false);
        UiFactory.Size(icon.gameObject, 64f, 64f);
        icon.preserveAspect = true;
        iconLoop = icon.gameObject.AddComponent<SpriteFrameLoop>();
        UiWiring.Set(iconLoop, "image", icon);

        // Strings.tsv'de her dil icin ZATEN buyuk harfle yazili (FİLTRELER/
        // PLAN/ESERLER ile ayni kalip) -- burada ikinci bir ToUpperInvariant
        // gerekmiyor.
        TMP_Text eyebrow = UiFactory.LocalizedText("Eyebrow", content, "rotus_restore.eyebrow", theme.semiBold, 12f,
            theme.rotusBlueLight, TextAlignmentOptions.Center);
        UiFactory.Size(eyebrow.gameObject, -1f, 16f);

        TMP_Text title = UiFactory.LocalizedText("Title", content, "rotus_restore.title", theme.bold, 24f,
            theme.textPrimary, TextAlignmentOptions.Center);
        UiFactory.Size(title.gameObject, -1f, 30f);

        subLabel = UiFactory.Text("Sub", content, string.Empty, theme.regular, 13f,
            theme.textMuted, TextAlignmentOptions.Center);
        UiFactory.Size(subLabel.gameObject, -1f, 18f);

        BuildPaintingBox(content, theme, out shadow, out painting, out flash);

        bodyLabel = UiFactory.Text("Body", content, string.Empty, theme.regular, 13f,
            theme.textMuted, TextAlignmentOptions.Center);
        bodyLabel.textWrappingMode = TextWrappingModes.Normal;
        UiFactory.Size(bodyLabel.gameObject, 280f, 40f);
    }

    // Kolonun tam genisligini alip TEK cocugunu ortalayan ara satir.
    //
    // Gerekli, cunku UiFactory.Column childForceExpandWidth=true kuruyor (metin
    // bloklari tam genisligi kullanip ortalanabilsin diye) -- ama ikon 64x64,
    // tablo kutusu 228x198 kare/sabit olmali. Dogrudan kolonun cocugu olsalardi
    // ikisi de kolonun tam genisligine yayilirdi (ikon da buyumus, dikeyde
    // "basilmis" gorunurdu). StreakCelebrationBuilder.CenteredRow ile AYNI
    // kalip, ayni gerekce.
    private static RectTransform CenteredRow(RectTransform parent, string name, float height)
    {
        RectTransform row = UiFactory.Rect(name, parent);
        UiFactory.Row(row, 0f, TextAnchor.MiddleCenter);
        UiFactory.Size(row.gameObject, -1f, height);

        return row;
    }

    // Tablo kutusu: dolgu 14, yaricap 18, koyu zemin. Tablonun kendisi
    // (golge + sprite + yanik hucre cerceveleri + onarim flas'i) merkezde,
    // boyutu RotusRestoreSheet.BindPainting tarafindan calisma aninda yazilir.
    private static void BuildPaintingBox(
        RectTransform parent, UiTheme theme, out Image shadow, out Image painting, out Image flash)
    {
        RectTransform row = CenteredRow(parent, "PaintingBoxRow", PaintingBoxHeight);
        RectTransform box = UiFactory.Rect("PaintingBox", row);
        UiFactory.Size(box.gameObject, PaintingBoxWidth, PaintingBoxHeight);

        Image background = UiFactory.Image("Background", box, theme.Rounded(18), new Color32(0x14, 0x0A, 0x04, 0x66));
        UiFactory.Fill(background.rectTransform);

        shadow = UiFactory.Image("Shadow", box, theme.shadowBox, theme.softShadow);
        shadow.fillCenter = false;
        CenterRect(shadow.rectTransform);

        painting = UiFactory.Image("Painting", box, null, Color.white, false);
        CenterRect(painting.rectTransform);

        flash = UiFactory.Image("Flash", painting.rectTransform, theme.radialGlow, theme.rotusGlow, false);
        UiFactory.Fill(flash.rectTransform);
        flash.gameObject.SetActive(false);
    }

    private static void CenterRect(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
    }

    // Birincil + ikincil dugme ALT ALTA, ikisi de tam genislikte -- teslim
    // dosyasinin "width:100%; display:flex; flex-direction:column; gap:10"
    // dizilimi, SettingsPanelBuilder.BuildConfirmSheet'in YAN YANA (Row)
    // footer'indan FARKLI.
    private static void BuildFooter(
        RectTransform sheet,
        UiTheme theme,
        out Button primaryButton,
        out UiGradient primaryGradient,
        out Shadow primaryShadow,
        out TMP_Text primaryLabel,
        out Button secondaryButton,
        out TMP_Text secondaryLabel)
    {
        RectTransform footer = UiFactory.StretchBottom(UiFactory.Rect("Footer", sheet), 16f, FooterHeight - 16f, 22f, 22f);
        UiFactory.Column(footer, 10f, TextAnchor.UpperCenter);

        Image primaryBackground = UiFactory.Image("Primary", footer, theme.Rounded(16), Color.white);
        LayoutElement primaryElement = primaryBackground.gameObject.AddComponent<LayoutElement>();
        primaryElement.preferredHeight = 52f;
        primaryGradient = UiFactory.Gradient(primaryBackground.gameObject, theme.goldButtonTop, theme.goldButtonBottom);
        primaryShadow = UiFactory.HardShadow(primaryBackground.gameObject, 4f, theme.goldButtonShadow);
        primaryButton = UiFactory.Button(primaryBackground);

        // "Bir parçayı onar" BILEREK LocalizedText olmuyor -- RotusRestoreSheet
        // bu etiketi acilista hemen durum basina (rotus_restore.primary_can_repair/
        // primary_fully_repaired/primary_no_rotus) yeniden yaziyor.
        primaryLabel = UiFactory.Text("Label", primaryBackground.rectTransform, "Bir parçayı onar",
            theme.semiBold, 17f, theme.textOnGold, TextAlignmentOptions.Center);
        UiFactory.Fill(primaryLabel.rectTransform);

        Image secondaryBackground = UiFactory.Image("Secondary", footer, null, Color.clear, false);
        LayoutElement secondaryElement = secondaryBackground.gameObject.AddComponent<LayoutElement>();
        secondaryElement.preferredHeight = 44f;
        secondaryButton = UiFactory.Button(secondaryBackground);

        // Ayni gerekce: "Kapat"/"Vazgeç" arasinda durum basina geciyor
        // (common.close_button/common.cancel_button, Faz 4'te RotusRestoreSheet.cs).
        secondaryLabel = UiFactory.Text("Label", secondaryBackground.rectTransform, "Vazgeç",
            theme.medium, 15f, theme.textMuted, TextAlignmentOptions.Center);
        UiFactory.Fill(secondaryLabel.rectTransform);
    }
}
