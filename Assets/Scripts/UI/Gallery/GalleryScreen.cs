using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 03 Galeri ekraninin 1. seviyesi: HabbitManager'a abone olan TEK yer,
// WorkshopScreen'in ayni abonelik kalibini tasiyor (bkz. CLAUDE.md > Screens).
//
// Aliskanlik basina bolumler HAVUZLANIR -- HabitsScreen.Refresh'teki
// "index'e gore kart baglan, kuyruk kapat" kaliginin aynisi.
[DisallowMultipleComponent]
public class GalleryScreen : MonoBehaviour
{
    [SerializeField] private HabbitManager manager;
    [SerializeField] private TMP_Text totalLabel;
    [SerializeField] private GalleryHabitCard cardPrefab;
    [SerializeField] private Transform content;
    [SerializeField] private GalleryDetailView detailView;
    [SerializeField] private GalleryPaintingViewer viewer;
    [SerializeField] private RotusRestoreSheet restoreSheet;

    private readonly List<GalleryHabitCard> cards = new List<GalleryHabitCard>(8);

    // Ilk tazeleme Start'ta: HabbitManager kaydi kendi Awake'inde yukluyor ve
    // Unity nesneler arasi Awake sirasini garanti etmiyor (bkz. WorkshopScreen).
    private bool started;

    // Disaridan abone olunabilir birak -- ama asil tuketicisi artik BURASI:
    // HandleCardClicked, tiklanan kartin RectTransform'unu detailView.Open'a
    // verebilmek icin zaten cards listesine erismek zorunda.
    public event Action<string> HabitSelected;

    // detailView/viewer sabit cocuklar (havuzlanmis degil), yani dinleyici
    // bagi butun bilesenin omru boyunca ayni -- GalleryWorkTile.Awake'teki
    // "bir kez tak" kaliginin ayni, OnEnable/OnDisable degil.
    private void Awake()
    {
        if (detailView != null)
        {
            detailView.WorkSelected += HandleWorkSelected;
            detailView.Closing += HandleDetailClosing;
            detailView.RepairRequested += HandleRepairRequested;
        }

        if (viewer != null)
        {
            viewer.RepairRequested += HandleRepairRequested;
        }
    }

    private void OnDestroy()
    {
        if (detailView != null)
        {
            detailView.WorkSelected -= HandleWorkSelected;
            detailView.Closing -= HandleDetailClosing;
            detailView.RepairRequested -= HandleRepairRequested;
        }

        if (viewer != null)
        {
            viewer.RepairRequested -= HandleRepairRequested;
        }
    }

    private void Start()
    {
        started = true;
        Refresh();
    }

    private void OnEnable()
    {
        if (manager == null)
        {
            return;
        }

        manager.HabitChanged += HandleHabitChanged;
        manager.HabitListChanged += HandleHabitListChanged;
        manager.PaintingFinished += HandlePaintingFinished;
        manager.DayRolledOver += HandleDayRolledOver;

        // Panel kapaliyken gelen degisiklikler olay olarak ulasmadi; ama ilk
        // acilista Start'i bekliyoruz (yukaridaki nota bakin).
        if (started)
        {
            Refresh();
        }
    }

    private void OnDisable()
    {
        if (manager == null)
        {
            return;
        }

        manager.HabitChanged -= HandleHabitChanged;
        manager.HabitListChanged -= HandleHabitListChanged;
        manager.PaintingFinished -= HandlePaintingFinished;
        manager.DayRolledOver -= HandleDayRolledOver;
    }

    public void Refresh()
    {
        if (manager == null || cardPrefab == null || content == null)
        {
            return;
        }

        IReadOnlyList<HabitData> habits = manager.Habits;
        int total = 0;

        for (int i = 0; i < habits.Count; i++)
        {
            HabitData habit = habits[i];
            GalleryHabitCard card = CardAt(i);
            card.gameObject.SetActive(true);
            card.Bind(habit, manager.Library);

            total += GalleryWorks.FinishedCount(habit);
        }

        for (int i = habits.Count; i < cards.Count; i++)
        {
            cards[i].gameObject.SetActive(false);
        }

        if (totalLabel != null)
        {
            totalLabel.text = total + " eser";
        }
    }

    private GalleryHabitCard CardAt(int index)
    {
        while (cards.Count <= index)
        {
            GalleryHabitCard created = Instantiate(cardPrefab, content);
            created.Clicked += HandleCardClicked;
            created.RepairRequested += HandleRepairRequested;
            cards.Add(created);
        }

        return cards[index];
    }

    // Duvardaki bir rozetten, "ESERLER" izgarasindaki bir karodan ya da
    // Seviye 3'un cipinden gelebilir -- hangisinden geldigi onarim sayfasi
    // icin onemsiz, habitId+paintingId yeterli (bkz. RotusRestoreSheet.Open).
    private void HandleRepairRequested(string habitId, string paintingId)
    {
        if (restoreSheet == null)
        {
            return;
        }

        HabitData habit = FindHabit(habitId);
        if (habit != null)
        {
            restoreSheet.Open(habit, paintingId);
        }
    }

    private void HandleCardClicked(string habitId)
    {
        HabitSelected?.Invoke(habitId);

        if (detailView == null || manager == null)
        {
            return;
        }

        HabitData habit = FindHabit(habitId);
        GalleryHabitCard card = FindCard(habitId);

        if (habit != null && card != null)
        {
            detailView.Open(habit, manager.Library, card.Rect);
        }
    }

    // GalleryDetailView.CurrentHabit zaten Bind'da cozulmus -- Seviye 3'un
    // hangi aliskanligi acacagini bulmak icin ikinci bir arama gerekmiyor.
    private void HandleWorkSelected(int index)
    {
        if (viewer == null || manager == null || detailView.CurrentHabit == null)
        {
            return;
        }

        viewer.Open(detailView.CurrentHabit, manager.Library, index);
    }

    private void HandleDetailClosing()
    {
        if (viewer != null)
        {
            viewer.CloseImmediate();
        }
    }

    private HabitData FindHabit(string habitId)
    {
        IReadOnlyList<HabitData> habits = manager.Habits;

        for (int i = 0; i < habits.Count; i++)
        {
            if (string.Equals(habits[i].id, habitId, StringComparison.Ordinal))
            {
                return habits[i];
            }
        }

        return null;
    }

    private GalleryHabitCard FindCard(string habitId)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (string.Equals(cards[i].HabitId, habitId, StringComparison.Ordinal))
            {
                return cards[i];
            }
        }

        return null;
    }

    // Yalnizca duvar kartlarini (Seviye 1) degil, bu habit su an ACIK olan
    // Seviye 2/3'u de yerinde yeniliyoruz -- yoksa orn. rotus onarimi sonrasi
    // (bkz. HabbitManager.UseRotus'un sonunda HabitChanged) acik ekranda hala
    // yanik tablo gorunur, galeriye cikip tekrar girene kadar.
    private void HandleHabitChanged(HabitData habit)
    {
        Refresh();

        if (detailView != null && detailView.CurrentHabit != null
            && string.Equals(detailView.CurrentHabit.id, habit.id, StringComparison.Ordinal))
        {
            detailView.Refresh(manager.Library);
        }

        if (viewer != null)
        {
            viewer.Refresh(manager.Library, habit.id);
        }
    }

    private void HandleHabitListChanged()
    {
        Refresh();
    }

    private void HandleDayRolledOver()
    {
        Refresh();
    }

    private void HandlePaintingFinished(HabitData habit, string finishedPaintingId)
    {
        Refresh();
    }
}
