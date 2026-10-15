using UnityEngine;
using UnityEngine.UI;

// Bir butonu ScreenNavigator'in bir ekranina baglayan kopru.
//
// Neden ayri bir bilesen: builder bu baglantiyi
// button.onClick.AddListener(lambda) ile kuruyordu ve o dinleyici SAHNEYE
// KAYDEDILMIYOR -- UnityEvent yalnizca m_PersistentCalls'i serilestiriyor,
// AddListener ise calisma-ani dinleyicisi ekliyor. Sahne kaydedilince iki nav
// butonu da sessizce olu kaliyordu: Atolye'den Aliskanliklar'a gecis Play
// modunda hic calismadi, panel ve HabitsScreen sahnede dogru bagliyken.
//
// Bilesen olunca referanslar [SerializeField] ve UiWiring (SerializedObject)
// ile yaziliyor, yani sahne dosyasinda duruyor.
[DisallowMultipleComponent]
public class ScreenNavButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private ScreenNavigator navigator;

    [Tooltip("ScreenNavigator.screens dizisindeki hedef indeks.")]
    [SerializeField] private int targetIndex;

    // HabitCard / HabitTile ile ayni kalip: Awake'te bagla, OnDestroy'da birak.
    // OnEnable/OnDisable degil -- panel kapaninca buton da pasiflesiyor ve her
    // acilista yeniden abone olmak bir sey kazandirmiyor.
    //
    // Geri butonu Panel_Habits icinde ve o panel pasif basliyor: Awake'i ilk
    // Show(1) aninda calisiyor, yani butona basilabilir hale gelmeden once.
    private void Awake()
    {
        if (button != null)
        {
            button.onClick.AddListener(Navigate);
            ButtonPressFeedback.EnsureOn(button);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(Navigate);
        }
    }

    private void Navigate()
    {
        if (navigator != null)
        {
            navigator.Show(targetIndex);
        }
    }
}
