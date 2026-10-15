using System;

// Zaman yolculugu: saati kaydirir ve uygulamanin gun donusu hattini calistirir.
//
// Gun / mood / streak kurallarindan HICBIRI burada tekrarlanmiyor. Kaydirmadan
// sonra tek is manager.RefreshDayIfNeeded() cagirmak; mood'un reset'ten ONCE
// gelmesi gibi sira kurallari orada ve orada kalmali
// (bkz. CLAUDE.md > "Mood must be settled before the daily reset").
public static class DevClock
{
    // Kaydirmayi uygular. Gun gercekten dondugunde true doner -- yani kayit
    // tazelendi, tikler sifirlandi ve ekranlar DayRolledOver ile haberlendi.
    //
    // manager null olabilir (sahne henuz calismiyor): saat yine kaydirilir,
    // kaydi tazeleyecek taraf bir sonraki acilista Load() olur.
    public static bool Apply(HabbitManager manager, int dayOffset)
    {
        AppClock.SetDayOffset(dayOffset);
        return manager != null && manager.RefreshDayIfNeeded();
    }

    public static bool Shift(HabbitManager manager, int deltaDays)
    {
        return Apply(manager, AppClock.DayOffset + deltaDays);
    }

    public static bool Reset(HabbitManager manager)
    {
        return Apply(manager, 0);
    }

    // Hedef tarihe gitmek icin gereken gun kaydirmasi. Saf: takvim matematigi
    // burada, yan etkisi olan Apply'dan ayri -- testler bunu dogrulayabiliyor.
    //
    // Saat ve dakika BILEREK tasinmiyor (yalnizca .Date farki): AppClock gunu
    // kaydiriyor, gunun icindeki saati degil.
    public static int DayOffsetFor(DateTime target, DateTime realNow)
    {
        return (int)(target.Date - realNow.Date).TotalDays;
    }

    public static bool GoTo(HabbitManager manager, DateTime target)
    {
        return Apply(manager, DayOffsetFor(target, AppClock.RealNow));
    }
}
