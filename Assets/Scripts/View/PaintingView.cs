using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Bir tabloyu UI'da gosteren birim: PaintPixels modulu + kendi kamerasi + kendi
// RenderTexture'i. Modul world space'te calisip kameranin tum gorunur alanini
// kapladigi icin bir UI kutusuna ancak boyle sigiyor.
//
// Kamera normalde KAPALI: RenderTexture son icerigini korudugu icin tablo
// degismedigi surece ek render pass yok.
[DisallowMultipleComponent]
public class PaintingView : MonoBehaviour
{
    [Header("Baglantilar (bos birakilirsa runtime'da kurulur)")]
    [SerializeField] private PaintPixels module;
    [SerializeField] private Camera viewCamera;

    [Header("RenderTexture")]
    [Tooltip("Kare texture kenari. Liste karti icin 256, detay ekrani icin 512 iyi baslangic.")]
    [SerializeField] private int textureSize = 256;

    [Tooltip("Tablonun arkasinda kalan renk. Tablolar opak oldugu icin normalde gorunmez.")]
    [SerializeField] private Color clearColor = new Color(0f, 0f, 0f, 0f);

    [Header("Sahne")]
    [Tooltip("Goruntu kameralarinin gordugu layer. Ana kameranin culling mask'inden CIKARILMALI.")]
    [SerializeField] private string paintingLayerName = "Paintings";

    [Tooltip("Kameranin ortografik boyutu. Modul kendini buna gore olcekledigi icin degerin kendisi onemli degil, sabit kalmasi yeterli.")]
    [SerializeField] private float orthographicSize = 5f;

    private RenderTexture renderTexture;
    private RawImage target;
    private string boundHabitId;

    // Sequential + delikli tablolarda acik parca indekslerini yeniden kurmak
    // icin tahsissiz tampon (bkz. PaintingPieceState.CollectOpenedIndices).
    private readonly List<int> openedScratch = new List<int>(64);

    // Kamerayi kac kare daha acik tutacagiz. Fade bitse bile bir kare fazladan
    // cizmek gerekiyor: son alpha degeri texture'a LateUpdate'te yukleniyor.
    private int holdRenderFrames;

    // Tablo tamamen acildiginda tetiklenir. Hangi goruntude oldugu argumanla
    // geliyor: PaintPixels.Completed parametresiz oldugu icin kimligi biz ekliyoruz.
    public event Action<PaintingView> PaintingCompleted;

    public string BoundHabitId => boundHabitId;
    public bool IsBound => !string.IsNullOrEmpty(boundHabitId);

    // Alttaki modul: veil rengi / fade suresi gibi gorsel ayarlar ve hata ayiklama
    // icin. ILERLEME BURADAN SURULMEZ -- hangi parcanin acilacagina HabbitManager
    // karar verir ve ayni indeksi tum goruntulere gonderir. Module.RevealNext()
    // cagirmak Random modda liste karti ile detay ekranini birbirinden ayirir.
    public PaintPixels Module => module;

    // Tablonun cizildigi RenderTexture. Bir RawImage'a takmak icin AttachTarget
    // yeterli; bu alan baska bir materyale ya da efekte beslemek isteyen taraf icin.
    public RenderTexture Texture => renderTexture;

    private void Awake()
    {
        EnsureHierarchy();
    }

    private void OnDestroy()
    {
        if (module != null)
        {
            module.Completed -= HandleModuleCompleted;
        }

        DetachTarget();

        if (renderTexture != null)
        {
            // RenderTexture GC ile toplanmaz; elle birakilmazsa sahne
            // degisimlerinde VRAM'de birikir.
            if (viewCamera != null)
            {
                viewCamera.targetTexture = null;
            }

            renderTexture.Release();
            Destroy(renderTexture);
            renderTexture = null;
        }
    }

    private void LateUpdate()
    {
        if (viewCamera == null || !viewCamera.enabled)
        {
            return;
        }

        // Fade suruyor: her karede yeniden cizilmeli.
        if (module != null && module.IsAnimating)
        {
            holdRenderFrames = 2;
            return;
        }

        if (holdRenderFrames > 0)
        {
            holdRenderFrames--;
            return;
        }

        // Kamera kapatiliyor ama RenderTexture son icerigini koruyor: tablo
        // ekranda durmaya devam eder, sadece yeniden cizilmez.
        viewCamera.enabled = false;
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused)
        {
            // Arka plandan donuste RenderTexture icerigi kaybolmus olabilir
            // (grafik cihazi sifirlanmasi). Bir kare yeniden cizdiriyoruz.
            RequestRender();
        }
    }

    // RenderTexture boyutunu Awake'ten ONCE degistirir; havuz liste kartlari ile
    // detay ekranina farkli cozunurluk verebilsin diye.
    //
    // Awake texture'i kurdugu icin bu cagri gec kalirsa etkisiz olur. Havuz bu
    // yuzden GameObject'i PASIF yaratip bileseni ekliyor, boyutu yaziyor ve ancak
    // sonra aktiflestiriyor -- Awake o anda calisiyor.
    public void OverrideTextureSize(int size)
    {
        if (renderTexture != null)
        {
            Debug.LogWarning(
                "PaintingView: RenderTexture zaten kuruldu, boyut degisikligi yok sayildi. " +
                "OverrideTextureSize, GameObject aktiflestirilmeden once cagrilmali.",
                this);
            return;
        }

        textureSize = Mathf.Max(16, size);
    }

    // Goruntuyu havuzdaki kendi slotuna tasir. Konum EBEVEYNE GORE: havuz
    // GameObject'i sahnede nerede dururken de slotlar birbirine gore ayni mesafede
    // kaliyor. Mesafe kritik -- her goruntunun kendi kamerasi var ve kameralar
    // birbirinin tablosunu gormemeli (bkz. PaintingViewPool).
    public void MoveToSlot(Vector3 localPosition)
    {
        transform.localPosition = localPosition;
    }

    // Goruntuyu bir aliskanligin aktif tablosuna baglar ve kayitli ilerlemeyi geri
    // yukler. Ayni goruntu daha sonra baska bir aliskanliga baglanabilir.
    public void Bind(string habitId, PaintingDefinition definition, HabitData habit)
    {
        if (definition == null || definition.sprite == null)
        {
            Debug.LogWarning("PaintingView: '" + habitId + "' icin gecerli bir tablo yok, goruntu bos kaliyor.", this);
            Unbind();
            return;
        }

        boundHabitId = habitId;

        // Yakilan parcalar modulun cizdigi SPRITE'in icinde geliyor (soldurulmus
        // + taramali, bkz. BurnedPaintingArt). Deligi olmayan tabloda tanimin
        // kendi sprite'i donuyor, yani yaygin durumda hicbir maliyet yok.
        Sprite sprite = BurnedPaintingCache.SpriteFor(definition, habit);
        bool burnedArt = sprite != null && sprite != definition.sprite;

        PaintPixels.PaintingSetup setup = PaintPixels.PaintingSetup.ForRenderTexture(
            sprite,
            definition.resolution,
            definition.revealOrder,
            definition.revealMode,
            viewCamera);

        // Kayitli ilerleme kurulumun PARCASI: modul kapali parca listesini kurdugu
        // an uyguluyor, ayri bir RestoreProgress cagrisi beklemiyor. Acik parca
        // indeksleri HER modda (Sequential dahil) acik liste olarak gonderiliyor:
        // delikler (missedPixels) Sequential'in "ilk N parca acik" kuralini
        // bozdugu icin tek bir sayi artik yetmiyor (bkz. PaintingPieceState).
        PaintingPieceState state = PaintingPieceState.For(definition, habit);
        state.CollectOpenedIndices(openedScratch);
        setup.OpenedIndices = openedScratch;
        setup.LockedIndices = state.MissedPieces;
        // Perde yakilan parcada CEKILSIN: gorunum zaten sprite'ta.
        setup.BurnedArtInSprite = burnedArt;

        // Sirasi onemli: Configure alanlari yazdiktan SONRA SetActive(true). Modul
        // ilk kez baglaniyorsa GameObject hala pasif (bkz. EnsureHierarchy), yani
        // Start daha hic calismamis; SetActive(true) onu simdi tetikler ve Start
        // artik dogru sprite'i gorur. Modul zaten aktifse (yeniden baglama) bu
        // cagri no-op'tur, Configure kendi ic yolundan gecer.
        module.Configure(setup);
        module.gameObject.SetActive(true);
        module.ApplyLayerToChildren();

        RequestRender();
    }

    public void Unbind()
    {
        boundHabitId = null;
    }

    // Belirli bir pikseli acar. Hangi piksel oldugunu HabbitManager belirliyor;
    // goruntu yalnizca gosteriyor. Ayni aliskanligin birden fazla goruntusu ayni
    // indeksi aldigi icin hepsi senkron kaliyor.
    public bool RevealPiece(int pieceIndex, bool animated)
    {
        if (module == null || !module.IsBuilt)
        {
            return false;
        }

        bool revealed = module.RevealPiece(pieceIndex, animated);
        if (revealed)
        {
            RequestRender();
        }

        return revealed;
    }

    public void AttachTarget(RawImage image)
    {
        DetachTarget();

        target = image;
        if (target != null)
        {
            target.texture = renderTexture;
            RequestRender();
        }
    }

    public void DetachTarget()
    {
        if (target != null)
        {
            target.texture = null;
            target = null;
        }
    }

    // Kamerayi birkac kare acik tutar. Fade suresince LateUpdate bunu
    // kendiliginden tazeliyor.
    public void RequestRender(int frames = 2)
    {
        if (viewCamera == null)
        {
            return;
        }

        if (frames > holdRenderFrames)
        {
            holdRenderFrames = frames;
        }

        viewCamera.enabled = true;
    }

    private void HandleModuleCompleted()
    {
        PaintingCompleted?.Invoke(this);
    }

    // -----------------------------------------------------------------------
    // Kurulum
    // -----------------------------------------------------------------------

    // Modul, kamera ve RenderTexture'i kurar. Inspector'da atanmadiysa hepsi
    // runtime'da yaratiliyor -- boylece bu bilesen bos bir GameObject'e
    // eklendiginde de calisiyor, prefab hazirlamaya gerek kalmiyor.
    private void EnsureHierarchy()
    {
        int layer = ResolveLayer();
        gameObject.layer = layer;

        if (module == null)
        {
            GameObject moduleObject = new GameObject("Painting");
            // Bind cagrilana kadar PASIF: aktif olsa Unity PaintPixels.Start()'i
            // hemen cagirir ve sprite henuz yokken bosuna uyari basar.
            moduleObject.SetActive(false);
            moduleObject.transform.SetParent(transform, false);
            module = moduleObject.AddComponent<PaintPixels>();
        }

        module.gameObject.layer = layer;
        module.ApplyLayerToChildren();
        module.Completed += HandleModuleCompleted;

        if (viewCamera == null)
        {
            GameObject cameraObject = new GameObject("View Camera");
            cameraObject.transform.SetParent(transform, false);
            // Modul z=0 duzleminde duruyor; kamera onun onunde.
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
            viewCamera = cameraObject.AddComponent<Camera>();
        }

        viewCamera.orthographic = true;
        viewCamera.orthographicSize = orthographicSize;
        viewCamera.clearFlags = CameraClearFlags.SolidColor;
        viewCamera.backgroundColor = clearColor;
        // Yalnizca kendi tablosunu gorsun: ana sahne bu kameraya girmemeli.
        viewCamera.cullingMask = 1 << layer;
        viewCamera.allowHDR = false;
        viewCamera.allowMSAA = false;
        // Bos beklerken tek bir ek render pass bile olmasin.
        viewCamera.enabled = false;

        // Derinlik 0 degil 24: URP Render Graph, bir kameranin cikis texture'inda
        // depth buffer zorunlu tutuyor -- 0 verilirse calisma aninda patliyor.
        renderTexture = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.Default)
        {
            name = name + " RT",
            // Piksel sanati: bilinear filtre tablolari bulaniklastirirdi.
            filterMode = FilterMode.Point,
            useMipMap = false,
            autoGenerateMips = false,
            antiAliasing = 1
        };
        renderTexture.Create();

        viewCamera.targetTexture = renderTexture;
    }

    private int ResolveLayer()
    {
        int layer = LayerMask.NameToLayer(paintingLayerName);
        if (layer < 0)
        {
            Debug.LogWarning(
                "PaintingView: '" + paintingLayerName + "' layer'i tanimli degil. " +
                "Project Settings > Tags and Layers'tan ekleyin ve Main Camera'nin " +
                "Culling Mask'inden cikarin; aksi halde tablolar ana sahnede de gorunur.",
                this);
            return gameObject.layer;
        }

        return layer;
    }
}
