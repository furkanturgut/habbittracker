using System.Collections.Generic;
using UnityEngine;

// Bir tabloyu ve uzerindeki perdeyi cizen, parcalari tek tek acan modul.
// Aliskanliktan, kayittan ve UI'dan haberi yok (bkz. CLAUDE.md > Rendering module).
public class PaintPixels : MonoBehaviour
{
    public enum RevealMode
    {
        Instant,
        Fade
    }

    // "Hangi parca" sorusu; RevealMode "nasil gorunur" sorusunu cevapliyor.
    public enum RevealOrder
    {
        Random,
        Sequential
    }

    public enum OverlayResolution
    {
        Size16 = 16,
        Size32 = 32,
        Size64 = 64
    }

    // Sutun her modda sabit; satir sayisi cozunurlukten turetilir (16 -> 8x2,
    // 32 -> 8x4, 64 -> 8x8), boylece bolme cizgileri uc modda birbirine oturur.
    private const int Columns = 8;

    [Header("Overlay")]
    [SerializeField] private OverlayResolution overlayResolution = OverlayResolution.Size32;

    [Header("Veil")]
    [ColorUsage(false)]
    [SerializeField] private Color veilColor = Color.black;
    // 0 = perde yok, 1 = tam opak.
    [Range(0f, 1f)]
    [SerializeField] private float veilStrength = 1f;

    [Header("Kacirilan (kilitli) parca")]
    [Tooltip("Planli oldugu halde yapilmayan gunde kalici kapanan parcanin rengi. Normal perdeden AYIRT EDILEBILIR olmali.")]
    [ColorUsage(false)]
    [SerializeField] private Color lockedColor = new Color(0.58f, 0.58f, 0.58f);
    // 1 yerine 0.78: tablo hafifce altindan gorunsun, tam opak kapatmasin.
    [Range(0f, 1f)]
    [SerializeField] private float lockedStrength = 0.78f;

    [Header("Reveal")]
    [SerializeField] private RevealMode revealMode = RevealMode.Fade;
    [SerializeField] private RevealOrder revealOrder = RevealOrder.Sequential;
    [SerializeField] private float fadeDuration = 0.3f;

    [Header("Scene")]
    [SerializeField] private Sprite backgroundSprite;
    // Kadrajda UST SINIR: ekran dar geldiginde gorsel bundan kucuk kalir, buyumez.
    [SerializeField] private float targetWorldHeight = 8f;
    // Bos birakilirsa Camera.main. Ana projede birden fazla kamera olabiliyor.
    [SerializeField] private Camera targetCamera;

    [Header("Mobile Framing")]
    // Gorunur alanin en fazla bu kadari kaplanir. RenderTexture'da 1 olmali.
    [Range(0.1f, 1f)]
    [SerializeField] private float screenFillRatio = 0.9f;
    // Yalnizca modul DOGRUDAN ekrana cizerken anlamli: Screen.safeArea ekrani
    // anlatir, RenderTexture'i degil.
    [SerializeField] private bool respectSafeArea = true;

    // Coroutine yerine duz veri: coroutine GameObject deaktif olunca oluyordu ve
    // parca kapali listeden cikmis oldugu icin yari solmus halde kaliyordu.
    private struct FadingPiece
    {
        public int PieceIndex;
        public byte StartAlpha;
        public float Elapsed;
    }

    private Texture2D overlayTexture;
    private Sprite overlaySprite;
    private SpriteRenderer overlayRenderer;
    // Tablo degisiminde ayni child yeniden kullaniliyor (bkz. BuildLayout).
    private SpriteRenderer backgroundRenderer;

    private bool runtimeBuilt;
    // Configure Start'tan once gelirse yalnizca alanlari yazip cikar.
    private bool startCalled;

    private Color32[] pixels;
    private readonly List<int> closedPieces = new List<int>();
    private readonly List<FadingPiece> fadingPieces = new List<FadingPiece>();

    // Kalici kapanan (kacirilan) parcalar. closedPieces'TEN AYRI: bir daha
    // oraya donmezler, RevealNext/RevealPiece bu yuzden onlari hic secemez.
    private readonly List<int> lockedPieces = new List<int>();
    private Camera cachedCamera;
    private bool pixelsDirty;
    private Vector2Int lastScreenSize;
    private Rect lastSafeArea;
    private int totalPieces;
    private int openedPieces;

    // Cozunurluk kurulumda bir kez okunuyor: alani canli okumak, Play mode'da
    // Inspector'dan degistirilince SetPieceAlpha'yi pixels dizisinin disina tasirdi.
    private int textureSize;
    private int rows;

    // Kayitli ilerleme; Configure ile gelir (bkz. PaintingSetup).
    private int restoreCount;
    private IReadOnlyList<int> restoreIndices;
    private IReadOnlyList<int> restoreLockedIndices;

    // bkz. PaintingSetup.BurnedArtInSprite.
    private bool burnedArtInSprite;

    public event System.Action Completed;

    public int TotalPieces => totalPieces;
    public int OpenedPieces => openedPieces;
    public int LockedPieces => lockedPieces.Count;

    // Sayac parca gercekten alpha 0'a ulastigi anda artiyor: Fade modunda son
    // parca kapali listeden cikmis ama hala soluyor olabilir. Kilitli parcalar
    // da TAM sayiliyor -- onlar bir daha asla acilmayacagi icin acik +
    // kilitli toplami tuvali hic yakalamazsa "tamamen cozuldu" asla true olmaz.
    public bool IsFullyRevealed => totalPieces > 0 && openedPieces + lockedPieces.Count >= totalPieces;

    // Goruntu kamerasi yalnizca resim degisirken acilsin diye.
    public bool IsAnimating => fadingPieces.Count > 0;

    public bool IsBuilt => runtimeBuilt;

    // Sradaki parcayi acar; hangisini revealOrder belirler.
    //
    // ANA UYGULAMA BUNU CAGIRMAZ -- secim HabbitManager'in isi, o ayni indeksi
    // RevealPiece ile tum goruntulere gonderir. Burasi modulu tek basina kullanan
    // taraf icin (bkz. PaintPixelsSandbox).
    public bool RevealNext()
    {
        if (closedPieces.Count == 0)
        {
            return false;
        }

        int pieceIndex = closedPieces[PickClosedListIndex()];

        // fadeDuration 0 iken bolme yapmadan ayirmak NaN riskini de bir karelik
        // gecikmeyi de ortadan kaldiriyor.
        return RevealPiece(pieceIndex, revealMode == RevealMode.Fade && fadeDuration > 0f);
    }

    // Belirli bir parcayi ANLIK acar; revealMode ne olursa olsun fade beklemez.
    public bool RevealPiece(int pieceIndex)
    {
        return RevealPiece(pieceIndex, false);
    }

    // Belirli bir parcayi acar. Parca zaten acikken, soluyorken ya da index
    // aralik disindayken false doner.
    public bool RevealPiece(int pieceIndex, bool animated)
    {
        if (!animated || revealMode == RevealMode.Instant || fadeDuration <= 0f)
        {
            return OpenPieceImmediate(pieceIndex, true);
        }

        if (pieceIndex < 0 || pieceIndex >= totalPieces)
        {
            return false;
        }

        int listIndex = closedPieces.IndexOf(pieceIndex);
        if (listIndex < 0)
        {
            // Zaten acik ya da soluyor. Ikinci bir fade sayaci iki kez artirir,
            // openedPieces == totalPieces karesi atlanir ve Completed hic gelmez.
            return false;
        }

        closedPieces.RemoveAt(listIndex);

        // Kapali alpha'dan basliyoruz: veilStrength 1'in altindayken sabit 255'ten
        // baslamak parcayi ilk karede tam opak hale sicratir.
        fadingPieces.Add(new FadingPiece
        {
            PieceIndex = pieceIndex,
            StartAlpha = CoveredAlpha,
            Elapsed = 0f
        });

        return true;
    }

    // count kadar parcayi anlik acar, acilan sayiyi doner. "Kalani ac" / "atla"
    // butonu icin RevealInstantly(TotalPieces).
    public int RevealInstantly(int count)
    {
        int opened = 0;
        while (opened < count && closedPieces.Count > 0)
        {
            // Index kapali listeden geldigi icin normalde hep true doner; aksi
            // halde ayni parcayi sonsuza kadar denememek icin cikiyoruz.
            if (!OpenPieceImmediate(closedPieces[PickClosedListIndex()], true))
            {
                break;
            }

            opened++;
        }

        return opened;
    }

    // Kayittan donus. Kurulum henuz yapilmadiysa istek saklanir. Completed
    // bilerek tetiklenmiyor -- acilista bitmis bir kayit yeniden bildirmemeli.
    public void RestoreProgress(IReadOnlyList<int> openedPieceIndices)
    {
        restoreIndices = openedPieceIndices;
        restoreCount = 0;
        ApplyRestoreIfBuilt();
    }

    // Ilk openedCount parca acik sayilir. Sequential bir kayit tek int olarak
    // saklanabilsin diye var; Random modda bu sayidan hangi parcalar oldugu cikmaz.
    public void RestoreProgress(int openedCount)
    {
        restoreCount = openedCount;
        restoreIndices = null;
        ApplyRestoreIfBuilt();
    }

    // Acilmis parca index'lerini artan sirada buffer'a yazar. Solmakta olanlar da
    // acilmis sayilir: kayit geri yuklendiginde yarim fade degil, acik parca
    // beklenir. Kilitli parcalar HARIC -- onlar acik degil, kalici kapali.
    public void GetOpenedPieces(List<int> buffer)
    {
        if (buffer == null)
        {
            return;
        }

        buffer.Clear();

        for (int i = 0; i < totalPieces; i++)
        {
            if (!ListLookup.Contains(closedPieces, i) && !ListLookup.Contains(lockedPieces, i))
            {
                buffer.Add(i);
            }
        }
    }

    // Verilen dunya noktasi perdenin uzerinde mi? Sadece x/y: ortografik kamerada
    // ScreenToWorldPoint'in z'si kameradan uzakligi tasir, sprite'in kalinligi 0 --
    // bounds.Contains() bu yuzden hep false donerdi.
    public bool ContainsWorldPoint(Vector3 worldPoint)
    {
        if (overlayRenderer == null)
        {
            return false;
        }

        Bounds bounds = overlayRenderer.bounds;
        return worldPoint.x >= bounds.min.x && worldPoint.x <= bounds.max.x
            && worldPoint.y >= bounds.min.y && worldPoint.y <= bounds.max.y;
    }

    // Child'lar yeni GameObject olarak dogdugu icin ebeveynin layer'ini
    // kendiliginden ALMAZLAR; almazlarsa goruntu kamerasi tabloyu hic gormez.
    public void ApplyLayerToChildren()
    {
        int layer = gameObject.layer;

        if (backgroundRenderer != null)
        {
            backgroundRenderer.gameObject.layer = layer;
        }

        if (overlayRenderer != null)
        {
            overlayRenderer.gameObject.layer = layer;
        }
    }

    // Configure'a gecirilen ayar paketi. Struct + `in`: tablo degisimi kutulama
    // ya da tahsis uretmesin.
    public struct PaintingSetup
    {
        public Sprite Sprite;
        public OverlayResolution Resolution;
        public RevealOrder Order;
        public RevealMode Mode;

        public float ScreenFillRatio;
        public bool RespectSafeArea;
        public float TargetWorldHeight;

        public Camera Camera;

        // Kayitli ilerleme kurulumla BIRLIKTE geliyor: SetActive(true) Start'i
        // senkron cagirmadigi icin ayri bir RestoreProgress ilk baglamada modul
        // kurulmadan once gelir. Sequential OpenedCount, Random OpenedIndices.
        public int OpenedCount;
        public List<int> OpenedIndices;

        // Kalici kapanan (kacirilan gun) parcalar. Modul bunun HABIT'ten mi
        // geldigini bilmiyor -- yalnizca "bu indeksler bir daha acilmaz, farkli
        // cizilir" komutunu aliyor (bkz. CLAUDE.md > PaintPixels habitten habersiz).
        public List<int> LockedIndices;

        // "Verdigim sprite kilitli parcalarin gorunumunu ZATEN tasiyor." Uygulama
        // yolunda bunu BurnedPaintingArt pisiriyor (soldurma + tarama), o yuzden
        // perde o parcada hicbir sey cizmemeli -- yoksa pisirilen gorunumun
        // ustune biner.
        //
        // Varsayilan false ONEMLI: modulu dogrudan suren bir cagiran
        // (PaintPixelsSandbox, standalone kullanim) bugunku duz lockedColor
        // ortusunu aynen almaya devam ediyor.
        public bool BurnedArtInSprite;

        // RenderTexture'a cizen bir goruntu icin dogru varsayilanlar: safe area
        // anlamsiz, dolgu orani 1 (yoksa tablo RT'nin icinde kucuk kalir).
        public static PaintingSetup ForRenderTexture(
            Sprite sprite,
            OverlayResolution resolution,
            RevealOrder order,
            RevealMode mode,
            Camera camera,
            float targetWorldHeight = 8f)
        {
            return new PaintingSetup
            {
                Sprite = sprite,
                Resolution = resolution,
                Order = order,
                Mode = mode,
                ScreenFillRatio = 1f,
                RespectSafeArea = false,
                TargetWorldHeight = targetWorldHeight,
                Camera = camera
            };
        }
    }

    // Modulu yeni bir tabloya baglar; kurulum ayaktaysa once yikilir (texture ve
    // overlay sprite cozunurluge bagli). Start'tan once cagrilirsa alanlar yazilir.
    public void Configure(in PaintingSetup setup)
    {
        backgroundSprite = setup.Sprite;
        overlayResolution = setup.Resolution;
        revealOrder = setup.Order;
        revealMode = setup.Mode;
        screenFillRatio = setup.ScreenFillRatio;
        respectSafeArea = setup.RespectSafeArea;
        targetWorldHeight = setup.TargetWorldHeight;
        targetCamera = setup.Camera;
        restoreCount = setup.OpenedCount;
        restoreIndices = setup.OpenedIndices;
        restoreLockedIndices = setup.LockedIndices;
        burnedArtInSprite = setup.BurnedArtInSprite;

        cachedCamera = null;

        if (!startCalled)
        {
            return;
        }

        TeardownRuntime();
        BuildRuntime();
    }

    private void Start()
    {
        startCalled = true;
        BuildRuntime();
    }

    private void OnDestroy()
    {
        TeardownRuntime();
    }

    private void Update()
    {
        // Configure beklenirken ApplyFraming null sprite uzerinde patlardi.
        if (!runtimeBuilt)
        {
            return;
        }

        // Kadraj ekrana bagli: cihaz degisimi, ekran donmesi, Game view boyutu.
        if (Screen.width != lastScreenSize.x
            || Screen.height != lastScreenSize.y
            || Screen.safeArea != lastSafeArea)
        {
            ApplyFraming();
        }

        AdvanceFades(Time.deltaTime);
    }

    // Texture yuklemesi kare basina tek sefer: es zamanli fade'ler ayni pixels
    // dizisini paylastigi icin her biri icin Apply() cagirmak bosuna GPU yuklemesi.
    private void LateUpdate()
    {
        if (!pixelsDirty || overlayTexture == null)
        {
            return;
        }

        overlayTexture.SetPixels32(pixels);
        overlayTexture.Apply(false);
        pixelsDirty = false;
    }

    private void BuildRuntime()
    {
        if (backgroundSprite == null)
        {
            Debug.LogWarning("PaintPixels: Background Sprite atanmamis. Inspector'da sprite'i surukleyin ya da Configure ile verin.", this);
            enabled = false;
            return;
        }

        textureSize = (int)overlayResolution;
        rows = textureSize / Columns;

        overlayTexture = CreateOverlayTexture();
        BuildLayout();
        ApplyFraming();

        totalPieces = Columns * rows;
        for (int i = 0; i < totalPieces; i++)
        {
            closedPieces.Add(i);
        }

        runtimeBuilt = true;
        // Sprite'siz acilip sonradan Configure ile tablo verilen modul, yukaridaki
        // erken donusun kapattigi gibi kapali kalirdi.
        enabled = true;

        ApplyRestore();
    }

    // Child objeler bilerek KORUNUYOR: Destroy kare sonuna ertelendigi icin onlari
    // her tablo degisiminde yeniden yaratmak eski tabloyu bir kare fazladan
    // ekranda birakirdi. Configure ve OnDestroy ayni yoldan geciyor ki uretilen
    // texture her degisimde sizmasin.
    private void TeardownRuntime()
    {
        closedPieces.Clear();
        fadingPieces.Clear();
        lockedPieces.Clear();
        openedPieces = 0;
        totalPieces = 0;
        textureSize = 0;
        rows = 0;
        pixels = null;
        pixelsDirty = false;

        // Sprite yok edilecek; referansi birakmazsak bir kare "missing sprite".
        if (overlayRenderer != null)
        {
            overlayRenderer.sprite = null;
        }

        if (overlaySprite != null)
        {
            Destroy(overlaySprite);
            overlaySprite = null;
        }

        if (overlayTexture != null)
        {
            Destroy(overlayTexture);
            overlayTexture = null;
        }

        // Kadraj damgasi sifirlanmali, yoksa yeni kurulumun ApplyFraming'i
        // Update'in "ekran degisti mi" kontrolune kalir.
        lastScreenSize = default;
        lastSafeArea = default;

        runtimeBuilt = false;
    }

    private byte CoveredAlpha => (byte)Mathf.RoundToInt(Mathf.Clamp01(veilStrength) * 255f);

    // Alpha'yi CoveredAlpha'dan aliyor ki iki yerde ayri hesaplanmasin.
    private Color32 CoveredColor => new Color32(
        (byte)Mathf.RoundToInt(Mathf.Clamp01(veilColor.r) * 255f),
        (byte)Mathf.RoundToInt(Mathf.Clamp01(veilColor.g) * 255f),
        (byte)Mathf.RoundToInt(Mathf.Clamp01(veilColor.b) * 255f),
        CoveredAlpha);

    private byte LockedAlpha => (byte)Mathf.RoundToInt(Mathf.Clamp01(lockedStrength) * 255f);

    private Color32 LockedColor => new Color32(
        (byte)Mathf.RoundToInt(Mathf.Clamp01(lockedColor.r) * 255f),
        (byte)Mathf.RoundToInt(Mathf.Clamp01(lockedColor.g) * 255f),
        (byte)Mathf.RoundToInt(Mathf.Clamp01(lockedColor.b) * 255f),
        LockedAlpha);

    // Sequential'da hep ilk eleman: liste artan sirada dolduruldugu ve bastan
    // silmek kalanlarin sirasini bozmadigi icin ayri bir sayac gerekmiyor.
    private int PickClosedListIndex()
    {
        return revealOrder == RevealOrder.Sequential ? 0 : Random.Range(0, closedPieces.Count);
    }

    // Sondan basa geziliyor ki biten parcayi cikarmak kalan index'leri kaydirmasin.
    private void AdvanceFades(float deltaTime)
    {
        for (int i = fadingPieces.Count - 1; i >= 0; i--)
        {
            FadingPiece piece = fadingPieces[i];
            piece.Elapsed += deltaTime;

            float t = Mathf.Clamp01(piece.Elapsed / fadeDuration);
            SetPieceAlpha(piece.PieceIndex, (byte)Mathf.RoundToInt(Mathf.Lerp(piece.StartAlpha, 0f, t)));
            pixelsDirty = true;

            if (t >= 1f)
            {
                fadingPieces.RemoveAt(i);
                MarkPieceOpened(true);
            }
            else
            {
                // Struct: kopyayi geri yazmazsak Elapsed hic ilerlemez.
                fadingPieces[i] = piece;
            }
        }
    }

    // Anlik acmanin tek yolu. Parca hangi listedeyse oradan cikariliyor: solmakta
    // olani fadingPieces'ta birakmak sayaci iki kez artirirdi.
    //
    // notify = false yalnizca kayit yuklemesi icin.
    private bool OpenPieceImmediate(int pieceIndex, bool notify)
    {
        // totalPieces kurulumdan once 0: bu kontrol "pixels dizisi yok"u da eliyor.
        if (pieceIndex < 0 || pieceIndex >= totalPieces)
        {
            return false;
        }

        int listIndex = closedPieces.IndexOf(pieceIndex);
        if (listIndex >= 0)
        {
            closedPieces.RemoveAt(listIndex);
        }
        else if (!TryStopFade(pieceIndex))
        {
            // Ne kapali ne soluyor: zaten acik, sayaci tekrar artirmiyoruz.
            return false;
        }

        SetPieceAlpha(pieceIndex, 0);
        pixelsDirty = true;
        MarkPieceOpened(notify);
        return true;
    }

    private bool TryStopFade(int pieceIndex)
    {
        for (int i = fadingPieces.Count - 1; i >= 0; i--)
        {
            if (fadingPieces[i].PieceIndex == pieceIndex)
            {
                fadingPieces.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    // Bir parcayi KALICI kapatir (kacirilan gun). closedPieces'ten (varsa
    // fading'den) cikarip lockedPieces'e tasir -- bir daha RevealNext/RevealPiece
    // bu indeksi hic gormez. openedPieces'e DOKUNMAZ: kilitli parca acik degil.
    private bool LockPieceImmediate(int pieceIndex)
    {
        if (pieceIndex < 0 || pieceIndex >= totalPieces || ListLookup.Contains(lockedPieces, pieceIndex))
        {
            return false;
        }

        int listIndex = closedPieces.IndexOf(pieceIndex);
        if (listIndex >= 0)
        {
            closedPieces.RemoveAt(listIndex);
        }
        else
        {
            TryStopFade(pieceIndex);
        }

        lockedPieces.Add(pieceIndex);

        if (burnedArtInSprite)
        {
            // Yakilan gorunum tablonun piksellerinde: perde CEKILIYOR, ustune
            // ikinci bir ortu cizmiyor (bkz. PaintingSetup.BurnedArtInSprite).
            SetPieceAlpha(pieceIndex, 0);
        }
        else
        {
            SetPieceColor(pieceIndex, LockedColor);
        }

        pixelsDirty = true;
        return true;
    }

    private void ApplyRestoreIfBuilt()
    {
        if (runtimeBuilt)
        {
            ApplyRestore();
        }
    }

    private void ApplyRestore()
    {
        // Kilitliler ONCE: acik parca indeksleri kilitlilerle hic kesismez
        // (bkz. PaintingProgress), ama sirayi tersine cevirmek ileride bir
        // kesisme olsa bile "kalici kapali" kazansin diye garanti olsun.
        if (restoreLockedIndices != null)
        {
            for (int i = 0; i < restoreLockedIndices.Count; i++)
            {
                LockPieceImmediate(restoreLockedIndices[i]);
            }
        }

        if (restoreIndices != null)
        {
            for (int i = 0; i < restoreIndices.Count; i++)
            {
                OpenPieceImmediate(restoreIndices[i], false);
            }

            return;
        }

        int limit = Mathf.Min(restoreCount, totalPieces);
        for (int i = 0; i < limit; i++)
        {
            OpenPieceImmediate(i, false);
        }
    }

    // Acik + kilitli toplami tuvali yakalayinca TAM sayilir -- kilitli parcalar
    // bir daha acilmayacagi icin openedPieces tek basina asla totalPieces'a
    // ulasmayabilir (bkz. IsFullyRevealed).
    private void MarkPieceOpened(bool notify)
    {
        openedPieces++;
        if (notify && openedPieces + lockedPieces.Count >= totalPieces)
        {
            Completed?.Invoke();
        }
    }

    // Parca 0 UST-SOL kabul edilir; indeks soldan saga, sonra bir satir ASAGIYA
    // artar (bkz. PaintingGrid, ayni konvansiyonun uGUI yarisi). Texture'da y = 0
    // ALTTADIR, bu yuzden parca satirini texture satirina cevirmek gerekiyor --
    // PaintPixels icinde indeksin geometriye donustugu TEK yer burasi.
    //
    // public static: PaintingGrid ile ayni sonucu verdigini sahnesiz test etmek
    // icin (bkz. Tests/HabitUiLogicTests.cs). rowCount sifir/negatif gelmez --
    // cagiranlar pieceIndex'i totalPieces'a karsi sinirlar ve totalPieces sifirken
    // rows da sifirdir.
    public static int TextureRowForPiece(int pieceIndex, int rowCount)
    {
        return rowCount - 1 - pieceIndex / Columns;
    }

    // Bir parcanin texel blogu. TextureRowForPiece ile ayni gerekceyle public
    // static: BurnedPaintingArt yakilan parcayi tablonun KENDI piksellerine
    // pisiriyor ve perdenin yazdigi blokla birebir ayni blogu bulmak zorunda.
    // Naif ters cevirme (col = x * Columns / width) tam bolunmeyen boylarda bir
    // kayiyor (42 px tablo 84 olur, 84 / 8 = 10.5), bu yuzden hesap kopyalanmiyor.
    //
    // Sinirlar uctan uca: bolme tam cikmasa bile parcalar texture'i bosluksuz
    // doser, kenarda hic acilmayan bir texel seridi kalmaz.
    public static void PieceBounds(
        int pieceIndex,
        int width,
        int height,
        int rowCount,
        out int startX,
        out int endX,
        out int startY,
        out int endY)
    {
        int col = pieceIndex % Columns;
        int row = TextureRowForPiece(pieceIndex, rowCount);

        startX = col * width / Columns;
        endX = (col + 1) * width / Columns;
        startY = row * height / rowCount;
        endY = (row + 1) * height / rowCount;
    }

    // Bir parcanin texel blogunu yazar -- PaintPixels icinde indeksin GEOMETRIYE
    // donustugu TEK yer (bkz. TextureRowForPiece, PieceBounds). SetPieceAlpha ve
    // SetPieceColor bunun ince sarmallari; ikisi de buradan gecer ki geometri
    // kopyalanmasin.
    private void WritePiece(int pieceIndex, Color32 color, bool alphaOnly)
    {
        int size = textureSize;
        PieceBounds(pieceIndex, size, size, rows, out int startX, out int endX, out int startY, out int endY);

        for (int y = startY; y < endY; y++)
        {
            int rowOffset = y * size;
            for (int x = startX; x < endX; x++)
            {
                if (alphaOnly)
                {
                    pixels[rowOffset + x].a = color.a;
                }
                else
                {
                    pixels[rowOffset + x] = color;
                }
            }
        }
    }

    // Fade ve ani acma icin: RGB'ye dokunmaz, pixels dizisi hep veilColor ile
    // baslatildigi icin acilirken/sonerken ihtiyac olan tek sey alpha.
    private void SetPieceAlpha(int pieceIndex, byte alpha)
    {
        WritePiece(pieceIndex, new Color32(0, 0, 0, alpha), true);
    }

    // Kilitli parca icin: RGB DAHIL yazilir, veil'den farkli bir ton gorunmeli.
    private void SetPieceColor(int pieceIndex, Color32 color)
    {
        WritePiece(pieceIndex, color, false);
    }

    private Texture2D CreateOverlayTexture()
    {
        int size = textureSize;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        pixels = new Color32[size * size];
        Color32 fill = CoveredColor;
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = fill;
        }

        // Apply(false): mipmap uretme.
        texture.SetPixels32(pixels);
        texture.Apply(false);

        return texture;
    }

    // Arka plan ve perde ayri child'lara aliniyor ki perde bagimsiz olceklenebilsin:
    // hangi boyutta / pixelsPerUnit / en-boy oraninda gorsel gelirse gelsin orter.
    private void BuildLayout()
    {
        Bounds bgBounds = backgroundSprite.bounds;
        Vector2 bgSize = bgBounds.size;

        if (backgroundRenderer == null)
        {
            backgroundRenderer = CreateChildRenderer("Background", 0);
        }
        backgroundRenderer.sprite = backgroundSprite;

        if (overlayRenderer == null)
        {
            overlayRenderer = CreateChildRenderer("Overlay", 1);
        }

        overlaySprite = Sprite.Create(
            overlayTexture,
            new Rect(0, 0, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            textureSize);
        overlayRenderer.sprite = overlaySprite;

        // Perde 1x1 birim uretildi; arka planin boyutuna gerdiriliyor. bounds.center
        // pivot merkezde degilse olusan ofseti telafi eder.
        overlayRenderer.transform.localPosition = bgBounds.center;
        overlayRenderer.transform.localScale = new Vector3(bgSize.x, bgSize.y, 1f);

        // Modul bir havuza alinip baska layer'a tasinmis olabilir.
        ApplyLayerToChildren();
    }

    private SpriteRenderer CreateChildRenderer(string childName, int sortingOrder)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = sortingOrder;
        // URP 2D varsayilani Sprite-Lit-Default: off-screen goruntu kameralarinda
        // Light2D olmadigi icin her sey siyah render olurdu. Unlit sart.
        renderer.sharedMaterial = GetUnlitSpriteMaterial();
        return renderer;
    }

    // Tum modul kopyalari arasinda paylasiliyor.
    private static Material cachedUnlitSpriteMaterial;

    private static Material GetUnlitSpriteMaterial()
    {
        if (cachedUnlitSpriteMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
            {
                // Paket surumu farkliysa legacy sprite shader'ina dus; 2D Renderer
                // onu da unlit olarak destekliyor.
                shader = Shader.Find("Sprites/Default");
            }

            cachedUnlitSpriteMaterial = new Material(shader);
        }

        return cachedUnlitSpriteMaterial;
    }

    // targetWorldHeight ust sinir oldugu icin genis ekranda sonuc degismez; dar
    // (portrait) ekranda genislik baglayici olur ve kenarlar disarida kalmaz.
    private void ApplyFraming()
    {
        Camera cam = GetCamera();
        if (cam == null)
        {
            return;
        }

        Vector2 bgSize = backgroundSprite.bounds.size;
        if (bgSize.x <= 0f || bgSize.y <= 0f)
        {
            return;
        }

        Vector2 view = GetVisibleWorldSize(cam) * Mathf.Clamp01(screenFillRatio);

        float scale = Mathf.Min(
            targetWorldHeight / bgSize.y,
            view.y / bgSize.y,
            view.x / bgSize.x);

        transform.localScale = Vector3.one * scale;

        // Damga ancak olcek UYGULANDIKTAN sonra basiliyor: erken donuslerde de
        // basmak, kamera bir kare gec olustugunda modulu kalici olarak olceksiz
        // birakiyordu (Update degisiklik goremedigi icin bir daha denemiyordu).
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        lastSafeArea = Screen.safeArea;
    }

    // Perspektifte gorselin durdugu z duzlemi esas aliniyor.
    private Vector2 GetVisibleWorldSize(Camera cam)
    {
        float halfHeight;
        if (cam.orthographic)
        {
            halfHeight = cam.orthographicSize;
        }
        else
        {
            float distance = Mathf.Abs(transform.position.z - cam.transform.position.z);
            halfHeight = distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }

        Vector2 size = new Vector2(halfHeight * 2f * cam.aspect, halfHeight * 2f);

        // Modul kendi konumunu degistirmiyor, sadece daha kucuk bir kutuya siginiyor;
        // sahnedeki yerlesim ana projenin isi.
        if (respectSafeArea && Screen.width > 0 && Screen.height > 0)
        {
            Rect safeArea = Screen.safeArea;
            size.x *= safeArea.width / Screen.width;
            size.y *= safeArea.height / Screen.height;
        }

        return size;
    }

    private Camera GetCamera()
    {
        if (targetCamera != null)
        {
            return targetCamera;
        }

        if (cachedCamera == null)
        {
            cachedCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        }

        return cachedCamera;
    }
}
