using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Uc seviyenin de paylasacagi tablo cizici: sprite (gerekirse yanik/delikli
// pisirilmis hali) + PaintingCoverGrid ortusu + delik rozeti.
//
// HabitTile.Bind / HabitCard.Bind'daki "sprite sec, burnedArt'i tespit et,
// ortuyu cagir" uclusunun UCUNCU bir kopyasi degil, bu ikisinin PAYLASTIGI
// kalibin kendisi -- Galeri'nin SUREN ve BITMIS tablolari ayni rect'e farkli
// PaintingPieceState fabrikalariyla besliyor (bkz. PaintingPieceState.ForFinished,
// BurnedPaintingCache'in tablo-anahtarli sozlugu).
//
// Boyut HER ZAMAN definition.sprite'in NATIVE boyutundan turetilir, baglanan
// sprite'tan ASLA -- BurnedPaintingArt.Build 2x buyutulmus bir texture
// donduruyor, ondan olcu almak delikli bir tabloyu yanlislikla buyuk cizerdi.
//
// Rotus rozeti RotusState.Count > 0 VE delik varken mavi+parildayan+tiklanabilir
// olur (bkz. README). RotusState degisince (sheet acilip bir parca onarilince)
// bu bilesen KENDI basina yeniden bicimlenir -- son Bind'daki delik sayisini
// onbellege alip ApplyBadgeStyle'i tekrar cagiriyoruz, tum tabloyu yeniden
// baglamadan.
[DisallowMultipleComponent]
public class GalleryWorkFrame : MonoBehaviour
{
    [SerializeField] private RectTransform paintingRect;
    [SerializeField] private Image painting;
    // Tablonun altindaki yumusak golge. Tablo ile birlikte kapaniyor: sprite
    // cozulemediginde paintingRect 0x0 olur ve golge tek basina 10 birimlik bir
    // leke olarak kalirdi.
    [SerializeField] private Image shadow;
    [SerializeField] private PaintingCoverGrid cover;
    [SerializeField] private GameObject holeBadge;
    [SerializeField] private Image holeBadgeBorder;
    [SerializeField] private Image holeBadgeFill;
    [SerializeField] private TMP_Text holeBadgeLabel;
    [SerializeField] private Button holeBadgeButton;
    [SerializeField] private Image holeGlow;
    [SerializeField] private UiTheme theme;

    // Son Bind'daki delik sayisi. -1 = hic Bind edilmedi. RotusState.Changed
    // geldiginde tum tabloyu degil yalnizca rozeti yeniden bicimlendirmek icin.
    private int cachedHoleCount = -1;

    // Rozetin tiklanmasi: hangi habit/paintingId oldugunu bu sinif BILMIYOR --
    // o bilgi GalleryWallItem/GalleryWorkTile'in zaten elinde (kendi Bind
    // parametreleri), onlar bu olayi kendi habitId/paintingId'leriyle
    // yukari TASIYOR (bkz. GalleryWallItem.RepairRequested).
    public event Action RepairRequested;

    private void Awake()
    {
        if (holeBadgeButton != null)
        {
            holeBadgeButton.onClick.AddListener(RaiseRepairRequested);
        }
    }

    private void OnDestroy()
    {
        if (holeBadgeButton != null)
        {
            holeBadgeButton.onClick.RemoveListener(RaiseRepairRequested);
        }
    }

    private void OnEnable()
    {
        RotusState.Changed += HandleRotusChanged;
    }

    private void OnDisable()
    {
        RotusState.Changed -= HandleRotusChanged;
    }

    // scale: duvarda/detayda 2 (GalleryBands.PaintingScale), tablo
    // goruntuleyicisinde tam sayi k (Faz 3).
    public void Bind(HabitData habit, in GalleryWork work, float scale)
    {
        PaintingDefinition definition = work.Definition;

        Sprite sprite = work.IsFinished
            ? BurnedPaintingCache.SpriteFor(definition, habit.id, work.Holes)
            : BurnedPaintingCache.SpriteFor(definition, habit);

        bool burnedArt = sprite != null && sprite != definition.sprite;

        painting.sprite = sprite;
        painting.enabled = sprite != null;

        if (shadow != null)
        {
            shadow.enabled = sprite != null;
        }

        Vector2 native = definition.sprite != null ? definition.sprite.rect.size : Vector2.zero;
        paintingRect.sizeDelta = native * scale;

        PaintingPieceState state = work.IsFinished
            ? PaintingPieceState.ForFinished(definition.PieceCount, work.Holes)
            : PaintingPieceState.For(definition, habit);

        cover.Show(state, false, burnedArt);

        cachedHoleCount = work.Holes.Count;
        ApplyBadgeStyle();
    }

    private void HandleRotusChanged()
    {
        ApplyBadgeStyle();
    }

    private void ApplyBadgeStyle()
    {
        if (holeBadge == null || cachedHoleCount < 0)
        {
            return;
        }

        bool hasHoles = cachedHoleCount > 0;
        holeBadge.SetActive(hasHoles);

        if (!hasHoles)
        {
            StopGlow();
            return;
        }

        if (holeBadgeLabel != null)
        {
            holeBadgeLabel.text = cachedHoleCount.ToString();
        }

        bool rotusActive = RotusState.Count > 0;

        float size = rotusActive ? GalleryBands.HoleBadgeActiveSize : GalleryBands.HoleBadgeSize;
        ((RectTransform)holeBadge.transform).sizeDelta = new Vector2(size, size);

        if (theme != null)
        {
            if (holeBadgeBorder != null)
            {
                holeBadgeBorder.color = rotusActive ? theme.rotusBlueLight : theme.galleryHoleBadgeBorder;
            }

            if (holeBadgeFill != null)
            {
                holeBadgeFill.color = rotusActive ? theme.rotusBlue : theme.background;
            }
        }

        // Tiklanabilirligi RAYCAST uzerinden kapatiyoruz, yalnizca Button.
        // interactable degil: interactable=false bile pointer olayini yutup
        // alttaki karta/tile'a gecirmezdi.
        if (holeBadgeButton != null)
        {
            holeBadgeButton.enabled = rotusActive;
        }

        if (holeBadgeBorder != null)
        {
            holeBadgeBorder.raycastTarget = rotusActive;
        }

        if (rotusActive)
        {
            StartGlow();
        }
        else
        {
            StopGlow();
        }
    }

    private void StartGlow()
    {
        if (holeGlow == null || theme == null)
        {
            return;
        }

        holeGlow.gameObject.SetActive(true);
        holeGlow.color = theme.rotusGlow;

        GlowPulse.EnsureOn(holeGlow).Play(
            GalleryBands.GlowMinAlpha, GalleryBands.GlowMaxAlpha,
            GalleryBands.GlowMinScale, GalleryBands.GlowMaxScale,
            GalleryBands.GlowPeriodSeconds);
    }

    private void StopGlow()
    {
        if (holeGlow == null)
        {
            return;
        }

        GlowPulse pulse = holeGlow.GetComponent<GlowPulse>();
        if (pulse != null)
        {
            pulse.Stop();
        }

        holeGlow.gameObject.SetActive(false);
    }

    private void RaiseRepairRequested()
    {
        RepairRequested?.Invoke();
    }
}
