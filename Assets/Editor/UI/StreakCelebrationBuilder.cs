using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Seri kutlamasinin tam ekran overlay'ini kurar.
//
// Ayri dosya: WorkshopSceneBuilder.cs zaten 565 satir, projenin 500 satirlik
// dosya butcesinin uzerinde -- PaintingChoiceSheetBuilder'in oradan ayrilma
// gerekcesiyle birebir ayni.
//
// Panel_Workshop'un SON cocugu olarak ekleniyor (PaintingChoiceSheet'ten de
// sonra): alt navigasyon cubugunu ve secim kutusunu da ortmeli. Ayarlar
// UI Root seviyesinde oldugu icin kendiliginden bunun USTUNDE kaliyor.
public static class StreakCelebrationBuilder
{
    // Kutlama alevi 32x32'lik sayfadan TAM SAYI katla buyutuluyor (5x = 160):
    // piksel sanati kesirli olcekte bulanir.
    private const float FlameSize = 160f;

    private const float RayRingSize = 260f;
    private const float ContentGap = 16f;

    // "Devam": teslim dosyasi padding 12/44 diyor, yani metin yuksekligi 24 +
    // 2x12 = 48 ve "Devam" genisligi + 2x44 ~ 180.
    private const float ButtonWidth = 180f;
    private const float ButtonHeight = 48f;

    public static StreakCelebration Build(
        RectTransform parent,
        UiTheme theme,
        ScreenNavigator navigator)
    {
        RectTransform root = UiFactory.Fill(UiFactory.Rect("StreakCelebration", parent));

        CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        // Duz zemin: teslim dosyasinin radyal gradyaninin DIS rengi (#2B1B12).
        // Parlama bunun ustunde ayri bir gorsel -- tek sprite iki renk
        // tasiyamaz (bkz. UiSpriteGenerator.RadialGlow).
        //
        // raycastTarget ACIK: overlay acikken altindaki tile'lara dokunulmamali.
        Image backdrop = UiFactory.Image("Backdrop", root, theme.white, theme.background, false);
        UiFactory.Fill(backdrop.rectTransform);
        backdrop.raycastTarget = true;

        Image glow = BuildGlow(root, theme);

        RectTransform content = BuildContent(root);

        BuildFlame(content, theme, out RectTransform flameRow, out RectTransform flameScale,
            out CanvasGroup flameGroup, out FlameSprite flame);

        // Isinlar alevin KENDI satirinin cocugu, Content'in degil: Content'in
        // merkezi alev+metin+dugme yiginininin ortasi, yani alevin degil --
        // oraya baglansa halka alevin altinda kalirdi.
        Image rays = BuildRays(flameRow, theme);

        BuildText(content, theme, out RectTransform textBlock, out CanvasGroup textGroup,
            out TMP_Text title, out TMP_Text line);

        Button continueButton = BuildButton(content, theme,
            out RectTransform buttonBlock, out CanvasGroup buttonGroup);

        StreakCelebration celebration = root.gameObject.AddComponent<StreakCelebration>();
        UiWiring.Set(celebration, "group", group);
        UiWiring.Set(celebration, "navigator", navigator);
        UiWiring.Set(celebration, "glow", glow);
        UiWiring.Set(celebration, "rays", rays);
        UiWiring.Set(celebration, "flameScale", flameScale);
        UiWiring.Set(celebration, "flameGroup", flameGroup);
        UiWiring.Set(celebration, "flame", flame);
        UiWiring.Set(celebration, "textBlock", textBlock);
        UiWiring.Set(celebration, "textGroup", textGroup);
        UiWiring.Set(celebration, "buttonBlock", buttonBlock);
        UiWiring.Set(celebration, "buttonGroup", buttonGroup);
        UiWiring.Set(celebration, "titleLabel", title);
        UiWiring.Set(celebration, "lineLabel", line);
        UiWiring.Set(celebration, "continueButton", continueButton);
        UiWiring.Set(celebration, "theme", theme);

        root.gameObject.SetActive(false);
        return celebration;
    }

    // Radyal parlama. Teslim dosyasi merkezi (%50, %40) diyor: ekranin biraz
    // USTUNDE, cunku icerik blogu asagi dogru uzuyor ve parlamanin merkezi
    // alevle ayni hizada olmali.
    private static Image BuildGlow(RectTransform parent, UiTheme theme)
    {
        Image glow = UiFactory.Image("Glow", parent, theme.radialGlow, theme.background, false);
        RectTransform rect = glow.rectTransform;

        // Sprite'in kendisi kare ve kenarda tamamen seffaf; ekrandan BUYUK
        // cizdiriyoruz ki koseler de kapansin (en genis kenarin 1.6 kati).
        rect.anchorMin = new Vector2(0.5f, 0.4f);
        rect.anchorMax = new Vector2(0.5f, 0.4f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(1040f, 1040f);

        glow.raycastTarget = false;
        return glow;
    }

    private static RectTransform BuildContent(RectTransform parent)
    {
        RectTransform content = UiFactory.Fill(UiFactory.Rect("Content", parent), 24f, 24f, 24f, 24f);

        VerticalLayoutGroup column = UiFactory.Column(content, ContentGap, TextAnchor.MiddleCenter);
        column.childForceExpandHeight = false;

        return content;
    }

    // Kolonun tam genisligini alip TEK cocugunu ortalayan ara satir.
    //
    // Gerekli, cunku UiFactory.Column childForceExpandWidth=true kuruyor (metin
    // bloklari tam genisligi kullanip ortalanabilsin diye) -- ama alev 160x160
    // kare, dugme 180x48 sabit olmali. Dogrudan kolonun cocugu olsalardi
    // ikisi de 318 birime yayilirdi. UiPrefabBuilder'daki "StreakRow" ile ayni
    // kalip, ayni gerekce.
    private static RectTransform CenteredRow(RectTransform parent, string name, float height)
    {
        RectTransform row = UiFactory.Rect(name, parent);
        UiFactory.Row(row, 0f, TextAnchor.MiddleCenter);
        UiFactory.Size(row.gameObject, -1f, height);

        return row;
    }

    // Donen isin halkasi: alev satirinin icinde, alevle ayni merkezde.
    private static Image BuildRays(RectTransform parent, UiTheme theme)
    {
        Image rays = UiFactory.Image("Rays", parent, theme.rayRing, Color.clear, false);
        RectTransform rect = rays.rectTransform;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(RayRingSize, RayRingSize);

        rays.raycastTarget = false;

        // ignoreLayout: halka satirin bir HUCRESI degil, alevin arkasindaki
        // serbest bir sus -- yoksa satiri 260 birime genisletip alevi yana
        // iterdi. Ayrica ILK cocuk olmali ki alevin ALTINDA cizilsin.
        LayoutElement element = rays.gameObject.AddComponent<LayoutElement>();
        element.ignoreLayout = true;
        rect.SetAsFirstSibling();

        return rays;
    }

    // Alev iki katmanli: disaridaki "Flame" kolonun satiri (sabit 160x160),
    // icerideki "Scale" ise animasyonun olcekledigi katman. MoodCircle'in
    // ayni gerekcesi -- kokun rect'i olceklenirse kolon her karede akardi.
    private static void BuildFlame(
        RectTransform parent,
        UiTheme theme,
        out RectTransform row,
        out RectTransform scale,
        out CanvasGroup group,
        out FlameSprite flame)
    {
        row = CenteredRow(parent, "FlameRow", FlameSize);

        RectTransform slot = UiFactory.Rect("Flame", row);
        UiFactory.Size(slot.gameObject, FlameSize, FlameSize);

        scale = UiFactory.Fill(UiFactory.Rect("Scale", slot));
        group = scale.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        Image image = UiFactory.Image("Image", scale, null, Color.white, false);
        UiFactory.Fill(image.rectTransform);
        image.preserveAspect = true;
        image.raycastTarget = false;

        flame = image.gameObject.AddComponent<FlameSprite>();
        UiWiring.Set(flame, "image", image);
        UiWiring.Set(flame, "theme", theme);
    }

    private static void BuildText(
        RectTransform parent,
        UiTheme theme,
        out RectTransform block,
        out CanvasGroup group,
        out TMP_Text title,
        out TMP_Text line)
    {
        block = UiFactory.Rect("Text", parent);
        UiFactory.Column(block, 4f, TextAnchor.UpperCenter);
        UiFactory.FitVertical(block.gameObject);

        group = block.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        // Basamak ADI degil "YENİ ALEV": basamak adlari (Kivilcim, Mum, ...)
        // uygulamada hicbir yerde gorunmuyor (bkz. StreakFlame).
        TMP_Text eyebrow = UiFactory.LocalizedText("Eyebrow", block, "streak_celebration.eyebrow", theme.semiBold, 13f,
            theme.gold, TextAlignmentOptions.Center);
        eyebrow.characterSpacing = 10f;
        UiFactory.Size(eyebrow.gameObject, -1f, 17f);

        // "0 gün seri!" BILEREK LocalizedText olmuyor -- StreakCelebration bu
        // basligi her kuyruk ogesi icin streak_celebration.title'i string.Format
        // ile hemen yeniden yaziyor (bkz. StreakCelebration.cs:289).
        title = UiFactory.Text("Title", block, "0 gün seri!", theme.bold, 32f,
            theme.textPrimary, TextAlignmentOptions.Center);
        UiFactory.Size(title.gameObject, -1f, 38f);

        line = UiFactory.Text("Line", block, string.Empty, theme.regular, 14f,
            theme.textMuted, TextAlignmentOptions.Center);
        line.textWrappingMode = TextWrappingModes.Normal;
        UiFactory.Size(line.gameObject, -1f, 20f);
    }

    // "Devam" -- HabitSheet/PaintingChoiceSheet'in altin onay dugmesiyle ayni
    // gorunum (gradyan + alt golge + ButtonPressFeedback), ama kendi rect'i:
    // bu bir alt bar degil, ortali tek dugme.
    private static Button BuildButton(
        RectTransform parent,
        UiTheme theme,
        out RectTransform block,
        out CanvasGroup group)
    {
        block = UiFactory.Rect("Continue", CenteredRow(parent, "ContinueRow", ButtonHeight));
        UiFactory.Size(block.gameObject, ButtonWidth, ButtonHeight);

        group = block.gameObject.AddComponent<CanvasGroup>();

        Image background = UiFactory.Image("Background", block, theme.Rounded(18), Color.white);
        UiFactory.Fill(background.rectTransform);
        UiFactory.Gradient(background.gameObject, theme.goldButtonTop, theme.goldButtonBottom);
        UiFactory.HardShadow(background.gameObject, 4f, theme.goldButtonShadow);

        Button button = UiFactory.Button(background);

        TMP_Text label = UiFactory.LocalizedText("Label", background.rectTransform, "streak_celebration.continue_button",
            theme.semiBold, 16f, theme.textOnGold, TextAlignmentOptions.Center);
        UiFactory.Fill(label.rectTransform);

        return button;
    }
}
