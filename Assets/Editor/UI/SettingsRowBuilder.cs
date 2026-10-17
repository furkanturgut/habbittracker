using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Ayarlar ekraninin satir/kart/etiket fabrikasi. SettingsPanelBuilder'dan
// AYRI dosya -- PaintingChoiceSheetBuilder/WorkshopSceneBuilder ile ayni
// "500 satir butcesi" gerekcesi.
//
// Satir ici yerlesim HorizontalLayoutGroup DEGIL, MUTLAK konumlandirma:
// etiket solda sabit, "trailing" icerik sagda sabit (anchorMin/Max=(1,.5)) --
// bu codebase'teki ayni kalip (HabitsPanelBuilder.BuildEndDateToggle'in
// track'i, daysHint/paintingHint). Kartin kendisi (satir SAYISI degisken
// oldugu icin, bkz. "Saat" satiri) VerticalLayoutGroup + FitVertical ile
// buyuyor -- bu ikisi farkli sorunlar, farkli araclar.
public static class SettingsRowBuilder
{
    public const float CardRadius = 20f;
    public const float RowSidePadding = 16f;

    // Baslik + icerigi tasiyan blok: HabitsPanelBuilder.Section'in kalibi,
    // burada TEKRAR yaziliyor cunku bu dosya HabitsPanelBuilder'a BAGLI
    // olmak istemiyor (ayri ekran, ayri builder).
    public static RectTransform Section(RectTransform parent, UiTheme theme, string captionKey)
    {
        RectTransform section = UiFactory.Rect(captionKey, parent);
        UiFactory.Column(section, 10f);
        UiFactory.FitVertical(section.gameObject);

        TMP_Text label = UiFactory.LocalizedText("Caption", section, captionKey, theme.semiBold, 12f, theme.gold,
            uppercase: true);
        UiFactory.Size(label.gameObject, -1f, 16f);

        return section;
    }

    // Kart zemini: kenarlik + gradyan dolgu (RoundIconButton'daki "border
    // ALTTA buyuk, fill USTTE 2px icerde" kaliginin aynisi), sert golge.
    // Donus degeri satirlarin EKLENECEGI rect -- cagiran Row/Divider'i buna
    // ekler, kartin kendisi ContentSizeFitter ile onlara gore buyur.
    public static RectTransform Card(RectTransform parent, UiTheme theme)
    {
        Image border = UiFactory.Image("Card", parent, theme.Rounded((int)CardRadius), theme.borderFaint);
        RectTransform cardRect = border.rectTransform;
        UiFactory.HardShadow(border.gameObject, 4f, theme.hardShadow);

        Image fill = UiFactory.Image("Fill", cardRect, theme.Rounded((int)CardRadius - 2), Color.white);
        UiFactory.Fill(fill.rectTransform, 2f, 2f, 2f, 2f);
        UiFactory.Gradient(fill.gameObject, theme.cardTop, theme.cardBottom);
        // Zemin kartin BUYUMESINE katilmiyor -- buyuklugunu kart (ebeveyn)
        // belirliyor, tersi degil. Aksi halde ContentSizeFitter zemini de
        // bir "cocuk" sayip dongu olusturabilirdi.
        LayoutElement fillIgnore = fill.gameObject.AddComponent<LayoutElement>();
        fillIgnore.ignoreLayout = true;

        UiFactory.Column(cardRect, 0f);
        UiFactory.FitVertical(border.gameObject);

        return cardRect;
    }

    // Tek satir: sabit yukseklik, sol etiket. "trailing" MUTLAK sagda --
    // cagiran onu dolduruyor (switch/giris alani/segment/ok).
    public static RectTransform Row(
        RectTransform card, UiTheme theme, string labelKey, float height,
        out TMP_Text labelText, out RectTransform trailing)
    {
        RectTransform row = UiFactory.Rect("Row " + labelKey, card);
        UiFactory.Size(row.gameObject, -1f, height);

        labelText = UiFactory.LocalizedText("Label", row, labelKey, theme.medium, 15f, theme.textPrimary);
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = new Vector2(0f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(RowSidePadding, 0f);
        labelRect.sizeDelta = new Vector2(170f, 20f);

        trailing = UiFactory.Rect("Trailing", row);
        trailing.anchorMin = new Vector2(1f, 0.5f);
        trailing.anchorMax = new Vector2(1f, 0.5f);
        trailing.pivot = new Vector2(1f, 0.5f);
        trailing.anchoredPosition = new Vector2(-RowSidePadding, 0f);

        return row;
    }

    // Bir alt etiket satiri (orn. "Tik atilmamis aliskanlik varsa") -- ana
    // etiketin ALTINA, ayni rowRoot'un icinde. titleLabel burada yukari
    // kaydiriliyor ki iki satir ust uste binmesin, aralarinda bosluk kalsin.
    public static TMP_Text SubLabel(RectTransform row, UiTheme theme, string textKey, TMP_Text titleLabel)
    {
        RectTransform titleRect = titleLabel.rectTransform;
        titleRect.anchoredPosition = new Vector2(titleRect.anchoredPosition.x, 6f);

        TMP_Text sub = UiFactory.LocalizedText("SubLabel", row, textKey, theme.regular, 12f, theme.textMuted);
        RectTransform rect = sub.rectTransform;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(RowSidePadding, -15f);
        rect.sizeDelta = new Vector2(220f, 16f);
        sub.textWrappingMode = TextWrappingModes.Normal;
        return sub;
    }

    // Satirlar arasi ince ayirici -- kartin TAM genisligi (satirlarin kendi
    // 16px ic dolgusuna DAHIL degil, CSS'teki border-top ile ayni: dolgunun
    // disinda, kartin kenarindan kenarina).
    public static void Divider(RectTransform card, UiTheme theme)
    {
        Image line = UiFactory.Image("Divider", card, theme.white, theme.borderFaint, false);
        UiFactory.Size(line.gameObject, -1f, 1f);
    }

    // 48x28 ray + 22 topuz -- SettingsToggleRow'un gorsel kismi, HitArea
    // SATIRIN TAMAMI (teslim dosyasi: butun satir onClick).
    public static SettingsToggleRow Toggle(RectTransform row, RectTransform trailing, UiTheme theme)
    {
        Image hit = UiFactory.Image("Hit", row, theme.white, Color.clear, false);
        UiFactory.Fill(hit.rectTransform);
        Button hitButton = UiFactory.Button(hit);

        Image track = UiFactory.Image("Track", trailing, theme.Rounded(14), theme.moodSegmentEmpty);
        RectTransform trackRect = track.rectTransform;
        trackRect.anchorMin = new Vector2(1f, 0.5f);
        trackRect.anchorMax = new Vector2(1f, 0.5f);
        trackRect.pivot = new Vector2(1f, 0.5f);
        trackRect.anchoredPosition = Vector2.zero;
        trackRect.sizeDelta = new Vector2(48f, 28f);

        Image knob = UiFactory.Image("Knob", trackRect, theme.circle, theme.textPrimary);
        RectTransform knobRect = knob.rectTransform;
        knobRect.anchorMin = new Vector2(0f, 0.5f);
        knobRect.anchorMax = new Vector2(0f, 0.5f);
        knobRect.pivot = new Vector2(0f, 0.5f);
        knobRect.anchoredPosition = new Vector2(3f, 0f);
        knobRect.sizeDelta = new Vector2(22f, 22f);

        SettingsToggleRow component = row.gameObject.AddComponent<SettingsToggleRow>();
        UiWiring.Set(component, "hitArea", hitButton);
        UiWiring.Set(component, "track", track);
        UiWiring.Set(component, "knob", knobRect);
        UiWiring.Set(component, "theme", theme);

        return component;
    }

    // "Pazartesi / Pazar" ikili segment: tek ray zemini (segmentTrack) + iki
    // dar sekme. OptionButton'i KULLANMIYOR (bkz. SettingsSegmentRow basi).
    public static SettingsSegmentRow Segment(RectTransform trailing, UiTheme theme)
    {
        Image track = UiFactory.Image("Track", trailing, theme.Rounded(15), theme.segmentTrack);
        RectTransform trackRect = track.rectTransform;
        trackRect.anchorMin = new Vector2(1f, 0.5f);
        trackRect.anchorMax = new Vector2(1f, 0.5f);
        trackRect.pivot = new Vector2(1f, 0.5f);
        trackRect.anchoredPosition = Vector2.zero;
        trackRect.sizeDelta = new Vector2(96f, 36f);

        Button mondayButton = SegmentOption(trackRect, theme, "Pt", 3f, out Image mondayBg, out TMP_Text mondayLabel);
        Button sundayButton = SegmentOption(trackRect, theme, "Pz", 49f, out Image sundayBg, out TMP_Text sundayLabel);

        SettingsSegmentRow component = trailing.gameObject.AddComponent<SettingsSegmentRow>();
        UiWiring.Set(component, "mondayButton", mondayButton);
        UiWiring.Set(component, "mondayBackground", mondayBg);
        UiWiring.Set(component, "mondayLabel", mondayLabel);
        UiWiring.Set(component, "sundayButton", sundayButton);
        UiWiring.Set(component, "sundayBackground", sundayBg);
        UiWiring.Set(component, "sundayLabel", sundayLabel);
        UiWiring.Set(component, "theme", theme);

        return component;
    }

    private static Button SegmentOption(
        RectTransform track, UiTheme theme, string label, float left, out Image background, out TMP_Text labelText)
    {
        background = UiFactory.Image("Segment " + label, track, theme.Rounded(13), Color.clear);
        RectTransform rect = background.rectTransform;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(left, 0f);
        rect.sizeDelta = new Vector2(44f, 30f);

        labelText = UiFactory.Text("Label", background.rectTransform, label, theme.semiBold, 12f,
            theme.textMuted, TextAlignmentOptions.Center);
        UiFactory.Fill(labelText.rectTransform);

        return UiFactory.Button(background);
    }

    // Deger bir sag-ok ("navigasyon satiri" -- Verileri disa aktar / Ilerlemeyi
    // sifirla) + istege bagli PREMIUM rozeti. HitArea SATIRIN TAMAMI.
    public static Button NavRow(RectTransform row, RectTransform trailing, UiTheme theme, Color arrowColor, bool showPremiumBadge)
    {
        Image hit = UiFactory.Image("Hit", row, theme.white, Color.clear, false);
        UiFactory.Fill(hit.rectTransform);
        Button button = UiFactory.Button(hit);
        ButtonPressFeedback.EnsureOn(button);

        if (showPremiumBadge)
        {
            PremiumBadge(trailing, theme, -24f);
        }

        TMP_Text arrow = UiFactory.Text("Arrow", trailing, "›", theme.semiBold, 18f, arrowColor, TextAlignmentOptions.Center);
        RectTransform arrowRect = arrow.rectTransform;
        arrowRect.anchorMin = new Vector2(1f, 0.5f);
        arrowRect.anchorMax = new Vector2(1f, 0.5f);
        arrowRect.pivot = new Vector2(1f, 0.5f);
        arrowRect.anchoredPosition = Vector2.zero;
        arrowRect.sizeDelta = new Vector2(16f, 20f);

        return button;
    }

    // Deger satirin saginda gosterilen, dokunulabilir satir ("Saat" gibi) --
    // NavRow'un ok yerine DEGER gosteren ikizi. HitArea SATIRIN TAMAMI.
    public static Button ValueRow(RectTransform row, RectTransform trailing, UiTheme theme, out TMP_Text valueLabel)
    {
        Image hit = UiFactory.Image("Hit", row, theme.white, Color.clear, false);
        UiFactory.Fill(hit.rectTransform);
        Button button = UiFactory.Button(hit);
        ButtonPressFeedback.EnsureOn(button);

        valueLabel = UiFactory.Text("Value", trailing, string.Empty, theme.semiBold, 15f,
            theme.gold, TextAlignmentOptions.Right);
        RectTransform rect = valueLabel.rectTransform;
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(90f, 36f);

        return button;
    }

    // "PREMIUM" kucuk pil rozeti -- Premium kartinin basligindaki AYNI gorsel
    // (kenarlikli, sade boyali ikisi de kullaniyor; renk cagiran tarafca).
    public static void PremiumBadge(RectTransform parent, UiTheme theme, float rightOffset)
    {
        Image badge = UiFactory.Image("PremiumBadge", parent, theme.Rounded(9), Color.clear);
        RectTransform rect = badge.rectTransform;
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(rightOffset, 0f);
        rect.sizeDelta = new Vector2(64f, 18f);

        Image outline = UiFactory.Image("Outline", rect, theme.Rounded(9), theme.premiumBorder);
        UiFactory.Fill(outline.rectTransform);

        Image inner = UiFactory.Image("Inner", outline.rectTransform, theme.Rounded(8), theme.cardBottom);
        UiFactory.Fill(inner.rectTransform, 1.5f, 1.5f, 1.5f, 1.5f);

        TMP_Text label = UiFactory.LocalizedText("Label", inner.rectTransform, "settings_row.premium_badge", theme.semiBold, 9f,
            theme.gold, TextAlignmentOptions.Center);
        UiFactory.Fill(label.rectTransform);
    }
}
