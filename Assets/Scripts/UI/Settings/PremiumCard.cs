using System;
using UnityEngine;
using UnityEngine.UI;

// Ayarlar ekraninin Premium blogu: IKI durum tek bilesen. notPremiumRoot
// (kart + rozet + madde listesi + "Premium'a gec") ya da isPremiumRoot
// (ince serit + "Premium aktif") -- SetPremium(bool) hangisinin aktif
// oldugunu secer, ikisi de ayni GameObject altinda, builder ikisini de
// kurar.
[DisallowMultipleComponent]
public class PremiumCard : MonoBehaviour
{
    [SerializeField] private GameObject notPremiumRoot;
    [SerializeField] private Button purchaseButton;
    [SerializeField] private GameObject isPremiumRoot;

    public event Action Purchase;

    private void Awake()
    {
        if (purchaseButton != null)
        {
            purchaseButton.onClick.AddListener(HandlePurchase);
        }
    }

    private void OnDestroy()
    {
        if (purchaseButton != null)
        {
            purchaseButton.onClick.RemoveListener(HandlePurchase);
        }
    }

    private void HandlePurchase()
    {
        Purchase?.Invoke();
    }

    public void SetPremium(bool isPremium)
    {
        if (notPremiumRoot != null)
        {
            notPremiumRoot.SetActive(!isPremium);
        }

        if (isPremiumRoot != null)
        {
            isPremiumRoot.SetActive(isPremium);
        }
    }
}
