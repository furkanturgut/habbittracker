using TMPro;
using UnityEngine;

// Bir TMP_Text'i bir ceviri anahtarina baglayan yaprak bilesen.
// NavMoodBadge/RotusStockPill ile AYNI "dogrudan statik aynaya abone ol"
// deseni: OnEnable'da AppSettings.Changed'e abone olup Refresh, OnDisable'da
// cik. Merkezi bir "tum ekranlari yenile" orkestrasyonu YOK -- her etiket
// kendi kendini yonetiyor, ekranlarin coklugu (3 panel + sheet'ler) bu
// sayede sorun olmuyor.
[DisallowMultipleComponent]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private string key;

    // Bazi baslik/eyebrow metinleri BUYUK HARFLE gosteriliyor (SettingsRowBuilder.
    // Section / HabitsPanelBuilder.Section'daki eski ToUpperInvariant() kalibi).
    // O donusum Editor-build-time'da TEK SEFERLIK yapiliyordu; Loc.Tr her dil
    // degisiminde YENIDEN cagrildigi icin donusumun burada, her Refresh'te
    // tekrarlanmasi gerekiyor -- yoksa dil degisince baslik kucuk harfe doner.
    [SerializeField] private bool uppercase;

    private void OnEnable()
    {
        AppSettings.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        AppSettings.Changed -= Refresh;
    }

    // Awake sirasi garantisi yok (PainterManager/AppSettings'in ayni notu) --
    // Start'ta da bir kez daha okunuyor.
    private void Start()
    {
        Refresh();
    }

    // UiFactory.LocalizedText'in Editor-build-time baglama cagrisi.
    public void Bind(TMP_Text target, string translationKey, bool upper = false)
    {
        label = target;
        key = translationKey;
        uppercase = upper;
        Refresh();
    }

    // Runtime'da bir etiketin anahtarini degistirmek icin (ornegin ayni
    // dugmenin iki durumunu tek bilesenle gostermek).
    public void SetKey(string translationKey)
    {
        key = translationKey;
        Refresh();
    }

    private void Refresh()
    {
        if (label == null || string.IsNullOrEmpty(key))
        {
            return;
        }

        string value = Loc.Tr(key);
        label.text = uppercase ? value.ToUpperInvariant() : value;
    }
}
