using System;
using UnityEngine;

// Ressamin mood puanini tutan, sahneler arasi yasayan tekil yonetici.
//
// Puan burada, gorsel karsiligi Painter'da. Painter sahneye ait oldugu icin
// sahne degisiminde yok olur, bu yonetici olmez: referans her sahne acilisinda
// RegisterPainter ile tazelenir.
public class PainterManager : MonoBehaviour
{
    public enum PainterMoods
    {
        UltraRich,
        Rich,
        Happy,
        Normal,
        Sad,
        Starve
    }

    public static PainterManager Instance { get; private set; }

    // Puan -> mod tablosu. Index puanin kendisi: MoodTable[0] en dusuk, son
    // eleman en yuksek puanin modu.
    //
    // Sozluk yerine dizi: sinirlar tablonun uzunlugundan turetildigi icin
    // (asagida) tablo ile sinirlarin birbirinden ayrismasi imkansiz. Sozluk
    // oldugu surumde MaxMoodPoint elle senkron tutuluyordu ve sinir buyutulse
    // PickMood calisma aninda KeyNotFoundException atardi.
    private static readonly PainterMoods[] MoodTable =
    {
        PainterMoods.Starve,    // 0
        PainterMoods.Starve,    // 1
        PainterMoods.Sad,       // 2
        PainterMoods.Sad,       // 3
        PainterMoods.Normal,    // 4
        PainterMoods.Normal,    // 5
        PainterMoods.Normal,    // 6
        PainterMoods.Happy,     // 7
        PainterMoods.Happy,     // 8
        PainterMoods.Rich,      // 9
        PainterMoods.Rich,      // 10
        PainterMoods.UltraRich, // 11
        PainterMoods.UltraRich  // 12
    };

    // Kayit katmani (PlayerSaveData) varsayilani buradan okuyor, boylece deger
    // iki ayri yerde tutulup birbirinden ayrismiyor.
    public const int MinMoodPoint = 0;
    public const int DefaultMoodPoint = 3;
    public static readonly int MaxMoodPoint = MoodTable.Length - 1;

    private Painter painter;

    // Habit tarafindan istenen ama painter sahnede olmadigi icin ertelenen boyama.
    private bool pendingPainting;

    private PainterMoods paintersMood;
    private int moodPoint = DefaultMoodPoint;

    public int MoodPoint => moodPoint;
    public PainterMoods CurrentMood => paintersMood;

    // Puan degisti. UI (mood pill) her kare singleton yoklamasin diye.
    //
    // DIKKAT: HabbitManager.Start() RestoreMoodPoint'i cagirdiginda UI henuz
    // abone olmamis olabilir -- dinleyen taraf OnEnable'da MoodPoint'i bir kez
    // KENDISI okumali, yalnizca olaya guvenmemeli.
    public event Action<int> MoodChanged;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        PickMood();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Painter sahne ile birlikte yok olup yeniden olustugu icin referans her
    // sahne acilisinda tazelenir. Mod ve bekleyen boyama tam bu anda uygulanir.
    public void RegisterPainter(Painter newPainter)
    {
        painter = newPainter;
        painter.SwitchMood(paintersMood);

        if (pendingPainting)
        {
            pendingPainting = false;
            painter.MakePainting();
        }
    }

    // Mood puanini delta kadar degistirir: +1 gunun odulu, -1 gun sonu cezasi.
    // Kapanan birden fazla gunun cezasi TEK cagrida gonderilmeli -- her adim
    // icin ayri cagirmak animator controller'i gereksiz yere ust uste degistirir.
    public void ChangeMoodPoint(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        moodPoint = Mathf.Clamp(moodPoint + delta, MinMoodPoint, MaxMoodPoint);
        PickMood();
        ApplyMoodToPainter();
        MoodChanged?.Invoke(moodPoint);

        // Yukselis kutlanir: boyama animasyonu yalnizca odulde oynuyor.
        if (delta > 0)
        {
            RequestPainting();
        }
    }

    // Kayittan donus: puani oldugu gibi kabul eder, animasyon oynatmaz.
    public void RestoreMoodPoint(int value)
    {
        moodPoint = Mathf.Clamp(value, MinMoodPoint, MaxMoodPoint);
        PickMood();
        ApplyMoodToPainter();
        MoodChanged?.Invoke(moodPoint);
    }

    // Puanin mod karsiligi. Statik ve sahnesiz: hem asagidaki PickMood hem de
    // testler ayni tablodan okusun -- tablonun ikinci bir kopyasi cikmasin.
    public static PainterMoods MoodForPoint(int point)
    {
        return MoodTable[Mathf.Clamp(point, MinMoodPoint, MaxMoodPoint)];
    }

    private void PickMood()
    {
        paintersMood = MoodForPoint(moodPoint);
    }

    // Painter sahnede olmayabilir (habit sahnesindeyiz); puan yine de guncellenir,
    // gorsel karsiligi sahneye donunce RegisterPainter ile uygulanir.
    private void ApplyMoodToPainter()
    {
        if (painter != null)
        {
            painter.SwitchMood(paintersMood);
        }
    }

    // Boyama animasyonunu oynatir. Painter sahnede yoksa animasyon kimse gormeden
    // bitecegi icin erteler; sahneye donulunce RegisterPainter oynatir.
    private void RequestPainting()
    {
        if (painter == null)
        {
            pendingPainting = true;
            return;
        }

        painter.MakePainting();
    }
}
