using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 02 Alisknliklar ekrani: gruplu liste, "Yeni alışkanlık" butonu ve alttan
// acilan ekle/duzenle formu.
public static class HabitsPanelBuilder
{
    public const string PanelName = "Panel_Habits";

    // Form kutusunun tasarim yuksekligi ve tepede birakilacak en az bosluk.
    private const float SheetHeight = 700f;
    private const float SheetTopMargin = 40f;

    public static GameObject Build(
        RectTransform canvas,
        UiTheme theme,
        UiPrefabBuilder.Prefabs prefabs,
        HabbitManager manager,
        ScreenNavigator navigator,
        out Button backButton)
    {
        RectTransform panel = UiFactory.Fill(UiFactory.Rect(PanelName, canvas));

        Image background = UiFactory.Image("Background", panel, theme.white, Color.white, false);
        UiFactory.Fill(background.rectTransform);
        UiFactory.Gradient(background.gameObject, theme.surface, theme.background);

        // Zemin kanvasin tam genisligine yayilsin: genis bir ekranda panel
        // 366 birimlik kolona kelepceli (SafeAreaFitter) ve gradyan orada
        // kalirsa kenarda ton farki gorunur. bleedTop: centikli telefonlarda
        // guvenli alanin tepesi ekranin tepesinden asagida kalir -- zemin de
        // oraya kadar cikmazsa o seritte Studio arka plani gorunur.
        FullBleedFitter backgroundFitter = background.gameObject.AddComponent<FullBleedFitter>();
        UiWiring.SetBool(backgroundFitter, "bleedTop", true);

        BuildHeader(panel, theme, prefabs, out TMP_Text activeCount, out backButton);

        RectTransform content = BuildList(panel, out _);
        GameObject archiveEntryRoot = BuildArchiveEntryRow(content, theme,
            out Button archiveEntryButton, out TMP_Text archiveCountLabel);
        Button addButton = BuildAddButton(panel, theme, out RectTransform bottomBar);

        // BottomBar'dan SONRA, HabitSheet'ten ONCE: alt sayfa acilinca cubugu
        // ORTMELI (bkz. NAVIGATION_SETTINGS_PLAN.md Faz 2).
        BottomNavBar navBar = BottomNavBuilder.Build(panel, theme, navigator, 1);

        // BottomNavBar'dan SONRA, HabitSheet'ten ONCE: arsiv sayfasi da
        // cubugu ortmeli, ve duzenleme sayfasi arsivin UZERINE acilabilmeli
        // (bir arsiv satirina dokunmak HabitSheet'i acar).
        ArchiveSheet archiveSheet = ArchiveSheetBuilder.Build(panel, theme, prefabs, manager, navigator);

        HabitSheet sheet = BuildSheet(panel, theme, prefabs, manager, navigator);

        // HabitSheet'ten SONRA: kategori secici onun UZERINE cizilmeli --
        // PaintingChoiceSheet'in Panel_Workshop'taki kardes sirasiyla ayni
        // mantik. HabitSheet zaten kurulduktan sonra kendi "categoryPicker"
        // alanina geriye donuk baglaniyor.
        CategoryPickerSheet categoryPicker = CategoryPickerSheetBuilder.Build(panel, theme, prefabs, manager, navigator);
        UiWiring.Set(sheet, "categoryPicker", categoryPicker);

        HabitsScreen screen = panel.gameObject.AddComponent<HabitsScreen>();
        UiWiring.Set(screen, "manager", manager);
        UiWiring.Set(screen, "activeCountLabel", activeCount);
        UiWiring.Set(screen, "sectionPrefab", prefabs.CategorySection.GetComponent<HabitCategorySection>());
        UiWiring.Set(screen, "content", content);
        UiWiring.Set(screen, "sheet", sheet);
        UiWiring.Set(screen, "addButton", addButton);
        UiWiring.Set(screen, "bottomBar", bottomBar);
        UiWiring.Set(screen, "navBar", navBar);
        UiWiring.Set(screen, "archiveEntryRoot", archiveEntryRoot);
        UiWiring.Set(screen, "archiveEntryButton", archiveEntryButton);
        UiWiring.Set(screen, "archiveCountLabel", archiveCountLabel);
        UiWiring.Set(screen, "archiveSheet", archiveSheet);

        panel.gameObject.SetActive(false);
        return panel.gameObject;
    }

    private static void BuildHeader(
        RectTransform parent,
        UiTheme theme,
        UiPrefabBuilder.Prefabs prefabs,
        out TMP_Text activeCount,
        out Button backButton)
    {
        RectTransform header = UiFactory.StretchTop(UiFactory.Rect("Header", parent), 6f, 46f, 18f, 18f);

        GameObject back = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.RoundIconButton, header);
        back.name = "BackButton";
        UiFactory.TopLeft((RectTransform)back.transform, 0f, 0f, 38f, 38f);
        backButton = back.GetComponentInChildren<Button>();

        // Galeri artik bir sekme (alt navigasyon cubugunda): bu rozet
        // Galeri'nin basligindakiyle AYNI bos 38x38 yuva (RightSpacer).
        RectTransform spacer = UiFactory.Rect("RightSpacer", header);
        spacer.anchorMin = new Vector2(1f, 1f);
        spacer.anchorMax = new Vector2(1f, 1f);
        spacer.pivot = new Vector2(1f, 1f);
        spacer.anchoredPosition = Vector2.zero;
        spacer.sizeDelta = new Vector2(38f, 38f);

        RectTransform titleBlock = UiFactory.Rect("TitleBlock", header);
        titleBlock.anchorMin = new Vector2(0.5f, 1f);
        titleBlock.anchorMax = new Vector2(0.5f, 1f);
        titleBlock.pivot = new Vector2(0.5f, 1f);
        titleBlock.anchoredPosition = Vector2.zero;
        titleBlock.sizeDelta = new Vector2(240f, 46f);
        UiFactory.Column(titleBlock, 2f, TextAnchor.UpperCenter);

        activeCount = UiFactory.Text("ActiveCount", titleBlock, "0 aktif", theme.medium, 14f,
            theme.textMuted, TextAlignmentOptions.Center);
        UiFactory.Size(activeCount.gameObject, -1f, 16f);

        TMP_Text title = UiFactory.LocalizedText("Title", titleBlock, "nav.tab.habits", theme.bold, 28f,
            theme.textPrimary, TextAlignmentOptions.Center);
        UiFactory.Size(title.gameObject, -1f, 32f);
    }

    private static RectTransform BuildList(RectTransform parent, out ScrollRect scrollRect)
    {
        RectTransform scroll = UiFactory.Fill(UiFactory.Rect("ListScroll", parent), 0f, 58f, 0f, 0f);

        // Bos alana dokunup kaydirabilmek icin gorunmez bir raycast hedefi.
        Image hit = UiFactory.Image("Hit", scroll, null, Color.clear, false);
        UiFactory.Fill(hit.rectTransform);
        hit.raycastTarget = true;

        RectTransform content = UiFactory.ScrollView(scroll, out scrollRect, out _);

        VerticalLayoutGroup column = UiFactory.Column(content, 22f);
        // 120 -> 110: CTA cubugu artik alt navigasyon cubugunun USTUNDE,
        // altindaki bosluk payi bir tik kisaldi (teslim dosyasi).
        column.padding = new RectOffset(20, 20, 18, 110);
        UiFactory.FitVertical(content.gameObject);

        return content;
    }

    // "Arsiv (N)" satiri -- content'in (yukaridaki VerticalLayoutGroup'un)
    // bir cocugu olarak kuruluyor, HabitsScreen.Refresh her tazelemede onu
    // SetAsLastSibling ile sona itiyor (SectionAt yeni bolumleri content'in
    // SONUNA Instantiate ediyor, yani sabit bir satir zamanla ortada kalirdi).
    private static GameObject BuildArchiveEntryRow(
        RectTransform content,
        UiTheme theme,
        out Button button,
        out TMP_Text countLabel)
    {
        Image row = UiFactory.Image("ArchiveEntryRow", content, theme.Rounded(14), theme.surface);
        UiFactory.Size(row.gameObject, -1f, 56f);

        countLabel = UiFactory.Text("Label", row.rectTransform, "Arşiv (0)",
            theme.semiBold, 15f, theme.textPrimary);
        RectTransform labelRect = countLabel.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(20f, 0f);
        labelRect.offsetMax = new Vector2(-40f, 0f);

        TMP_Text arrow = UiFactory.Text("Arrow", row.rectTransform, "›", theme.semiBold, 18f,
            theme.textFaint, TextAlignmentOptions.Center);
        RectTransform arrowRect = arrow.rectTransform;
        arrowRect.anchorMin = new Vector2(1f, 0.5f);
        arrowRect.anchorMax = new Vector2(1f, 0.5f);
        arrowRect.pivot = new Vector2(1f, 0.5f);
        arrowRect.anchoredPosition = new Vector2(-16f, 0f);
        arrowRect.sizeDelta = new Vector2(16f, 20f);

        button = UiFactory.Button(row);

        row.gameObject.SetActive(false);
        return row.gameObject;
    }

    private static Button BuildAddButton(RectTransform parent, UiTheme theme, out RectTransform bar)
    {
        bar = UiFactory.StretchBottom(
            UiFactory.Rect("BottomBar", parent), 0f, HabitsScreen.BottomBarHeight, 0f, 0f);

        Image fade = UiFactory.Image("Fade", bar, theme.white, Color.white, false);
        UiFactory.Fill(fade.rectTransform);
        Color transparent = theme.background;
        transparent.a = 0f;
        UiFactory.Gradient(fade.gameObject, transparent, theme.background);

        Image button = UiFactory.Image("AddButton", bar, theme.Rounded(18), Color.white);
        UiFactory.StretchBottom(button.rectTransform, 30f, 56f, 20f, 20f);
        UiFactory.Gradient(button.gameObject, theme.goldButtonTop, theme.goldButtonBottom);
        UiFactory.HardShadow(button.gameObject, 5f, theme.goldButtonShadow);

        TMP_Text label = UiFactory.LocalizedText("Label", button.rectTransform, "habits_panel.new_habit_button",
            theme.semiBold, 17f, theme.textOnGold, TextAlignmentOptions.Center);
        UiFactory.Fill(label.rectTransform);

        return UiFactory.Button(button);
    }

    // -----------------------------------------------------------------------
    // Bottom sheet
    // -----------------------------------------------------------------------

    private static HabitSheet BuildSheet(
        RectTransform parent,
        UiTheme theme,
        UiPrefabBuilder.Prefabs prefabs,
        HabbitManager manager,
        ScreenNavigator navigator)
    {
        RectTransform root = UiFactory.Fill(UiFactory.Rect("HabitSheet", parent));

        Image scrim = UiFactory.Image("Scrim", root, theme.white, theme.scrim, false);
        UiFactory.Fill(scrim.rectTransform);
        Button scrimButton = UiFactory.Button(scrim);
        CanvasGroup scrimGroup = scrim.gameObject.AddComponent<CanvasGroup>();

        Image sheet = UiFactory.Image("Sheet", root, theme.Rounded(30), theme.surface);
        RectTransform sheetRect = UiFactory.StretchBottom(sheet.rectTransform, 0f, SheetHeight, 0f, 0f);
        sheet.raycastTarget = true;

        // 700, canvas'in dikey birim yuksekliginden (16:9 bir telefonda ~651,
        // guvenli alan dusulunce daha az) buyuk olabiliyor ve kutunun ust bari
        // ekranin disinda kaliyordu. Fitter her olcu degisiminde kirpiyor.
        MaxHeightFitter fitter = sheet.gameObject.AddComponent<MaxHeightFitter>();
        UiWiring.SetFloat(fitter, "designHeight", SheetHeight);
        UiWiring.SetFloat(fitter, "topMargin", SheetTopMargin);

        Image topBorder = UiFactory.Image("TopBorder", sheetRect, theme.white, theme.borderGold, false);
        UiFactory.StretchTop(topBorder.rectTransform, 0f, 2f, 0f, 0f);

        BuildSheetTopBar(sheetRect, theme, out TMP_Text sheetTitle, out Button cancelButton);

        BuildSheetFields(
            sheetRect, theme, prefabs,
            out TMP_InputField titleInput,
            out OptionButton buildOption,
            out OptionButton quitOption,
            out Button categoryButton,
            out TMP_Text categoryValueLabel,
            out DayChipRow dayToggles,
            out TMP_Text daysHint,
            out CanvasSizeOption[] canvasOptions,
            out Button repsMinusButton,
            out Button repsPlusButton,
            out TMP_Text repsValueLabel,
            out GameObject paintingSectionRoot,
            out PaintingPickerRow paintingRow,
            out TMP_Text paintingHint,
            out Button endDateToggle,
            out Image endDateTrack,
            out RectTransform endDateKnob,
            out GameObject endDateRow,
            out TMP_InputField endDateInput);

        BuildSheetFooter(sheetRect, theme,
            out GameObject deleteRoot,
            out Button deleteButton,
            out GameObject archiveRoot,
            out Button archiveButton,
            out TMP_Text archiveLabel,
            out Button saveButton,
            out TMP_Text saveLabel,
            out Image saveBackground);

        HabitSheet component = root.gameObject.AddComponent<HabitSheet>();
        UiWiring.Set(component, "manager", manager);
        UiWiring.Set(component, "navigator", navigator);
        UiWiring.Set(component, "scrimGroup", scrimGroup);
        UiWiring.Set(component, "scrimButton", scrimButton);
        UiWiring.Set(component, "panel", sheetRect);
        UiWiring.Set(component, "sheetTitle", sheetTitle);
        UiWiring.Set(component, "cancelButton", cancelButton);
        UiWiring.Set(component, "titleInput", titleInput);
        UiWiring.Set(component, "buildOption", buildOption);
        UiWiring.Set(component, "quitOption", quitOption);
        UiWiring.Set(component, "categoryButton", categoryButton);
        UiWiring.Set(component, "categoryValueLabel", categoryValueLabel);
        UiWiring.Set(component, "dayToggles", dayToggles);
        UiWiring.Set(component, "daysHint", daysHint);
        UiWiring.SetArray(component, "canvasOptions", canvasOptions);
        UiWiring.Set(component, "repsMinusButton", repsMinusButton);
        UiWiring.Set(component, "repsPlusButton", repsPlusButton);
        UiWiring.Set(component, "repsValueLabel", repsValueLabel);
        UiWiring.Set(component, "paintingSectionRoot", paintingSectionRoot);
        UiWiring.Set(component, "paintingRow", paintingRow);
        UiWiring.Set(component, "paintingHint", paintingHint);
        UiWiring.Set(component, "endDateToggle", endDateToggle);
        UiWiring.Set(component, "endDateTrack", endDateTrack);
        UiWiring.Set(component, "endDateKnob", endDateKnob);
        UiWiring.Set(component, "endDateRow", endDateRow);
        UiWiring.Set(component, "endDateInput", endDateInput);
        UiWiring.Set(component, "deleteRoot", deleteRoot);
        UiWiring.Set(component, "deleteButton", deleteButton);
        UiWiring.Set(component, "archiveRoot", archiveRoot);
        UiWiring.Set(component, "archiveButton", archiveButton);
        UiWiring.Set(component, "archiveLabel", archiveLabel);
        UiWiring.Set(component, "saveButton", saveButton);
        UiWiring.Set(component, "saveLabel", saveLabel);
        UiWiring.Set(component, "saveBackground", saveBackground);
        UiWiring.Set(component, "theme", theme);

        root.gameObject.SetActive(false);
        return component;
    }

    private static void BuildSheetTopBar(
        RectTransform sheet,
        UiTheme theme,
        out TMP_Text title,
        out Button cancelButton)
    {
        RectTransform bar = UiFactory.StretchTop(UiFactory.Rect("TopBar", sheet), 10f, 50f, 20f, 20f);

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

        // "Yeni alışkanlık" BILEREK LocalizedText olmuyor -- HabitSheet.Open
        // her acilista bu basligi new/edit durumuna gore (habit_sheet.title_new/
        // title_edit) yeniden yaziyor, Editor varsayilani hep ezilir (bkz.
        // LOCALIZATION_PLAN.md Faz 3 placeholder kurali).
        title = UiFactory.Text("Title", bar, "Yeni alışkanlık", theme.semiBold, 18f,
            theme.textPrimary, TextAlignmentOptions.Center);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -15f);
        titleRect.sizeDelta = new Vector2(220f, 26f);
    }

    private static RectTransform BuildSheetFields(
        RectTransform sheet,
        UiTheme theme,
        UiPrefabBuilder.Prefabs prefabs,
        out TMP_InputField titleInput,
        out OptionButton buildOption,
        out OptionButton quitOption,
        out Button categoryButton,
        out TMP_Text categoryValueLabel,
        out DayChipRow dayToggles,
        out TMP_Text daysHint,
        out CanvasSizeOption[] canvasOptions,
        out Button repsMinusButton,
        out Button repsPlusButton,
        out TMP_Text repsValueLabel,
        out GameObject paintingSectionRoot,
        out PaintingPickerRow paintingRow,
        out TMP_Text paintingHint,
        out Button endDateToggle,
        out Image endDateTrack,
        out RectTransform endDateKnob,
        out GameObject endDateRow,
        out TMP_InputField endDateInput)
    {
        RectTransform scroll = UiFactory.Fill(UiFactory.Rect("Fields", sheet), 0f, 64f, 0f, 96f);

        // TileScroll'daki ile ayni gerekce: bolumler arasindaki bosluklarin
        // raycast hedefi yok, o yuzden parmak orada baslarsa ScrollRect hic
        // surukleme almiyordu -- yalnizca bir eleman (input, buton...) uzerinde
        // baslayan surukleme yukari kabarcaklanip calisiyordu. Gorunmez ama
        // raycast alan bir zemin bosluklari da dokunulabilir yapiyor.
        Image hit = scroll.gameObject.AddComponent<Image>();
        hit.sprite = theme.white;
        hit.color = Color.clear;
        hit.raycastTarget = true;

        RectTransform content = UiFactory.ScrollView(scroll, out _, out _);

        VerticalLayoutGroup column = UiFactory.Column(content, 22f);
        column.padding = new RectOffset(20, 20, 14, 20);
        UiFactory.FitVertical(content.gameObject);

        // Baslik
        RectTransform titleSection = Section(content, theme, "habit_sheet.section.title", out _);
        titleInput = BuildInput(titleSection, theme, "habit_sheet.title_input.placeholder");

        // Tur
        RectTransform typeSection = Section(content, theme, "habit_sheet.section.type", out _);
        RectTransform typeRow = UiFactory.Rect("Options", typeSection);
        UiFactory.Row(typeRow, 10f, TextAnchor.UpperLeft).childForceExpandWidth = true;
        UiFactory.Size(typeRow.gameObject, -1f, 70f);

        buildOption = SpawnOption(prefabs.TypeOptionCard, typeRow, "habit_type.build", "habit_type.build_hint");
        quitOption = SpawnOption(prefabs.TypeOptionCard, typeRow, "habit_type.quit", "habit_type.quit_hint");

        // Kategori: artik on iki yerlesik + kullanicinin ozel kategorileriyle
        // sinirsiz buyuyebiliyor, bu yuzden form icinde sabit bir izgara
        // DEGIL -- tek bir deger satiri. Dokunulunca CategoryPickerSheet
        // (alttan acilan ayri bir kutu) acilir; BuildInput'un kenarlikli
        // gorunumunun ayni kalibi, sadece sagina bir ok eklenmis hali.
        RectTransform categorySection = Section(content, theme, "habit_sheet.section.category", out _);

        Image categoryRow = UiFactory.Image("Value", categorySection, theme.Rounded(14), theme.borderFaint);
        UiFactory.Size(categoryRow.gameObject, -1f, 50f);
        categoryRow.raycastTarget = true;

        Image categoryFill = UiFactory.Image("Fill", categoryRow.rectTransform, theme.Rounded(12), theme.background);
        UiFactory.Fill(categoryFill.rectTransform, 2f, 2f, 2f, 2f);

        categoryValueLabel = UiFactory.Text("Value", categoryRow.rectTransform, HabitCategories.Default,
            theme.medium, 16f, theme.textPrimary);
        UiFactory.Fill(categoryValueLabel.rectTransform, 14f, 0f, 36f, 0f);

        TMP_Text categoryArrow = UiFactory.Text("Arrow", categoryRow.rectTransform, "›", theme.semiBold, 18f,
            theme.textMuted, TextAlignmentOptions.Center);
        RectTransform categoryArrowRect = categoryArrow.rectTransform;
        categoryArrowRect.anchorMin = new Vector2(1f, 0.5f);
        categoryArrowRect.anchorMax = new Vector2(1f, 0.5f);
        categoryArrowRect.pivot = new Vector2(1f, 0.5f);
        categoryArrowRect.anchoredPosition = new Vector2(-14f, 0f);
        categoryArrowRect.sizeDelta = new Vector2(16f, 20f);

        categoryButton = UiFactory.Button(categoryRow);

        // Gunler
        RectTransform daysSection = Section(content, theme, "habit_sheet.section.days", out TMP_Text daysLabel);
        daysHint = UiFactory.LocalizedText("Hint", daysLabel.transform.parent, "habit_sheet.days_hint",
            theme.regular, 12f, theme.textMuted, TextAlignmentOptions.Right);
        RectTransform hintRect = daysHint.rectTransform;
        hintRect.anchorMin = new Vector2(1f, 1f);
        hintRect.anchorMax = new Vector2(1f, 1f);
        hintRect.pivot = new Vector2(1f, 1f);
        hintRect.anchoredPosition = Vector2.zero;
        hintRect.sizeDelta = new Vector2(160f, 16f);

        RectTransform dayRow = UiFactory.Rect("Days", daysSection);
        UiFactory.Row(dayRow, 6f, TextAnchor.UpperLeft).childForceExpandWidth = true;
        UiFactory.Size(dayRow.gameObject, -1f, 44f);

        dayToggles = dayRow.gameObject.AddComponent<DayChipRow>();
        UiWiring.Set(dayToggles, "chipPrefab", prefabs.DayToggle.GetComponent<OptionButton>());
        UiWiring.Set(dayToggles, "root", dayRow);
        UiWiring.SetBool(dayToggles, "useInitials", false);
        UiWiring.SetBool(dayToggles, "interactable", true);

        // Gunluk tur: "kediyle oyna 2 tur" gibi gunde birden fazla tik
        // isteyen aliskanlik icin hedef. Gunler ile Tuval arasinda duruyor --
        // ikisi de "ne zaman / ne kadar" sorusunun parcasi, tablo secimi ise
        // bambaska bir karar.
        //
        // Adimlayici TimePickerSheetBuilder.StepperButton gorsel kalibinin
        // YATAY hali: orada dugmeler mutlak anchoredPosition ile yerlestiriliyor
        // (sheet'in kendi izgarasi), burada satirin HorizontalLayoutGroup'u
        // yerlestiriyor -- kare dugme Column'un DOGRUDAN cocugu olsaydi tam
        // genislige yayilirdi (bkz. UiFactory.Column childForceExpandWidth).
        RectTransform repsSection = Section(content, theme, "habit_sheet.section.reps", out TMP_Text repsCaption);

        TMP_Text repsHint = UiFactory.LocalizedText("Hint", repsCaption.transform.parent, "habit_sheet.reps_hint",
            theme.regular, 12f, theme.textMuted, TextAlignmentOptions.Right);
        RectTransform repsHintRect = repsHint.rectTransform;
        repsHintRect.anchorMin = new Vector2(1f, 1f);
        repsHintRect.anchorMax = new Vector2(1f, 1f);
        repsHintRect.pivot = new Vector2(1f, 1f);
        repsHintRect.anchoredPosition = Vector2.zero;
        repsHintRect.sizeDelta = new Vector2(200f, 16f);

        RectTransform repsRow = UiFactory.Rect("Reps", repsSection);
        UiFactory.Row(repsRow, 14f, TextAnchor.MiddleLeft);
        UiFactory.Size(repsRow.gameObject, -1f, 44f);

        repsMinusButton = RepsStepperButton(repsRow, theme, "-");

        repsValueLabel = UiFactory.Text("Value", repsRow, "1", theme.bold, 20f,
            theme.textPrimary, TextAlignmentOptions.Center);
        UiFactory.Size(repsValueLabel.gameObject, 44f, 40f);

        repsPlusButton = RepsStepperButton(repsRow, theme, "+");

        // Tuval
        RectTransform canvasSection = Section(content, theme, "habit_sheet.section.canvas", out _);
        RectTransform canvasRow = UiFactory.Rect("Options", canvasSection);
        UiFactory.Row(canvasRow, 10f, TextAnchor.UpperLeft).childForceExpandWidth = true;
        UiFactory.Size(canvasRow.gameObject, -1f, 120f);

        canvasOptions = new CanvasSizeOption[CanvasSizes.All.Length];
        for (int i = 0; i < canvasOptions.Length; i++)
        {
            GameObject option = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.CanvasSizeOption, canvasRow);
            option.name = "Size " + CanvasSizes.All[i];
            canvasOptions[i] = option.GetComponent<CanvasSizeOption>();
        }

        // Tablo: yalnizca yeni aliskanlikta gorunur (bkz. HabitSheet.Open).
        RectTransform paintingSection = BuildPaintingSection(
            content, theme, prefabs, out paintingRow, out paintingHint);
        paintingSectionRoot = paintingSection.gameObject;

        // Bitis tarihi
        RectTransform endSection = Section(content, theme, "habit_sheet.section.end_date", out TMP_Text endLabel);
        BuildEndDateToggle(endLabel.transform.parent as RectTransform, theme,
            out endDateToggle, out endDateTrack, out endDateKnob);

        endDateInput = BuildInput(endSection, theme, "habit_sheet.end_date_input.placeholder");
        endDateRow = endDateInput.gameObject;
        endDateRow.SetActive(false);

        return content;
    }

    // Kare adimlayici dugmesi. TimePickerSheetBuilder.StepperButton ile ayni
    // gorsel recete (kenarlikli yuvarlak kutu + ortali glif), tek fark konumu
    // satirin layout grubundan almasi: boylece "-" ve "+" arasindaki deger
    // etiketi de ayni satirda akiyor.
    private static Button RepsStepperButton(RectTransform parent, UiTheme theme, string glyph)
    {
        Image background = UiFactory.Image("Stepper " + glyph, parent, theme.Rounded(18), theme.borderFaint);
        UiFactory.Size(background.gameObject, 40f, 40f);

        TMP_Text label = UiFactory.Text("Label", background.rectTransform, glyph, theme.semiBold, 20f,
            theme.textPrimary, TextAlignmentOptions.Center);
        UiFactory.Fill(label.rectTransform);

        Button button = UiFactory.Button(background);
        ButtonPressFeedback.EnsureOn(button);
        return button;
    }

    // Tablo izgarasi: "Rastgele" + secili tuval boyutundaki her tablo.
    // Tile'lar burada ELLE uretilmiyor -- sayi kitaplik boyutuna ve tuval
    // filtresine gore degistigi icin PaintingPickerRow kendi havuzunu
    // calisma zamaninda kuruyor (bkz. DayChipRow kaligi).
    private static RectTransform BuildPaintingSection(
        RectTransform content,
        UiTheme theme,
        UiPrefabBuilder.Prefabs prefabs,
        out PaintingPickerRow row,
        out TMP_Text hint)
    {
        RectTransform section = Section(content, theme, "habit_sheet.section.painting", out TMP_Text label);

        // "Rastgele" BILEREK LocalizedText olmuyor -- PaintingPickerRow
        // calisma zamaninda bu hint'i hemen eziyor (placeholder, bkz.
        // LOCALIZATION_PLAN.md Faz 3 "placeholder/sayi metinleri DOKUNULMAZ").
        hint = UiFactory.Text("Hint", label.transform.parent, "Rastgele",
            theme.regular, 12f, theme.textMuted, TextAlignmentOptions.Right);
        RectTransform hintRect = hint.rectTransform;
        hintRect.anchorMin = new Vector2(1f, 1f);
        hintRect.anchorMax = new Vector2(1f, 1f);
        hintRect.pivot = new Vector2(1f, 1f);
        hintRect.anchoredPosition = Vector2.zero;
        hintRect.sizeDelta = new Vector2(160f, 16f);

        RectTransform grid = UiFactory.Rect("Options", section);
        GridLayoutGroup gridGroup = grid.gameObject.AddComponent<GridLayoutGroup>();
        gridGroup.spacing = new Vector2(8f, 8f);

        // GridCellFitter spacing'i Apply()'da okuyor; spacing'i ONDAN ONCE
        // yazmak gerekiyor cunku AddComponent aktif bir GameObject'te OnEnable'i
        // aninda tetikliyor (bkz. GridCellFitter.OnEnable).
        GridCellFitter fitter = grid.gameObject.AddComponent<GridCellFitter>();
        UiWiring.SetInt(fitter, "columns", 3);
        UiWiring.SetFloat(fitter, "extraHeight", 22f);

        UiFactory.FitVertical(grid.gameObject);

        row = grid.gameObject.AddComponent<PaintingPickerRow>();
        UiWiring.Set(row, "optionPrefab", prefabs.PaintingOption.GetComponent<PaintingOption>());
        UiWiring.Set(row, "optionRoot", grid);

        return section;
    }

    // Baslik + icerigi tasiyan dikey blok.
    private static RectTransform Section(RectTransform parent, UiTheme theme, string captionKey, out TMP_Text label)
    {
        RectTransform section = UiFactory.Rect(captionKey, parent);
        UiFactory.Column(section, 8f);
        UiFactory.FitVertical(section.gameObject);

        RectTransform header = UiFactory.Rect("Caption", section);
        UiFactory.Size(header.gameObject, -1f, 16f);

        label = UiFactory.LocalizedText("Label", header, captionKey, theme.semiBold, 12f, theme.gold, uppercase: true);
        UiFactory.Fill(label.rectTransform);

        return section;
    }

    private static TMP_InputField BuildInput(RectTransform parent, UiTheme theme, string placeholderKey)
    {
        // Kenarlik altta, dolgu onun icinde: cocuk her zaman ebeveynin ustune
        // cizildigi icin ters sira dolguyu gizlerdi.
        Image border = UiFactory.Image("Input", parent, theme.Rounded(14), theme.borderFaint);
        UiFactory.Size(border.gameObject, -1f, 50f);
        border.raycastTarget = true;

        Image background = UiFactory.Image("Fill", border.rectTransform, theme.Rounded(12), theme.background);
        UiFactory.Fill(background.rectTransform, 2f, 2f, 2f, 2f);

        RectTransform textArea = UiFactory.Fill(UiFactory.Rect("TextArea", border.rectTransform), 14f, 0f, 14f, 0f);
        textArea.gameObject.AddComponent<RectMask2D>();

        TMP_Text placeholderText = UiFactory.LocalizedText("Placeholder", textArea, placeholderKey,
            theme.medium, 16f, theme.textFaint);
        UiFactory.Fill(placeholderText.rectTransform);

        TMP_Text inputText = UiFactory.Text("Text", textArea, string.Empty, theme.medium, 16f, theme.textPrimary);
        UiFactory.Fill(inputText.rectTransform);

        TMP_InputField input = border.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = textArea;
        input.textComponent = (TextMeshProUGUI)inputText;
        input.placeholder = placeholderText;
        input.targetGraphic = border;
        input.lineType = TMP_InputField.LineType.SingleLine;

        return input;
    }

    private static void BuildEndDateToggle(
        RectTransform header,
        UiTheme theme,
        out Button toggle,
        out Image track,
        out RectTransform knob)
    {
        track = UiFactory.Image("Track", header, theme.Rounded(14), theme.borderSoft);
        RectTransform trackRect = track.rectTransform;
        trackRect.anchorMin = new Vector2(1f, 1f);
        trackRect.anchorMax = new Vector2(1f, 1f);
        trackRect.pivot = new Vector2(1f, 1f);
        trackRect.anchoredPosition = new Vector2(0f, 6f);
        trackRect.sizeDelta = new Vector2(48f, 28f);
        toggle = UiFactory.Button(track);

        Image knobImage = UiFactory.Image("Knob", trackRect, theme.circle, theme.textPrimary);
        knob = knobImage.rectTransform;
        knob.anchorMin = new Vector2(0f, 0.5f);
        knob.anchorMax = new Vector2(0f, 0.5f);
        knob.pivot = new Vector2(0f, 0.5f);
        knob.anchoredPosition = new Vector2(3f, 0f);
        knob.sizeDelta = new Vector2(22f, 22f);
    }

    private static void BuildSheetFooter(
        RectTransform sheet,
        UiTheme theme,
        out GameObject deleteRoot,
        out Button deleteButton,
        out GameObject archiveRoot,
        out Button archiveButton,
        out TMP_Text archiveLabel,
        out Button saveButton,
        out TMP_Text saveLabel,
        out Image saveBackground)
    {
        RectTransform footer = UiFactory.StretchBottom(UiFactory.Rect("Footer", sheet), 0f, 96f, 20f, 20f);
        HorizontalLayoutGroup row = UiFactory.Row(footer, 10f, TextAnchor.UpperLeft);
        row.padding = new RectOffset(0, 0, 12, 30);
        row.childForceExpandHeight = true;

        Image delete = UiFactory.Image("Delete", footer, theme.Rounded(14), theme.burgundy);
        UiFactory.Size(delete.gameObject, 84f, 54f);
        UiFactory.HardShadow(delete.gameObject, 4f, theme.burgundyShadow);
        deleteButton = UiFactory.Button(delete);
        deleteRoot = delete.gameObject;

        TMP_Text deleteLabel = UiFactory.LocalizedText("Label", delete.rectTransform, "common.delete_button",
            theme.semiBold, 15f, theme.textPrimary, TextAlignmentOptions.Center);
        UiFactory.Fill(deleteLabel.rectTransform);

        // Geri alinabilir bir eylem -- Sil'in tehlike rengi (burgundy) degil,
        // Ayarlar kartlarindaki NOTR yukselti gradyani (SettingsPanelBuilder'in
        // kart arka planiyla ayni ikili).
        Image archive = UiFactory.Image("Archive", footer, theme.Rounded(14), Color.white);
        UiFactory.Size(archive.gameObject, 84f, 54f);
        UiFactory.Gradient(archive.gameObject, theme.cardTop, theme.cardBottom);
        archiveButton = UiFactory.Button(archive);
        archiveRoot = archive.gameObject;

        // "Arşivle" BILEREK LocalizedText olmuyor -- HabitSheet.Open bu etiketi
        // arsivli/arsivsiz durumuna gore (common.archive_button/unarchive_button)
        // yeniden yaziyor, Editor varsayilani hep ezilir.
        archiveLabel = UiFactory.Text("Label", archive.rectTransform, "Arşivle",
            theme.semiBold, 14f, theme.textPrimary, TextAlignmentOptions.Center);
        UiFactory.Fill(archiveLabel.rectTransform);

        saveBackground = UiFactory.Image("Save", footer, theme.Rounded(14), Color.white);
        LayoutElement saveElement = saveBackground.gameObject.AddComponent<LayoutElement>();
        saveElement.flexibleWidth = 1f;
        saveElement.preferredHeight = 54f;
        saveElement.minHeight = 54f;
        UiFactory.Gradient(saveBackground.gameObject, theme.goldButtonTop, theme.goldButtonBottom);
        UiFactory.HardShadow(saveBackground.gameObject, 4f, theme.goldButtonShadow);
        saveButton = UiFactory.Button(saveBackground);

        // "Ekle" BILEREK LocalizedText olmuyor -- HabitSheet.Open bu etiketi
        // yeni/duzenleme durumuna gore (common.add_button/save_button)
        // yeniden yaziyor, Editor varsayilani hep ezilir.
        saveLabel = UiFactory.Text("Label", saveBackground.rectTransform, "Ekle",
            theme.semiBold, 16f, theme.textOnGold, TextAlignmentOptions.Center);
        UiFactory.Fill(saveLabel.rectTransform);
    }

    private static OptionButton SpawnOption(GameObject prefab, RectTransform parent, string titleKey, string hintKey)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = titleKey;

        TMP_Text[] texts = instance.GetComponentsInChildren<TMP_Text>(true);
        if (texts.Length > 0)
        {
            texts[0].gameObject.AddComponent<LocalizedText>().Bind(texts[0], titleKey);
        }

        if (texts.Length > 1)
        {
            texts[1].gameObject.AddComponent<LocalizedText>().Bind(texts[1], hintKey);
        }

        return instance.GetComponent<OptionButton>();
    }
}
