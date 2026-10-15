using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 02 Alisknliklar listesinin sonundaki "Arsiv (N)" satirinin actigi alttan
// acilan kutu.
//
// HabitsPanelBuilder zaten 500 satir butcesine yakin oldugu icin ayri dosya --
// CategorySettingsSheetBuilder'in sablonunu birebir izliyor (scrim + alttan
// kayan kutu + MaxHeightFitter + pooled satir sablonu).
public static class ArchiveSheetBuilder
{
    private const float SheetHeight = 480f;
    private const float SheetTopMargin = 40f;
    private const float TopBarHeight = 64f;

    public static ArchiveSheet Build(
        RectTransform parent,
        UiTheme theme,
        UiPrefabBuilder.Prefabs prefabs,
        HabbitManager manager,
        ScreenNavigator navigator)
    {
        RectTransform root = UiFactory.Fill(UiFactory.Rect("ArchiveSheet", parent));

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

        Button closeButton = BuildTopBar(sheetRect, theme);

        BuildContent(sheetRect, theme, out ArchiveRow rowPrefab, out RectTransform rowRoot, out GameObject emptyHint);

        ArchiveSheet component = root.gameObject.AddComponent<ArchiveSheet>();
        UiWiring.Set(component, "manager", manager);
        UiWiring.Set(component, "navigator", navigator);
        UiWiring.Set(component, "scrimGroup", scrimGroup);
        UiWiring.Set(component, "scrimButton", scrimButton);
        UiWiring.Set(component, "panel", sheetRect);
        UiWiring.Set(component, "closeButton", closeButton);
        UiWiring.Set(component, "rowPrefab", rowPrefab);
        UiWiring.Set(component, "rowRoot", rowRoot);
        UiWiring.Set(component, "emptyHint", emptyHint);

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

        TMP_Text title = UiFactory.LocalizedText("Title", bar, "archive_sheet.title", theme.semiBold, 18f,
            theme.textPrimary, TextAlignmentOptions.Center);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -17f);
        titleRect.sizeDelta = new Vector2(220f, 24f);

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
        out ArchiveRow rowPrefab,
        out RectTransform rowRoot,
        out GameObject emptyHint)
    {
        RectTransform scroll = UiFactory.Fill(UiFactory.Rect("Scroll", sheet), 0f, TopBarHeight, 0f, 0f);

        Image hit = UiFactory.Image("Hit", scroll, null, Color.clear, false);
        UiFactory.Fill(hit.rectTransform);
        hit.raycastTarget = true;

        RectTransform content = UiFactory.ScrollView(scroll, out _, out _);

        VerticalLayoutGroup column = UiFactory.Column(content, 10f);
        column.padding = new RectOffset(20, 20, 14, 32);
        UiFactory.FitVertical(content.gameObject);

        TMP_Text hint = UiFactory.LocalizedText("EmptyHint", content, "archive_sheet.empty_state",
            theme.regular, 13f, theme.textMuted);
        UiFactory.Size(hint.gameObject, -1f, 20f);
        emptyHint = hint.gameObject;

        rowRoot = UiFactory.Rect("Rows", content);
        UiFactory.Column(rowRoot, 8f);
        UiFactory.FitVertical(rowRoot.gameObject);

        rowPrefab = BuildRowTemplate(rowRoot, theme);
    }

    // Havuzun kaynagi -- sahnede GORUNMEYEN, inaktif tek bir orijinal
    // (ArchiveSheet.RowAt bunu Instantiate ile cogaltiyor, CategorySettingsSheet.
    // CustomRowAt kaligi).
    private static ArchiveRow BuildRowTemplate(RectTransform parent, UiTheme theme)
    {
        RectTransform row = UiFactory.Rect("ArchiveRowTemplate", parent);
        UiFactory.Size(row.gameObject, -1f, 64f);

        Image background = UiFactory.Image("Background", row, theme.Rounded(14), theme.surface);
        UiFactory.Fill(background.rectTransform);
        Button hitButton = UiFactory.Button(background);

        TMP_Text title = UiFactory.Text("Title", row, string.Empty, theme.semiBold, 15f, theme.textPrimary);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(0f, 0.5f);
        titleRect.pivot = new Vector2(0f, 0.5f);
        titleRect.anchoredPosition = new Vector2(16f, 10f);
        titleRect.sizeDelta = new Vector2(180f, 20f);
        title.overflowMode = TextOverflowModes.Ellipsis;

        TMP_Text streak = UiFactory.Text("Streak", row, string.Empty, theme.regular, 12f, theme.textMuted);
        RectTransform streakRect = streak.rectTransform;
        streakRect.anchorMin = new Vector2(0f, 0.5f);
        streakRect.anchorMax = new Vector2(0f, 0.5f);
        streakRect.pivot = new Vector2(0f, 0.5f);
        streakRect.anchoredPosition = new Vector2(16f, -10f);
        streakRect.sizeDelta = new Vector2(120f, 16f);

        // Geri al: ayni notr ton (burgundy degil, Sil ile karistirilmamali).
        Image unarchive = UiFactory.Image("Unarchive", row, theme.Rounded(12), Color.white);
        RectTransform unarchiveRect = unarchive.rectTransform;
        unarchiveRect.anchorMin = new Vector2(1f, 0.5f);
        unarchiveRect.anchorMax = new Vector2(1f, 0.5f);
        unarchiveRect.pivot = new Vector2(1f, 0.5f);
        unarchiveRect.anchoredPosition = new Vector2(-12f, 0f);
        unarchiveRect.sizeDelta = new Vector2(84f, 40f);
        UiFactory.Gradient(unarchive.gameObject, theme.cardTop, theme.cardBottom);
        Button unarchiveButton = UiFactory.Button(unarchive);
        ButtonPressFeedback.EnsureOn(unarchiveButton);

        TMP_Text unarchiveLabel = UiFactory.LocalizedText("Label", unarchive.rectTransform, "common.unarchive_button",
            theme.semiBold, 13f, theme.textPrimary, TextAlignmentOptions.Center);
        UiFactory.Fill(unarchiveLabel.rectTransform);

        ArchiveRow component = row.gameObject.AddComponent<ArchiveRow>();
        UiWiring.Set(component, "hitButton", hitButton);
        UiWiring.Set(component, "titleLabel", title);
        UiWiring.Set(component, "streakLabel", streak);
        UiWiring.Set(component, "unarchiveButton", unarchiveButton);

        row.gameObject.SetActive(false);
        return component;
    }
}
