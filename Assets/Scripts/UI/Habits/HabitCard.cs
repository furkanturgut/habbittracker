using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Alisknliklar listesindeki tek kart.
//
// Kucuk resim HAFIF yolla ciziliyor: Image + PaintingCoverGrid, kamera ve
// RenderTexture yok. Liste uzunlugu sinirsiz ama PaintingView havuzu dokuz
// elemanli; her karta bir goruntu ayirmak havuzu aninda tuketirdi.
//
// Giris animasyonu (fade + kucuk olcek) yalnizca OnEnable'da oynuyor: kart
// zaten gorunurken Bind() ile veri yenilenmesi (tik atma, gun donusumu)
// SetActive(true)'i tekrar cagirsa da nesne zaten aktifse OnEnable yeniden
// tetiklenmez -- yani animasyon sadece havuzdan YENI acilan/yeniden gorunur
// olan kart icin oynar, listedeki butun kartlar her tazelemede yanip
// sonmez (bkz. UI_ANIMATION_PLAN.md Faz 5). `cardRoot`ta VerticalLayoutGroup
// oldugundan (`UiPrefabBuilder.BuildCategorySection`) kaydirma yerine
// localScale+alpha kullanildi -- ButtonPressFeedback'teki ayni gerekce:
// localScale LayoutGroup'u tetiklemiyor, anchoredPosition/sizeDelta tetikliyor.
[DisallowMultipleComponent]
public class HabitCard : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image painting;
    [SerializeField] private PaintingCoverGrid cover;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text typeLabel;
    [SerializeField] private DayChipRow days;
    [SerializeField] private RectTransform progressFill;
    [SerializeField] private TMP_Text streakLabel;
    [SerializeField] private UiTheme theme;

    [Tooltip("Giris animasyonu suresi (sn). 0 ise aninda belirir.")]
    [SerializeField] private float enterSeconds = 0.22f;

    private const float EnterStartScale = 0.94f;

    private CanvasGroup canvasGroup;
    private Coroutine enterRoutine;

    public string HabitId { get; private set; }

    public event Action<string> Clicked;

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.AddListener(RaiseClicked);
            ButtonPressFeedback.EnsureOn(button);
        }

        EnsureCanvasGroup();
    }

    // Builder'in sifirdan kurdugu CategorySection prefabina CanvasGroup elle
    // eklemek bir sonraki "Build Painter Habits UI" calistirmasinda silinirdi;
    // ScreenNavigator.EnsureGroups ile ayni kalip, builder'a dokunmadan calisir.
    private void EnsureCanvasGroup()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void OnEnable()
    {
        PlayEnterAnimation();
    }

    private void OnDisable()
    {
        StopEnterAnimation();
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(RaiseClicked);
        }
    }

    private void PlayEnterAnimation()
    {
        StopEnterAnimation();

        if (enterSeconds <= 0f)
        {
            ApplyEnterVisual(1f);
            return;
        }

        ApplyEnterVisual(0f);
        enterRoutine = StartCoroutine(EnterAnimation());
    }

    private void StopEnterAnimation()
    {
        if (enterRoutine != null)
        {
            StopCoroutine(enterRoutine);
            enterRoutine = null;
        }
    }

    private IEnumerator EnterAnimation()
    {
        float elapsed = 0f;

        while (elapsed < enterSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            ApplyEnterVisual(Easing.OutCubic(Mathf.Clamp01(elapsed / enterSeconds)));
            yield return null;
        }

        ApplyEnterVisual(1f);
        enterRoutine = null;
    }

    private void ApplyEnterVisual(float k)
    {
        canvasGroup.alpha = k;

        float scale = Mathf.Lerp(EnterStartScale, 1f, k);
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    public void Bind(HabitData habit, PaintingLibrary library)
    {
        HabitId = habit.id;

        title.text = habit.title;
        typeLabel.text = UiTheme.TypeLabel(habit);
        typeLabel.color = theme.TypeColor(habit);

        days.ShowFrequency(habit.frequency);

        // Yakilan parcalar tablonun KENDI piksellerinde geliyor; ortu o hucrede
        // hicbir sey cizmesin diye ayni cevabi ona da soyluyoruz
        // (bkz. BurnedPaintingArt).
        bool burnedArt = false;

        if (PaintingAssignment.TryGetDefinition(library, habit, out PaintingDefinition definition))
        {
            Sprite sprite = BurnedPaintingCache.SpriteFor(definition, habit);
            burnedArt = sprite != null && sprite != definition.sprite;

            painting.sprite = sprite;
            painting.enabled = sprite != null;
        }
        else
        {
            painting.enabled = false;
        }

        PaintingPieceState pieceState = PaintingPieceState.For(library, habit);
        cover.Show(pieceState, false, burnedArt);

        // Delikli bir tablo bilerek %100'e ulasmaz: dolgu SADECE acik parcayi
        // sayar, yakilani degil.
        float ratio = habit.canvasSize > 0 ? (float)pieceState.OpenedCount / habit.canvasSize : 0f;

        // Dolgu anchor ile buyuyor: kartin genisligi cihazdan cihaza degisiyor
        // ve piksel hesabi yapmak gerekmesin.
        Vector2 max = progressFill.anchorMax;
        max.x = Mathf.Clamp01(ratio);
        progressFill.anchorMax = max;

        streakLabel.text = string.Format(Loc.Tr("habit_card.streak_label"), habit.currentStreak, habit.longestStreak);
    }

    private void RaiseClicked()
    {
        Clicked?.Invoke(HabitId);
    }
}
