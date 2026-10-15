using UnityEngine;

// Ressamin KENDI tablosunun 6x6 ortusu. Bugun tamamlanan aliskanlik orani kadar
// hucre aciliyor; gun donunce kendiliginden kapaniyor (oran sifirlaniyor).
//
// Neden PaintPixels degil: o modul 8 sutuna ve 16/32/64 parcaya KILITLI ve bu
// kural kayit formatinin parcasi (bkz. CLAUDE.md). 36 parcalik bir izgara icin
// onu genisletmek, aliskanlik tablolarinin invariant'ini bozardi. Bu yuzden
// sovalenin ortusu ayri, kucuk ve tamamen bagimsiz.
//
// Hucreler builder tarafindan uretiliyor, calisma aninda degil: sahnede gorunur
// ve ayarlanabilir kaliyorlar, ve layer'lari (0) builder tarafindan garanti
// ediliyor -- layer 8'e dusen bir hucre butun tablo kameralarinda gorunurdu.
[DisallowMultipleComponent]
public class EaselCover : MonoBehaviour
{
    public const int Columns = 6;
    public const int Rows = 6;
    public const int CellCount = Columns * Rows;

    [Tooltip("Sol ustten saga dogru, satir satir. Builder dolduruyor.")]
    [SerializeField] private SpriteRenderer[] cells = new SpriteRenderer[0];

    [SerializeField] private Color coverColor = new Color32(0xE9, 0xDC, 0xBF, 0xFF);
    [SerializeField] private float fadeSeconds = 0.5f;

    private float[] alphas;
    private int revealed = -1;
    private bool animating;

    private void OnEnable()
    {
        EnsureAlphas();
        // Ekran yeniden acilirken yarim kalmis bir gecis birakmiyoruz.
        ApplyAll(true);
    }

    // Bugunun tamamlanma orani (0..1).
    public void SetRevealedRatio(float ratio)
    {
        SetRevealedCount(Mathf.RoundToInt(Mathf.Clamp01(ratio) * CellCount));
    }

    public void SetRevealedCount(int count)
    {
        count = Mathf.Clamp(count, 0, CellCount);
        if (count == revealed)
        {
            return;
        }

        revealed = count;
        animating = true;
    }

    private void Update()
    {
        if (!animating)
        {
            return;
        }

        animating = ApplyAll(false);
    }

    // Butun hucreleri hedef alfalarina tasir. Hala hareket eden varsa true doner.
    private bool ApplyAll(bool instant)
    {
        EnsureAlphas();

        float step = instant || fadeSeconds <= 0f ? 1f : Time.deltaTime / fadeSeconds;
        bool moving = false;

        for (int i = 0; i < cells.Length && i < alphas.Length; i++)
        {
            if (cells[i] == null)
            {
                continue;
            }

            float target = i < revealed ? 0f : 1f;
            alphas[i] = Mathf.MoveTowards(alphas[i], target, step);

            Color color = coverColor;
            color.a *= alphas[i];
            cells[i].color = color;

            if (!Mathf.Approximately(alphas[i], target))
            {
                moving = true;
            }
        }

        return moving;
    }

    private void EnsureAlphas()
    {
        if (alphas == null || alphas.Length != cells.Length)
        {
            alphas = new float[cells.Length];
            for (int i = 0; i < alphas.Length; i++)
            {
                alphas[i] = 1f;
            }
        }

        if (revealed < 0)
        {
            revealed = 0;
        }
    }
}
