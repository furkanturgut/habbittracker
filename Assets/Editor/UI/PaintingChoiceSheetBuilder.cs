using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Biten bir tablonun yerine siradakini sectiren alttan acilan kutu.
//
// WorkshopSceneBuilder zaten 500 satir sinirina yakin oldugu icin ayri dosya.
// HabitsPanelBuilder.BuildSheet / BuildPaintingSection / BuildSheetFooter / Section
// kaliplarini birebir izliyor.
public static class PaintingChoiceSheetBuilder
{
    private const float SheetHeight = 520f;
    private const float SheetTopMargin = 40f;
    private const float TopBarHeight = 64f;
    private const float FooterHeight = 96f;

    public static PaintingChoiceSheet Build(
        RectTransform parent,
        UiTheme theme,
        UiPrefabBuilder.Prefabs prefabs,
        HabbitManager manager,
        ScreenNavigator navigator)
    {
        RectTransform root = UiFactory.Fill(UiFactory.Rect("PaintingChoiceSheet", parent));

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

        BuildTopBar(sheetRect, theme, out TMP_Text sheetTitle, out TMP_Text subtitle, out Button cancelButton);

        PaintingPickerRow row = BuildOptions(sheetRect, theme, prefabs);

        BuildFooter(sheetRect, theme, out Button confirmButton, out Image confirmBackground);

        PaintingChoiceSheet component = root.gameObject.AddComponent<PaintingChoiceSheet>();
        UiWiring.Set(component, "manager", manager);
        UiWiring.Set(component, "navigator", navigator);
        UiWiring.Set(component, "scrimGroup", scrimGroup);
        UiWiring.Set(component, "scrimButton", scrimButton);
        UiWiring.Set(component, "panel", sheetRect);
        UiWiring.Set(component, "sheetTitle", sheetTitle);
        UiWiring.Set(component, "subtitle", subtitle);
        UiWiring.Set(component, "cancelButton", cancelButton);
        UiWiring.Set(component, "paintingRow", row);
        UiWiring.Set(component, "confirmButton", confirmButton);
        UiWiring.Set(component, "confirmBackground", confirmBackground);
        UiWiring.Set(component, "theme", theme);

        root.gameObject.SetActive(false);
        return component;
    }

    private static void BuildTopBar(
        RectTransform sheet,
        UiTheme theme,
        out TMP_Text title,
        out TMP_Text subtitle,
        out Button cancelButton)
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
        cancelButton = UiFactory.Button(cancel);
        TMP_Text cancelLabel = UiFactory.LocalizedText("Label", cancel.rectTransform, "common.cancel_button",
            theme.medium, 15f, theme.textMuted);
        UiFactory.Fill(cancelLabel.rectTransform);

        title = UiFactory.LocalizedText("Title", bar, "painting_choice.title", theme.semiBold, 18f,
            theme.textPrimary, TextAlignmentOptions.Center);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -15f);
        titleRect.sizeDelta = new Vector2(220f, 24f);

        subtitle = UiFactory.Text("Subtitle", bar, string.Empty, theme.regular, 13f,
            theme.textMuted, TextAlignmentOptions.Center);
        RectTransform subtitleRect = subtitle.rectTransform;
        subtitleRect.anchorMin = new Vector2(0.5f, 1f);
        subtitleRect.anchorMax = new Vector2(0.5f, 1f);
        subtitleRect.pivot = new Vector2(0.5f, 1f);
        subtitleRect.anchoredPosition = new Vector2(0f, -37f);
        subtitleRect.sizeDelta = new Vector2(280f, 18f);
    }

    // Secim izgarasi: PaintingChoiceSheet.Open onceden filtrelenmis listeyi
    // PaintingPickerRow'un yeni asiri yukune veriyor -- bu satir yalnizca
    // kutuyu kuruyor, "hangi tablolar" karari PaintingChoices.Collect'in isi.
    private static PaintingPickerRow BuildOptions(
        RectTransform sheet,
        UiTheme theme,
        UiPrefabBuilder.Prefabs prefabs)
    {
        RectTransform scroll = UiFactory.Fill(UiFactory.Rect("Options", sheet), 0f, TopBarHeight, 0f, FooterHeight);
        RectTransform content = UiFactory.ScrollView(scroll, out _, out _);

        VerticalLayoutGroup column = UiFactory.Column(content, 12f);
        column.padding = new RectOffset(20, 20, 14, 20);
        UiFactory.FitVertical(content.gameObject);

        RectTransform grid = UiFactory.Rect("Grid", content);
        GridLayoutGroup gridGroup = grid.gameObject.AddComponent<GridLayoutGroup>();
        gridGroup.spacing = new Vector2(8f, 8f);

        // GridCellFitter spacing'i Apply()'da okuyor; spacing'i ONDAN ONCE
        // yazmak gerekiyor (bkz. HabitsPanelBuilder.BuildPaintingSection).
        GridCellFitter fitter = grid.gameObject.AddComponent<GridCellFitter>();
        UiWiring.SetInt(fitter, "columns", 3);
        UiWiring.SetFloat(fitter, "extraHeight", 22f);

        UiFactory.FitVertical(grid.gameObject);

        PaintingPickerRow row = grid.gameObject.AddComponent<PaintingPickerRow>();
        UiWiring.Set(row, "optionPrefab", prefabs.PaintingOption.GetComponent<PaintingOption>());
        UiWiring.Set(row, "optionRoot", grid);

        return row;
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

        TMP_Text label = UiFactory.LocalizedText("Label", confirmBackground.rectTransform, "painting_choice.confirm_button",
            theme.semiBold, 16f, theme.textOnGold, TextAlignmentOptions.Center);
        UiFactory.Fill(label.rectTransform);
    }
}
