using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Galeri'nin 1. seviyesindeki tek bolum: baslik satiri + duvar. Bolumun
// TAMAMI bir dokunma hedefi -- tasarimin "the whole section is one tap
// target" kurali, HabitCategorySection'daki kart listesinden farkli olarak
// burada tek bir Button butun bolumu kapsiyor.
//
// Duvar ogeleri (GalleryWallItem) HabitCategorySection'daki kart havuzuyla
// AYNI kalipta havuzlanir: GameObject yaratip yok etmek yerine var olanlar
// yeniden baglanir, kuyruk SetActive(false) ile kapanir.
[DisallowMultipleComponent]
public class GalleryHabitCard : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text typeLabel;
    [SerializeField] private LayoutElement typePillElement;
    [SerializeField] private TMP_Text countLabel;
    [SerializeField] private LayoutElement countLabelElement;
    [SerializeField] private GalleryWallItem wallItemPrefab;
    [SerializeField] private Transform wallRoot;
    [SerializeField] private UiTheme theme;

    private readonly List<GalleryWallItem> items = new List<GalleryWallItem>(4);
    private readonly List<GalleryWork> works = new List<GalleryWork>(8);

    public string HabitId { get; private set; }

    // GalleryDetailView'in kart->tam-ekran buyumesinin BASLANGIC rect'i --
    // bkz. GalleryScreen.HandleCardClicked.
    public RectTransform Rect => (RectTransform)transform;

    public event Action<string> Clicked;

    // Duvardaki herhangi bir eserin rozetinden gelen onarim istegi -- hangi
    // GalleryWallItem oldugu burada onemsiz, GalleryScreen'e habitId+paintingId
    // olarak iletiliyor.
    public event Action<string, string> RepairRequested;

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.AddListener(RaiseClicked);
            ButtonPressFeedback.EnsureOn(button, 0.985f);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(RaiseClicked);
        }
    }

    public void Bind(HabitData habit, PaintingLibrary library)
    {
        HabitId = habit.id;

        title.text = habit.title;
        typeLabel.text = UiTheme.TypeLabel(habit);
        typeLabel.color = theme.TypeColor(habit);

        int count = GalleryWorks.FinishedCount(habit);
        countLabel.text = count > 0
            ? string.Format(Loc.Tr("gallery_card.works_count"), count)
            : Loc.Tr("gallery_detail.no_works_yet");

        FitToText(typeLabel, typePillElement, GalleryBands.TypePillPaddingX, GalleryBands.TypePillMinWidth);
        FitToText(countLabel, countLabelElement, 0f, 0f);

        GalleryWorks.Collect(library, habit, works);

        for (int i = 0; i < works.Count; i++)
        {
            GalleryWallItem item = ItemAt(i);
            item.gameObject.SetActive(true);
            item.Bind(habit, works[i]);
        }

        for (int i = works.Count; i < items.Count; i++)
        {
            items[i].gameObject.SetActive(false);
        }
    }

    // Bir etiketi metninin olcusune kelepceler: min == preferred yazildigi icin
    // satirin yer darliginda bu oge ARTIK KUCULMUYOR -- teslim dosyasindaki
    // "flex:none"un uGUI karsiligi. Daralmasi gereken tek oge baslik; kendi
    // Ellipsis'i zaten var.
    //
    // ForceMeshUpdate sart: TMP'nin preferredWidth'i olcum zorlanmadan BIR KARE
    // geriden gelir, yani ilk bind'da "BIRAK" pili hala "YAP" olcusunde cizilir.
    // GalleryWallItem.Bind'daki plaka ile ayni kalip.
    private static void FitToText(TMP_Text label, LayoutElement element, float padding, float minWidth)
    {
        if (label == null || element == null)
        {
            return;
        }

        label.ForceMeshUpdate();

        float width = Mathf.Max(minWidth, label.preferredWidth + padding);
        element.preferredWidth = width;
        element.minWidth = width;
    }

    private GalleryWallItem ItemAt(int index)
    {
        while (items.Count <= index)
        {
            GalleryWallItem item = Instantiate(wallItemPrefab, wallRoot);
            item.RepairRequested += HandleItemRepairRequested;
            items.Add(item);
        }

        return items[index];
    }

    private void RaiseClicked()
    {
        Clicked?.Invoke(HabitId);
    }

    private void HandleItemRepairRequested(string habitId, string paintingId)
    {
        RepairRequested?.Invoke(habitId, paintingId);
    }
}
