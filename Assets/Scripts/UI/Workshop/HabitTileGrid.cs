using System;
using System.Collections.Generic;
using UnityEngine;

// Aktif filtreye uyan aliskanliklar icin tile listesini kurar.
//
// Tile'lar HAVUZLANIYOR: filtre degistiginde ya da liste tazelendiginde
// GameObject yaratilip yok edilmiyor, var olanlar yeniden kullaniliyor. Kart
// sayisi tik atildikca degismiyor, yalnizca filtre ve ekle/sil degistiriyor.
[DisallowMultipleComponent]
public class HabitTileGrid : MonoBehaviour
{
    [SerializeField] private HabbitManager manager;
    [SerializeField] private HabitTile tilePrefab;
    [SerializeField] private Transform content;
    [SerializeField] private PaintingViewBinder binder;

    private readonly List<HabitTile> tiles = new List<HabitTile>();

    // Yalnizca gorunen (aktif) tile'lar; binder bu listeyi geziyor.
    private readonly List<HabitTile> visible = new List<HabitTile>();

    private HabitFilters.HabitFilter filter = HabitFilters.HabitFilter.All;

    // Bir tile'a dokunuldu. Tik atma karari ekranin isi, tile'in degil.
    public event Action<string> TileClicked;

    public void SetFilter(in HabitFilters.HabitFilter value)
    {
        filter = value;
        Rebuild();
    }

    // Liste yapisi degisti (ekleme, silme, kategori degisimi) ya da ekran acildi.
    public void Rebuild()
    {
        if (manager == null || tilePrefab == null || content == null)
        {
            return;
        }

        visible.Clear();

        IReadOnlyList<HabitData> habits = manager.Habits;
        int used = 0;

        for (int i = 0; i < habits.Count; i++)
        {
            HabitData habit = habits[i];
            if (!HabitFilters.Matches(filter, habit))
            {
                continue;
            }

            HabitTile tile = TileAt(used);
            tile.gameObject.SetActive(true);
            tile.Bind(habit, manager.Library, false, manager.NeedsPaintingChoice(habit));

            visible.Add(tile);
            used++;
        }

        for (int i = used; i < tiles.Count; i++)
        {
            // Kapanan tile'in goruntusu kalirsa havuz sizar.
            ReleaseView(tiles[i]);
            tiles[i].gameObject.SetActive(false);
        }

        if (binder != null)
        {
            binder.SetTiles(visible);
        }
    }

    // Tek bir aliskanligin icerigi degisti: tile'i yerinde tazele.
    //
    // Bagli bir tile'in tablosu PaintingView tarafindan ciziliyor ve parcayi
    // HabbitManager zaten oraya gonderdi; ortuyu burada tekrar yazmak gerekmiyor
    // ama rozet, baslik ve seri guncellenmeli.
    public void Refresh(HabitData habit)
    {
        for (int i = 0; i < visible.Count; i++)
        {
            HabitTile tile = visible[i];
            if (string.Equals(tile.HabitId, habit.id, StringComparison.Ordinal))
            {
                tile.Bind(habit, manager.Library, true, manager.NeedsPaintingChoice(habit));
                return;
            }
        }
    }

    private HabitTile TileAt(int index)
    {
        while (tiles.Count <= index)
        {
            HabitTile created = Instantiate(tilePrefab, content);
            created.Clicked += HandleTileClicked;
            tiles.Add(created);
        }

        return tiles[index];
    }

    private void ReleaseView(HabitTile tile)
    {
        if (tile.View != null && manager != null)
        {
            manager.ReleaseView(tile.View);
            tile.SetView(null);
        }
    }

    private void HandleTileClicked(string habitId)
    {
        TileClicked?.Invoke(habitId);
    }
}
