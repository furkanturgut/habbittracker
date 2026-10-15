using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Ayarlar > "Kategoriler ve filtreler" sayfasinin kurucusu.
//
// SettingsPanelBuilder.BuildConfirmSheet'in sablonu (scrim + alttan kayan
// kutu) + PaintingChoiceSheetBuilder'in ScrollView/Grid kalibi -- ayri dosya,
// SettingsPanelBuilder zaten 500 satir butcesine yakin (TimePickerSheetBuilder
// ile ayni gerekce).
public static class CategorySettingsSheetBuilder
{
    private const float SheetHeight = 640f;
    private const float SheetTopMargin = 40f;
    private const float TopBarHeight = 56f;

    public static CategorySettingsSheet Build(RectTransform parent, UiTheme theme, HabbitManager manager)
    {
        RectTransform root = UiFactory.Fill(UiFactory.Rect("CategorySettingsSheet", parent));

        Image scrim = UiFactory.Image("Scrim", root, theme.white, theme.scrim, false);
        UiFactory.Fill(scrim.rectTransform);
        Button scrimButton = UiFactory.Button(scrim);
        CanvasGroup scrimGroup = scrim.gameObject.AddComponent<CanvasGroup>();

        Image sheet = UiFactory.Image("Sheet", root, theme.Rounded(24), theme.surface);
        RectTransform sheetRect = UiFactory.StretchBottom(sheet.rectTransform, 0f, SheetHeight, 0f, 0f);
        sheet.raycastTarget = true;

        MaxHeightFitter fitter = sheet.gameObject.AddComponent<MaxHeightFitter>();
        UiWiring.SetFloat(fitter, "designHeight", SheetHeight);
        UiWiring.SetFloat(fitter, "topMargin", SheetTopMargin);

        Image topBorder = UiFactory.Image("TopBorder", sheetRect, theme.white, theme.borderGold, false);
        UiFactory.StretchTop(topBorder.rectTransform, 0f, 2f, 0f, 0f);

        Button closeButton = BuildTopBar(sheetRect, theme);

        BuildContent(
            sheetRect, theme,
            out TMP_Text selectionCountLabel,
            out OptionButton filterChipPrefab,
            out RectTransform filterChipRoot,
            out CategoryCustomRow customRowPrefab,
            out RectTransform customRowRoot,
            out GameObject customEmptyHint);

        CategorySettingsSheet component = root.gameObject.AddComponent<CategorySettingsSheet>();
        UiWiring.Set(component, "manager", manager);
        UiWiring.Set(component, "scrimGroup", scrimGroup);
        UiWiring.Set(component, "scrimButton", scrimButton);
        UiWiring.Set(component, "panel", sheetRect);
        UiWiring.Set(component, "closeButton", closeButton);
        UiWiring.Set(component, "selectionCountLabel", selectionCountLabel);
        UiWiring.Set(component, "filterChipPrefab", filterChipPrefab);
        UiWiring.Set(component, "filterChipRoot", filterChipRoot);
        UiWiring.Set(component, "customRowPrefab", customRowPrefab);
        UiWiring.Set(component, "customRowRoot", customRowRoot);
        UiWiring.Set(component, "customEmptyHint", customEmptyHint);

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

        TMP_Text title = UiFactory.LocalizedText("Title", bar, "category_settings.title", theme.semiBold, 17f,
            theme.textPrimary, TextAlignmentOptions.Center);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -17f);
        titleRect.sizeDelta = new Vector2(280f, 24f);

        Image close = UiFactory.Image("Close", bar, null, Color.clear, false);
        RectTransform closeRect = close.rectTransform;
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(0f, -8f);
        closeRect.sizeDelta = new Vector2(44f, 28f);
        Button closeButton = UiFactory.Button(close);

        TMP_Text closeLabel = UiFactory.LocalizedText("Label", close.rectTransform, "common.close_button",
            theme.medium, 14f, theme.textMuted, TextAlignmentOptions.Right);
        UiFactory.Fill(closeLabel.rectTransform);

        return closeButton;
    }

    private static void BuildContent(
        RectTransform sheet,
        UiTheme theme,
        out TMP_Text selectionCountLabel,
        out OptionButton filterChipPrefab,
        out RectTransform filterChipRoot,
        out CategoryCustomRow customRowPrefab,
        out RectTransform customRowRoot,
        out GameObject customEmptyHint)
    {
        RectTransform scroll = UiFactory.Fill(UiFactory.Rect("Scroll", sheet), 0f, TopBarHeight, 0f, 0f);

        Image hit = UiFactory.Image("Hit", scroll, null, Color.clear, false);
        UiFactory.Fill(hit.rectTransform);
        hit.raycastTarget = true;

        RectTransform content = UiFactory.ScrollView(scroll, out _, out _);

        VerticalLayoutGroup column = UiFactory.Column(content, 22f);
        column.padding = new RectOffset(20, 20, 14, 32);
        UiFactory.FitVertical(content.gameObject);

        BuildFilterSection(content, theme, out selectionCountLabel, out filterChipRoot);
        filterChipPrefab = BuildFilterChipPrefab(content, theme);

        BuildCustomSection(content, theme, out customRowRoot, out customEmptyHint);
        customRowPrefab = BuildCustomRowTemplate(content, theme);
    }

    // Cip izgarasi: sabit 3 sutun, sabit hucre boyutu -- GridCellFitter
    // KULLANILMIYOR (kareye yakin hucre uretir, cipler 36 birim yukseklikte
    // SABIT kalmali, bkz. CategoryPickerSheetBuilder).
    private static void BuildFilterSection(
        RectTransform content, UiTheme theme, out TMP_Text selectionCountLabel, out RectTransform filterGrid)
    {
        RectTransform section = UiFactory.Rect("Filters", content);
        UiFactory.Column(section, 10f);
        UiFactory.FitVertical(section.gameObject);

        RectTransform header = UiFactory.Rect("Header", section);
        UiFactory.Size(header.gameObject, -1f, 16f);

        TMP_Text caption = UiFactory.LocalizedText("Caption", header, "category_settings.filters_caption", theme.semiBold, 12f, theme.gold);
        UiFactory.Fill(caption.rectTransform);

        selectionCountLabel = UiFactory.Text("Count", header, "0/4", theme.regular, 12f,
            theme.textMuted, TextAlignmentOptions.Right);
        RectTransform countRect = selectionCountLabel.rectTransform;
        countRect.anchorMin = new Vector2(1f, 0.5f);
        countRect.anchorMax = new Vector2(1f, 0.5f);
        countRect.pivot = new Vector2(1f, 0.5f);
        countRect.anchoredPosition = Vector2.zero;
        countRect.sizeDelta = new Vector2(50f, 16f);

        filterGrid = UiFactory.Rect("Grid", section);
        GridLayoutGroup grid = filterGrid.gameObject.AddComponent<GridLayoutGroup>();
        grid.spacing = new Vector2(8f, 8f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.cellSize = new Vector2(98f, 36f);
        UiFactory.FitVertical(filterGrid.gameObject);
    }

    // CategoryPickerSheetBuilder'daki BuildOptions'in cip prefabi ile AYNI
    // gorunum (OptionButton + CategoryPill'in kenarlik/dolgu/etiket deseni) --
    // UiPrefabBuilder.Prefabs'a yeni bir girdi eklemek yerine burada yerel
    // bir sablon olarak kuruluyor, cunku bu cip sadece bu sayfada kullaniliyor.
    private static OptionButton BuildFilterChipPrefab(RectTransform parent, UiTheme theme)
    {
        RectTransform root = UiFactory.Rect("FilterChipTemplate", parent);
        root.sizeDelta = new Vector2(98f, 36f);

        Image border = root.gameObject.AddComponent<Image>();
        border.sprite = theme.Rounded(18);
        border.type = Image.Type.Sliced;
        border.color = theme.borderSoft;
        UiFactory.Button(border);

        Image background = UiFactory.Image("Fill", root, theme.Rounded(16), Color.clear);
        UiFactory.Fill(background.rectTransform, 2f, 2f, 2f, 2f);

        TMP_Text label = UiFactory.Text("Label", root, string.Empty, theme.medium, 13f,
            theme.textPrimary, TextAlignmentOptions.Center);
        UiFactory.Fill(label.rectTransform, 6f, 0f, 6f, 0f);
        label.overflowMode = TextOverflowModes.Ellipsis;

        OptionButton option = root.gameObject.AddComponent<OptionButton>();
        UiWiring.Set(option, "background", background);
        UiWiring.Set(option, "border", border);
        UiWiring.Set(option, "label", label);
        UiWiring.SetColor(option, "selectedBackground", theme.gold);
        UiWiring.SetColor(option, "unselectedBackground", Color.clear);
        UiWiring.SetColor(option, "selectedBorder", theme.gold);
        UiWiring.SetColor(option, "unselectedBorder", theme.borderSoft);
        UiWiring.SetColor(option, "selectedLabel", theme.textOnGold);
        UiWiring.SetColor(option, "unselectedLabel", theme.textPrimary);

        root.gameObject.SetActive(false);
        return option;
    }

    private static void BuildCustomSection(
        RectTransform content, UiTheme theme, out RectTransform customRowRoot, out GameObject emptyHint)
    {
        RectTransform section = UiFactory.Rect("Custom", content);
        UiFactory.Column(section, 10f);
        UiFactory.FitVertical(section.gameObject);

        TMP_Text caption = UiFactory.LocalizedText("Caption", section, "category_settings.custom_caption", theme.semiBold, 12f, theme.gold);
        UiFactory.Size(caption.gameObject, -1f, 16f);

        TMP_Text hint = UiFactory.LocalizedText("EmptyHint", section, "category_settings.custom_empty_state",
            theme.regular, 13f, theme.textMuted);
        UiFactory.Size(hint.gameObject, -1f, 20f);
        emptyHint = hint.gameObject;

        customRowRoot = UiFactory.Rect("Rows", section);
        UiFactory.Column(customRowRoot, 8f);
        UiFactory.FitVertical(customRowRoot.gameObject);
    }

    // Ad girisi (yeniden adlandirma) + sil butonu. Havuzun kaynagi -- sahnede
    // GORUNMEYEN, inaktif tek bir orijinal (CategorySettingsSheet.CustomRowAt
    // bunu Instantiate ile cogaltiyor, PaintingPickerRow.OptionAt kaligi).
    private static CategoryCustomRow BuildCustomRowTemplate(RectTransform parent, UiTheme theme)
    {
        RectTransform row = UiFactory.Rect("CustomRowTemplate", parent);
        UiFactory.Size(row.gameObject, -1f, 50f);

        Image border = UiFactory.Image("Input", row, theme.Rounded(12), theme.borderFaint);
        UiFactory.Fill(border.rectTransform, 0f, 0f, 60f, 0f);
        border.raycastTarget = true;

        RectTransform textArea = UiFactory.Fill(UiFactory.Rect("TextArea", border.rectTransform), 14f, 0f, 14f, 0f);
        textArea.gameObject.AddComponent<RectMask2D>();

        TMP_Text inputText = UiFactory.Text("Text", textArea, string.Empty, theme.medium, 15f, theme.textPrimary);
        UiFactory.Fill(inputText.rectTransform);

        TMP_InputField input = border.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = textArea;
        input.textComponent = (TextMeshProUGUI)inputText;
        input.targetGraphic = border;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = HabitCategories.MaxNameLength;

        Image deleteBackground = UiFactory.Image("Delete", row, theme.Rounded(12), theme.burgundy);
        RectTransform deleteRect = deleteBackground.rectTransform;
        deleteRect.anchorMin = new Vector2(1f, 0.5f);
        deleteRect.anchorMax = new Vector2(1f, 0.5f);
        deleteRect.pivot = new Vector2(1f, 0.5f);
        deleteRect.anchoredPosition = Vector2.zero;
        deleteRect.sizeDelta = new Vector2(50f, 50f);
        Button deleteButton = UiFactory.Button(deleteBackground);
        ButtonPressFeedback.EnsureOn(deleteButton);

        TMP_Text deleteLabel = UiFactory.Text("Label", deleteBackground.rectTransform, "✕",
            theme.semiBold, 14f, theme.textPrimary, TextAlignmentOptions.Center);
        UiFactory.Fill(deleteLabel.rectTransform);

        CategoryCustomRow component = row.gameObject.AddComponent<CategoryCustomRow>();
        UiWiring.Set(component, "nameInput", input);
        UiWiring.Set(component, "deleteButton", deleteButton);

        row.gameObject.SetActive(false);
        return component;
    }
}
