using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// FlowLayout'un RectTransform'a uygulanmasi: degisken genislikli duvar
// ogelerini soldan saga dizen, sigmayinca satir atlayan ve her satiri
// ORTALAYAN bir LayoutGroup.
//
// Hazir Unity gruplarindan hicbiri bunu yapmiyor: HorizontalLayoutGroup
// sarmiyor, GridLayoutGroup sabit hucre olcusu kullanip son satiri sola
// yapistiriyor. Galeri duvarindaki yuva genislikleri degisken
// (bkz. GalleryBands.SlotWidth), bu yuzden LayoutGroup'tan turetilen OZEL bir
// sinif gerekti.
//
// Aritmetik FlowLayout'ta (saf, test edilebilir); burasi SADECE RectTransform'a
// yazan taraf -- PaintingGrid/PaintingCoverGrid ayrimiyla ayni kural.
//
// LayoutGroup'tan turemenin faydasi: havuzlanmis duvar ogeleri SetActive
// (true/false) ile gorunurlugu degistirdiginde, Graphic.OnEnable/OnDisable
// zaten LayoutRebuilder.MarkLayoutForRebuild'i EN YAKIN ILayoutController'a
// tasiyor. LayoutGroup olmak bizi otomatik olarak o controller yapiyor --
// yani sahibin (GalleryHabitCard) her degisiklikten sonra elle bir Apply()
// cagirmasi GEREKMIYOR; tam da HabitCategorySection/HabitTileGrid'in
// GridLayoutGroup icin zaten bedava faydalandigi mekanizma.
[DisallowMultipleComponent]
public class FlowLayoutGroup : LayoutGroup
{
    [Tooltip("Yuvalar arasi yatay bosluk (tasarim birimi).")]
    [SerializeField] private float spacingX = 16f;

    [Tooltip("Satirlar arasi dikey bosluk (tasarim birimi).")]
    [SerializeField] private float spacingY = 12f;

    [Tooltip("Her yuvanin sabit yuksekligi (resim + plaka).")]
    [SerializeField] private float itemHeight = 112f;

    // CalculateLayoutInputHorizontal'da BIR KEZ hesaplanir; SetLayoutHorizontal/
    // Vertical onu sadece UYGULAR. Sebep: Unity'nin gecis sirasi butun
    // elemanlar icin once CalculateLayoutInputHorizontal (asagidan yukari),
    // sonra SetLayoutHorizontal (yukaridan asagi), sonra
    // CalculateLayoutInputVertical, en sonda SetLayoutVertical. Satir/x
    // atamasi CalculateLayoutInputVertical'a birakilsaydi SetLayoutHorizontal
    // (ONCE calisan) bir onceki karenin bayat verisiyle calisirdi.
    private readonly List<float> widthScratch = new List<float>(16);
    private readonly List<FlowItem> itemScratch = new List<FlowItem>(16);
    private float solvedHeight;

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        Solve();
    }

    public override void CalculateLayoutInputVertical()
    {
        SetLayoutInputForAxis(solvedHeight, solvedHeight, -1f, 1);
    }

    public override void SetLayoutHorizontal()
    {
        for (int i = 0; i < rectChildren.Count && i < itemScratch.Count; i++)
        {
            SetChildAlongAxis(rectChildren[i], 0, padding.left + itemScratch[i].X, widthScratch[i]);
        }
    }

    public override void SetLayoutVertical()
    {
        for (int i = 0; i < rectChildren.Count && i < itemScratch.Count; i++)
        {
            SetChildAlongAxis(rectChildren[i], 1, padding.top + itemScratch[i].Y, itemHeight);
        }
    }

    // Cocuklarin genisligi KENDI LayoutElement'lerinden geliyor (bkz.
    // GalleryWallItem.Bind) -- bu grup genislik UYGULAMAZ, sadece OKUR.
    private void Solve()
    {
        widthScratch.Clear();
        for (int i = 0; i < rectChildren.Count; i++)
        {
            widthScratch.Add(LayoutUtility.GetPreferredSize(rectChildren[i], 0));
        }

        float availableWidth = rectTransform.rect.width - padding.left - padding.right;
        float contentHeight = FlowLayout.Solve(widthScratch, availableWidth, spacingX, spacingY, itemHeight, itemScratch);
        solvedHeight = contentHeight + padding.top + padding.bottom;
    }
}
