using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Ilerlemeyi sifirla" gibi yikici eylemler icin onay kutusu: scrim + alttan
// acilan kucuk kutu -- projenin kendi deseni (HabitSheet'in scrim/kayma
// kalibinin KUCUK olcekli, BAGIMSIZ bir kopyasi; HabitSheet'in kodunu
// PAYLASMIYOR, aynen PaintingChoiceSheet'in de paylasmadigi gibi -- gorsel
// desen benziyor ama eksen ve sahiplik farkli).
//
// Show(title, body, confirmLabel, Action) -- SettingsScreen'in cocugu,
// YENIDEN KULLANILABILIR olmasi bilincli: ileride "Sil" onaylari da buradan
// gecebilir.
[DisallowMultipleComponent]
public class ConfirmSheet : MonoBehaviour
{
    [SerializeField] private CanvasGroup scrimGroup;
    [SerializeField] private Button scrimButton;
    [SerializeField] private RectTransform panel;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text bodyLabel;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text confirmLabel;

    [SerializeField] private float slideSeconds = 0.22f;

    private float panelHeight;
    private Action onConfirm;
    private Coroutine routine;

    private void Awake()
    {
        if (panel != null)
        {
            panelHeight = panel.rect.height;
        }

        if (scrimButton != null)
        {
            scrimButton.onClick.AddListener(Close);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(Close);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(HandleConfirm);
        }
    }

    private void OnDestroy()
    {
        if (scrimButton != null)
        {
            scrimButton.onClick.RemoveListener(Close);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(Close);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(HandleConfirm);
        }

        StopRoutine();
    }

    public void Show(string title, string body, string confirmText, Action confirmAction)
    {
        if (titleLabel != null)
        {
            titleLabel.text = title;
        }

        if (bodyLabel != null)
        {
            bodyLabel.text = body;
        }

        if (confirmLabel != null)
        {
            confirmLabel.text = confirmText;
        }

        onConfirm = confirmAction;

        gameObject.SetActive(true);
        StopRoutine();
        routine = StartCoroutine(Slide());
    }

    public void Close()
    {
        if (!gameObject.activeSelf)
        {
            return;
        }

        StopRoutine();
        routine = StartCoroutine(CloseSlide());
    }

    // Once kapat, SONRA cagir: onConfirm bir ust ekrani kapatirsa (HabbitManager.
    // DeleteAllData sonrasi sahne tazelemesi gibi) bu nesnenin kendi kapanisi
    // zaten tamamlanmis olmali.
    private void HandleConfirm()
    {
        Action action = onConfirm;
        Close();
        action?.Invoke();
    }

    private void StopRoutine()
    {
        if (routine == null)
        {
            return;
        }

        StopCoroutine(routine);
        routine = null;
    }

    private IEnumerator Slide()
    {
        if (panel != null)
        {
            panel.anchoredPosition = new Vector2(0f, -panelHeight);
        }

        if (scrimGroup != null)
        {
            scrimGroup.alpha = 0f;
            scrimGroup.blocksRaycasts = true;
            scrimGroup.interactable = true;
        }

        float elapsed = 0f;

        while (elapsed < slideSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float eased = Easing.OutCubic(Mathf.Clamp01(elapsed / slideSeconds));

            if (scrimGroup != null)
            {
                scrimGroup.alpha = eased;
            }

            if (panel != null)
            {
                panel.anchoredPosition = new Vector2(0f, Mathf.Lerp(-panelHeight, 0f, eased));
            }

            yield return null;
        }

        if (scrimGroup != null)
        {
            scrimGroup.alpha = 1f;
        }

        if (panel != null)
        {
            panel.anchoredPosition = Vector2.zero;
        }

        routine = null;
    }

    private IEnumerator CloseSlide()
    {
        float startAlpha = scrimGroup != null ? scrimGroup.alpha : 1f;
        float startY = panel != null ? panel.anchoredPosition.y : 0f;
        float elapsed = 0f;

        while (elapsed < slideSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float eased = Easing.OutCubic(Mathf.Clamp01(elapsed / slideSeconds));

            if (scrimGroup != null)
            {
                scrimGroup.alpha = Mathf.Lerp(startAlpha, 0f, eased);
            }

            if (panel != null)
            {
                panel.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, -panelHeight, eased));
            }

            yield return null;
        }

        if (scrimGroup != null)
        {
            scrimGroup.alpha = 0f;
            scrimGroup.blocksRaycasts = false;
            scrimGroup.interactable = false;
        }

        gameObject.SetActive(false);
        routine = null;
    }
}
