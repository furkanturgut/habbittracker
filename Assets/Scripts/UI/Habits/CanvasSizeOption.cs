using TMPro;
using UnityEngine;

// Formdaki tek bir tuval secenegi: mini izgara onizlemesi + "{n} parça" + ipucu.
//
// Onizleme, kartlarla AYNI PaintingCoverGrid bilesenini kullaniyor: 16/32/64'un
// gercekte nasil bolundugu (8 sutun, degisen satir) tek bir yerde tanimli
// kalsin, onizleme icin ikinci bir izgara matematigi yazilmasin.
[DisallowMultipleComponent]
public class CanvasSizeOption : MonoBehaviour
{
    [SerializeField] private OptionButton option;
    [SerializeField] private PaintingCoverGrid preview;
    [SerializeField] private TMP_Text countLabel;
    [SerializeField] private TMP_Text hintLabel;
    [SerializeField] private UiTheme theme;

    public int Size { get; private set; }

    public OptionButton Option => option;

    public void Setup(int size)
    {
        Size = size;
        countLabel.text = string.Format(Loc.Tr("canvas_size.piece_count_label"), size);
        hintLabel.text = CanvasSizes.Hint(size);
        preview.ShowAllClosed(size);
    }

    // countLabel/preview rengi kasitli olarak hep aninda degisiyor -- yalnizca
    // kartin kendi butonu (option) HabitSheet'in tek surucu coroutine'i
    // tarafindan animasyonlu suruluyor.
    public void SetSelected(bool selected, bool instant)
    {
        option.SetSelected(selected, instant);
        countLabel.color = selected ? theme.gold : theme.textPrimary;
        preview.SetClosedColor(selected ? theme.gold : theme.textFaint);
    }
}
