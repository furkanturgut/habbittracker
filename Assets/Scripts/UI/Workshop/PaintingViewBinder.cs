using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Gorunur tile'lara PaintingView baglar, gorunmez olanlarinkini birakir.
//
// Havuzda dokuz goruntu var ve her biri kendi kamerasi + RenderTexture'i demek;
// izgara ise onlarca aliskanlik tasiyabiliyor. Geri donusum olmazsa havuz
// tukenir ve alttaki kartlar tablosuz kalirdi.
//
// ONCE BIRAKMA, SONRA BAGLAMA: ters sirada, ekrandan cikan goruntuler henuz
// serbest kalmadan yeni istekler gelir ve havuz gereksiz yere tukenirdi.
[DisallowMultipleComponent]
public class PaintingViewBinder : MonoBehaviour
{
    [SerializeField] private HabbitManager manager;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform viewport;

    [Tooltip("Viewport'un disinda bu kadar piksel kalan tile da bagli tutulur: kaydirirken bag/birak titremesini onler.")]
    [SerializeField] private float margin = 40f;

    [Tooltip("Ayni anda bagli tutulacak en fazla goruntu. Havuz boyutundan kucuk olmali.")]
    [SerializeField] private int budget = 8;

    private List<HabitTile> tiles;
    private bool dirty;

    public void SetTiles(List<HabitTile> value)
    {
        tiles = value;
        dirty = true;
    }

    public void Invalidate()
    {
        dirty = true;
    }

    private void OnEnable()
    {
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.AddListener(HandleScrolled);
        }

        dirty = true;
    }

    // Panel kapanirken HER goruntu birakilmali. Birakilmazsa Alisknliklar
    // ekranina gecildiginde dokuzluk havuz kalici olarak tukenmis kalir ve
    // Atolye'ye donuldugunde hicbir tile animasyonlu tablo gosteremez.
    private void OnDisable()
    {
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.RemoveListener(HandleScrolled);
        }

        ReleaseAll();
    }

    private void LateUpdate()
    {
        if (!dirty)
        {
            return;
        }

        dirty = false;
        Apply();
    }

    private void HandleScrolled(Vector2 _)
    {
        dirty = true;
    }

    private void Apply()
    {
        if (tiles == null || manager == null || viewport == null)
        {
            return;
        }

        // 1. Gorunmeyenleri birak.
        for (int i = 0; i < tiles.Count; i++)
        {
            HabitTile tile = tiles[i];
            if (tile.View != null && !IsVisible(tile))
            {
                Release(tile);
            }
        }

        // 2. Bagli kalanlari say, sonra gorunenleri butce kadar bagla.
        int bound = 0;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i].View != null)
            {
                bound++;
            }
        }

        for (int i = 0; i < tiles.Count && bound < budget; i++)
        {
            HabitTile tile = tiles[i];
            if (tile.View != null || !tile.gameObject.activeInHierarchy || !IsVisible(tile))
            {
                continue;
            }

            PaintingView view = manager.BindView(tile.HabitId);
            if (view == null)
            {
                // Havuz tukendi: tile statik yolda kaliyor, bir sonraki
                // kaydirmada tekrar denenecek.
                continue;
            }

            view.AttachTarget(tile.RenderTarget);
            tile.SetView(view);
            bound++;
        }
    }

    private void ReleaseAll()
    {
        if (tiles == null)
        {
            return;
        }

        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i].View != null)
            {
                Release(tiles[i]);
            }
        }
    }

    private void Release(HabitTile tile)
    {
        if (manager != null)
        {
            manager.ReleaseView(tile.View);
        }

        tile.SetView(null);
    }

    // Dikey kaydirma: yalnizca y ekseninde ortusme yetiyor.
    private bool IsVisible(HabitTile tile)
    {
        RectTransform rect = tile.Rect;
        Vector3 center = viewport.InverseTransformPoint(rect.TransformPoint(rect.rect.center));

        float limit = viewport.rect.height * 0.5f + rect.rect.height * 0.5f + margin;
        return Mathf.Abs(center.y - viewport.rect.center.y) <= limit;
    }
}
