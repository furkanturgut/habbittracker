using UnityEngine;
using UnityEngine.UI;

// Uc panelin kose rozetini Ayarlar paneline baglayan kopru.
//
// ScreenNavButton'in BIREBIR kalibi: Ayarlar bir ScreenNavigator sekmesi
// OLMADIGI icin (UI Root'un ucunden SONRAKI cocugu, bkz. SettingsScreen)
// ayri bir bilesen -- navigator.Show(index) yerine settingsScreen.Open()
// cagiriyor. Awake'te bagla, OnDestroy'da birak (ScreenNavButton ile ayni
// gerekce: panel pasif baslasa da Awake ilk Show'dan once calisir).
[DisallowMultipleComponent]
public class SettingsButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private SettingsScreen settingsScreen;

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.AddListener(Open);
            ButtonPressFeedback.EnsureOn(button);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(Open);
        }
    }

    private void Open()
    {
        if (settingsScreen != null)
        {
            settingsScreen.Open();
        }
    }
}
