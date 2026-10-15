using UnityEngine;

// Mood puanini elle surmek. Iki ayri yol var ve ikisi de URETIMDEKI yollar:
//
//   Step  -> ApplyMood(delta), yani gunluk odul/cezanin aynisi (animasyonlu).
//   Jump  -> RestoreMoodPoint, yani kayittan donusun aynisi (animasyonsuz).
//
// Ikisini birlikte sunmak bilincli: "mood dustugunde ressam ne yapiyor" ile
// "su moodla acilista nasil gorunuyor" farkli sorular ve dev modun ikisini de
// gosterebilmesi gerekiyor.
public static class DevMoodTools
{
    public static void Step(HabbitManager manager, int delta)
    {
        if (manager == null || delta == 0)
        {
            return;
        }

        manager.DevStepMood(delta);
    }

    public static void SetPoint(HabbitManager manager, int point)
    {
        if (manager == null)
        {
            return;
        }

        manager.DevJumpMood(Mathf.Clamp(point, PainterManager.MinMoodPoint, PainterManager.MaxMoodPoint));
    }

    public static void SetMood(HabbitManager manager, PainterManager.PainterMoods mood)
    {
        SetPoint(manager, LowestPointFor(mood));
    }

    // Bir modun EN DUSUK puan karsiligi. Saf: puan -> mod tablosu
    // PainterManager'da (MoodForPoint) ve ikinci bir kopyasi cikmiyor.
    //
    // En dusugu seciyoruz cunku dev modun isi gecisleri gormek: oraya oturunca
    // bir sonraki -1 adimi modu gercekten degistirir. En yuksek temsilci
    // secilseydi ayni mod icinde birkac tik bosa giderdi.
    //
    // Mod tabloda hic yoksa (enum buyudu, tablo guncellenmedi) -1 degil
    // varsayilan puan doner: dev araci yuzunden kayit bozulmasin.
    public static int LowestPointFor(PainterManager.PainterMoods mood)
    {
        for (int point = PainterManager.MinMoodPoint; point <= PainterManager.MaxMoodPoint; point++)
        {
            if (PainterManager.MoodForPoint(point) == mood)
            {
                return point;
            }
        }

        return PainterManager.DefaultMoodPoint;
    }
}
