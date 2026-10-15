using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Alt navigasyon cubugunun sag ucundaki canli mod rozeti: ikon + etiket.
//
// PainterManager abonesi, HabbitManager DEGIL -- MoodRow/MoodPill'deki ayni
// gerekce: WorkshopScreen 01'in tek HabbitManager abonesi kalmali, mod
// puani zaten sahneler arasi PainterManager'da yasiyor.
//
// Dokunma su an HICBIR SEY YAPMIYOR (teslim dosyasi: "display-only") --
// bu yuzden Button/ScreenNavButton yok, sadece gorsel.
[DisallowMultipleComponent]
public class NavMoodBadge : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;
    [SerializeField] private UiTheme theme;

    // Etiket rect'i MoodColumnWidth'ten (62) GENIS ve ortali: "COK ZENGIN"
    // 10pt'de 62'ye sigmiyor, teslim dosyasi white-space:nowrap diyor --
    // kirpmak yerine simetrik tasiyoruz (TextOverflowModes.Overflow burada
    // DEGIL, builder'in label'a verdigi ayarda; bkz. BottomNavBuilder).

    private bool subscribed;

    private void OnEnable()
    {
        TrySubscribe();
        Refresh();
    }

    // OnEnable'da PainterManager.Instance henuz kurulmamis olabilir: Unity
    // nesneler arasi Awake sirasini garanti etmiyor. Start'ta kesin hazir.
    private void Start()
    {
        TrySubscribe();
        Refresh();
    }

    private void OnDisable()
    {
        if (!subscribed)
        {
            return;
        }

        PainterManager painter = PainterManager.Instance;
        if (painter != null)
        {
            painter.MoodChanged -= HandleMoodChanged;
        }

        subscribed = false;
    }

    public void Refresh()
    {
        PainterManager painter = PainterManager.Instance;

        PainterManager.PainterMoods mood = painter != null
            ? painter.CurrentMood
            : PainterManager.PainterMoods.Normal;

        if (icon != null)
        {
            Sprite sprite = theme != null ? theme.IconFor(mood) : null;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        if (label != null)
        {
            label.text = MoodPresentation.Label(mood).ToUpperInvariant();
        }
    }

    private void HandleMoodChanged(int _)
    {
        Refresh();
    }

    private void TrySubscribe()
    {
        if (subscribed)
        {
            return;
        }

        PainterManager painter = PainterManager.Instance;
        if (painter == null)
        {
            return;
        }

        painter.MoodChanged += HandleMoodChanged;
        subscribed = true;
    }
}
