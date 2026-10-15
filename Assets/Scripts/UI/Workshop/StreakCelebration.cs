using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Yeni bir alev basamagina ulasildiginda acilan tam ekran kutlama.
//
// Panel_Workshop'un SON cocugu: alt navigasyon cubugunu ve tablo secim
// kutusunu da ortuyor, ama Ayarlar UI Root seviyesinde oldugu icin onun
// ALTINDA kaliyor (bkz. CLAUDE.md > panel sahipligi).
//
// HabbitManager'a KENDISI abone DEGIL: ekran 01'de yoneticiyi dinleyen tek
// yer WorkshopScreen (bkz. CLAUDE.md -- MoodRow'un PainterManager'a abone
// olma gerekcesiyle ayni). WorkshopScreen olayi alip Enqueue cagiriyor.
//
// Kaydirma/solma kodu PaintingChoiceSheet ile PAYLASILMIYOR: bu bir alt sayfa
// degil, tam ekran bir overlay ve hareketi tamamen farkli (bkz. o dosyanin
// "ayni kalip, ayri uygulama" notu).
[DisallowMultipleComponent]
public class StreakCelebration : MonoBehaviour
{
    // Tikten SONRA bu kadar beklenip aciliyor: ressamin tik tepkisi ve
    // parcanin acilmasi once oynasin, kutlama onlarin ustune binmesin.
    private const float OpenDelaySeconds = 0.7f;

    private const float FadeInSeconds = 0.25f;
    private const float FadeOutSeconds = 0.2f;

    private const float FlameSeconds = 0.7f;
    private const float TextSeconds = 0.4f;
    private const float TextDelaySeconds = 0.35f;
    private const float ButtonDelaySeconds = 0.5f;

    private const float RaysFadeSeconds = 0.6f;
    private const float RaysTurnSeconds = 14f;

    // Alevin "pop" egrisi: 0.2'den baslar, %55'te 1.18'e firlar, %75'te
    // 0.94'e duser, 1'de oturur. Teslim dosyasinin flamePop keyframe'leri.
    private const float FlamePeakAt = 0.55f;
    private const float FlameDipAt = 0.75f;
    private const float FlameStartScale = 0.2f;
    private const float FlamePeakScale = 1.18f;
    private const float FlameDipScale = 0.94f;

    private const float TextRiseDistance = 14f;

    [SerializeField] private CanvasGroup group;

    [Tooltip("Acikken ressamin dokunmasi kapatiliyor.")]
    [SerializeField] private ScreenNavigator navigator;

    [Header("Zemin")]
    [SerializeField] private Image glow;
    [SerializeField] private Image rays;

    [Header("Icerik")]
    [SerializeField] private RectTransform flameScale;
    [SerializeField] private CanvasGroup flameGroup;
    [SerializeField] private FlameSprite flame;
    [SerializeField] private RectTransform textBlock;
    [SerializeField] private CanvasGroup textGroup;
    [SerializeField] private RectTransform buttonBlock;
    [SerializeField] private CanvasGroup buttonGroup;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text lineLabel;
    [SerializeField] private Button continueButton;

    [SerializeField] private UiTheme theme;

    // Bekleyen kutlamalar. Iki aliskanlik ayni tikle (dev "hepsini tikle")
    // basamak atlarsa sirayla gosteriliyor -- ust uste acmak ikisini de
    // okunamaz yapardi.
    //
    // Queue<T> degil iki paralel liste DEGIL, kucuk bir struct listesi:
    // kuyruk neredeyse her zaman bos ya da tek elemanli, ve struct listesi
    // kutulama yapmiyor (projenin tahsis kurali).
    private readonly List<Pending> queue = new List<Pending>(2);

    private Coroutine routine;
    private bool wired;
    private bool open;

    private struct Pending
    {
        public int tier;
        public int streak;
    }

    // Aliskanligin KENDISI saklanmiyor, yalnizca o andaki iki sayisi: kutlama
    // 0.7 s gecikmeyle aciliyor ve o arada aliskanlik degisebilir (hatta
    // silinebilir). Gosterilen sey "tikin oldugu andaki durum" olmali.
    public void Enqueue(HabitData habit, int tier)
    {
        if (habit == null)
        {
            return;
        }

        if (group == null)
        {
            // Sahne yeniden kurulmamis: overlay'in parcalari bagli degil.
            // Sessiz kalmak "kutlama neden acilmiyor" sorusunu cevapsiz
            // birakirdi (WorkshopScreen'in secim kutusu uyarisiyla ayni kalip).
            Debug.LogWarning("StreakCelebration: overlay bagli degil; " +
                "HabitTracker > Build Painter Habits UI calistirin.", this);
            return;
        }

        EnsureWired();

        queue.Add(new Pending { tier = tier, streak = habit.currentStreak });

        if (routine == null)
        {
            // Coroutine AKTIF olmayan bir nesnede baslatilamaz, bu yuzden
            // once aciliyor -- ama gorunmez ve dokunma gecirgen olarak:
            // acilis 0.7 s gecikmeli ve o sure boyunca overlay ekrandaki
            // hicbir seyi ne ortmeli ne de yutmali.
            gameObject.SetActive(true);
            PrepareHidden();

            routine = StartCoroutine(RunQueue());
        }
    }

    private void PrepareHidden()
    {
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    private void EnsureWired()
    {
        if (wired)
        {
            return;
        }

        wired = true;

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(Close);
            ButtonPressFeedback.EnsureOn(continueButton);
        }
    }

    public void Close()
    {
        if (!open)
        {
            return;
        }

        open = false;
    }

    private void OnDisable()
    {
        // Panel kapandi: kuyrugu ve kilidi birakiyoruz. Kutlama "kacirilmis"
        // oluyor ama bu dogru davranis -- kullanici baska bir ekrana gectiyse
        // oraya tam ekran bir overlay dusurmek daha kotu olurdu.
        queue.Clear();
        routine = null;
        open = false;

        if (navigator != null)
        {
            navigator.SetModalOpen(false);
        }
    }

    private IEnumerator RunQueue()
    {
        // Gecikme kuyrugun DISINDA, bir kez: pes pese iki kutlama varsa
        // ikincisi birincinin hemen ardindan gelmeli, bir 0.7 s daha
        // beklememeli.
        yield return WaitUnscaled(OpenDelaySeconds);

        while (queue.Count > 0)
        {
            Pending next = queue[0];
            queue.RemoveAt(0);

            yield return Show(next);
        }

        routine = null;

        // Kapanis ve kilidin birakilmasi KUYRUGUN SONUNDA, tek kutlamanin
        // sonunda degil: ikisi arka arkaya geldiyse arada bir kare boyunca
        // ekran acilip kapanmamali.
        if (navigator != null)
        {
            navigator.SetModalOpen(false);
        }

        gameObject.SetActive(false);
    }

    private IEnumerator Show(Pending pending)
    {
        Bind(pending);

        open = true;
        group.blocksRaycasts = true;
        group.interactable = true;

        if (navigator != null)
        {
            navigator.SetModalOpen(true);
        }

        float elapsed = 0f;
        float raysAngle = 0f;

        // Tek coroutine butun ogeleri suruyor (MoodRow'un "tek surucu, cok
        // gorsel" kalibi): bes ayri coroutine ayni kareleri bes kez
        // uyandirirdi ve kapanista bes ayri durdurma gerekirdi.
        while (open)
        {
            float delta = Time.unscaledDeltaTime;
            elapsed += delta;

            group.alpha = Mathf.Clamp01(elapsed / FadeInSeconds);

            ApplyFlame(Mathf.Clamp01(elapsed / FlameSeconds));
            ApplyRise(textBlock, textGroup, elapsed - TextDelaySeconds);
            ApplyRise(buttonBlock, buttonGroup, elapsed - ButtonDelaySeconds);

            if (rays != null)
            {
                raysAngle -= delta / RaysTurnSeconds * 360f;
                rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, raysAngle);

                Color color = rays.color;
                color.a = RaysAlpha * Mathf.Clamp01(elapsed / RaysFadeSeconds);
                rays.color = color;
            }

            yield return null;
        }

        yield return FadeOut();
    }

    private IEnumerator FadeOut()
    {
        float startAlpha = group.alpha;

        for (float t = 0f; t < FadeOutSeconds; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(startAlpha, 0f, Easing.OutCubic(Mathf.Clamp01(t / FadeOutSeconds)));
            yield return null;
        }

        PrepareHidden();
    }

    // Isin halkasinin tepe opakligi: teslim dosyasinda renk "{num}33",
    // yani %20 alfa.
    private const float RaysAlpha = 0.2f;

    private void Bind(Pending pending)
    {
        int tier = pending.tier;

        if (flame != null)
        {
            flame.SetTier(tier, pending: false, large: true);
        }

        if (glow != null)
        {
            glow.color = theme.FlameGlow(tier);
        }

        if (rays != null)
        {
            Color color = theme.FlameNumber(tier);
            color.a = 0f;
            rays.color = color;
        }

        if (titleLabel != null)
        {
            titleLabel.text = string.Format(Loc.Tr("streak_celebration.title"), pending.streak);
        }

        if (lineLabel != null)
        {
            lineLabel.text = tier == StreakFlame.TierCount - 1
                ? Loc.Tr("streak_celebration.max_tier_line")
                : string.Format(Loc.Tr("streak_celebration.next_tier_line"), StreakFlame.NextThreshold(tier));
        }

        // Acilis degerleri: ilk karede bir an dolu gorunmesinler.
        group.alpha = 0f;
        ApplyFlame(0f);
        ApplyRise(textBlock, textGroup, -TextDelaySeconds);
        ApplyRise(buttonBlock, buttonGroup, -ButtonDelaySeconds);
    }

    // Uc bacakli "pop": her bacak kendi icinde OutCubic. Teslim dosyasinin
    // cubic-bezier(.2,.8,.2,1) egrisi her keyframe arasinda ayri ayri
    // calisiyor, tek bir egri butun yola yayilmiyor.
    private void ApplyFlame(float k)
    {
        if (flameScale == null)
        {
            return;
        }

        float scale;

        if (k < FlamePeakAt)
        {
            scale = Mathf.Lerp(FlameStartScale, FlamePeakScale, Easing.OutCubic(k / FlamePeakAt));
        }
        else if (k < FlameDipAt)
        {
            scale = Mathf.Lerp(FlamePeakScale, FlameDipScale,
                Easing.OutCubic((k - FlamePeakAt) / (FlameDipAt - FlamePeakAt)));
        }
        else
        {
            scale = Mathf.Lerp(FlameDipScale, 1f,
                Easing.OutCubic((k - FlameDipAt) / (1f - FlameDipAt)));
        }

        flameScale.localScale = new Vector3(scale, scale, 1f);

        if (flameGroup != null)
        {
            // Opaklik tepe noktasinda tamamlaniyor, sonuna kadar surmuyor.
            flameGroup.alpha = Mathf.Clamp01(k / FlamePeakAt);
        }
    }

    // Asagidan yukari suzulen blok. elapsed NEGATIF gelebilir (henuz sirasi
    // gelmedi) -- Clamp01 onu 0'a cekiyor, yani blok baslangic konumunda
    // ve gorunmez bekliyor.
    private static void ApplyRise(RectTransform block, CanvasGroup blockGroup, float elapsed)
    {
        if (block == null)
        {
            return;
        }

        float k = Easing.OutCubic(Mathf.Clamp01(elapsed / TextSeconds));

        Vector2 position = block.anchoredPosition;
        position.y = Mathf.Lerp(-TextRiseDistance, 0f, k);
        block.anchoredPosition = position;

        if (blockGroup != null)
        {
            blockGroup.alpha = k;
        }
    }

    private static IEnumerator WaitUnscaled(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            yield return null;
        }
    }
}
