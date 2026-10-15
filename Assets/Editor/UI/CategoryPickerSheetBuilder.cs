using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Formdaki "Kategori" satirini acan alttan acilan kutu.
//
// HabitsPanelBuilder zaten 500 satir butcesine yakin oldugu icin ayri dosya --
// PaintingChoiceSheetBuilder'in sablonunu birebir izliyor (scrim + alttan
// kayan kutu + MaxHeightFitter).
public static class CategoryPickerSheetBuilder
{
    private const float SheetHeight = 520f;
    private const float SheetTopMargin = 40f;
    private const float TopBarHeight = 64f;
    private const float FooterHeight = 96f;
    private const float CreateRowHeight = 56f;

    public static CategoryPickerSheet Build(
        RectTransform parent,
        UiTheme theme,
        UiPrefabBuilder.Prefabs prefabs,
        HabbitManager manager,
        ScreenNavigator navigator)
    {
        RectTransform root = UiFactory.Fill(UiFactory.Rect("CategoryPickerSheet", parent));

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

        BuildOptions(
            sheetRect, theme, prefabs,
            out CategoryChipRow chipRow,
            out GameObject createRow,
            out TMP_InputField createInput,
            out Button createConfirmButton,
            out TMP_Text createHint);

        BuildFooter(sheetRect, theme, out Button confirmButton, out Image confirmBackground);

        CategoryPickerSheet component = root.gameObject.AddComponent<CategoryPickerSheet>();
        UiWiring.Set(component, "manager", manager);
        UiWiring.Set(component, "navigator", navigator);
        UiWiring.Set(component, "scrimGroup", scrimGroup);
        UiWiring.Set(component, "scrimButton", scrimButton);
        UiWiring.Set(component, "panel", sheetRect);
        UiWiring.Set(component, "cancelButton", cancelButton);
        UiWiring.Set(component, "chipRow", chipRow);
        UiWiring.Set(component, "createRow", createRow);
        UiWiring.Set(component, "createInput", createInput);
        UiWiring.Set(component, "createConfirmButton", createConfirmButton);
        UiWiring.Set(component, "createHint", createHint);
        UiWiring.Set(component, "confirmButton", confirmButton);
        UiWiring.Set(component, "confirmBackground", confirmBackground);
        UiWiring.Set(component, "theme", theme);

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

        TMP_Text title = UiFactory.LocalizedText("Title", bar, "category_picker.title", theme.semiBold, 18f,
            theme.textPrimary, TextAlignmentOptions.Center);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -15f);
        titleRect.sizeDelta = new Vector2(220f, 24f);

        return cancelButton;
    }

    // Cip izgarasi: sabit 3 sutun, sabit hucre boyutu (GridCellFitter
    // KULLANILMIYOR -- o kareye yakin hucre uretir, cipler 36 birim yukseklikte
    // SABIT kalmali). 3*98 + 2*8 = 310, 326 birimlik ic alana (366 - 2*20) sigar.
    private static void BuildOptions(
        RectTransform sheet,
        UiTheme theme,
        UiPrefabBuilder.Prefabs prefabs,
        out CategoryChipRow chipRow,
        out GameObject createRow,
        out TMP_InputField createInput,
        out Button createConfirmButton,
        out TMP_Text createHint)
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
        gridGroup.constraintCount = 3;
        gridGroup.cellSize = new Vector2(98f, 36f);
        UiFactory.FitVertical(grid.gameObject);

        chipRow = grid.gameObject.AddComponent<CategoryChipRow>();
        UiWiring.Set(chipRow, "chipPrefab", prefabs.CategoryPill.GetComponent<OptionButton>());
        UiWiring.Set(chipRow, "root", grid);

        createRow = BuildCreateRow(content, theme, out createInput, out createConfirmButton, out createHint);
        createRow.SetActive(false);
    }

    private static GameObject BuildCreateRow(
        RectTransform parent,
        UiTheme theme,
        out TMP_InputField input,
        out Button confirmButton,
        out TMP_Text hint)
    {
        RectTransform row = UiFactory.Rect("CreateRow", parent);
        UiFactory.Size(row.gameObject, -1f, CreateRowHeight);

        Image border = UiFactory.Image("Input", row, theme.Rounded(14), theme.borderFaint);
        UiFactory.StretchTop(border.rectTransform, 0f, 50f, 0f, 70f);
        border.raycastTarget = true;

        Image background = UiFactory.Image("Fill", border.rectTransform, theme.Rounded(12), theme.background);
        UiFactory.Fill(background.rectTransform, 2f, 2f, 2f, 2f);

        RectTransform textArea = UiFactory.Fill(UiFactory.Rect("TextArea", border.rectTransform), 14f, 0f, 14f, 0f);
        textArea.gameObject.AddComponent<RectMask2D>();

        TMP_Text placeholderText = UiFactory.LocalizedText("Placeholder", textArea, "category_picker.create_row.placeholder",
            theme.medium, 15f, theme.textFaint);
        UiFactory.Fill(placeholderText.rectTransform);

        TMP_Text inputText = UiFactory.Text("Text", textArea, string.Empty, theme.medium, 15f, theme.textPrimary);
        UiFactory.Fill(inputText.rectTransform);

        input = border.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = textArea;
        input.textComponent = (TextMeshProUGUI)inputText;
        input.placeholder = placeholderText;
        input.targetGraphic = border;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = HabitCategories.MaxNameLength;

        Image confirm = UiFactory.Image("Add", row, theme.Rounded(12), Color.white);
        RectTransform confirmRect = confirm.rectTransform;
        confirmRect.anchorMin = new Vector2(1f, 1f);
        confirmRect.anchorMax = new Vector2(1f, 1f);
        confirmRect.pivot = new Vector2(1f, 1f);
        confirmRect.anchoredPosition = Vector2.zero;
        confirmRect.sizeDelta = new Vector2(60f, 50f);
        UiFactory.Gradient(confirm.gameObject, theme.goldButtonTop, theme.goldButtonBottom);
        confirmButton = UiFactory.Button(confirm);

        TMP_Text confirmLabel = UiFactory.LocalizedText("Label", confirm.rectTransform, "common.add_button",
            theme.semiBold, 14f, theme.textOnGold, TextAlignmentOptions.Center);
        UiFactory.Fill(confirmLabel.rectTransform);

        hint = UiFactory.Text("Hint", row, string.Empty, theme.regular, 12f, theme.quitColor);
        RectTransform hintRect = hint.rectTransform;
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(1f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.anchoredPosition = Vector2.zero;
        hintRect.sizeDelta = new Vector2(0f, 16f);

        return row.gameObject;
    }

    private static void BuildFooter(
        RectTransform sheet,
        UiTheme theme,
        out Button confirmButton,
        out Image confirmBackground)
    {
        RectTransform footer = UiFactory.StretchBottom(UiFactory.Rect("Footer", sheet), 0f, FooterHeight, 20f, 20f);

        confirmBackground = UiFactory.Image("Confirm", footer, theme.Rounded(14), Color.white);
        UiFactory.StretchBottom(confirmBackground.rectTransform, 30f, 54f, 0f, 0f);
        UiFactory.Gradient(confirmBackground.gameObject, theme.goldButtonTop, theme.goldButtonBottom);
        UiFactory.HardShadow(confirmBackground.gameObject, 4f, theme.goldButtonShadow);
        confirmButton = UiFactory.Button(confirmBackground);

        TMP_Text label = UiFactory.LocalizedText("Label", confirmBackground.rectTransform, "category_picker.confirm_button",
            theme.semiBold, 16f, theme.textOnGold, TextAlignmentOptions.Center);
        UiFactory.Fill(label.rectTransform);
    }
}
