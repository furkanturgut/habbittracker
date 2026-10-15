using System;
using UnityEngine;
using UnityEngine.UI;

// 03 Galeri ekraninin 3. seviyesindeki kucuk resim. GalleryWorkFrame'in
// cover/delik/golge ucluyunu TASIMIYOR -- teslim dosyasi bu seride sadece duz
// sprite'i 36x36'ya sigdirip suren tabloyu .55 opaklikla cizmeyi istiyor
// (bkz. README "Thumbnails"), kapali/yanik parca gorunumu burada hic yok.
//
// Index, GalleryWorkTile'daki ile AYNI kalip: kapanan closure degil, her
// Bind'da yeniden yazilan bir alan -- Button dinleyicisi Awake'te BIR KEZ
// takilip tikta hep GUNCEL Index'i okuyor.
[DisallowMultipleComponent]
public class GalleryThumb : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image border;
    [SerializeField] private RectTransform paintingRect;
    [SerializeField] private Image painting;
    [SerializeField] private UiTheme theme;

    public int Index { get; private set; }

    public event Action<int> Clicked;

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.AddListener(RaiseClicked);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(RaiseClicked);
        }
    }

    public void Bind(in GalleryWork work, bool selected, int index)
    {
        Index = index;

        Sprite sprite = work.Definition.sprite;
        painting.sprite = sprite;
        painting.enabled = sprite != null;

        Vector2 native = sprite != null ? sprite.rect.size : Vector2.zero;
        paintingRect.sizeDelta = GalleryBands.ThumbFit(native.x, native.y);

        Color color = Color.white;
        color.a = work.IsFinished ? 1f : 0.55f;
        painting.color = color;

        // Secili kenarlik tam deger (theme.gold); secili-olmayan icin tasarim
        // rgba(243,230,204,.12) istiyor ama mevcut alanlarin en yakini
        // borderSoft (.14) -- Faz 1'in "kenarlik .102 vs .08" sapmasiyla ayni
        // gerekce, yeni bir tema alani acmaya degmeyecek kadar kucuk bir fark.
        border.color = selected ? theme.gold : theme.borderSoft;
    }

    private void RaiseClicked()
    {
        Clicked?.Invoke(Index);
    }
}
