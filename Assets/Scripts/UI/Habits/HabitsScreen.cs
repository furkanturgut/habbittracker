using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 02 Alisknliklar ekrani: kategoriye gore gruplu liste + "Yeni alışkanlık".
//
// Gruplama HabitCategories.All sirasini izliyor; kayitta o listede OLMAYAN bir
// kategori varsa (eski kayit, elle duzenleme) kendi basligiyla sona ekleniyor.
// Kayittaki degeri asla yeniden yazmiyoruz -- kullanicinin verisi bizim
// secenek listemize uymak zorunda degil.
[DisallowMultipleComponent]
public class HabitsScreen : MonoBehaviour
{
    [SerializeField] private HabbitManager manager;
    [SerializeField] private TMP_Text activeCountLabel;
    [SerializeField] private HabitCategorySection sectionPrefab;
    [SerializeField] private Transform content;
    [SerializeField] private HabitSheet sheet;
    [SerializeField] private Button addButton;

    // Listenin SONUNDAKI katlanir "Arşiv (N)" satiri. content, SectionAt
    // tarafindan Instantiate ile buyuyen bir VerticalLayoutGroup -- bu satir
    // her Refresh'te SetAsLastSibling ile sona itilir, yoksa yeni acilan bir
    // kategori bolumu onun altinda kalirdi.
    [SerializeField] private GameObject archiveEntryRoot;
    [SerializeField] private Button archiveEntryButton;
    [SerializeField] private TMP_Text archiveCountLabel;
    [SerializeField] private ArchiveSheet archiveSheet;

    // CTA cubugu artik ekranin dibinde degil, alt navigasyon cubugunun
    // USTUNDE (bkz. NAVIGATION_SETTINGS_PLAN.md Faz 2). Cubugun yuksekligi
    // cihaza gore degistigi icin (guvenli alan payi) burasi BottomNavBar.
    // HeightChanged'i dinleyip BottomBar'in alt ofsetini tazeler -- TEK
    // YAZICI kurali: bu rect'i baska hicbir yer yazmaz.
    [SerializeField] private RectTransform bottomBar;
    [SerializeField] private BottomNavBar navBar;

    // HabitsPanelBuilder.BuildAddButton ile paylasilan TEK kaynak: builder
    // bar'i bu yukseklikte kuruyor, burasi aninda ayni yukseklikle yeniden
    // konumluyor.
    public const float BottomBarHeight = 126f;

    private readonly List<HabitCategorySection> sections = new List<HabitCategorySection>(8);

    // Gruplama icin yeniden kullanilan tamponlar: her tazelemede yeni liste
    // ayirmamak icin alan olarak duruyorlar.
    private readonly List<string> categoryOrder = new List<string>(8);
    private readonly Dictionary<string, List<HabitData>> grouped =
        new Dictionary<string, List<HabitData>>(8, StringComparer.Ordinal);
    private readonly Stack<List<HabitData>> bucketPool = new Stack<List<HabitData>>();

    // WorkshopScreen ile ayni gerekce: ilk tazeleme Start'tan once yapilmamali,
    // HabbitManager kaydi Awake'te yukluyor ve sira garanti degil.
    private bool started;

    private void Start()
    {
        started = true;
        Refresh();
    }

    private void OnEnable()
    {
        if (manager != null)
        {
            manager.HabitListChanged += Refresh;
            manager.HabitChanged += HandleHabitChanged;
        }

        if (addButton != null)
        {
            addButton.onClick.AddListener(OpenNew);
        }

        if (archiveEntryButton != null)
        {
            archiveEntryButton.onClick.AddListener(OpenArchive);
        }

        if (archiveSheet != null)
        {
            archiveSheet.EditRequested += HandleCardClicked;
        }

        if (sheet != null)
        {
            sheet.Submitted += Refresh;
        }

        if (navBar != null)
        {
            navBar.HeightChanged += HandleNavHeightChanged;
        }

        // Panel kapaliyken gelen degisiklikler olay olarak ulasmadi: acilista
        // her zaman tam tazeleme. Cubuk yuksekligi de ayni gerekceyle hemen
        // okunuyor -- olaya degil, ONA DA guvenmiyoruz (MoodRow/BottomNavBar'in
        // PainterManager/ScreenNavigator icin yaptigi ayni kalip).
        HandleNavHeightChanged();

        if (started)
        {
            Refresh();
        }
    }

    private void OnDisable()
    {
        if (manager != null)
        {
            manager.HabitListChanged -= Refresh;
            manager.HabitChanged -= HandleHabitChanged;
        }

        if (addButton != null)
        {
            addButton.onClick.RemoveListener(OpenNew);
        }

        if (archiveEntryButton != null)
        {
            archiveEntryButton.onClick.RemoveListener(OpenArchive);
        }

        if (archiveSheet != null)
        {
            archiveSheet.EditRequested -= HandleCardClicked;
        }

        if (sheet != null)
        {
            sheet.Submitted -= Refresh;
        }

        if (navBar != null)
        {
            navBar.HeightChanged -= HandleNavHeightChanged;
        }
    }

    private void HandleNavHeightChanged()
    {
        if (bottomBar == null || navBar == null)
        {
            return;
        }

        UiRect.StretchBottom(bottomBar, navBar.CurrentHeight, BottomBarHeight, 0f, 0f);
    }

    public void Refresh()
    {
        if (manager == null || sectionPrefab == null || content == null)
        {
            return;
        }

        IReadOnlyList<HabitData> habits = manager.Habits;
        int archivedCount = manager.ArchivedCount;
        activeCountLabel.text = string.Format(Loc.Tr("habits.active_count_label"), habits.Count - archivedCount);

        Group(habits);

        for (int i = 0; i < categoryOrder.Count; i++)
        {
            string category = categoryOrder[i];

            HabitCategorySection section = SectionAt(i);
            section.gameObject.SetActive(true);
            section.Show(category, grouped[category], manager.Library);
        }

        for (int i = categoryOrder.Count; i < sections.Count; i++)
        {
            sections[i].gameObject.SetActive(false);
        }

        ReleaseBuckets();

        // Arsiv satiri HER ZAMAN sonda: SectionAt yeni bolumleri content'in
        // sonuna Instantiate ediyor, yani buyuyen bir liste bu satiri ortada
        // bitakirdi. SetAsLastSibling idempotent ve zaten sondaysa bedava.
        if (archiveEntryRoot != null)
        {
            archiveEntryRoot.SetActive(archivedCount > 0);

            if (archivedCount > 0)
            {
                if (archiveCountLabel != null)
                {
                    archiveCountLabel.text = string.Format(Loc.Tr("habits.archive_count_label"), archivedCount);
                }

                archiveEntryRoot.transform.SetAsLastSibling();
            }
        }
    }

    private void HandleHabitChanged(HabitData habit)
    {
        Refresh();
    }

    private void OpenNew()
    {
        if (sheet != null)
        {
            sheet.OpenForNew();
        }
    }

    private void HandleCardClicked(string habitId)
    {
        if (sheet != null && manager != null && manager.TryGetHabit(habitId, out HabitData habit))
        {
            sheet.OpenForEdit(habit);
        }
    }

    private void OpenArchive()
    {
        if (archiveSheet != null)
        {
            archiveSheet.Open();
        }
    }

    // -----------------------------------------------------------------------
    // Gruplama
    // -----------------------------------------------------------------------

    private void Group(IReadOnlyList<HabitData> habits)
    {
        categoryOrder.Clear();
        grouped.Clear();

        // Once kanonik sira, sonra kayitta bulunan yabanci kategoriler.
        // Arsivdekiler hic bir bolume girmez -- kendi katlanir satirlarinda.
        for (int i = 0; i < habits.Count; i++)
        {
            if (habits[i].isArchived)
            {
                continue;
            }

            string category = Resolve(habits[i]);

            if (!grouped.TryGetValue(category, out List<HabitData> bucket))
            {
                bucket = bucketPool.Count > 0 ? bucketPool.Pop() : new List<HabitData>(8);
                bucket.Clear();
                grouped.Add(category, bucket);
            }

            bucket.Add(habits[i]);
        }

        for (int i = 0; i < HabitCategories.All.Length; i++)
        {
            if (grouped.ContainsKey(HabitCategories.All[i]))
            {
                categoryOrder.Add(HabitCategories.All[i]);
            }
        }

        foreach (KeyValuePair<string, List<HabitData>> pair in grouped)
        {
            if (HabitCategories.IndexOf(pair.Key) < 0)
            {
                categoryOrder.Add(pair.Key);
            }
        }
    }

    private static string Resolve(HabitData habit)
    {
        return string.IsNullOrEmpty(habit.categoryGroup) ? HabitCategories.Default : habit.categoryGroup;
    }

    private void ReleaseBuckets()
    {
        foreach (KeyValuePair<string, List<HabitData>> pair in grouped)
        {
            pair.Value.Clear();
            bucketPool.Push(pair.Value);
        }

        grouped.Clear();
    }

    private HabitCategorySection SectionAt(int index)
    {
        while (sections.Count <= index)
        {
            HabitCategorySection created = Instantiate(sectionPrefab, content);
            created.CardClicked += HandleCardClicked;
            sections.Add(created);
        }

        return sections[index];
    }
}
