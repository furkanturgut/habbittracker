using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Eserler" izgarasindaki tek kare: GalleryWorkFrame + ad + alt satir.
// GalleryWallItem'in Seviye 2 karsiligi -- plaka yerine iki ayri metin, cunku
// bu ekranda zaten tek bir aliskanlik gosteriliyor ve plakanin tasidigi "hangi
// aliskanlik" bilgisine gerek yok.
//
// Index, GalleryHabitCard'daki HabitId ile AYNI kalip: kapanan closure degil,
// her Bind'da yeniden yazilan bir alan -- Button dinleyicisi Awake'te BIR KEZ
// takilip tikta hep GUNCEL Index'i okuyor.
[DisallowMultipleComponent]
public class GalleryWorkTile : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private GalleryWorkFrame frame;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text subLabel;
    [SerializeField] private UiTheme theme;

    // Son Bind'daki kimlik: frame.RepairRequested habit/paintingId bilmiyor
    // (bkz. GalleryWorkFrame basindaki not), burada cachelenip yukari tasiniyor.
    private string lastHabitId;
    private string lastPaintingId;

    public int Index { get; private set; }

    public event Action<int> Clicked;

    // GalleryDetailView'in kendi RepairRequested'ine toplayip GalleryScreen'e
    // ilettigi olay.
    public event Action<string, string> RepairRequested;

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.AddListener(RaiseClicked);
            ButtonPressFeedback.EnsureOn(button, 0.97f);
        }

        if (frame != null)
        {
            frame.RepairRequested += HandleFrameRepairRequested;
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(RaiseClicked);
        }

        if (frame != null)
        {
            frame.RepairRequested -= HandleFrameRepairRequested;
        }
    }

    public void Bind(HabitData habit, in GalleryWork work, int index)
    {
        Index = index;
        lastHabitId = habit.id;
        lastPaintingId = work.Definition.id;

        frame.Bind(habit, work, GalleryBands.PaintingScale);

        nameLabel.text = work.Definition.displayName;

        if (work.IsFinished)
        {
            subLabel.text = string.Format(Loc.Tr("gallery_tile.finished_sub_pieces"), work.Definition.PieceCount)
                + (work.Holes.Count > 0 ? string.Format(Loc.Tr("gallery_tile.finished_sub_burned_suffix"), work.Holes.Count) : string.Empty);
            subLabel.color = theme.textMuted;
        }
        else
        {
            subLabel.text = string.Format(Loc.Tr("gallery_tile.in_progress_sub"), work.OpenedCount, work.Definition.PieceCount);
            subLabel.color = theme.gold;
        }
    }

    private void RaiseClicked()
    {
        Clicked?.Invoke(Index);
    }

    private void HandleFrameRepairRequested()
    {
        RepairRequested?.Invoke(lastHabitId, lastPaintingId);
    }
}
