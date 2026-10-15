using UnityEngine;

// Butun ses efektlerinin TEK kutuphanesi -- UiTheme'in "her gorsel/renk tek
// asset'te" deseniyle ayni, ama tema degil ses icin. SfxLibraryBuilder
// (Editor) Assets/Sounds altindaki 6 sabit dosyayi buraya otomatik baglar;
// alanlar da elle surukleyip birakilabilir.
[CreateAssetMenu(fileName = "SfxLibrary", menuName = "Habit Tracker/Sfx Library")]
public class SfxLibrary : ScriptableObject
{
    public AudioClip dayCompleted;
    public AudioClip rotusApplied;
    public AudioClip rotusEarned;
    public AudioClip newPainting;
    public AudioClip slap;
    public AudioClip slapReaction;
}
