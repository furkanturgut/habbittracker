using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Duvardaki tek yuva: GalleryWorkFrame + plaka. Yuvanin genisligi SABIT
// DEGIL -- GalleryBands.SlotWidth'in max(84, native*2, plaka) kuralina gore
// Bind'da hesaplanip hem rect'e hem LayoutElement'e yazilir. FlowLayoutGroup
// bu genisligi OKUR, kendisi genislik UYGULAMAZ (bkz. FlowLayoutGroup.Solve).
[DisallowMultipleComponent]
public class GalleryWallItem : MonoBehaviour
{
    [SerializeField] private RectTransform rect;
    [SerializeField] private LayoutElement layoutElement;
    [SerializeField] private GalleryWorkFrame frame;
    [SerializeField] private RectTransform plaqueRect;
    [SerializeField] private Image plaqueBorder;
    [SerializeField] private Image plaqueBackground;
    [SerializeField] private TMP_Text plaqueLabel;
    [SerializeField] private UiTheme theme;

    // Plaka padding'i (2/8), iki yan.
    private const float PlaquePaddingX = 16f;

    // Son Bind'daki kimlik: frame.RepairRequested habit/paintingId bilmiyor
    // (bkz. GalleryWorkFrame basindaki not), burada cachelenip yukari tasiniyor.
    private string lastHabitId;
    private string lastPaintingId;

    // GalleryHabitCard'in kendi RepairRequested'ine toplayip GalleryScreen'e
    // ilettigi olay.
    public event Action<string, string> RepairRequested;

    private void Awake()
    {
        if (frame != null)
        {
            frame.RepairRequested += HandleFrameRepairRequested;
        }
    }

    private void OnDestroy()
    {
        if (frame != null)
        {
            frame.RepairRequested -= HandleFrameRepairRequested;
        }
    }

    public void Bind(HabitData habit, in GalleryWork work)
    {
        lastHabitId = habit.id;
        lastPaintingId = work.Definition.id;

        frame.Bind(habit, work, GalleryBands.PaintingScale);

        if (work.IsFinished)
        {
            plaqueLabel.text = work.Definition.displayName;
            plaqueLabel.color = theme.textOnGold;
            plaqueBackground.color = theme.easelCover;
            plaqueBorder.color = theme.easelCover;
        }
        else
        {
            plaqueLabel.text = string.Format(Loc.Tr("gallery_tile.in_progress_sub"), work.OpenedCount, work.Definition.PieceCount);
            plaqueLabel.color = theme.gold;
            plaqueBackground.color = theme.galleryPlaqueBackground;
            plaqueBorder.color = theme.galleryPlaqueBorder;
        }

        // Plaka genisligi metne gore degisiyor; TMP'nin olcumunu once
        // zorlamadan preferredWidth bir kare geriden gelirdi.
        plaqueLabel.ForceMeshUpdate();

        Vector2 native = work.Definition.sprite != null ? work.Definition.sprite.rect.size : Vector2.zero;
        float plaqueWidth = plaqueLabel.preferredWidth + PlaquePaddingX;
        float width = GalleryBands.SlotWidth(native.x * GalleryBands.PaintingScale, plaqueWidth);

        rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        layoutElement.preferredWidth = width;
        layoutElement.minWidth = width;

        // Plaka kendi genisligine (metin + dolgu) gore ortalaniyor -- yuvanin
        // tam genisligine GERMIYORUZ, teslim dosyasinda "white-space: nowrap"
        // bir rozet, sutuna yapismiyor.
        plaqueRect.sizeDelta = new Vector2(plaqueWidth, plaqueRect.sizeDelta.y);
    }

    private void HandleFrameRepairRequested()
    {
        RepairRequested?.Invoke(lastHabitId, lastPaintingId);
    }
}
