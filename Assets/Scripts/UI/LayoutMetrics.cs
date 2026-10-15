using UnityEngine;

// Ekran boyutunu tasarim birimine ceviren TEK kural.
//
// Teslim dosyasi 366x820. Canvas Scaler eskiden yalnizca GENISLIGE uyuyordu
// (match = 0), yani kanvas her cihazda tam 366 birim genis ve "366 / en-boy"
// kadar yuksekti. Telefonda bu iyi calisiyor (en-boy ~0.46, kanvas ~792 birim)
// ama tablette (4:3, en-boy 0.75) kanvas 488 birime dusuyor: studyo seridi
// dikey butcenin tamamini yiyor, izgaraya yarim kutu satiri bile kalmiyor ve
// 366 birim 10 inclik bir genislige yayildigi icin her sey devasa gorunuyor.
//
// Buradaki kural bunu bitiriyor:
//
//     olcek = min(ekranGenisligi / 366, ekranYuksekligi / 650)
//
// Bu tam olarak Unity'nin CanvasScaler.ScreenMatchMode.Expand modu (iki
// olcegin kucugu), yani calisma aninda olcek kodu yazmiyoruz -- referans
// cozunurlugu (366, 650) yapip modu Expand'e almak yetiyor. CanvasScalerDriver
// bu dosyadaki sabitleri scaler'a yaziyor.
//
// Esik en-boy orani 366 / 650 = 0.56308:
//   - en-boy <= 0.56308 (16:9 = 0.5625 dahil HER telefon) min genisligi
//     seciyor -> kanvas tam 366 birim -> eski davranisin birebir aynisi.
//     Telefonlarda sifir degisiklik.
//   - en-boy > 0.56308 (tabletler, kare/yatay pencereler) min yuksekligi
//     seciyor -> kanvas 366'dan GENIS olur. Fazla genislik iki yana bosluk
//     olarak dagiliyor, icerik 366 birimlik kolonda ortalaniyor.
//
// Boylece her en-boy oraninda ayni dogrulanmis kompozisyon ciziliyor ve
// yatay teslim sayilarinin hepsi birebir gecerli kaliyor.
//
// Saf mantik: icinde Screen.* yok, degerler disaridan geliyor. Tests/
// ResponsiveLayoutTests bir cihaz matrisi uzerinde bu dosyayi dogruluyor.
public static class LayoutMetrics
{
    // Icerik kolonunun genisligi (tasarim pikseli). Teslim dosyasinin icerik
    // alani; her yatay ofset bu genislige gore yazilmis.
    public const float DesignWidth = 366f;

    // Kanvas bundan kisa olamaz. 650, 16:9 bir telefonun bugun zaten aldigi
    // yukseklige (650.7) bir tik altta duruyor: esik boylece 16:9'un hemen
    // asagisina dusuyor ve 16:9 ya da daha uzun HER telefon eski davranisinda
    // kaliyor. Yalnizca daha bodur ekranlar kolona geciyor. Degeri buyutmek
    // (orn. 651) esigi 16:9'un ustune cikarir ve o telefonlari da kolona sokar.
    public const float MinDesignHeight = 650f;

    // Kolona gecisin basladigi en-boy orani (genislik / yukseklik).
    public const float ColumnAspect = DesignWidth / MinDesignHeight;

    // Canvas Scaler'in uretecegi olcek: cihaz pikseli / tasarim pikseli.
    public static float ScaleFactor(float screenWidth, float screenHeight)
    {
        if (screenWidth <= 0f || screenHeight <= 0f)
        {
            return 1f;
        }

        return Mathf.Min(screenWidth / DesignWidth, screenHeight / MinDesignHeight);
    }

    // Kanvasin tasarim birimi cinsinden genisligi. Telefonda tam 366.
    public static float CanvasWidth(float screenWidth, float screenHeight)
    {
        float scale = ScaleFactor(screenWidth, screenHeight);
        return scale <= 0f ? DesignWidth : screenWidth / scale;
    }

    // Kanvasin tasarim birimi cinsinden yuksekligi. Her zaman >= 650.
    public static float CanvasHeight(float screenWidth, float screenHeight)
    {
        float scale = ScaleFactor(screenWidth, screenHeight);
        return scale <= 0f ? MinDesignHeight : screenHeight / scale;
    }

    // Icerigin kaplayacagi genislik: kanvas dardan genise gitse de 366'yi
    // asmiyor. Kanvas 366'dan darsa (teorik) kolon kanvasa esitleniyor.
    public static float ContentWidth(float canvasWidth)
    {
        return Mathf.Min(canvasWidth, DesignWidth);
    }

    // Kolonun bir yanindaki bosluk (tasarim pikseli). Telefonda 0.
    public static float SideMargin(float canvasWidth)
    {
        return Mathf.Max(0f, (canvasWidth - ContentWidth(canvasWidth)) * 0.5f);
    }
}
