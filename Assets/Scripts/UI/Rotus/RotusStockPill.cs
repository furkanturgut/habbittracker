using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Galeri basligindaki rotus stok sayaci. RotusState'i okur, HabbitManager
// referansi YOK -- NavMoodBadge'in PainterManager'a abone olmasiyla AYNI
// gerekce: GalleryScreen ekranin tek HabbitManager abonesi olarak kalmali
// (bkz. CLAUDE.md).
//
// Genislik ICERIGE GORE hesaplanir (RefreshLayout) -- GalleryHabitCard.
// FitToText'in ayni fikri, ama bu pill bir layout grubunun cocugu DEGIL
// (RightSpacer'in yerini alan sabit-ankrajli bir rozet), bu yuzden
// LayoutElement yerine dogrudan rect.sizeDelta yaziliyor. pivot (1,1)
// oldugu icin genisleme SOLA dogru olur, sag kenar sabit kalir.
[DisallowMultipleComponent]
public class RotusStockPill : MonoBehaviour
{
    private const float Height = 38f;
    private const float PaddingLeft = 2f;
    private const float PaddingRight = 12f;
    private const float Spacing = 1f;
    private const float IconSize = 32f;
    private const float FrameSeconds = 0.4f;

    [SerializeField] private RectTransform rect;
    [SerializeField] private RectTransform iconRect;
    [SerializeField] private SpriteFrameLoop iconLoop;
    [SerializeField] private TMP_Text countLabel;
    [SerializeField] private UiTheme theme;

    private void OnEnable()
    {
        RotusState.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        RotusState.Changed -= Refresh;
    }

    // Awake sirasi garantisi yok (PainterManager/AppSettings'in ayni notu) --
    // Start'ta da bir kez daha okunuyor.
    private void Start()
    {
        Refresh();
    }

    private void Refresh()
    {
        int count = RotusState.Count;
        bool hasStock = count > 0;

        if (theme != null && iconLoop != null)
        {
            iconLoop.Show(theme.RotusFrames(hasStock), FrameSeconds);
        }

        if (countLabel != null)
        {
            countLabel.text = count.ToString();

            if (theme != null)
            {
                Color color = theme.textPrimary;
                color.a = hasStock ? 1f : 0.5f;
                countLabel.color = color;
            }
        }

        RefreshLayout();
    }

    private void RefreshLayout()
    {
        if (rect == null || iconRect == null || countLabel == null)
        {
            return;
        }

        // ForceMeshUpdate sart: preferredWidth olcum zorlanmadan bir kare
        // geriden gelir (FitToText'teki ayni not) -- ilk Bind'da "0" olcusu
        // kullanilirdi.
        countLabel.ForceMeshUpdate();
        float labelWidth = countLabel.preferredWidth;

        float width = PaddingLeft + IconSize + Spacing + labelWidth + PaddingRight;
        rect.sizeDelta = new Vector2(width, Height);

        iconRect.anchoredPosition = new Vector2(PaddingLeft, 0f);

        RectTransform labelRect = countLabel.rectTransform;
        labelRect.anchoredPosition = new Vector2(PaddingLeft + IconSize + Spacing, 0f);
        labelRect.sizeDelta = new Vector2(labelWidth, Height);
    }
}
