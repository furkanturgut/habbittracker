using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Ressamin ruh halini gosteren etiket + segment serisi.
//
// Segment sayisi PainterManager.MaxMoodPoint'ten TURETILIYOR, 12 diye sabit
// yazilmiyor: mood tablosu buyurse pill de kendiliginden buyuyor (tabloyla
// sinirlarin ayrisamamasi PainterManager'in tasarim kararlarindan biri).
[DisallowMultipleComponent]
public class MoodPill : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private Transform segmentRoot;
    [SerializeField] private Image segmentPrefab;
    [SerializeField] private UiTheme theme;

    private readonly System.Collections.Generic.List<Image> segments =
        new System.Collections.Generic.List<Image>(16);

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

    public void Refresh()
    {
        PainterManager painter = PainterManager.Instance;

        int point = painter != null ? painter.MoodPoint : PainterManager.DefaultMoodPoint;
        PainterManager.PainterMoods mood = painter != null
            ? painter.CurrentMood
            : PainterManager.PainterMoods.Normal;

        if (label != null)
        {
            label.text = MoodPresentation.Label(mood).ToUpperInvariant();
        }

        Build(PainterManager.MaxMoodPoint);

        for (int i = 0; i < segments.Count; i++)
        {
            segments[i].color = i < point ? theme.gold : theme.moodSegmentEmpty;
        }
    }

    private void HandleMoodChanged(int _)
    {
        Refresh();
    }

    private void Build(int count)
    {
        if (segmentRoot == null || segmentPrefab == null || segments.Count == count)
        {
            return;
        }

        while (segments.Count < count)
        {
            segments.Add(Instantiate(segmentPrefab, segmentRoot));
        }

        for (int i = 0; i < segments.Count; i++)
        {
            segments[i].gameObject.SetActive(i < count);
        }
    }
}
