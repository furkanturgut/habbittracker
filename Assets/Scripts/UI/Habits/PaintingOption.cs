using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tablo izgarasindaki tek secenek: ya gercek bir tablonun kucuk resmi, ya da
// "Rastgele" karesi.
//
// Rastgele kare icin ikinci bir gorsel aramak yerine PaintingCoverGrid'in
// kapali durumunu kullaniyoruz: "hangi tablo olduğu belli degil" fikrini
// ikinci bir izgara matematigi yazmadan anlatiyor (bkz. CanvasSizeOption).
[DisallowMultipleComponent]
public class PaintingOption : MonoBehaviour
{
    [SerializeField] private OptionButton option;
    [SerializeField] private Image thumbnail;
    [SerializeField] private PaintingCoverGrid cover;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private UiTheme theme;

    // Bos = Rastgele.
    public string PaintingId { get; private set; } = string.Empty;

    public OptionButton Option => option;

    public void ShowPainting(PaintingDefinition definition)
    {
        PaintingId = definition != null ? definition.id : string.Empty;

        thumbnail.sprite = definition != null ? definition.sprite : null;
        thumbnail.enabled = thumbnail.sprite != null;
        cover.gameObject.SetActive(false);

        nameLabel.text = definition != null ? definition.displayName : string.Empty;
    }

    public void ShowRandom(int pieceCount)
    {
        PaintingId = string.Empty;

        thumbnail.enabled = false;
        cover.gameObject.SetActive(true);
        cover.ShowAllClosed(pieceCount);

        nameLabel.text = Loc.Tr("painting.random_label");
    }

    // nameLabel rengi kasitli olarak hep aninda degisiyor -- yalnizca kartin
    // kendi butonu (option) PaintingPickerRow'un tek surucu coroutine'i
    // tarafindan animasyonlu suruluyor (bkz. CanvasSizeOption.SetSelected).
    public void SetSelected(bool selected, bool instant)
    {
        option.SetSelected(selected, instant);
        nameLabel.color = selected ? theme.gold : theme.textPrimary;
    }
}
