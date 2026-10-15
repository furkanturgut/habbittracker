using System;
using System.Collections;
using UnityEngine;

// Ekranlar arasi gecis. SceneManager.LoadScene DEGIL: her iki ekran da ayni
// sahnede, cunku sahne degistirmek HabbitManager'i ve PaintingViewPool'u
// yeniden kurar, bekleyen (debounce'lu) yazmayi dusurur ve bagli tablolari
// bastan yuklerdi.
//
// Ayrica ressamin dokunma kapisinin TEK yazicisi burasi: hem ekran degisimi hem
// bottom sheet bu karari etkiliyor ve iki ayri yerden yazilsa biri digerini
// sessizce geri alirdi.
[DisallowMultipleComponent]
public class ScreenNavigator : MonoBehaviour
{
    [SerializeField] private GameObject[] screens;

    [Tooltip("Ressamin gorundugu ekranin indeksi; yalnizca orada dokunmaya tepki verir.")]
    [SerializeField] private int painterScreenIndex;

    [Tooltip("Bos birakilabilir: ressam sahnede yoksa dokunma kapisi atlanir.")]
    [SerializeField] private Painter painter;

    [SerializeField] private float crossFadeSeconds = 0f;

    private int current;
    private bool modalOpen;
    private CanvasGroup[] groups;
    private Coroutine transitionRoutine;

    public event Action<int> ScreenChanged;

    public int Current => current;

    private void Awake()
    {
        EnsureGroups();
    }

    // Panellere CanvasGroup burada, calisma aninda ekleniyor -- builder'in
    // sifirdan kurdugu sahne koklerine (Panel_Workshop, Panel_Habits) elle
    // bilesen eklemek bir sonraki "Build Painter Habits UI" calistirmasinda
    // silinirdi (bkz. UI_ANIMATION_PLAN.md). ButtonPressFeedback.EnsureOn ile
    // ayni kalip: builder'a hic dokunmadan hem builder'in kurdugu hem de
    // mevcut sahnede calisir.
    private void EnsureGroups()
    {
        if (screens == null)
        {
            return;
        }

        groups = new CanvasGroup[screens.Length];
        for (int i = 0; i < screens.Length; i++)
        {
            if (screens[i] == null)
            {
                continue;
            }

            CanvasGroup group = screens[i].GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = screens[i].AddComponent<CanvasGroup>();
            }

            groups[i] = group;
        }
    }

    private void Start()
    {
        // Awake'te degil: Painter kendini Start'ta PainterManager'a tanitiyor ve
        // acilis durumunu ondan sonra uygulamak sirayi belirsiz birakmiyor.
        // Acilis anlik: ilk ekran hicbir seyden solmuyor, dogrudan gorunur.
        if (screens == null || screens.Length == 0)
        {
            return;
        }

        current = current < 0 || current >= screens.Length ? 0 : current;
        ApplyInstant(current);
        ApplyPainterInput();
        ScreenChanged?.Invoke(current);
    }

    public void Show(int index)
    {
        if (screens == null || groups == null || index < 0 || index >= screens.Length)
        {
            return;
        }

        if (index == current && transitionRoutine == null)
        {
            return;
        }

        if (transitionRoutine != null)
        {
            // Yarim kalmis bir gecis varsa hedefine anlik atla, sonra yeni
            // gecisi o temiz durumdan baslat -- HabitSheet'teki "durdur ve
            // yeniden baslat" kalibiyla ayni.
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
            ApplyInstant(current);
        }

        int from = current;
        current = index;

        // ScreenChanged/painter kapisi gecisin BASINDA tetikleniyor, eski
        // aninda-kesme davranisiyla ayni zamanlamada -- su an baska abone
        // olmadigi icin risksiz (bkz. UI_ANIMATION_PLAN.md Faz 3 notu).
        ApplyPainterInput();
        ScreenChanged?.Invoke(current);

        if (from != index)
        {
            transitionRoutine = StartCoroutine(CrossFade(from, index));
        }
    }

    private void ApplyInstant(int index)
    {
        for (int i = 0; i < screens.Length; i++)
        {
            if (screens[i] == null)
            {
                continue;
            }

            bool show = i == index;
            screens[i].SetActive(show);

            CanvasGroup group = groups[i];
            if (group != null)
            {
                group.alpha = show ? 1f : 0f;
                group.blocksRaycasts = show;
                group.interactable = show;
            }
        }
    }

    private IEnumerator CrossFade(int from, int to)
    {
        CanvasGroup fromGroup = groups[from];
        CanvasGroup toGroup = groups[to];

        screens[to].SetActive(true);
        if (toGroup != null)
        {
            toGroup.alpha = 0f;
            toGroup.blocksRaycasts = true;
            toGroup.interactable = true;
        }

        // Soluk ekranin raycast'i hemen kapatiliyor: solma surerken uzerine
        // kazara dokunulursa altindaki eski ekrana degil hicbir seye gitmeli.
        if (fromGroup != null)
        {
            fromGroup.blocksRaycasts = false;
            fromGroup.interactable = false;
        }

        for (float t = 0f; t < crossFadeSeconds; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / crossFadeSeconds);
            float eased = Easing.OutCubic(k);

            if (fromGroup != null)
            {
                fromGroup.alpha = 1f - eased;
            }

            if (toGroup != null)
            {
                toGroup.alpha = eased;
            }

            yield return null;
        }

        if (fromGroup != null)
        {
            fromGroup.alpha = 0f;
        }

        if (toGroup != null)
        {
            toGroup.alpha = 1f;
        }

        if (screens[from] != null)
        {
            screens[from].SetActive(false);
        }

        transitionRoutine = null;
    }

    // Bottom sheet acilinca/kapaninca cagriliyor.
    public void SetModalOpen(bool open)
    {
        modalOpen = open;
        ApplyPainterInput();
    }

    private void ApplyPainterInput()
    {
        if (painter != null)
        {
            painter.SetClickEnabled(current == painterScreenIndex && !modalOpen);
        }
    }
}
