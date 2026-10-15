using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Ayarlar panelini kurar: sagdan kayan tam ekran katman, `UI Root`'un UC
// PANELDEN SONRAKI cocugu (ScreenNavigator.screens dizisine GIRMIYOR -- bir
// sekme degil).
//
// Satir/kart fabrikasi SettingsRowBuilder'da (ayri dosya, 500 satir butcesi);
// burasi yalnizca blok sirasini ve HabbitManager baglantisini kuruyor.
public static class SettingsPanelBuilder
{
    public const string PanelName = "Panel_Settings";

    public static SettingsScreen Build(
        RectTransform uiRoot, UiTheme theme, UiPrefabBuilder.Prefabs prefabs, HabbitManager manager)
    {
        // Dikeyde gerili, genislikte SABIT (366) -- Fill KULLANMIYORUZ:
        // yatay kayma anchoredPosition.x ile yapiliyor ve bir Fill rect'te
        // (anchorMin.x != anchorMax.x) bu alan anlamsiz kalirdi.
        RectTransform root = UiFactory.Rect(PanelName, uiRoot);
        root.anchorMin = new Vector2(0f, 0f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 0.5f);
        root.sizeDelta = new Vector2(LayoutMetrics.DesignWidth, 0f);
        root.anchoredPosition = new Vector2(LayoutMetrics.DesignWidth, 0f);

        Image background = UiFactory.Image("Background", root, theme.white, Color.white, false);
        UiFactory.Fill(background.rectTransform);
        UiFactory.Gradient(background.gameObject, theme.surface, theme.background);

        BuildHeader(root, theme, prefabs, out Button closeButton);

        RectTransform content = BuildContent(root);

        PremiumCard premiumCard = BuildPremiumBlock(content, theme);
        TMP_InputField nameInput = BuildPainterNameBlock(content, theme);
        BuildReminderBlock(content, theme, out SettingsToggleRow reminderToggle,
            out GameObject timeRow, out Button timeButton, out TMP_Text timeValueLabel);
        BuildExperienceBlock(content, theme, out SettingsToggleRow soundToggle, out SettingsToggleRow hapticToggle);
        SettingsSegmentRow weekStartRow = BuildCalendarBlock(content, theme);
        Button languageButton = BuildLanguageBlock(content, theme, out TMP_Text languageValueLabel);
        Button categoriesButton = BuildCategoriesBlock(content, theme);
        BuildDataBlock(content, theme, out Button exportButton, out GameObject exportPremiumBadge,
            out Button importButton, out Button resetButton);

        TMP_Text versionLabel = UiFactory.Text("Version", content, string.Empty, theme.regular, 12f,
            theme.textFaint, TextAlignmentOptions.Center);
        UiFactory.Size(versionLabel.gameObject, -1f, 16f);

        // CategorySettingsSheet, ConfirmSheet ve TimePickerSheet EN SONDAKI
        // cocuklar: butun ekranin USTUNDE cizilmeliler. CategorySettingsSheet
        // ConfirmSheet'ten ONCE eklenir -- "Kategoriyi sil" onayi, kategori
        // sayfasinin KENDI uzerine cizilen ConfirmSheet'ten gecer (bkz.
        // CategorySettingsSheet sinif basi yorumu). TimePickerSheet en ustte --
        // Saat satirindan acilir, ConfirmSheet'in ustunu de kapatabilir ama
        // ikisi asla ayni anda acik olmuyor.
        CategorySettingsSheet categorySheet = CategorySettingsSheetBuilder.Build(root, theme, manager);
        ConfirmSheet confirmSheet = BuildConfirmSheet(root, theme);
        TimePickerSheet timePicker = TimePickerSheetBuilder.Build(root, theme);
        LanguagePickerSheet languagePicker = LanguagePickerSheetBuilder.Build(root, theme, prefabs);

        SettingsScreen screen = root.gameObject.AddComponent<SettingsScreen>();
        UiWiring.Set(screen, "manager", manager);
        UiWiring.Set(screen, "panel", root);
        UiWiring.Set(screen, "closeButton", closeButton);
        UiWiring.Set(screen, "premiumCard", premiumCard);
        UiWiring.Set(screen, "nameInput", nameInput);
        UiWiring.Set(screen, "reminderToggle", reminderToggle);
        UiWiring.Set(screen, "timeRow", timeRow);
        UiWiring.Set(screen, "timeButton", timeButton);
        UiWiring.Set(screen, "timeValueLabel", timeValueLabel);
        UiWiring.Set(screen, "timePicker", timePicker);
        UiWiring.Set(screen, "soundToggle", soundToggle);
        UiWiring.Set(screen, "hapticToggle", hapticToggle);
        UiWiring.Set(screen, "weekStartRow", weekStartRow);
        UiWiring.Set(screen, "languageButton", languageButton);
        UiWiring.Set(screen, "languageValueLabel", languageValueLabel);
        UiWiring.Set(screen, "languagePicker", languagePicker);
        UiWiring.Set(screen, "categoriesButton", categoriesButton);
        UiWiring.Set(screen, "categorySheet", categorySheet);
        UiWiring.Set(screen, "exportButton", exportButton);
        UiWiring.Set(screen, "exportPremiumBadge", exportPremiumBadge);
        UiWiring.Set(screen, "importButton", importButton);
        UiWiring.Set(screen, "resetButton", resetButton);
        UiWiring.Set(screen, "confirmSheet", confirmSheet);
        UiWiring.Set(screen, "versionLabel", versionLabel);

        // HabitSheet/ToastView ile AYNI kalip: builder acilista KAPALI
        // birakiyor, Open() calisma aninda aciyor.
        root.gameObject.SetActive(false);
        return screen;
    }

    // Uc panelin kose rozetini bu ekrana baglar -- ScreenNavButton'un
    // BindNav'ina denk gelen Ayarlar karsiligi.
    public static void BindButton(Button button, SettingsScreen screen)
    {
        SettingsButton component = button.gameObject.GetComponent<SettingsButton>();
        if (component == null)
        {
            component = button.gameObject.AddComponent<SettingsButton>();
        }

        UiWiring.Set(component, "button", button);
        UiWiring.Set(component, "settingsScreen", screen);
    }

    // -----------------------------------------------------------------------
    // Baslik
    // -----------------------------------------------------------------------

    private static void BuildHeader(RectTransform parent, UiTheme theme, UiPrefabBuilder.Prefabs prefabs, out Button closeButton)
    {
        RectTransform header = UiFactory.StretchTop(UiFactory.Rect("Header", parent), 6f, 46f, 18f, 18f);

        GameObject close = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefabs.RoundIconButton, header);
        close.name = "CloseButton";
        UiFactory.TopLeft((RectTransform)close.transform, 0f, 0f, 38f, 38f);
        closeButton = close.GetComponentInChildren<Button>();

        TMP_Text title = UiFactory.LocalizedText("Title", header, "settings_panel.title", theme.bold, 28f,
            theme.textPrimary, TextAlignmentOptions.Center);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(220f, 32f);

        // Teslim dosyasi: 42x42 bos yuva (diger ekranlarin 38x38 RightSpacer'i
        // DEGIL -- burada farkli, teslim dosyasindan birebir).
        RectTransform spacer = UiFactory.Rect("RightSpacer", header);
        spacer.anchorMin = new Vector2(1f, 1f);
        spacer.anchorMax = new Vector2(1f, 1f);
        spacer.pivot = new Vector2(1f, 1f);
        spacer.anchoredPosition = new Vector2(0f, -2f);
        spacer.sizeDelta = new Vector2(42f, 42f);
    }

    private static RectTransform BuildContent(RectTransform parent)
    {
        RectTransform scroll = UiFactory.Fill(UiFactory.Rect("Scroll", parent), 0f, 58f, 0f, 0f);

        Image hit = UiFactory.Image("Hit", scroll, null, Color.clear, false);
        UiFactory.Fill(hit.rectTransform);
        hit.raycastTarget = true;

        RectTransform content = UiFactory.ScrollView(scroll, out _, out _);

        VerticalLayoutGroup column = UiFactory.Column(content, 22f);
        column.padding = new RectOffset(20, 20, 22, 48);
        UiFactory.FitVertical(content.gameObject);

        return content;
    }

    // -----------------------------------------------------------------------
    // Premium
    // -----------------------------------------------------------------------

    private static readonly string[] PerkKeys =
    {
        "premium_card.perk.gallery_stats",
        "premium_card.perk.export",
        "premium_card.perk.support",
    };

    private static PremiumCard BuildPremiumBlock(RectTransform content, UiTheme theme)
    {
        RectTransform root = UiFactory.Rect("Premium", content);
        UiFactory.Column(root, 0f);
        UiFactory.FitVertical(root.gameObject);

        GameObject notPremiumRoot = BuildNotPremiumCard(root, theme);
        GameObject isPremiumRoot = BuildIsPremiumStrip(root, theme);

        PremiumCard card = root.gameObject.AddComponent<PremiumCard>();
        UiWiring.Set(card, "notPremiumRoot", notPremiumRoot);
        UiWiring.Set(card, "purchaseButton", notPremiumRoot.transform.Find("Purchase")?.GetComponent<Button>());
        UiWiring.Set(card, "isPremiumRoot", isPremiumRoot);

        return card;
    }

    private static GameObject BuildNotPremiumCard(RectTransform parent, UiTheme theme)
    {
        Image card = UiFactory.Image("NotPremium", parent, theme.Rounded(20), Color.white);
        UiFactory.Gradient(card.gameObject, theme.cardTop, theme.cardBottom);
        UiFactory.HardShadow(card.gameObject, 4f, theme.hardShadow);

        Image outline = UiFactory.Image("Border", card.rectTransform, theme.Rounded(20), theme.premiumBorder);
        UiFactory.Fill(outline.rectTransform);
        LayoutElement outlineIgnore = outline.gameObject.AddComponent<LayoutElement>();
        outlineIgnore.ignoreLayout = true;

        VerticalLayoutGroup column = UiFactory.Column(card.rectTransform, 14f);
        column.padding = new RectOffset(18, 18, 18, 16);
        UiFactory.FitVertical(card.gameObject);

        RectTransform titleRow = UiFactory.Rect("TitleRow", card.rectTransform);
        UiFactory.Size(titleRow.gameObject, -1f, 24f);

        TMP_Text title = UiFactory.LocalizedText("Title", titleRow, "premium_card.title", theme.bold, 20f, theme.textPrimary);
        UiFactory.Fill(title.rectTransform);

        SettingsRowBuilder.PremiumBadge(titleRow, theme, 0f);

        RectTransform perksColumn = UiFactory.Rect("Perks", card.rectTransform);
        UiFactory.Column(perksColumn, 8f);
        UiFactory.FitVertical(perksColumn.gameObject);

        for (int i = 0; i < PerkKeys.Length; i++)
        {
            BuildPerkRow(perksColumn, theme, PerkKeys[i]);
        }

        Image purchase = UiFactory.Image("Purchase", card.rectTransform, theme.Rounded(14), Color.white);
        UiFactory.Size(purchase.gameObject, -1f, 46f);
        UiFactory.Gradient(purchase.gameObject, theme.goldButtonTop, theme.goldButtonBottom);
        UiFactory.Button(purchase);

        TMP_Text purchaseLabel = UiFactory.LocalizedText("Label", purchase.rectTransform, "premium_card.purchase_button",
            theme.semiBold, 15f, theme.textOnGold, TextAlignmentOptions.Center);
        UiFactory.Fill(purchaseLabel.rectTransform);

        return card.gameObject;
    }

    private static void BuildPerkRow(RectTransform parent, UiTheme theme, string textKey)
    {
        RectTransform row = UiFactory.Rect("Perk", parent);
        UiFactory.FitVertical(row.gameObject);
        UiFactory.Size(row.gameObject, -1f, 20f);

        Image diamond = UiFactory.Image("Diamond", row, theme.white, theme.gold, false);
        RectTransform diamondRect = diamond.rectTransform;
        diamondRect.anchorMin = new Vector2(0f, 1f);
        diamondRect.anchorMax = new Vector2(0f, 1f);
        diamondRect.pivot = new Vector2(0.5f, 0.5f);
        diamondRect.anchoredPosition = new Vector2(4f, -9f);
        diamondRect.sizeDelta = new Vector2(7f, 7f);
        diamondRect.localEulerAngles = new Vector3(0f, 0f, 45f);

        TMP_Text label = UiFactory.LocalizedText("Label", row, textKey, theme.regular, 14f, theme.premiumListText);
        label.textWrappingMode = TMPro.TextWrappingModes.Normal;
        RectTransform labelRect = label.rectTransform;
        UiFactory.Fill(labelRect, 17f, 0f, 0f, 0f);
    }

    private static GameObject BuildIsPremiumStrip(RectTransform parent, UiTheme theme)
    {
        Image card = UiFactory.Image("IsPremium", parent, theme.Rounded(20), Color.white);
        UiFactory.Gradient(card.gameObject, theme.cardTop, theme.cardBottom);
        UiFactory.HardShadow(card.gameObject, 4f, theme.hardShadow);
        UiFactory.Size(card.gameObject, -1f, 70f);

        Image outline = UiFactory.Image("Border", card.rectTransform, theme.Rounded(20), theme.premiumActiveBorder);
        UiFactory.Fill(outline.rectTransform);

        TMP_Text title = UiFactory.LocalizedText("Title", card.rectTransform, "premium_active.title", theme.semiBold, 15f, theme.textPrimary);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(0f, 0.5f);
        titleRect.pivot = new Vector2(0f, 0.5f);
        titleRect.anchoredPosition = new Vector2(16f, 10f);
        titleRect.sizeDelta = new Vector2(200f, 18f);

        TMP_Text subtitle = UiFactory.LocalizedText("Subtitle", card.rectTransform,
            "premium_active.subtitle", theme.regular, 12f, theme.textMuted);
        subtitle.textWrappingMode = TMPro.TextWrappingModes.Normal;
        RectTransform subtitleRect = subtitle.rectTransform;
        subtitleRect.anchorMin = new Vector2(0f, 0.5f);
        subtitleRect.anchorMax = new Vector2(0f, 0.5f);
        subtitleRect.pivot = new Vector2(0f, 0.5f);
        subtitleRect.anchoredPosition = new Vector2(16f, -10f);
        subtitleRect.sizeDelta = new Vector2(210f, 28f);

        SettingsRowBuilder.PremiumBadge(card.rectTransform, theme, -16f);

        return card.gameObject;
    }

    // -----------------------------------------------------------------------
    // Ressam
    // -----------------------------------------------------------------------

    private static TMP_InputField BuildPainterNameBlock(RectTransform content, UiTheme theme)
    {
        RectTransform section = SettingsRowBuilder.Section(content, theme, "settings_section.painter");
        RectTransform card = SettingsRowBuilder.Card(section, theme);

        RectTransform row = SettingsRowBuilder.Row(card, theme, "settings_row.painter_name", 56f, out _, out RectTransform trailing);

        // Image (seffaf) olarak kuruyoruz, cirilcirlak RectTransform degil --
        // GraphicRaycaster yalnizca bir Graphic'i olan nesneleri vuruyor,
        // yoksa alan hic tiklanamaz kalirdi.
        Image inputBg = UiFactory.Image("Input", trailing, theme.white, Color.clear, false);
        RectTransform inputRect = inputBg.rectTransform;
        inputRect.anchorMin = new Vector2(1f, 0.5f);
        inputRect.anchorMax = new Vector2(1f, 0.5f);
        inputRect.pivot = new Vector2(1f, 0.5f);
        inputRect.anchoredPosition = Vector2.zero;
        inputRect.sizeDelta = new Vector2(170f, 36f);
        inputBg.raycastTarget = true;

        TMP_Text inputText = UiFactory.Text("Text", inputRect, string.Empty, theme.semiBold, 15f,
            theme.gold, TextAlignmentOptions.Right);
        UiFactory.Fill(inputText.rectTransform);

        TMP_InputField input = inputBg.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = inputRect;
        input.textComponent = (TextMeshProUGUI)inputText;
        input.targetGraphic = inputBg;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 14;

        return input;
    }

    // -----------------------------------------------------------------------
    // Hatirlatma
    // -----------------------------------------------------------------------

    private static void BuildReminderBlock(
        RectTransform content, UiTheme theme,
        out SettingsToggleRow reminderToggle, out GameObject timeRow, out Button timeButton, out TMP_Text timeValueLabel)
    {
        RectTransform section = SettingsRowBuilder.Section(content, theme, "settings_section.reminder");
        RectTransform card = SettingsRowBuilder.Card(section, theme);

        RectTransform remindRowRect = SettingsRowBuilder.Row(
            card, theme, "settings_row.reminder_toggle", 62f, out _, out RectTransform remindTrailing);
        // Basit-gunluk-tekrar karari dogru olani kisa soyluyor -- eski metin
        // ("Tik atilmamis aliskanlik varsa" kosulu) artik Faz 3'un tek
        // NotificationRepeatInterval.Daily bildirimiyle UYUMSUZ.
        SettingsRowBuilder.SubLabel(remindRowRect, theme, "settings_row.reminder_sublabel");
        reminderToggle = SettingsRowBuilder.Toggle(remindRowRect, remindTrailing, theme);

        SettingsRowBuilder.Divider(card, theme);

        RectTransform timeRowRect = SettingsRowBuilder.Row(card, theme, "settings_row.reminder_time", 56f, out _, out RectTransform timeTrailing);
        timeRow = timeRowRect.gameObject;
        timeButton = SettingsRowBuilder.ValueRow(timeRowRect, timeTrailing, theme, out timeValueLabel);
    }

    // -----------------------------------------------------------------------
    // Deneyim
    // -----------------------------------------------------------------------

    private static void BuildExperienceBlock(
        RectTransform content, UiTheme theme, out SettingsToggleRow soundToggle, out SettingsToggleRow hapticToggle)
    {
        RectTransform section = SettingsRowBuilder.Section(content, theme, "settings_section.experience");
        RectTransform card = SettingsRowBuilder.Card(section, theme);

        RectTransform soundRow = SettingsRowBuilder.Row(card, theme, "settings_row.sound", 56f, out _, out RectTransform soundTrailing);
        soundToggle = SettingsRowBuilder.Toggle(soundRow, soundTrailing, theme);

        SettingsRowBuilder.Divider(card, theme);

        RectTransform hapticRow = SettingsRowBuilder.Row(card, theme, "settings_row.haptics", 56f, out _, out RectTransform hapticTrailing);
        hapticToggle = SettingsRowBuilder.Toggle(hapticRow, hapticTrailing, theme);
    }

    // -----------------------------------------------------------------------
    // Takvim
    // -----------------------------------------------------------------------

    private static SettingsSegmentRow BuildCalendarBlock(RectTransform content, UiTheme theme)
    {
        RectTransform section = SettingsRowBuilder.Section(content, theme, "settings_section.calendar");
        RectTransform card = SettingsRowBuilder.Card(section, theme);

        RectTransform row = SettingsRowBuilder.Row(card, theme, "settings_row.week_start", 60f, out _, out RectTransform trailing);
        return SettingsRowBuilder.Segment(trailing, theme);
    }

    // -----------------------------------------------------------------------
    // Dil
    // -----------------------------------------------------------------------

    // "Saat" satiriyla AYNI sekil (BuildReminderBlock): deger sagda gosterilen,
    // dokunulabilir bir satir -- categoriesButton'un NavRow'undan farkli,
    // burada ok yerine SECILI DILIN ADI gosteriliyor.
    private static Button BuildLanguageBlock(RectTransform content, UiTheme theme, out TMP_Text languageValueLabel)
    {
        RectTransform section = SettingsRowBuilder.Section(content, theme, "settings_section.language");
        RectTransform card = SettingsRowBuilder.Card(section, theme);

        RectTransform row = SettingsRowBuilder.Row(card, theme, "settings_section.language", 56f, out _, out RectTransform trailing);
        return SettingsRowBuilder.ValueRow(row, trailing, theme, out languageValueLabel);
    }

    // -----------------------------------------------------------------------
    // Kategoriler
    // -----------------------------------------------------------------------

    // Hem kategorileri (ekleme/yeniden adlandirma/silme) hem ana sayfanin
    // filtre satirini (en fazla HabitFilters.MaxSlots - 1 secim) tek bir
    // sayfadan yonetir -- UCRETSIZ, PremiumBadge YOK (bkz. CategorySettingsSheet
    // sinif basi yorumu).
    private static Button BuildCategoriesBlock(RectTransform content, UiTheme theme)
    {
        RectTransform section = SettingsRowBuilder.Section(content, theme, "settings_section.categories");
        RectTransform card = SettingsRowBuilder.Card(section, theme);

        RectTransform row = SettingsRowBuilder.Row(
            card, theme, "category_settings.title", 56f, out _, out RectTransform trailing);
        return SettingsRowBuilder.NavRow(row, trailing, theme, theme.gold, false);
    }

    // -----------------------------------------------------------------------
    // Veri
    // -----------------------------------------------------------------------

    private static void BuildDataBlock(
        RectTransform content, UiTheme theme,
        out Button exportButton, out GameObject exportPremiumBadge, out Button importButton, out Button resetButton)
    {
        RectTransform section = SettingsRowBuilder.Section(content, theme, "settings_section.data");
        RectTransform card = SettingsRowBuilder.Card(section, theme);

        RectTransform exportRow = SettingsRowBuilder.Row(
            card, theme, "settings_row.export", 56f, out _, out RectTransform exportTrailing);
        exportButton = SettingsRowBuilder.NavRow(exportRow, exportTrailing, theme, theme.gold, true);
        exportPremiumBadge = exportTrailing.Find("PremiumBadge")?.gameObject;

        SettingsRowBuilder.Divider(card, theme);

        // Ice aktarma UCRETSIZ -- export'un aksine premium rozeti yok (bkz.
        // SettingsScreen.HandleImport sinif basi yorumu).
        RectTransform importRow = SettingsRowBuilder.Row(
            card, theme, "settings_row.import", 56f, out _, out RectTransform importTrailing);
        importButton = SettingsRowBuilder.NavRow(importRow, importTrailing, theme, theme.gold, false);

        SettingsRowBuilder.Divider(card, theme);

        RectTransform resetRow = SettingsRowBuilder.Row(
            card, theme, "settings.reset_progress_title", 56f, out TMP_Text resetLabel, out RectTransform resetTrailing);
        resetLabel.color = theme.quitColor;
        resetButton = SettingsRowBuilder.NavRow(resetRow, resetTrailing, theme, theme.quitColor, false);
    }

    // -----------------------------------------------------------------------
    // Onay kutusu
    // -----------------------------------------------------------------------

    private static ConfirmSheet BuildConfirmSheet(RectTransform parent, UiTheme theme)
    {
        RectTransform root = UiFactory.Fill(UiFactory.Rect("ConfirmSheet", parent));

        Image scrim = UiFactory.Image("Scrim", root, theme.white, theme.scrim, false);
        UiFactory.Fill(scrim.rectTransform);
        Button scrimButton = UiFactory.Button(scrim);
        CanvasGroup scrimGroup = scrim.gameObject.AddComponent<CanvasGroup>();

        Image sheet = UiFactory.Image("Sheet", root, theme.Rounded(24), theme.surface);
        RectTransform sheetRect = UiFactory.StretchBottom(sheet.rectTransform, 0f, 220f, 0f, 0f);

        Image topBorder = UiFactory.Image("TopBorder", sheetRect, theme.white, theme.borderGold, false);
        UiFactory.StretchTop(topBorder.rectTransform, 0f, 2f, 0f, 0f);

        TMP_Text title = UiFactory.Text("Title", sheetRect, string.Empty, theme.semiBold, 18f, theme.textPrimary);
        UiFactory.StretchTop(title.rectTransform, 26f, 24f, 24f, 24f);

        TMP_Text body = UiFactory.Text("Body", sheetRect, string.Empty, theme.regular, 13f, theme.textMuted);
        body.textWrappingMode = TMPro.TextWrappingModes.Normal;
        UiFactory.StretchTop(body.rectTransform, 58f, 54f, 24f, 24f);

        RectTransform footer = UiFactory.StretchBottom(UiFactory.Rect("Footer", sheetRect), 24f, 54f, 24f, 24f);
        HorizontalLayoutGroup row = UiFactory.Row(footer, 10f, TextAnchor.UpperLeft);
        row.childForceExpandHeight = true;

        Image cancel = UiFactory.Image("Cancel", footer, theme.Rounded(14), theme.borderFaint);
        LayoutElement cancelElement = cancel.gameObject.AddComponent<LayoutElement>();
        cancelElement.flexibleWidth = 1f;
        cancelElement.preferredHeight = 54f;
        Button cancelButton = UiFactory.Button(cancel);

        TMP_Text cancelLabel = UiFactory.LocalizedText("Label", cancel.rectTransform, "common.cancel_button", theme.semiBold, 15f,
            theme.textMuted, TextAlignmentOptions.Center);
        UiFactory.Fill(cancelLabel.rectTransform);

        Image confirm = UiFactory.Image("Confirm", footer, theme.Rounded(14), theme.quitColor);
        LayoutElement confirmElement = confirm.gameObject.AddComponent<LayoutElement>();
        confirmElement.flexibleWidth = 1f;
        confirmElement.preferredHeight = 54f;
        Button confirmButton = UiFactory.Button(confirm);

        TMP_Text confirmLabel = UiFactory.Text("Label", confirm.rectTransform, string.Empty, theme.semiBold, 15f,
            theme.textPrimary, TextAlignmentOptions.Center);
        UiFactory.Fill(confirmLabel.rectTransform);

        ConfirmSheet component = root.gameObject.AddComponent<ConfirmSheet>();
        UiWiring.Set(component, "scrimGroup", scrimGroup);
        UiWiring.Set(component, "scrimButton", scrimButton);
        UiWiring.Set(component, "panel", sheetRect);
        UiWiring.Set(component, "titleLabel", title);
        UiWiring.Set(component, "bodyLabel", body);
        UiWiring.Set(component, "cancelButton", cancelButton);
        UiWiring.Set(component, "confirmButton", confirmButton);
        UiWiring.Set(component, "confirmLabel", confirmLabel);

        root.gameObject.SetActive(false);
        return component;
    }
}
