using TMPro;
using UnityEngine;
using UnityEngine.UI;

// TimePickerSheet'in Editor kurucusu -- SettingsPanelBuilder.BuildConfirmSheet
// ile AYNI sablon (scrim + alttan kayan kutu), AYRI dosya: SettingsPanelBuilder
// zaten 500 satir butcesine yakin (PaintingChoiceSheetBuilder'la ayni gerekce).
public static class TimePickerSheetBuilder
{
    private const float SheetHeight = 260f;
    private const float ColumnHalfGap = 56f;

    public static TimePickerSheet Build(RectTransform parent, UiTheme theme)
    {
        RectTransform root = UiFactory.Fill(UiFactory.Rect("TimePickerSheet", parent));

        Image scrim = UiFactory.Image("Scrim", root, theme.white, theme.scrim, false);
        UiFactory.Fill(scrim.rectTransform);
        Button scrimButton = UiFactory.Button(scrim);
        CanvasGroup scrimGroup = scrim.gameObject.AddComponent<CanvasGroup>();

        Image sheet = UiFactory.Image("Sheet", root, theme.Rounded(24), theme.surface);
        RectTransform sheetRect = UiFactory.StretchBottom(sheet.rectTransform, 0f, SheetHeight, 0f, 0f);

        Image topBorder = UiFactory.Image("TopBorder", sheetRect, theme.white, theme.borderGold, false);
        UiFactory.StretchTop(topBorder.rectTransform, 0f, 2f, 0f, 0f);

        TMP_Text title = UiFactory.LocalizedText("Title", sheetRect, "time_picker.title", theme.semiBold, 18f,
            theme.textPrimary, TextAlignmentOptions.Center);
        UiFactory.StretchTop(title.rectTransform, 26f, 24f, 24f, 24f);

        RectTransform steppers = UiFactory.TopCenter(UiFactory.Rect("Steppers", sheetRect), 76f, 220f, 96f);

        Button hourUp = BuildStepperColumn(steppers, theme, -ColumnHalfGap, "+", out TMP_Text hourLabel, out Button hourDown);
        Button minuteUp = BuildStepperColumn(steppers, theme, ColumnHalfGap, "+", out TMP_Text minuteLabel, out Button minuteDown);

        TMP_Text separator = UiFactory.Text("Separator", steppers, ":", theme.semiBold, 28f,
            theme.textPrimary, TextAlignmentOptions.Center);
        RectTransform separatorRect = separator.rectTransform;
        separatorRect.anchorMin = new Vector2(0.5f, 0.5f);
        separatorRect.anchorMax = new Vector2(0.5f, 0.5f);
        separatorRect.pivot = new Vector2(0.5f, 0.5f);
        separatorRect.anchoredPosition = Vector2.zero;
        separatorRect.sizeDelta = new Vector2(20f, 36f);

        RectTransform footer = UiFactory.StretchBottom(UiFactory.Rect("Footer", sheetRect), 24f, 54f, 24f, 24f);

        Image confirm = UiFactory.Image("Confirm", footer, theme.Rounded(14), Color.white);
        UiFactory.Fill(confirm.rectTransform);
        UiFactory.Gradient(confirm.gameObject, theme.goldButtonTop, theme.goldButtonBottom);
        Button confirmButton = UiFactory.Button(confirm);

        TMP_Text confirmLabel = UiFactory.LocalizedText("Label", confirm.rectTransform, "common.ok_button", theme.semiBold, 15f,
            theme.textOnGold, TextAlignmentOptions.Center);
        UiFactory.Fill(confirmLabel.rectTransform);

        TimePickerSheet component = root.gameObject.AddComponent<TimePickerSheet>();
        UiWiring.Set(component, "scrimGroup", scrimGroup);
        UiWiring.Set(component, "scrimButton", scrimButton);
        UiWiring.Set(component, "panel", sheetRect);
        UiWiring.Set(component, "hourLabel", hourLabel);
        UiWiring.Set(component, "minuteLabel", minuteLabel);
        UiWiring.Set(component, "hourUpButton", hourUp);
        UiWiring.Set(component, "hourDownButton", hourDown);
        UiWiring.Set(component, "minuteUpButton", minuteUp);
        UiWiring.Set(component, "minuteDownButton", minuteDown);
        UiWiring.Set(component, "confirmButton", confirmButton);

        root.gameObject.SetActive(false);
        return component;
    }

    // Tek bir sutun: ustte "+" dugmesi, ortada deger etiketi, altta "-"
    // dugmesi -- cagiran bunu saat ve dakika icin IKI KEZ cagirir (centerX
    // ile ayni "Steppers" rect'inin solunda/saginda).
    private static Button BuildStepperColumn(
        RectTransform parent, UiTheme theme, float centerX, string upGlyph, out TMP_Text valueLabel, out Button downButton)
    {
        Button upButton = StepperButton(parent, theme, centerX, 30f, upGlyph);
        downButton = StepperButton(parent, theme, centerX, -30f, "-");

        valueLabel = UiFactory.Text("Value", parent, "00", theme.bold, 22f,
            theme.textPrimary, TextAlignmentOptions.Center);
        RectTransform valueRect = valueLabel.rectTransform;
        valueRect.anchorMin = new Vector2(0.5f, 0.5f);
        valueRect.anchorMax = new Vector2(0.5f, 0.5f);
        valueRect.pivot = new Vector2(0.5f, 0.5f);
        valueRect.anchoredPosition = new Vector2(centerX, 0f);
        valueRect.sizeDelta = new Vector2(48f, 28f);

        return upButton;
    }

    private static Button StepperButton(RectTransform parent, UiTheme theme, float centerX, float centerY, string glyph)
    {
        Image background = UiFactory.Image("Stepper " + glyph + " " + centerY, parent, theme.Rounded(18), theme.borderFaint);
        RectTransform rect = background.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(centerX, centerY);
        rect.sizeDelta = new Vector2(36f, 36f);

        TMP_Text label = UiFactory.Text("Label", background.rectTransform, glyph, theme.semiBold, 18f,
            theme.textPrimary, TextAlignmentOptions.Center);
        UiFactory.Fill(label.rectTransform);

        Button button = UiFactory.Button(background);
        ButtonPressFeedback.EnsureOn(button);
        return button;
    }
}
