using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Ayarlar > "Kategoriler ve filtreler" sayfasindaki ozel kategori satiri: ad
// (yeniden adlandirmak icin duzenlenebilir TMP_InputField) + sil butonu.
//
// Kucuk ve tek-amacli: CategorySettingsSheet bu satirlardan havuzlanmis bir
// liste tutuyor (HabitCategorySection.CardAt kaligi), satirin kendisi hangi
// adin hangi kategoriye ait oldugunu bilmek disinda bir karar vermiyor.
[DisallowMultipleComponent]
public class CategoryCustomRow : MonoBehaviour
{
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Button deleteButton;

    private string category;

    // (eskiAd, yeniAd).
    public event Action<string, string> Renamed;
    public event Action<string> DeleteRequested;

    private void Awake()
    {
        if (nameInput != null)
        {
            nameInput.onEndEdit.AddListener(HandleEndEdit);
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.AddListener(HandleDelete);
            ButtonPressFeedback.EnsureOn(deleteButton);
        }
    }

    private void OnDestroy()
    {
        if (nameInput != null)
        {
            nameInput.onEndEdit.RemoveListener(HandleEndEdit);
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveListener(HandleDelete);
        }
    }

    public void Show(string categoryName)
    {
        category = categoryName;

        if (nameInput != null)
        {
            nameInput.SetTextWithoutNotify(categoryName);
        }
    }

    private void HandleEndEdit(string value)
    {
        if (string.Equals(value, category, StringComparison.Ordinal))
        {
            return;
        }

        Renamed?.Invoke(category, value);
    }

    private void HandleDelete()
    {
        DeleteRequested?.Invoke(category);
    }
}
