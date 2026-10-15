using UnityEngine;

// Tek bir tablonun tanimi. Bir tablonun NASIL acildigi (sirayla mi rastgele mi,
// fade mi anlik mi) tablonun kendi ozelligidir, aliskanligin degil: ayni
// aliskanlik sirayla farkli tablolar boyar ve her biri kendi ritmini getirir.
[CreateAssetMenu(fileName = "Painting", menuName = "HabitTracker/Painting Definition")]
public class PaintingDefinition : ScriptableObject
{
    [Tooltip("Kayitta saklanan kimlik. Bir kez verildikten sonra DEGISTIRILMEMELI: " +
             "kullanicilarin galerisi ve aktif siparisi bu id'ye bakiyor.")]
    public string id;

    public string displayName;

    public Sprite sprite;

    [Tooltip("Parca sayisi. HabitData.canvasSize ile eslesmeli (16/32/64).")]
    public PaintPixels.OverlayResolution resolution = PaintPixels.OverlayResolution.Size32;

    // Kayitta hangi alanin gecerli oldugunu da bu belirliyor: Sequential tablolar
    // HabitData.totalPixelsPainted, Random tablolar randomOpenedPixels uzerinden
    // geri yukleniyor. Var olan bir tanimda degistirilirse kayitli ilerleme
    // sessizce gecersizlesir.
    public PaintPixels.RevealOrder revealOrder = PaintPixels.RevealOrder.Sequential;
    public PaintPixels.RevealMode revealMode = PaintPixels.RevealMode.Fade;

    // Izgara sabit 8 sutunlu oldugu icin parca sayisi cozunurluge esit.
    public int PieceCount => (int)resolution;

    public bool IsSequential => revealOrder == PaintPixels.RevealOrder.Sequential;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Bos id kayitta "tablo bulunamadi" olarak doner; erken yakalanmasi iyi.
        if (string.IsNullOrEmpty(id))
        {
            id = name;
        }
    }
#endif
}
