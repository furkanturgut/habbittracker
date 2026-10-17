using System;

// Uygulamanin "simdi"si. Uretimde DateTime.Now ile birebir ayni; gelistirici
// araclari (bkz. Scripts/Dev) gun kaydirip tarih senaryolarini deneyebilsin diye
// okuma noktasi tek bir kapiya toplandi.
//
// NEDEN STATIC BIR KAPI, IClock DEGIL: gun / streak / mood kurallari zaten saf
// servislerde ve tarihi PARAMETRE olarak aliyor (DailyResetService, MoodService,
// HabitScheduling) -- yani test edilebilirlik icin enjeksiyona ihtiyac yok.
// Tarihi URETEN taraf ise sahnedeki MonoBehaviour'lar; Unity nesneleri kurucu
// almadigi icin onlara saat enjekte etmenin temiz bir yolu yok. Bu yuzden
// "simdi"yi okuyan iki sinif (HabbitManager, WorkshopScreen) buradan okuyor.
//
// Kaydirma varsayilan olarak 0 ve yalnizca dev araclari yaziyor: uretimde bu
// dosya DateTime.Now'in onunde duran bedava bir karsilastirmadan baska bir sey
// degil.
//dogu perincek
public static class AppClock
{
    // Kaydirma siniri HabitScheduling.MaxCatchUpDays ile ayni cizgide: gun sonu
    // telafisi o kadar gun geriye bakiyor, daha ileriye atlamanin gozlemlenebilir
    // bir karsiligi yok.
    public const int MaxDayOffset = HabitScheduling.MaxCatchUpDays;

    private static int dayOffset;

    // Gercek tarihe eklenen gun sayisi. Saat ve dakika BILEREK kaydirilmiyor:
    // gece yarisi coroutine'i (HabbitManager.WatchMidnight) gercek gece yarisinda
    // uyanmaya devam etsin.
    public static int DayOffset => dayOffset;

    public static bool IsShifted => dayOffset != 0;

    public static DateTime Now => dayOffset == 0 ? DateTime.Now : DateTime.Now.AddDays(dayOffset);

    public static DateTime Today => Now.Date;

    // Gercek tarih: dev panelinin "kaydirma ne kadar" karsilastirmasi icin.
    public static DateTime RealNow => DateTime.Now;

    public static int Clamp(int days)
    {
        if (days < -MaxDayOffset)
        {
            return -MaxDayOffset;
        }

        return days > MaxDayOffset ? MaxDayOffset : days;
    }

    // Kirpilmis haliyle uygulanan gun sayisini doner: cagiran taraf kendi
    // alanini bu degere esitleyerek panelde gercegi gosterebiliyor.
    public static int SetDayOffset(int days)
    {
        dayOffset = Clamp(days);
        return dayOffset;
    }

    public static void Reset()
    {
        dayOffset = 0;
    }
}
