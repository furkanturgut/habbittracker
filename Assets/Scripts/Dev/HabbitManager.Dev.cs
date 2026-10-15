// HabbitManager'in gelistirici araclarina acilan kapisi.
//
// NEDEN PARTIAL: asagidaki bes kapinin dayandigi uyeler (RebindViewsFor,
// MarkDirtyDeferred, HabitChanged/HabitListChanged, ApplyMood,
// CompleteActivePainting) HabbitManager'da private ve private KALMALI. Partial
// bir parca ayni sinifin icinden eristigi icin bu kapilar HabbitManager.cs'e tek
// satir eklemeden burada durabiliyor; dosya silinirse uretim kodu oldugu gibi
// calismaya devam eder.
//
// Kaydi kirletmek ayri bir kapi DEGIL: her komut kendi isini bitirirken onu da
// yapiyor, boylece "degistirdim ama kaydetmeyi unuttum" durumu olusmuyor.
//
// NEDEN DEV ARACLARI KENDI BASINA YAPMIYOR: ilerlemeyi yazip goruntuleri elle
// tazelemeye kalkmak, manager'in ic sirasini (bkz. UncompleteHabit) ikinci bir
// yerde kopyalamak demekti -- ilk degisen kuralda sessizce ayrisirdi.
//
// Uyeler INTERNAL: dev servisleri ayni assembly'de (HabitTracker.Runtime).
// Editor tarafi bu kapilari goremez, yalnizca DevTools ile konusur.
public partial class HabbitManager
{
    // Bir aliskanligin ICERIGI dev tarafindan degisti. Sira UncompleteHabit ile
    // ayni: acilmis parcayi geri almanin yolu goruntuyu bastan baglamak.
    internal void DevNotifyHabitChanged(HabitData habit)
    {
        RebindViewsFor(habit);
        MarkDirtyDeferred();
        HabitChanged?.Invoke(habit);
    }

    // Liste YAPISI degisti (toplu ekleme, toplu sifirlama). HabitChanged ile
    // ayni sey degil (bkz. CLAUDE.md > HabitListChanged).
    internal void DevNotifyListChanged()
    {
        MarkDirtyDeferred();
        HabitListChanged?.Invoke();
    }

    // Mood'u uretimdeki odul/ceza yolundan gecirir: animator degisimi, yukseliste
    // kutlama animasyonu ve kayda yazma dahil.
    internal void DevStepMood(int delta)
    {
        ApplyMood(delta);
    }

    // Mood'u dogrudan bir puana oturtur: kayittan donus yolu (RestoreMoodPoint),
    // yani animator guncellenir ama kutlama animasyonu oynamaz. Puanin kayda
    // yazilmasi ApplyMood'daki satirin aynisi -- PainterManager clamp'ledikten
    // SONRA okunmali, istenen degeri degil gerceklesen degeri saklamak icin.
    internal void DevJumpMood(int point)
    {
        PainterManager painter = PainterManager.Instance;
        if (painter == null)
        {
            return;
        }

        painter.RestoreMoodPoint(point);
        saveSystem.Data.painterMoodPoint = painter.MoodPoint;
        MarkDirtyDeferred();
    }

    // Aktif tabloyu bitirir: galeriye ekler, yenisini atar, olaylari yayar.
    // Dagitim dongusu ertelemesi de dahil (bkz. CompleteActivePainting).
    internal void DevCompleteActivePainting(HabitData habit)
    {
        CompleteActivePainting(habit);
    }
}
