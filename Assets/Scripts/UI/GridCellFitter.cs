using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// GridLayoutGroup'un hucre boyutunu SUTUN SAYISINDAN hesaplar.
//
// uGUI'de "genislige gore esit bolun" diye bir kip yok: cellSize sabit piksel.
// Tasarim 3 sutun istiyor ve telefon genisligi cihazdan cihaza degisiyor, bu
// yuzden boyutu her yeniden olculendirmede kendimiz yaziyoruz.
[RequireComponent(typeof(GridLayoutGroup))]
[DisallowMultipleComponent]
public class GridCellFitter : UIBehaviour
{
    [SerializeField] private int columns = 3;

    [Tooltip("Kare gorselin ALTINA eklenecek yukseklik (baslik + seri satiri).")]
    [SerializeField] private float extraHeight;

    private GridLayoutGroup grid;
    private RectTransform rect;

    protected override void OnEnable()
    {
        base.OnEnable();
        Apply();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        Apply();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        Apply();
    }
#endif

    public void Apply()
    {
        if (columns <= 0)
        {
            return;
        }

        if (grid == null)
        {
            grid = GetComponent<GridLayoutGroup>();
            rect = (RectTransform)transform;
        }

        float usable = rect.rect.width
            - grid.padding.left - grid.padding.right
            - grid.spacing.x * (columns - 1);

        if (usable <= 0f)
        {
            return;
        }

        float side = usable / columns;

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.cellSize = new Vector2(side, side + extraHeight);
    }
}
