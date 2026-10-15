using UnityEngine;

// Hangi olayda hangi ses calacagini soyleyen sabit kume. HapticCue ile ayni
// kalip: cagri yeri cue'yu seciyor, hangi AudioClip'e karsilik geldigine
// SfxLibrary bakiyor.
public enum SfxCue
{
    DayCompleted,
    RotusApplied,
    RotusEarned,
    NewPainting,
    Slap,
    SlapReaction,
}

// Ses efektleri icin TEK giris noktasi -- HapticFeedback.cs ile BIREBIR AYNI
// kalip (cue -> kaynak cozme -> AppSettings kontrolu -> calma). Kutuphane
// AppSettings/RotusState ile ayni "static ayna" deseniyle BIR KEZ takilir
// (bkz. HabbitManager.Awake), boylece Painter gibi HabbitManager'i hic
// gormeyen cagri yerleri de kendi referansini tasimadan SfxPlayer.Play
// diyebilir.
public static class SfxPlayer
{
    private static SfxLibrary library;
    private static AudioSource source;

    public static void Apply(SfxLibrary value)
    {
        library = value;
    }

    public static void Play(SfxCue cue)
    {
        if (!AppSettings.SoundOn || library == null)
        {
            return;
        }

        AudioClip clip = Resolve(cue);
        if (clip == null)
        {
            return;
        }

        EnsureSource();
        source.PlayOneShot(clip);
    }

    private static AudioClip Resolve(SfxCue cue)
    {
        switch (cue)
        {
            case SfxCue.DayCompleted: return library.dayCompleted;
            case SfxCue.RotusApplied: return library.rotusApplied;
            case SfxCue.RotusEarned: return library.rotusEarned;
            case SfxCue.NewPainting: return library.newPainting;
            case SfxCue.Slap: return library.slap;
            case SfxCue.SlapReaction: return library.slapReaction;
            default: return null;
        }
    }

    // Sahneler arasi yasayan tek AudioSource -- PainterManager'in
    // DontDestroyOnLoad singleton deseniyle ayni gerekce, ama bunun icin ayrica
    // sahneye elle bir nesne eklemeye gerek kalmasin diye ilk cagrida kendi
    // GameObject'ini kendisi kurar. spatialBlend=0: bu 2D bir UI/geri bildirim
    // sesi, dunya konumuna gore sonmemeli.
    private static void EnsureSource()
    {
        if (source != null)
        {
            return;
        }

        GameObject host = new GameObject("SfxPlayer");
        Object.DontDestroyOnLoad(host);

        source = host.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
    }
}
