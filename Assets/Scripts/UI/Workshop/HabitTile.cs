using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Atolye izgarasindaki tek kart.
//
// Tablo IKI yoldan cizilebiliyor:
//   * Bagli: PaintingView'in RenderTexture'i (gercek PaintPixels fade'i).
//   * Bagsiz: Image + PaintingCoverGrid (kamera ve RenderTexture yok).
// Havuzda dokuz goruntu var ve izgara daha uzun olabiliyor; ikinci yol sayesinde
// "tablosuz tile" diye bir durum olusmuyor, yalnizca animasyon olmuyor.
//
// Tile ILERLEMEYI SURMEZ: dokunma HabbitManager'a gider, hangi parcanin
// acilacagina o karar verir (bkz. CLAUDE.md > Invariants).
//
// Giris animasyonu (fade+scale) HabitCard'daki OnEnable kaliginin ayni: sadece
// nesne pasiften aktife gecince oynuyor, zaten gorunurken Bind() ile tazelenen
// bir tile'da tekrarlamiyor. TEK FARK: burada animasyon `transform` UZERINDE
// DEGIL, `visualRoot` adli ayri bir cocukta oynuyor. `PaintingViewBinder.IsVisible`
// tam olarak `Rect` (= bu transform) merkezini okuyup view baglama kararini
// veriyor; `Rect`in pivotu (0,1) (UiFactory.Rect varsayilani) oldugu icin onu
// dogrudan olceklemek o merkez noktasini kaydirip gorunurluk hesabini animasyon
// surerken bozabilirdi. `visualRoot` (`UiPrefabBuilder.BuildHabitTile`de
// `Fill()` ile pivot (0.5,0.5) kurulmus) ise saf gorsel bir katman -- Rect'in
// boyutunu/konumunu hic etkilemiyor, MoodCircle'daki "Scale" child kaliginin
// ayni gerekcesi (bkz. UI_ANIMATION_PLAN.md Faz 5(B)). Eski (yeniden
// derlenmemis) prefablarda bu alan bos kalabilir -- o yuzden her yerde null
// kontrolu var, yoksa animasyon sessizce atlanir, kod patlamaz.
[DisallowMultipleComponent]
public class HabitTile : MonoBehaviour
{
    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private Button button;
    [SerializeField] private Image frame;

    [Tooltip("Image + ortu izgarasini tasiyan kok. Goruntu baglandiginda kapaniyor.")]
    [SerializeField] private GameObject staticRoot;
    [SerializeField] private Image staticPainting;
    [SerializeField] private PaintingCoverGrid cover;
    [SerializeField] private RawImage rendered;
    [SerializeField] private GameObject doneBadge;

    [Tooltip("Gunluk tur sayaci pili (1/2). Hedef 1 ise kapali. Eski (yeniden derlenmemis) prefabda bos kalabilir.")]
    [SerializeField] private GameObject repsBadge;
    [SerializeField] private TMP_Text repsLabel;

    [Tooltip("PaintingBox'in son cocugu: secim beklerken uzerinde aydinlik/karanlik nefes alan ortu.")]
    [SerializeField] private Image choiceGlow;
    [SerializeField] private TMP_Text typeLabel;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text streak;

    [Tooltip("Seri cipindeki alev. Eski (yeniden derlenmemis) prefabda bos kalabilir.")]
    [SerializeField] private FlameSprite flame;

    [SerializeField] private UiTheme theme;

    [Tooltip("Giris animasyonu suresi (sn). 0 ya da visualRoot bos ise aninda belirir.")]
    [SerializeField] private float enterSeconds = 0.22f;

    private const float EnterStartScale = 0.94f;

    // Siradaki tablo secimini bekleyen tile'in ortasinda nefes alan
    // aydinlik/karanlik ortu -- kenarlik (frame) BILEREK sabit altin kaliyor,
    // dikkat cekme merkezde oluyor (bkz. Bind).
    private const float PulseSeconds = 1.6f;
    private static readonly Color GlowBright = new Color(1f, 1f, 1f, 0.32f);
    private static readonly Color GlowDark = new Color(0f, 0f, 0f, 0.38f);

    private CanvasGroup visualCanvasGroup;
    private Coroutine enterRoutine;
    private AttentionPulse glowPulse;

    private PaintingView view;

    public string HabitId { get; private set; }
    public RectTransform Rect => (RectTransform)transform;
    public RawImage RenderTarget => rendered;
    public PaintingView View => view;

    public event Action<string> Clicked;

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.AddListener(RaiseClicked);
        }

        EnsureVisualCanvasGroup();

        if (choiceGlow != null)
        {
            glowPulse = AttentionPulse.EnsureOn(choiceGlow);
        }
    }

    // Builder'in sifirdan kurdugu prefaba CanvasGroup elle eklemek bir sonraki
    // "Build Painter Habits UI" calistirmasinda silinirdi; ScreenNavigator.EnsureGroups
    // ile ayni kalip, builder'a dokunmadan calisir. visualRoot bos ise (eski,
    // yeniden derlenmemis bir prefab) sessizce atlaniyor.
    private void EnsureVisualCanvasGroup()
    {
        if (visualRoot == null)
        {
            return;
        }

        visualCanvasGroup = visualRoot.GetComponent<CanvasGroup>();
        if (visualCanvasGroup == null)
        {
            visualCanvasGroup = visualRoot.gameObject.AddComponent<CanvasGroup>();
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

        if (visualCanvasGroup == null)
        {
            return;
        }

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
        visualCanvasGroup.alpha = k;

        float scale = Mathf.Lerp(EnterStartScale, 1f, k);
        visualRoot.localScale = new Vector3(scale, scale, 1f);
    }

    // animated: bu tazeleme bir tik sonrasi mi (parca solarak acilsin) yoksa
    // yeniden baglama mi (anlik). awaitingChoice: bu tile su an siradaki
    // tabloyu secmeyi bekliyor mu (bkz. PaintingChoicePrompt).
    public void Bind(HabitData habit, PaintingLibrary library, bool animated, bool awaitingChoice)
    {
        HabitId = habit.id;

        title.text = habit.title;

        // Seri artik metin degil ALEV + sayi: cip sadece rakami gosteriyor,
        // "gun seri" kelimelerini alevin kendisi anlatiyor (teslim dosyasi).
        // Sayinin rengi basamaktan geliyor; sonuk (0) ve bekleyen (bugun
        // yapilmamis) hallerde sonmus gri -- alevin griligiyle ayni mesaj.
        int tier = StreakFlame.Tier(habit.currentStreak);
        bool pending = StreakFlame.IsPending(habit, AppClock.Now);

        streak.text = habit.currentStreak.ToString();
        streak.color = pending || habit.currentStreak == 0 ? theme.textMuted : theme.FlameNumber(tier);

        if (flame != null)
        {
            flame.SetTier(tier, pending, large: false);
        }

        typeLabel.text = UiTheme.TypeLabel(habit);
        typeLabel.color = theme.TypeColor(habit);

        doneBadge.SetActive(habit.isCompletedToday);

        // Gunluk tur sayaci: yalnizca hedef 1'den buyukse gorunuyor -- tek
        // tiklik aliskanliklarda ekranda yeni hicbir sey yok (bkz.
        // HabitReps.IsMulti). Rozet SOL ustte, tamamlandi rozeti SAG ustte:
        // ikisi ayni anda gorunebiliyor ve carpismiyorlar.
        bool multiReps = HabitReps.IsMulti(habit);

        if (repsBadge != null)
        {
            repsBadge.SetActive(multiReps);
        }

        if (multiReps && repsLabel != null)
        {
            repsLabel.text = HabitReps.Label(habit);
        }

        // Disteki serit beklerken de altin KALIR -- yalnizca sabit, nabiz
        // kenarlikta degil ortadaki ortude (asagida).
        frame.color = (habit.isCompletedToday || awaitingChoice) ? theme.gold : theme.borderFaint;

        // Nabiz beklemedeyken baslar, degilken Bind durdurur -- renk kanalinin
        // dinlenme degerini (seffaf) biz burada sifirliyoruz, AttentionPulse
        // genel bir bilesen oldugu icin "dinlenme rengi" kavramini bilmiyor.
        if (glowPulse != null)
        {
            if (awaitingChoice)
            {
                glowPulse.Play(GlowBright, GlowDark, PulseSeconds);
            }
            else
            {
                glowPulse.Stop();
                choiceGlow.color = Color.clear;
            }
        }

        // Yakilan parcalar tablonun KENDI piksellerinde geliyor (soldurulmus +
        // taramali); ortu o hucrede hicbir sey cizmesin diye ayni cevabi ona da
        // soyluyoruz (bkz. BurnedPaintingArt).
        bool burnedArt = false;

        if (PaintingAssignment.TryGetDefinition(library, habit, out PaintingDefinition definition))
        {
            Sprite sprite = BurnedPaintingCache.SpriteFor(definition, habit);
            burnedArt = sprite != null && sprite != definition.sprite;

            staticPainting.sprite = sprite;
            staticPainting.enabled = sprite != null;
        }
        else
        {
            staticPainting.enabled = false;
        }

        cover.Show(PaintingPieceState.For(library, habit), animated, burnedArt);
    }

    // Goruntu baglandi ya da birakildi. Null gelince statik yola donuyoruz.
    public void SetView(PaintingView value)
    {
        view = value;

        // Bagliyken statik KOK tamamen kapaniyor: PaintPixels tabloyu da perdeyi
        // de RenderTexture'a cizdigi icin uGUI ortusu ustune binerdi.
        bool live = value != null;
        rendered.gameObject.SetActive(live);
        staticRoot.SetActive(!live);
    }

    private void RaiseClicked()
    {
        Clicked?.Invoke(HabitId);
    }
}
