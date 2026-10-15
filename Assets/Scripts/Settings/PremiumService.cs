using UnityEngine;

// Magaza/IAP entegrasyonu TESLIM DOSYASININ KAPSAMI DISINDA -- hook.
// isPremium kayda yaziliyor ama URETIMDE magaza makbuzundan TURETILMELI
// (teslim dosyasinin kendi notu); su an gercek bir magaza akisi olmadigi
// icin dogrudan isaretliyor.
public static class PremiumService
{
    public static void Purchase(HabbitManager manager)
    {
        Debug.Log("PremiumService.Purchase: magaza akisi henuz yok (kapsam disi), isPremium dogrudan isaretlendi.");

        if (manager != null)
        {
            manager.SetPremium(true);
        }
    }
}
