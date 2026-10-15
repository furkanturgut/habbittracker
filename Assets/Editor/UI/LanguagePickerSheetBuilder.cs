using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Ayarlar'daki "Dil" satirini acan alttan acilan kutu.
//
// CategoryPickerSheetBuilder'in AYNI sablonunu izliyor (scrim + alttan kayan
// kutu + MaxHeightFitter) ama iki fark var: "+ olustur" satiri YOK
// (AppLanguageNames.Selectable sabit 8 elemanli) ve navigator parametresi
// YOK -- CategorySettingsSheetBuilder/TimePickerSheetBuilder ile AYNI
// gerekce, bu sheet Ayarlar panelinin KENDI icinde aciliyor. Cip prefabi de
// CategoryPickerSheetBuilder'la AYNI (prefabs.CategoryPill): ikisi de duz
// metinli bir secim pili, ikinci bir prefab uretmeye gerek yok.
public static class LanguagePickerSheetBuilder
{
    private const float SheetHeight = 420f;
    private const float SheetTopMargin = 40f;
    private const float TopBarHeight = 64f;
    private const float FooterHeight = 96f;

    public static LanguagePickerSheet Build(
        RectTransform parent,
        UiTheme theme,
        UiPrefabBuilder.Prefabs prefabs)
    {
        RectTransform root = UiFactory.Fill(UiFactory.Rect("LanguagePickerSheet", parent));

        Image scrim = UiFactory.Image("Scrim", root, theme.white, theme.scrim, false);
        UiFactory.Fill(scrim.rectTransform);
        Button scrimButton = UiFactory.Button(scrim);
        CanvasGroup scrimGroup = scrim.gameObject.AddComponent<CanvasGroup>();

        Image sheet = UiFactory.Image("Sheet", root, theme.Rounded(30), theme.surface);
        RectTransform sheetRect = UiFactory.StretchBottom(sheet.rectTransform, 0f, SheetHeight, 0f, 0f);
        sheet.raycastTarget = true;

        MaxHeightFitter fitter = sheet.gameObject.AddComponent<MaxHeightFitter>();
        UiWiring.SetFloat(fitter, "designHeight", SheetHeight);
        UiWiring.SetFloat(fitter, "topMargin", SheetTopMargin);

        Image topBorder = UiFactory.Image("TopBorder", sheetRect, theme.white, theme.borderGold, false);
        UiFactory.StretchTop(topBorder.rectTransform, 0f, 2f, 0f, 0f);

        Button cancelButton = BuildTopBar(sheetRect, theme);
        LanguageChipRow chipRow = BuildOptions(sheetRect, theme, prefabs);
        Button confirmButton = BuildFooter(sheetRect, theme);

        LanguagePickerSheet component = root.gameObject.AddComponent<LanguagePickerSheet>();
        UiWiring.Set(component, "scrimGroup", scrimGroup);
        UiWiring.Set(component, "scrimButton", scrimButton);
        UiWiring.Set(component, "panel", sheetRect);
        UiWiring.Set(component, "cancelButton", cancelButton);
        UiWiring.Set(component, "chipRow", chipRow);
        UiWiring.Set(component, "confirmButton", confirmButton);

        root.gameObject.SetActive(false);
        return component;
    }

    private static Button BuildTopBar(RectTransform sheet, UiTheme theme)
    {
        RectTransform bar = UiFactory.StretchTop(UiFactory.Rect("TopBar", sheet), 10f, TopBarHeight, 20f, 20f);

        Image grabber = UiFactory.Image("Grabber", bar, theme.Rounded(5), theme.borderSoft);
        RectTransform grabberRect = grabber.rectTransform;
        grabberRect.anchorMin = new Vector2(0.5f, 1f);
        grabberRect.anchorMax = new Vector2(0.5f, 1f);
        grabberRect.pivot = new Vector2(0.5f, 1f);
        grabberRect.anchoredPosition = Vector2.zero;
        grabberRect.sizeDelta = new Vector2(40f, 5f);

        Image cancel = UiFactory.Image("Cancel", bar, null, Color.clear, false);
        UiFactory.TopLeft(cancel.rectTransform, 0f, 17f, 60f, 24f);
        Button cancelButton = UiFactory.Button(cancel);
        TMP_Text cancelLabel = UiFactory.LocalizedText("Label", cancel.rectTransform, "common.cancel_button",
            theme.medium, 15f, theme.textMuted);
        UiFactory.Fill(cancelLabel.rectTransform);

        TMP_Text title = UiFactory.LocalizedText("Title", bar, "language_picker.title", theme.semiBold, 18f,
            theme.textPrimary, TextAlignmentOptions.Center);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -15f);
        titleRect.sizeDelta = new Vector2(220f, 24f);

        return cancelButton;
    }

    // Cip izgarasi: sabit 2 sutun, sabit hucre boyutu (CategoryPickerSheetBuilder.
    // BuildOptions'daki AYNI gerekce -- cipler sabit yukseklikte kalmali). Dil
    // adlari ("Português (Brasil)", "Русский") kategori adlarindan uzun
    // olabildigi icin kategori cipinin 3 sutunu yerine 2 sutun, daha genis
    // hucre (159 birim): 2*159 + 8 = 326, 326 birimlik ic alana (366 - 2*20) sigar.
    private static LanguageChipRow BuildOptions(
        RectTransform sheet, UiTheme theme, UiPrefabBuilder.Prefabs prefabs)
    {
        RectTransform scroll = UiFactory.Fill(UiFactory.Rect("Options", sheet), 0f, TopBarHeight, 0f, FooterHeight);
        RectTransform content = UiFactory.ScrollView(scroll, out _, out _);

        VerticalLayoutGroup column = UiFactory.Column(content, 12f);
        column.padding = new RectOffset(20, 20, 14, 20);
        UiFactory.FitVertical(content.gameObject);

        RectTransform grid = UiFactory.Rect("Grid", content);
        GridLayoutGroup gridGroup = grid.gameObject.AddComponent<GridLayoutGroup>();
        gridGroup.spacing = new Vector2(8f, 8f);
        gridGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridGroup.constraintCount = 2;
        gridGroup.cellSize = new Vector2(159f, 40f);
        UiFactory.FitVertical(grid.gameObject);

        LanguageChipRow chipRow = grid.gameObject.AddComponent<LanguageChipRow>();
        UiWiring.Set(chipRow, "chipPrefab", prefabs.CategoryPill.GetComponent<OptionButton>());
        UiWiring.Set(chipRow, "root", grid);

        return chipRow;
    }

    private static Button BuildFooter(RectTransform sheet, UiTheme theme)
    {
        RectTransform footer = UiFactory.StretchBottom(UiFactory.Rect("Footer", sheet), 0f, FooterHeight, 20f, 20f);

        Image confirmBackground = UiFactory.Image("Confirm", footer, theme.Rounded(14), Color.white);
        UiFactory.StretchBottom(confirmBackground.rectTransform, 30f, 54f, 0f, 0f);
        UiFactory.Gradient(confirmBackground.gameObject, theme.goldButtonTop, theme.goldButtonBottom);
        UiFactory.HardShadow(confirmBackground.gameObject, 4f, theme.goldButtonShadow);
        Button confirmButton = UiFactory.Button(confirmBackground);

        TMP_Text label = UiFactory.LocalizedText("Label", confirmBackground.rectTransform, "language_picker.confirm_button",
            theme.semiBold, 16f, theme.textOnGold, TextAlignmentOptions.Center);
        UiFactory.Fill(label.rectTransform);

        return confirmButton;
    }
}
