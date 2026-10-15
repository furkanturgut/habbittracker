using NUnit.Framework;

// Ekran uyumunun cihaz matrisi.
//
// Bu dosyanin varlik sebebi: gorsel bir yerlesim degisikligi telefonda dogru
// gorunup tablette patlayabiliyordu ve bunu ancak cihaz cihaz denemekle fark
// ediyorduk. Buradaki degismezler o donguyu bitiriyor -- yerlesim sabitlerine
// dokunan her degisiklik once burada dusuyor.
//
// Saf mantik (LayoutMetrics + WorkshopBands) test ediliyor: sahne, Canvas ya da
// PlayMode gerekmiyor.
public class ResponsiveLayoutTests
{
    // Bir cihazin testte ihtiyac duydugu her sey. Guvenli alan insetleri CIHAZ
    // PIKSELI cinsinden, tipki Screen.safeArea gibi.
    private readonly struct Device
    {
        public readonly string Name;
        public readonly int Width;
        public readonly int Height;
        public readonly float SafeTopPixels;
        public readonly float SafeBottomPixels;

        public Device(string name, int width, int height, float safeTopPixels, float safeBottomPixels)
        {
            Name = name;
            Width = width;
            Height = height;
            SafeTopPixels = safeTopPixels;
            SafeBottomPixels = safeBottomPixels;
        }

        public float Aspect => (float)Width / Height;

        public override string ToString()
        {
            return Name + " (" + Width + "x" + Height + ")";
        }
    }

    // Telefonlar: en-boy orani LayoutMetrics.ColumnAspect degerinin ALTINDA,
    // yani genislige uyum devam ediyor ve davranis degismiyor.
    private static readonly Device[] Phones =
    {
        new Device("iPhone SE 16:9", 750, 1334, 40f, 0f),
        new Device("iPhone 14", 1170, 2532, 141f, 102f),
        new Device("iPhone 15 Pro Max", 1290, 2796, 147f, 102f),
        new Device("Pixel 7", 1080, 2400, 108f, 72f),
        new Device("Uzun 21:9", 1080, 2520, 100f, 60f),
    };

    // Tabletler ve bodur pencereler: kolona geciyorlar.
    private static readonly Device[] WideScreens =
    {
        new Device("iPad 9 (4:3)", 1536, 2048, 40f, 0f),
        new Device("iPad Air 10.9", 1640, 2360, 48f, 40f),
        new Device("iPad Pro 12.9", 2048, 2732, 48f, 40f),
        new Device("Galaxy Tab 16:10", 1600, 2560, 0f, 0f),
        new Device("Kare pencere", 1000, 1000, 0f, 0f),
        new Device("Yatay 4:3", 2048, 1536, 0f, 0f),
        new Device("Android asgari pencere", 400, 300, 0f, 0f),
    };

    private static Device[] AllDevices
    {
        get
        {
            Device[] all = new Device[Phones.Length + WideScreens.Length];
            Phones.CopyTo(all, 0);
            WideScreens.CopyTo(all, Phones.Length);
            return all;
        }
    }

    // -----------------------------------------------------------------------
    // LayoutMetrics: kanvas hicbir eksende tasarimin altina dusmuyor
    // -----------------------------------------------------------------------

    [Test]
    public void EveryDevice_CanvasNeverSmallerThanDesign()
    {
        foreach (Device device in AllDevices)
        {
            float width = LayoutMetrics.CanvasWidth(device.Width, device.Height);
            float height = LayoutMetrics.CanvasHeight(device.Width, device.Height);

            Assert.GreaterOrEqual(
                width, LayoutMetrics.DesignWidth - 0.01f,
                device + ": kanvas genisligi tasarimin altinda.");
            Assert.GreaterOrEqual(
                height, LayoutMetrics.MinDesignHeight - 0.01f,
                device + ": kanvas yuksekligi asgari tasarimin altinda.");
        }
    }

    [Test]
    public void EveryDevice_ContentColumnIsExactlyDesignWidth()
    {
        foreach (Device device in AllDevices)
        {
            float canvasWidth = LayoutMetrics.CanvasWidth(device.Width, device.Height);

            Assert.AreEqual(
                LayoutMetrics.DesignWidth,
                LayoutMetrics.ContentWidth(canvasWidth),
                0.01f,
                device + ": icerik kolonu 366 birim degil.");
            Assert.GreaterOrEqual(
                LayoutMetrics.SideMargin(canvasWidth), 0f,
                device + ": yan bosluk negatif.");
        }
    }

    // Kolon her zaman ortali: iki yan bosluk ve kolon toplami kanvasa esit.
    [Test]
    public void EveryDevice_ColumnIsCentred()
    {
        foreach (Device device in AllDevices)
        {
            float canvasWidth = LayoutMetrics.CanvasWidth(device.Width, device.Height);
            float margin = LayoutMetrics.SideMargin(canvasWidth);

            Assert.AreEqual(
                canvasWidth,
                margin + LayoutMetrics.ContentWidth(canvasWidth) + margin,
                0.01f,
                device + ": kolon ortalanmamis.");
        }
    }

    // -----------------------------------------------------------------------
    // Regresyon kilidi: telefonlarda hicbir sey degismiyor
    // -----------------------------------------------------------------------

    [Test]
    public void Phones_KeepPureWidthMatch()
    {
        foreach (Device device in Phones)
        {
            Assert.Less(
                device.Aspect, LayoutMetrics.ColumnAspect,
                device + ": telefon sayilmasi icin en-boy orani esigin altinda olmali.");

            Assert.AreEqual(
                device.Width / LayoutMetrics.DesignWidth,
                LayoutMetrics.ScaleFactor(device.Width, device.Height),
                0.0001f,
                device + ": olcek artik genislige uymuyor -- telefon davranisi degisti.");

            Assert.AreEqual(
                LayoutMetrics.DesignWidth,
                LayoutMetrics.CanvasWidth(device.Width, device.Height),
                0.01f,
                device + ": kanvas genisligi 366 birim degil.");

            Assert.AreEqual(
                0f,
                LayoutMetrics.SideMargin(LayoutMetrics.CanvasWidth(device.Width, device.Height)),
                0.01f,
                device + ": telefonda yan bosluk olusmamali.");
        }
    }

    // Eski davranisin birebir kopyasi: kanvas yuksekligi 366 / en-boy.
    [Test]
    public void Phones_CanvasHeightMatchesLegacyFormula()
    {
        foreach (Device device in Phones)
        {
            Assert.AreEqual(
                LayoutMetrics.DesignWidth / device.Aspect,
                LayoutMetrics.CanvasHeight(device.Width, device.Height),
                0.05f,
                device + ": kanvas yuksekligi eski formulden sapti.");
        }
    }

    [Test]
    public void WideScreens_SwitchToColumn()
    {
        foreach (Device device in WideScreens)
        {
            Assert.Greater(
                device.Aspect, LayoutMetrics.ColumnAspect,
                device + ": genis ekran sayilmasi icin en-boy orani esigin ustunde olmali.");

            Assert.Greater(
                LayoutMetrics.SideMargin(LayoutMetrics.CanvasWidth(device.Width, device.Height)),
                0f,
                device + ": genis ekranda yan bosluk olusmali.");

            Assert.AreEqual(
                LayoutMetrics.MinDesignHeight,
                LayoutMetrics.CanvasHeight(device.Width, device.Height),
                0.01f,
                device + ": genis ekranda kanvas yuksekligi 650 birim olmali.");
        }
    }

    // -----------------------------------------------------------------------
    // WorkshopBands: izgara hicbir cihazda aclik cekmiyor
    // -----------------------------------------------------------------------

    [Test]
    public void EveryDevice_GridKeepsItsMinimumHeight()
    {
        foreach (Device device in AllDevices)
        {
            WorkshopBands.Result bands = SolveFor(device);

            Assert.GreaterOrEqual(
                bands.GridHeight,
                WorkshopBands.MinGridHeight - 0.01f,
                device + ": izgaraya birakilan yukseklik asgarinin altinda.");
        }
    }

    [Test]
    public void EveryDevice_ShiftStaysWithinBounds()
    {
        foreach (Device device in AllDevices)
        {
            WorkshopBands.Result bands = SolveFor(device);

            Assert.GreaterOrEqual(bands.ShiftUp, 0f, device + ": kaydirma negatif.");
            Assert.LessOrEqual(
                bands.ShiftUp,
                WorkshopBands.SceneHeight - WorkshopBands.MinSceneHeight + 0.01f,
                device + ": kaydirma ressamin kafasini basligin altina sokuyor.");
        }
    }

    // Faz 3'ten once bu deger HER cihazda 0'a sabitti (46 birimlik tek satirli
    // baslik, ressamin kafasiyla hic yarismiyordu). Baslik 82'ye cikinca
    // (TitleBlock + MoodRow) modern telefonlarda yine 0 kaliyor ama SE ve
    // bodur/tabletlerde studyo ~60 birim kirpiliyor -- bu artik KABUL EDILEN
    // bir mekanizma (bkz. WorkshopBands dosya basi yorumu), gercek degismez
    // "ressamin kafasi basligin altina giriyor mu" sorusu ve onu
    // Header_PainterHeadClearsHeader soruyor.
    private static float PainterHeaderClearance(Device device, WorkshopBands.Result bands)
    {
        float headerBottomScreenDepth = bands.HeaderTop + WorkshopBands.HeaderHeight + SafeTopUnits(device);
        float painterHeadScreenDepth = WorkshopBands.PainterTopDesign - bands.ShiftUp;
        return painterHeadScreenDepth - headerBottomScreenDepth;
    }

    [Test]
    public void Header_PainterHeadClearsHeader()
    {
        foreach (Device device in AllDevices)
        {
            float clearance = PainterHeaderClearance(device, SolveFor(device));

            Assert.GreaterOrEqual(
                clearance,
                WorkshopBands.PainterHeaderClearance - 0.01f,
                device + ": ressamin kafasi basligin icine giriyor (aciklik " + clearance + ").");
        }
    }

    // HeaderHeight buyurse (yeni bir satir, buyuyen font...) MinSceneHeight
    // onu otomatik takip etsin diye TURETILMIS -- bu test o bagi pinler.
    [Test]
    public void MinSceneHeight_IsDerivedFromHeaderHeight()
    {
        float expected = WorkshopBands.SceneHeight - WorkshopBands.PainterTopDesign
            + WorkshopBands.HeaderTop + WorkshopBands.HeaderHeight + WorkshopBands.PainterHeaderClearance;

        Assert.AreEqual(
            expected, WorkshopBands.MinSceneHeight, 0.01f,
            "MinSceneHeight, HeaderHeight'ten turetilme formulunden sapti.");
    }

    [Test]
    public void EveryDevice_BandsAreOrderedAndPositive()
    {
        foreach (Device device in AllDevices)
        {
            WorkshopBands.Result bands = SolveFor(device);

            Assert.GreaterOrEqual(
                bands.HeaderTop, WorkshopBands.HeaderMinTop - 0.01f,
                device + ": baslik ekranin disina cikti.");
            Assert.Greater(
                bands.FilterTop, bands.HeaderTop + WorkshopBands.HeaderHeight,
                device + ": filtre satiri basligin altina inmiyor.");
            Assert.AreEqual(
                bands.SceneBottom + WorkshopBands.SkirtHeight, bands.GridTop, 0.01f,
                device + ": izgara serit bitisine oturmuyor.");
            Assert.AreEqual(
                bands.FilterTop + WorkshopBands.FilterHeight, bands.GridTop, 0.01f,
                device + ": filtre satiri izgara ustunde bitmiyor.");
        }
    }

    // Filtre satiri her ekranda studyonun ayni seridini (394-452) ortmeli;
    // WorkshopBands.ShiftUp tam da bunun icin var.
    [Test]
    public void EveryDevice_FilterRowCoversTheSameStudioStrip()
    {
        foreach (Device device in AllDevices)
        {
            WorkshopBands.Result bands = SolveFor(device);

            // Filtre satirinin panel koordinatindaki ustunu studyo
            // koordinatina ceviriyoruz: safeTop ile ekran derinligine, ShiftUp
            // ile de studyonun kendi satir numarasina.
            float filterTopInStudio = bands.FilterTop + SafeTopUnits(device) + bands.ShiftUp;

            Assert.AreEqual(
                WorkshopBands.SceneHeight - WorkshopBands.FilterOverlap,
                filterTopInStudio,
                0.01f,
                device + ": filtre satiri studyonun farkli bir seridini ortuyor.");
        }
    }

    // -----------------------------------------------------------------------
    // BottomNavBands: alt navigasyon cubugu hicbir cihazda daralmiyor
    // -----------------------------------------------------------------------

    [Test]
    public void EveryDevice_BarHeightNeverShrinksBelowContent()
    {
        foreach (Device device in AllDevices)
        {
            float safeBottom = device.SafeBottomPixels / LayoutMetrics.ScaleFactor(device.Width, device.Height);

            Assert.GreaterOrEqual(
                BottomNavBands.BarHeight(safeBottom),
                BottomNavBands.ContentHeight - 0.01f,
                device + ": cubuk icerigi sigdiramiyor.");
        }
    }

    // Ekranin dibinden olculen GORUNUR yukseklik (cubuk + guvenli alan payi)
    // hicbir cihazda tasarim yuksekliginin altina inmiyor -- guvenli alan
    // payi DesignBottomPadding'i karsilamaya yetmedigi durumlarda cubugun
    // kendisi farki tamamliyor.
    [Test]
    public void EveryDevice_VisibleBarHeightNeverBelowDesign()
    {
        foreach (Device device in AllDevices)
        {
            float safeBottom = device.SafeBottomPixels / LayoutMetrics.ScaleFactor(device.Width, device.Height);
            float visible = BottomNavBands.BarHeight(safeBottom) + safeBottom;

            Assert.GreaterOrEqual(
                visible,
                BottomNavBands.DesignHeight - 0.01f,
                device + ": gorunur cubuk yuksekligi tasarimin altinda.");
        }
    }

    [Test]
    public void TabWidth_FitsHabitsLabelAtDesignWidth()
    {
        float width = BottomNavBands.TabWidth(LayoutMetrics.DesignWidth);

        Assert.Greater(width, 0f, "Sekme genisligi sifir ya da negatif.");
        Assert.GreaterOrEqual(width, 70f, "\"Aliskanliklar\" etiketinin kaba genisligine (~70) yetmiyor.");
    }

    // Cubuk + izgara + serit + sahne toplami 'available'i asmiyor: bantlar
    // ust uste binmiyor. WorkshopBands.Solve'un kendi ic ozdesligi (GridHeight
    // = available - bottomReserve - gridTop) burada cihaz matrisi uzerinden
    // dogrulaniyor.
    [Test]
    public void EveryDevice_NavBarAndGridNeverOverlapAvailable()
    {
        foreach (Device device in AllDevices)
        {
            float scale = LayoutMetrics.ScaleFactor(device.Width, device.Height);
            float canvasHeight = LayoutMetrics.CanvasHeight(device.Width, device.Height);
            float safeTop = device.SafeTopPixels / scale;
            float safeBottom = device.SafeBottomPixels / scale;
            float available = canvasHeight - safeTop - safeBottom;
            float bottomReserve = BottomNavBands.BarHeight(safeBottom);

            WorkshopBands.Result bands = WorkshopBands.Solve(available, safeTop, bottomReserve);

            Assert.LessOrEqual(
                bands.GridTop + bands.GridHeight + bottomReserve,
                available + 0.01f,
                device + ": cubuk + izgara 'available'i asiyor.");
        }
    }

    // -----------------------------------------------------------------------
    // Mod gostergesi: basligin icinde, iki kose rozetinin arasinda
    // -----------------------------------------------------------------------

    // Satir basligin cocugu, yani dikey yeri basliktan geliyor ve ayri bir
    // cihaz hesabi gerektirmiyor. Kritik olan YATAY: Faz 3'ten SONRA satir
    // kose rozetlerinin ARASINA degil basligin TAMAMINA sigmali (kendi
    // bandinda, rozetler ustteki satirda) -- isim bunu yansitiyor.
    [Test]
    public void MoodRow_FitsInsideHeaderWidth()
    {
        float available = WorkshopBands.MoodRowAvailableWidth(LayoutMetrics.DesignWidth);
        float width = WorkshopBands.MoodRowWidth(MoodScale.Count);

        Assert.Greater(available, 0f, "Baslik genisligi sifir ya da negatif.");
        Assert.LessOrEqual(width, available,
            "Mod satiri basligin genisligine sigmiyor (" + width + " > " + available + ").");
    }

    // Daireler basligin dikey bandina da sigmali.
    [Test]
    public void MoodRow_FitsInsideHeaderHeight()
    {
        Assert.LessOrEqual(WorkshopBands.MoodCircleSize, WorkshopBands.HeaderHeight,
            "Daire basliktan uzun.");
    }

    // Gosterge bilgi, kose rozetleri kontrol: daireler rozetlerden buyuk
    // olmamali, yoksa goz once onlara gider.
    [Test]
    public void MoodRow_CirclesAreNotLargerThanBadges()
    {
        Assert.LessOrEqual(WorkshopBands.MoodCircleSize, WorkshopBands.HeaderBadgeSize,
            "Mod daireleri kose rozetlerinden buyuk olmamali.");
    }

    // Kolon daralsa bile (teorik dar kanvas) satir tasmamali -- bu test bir
    // gun DesignWidth ya da rozet olcusu degisirse otomatik uyarir.
    [Test]
    public void MoodRow_LeavesSlackOnEachSide()
    {
        float available = WorkshopBands.MoodRowAvailableWidth(LayoutMetrics.DesignWidth);
        float slack = (available - WorkshopBands.MoodRowWidth(MoodScale.Count)) * 0.5f;

        Assert.GreaterOrEqual(slack, 0f,
            "Satir ile rozetler arasinda pay kalmiyor.");
    }

    // -----------------------------------------------------------------------
    // MaxHeightFitter: alt sayfanin yuksekligi hicbir cihazda tasip ust
    // bari (Vazgec + baslik) ekran disina itmiyor, ve cozum idempotent --
    // StackOverflowException'a yol acan ozyinelemeyi tam da bu idempotentlik
    // kirildiginda yasadik (bkz. MaxHeightFitter.cs yorumu).
    // -----------------------------------------------------------------------

    // HabitsPanelBuilder.BuildSheet ile ayni degerler (SheetHeight/SheetTopMargin).
    private const float SheetDesignHeight = 700f;
    private const float SheetTopMargin = 40f;

    [Test]
    public void SheetHeight_NeverExceedsDesign()
    {
        foreach (Device device in AllDevices)
        {
            float canvasHeight = LayoutMetrics.CanvasHeight(device.Width, device.Height);
            float height = MaxHeightFitter.SolveHeight(canvasHeight, SheetTopMargin, SheetDesignHeight);

            Assert.LessOrEqual(
                height, SheetDesignHeight + 0.01f,
                device + ": alt sayfa tasarim yuksekligini asiyor.");
        }
    }

    [Test]
    public void SheetHeight_LeavesTopMargin()
    {
        foreach (Device device in AllDevices)
        {
            float canvasHeight = LayoutMetrics.CanvasHeight(device.Width, device.Height);
            float height = MaxHeightFitter.SolveHeight(canvasHeight, SheetTopMargin, SheetDesignHeight);

            Assert.LessOrEqual(
                height, canvasHeight - SheetTopMargin + 0.01f,
                device + ": alt sayfanin ust bari (Vazgec + baslik) ekran disina cikiyor.");
        }
    }

    // Cokmenin ozu buydu: Apply() kendi yazdigi sonucu tekrar girdi olarak
    // alinca farkli bir deger uretmemeli, yoksa OnRectTransformDimensionsChange
    // zinciri hic bitmez.
    [Test]
    public void SheetHeight_IsIdempotent()
    {
        foreach (Device device in AllDevices)
        {
            float canvasHeight = LayoutMetrics.CanvasHeight(device.Width, device.Height);
            float first = MaxHeightFitter.SolveHeight(canvasHeight, SheetTopMargin, SheetDesignHeight);
            if (first < 0f)
            {
                continue;
            }

            // Ikinci cagri: rect'in kendi yuksekligi artik "first" -- ebeveyn
            // degismedigi icin SolveHeight ebeveynden hesapliyor, rect'ten
            // degil, bu yuzden girdi aslinda hep ayni. Asil garanti asagidaki:
            // ayni ebeveyn yuksekligiyle tekrar cagrilinca ayni sonuc cikmali.
            float second = MaxHeightFitter.SolveHeight(canvasHeight, SheetTopMargin, SheetDesignHeight);

            Assert.AreEqual(first, second, 0.0001f,
                device + ": SolveHeight idempotent degil -- ozyineleme riski.");
        }
    }

    [Test]
    public void SheetHeight_ReturnsSkipWhenParentUnmeasured()
    {
        Assert.AreEqual(-1f, MaxHeightFitter.SolveHeight(SheetTopMargin, SheetTopMargin, SheetDesignHeight),
            "Ebeveyn yuksekligi topMargin'e esitken yapacak is yok -- -1 donmeli.");
        Assert.AreEqual(-1f, MaxHeightFitter.SolveHeight(0f, SheetTopMargin, SheetDesignHeight),
            "Ebeveyn hic olculmemisken (yukseklik 0) -1 donmeli.");
    }

    // -----------------------------------------------------------------------
    // Yardimcilar
    // -----------------------------------------------------------------------

    private static float SafeTopUnits(Device device)
    {
        return device.SafeTopPixels / LayoutMetrics.ScaleFactor(device.Width, device.Height);
    }

    private static WorkshopBands.Result SolveFor(Device device)
    {
        float scale = LayoutMetrics.ScaleFactor(device.Width, device.Height);
        float canvasHeight = LayoutMetrics.CanvasHeight(device.Width, device.Height);

        float safeTop = device.SafeTopPixels / scale;
        float safeBottom = device.SafeBottomPixels / scale;

        // Panel = guvenli alan; WorkshopLayout icindeki "available" degeri bu.
        float available = canvasHeight - safeTop - safeBottom;

        float bottomReserve = BottomNavBands.BarHeight(safeBottom);

        return WorkshopBands.Solve(available, safeTop, bottomReserve);
    }
}
