using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Tekrar eden UI parcalarini prefab olarak uretir.
//
// Prefab'lar her calistirmada SIFIRDAN kuruluyor. Bu bilincli: iki yuz alani
// diff'leyip elle degistirilmis olani korumaya calismak bakilabilir degil.
// Gorunusu degistirmenin yolu UiTheme.asset'i duzenleyip komutu yeniden
// calistirmak -- prefab'lara elle dokunmak degil.
public static class UiPrefabBuilder
{
    public const string Folder = "Assets/Prefabs/UI";

    // Atolye tile'indaki seri cipi. Alev 16x16'lik bir sayfadan geliyor ve
    // 24'te 1.5x cizilecek -- teslim dosyasi bunu 3x yogunluklu telefonlarda
    // sorunsuz buluyor. Titreme gorulurse tam sayi olan 32'ye cikilmali
    // (ve TileInfoHeight de onunla birlikte).
    private const float TileFlameSize = 24f;

    // Cip yuksekligi: alev + 2'ser birim ust/alt dolgu.
    private const float TileChipHeight = TileFlameSize + 4f;

    public class Prefabs
    {
        public GameObject RoundIconButton;
        public GameObject MoodSegment;
        public GameObject MoodCircle;
        public GameObject FilterDiamond;
        public GameObject HabitTile;
        public GameObject HabitCard;
        public GameObject CategorySection;
        public GameObject DayChip;
        public GameObject DayToggle;
        public GameObject PlanDayChip;
        public GameObject CategoryPill;
        public GameObject TypeOptionCard;
        public GameObject CanvasSizeOption;
        public GameObject PaintingOption;
    }

    public static Prefabs Build(UiTheme theme)
    {
        EditorAssetUtil.EnsureFolderPath(Folder);

        Prefabs prefabs = new Prefabs
        {
            RoundIconButton = Save(BuildRoundIconButton(theme), "RoundIconButton"),
            MoodSegment = Save(BuildMoodSegment(theme), "MoodSegment"),
            MoodCircle = Save(BuildMoodCircle(theme), "MoodCircle"),
            FilterDiamond = Save(BuildFilterDiamond(theme), "FilterDiamond"),
            DayChip = Save(BuildDayChip(theme, 18f, 5, true), "DayChip"),
            DayToggle = Save(BuildDayChip(theme, 44f, 12, false), "DayToggle"),
            // Galeri 2. seviyenin plan satiri: DayToggle ile ayni "iki harf,
            // tiklanamaz" gorunumu ama teslim dosyasinin 36 yuksekligine ve
            // 10 yaricapina gore -- DayChipRow'un kendisi interactable=false
            // ile kullaniyor, burada SADECE boyut farki var.
            PlanDayChip = Save(BuildDayChip(theme, 36f, 10, false), "PlanDayChip"),
            CategoryPill = Save(BuildCategoryPill(theme), "CategoryPill"),
            TypeOptionCard = Save(BuildTypeOptionCard(theme), "TypeOptionCard"),
            CanvasSizeOption = Save(BuildCanvasSizeOption(theme), "CanvasSizeOption"),
            PaintingOption = Save(BuildPaintingOption(theme), "PaintingOption")
        };

        prefabs.HabitTile = Save(BuildHabitTile(theme), "HabitTile");

        GameObject card = BuildHabitCard(theme, prefabs.DayChip);
        prefabs.HabitCard = Save(card, "HabitCard");
        prefabs.CategorySection = Save(BuildCategorySection(theme, prefabs.HabitCard), "CategorySection");

        return prefabs;
    }

    private static GameObject Save(GameObject instance, string name)
    {
        instance.name = name;
        string path = Folder + "/" + name + ".prefab";

        GameObject asset = PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        return asset;
    }

    private static RectTransform Root(string name, float width, float height)
    {
        RectTransform rect = UiFactory.Rect(name, null);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    // -----------------------------------------------------------------------
    // Kucuk parcalar
    // -----------------------------------------------------------------------

    private static GameObject BuildRoundIconButton(UiTheme theme)
    {
        RectTransform root = Root("RoundIconButton", 38f, 38f);

        Image border = UiFactory.Image("Border", root, theme.circle, theme.borderGold);
        UiFactory.Fill(border.rectTransform);
        UiFactory.HardShadow(border.gameObject, 3f, theme.hardShadow);
        UiFactory.Button(border);

        Image fill = UiFactory.Image("Fill", border.rectTransform, theme.circle, theme.burgundy);
        UiFactory.Fill(fill.rectTransform, 2f, 2f, 2f, 2f);

        TMP_Text label = UiFactory.Text("Label", border.rectTransform, "‹", theme.semiBold, 20f,
            theme.textPrimary, TextAlignmentOptions.Center);
        UiFactory.Fill(label.rectTransform);

        return root.gameObject;
    }

    private static GameObject BuildMoodSegment(UiTheme theme)
    {
        RectTransform root = Root("MoodSegment", 8f, 10f);
        Image image = root.gameObject.AddComponent<Image>();
        image.sprite = theme.Rounded(5);
        image.type = Image.Type.Sliced;
        image.color = theme.gold;
        image.raycastTarget = false;

        UiFactory.Size(root.gameObject, 8f, 10f);
        return root.gameObject;
    }

    // Mod gostergesinin tek dairesi.
    //
    // Kokun olcusu SABIT (LayoutElement, MoodCircleSize kare) ve olcek iceride
    // "Scale" cocuguna uygulaniyor: secili daire buyurken satirin akmamasi icin
    // kokun rect'i hic degismemeli. Ayni sebeple opaklik da CanvasGroup'tan
    // geliyor, tek tek Image renklerinden degil.
    private static GameObject BuildMoodCircle(UiTheme theme)
    {
        float size = WorkshopBands.MoodCircleSize;

        RectTransform root = Root("MoodCircle", size, size);
        UiFactory.Size(root.gameObject, size, size);

        CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;

        // Gosterge dekoratif: dokunmayi YUTMAMALI. Painter.Update dokunmayi
        // EventSystem.IsPointerOverGameObject ile eliyor, yani raycast alan bir
        // parca ressama dokunmayi sessizce olduruyor. UiFactory.Image zaten
        // raycastTarget'i kapatiyor; bu da ikinci emniyet.
        group.blocksRaycasts = false;

        RectTransform scale = UiFactory.Fill(UiFactory.Rect("Scale", root));

        Image border = UiFactory.Image("Border", scale, theme.circle, theme.gold);
        UiFactory.Fill(border.rectTransform);

        Image fill = UiFactory.Image("Fill", border.rectTransform, theme.circle, theme.burgundy);
        UiFactory.Fill(fill.rectTransform, 2f, 2f, 2f, 2f);

        // Kullanicinin sonradan UiTheme.moodIcons'a koyacagi gorsel buraya
        // dusuyor; atanana kadar Image kapali ve daire sade kaliyor.
        Image icon = UiFactory.Image("Icon", fill.rectTransform, null, Color.white, false);
        UiFactory.Fill(icon.rectTransform, 4f, 4f, 4f, 4f);
        icon.preserveAspect = true;
        icon.enabled = false;

        MoodCircle circle = root.gameObject.AddComponent<MoodCircle>();
        UiWiring.Set(circle, "scaleRoot", scale);
        UiWiring.Set(circle, "group", group);
        UiWiring.Set(circle, "border", border);
        UiWiring.Set(circle, "fill", fill);
        UiWiring.Set(circle, "icon", icon);
        UiWiring.Set(circle, "theme", theme);

        return root.gameObject;
    }

    private static GameObject BuildFilterDiamond(UiTheme theme)
    {
        RectTransform root = Root("FilterDiamond", 62f, 92f);

        // Sutunun tamami tiklanabilir olsun: gorunmez ama raycast alan bir zemin.
        Image hit = UiFactory.Image("Hit", root, null, Color.clear, false);
        UiFactory.Fill(hit.rectTransform);
        Button button = UiFactory.Button(hit);

        RectTransform diamond = UiFactory.Rect("Diamond", root);
        diamond.anchorMin = new Vector2(0.5f, 1f);
        diamond.anchorMax = new Vector2(0.5f, 1f);
        // Pivot MERKEZDE olmali, ust-ortada DEGIL: kare 45 derece pivot
        // etrafinda donuyor ve ust-orta pivotla donunce gorsel merkez
        // kenar/(2*kok2) kadar saga kayiyor (50 birim icin 17.7). O kayma
        // elmasi kendi sutununun ortasindan cikariyor, altindaki yaziyla
        // hizasini bozuyor ve bes elmasin tamaminda birikerek satiri saga
        // itiyordu. Merkez pivotla donus merkezi hic oynatmiyor.
        diamond.pivot = new Vector2(0.5f, 0.5f);
        diamond.sizeDelta = new Vector2(50f, 50f);
        diamond.anchoredPosition = new Vector2(
            0f, -(4f + 50f * HabitFilterButton.RotatedCenterOffset));
        diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);

        Image border = UiFactory.Image("Border", diamond, theme.Rounded(12), theme.gold);
        UiFactory.Fill(border.rectTransform);
        UiFactory.HardShadow(border.gameObject, 4f, theme.hardShadow);

        Image background = UiFactory.Image("Background", border.rectTransform, theme.Rounded(12), Color.white);
        UiFactory.Fill(background.rectTransform, 2f, 2f, 2f, 2f);
        UiGradient gradient = UiFactory.Gradient(background.gameObject, theme.burgundyLight, theme.burgundy, 160f);

        TMP_Text count = UiFactory.Text("Count", border.rectTransform, "0", theme.bold, 16f,
            theme.textPrimary, TextAlignmentOptions.Center);
        UiFactory.Fill(count.rectTransform);
        // Elmas 45 derece dondurulmus; sayi duz dursun diye ters donuyor.
        count.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);

        TMP_Text name = UiFactory.Text("Name", root, "Tümü", theme.medium, 11f,
            theme.textMuted, TextAlignmentOptions.Center);
        UiFactory.StretchBottom(name.rectTransform, 0f, 16f, 0f, 0f);

        HabitFilterButton filter = root.gameObject.AddComponent<HabitFilterButton>();
        UiWiring.Set(filter, "button", button);
        UiWiring.Set(filter, "diamond", diamond);
        UiWiring.Set(filter, "diamondBackground", background);
        UiWiring.Set(filter, "diamondBorder", border);
        UiWiring.Set(filter, "diamondGradient", gradient);
        UiWiring.Set(filter, "countLabel", count);
        UiWiring.Set(filter, "nameLabel", name);
        UiWiring.Set(filter, "theme", theme);

        UiFactory.Size(root.gameObject, 62f, 92f);
        return root.gameObject;
    }

    private static GameObject BuildDayChip(UiTheme theme, float size, int radius, bool initials)
    {
        RectTransform root = Root(initials ? "DayChip" : "DayToggle", size, size);

        Image background = root.gameObject.AddComponent<Image>();
        background.sprite = theme.Rounded(radius);
        background.type = Image.Type.Sliced;
        background.color = theme.inputBackground;
        UiFactory.Button(background);

        TMP_Text label = UiFactory.Text("Label", root, "P", theme.semiBold, initials ? 10f : 13f,
            theme.textFaint, TextAlignmentOptions.Center);
        UiFactory.Fill(label.rectTransform);

        OptionButton option = root.gameObject.AddComponent<OptionButton>();
        UiWiring.Set(option, "background", background);
        UiWiring.Set(option, "label", label);
        UiWiring.SetColor(option, "selectedBackground", theme.goldSoft);
        UiWiring.SetColor(option, "unselectedBackground", theme.inputBackground);
        UiWiring.SetColor(option, "selectedLabel", theme.gold);
        UiWiring.SetColor(option, "unselectedLabel", theme.textFaint);

        if (initials)
        {
            UiFactory.Size(root.gameObject, size, size);
        }
        else
        {
            LayoutElement element = root.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = size;
            element.minHeight = size;
            element.flexibleWidth = 1f;
        }

        return root.gameObject;
    }

    private static GameObject BuildCategoryPill(UiTheme theme)
    {
        RectTransform root = Root("CategoryPill", 100f, 36f);

        // Kenarlik ALTTA (kok), dolgu onun iceri kacirilmis cocugu. uGUI'de cocuk
        // her zaman ebeveynin ustune cizildigi icin ters sira kenarligi degil
        // dolguyu gizlerdi.
        Image border = root.gameObject.AddComponent<Image>();
        border.sprite = theme.Rounded(18);
        border.type = Image.Type.Sliced;
        border.color = theme.borderSoft;
        UiFactory.Button(border);

        Image background = UiFactory.Image("Fill", root, theme.Rounded(16), Color.clear);
        UiFactory.Fill(background.rectTransform, 2f, 2f, 2f, 2f);

        TMP_Text label = UiFactory.Text("Label", root, "Sağlık", theme.medium, 14f,
            theme.textPrimary, TextAlignmentOptions.Center);
        UiFactory.Fill(label.rectTransform, 8f, 0f, 8f, 0f);
        label.overflowMode = TextOverflowModes.Ellipsis;

        // Boyutu ebeveyn (GridLayoutGroup) veriyor: kategori adlari farkli
        // uzunlukta ve esit genislikte duran piller daha duzenli duruyor.
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

        return root.gameObject;
    }

    private static GameObject BuildTypeOptionCard(UiTheme theme)
    {
        RectTransform root = Root("TypeOptionCard", 160f, 70f);

        Image border = root.gameObject.AddComponent<Image>();
        border.sprite = theme.Rounded(14);
        border.type = Image.Type.Sliced;
        border.color = theme.borderFaint;
        UiFactory.Button(border);

        Image background = UiFactory.Image("Fill", root, theme.Rounded(12), theme.background);
        UiFactory.Fill(background.rectTransform, 2f, 2f, 2f, 2f);

        RectTransform body = UiFactory.Fill(UiFactory.Rect("Body", root), 12f, 12f, 12f, 12f);
        UiFactory.Column(body, 2f);

        TMP_Text title = UiFactory.Text("Title", body, "Yap", theme.semiBold, 15f, theme.textPrimary);
        TMP_Text hint = UiFactory.Text("Hint", body, "Her gün yapınca tik at", theme.regular, 12f, theme.textMuted);
        hint.textWrappingMode = TextWrappingModes.Normal;
        UiFactory.Size(title.gameObject, -1f, 20f);
        UiFactory.Size(hint.gameObject, -1f, 30f);

        OptionButton option = root.gameObject.AddComponent<OptionButton>();
        UiWiring.Set(option, "background", background);
        UiWiring.Set(option, "border", border);
        UiWiring.Set(option, "label", title);
        UiWiring.SetColor(option, "selectedBackground", theme.goldSoft);
        UiWiring.SetColor(option, "unselectedBackground", theme.background);
        UiWiring.SetColor(option, "selectedBorder", theme.gold);
        UiWiring.SetColor(option, "unselectedBorder", theme.borderFaint);
        UiWiring.SetColor(option, "selectedLabel", theme.gold);
        UiWiring.SetColor(option, "unselectedLabel", theme.textPrimary);

        LayoutElement element = root.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 70f;
        element.flexibleWidth = 1f;

        return root.gameObject;
    }

    private static GameObject BuildCanvasSizeOption(UiTheme theme)
    {
        RectTransform root = Root("CanvasSizeOption", 100f, 120f);

        Image border = root.gameObject.AddComponent<Image>();
        border.sprite = theme.Rounded(14);
        border.type = Image.Type.Sliced;
        border.color = theme.borderFaint;
        UiFactory.Button(border);

        Image background = UiFactory.Image("Fill", root, theme.Rounded(12), theme.background);
        UiFactory.Fill(background.rectTransform, 2f, 2f, 2f, 2f);

        RectTransform body = UiFactory.Fill(UiFactory.Rect("Body", root), 10f, 12f, 10f, 10f);
        VerticalLayoutGroup column = UiFactory.Column(body, 8f, TextAnchor.UpperCenter);
        column.childForceExpandWidth = true;

        RectTransform previewRoot = UiFactory.Rect("Preview", body);
        UiFactory.Size(previewRoot.gameObject, 56f, 56f);
        PaintingCoverGrid preview = previewRoot.gameObject.AddComponent<PaintingCoverGrid>();
        UiWiring.Set(preview, "cellSprite", theme.white);
        UiWiring.SetColor(preview, "closedColor", theme.textFaint);
        UiWiring.SetFloat(preview, "spacing", 1f);
        UiWiring.SetFloat(preview, "fadeSeconds", 0f);

        TMP_Text count = UiFactory.Text("Count", body, "32 parça", theme.semiBold, 14f,
            theme.textPrimary, TextAlignmentOptions.Center);
        TMP_Text hint = UiFactory.Text("Hint", body, "Orta", theme.regular, 11f,
            theme.textMuted, TextAlignmentOptions.Center);
        UiFactory.Size(count.gameObject, -1f, 18f);
        UiFactory.Size(hint.gameObject, -1f, 14f);

        OptionButton option = root.gameObject.AddComponent<OptionButton>();
        UiWiring.Set(option, "background", background);
        UiWiring.Set(option, "border", border);
        UiWiring.Set(option, "label", count);
        UiWiring.SetColor(option, "selectedBackground", theme.goldSoft);
        UiWiring.SetColor(option, "unselectedBackground", theme.background);
        UiWiring.SetColor(option, "selectedBorder", theme.gold);
        UiWiring.SetColor(option, "unselectedBorder", theme.borderFaint);
        UiWiring.SetColor(option, "selectedLabel", theme.gold);
        UiWiring.SetColor(option, "unselectedLabel", theme.textPrimary);

        CanvasSizeOption sizeOption = root.gameObject.AddComponent<CanvasSizeOption>();
        UiWiring.Set(sizeOption, "option", option);
        UiWiring.Set(sizeOption, "preview", preview);
        UiWiring.Set(sizeOption, "countLabel", count);
        UiWiring.Set(sizeOption, "hintLabel", hint);
        UiWiring.Set(sizeOption, "theme", theme);

        LayoutElement element = root.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 120f;
        element.flexibleWidth = 1f;

        return root.gameObject;
    }

    // Formdaki tablo izgarasinin tek karesi -- CanvasSizeOption'in aynisi,
    // onizleme yerine gercek tablonun kucuk resmi (ya da Rastgele icin kapali
    // PaintingCoverGrid) var. Boyutu GridCellFitter veriyor (bkz.
    // HabitsPanelBuilder.BuildPaintingSection), bu yuzden burada sabit bir
    // LayoutElement yok.
    private static GameObject BuildPaintingOption(UiTheme theme)
    {
        RectTransform root = Root("PaintingOption", 100f, 120f);

        Image border = root.gameObject.AddComponent<Image>();
        border.sprite = theme.Rounded(14);
        border.type = Image.Type.Sliced;
        border.color = theme.borderFaint;
        UiFactory.Button(border);

        Image background = UiFactory.Image("Fill", root, theme.Rounded(12), theme.background);
        UiFactory.Fill(background.rectTransform, 2f, 2f, 2f, 2f);

        RectTransform body = UiFactory.Fill(UiFactory.Rect("Body", root), 10f, 12f, 10f, 10f);
        VerticalLayoutGroup column = UiFactory.Column(body, 8f, TextAnchor.UpperCenter);
        column.childForceExpandWidth = true;

        RectTransform previewRoot = UiFactory.Rect("Preview", body);
        UiFactory.Size(previewRoot.gameObject, 56f, 56f);

        Image thumbnail = UiFactory.Image("Thumbnail", previewRoot, null, Color.white, false);
        UiFactory.Fill(thumbnail.rectTransform);
        thumbnail.preserveAspect = true;

        RectTransform coverRoot = UiFactory.Fill(UiFactory.Rect("Cover", previewRoot));
        PaintingCoverGrid cover = coverRoot.gameObject.AddComponent<PaintingCoverGrid>();
        UiWiring.Set(cover, "cellSprite", theme.white);
        UiWiring.SetColor(cover, "closedColor", theme.textFaint);
        UiWiring.SetFloat(cover, "spacing", 1f);
        UiWiring.SetFloat(cover, "fadeSeconds", 0f);

        TMP_Text name = UiFactory.Text("Name", body, "Rastgele", theme.semiBold, 12f,
            theme.textPrimary, TextAlignmentOptions.Center);
        name.overflowMode = TextOverflowModes.Ellipsis;
        UiFactory.Size(name.gameObject, -1f, 16f);

        OptionButton option = root.gameObject.AddComponent<OptionButton>();
        UiWiring.Set(option, "background", background);
        UiWiring.Set(option, "border", border);
        UiWiring.Set(option, "label", name);
        UiWiring.SetColor(option, "selectedBackground", theme.goldSoft);
        UiWiring.SetColor(option, "unselectedBackground", theme.background);
        UiWiring.SetColor(option, "selectedBorder", theme.gold);
        UiWiring.SetColor(option, "unselectedBorder", theme.borderFaint);
        UiWiring.SetColor(option, "selectedLabel", theme.gold);
        UiWiring.SetColor(option, "unselectedLabel", theme.textPrimary);

        PaintingOption paintingOption = root.gameObject.AddComponent<PaintingOption>();
        UiWiring.Set(paintingOption, "option", option);
        UiWiring.Set(paintingOption, "thumbnail", thumbnail);
        UiWiring.Set(paintingOption, "cover", cover);
        UiWiring.Set(paintingOption, "nameLabel", name);
        UiWiring.Set(paintingOption, "theme", theme);

        return root.gameObject;
    }

    // -----------------------------------------------------------------------
    // Atolye tile'i
    // -----------------------------------------------------------------------

    private static GameObject BuildHabitTile(UiTheme theme)
    {
        // Hucre boyutunu GridCellFitter veriyor: kare gorsel + altta 44 px metin.
        RectTransform root = Root("HabitTile", 100f, 100f + WorkshopBands.TileInfoHeight);

        // Giris animasyonu (fade+scale) bu araya, "Visual" sarmalayicisina
        // uygulanacak -- root'un KENDISINE DEGIL. PaintingViewBinder.IsVisible
        // tile.Rect'in (= bu root'un) merkezini okuyup gorunurluk/view-baglama
        // kararini veriyor; root'un pivotu (0,1) oldugundan root'u olceklemek
        // o merkez noktasini kaydirir ve binder'in kararini animasyon
        // surerken yanlis karede bozabilir. Fill() pivotu (0.5,0.5) verdigi
        // icin bu sarmalayici merkezden duzgun buzuluyor/buyuyor, root ise
        // boyut/konum acisindan tamamen degismeden kaliyor (bkz. UI_ANIMATION_PLAN.md Faz 5(B)).
        RectTransform visual = UiFactory.Fill(UiFactory.Rect("Visual", root));

        // Kare kart: genislikten yuksekligi turetiyor, boylece hucre genisligi
        // cihazdan cihaza degisse de kart kare kaliyor.
        RectTransform card = UiFactory.Rect("Card", visual);
        card.anchorMin = new Vector2(0f, 1f);
        card.anchorMax = new Vector2(1f, 1f);
        card.pivot = new Vector2(0.5f, 1f);
        card.offsetMin = new Vector2(0f, -100f);
        card.offsetMax = Vector2.zero;
        AspectRatioFitter aspect = card.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
        aspect.aspectRatio = 1f;

        Image frame = UiFactory.Image("Frame", card, theme.Rounded(22), theme.borderFaint);
        UiFactory.Fill(frame.rectTransform);
        UiFactory.HardShadow(frame.gameObject, 5f, theme.hardShadow);
        Button button = UiFactory.Button(frame);

        Image fill = UiFactory.Image("Fill", frame.rectTransform, theme.Rounded(20), Color.white);
        UiFactory.Fill(fill.rectTransform, 3f, 3f, 3f, 3f);
        UiFactory.Gradient(fill.gameObject, theme.cardTop, theme.cardBottom, 160f);

        // Tablo kartin %62'si, ortali.
        RectTransform box = UiFactory.Rect("PaintingBox", frame.rectTransform);
        box.anchorMin = new Vector2(0.19f, 0.19f);
        box.anchorMax = new Vector2(0.81f, 0.81f);
        box.pivot = new Vector2(0.5f, 0.5f);
        box.offsetMin = Vector2.zero;
        box.offsetMax = Vector2.zero;

        RectTransform staticRoot = UiFactory.Fill(UiFactory.Rect("Static", box));
        Image painting = UiFactory.Image("Painting", staticRoot, null, Color.white, false);
        UiFactory.Fill(painting.rectTransform);

        RectTransform coverRoot = UiFactory.Fill(UiFactory.Rect("Cover", staticRoot));
        PaintingCoverGrid cover = coverRoot.gameObject.AddComponent<PaintingCoverGrid>();
        UiWiring.Set(cover, "cellSprite", theme.white);
        UiWiring.SetColor(cover, "closedColor", theme.coverClosed);
        UiWiring.SetColor(cover, "missedColor", theme.coverMissed);

        RectTransform renderedRoot = UiFactory.Fill(UiFactory.Rect("Rendered", box));
        RawImage rendered = renderedRoot.gameObject.AddComponent<RawImage>();
        // Dokunma alttaki Frame butonuna gitmeli.
        rendered.raycastTarget = false;
        renderedRoot.gameObject.SetActive(false);

        // Secim beklerken AttentionPulse'un ortasinda aydinlik/karanlik nefes
        // aldirdigi ortu. box'un SON cocugu: Static ve Rendered'in USTUNDE
        // cizilmeli, hangisi aktif olursa olsun (bagli/bagsiz tile).
        Image choiceGlow = UiFactory.Image("ChoiceGlow", box, theme.white, Color.clear, false);
        UiFactory.Fill(choiceGlow.rectTransform);
        choiceGlow.raycastTarget = false;

        RectTransform badge = UiFactory.TopLeft(
            UiFactory.Rect("DoneBadge", frame.rectTransform), 0f, 0f, 24f, 24f);
        badge.anchorMin = new Vector2(1f, 1f);
        badge.anchorMax = new Vector2(1f, 1f);
        badge.pivot = new Vector2(1f, 1f);
        badge.anchoredPosition = new Vector2(8f, 8f);
        Image badgeImage = badge.gameObject.AddComponent<Image>();
        badgeImage.sprite = theme.circle;
        badgeImage.color = theme.gold;
        badgeImage.raycastTarget = false;
        // Font'a bagimli TMP "✓" karakteri DEGIL: Fredoka-Bold SDF (ve
        // fallback'leri) bu glyph'i icermiyordu, eksik glyph TextMeshPro'yu
        // her karede atlas/duzen yeniden kurmaya zorluyor ve bu da
        // Canvas.ForceUpdateCanvases dongusune girip Editor'u konsol
        // spam'iyla dondurmus gibi gosteriyordu (bkz. UiSpriteGenerator.Checkmark).
        Image check = UiFactory.Image("Check", badge, theme.checkmark, theme.textOnGold, false);
        UiFactory.Fill(check.rectTransform, 5f, 5f, 5f, 5f);

        TMP_Text typeLabel = UiFactory.Text("TypeLabel", frame.rectTransform, "YAP",
            theme.semiBold, 10f, theme.buildColor);
        UiFactory.TopLeft(typeLabel.rectTransform, 8f, 0f, 60f, 14f);
        typeLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
        typeLabel.rectTransform.anchorMax = new Vector2(0f, 0f);
        typeLabel.rectTransform.pivot = new Vector2(0f, 0f);
        typeLabel.rectTransform.anchoredPosition = new Vector2(8f, 6f);

        // Karenin altindaki metin blogu. Yukseklik 40 -> InfoHeight: seri artik
        // tek satir metin degil, 24'luk alevi tasiyan bir cip (bkz. asagisi).
        // WorkshopSceneBuilder.BuildTileGrid'in GridCellFitter.extraHeight'i ile
        // AYNI sayi olmak zorunda, yoksa cip hucreden tasar -- o yuzden ikisi de
        // buradaki sabiti okuyor.
        RectTransform info = UiFactory.StretchBottom(
            UiFactory.Rect("Info", visual), 0f, WorkshopBands.TileInfoHeight, 0f, 0f);
        VerticalLayoutGroup infoColumn = UiFactory.Column(info, 3f, TextAnchor.UpperCenter);
        infoColumn.padding = new RectOffset(0, 0, 7, 0);

        TMP_Text title = UiFactory.Text("Title", info, "Alışkanlık", theme.medium, 13f,
            theme.textPrimary, TextAlignmentOptions.Top);
        title.textWrappingMode = TextWrappingModes.Normal;
        title.overflowMode = TextOverflowModes.Ellipsis;
        UiFactory.Size(title.gameObject, -1f, 17f);

        // Seri cipi iki katmanli, ve bu SART: "Info" kolonu
        // childForceExpandWidth=true (baslik tile'in tam genisligini kullanip
        // satir kirabilsin diye), yani kolonun DOGRUDAN cocugu olan her sey
        // tam genislige yayilir -- pil zemini de 100 birim boyunca uzanirdi.
        //
        // Disaridaki "StreakRow" o tam genisligi aliyor ve tek cocugunu
        // ORTALIYOR; icerideki "StreakChip" ise childForceExpandWidth=false
        // sayesinde kendi TERCIH ETTIGI genislikte kaliyor -- yani icerige
        // gore daralip genisliyor (tek haneli seri ile uc haneli ayni
        // genislikte durmuyor). ContentSizeFitter'a gerek yok: tercih edilen
        // genisligi cipin kendi HorizontalLayoutGroup'u zaten hesapliyor ve
        // ustune bir de Fitter koymak ikisini ayni alan icin yarismaya sokardi.
        RectTransform streakRow = UiFactory.Rect("StreakRow", info);
        UiFactory.Row(streakRow, 0f, TextAnchor.MiddleCenter);
        UiFactory.Size(streakRow.gameObject, -1f, TileChipHeight);

        RectTransform chip = UiFactory.Rect("StreakChip", streakRow);
        Image chipBackground = chip.gameObject.AddComponent<Image>();
        chipBackground.sprite = theme.Rounded(12);
        chipBackground.type = Image.Type.Sliced;
        chipBackground.color = theme.flameChipBackground;
        chipBackground.raycastTarget = false;

        HorizontalLayoutGroup chipRow = UiFactory.Row(chip, 3f, TextAnchor.MiddleCenter);
        chipRow.padding = new RectOffset(4, 9, 2, 2);
        UiFactory.Size(chip.gameObject, -1f, TileChipHeight);

        // Alev IKI katmanli: disaridaki "FlameSlot" duzenin gordugu kutu
        // (24x24 LayoutElement), icerideki "Image" ise FlameSprite'in optik
        // hizalama icin YUKARI KAYDIRDIGI katman. Tek katman olsaydi
        // HorizontalLayoutGroup her duzen gecisinde cocugunun
        // anchoredPosition'ini kendisi yazip kaydirmayi geri alirdi
        // (bkz. FlameSprite.ApplyLift).
        RectTransform flameSlot = UiFactory.Rect("FlameSlot", chip);
        UiFactory.Size(flameSlot.gameObject, TileFlameSize, TileFlameSize);

        Image flameImage = UiFactory.Image("Image", flameSlot, null, Color.white, false);
        UiFactory.Fill(flameImage.rectTransform);
        flameImage.preserveAspect = true;
        flameImage.raycastTarget = false;
        FlameSprite flame = flameImage.gameObject.AddComponent<FlameSprite>();
        UiWiring.Set(flame, "image", flameImage);
        UiWiring.Set(flame, "theme", theme);

        TMP_Text streak = UiFactory.Text("Streak", chip, "0", theme.semiBold, 12f,
            theme.textMuted, TextAlignmentOptions.Midline);
        streak.lineSpacing = 0f;

        HabitTile tile = root.gameObject.AddComponent<HabitTile>();
        UiWiring.Set(tile, "visualRoot", visual);
        UiWiring.Set(tile, "button", button);
        UiWiring.Set(tile, "frame", frame);
        UiWiring.Set(tile, "staticRoot", staticRoot.gameObject);
        UiWiring.Set(tile, "staticPainting", painting);
        UiWiring.Set(tile, "cover", cover);
        UiWiring.Set(tile, "rendered", rendered);
        UiWiring.Set(tile, "doneBadge", badge.gameObject);
        UiWiring.Set(tile, "choiceGlow", choiceGlow);
        UiWiring.Set(tile, "typeLabel", typeLabel);
        UiWiring.Set(tile, "title", title);
        UiWiring.Set(tile, "streak", streak);
        UiWiring.Set(tile, "flame", flame);
        UiWiring.Set(tile, "theme", theme);

        return root.gameObject;
    }

    // -----------------------------------------------------------------------
    // Aliskanlik karti ve kategori blogu
    // -----------------------------------------------------------------------

    private static GameObject BuildHabitCard(UiTheme theme, GameObject dayChipPrefab)
    {
        RectTransform root = Root("HabitCard", 326f, 84f);

        Image border = root.gameObject.AddComponent<Image>();
        border.sprite = theme.Rounded(20);
        border.type = Image.Type.Sliced;
        border.color = theme.borderFaint;
        Button button = UiFactory.Button(border);
        UiFactory.HardShadow(root.gameObject, 4f, theme.hardShadow);

        Image fill = UiFactory.Image("Fill", root, theme.Rounded(18), Color.white);
        UiFactory.Fill(fill.rectTransform, 2f, 2f, 2f, 2f);
        UiFactory.Gradient(fill.gameObject, theme.cardTop, theme.cardBottom, 160f);

        HorizontalLayoutGroup row = UiFactory.Row(fill.rectTransform, 14f, TextAnchor.MiddleLeft);
        row.padding = new RectOffset(10, 12, 10, 10);

        RectTransform thumb = UiFactory.Rect("Thumb", fill.rectTransform);
        UiFactory.Size(thumb.gameObject, 60f, 60f);
        Image painting = UiFactory.Image("Painting", thumb, null, Color.white, false);
        UiFactory.Fill(painting.rectTransform);
        RectTransform coverRoot = UiFactory.Fill(UiFactory.Rect("Cover", thumb));
        PaintingCoverGrid cover = coverRoot.gameObject.AddComponent<PaintingCoverGrid>();
        UiWiring.Set(cover, "cellSprite", theme.white);
        UiWiring.SetColor(cover, "closedColor", theme.coverClosed);
        UiWiring.SetColor(cover, "missedColor", theme.coverMissed);
        UiWiring.SetFloat(cover, "fadeSeconds", 0f);

        RectTransform body = UiFactory.Rect("Body", fill.rectTransform);
        VerticalLayoutGroup column = UiFactory.Column(body, 7f);
        LayoutElement bodyElement = body.gameObject.AddComponent<LayoutElement>();
        bodyElement.flexibleWidth = 1f;

        // 1. satir: baslik + tur pili
        RectTransform titleRow = UiFactory.Rect("TitleRow", body);
        UiFactory.Row(titleRow, 8f);
        UiFactory.Size(titleRow.gameObject, -1f, 20f);

        TMP_Text title = UiFactory.Text("Title", titleRow, "Alışkanlık", theme.medium, 16f, theme.textPrimary);
        title.overflowMode = TextOverflowModes.Ellipsis;
        LayoutElement titleElement = title.gameObject.AddComponent<LayoutElement>();
        titleElement.flexibleWidth = 1f;

        RectTransform pill = UiFactory.Rect("TypePill", titleRow);
        Image pillBackground = pill.gameObject.AddComponent<Image>();
        pillBackground.sprite = theme.Rounded(6);
        pillBackground.type = Image.Type.Sliced;
        pillBackground.color = theme.inputBackground;
        pillBackground.raycastTarget = false;
        UiFactory.Size(pill.gameObject, 52f, 18f);
        TMP_Text typeLabel = UiFactory.Text("Label", pill, "YAP", theme.semiBold, 10f,
            theme.buildColor, TextAlignmentOptions.Center);
        UiFactory.Fill(typeLabel.rectTransform);

        // 2. satir: gun rozetleri
        RectTransform dayRow = UiFactory.Rect("Days", body);
        UiFactory.Row(dayRow, 4f);
        UiFactory.Size(dayRow.gameObject, -1f, 18f);
        DayChipRow days = dayRow.gameObject.AddComponent<DayChipRow>();
        UiWiring.Set(days, "chipPrefab", dayChipPrefab.GetComponent<OptionButton>());
        UiWiring.Set(days, "root", dayRow);
        UiWiring.SetBool(days, "useInitials", true);
        UiWiring.SetBool(days, "interactable", false);

        // 3. satir: ilerleme + seri
        RectTransform progressRow = UiFactory.Rect("Progress", body);
        UiFactory.Row(progressRow, 8f, TextAnchor.MiddleLeft);
        UiFactory.Size(progressRow.gameObject, -1f, 14f);

        RectTransform track = UiFactory.Rect("Track", progressRow);
        Image trackImage = track.gameObject.AddComponent<Image>();
        trackImage.sprite = theme.Rounded(5);
        trackImage.type = Image.Type.Sliced;
        trackImage.color = theme.inputBackground;
        trackImage.raycastTarget = false;
        LayoutElement trackElement = track.gameObject.AddComponent<LayoutElement>();
        trackElement.flexibleWidth = 1f;
        trackElement.preferredHeight = 6f;
        trackElement.minHeight = 6f;

        Image progressFill = UiFactory.Image("Fill", track, theme.Rounded(5), theme.gold);
        progressFill.rectTransform.anchorMin = Vector2.zero;
        progressFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        progressFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        progressFill.rectTransform.offsetMin = Vector2.zero;
        progressFill.rectTransform.offsetMax = Vector2.zero;

        TMP_Text streak = UiFactory.Text("Streak", progressRow, "0 gün · en iyi 0",
            theme.regular, 11f, theme.textMuted, TextAlignmentOptions.Right);
        UiFactory.Size(streak.gameObject, 120f, 14f);

        HabitCard card = root.gameObject.AddComponent<HabitCard>();
        UiWiring.Set(card, "button", button);
        UiWiring.Set(card, "painting", painting);
        UiWiring.Set(card, "cover", cover);
        UiWiring.Set(card, "title", title);
        UiWiring.Set(card, "typeLabel", typeLabel);
        UiWiring.Set(card, "days", days);
        UiWiring.Set(card, "progressFill", progressFill.rectTransform);
        UiWiring.Set(card, "streakLabel", streak);
        UiWiring.Set(card, "theme", theme);

        LayoutElement element = root.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 84f;
        element.minHeight = 84f;

        return root.gameObject;
    }

    private static GameObject BuildCategorySection(UiTheme theme, GameObject cardPrefab)
    {
        RectTransform root = Root("CategorySection", 326f, 120f);
        UiFactory.Column(root, 10f);
        UiFactory.FitVertical(root.gameObject);

        RectTransform header = UiFactory.Rect("Header", root);
        UiFactory.Row(header, 8f, TextAnchor.LowerLeft);
        UiFactory.Size(header.gameObject, -1f, 18f);
        header.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(4, 4, 0, 0);

        TMP_Text name = UiFactory.Text("Name", header, "SAĞLIK", theme.semiBold, 13f, theme.gold);
        LayoutElement nameElement = name.gameObject.AddComponent<LayoutElement>();
        nameElement.flexibleWidth = 1f;

        TMP_Text count = UiFactory.Text("Count", header, "0", theme.regular, 12f,
            theme.textMuted, TextAlignmentOptions.Right);
        UiFactory.Size(count.gameObject, 30f, 16f);

        RectTransform cards = UiFactory.Rect("Cards", root);
        UiFactory.Column(cards, 10f);
        UiFactory.FitVertical(cards.gameObject);

        HabitCategorySection section = root.gameObject.AddComponent<HabitCategorySection>();
        UiWiring.Set(section, "nameLabel", name);
        UiWiring.Set(section, "countLabel", count);
        UiWiring.Set(section, "cardPrefab", cardPrefab.GetComponent<HabitCard>());
        UiWiring.Set(section, "cardRoot", cards);

        return root.gameObject;
    }
}
