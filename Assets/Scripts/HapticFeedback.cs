using UnityEngine;

// Titresim icin TEK giris noktasi. Hangi olayin nerede titresim tetikleyecegine
// henuz karar verilmedi -- bu dosya o karar bekleyen cagri yerlerinin takacagi
// priz. Bugun her HapticCue ayni tek darbeye (Handheld.Vibrate) dusuyor; yarin
// platforma ozgu desenler (Android VibrationEffect genlik/suresi, iOS
// UIFeedbackGenerator) eklenirse tek degisen yer PlayNow'un icidir -- cagri
// yerleri (HapticFeedback.Play(HapticCue.Success) gibi) DEGISMEZ.
public enum HapticCue
{
    Light,
    Medium,
    Heavy,
    Success,
    Warning,
}

public static class HapticFeedback
{
    public static void Play(HapticCue cue = HapticCue.Medium)
    {
        if (!AppSettings.HapticsOn)
        {
            return;
        }

        if (Application.isEditor)
        {
            Debug.Log("HapticFeedback.Play: Editor'de gercek titresim yok. cue=" + cue);
            return;
        }

        PlayNow(cue);
    }

    // Ayri tutuldu: AppSettings/Editor denetiminden sonraki tek sorumlulugu
    // "bu cue'yu cihazda nasil calar" -- platforma ozgu desenler buraya eklenir.
    private static void PlayNow(HapticCue cue)
    {
        Handheld.Vibrate();
    }
}
