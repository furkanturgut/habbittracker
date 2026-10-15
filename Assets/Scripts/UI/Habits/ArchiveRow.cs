using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Arsiv sayfasindaki tek satir: baslik + seri + "Geri al" butonu.
//
// CategoryCustomRow'un kalibi -- ArchiveSheet bu satirlardan havuzlanmis bir
// liste tutuyor (HabitCategorySection.CardAt kaligi), satirin kendisi hangi
// aliskanligin arsivde oldugunu bilmek disinda bir karar vermiyor.
[DisallowMultipleComponent]
public class ArchiveRow : MonoBehaviour
{
    [SerializeField] private Button hitButton;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text streakLabel;
    [SerializeField] private Button unarchiveButton;

    private string habitId;

    public event Action<string> Clicked;
    public event Action<string> UnarchiveRequested;

    private void Awake()
    {
        if (hitButton != null)
        {
            hitButton.onClick.AddListener(HandleClicked);
        }

        if (unarchiveButton != null)
        {
            unarchiveButton.onClick.AddListener(HandleUnarchive);
            ButtonPressFeedback.EnsureOn(unarchiveButton);
        }
    }

    private void OnDestroy()
    {
        if (hitButton != null)
        {
            hitButton.onClick.RemoveListener(HandleClicked);
        }

        if (unarchiveButton != null)
        {
            unarchiveButton.onClick.RemoveListener(HandleUnarchive);
        }
    }

    public void Show(HabitData habit)
    {
        habitId = habit.id;

        if (titleLabel != null)
        {
            titleLabel.text = habit.title;
        }

        if (streakLabel != null)
        {
            streakLabel.text = string.Format(Loc.Tr("archive_sheet.row_streak"), habit.currentStreak);
        }
    }

    private void HandleClicked()
    {
        Clicked?.Invoke(habitId);
    }

    private void HandleUnarchive()
    {
        UnarchiveRequested?.Invoke(habitId);
    }
}
