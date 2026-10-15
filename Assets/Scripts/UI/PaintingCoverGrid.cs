using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Bir tablonun uzerindeki kapali parca ortusu -- yalnizca uGUI, kamera ve
// RenderTexture yok.
//
// SALT OKUNUR: burada parca acan bir API BILEREK yok. Hangi parcanin acilacagina
// HabbitManager karar veriyor ve ayni indeksi butun goruntulere gonderiyor
// (bkz. CLAUDE.md > Invariants). Bu bilesen yalnizca kayittaki durumu cizer.
//
// Hucreler layout bileseniyle degil, dogrudan anchor kesirleriyle yerlestiriliyor:
// boyut degisiminde yeniden hesap gerekmiyor ve izgara her zaman tam oturuyor.
[DisallowMultipleComponent]
public class PaintingCoverGrid : MonoBehaviour
{
    private struct Fade
    {
        public int Cell;
        public float Elapsed;
    }

    [Tooltip("Kapali parcanin rengi. Tuval onizlemesinde altin, tablo ustunde koyu kahve.")]
    [SerializeField] private Color closedColor = new Color32(0x22, 0x15, 0x0D, 0xE6);

    [Tooltip("Yakilan parcanin YEDEK rengi. Normalde gorunum tablonun piksellerine " +
             "pisirilmis gelir (bkz. BurnedPaintingArt) ve bu renk hic kullanilmaz; " +
             "soldurulecek gorsel yoksa devreye girer.")]
    [SerializeField] private Color missedColor = new Color32(0x94, 0x94, 0x94, 0xC8);

    [Tooltip("Hucreler arasi bosluk (piksel). Tablo ortusunde 0, onizlemede 1.")]
    [SerializeField] private float spacing;

    [Tooltip("Bir parca acildiginda kapanma suresi. 0 = anlik.")]
    [SerializeField] private float fadeSeconds = 0.3f;

    [SerializeField] private Sprite cellSprite;

    private readonly List<Image> cells = new List<Image>(64);
    private readonly List<PaintingPieceStatus> cellStatus = new List<PaintingPieceStatus>(64);
    private readonly List<Fade> fading = new List<Fade>(8);

    private int pieceCount;

    // Yakilan parcanin gorunumu tablonun KENDI piksellerinde mi? Oyleyse ortu o
    // hucrede hicbir sey cizmemeli (bkz. BurnedPaintingArt).
    private bool missedDrawnByArt;

    public void Show(in PaintingPieceState state, bool animated)
    {
        Show(state, animated, false);
    }

    // Aktif tablonun gorunumunu cizer. Sequential/Random dallanmasi burada
    // DEGIL -- PaintingPieceState'in icinde, tek yerde (bkz. CLAUDE.md).
    //
    // missedDrawnByArt: cagiran, altindaki gorsele yakilan parcalarin pisirilmis
    // oldugunu biliyorsa true. O zaman Missed hucresi tamamen saydam kaliyor;
    // false ise eski duz missedColor yedegi ciziliyor.
    public void Show(in PaintingPieceState state, bool animated, bool missedDrawnByArt)
    {
        this.missedDrawnByArt = missedDrawnByArt;

        Build(state.PieceCount);

        for (int cell = 0; cell < pieceCount; cell++)
        {
            int piece = PaintingGrid.PieceIndexForCell(cell, pieceCount);
            SetCell(cell, state.StatusOf(piece), animated);
        }
    }

    // Tuval boyutu onizlemesi: butun hucreler kapali, yalnizca izgaranin sekli
    // gorunsun diye.
    public void ShowAllClosed(int totalPieces)
    {
        Build(totalPieces);

        for (int cell = 0; cell < pieceCount; cell++)
        {
            SetCell(cell, PaintingPieceStatus.Closed, false);
        }
    }

    public void SetClosedColor(Color color)
    {
        closedColor = color;
        RepaintWithoutFade(PaintingPieceStatus.Closed, closedColor);
    }

    public void SetMissedColor(Color color)
    {
        missedColor = color;
        RepaintWithoutFade(PaintingPieceStatus.Missed, missedColor);
    }

    private void RepaintWithoutFade(PaintingPieceStatus status, Color color)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            if (i < cellStatus.Count && cellStatus[i] == status)
            {
                SetAlpha(i, AlphaFor(status), color);
            }
        }
    }

    private void Update()
    {
        if (fading.Count == 0)
        {
            return;
        }

        float step = Time.deltaTime;

        for (int i = fading.Count - 1; i >= 0; i--)
        {
            Fade fade = fading[i];
            fade.Elapsed += step;

            float t = fadeSeconds > 0f ? Mathf.Clamp01(fade.Elapsed / fadeSeconds) : 1f;
            SetAlpha(fade.Cell, 1f - t, closedColor);

            if (t >= 1f)
            {
                fading.RemoveAt(i);
            }
            else
            {
                fading[i] = fade;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Kurulum
    // -----------------------------------------------------------------------

    // Parca sayisi degismediyse hucreler YENIDEN KURULMAZ: liste kaydirilirken
    // ayni kart baska bir aliskanliga baglaniyor ve her seferinde 64 GameObject
    // yaratip yok etmek gereksiz cop uretirdi.
    private void Build(int totalPieces)
    {
        if (totalPieces == pieceCount)
        {
            return;
        }

        pieceCount = Mathf.Max(0, totalPieces);
        fading.Clear();

        while (cells.Count < pieceCount)
        {
            cells.Add(CreateCell(cells.Count));
            cellStatus.Add(PaintingPieceStatus.Closed);
        }

        for (int i = 0; i < cells.Count; i++)
        {
            bool used = i < pieceCount;
            cells[i].gameObject.SetActive(used);

            // Bayat durum birakmiyoruz: satir sayisi degistigi icin ayni hucre
            // artik baska bir parcayi temsil ediyor.
            cellStatus[i] = PaintingPieceStatus.Closed;

            if (used)
            {
                Place(cells[i].rectTransform, i);
                SetAlpha(i, 1f, closedColor);
            }
        }
    }

    private Image CreateCell(int index)
    {
        GameObject cellObject = new GameObject("Cell " + index, typeof(RectTransform), typeof(Image));
        cellObject.layer = gameObject.layer;
        cellObject.transform.SetParent(transform, false);

        Image image = cellObject.GetComponent<Image>();
        image.sprite = cellSprite;
        image.color = closedColor;
        // Ortu dokunmayi yutmamali: tile'in kendi butonu tiklanabilir kalsin.
        image.raycastTarget = false;

        return image;
    }

    // Hucre indeksi uGUI sirasinda: 0 = sol ust, satirlar yukaridan asagiya.
    private void Place(RectTransform rect, int cellIndex)
    {
        if (PaintingGrid.RowsFor(pieceCount) <= 0)
        {
            return;
        }

        CellAnchors(cellIndex, pieceCount, out Vector2 anchorMin, out Vector2 anchorMax);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;

        float half = spacing * 0.5f;
        rect.offsetMin = new Vector2(half, half);
        rect.offsetMax = new Vector2(-half, -half);
        rect.localScale = Vector3.one;
    }

    // Hucre indeksinin anchor kesirleri (0..1), BOSLUK UYGULANMADAN. Place'in
    // kullandigi aritmetigin AYNISI, tek yerde: RotusRestoreSheet'in yanik
    // hucrelerin uzerine cizdigi 1pt cerceve de bunu cagiriyor, boylece hucre
    // geometrisinin ikinci bir kopyasi cikmiyor (bkz. CLAUDE.md -- piece
    // index -> geometri tek edit noktasi kurali).
    public static void CellAnchors(int cellIndex, int pieceCount, out Vector2 min, out Vector2 max)
    {
        int rows = PaintingGrid.RowsFor(pieceCount);
        if (rows <= 0)
        {
            min = Vector2.zero;
            max = Vector2.zero;
            return;
        }

        int column = cellIndex % PaintingGrid.Columns;
        int row = cellIndex / PaintingGrid.Columns;

        float left = (float)column / PaintingGrid.Columns;
        float right = (float)(column + 1) / PaintingGrid.Columns;
        float top = 1f - (float)row / rows;
        float bottom = 1f - (float)(row + 1) / rows;

        min = new Vector2(left, bottom);
        max = new Vector2(right, top);
    }

    private void SetCell(int cell, PaintingPieceStatus status, bool animated)
    {
        PaintingPieceStatus previous = cellStatus[cell];
        cellStatus[cell] = status;

        if (status == previous)
        {
            // Animasyonlu cagrida dokunmuyoruz ki suren bir fade yarida kalmasin;
            // animasyonsuz cagri (yeniden baglama) degeri tazeliyor.
            if (animated)
            {
                return;
            }

            StopFade(cell);
            SetAlpha(cell, AlphaFor(status), ColorFor(status));
            return;
        }

        StopFade(cell);

        // Yalnizca Closed -> Opened gecisi solarak kaybolur. Yakilan parca
        // (-> Missed) HIC solmaz: kalici bir durumdur, anlik beliriyor.
        if (status == PaintingPieceStatus.Opened && previous == PaintingPieceStatus.Closed
            && animated && fadeSeconds > 0f)
        {
            SetAlpha(cell, 1f, closedColor);
            fading.Add(new Fade { Cell = cell, Elapsed = 0f });
            return;
        }

        SetAlpha(cell, AlphaFor(status), ColorFor(status));
    }

    // Yakilan parca da acik parca gibi TAMAMEN saydam kaliyor: gorunumu tablonun
    // pikselleri tasiyor, ortunun ustune bir sey cizmesi onu bastirirdi. Yedek
    // yolda (soldurulecek gorsel yok) eskisi gibi opak missedColor.
    private float AlphaFor(PaintingPieceStatus status)
    {
        if (status == PaintingPieceStatus.Opened)
        {
            return 0f;
        }

        return status == PaintingPieceStatus.Missed && missedDrawnByArt ? 0f : 1f;
    }

    private Color ColorFor(PaintingPieceStatus status)
    {
        return status == PaintingPieceStatus.Missed ? missedColor : closedColor;
    }

    private void StopFade(int cell)
    {
        for (int i = 0; i < fading.Count; i++)
        {
            if (fading[i].Cell == cell)
            {
                fading.RemoveAt(i);
                return;
            }
        }
    }

    private void SetAlpha(int cell, float alpha, Color baseColor)
    {
        Color color = baseColor;
        color.a *= alpha;
        cells[cell].color = color;
    }
}
