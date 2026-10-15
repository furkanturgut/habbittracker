using System.Collections.Generic;
using UnityEngine;

// Tablo siparisi: hangi aliskanliga hangi tablo atanir, biten tablo galeriye
// nasil girer, kayittaki siparisler acilista nasil denetlenir.
//
// PaintingProgress "bu tabloda kac parca acik" sorusuna bakiyor, burasi "hangi
// tablo" sorusuna. Ikisi de saf ve static: sahne, MonoBehaviour ya da goruntu
// gerekmiyor, boylece galeri ve boyut hizalama kurallari test edilebiliyor.
//
// HabbitManager bu kararlari aliyor ve ustune kendi isini ekliyor: goruntuleri
// yeniden baglamak, kaydi isaretlemek, olay yayinlamak.
public static class PaintingAssignment
{
    // Aliskanligin AKTIF tablosunun tanimi. Havuz atanmamissa ya da tablo
    // havuzdan kalkmissa false doner.
    public static bool TryGetDefinition(
        PaintingLibrary library,
        HabitData habit,
        out PaintingDefinition definition)
    {
        if (library == null)
        {
            definition = null;
            return false;
        }

        return library.TryGet(habit.activePaintingId, out definition);
    }

    // Aktif tablo sirayla mi aciliyor? Reveal sirasi TABLONUN ozelligi oldugu icin
    // havuzdan okunuyor.
    //
    // Tablo bulunamazsa Random varsayiliyor: indeks listesi hem sirali hem
    // rastgele ilerlemeyi tasiyabilir, tek sayi tasiyamaz -- yani yanlis tahminde
    // bile kayitli ilerleme okunabilir kaliyor.
    public static bool IsSequential(PaintingLibrary library, HabitData habit)
    {
        return TryGetDefinition(library, habit, out PaintingDefinition definition)
            && definition.IsSequential;
    }

    // Havuzdan yeni bir tablo secip atar ve ilerlemeyi sifirlar. Atama yapildiysa
    // true doner.
    public static bool AssignNext(PaintingLibrary library, HabitData habit)
    {
        if (library == null)
        {
            return false;
        }

        PaintingDefinition next = library.PickNext(habit.canvasSize, habit.habitGallery);
        if (next == null)
        {
            return false;
        }

        habit.activePaintingId = next.id;

        // Secilen tablo istenen boyutta olmayabilir (havuzda o boyut yoksa en
        // yakina dusuluyor). canvasSize tablonun parca sayisiyla ayni kalmali,
        // yoksa "tablo bitti" esigi hic yakalanmaz.
        habit.canvasSize = next.PieceCount;

        PaintingProgress.ResetProgress(habit);

        // Bir tablo atamak bekleme durumunu HER ZAMAN bitirir -- Resize ve
        // RepairAll'in da bu satirdan bedava faydalanmasi icin burada.
        habit.awaitingPaintingChoice = false;
        return true;
    }

    // Kullanicinin SECTIGI tabloyu atar. AssignNext'in aynisi, tek farki tablonun
    // rastgele degil id ile gelmesi: canvasSize yine TANIMDAN tureniyor
    // (canvasSize == PieceCount degismezi) ve ilerleme sifirlaniyor.
    //
    // Id cozulemezse FALSE doner ve habit'e DOKUNMAZ: cagiran AssignNext'e
    // dusebilsin diye (kitaplik degismis, tanim silinmis olabilir).
    public static bool Assign(PaintingLibrary library, HabitData habit, string paintingId)
    {
        if (library == null || habit == null || string.IsNullOrEmpty(paintingId))
        {
            return false;
        }

        if (!library.TryGet(paintingId, out PaintingDefinition definition))
        {
            return false;
        }

        habit.activePaintingId = definition.id;
        habit.canvasSize = definition.PieceCount;
        PaintingProgress.ResetProgress(habit);
        habit.awaitingPaintingChoice = false;
        return true;
    }

    // Kullanicinin secim kutusundan yaptigi secimi uygular. paintingId bos ise
    // "Rastgele" karesi secilmis demektir ve AssignNext'e dusulur -- habit
    // sayfasindaki kalibin aynisi (bkz. HabbitManager.CreateHabit).
    //
    // Id cozulemezse (kitaplik degismis olabilir) de AssignNext'e dusuyoruz:
    // habit'i bayrak acik, tablosuz birakmamak icin.
    public static bool ChooseNext(PaintingLibrary library, HabitData habit, string paintingId)
    {
        if (!string.IsNullOrEmpty(paintingId) && Assign(library, habit, paintingId))
        {
            return true;
        }

        return AssignNext(library, habit);
    }

    // Bekleyen bir secimi, tablo atamadan birakir. Tik geri alindiginda
    // (UncompleteHabit) biten tablo da geri alinmali, yoksa ilerlemesi artik
    // dolu olmayan bir tabloda bayrak asili kalir ve o tablo bir daha
    // bitirilemez. Galeriden CIKARMAZ -- "bir kez bitirildi" bilgisi dogru kalir.
    //
    // Beklemede degilse false doner.
    public static bool TryReleaseHold(HabitData habit)
    {
        if (habit == null || !habit.awaitingPaintingChoice)
        {
            return false;
        }

        habit.awaitingPaintingChoice = false;
        return true;
    }

    // Tuval boyutu degistiginde (duzenleme ekrani): ayni tablo buyuyup kuculemez --
    // parca sayisi TABLONUN ozelligi. Bu yuzden yeni boyutta bir tablo atanir ve
    // acilmis parca SAYISI min(onceki, yeni) olarak tasinir; yoksa kullanici
    // boyutu degistirdi diye haftalarca biriken ilerleme silinirdi.
    //
    // Tasima yeni tabloyu DOLDURABILIR; o durumda completed true doner. Galeriye
    // ekleme ve yeni siparis burada YAPILMAZ: goruntuleri yeniden baglamak ve
    // dagitim dongusu icindeki tamamlamayi ertelemek cagiran tarafin isi
    // (bkz. HabbitManager.CompleteActivePainting).
    public static bool Resize(
        PaintingLibrary library,
        HabitData habit,
        int newPieceCount,
        List<int> scratch,
        out bool completed)
    {
        completed = false;

        if (library == null || newPieceCount <= 0 || newPieceCount == habit.canvasSize)
        {
            return false;
        }

        // Beklemede ESKI tablo bitmis ve galeriye girmis: parcalarini yeni tabloya
        // tasimak carry == canvasSize demek olurdu, yani kullanici tuval boyutunu
        // degistirdi diye hic boyamadigi ikinci bir eser kazanirdi.
        //
        // Sayilar ESKI tablonun reveal sirasiyla okunuyor: hangi kayit alaninin
        // gecerli oldugunu tablo belirliyor ve tablo birazdan degisecek.
        int openedCarry = habit.awaitingPaintingChoice
            ? 0
            : PaintingProgress.GetOpenedCount(habit, IsSequential(library, habit));
        int missedCarry = habit.awaitingPaintingChoice ? 0 : habit.missedPixels.Count;

        // Kullanicinin ISTEDIGI boyuta gore ilk sinirlama: tuketilen (acik +
        // yakilmis) toplam yeni boyutu asamaz. ACIK parcalar ONCELIKLI tasinir --
        // kirpma gerekiyorsa once yakilan parcalardan kirpilir, kullanicinin
        // boyadigi parca hic kaybolmaz.
        int consumedCarry = Mathf.Min(openedCarry + missedCarry, newPieceCount);
        openedCarry = Mathf.Min(openedCarry, consumedCarry);
        missedCarry = consumedCarry - openedCarry;

        habit.canvasSize = newPieceCount;

        if (!AssignNext(library, habit))
        {
            return false;
        }

        // Ikinci sinirlama SART: havuzda istenen boyutta tablo yoksa PickNext en
        // yakinina duser ve AssignNext canvasSize'i ona gore duzeltir. Yalnizca
        // buradaki sinirlama olsaydi 16 isteyip 64 alan bir aliskanlik eski 64
        // parcasini tasiyip tabloyu aninda bitirmis olurdu.
        consumedCarry = Mathf.Min(consumedCarry, habit.canvasSize);
        openedCarry = Mathf.Min(openedCarry, consumedCarry);
        missedCarry = consumedCarry - openedCarry;

        bool sequential = IsSequential(library, habit);
        if (sequential)
        {
            habit.totalPixelsPainted = openedCarry;

            // Yakilan parcalar acik parcalardan sonraki tuketilen onege
            // eklenir: sequential, hangi INDEKSIN acildigini zaten tutmuyor
            // (tek sayi), bu yuzden eski tablodaki konumlari da anlamsiz.
            for (int i = 0; i < missedCarry; i++)
            {
                PaintingProgress.MissPiece(habit, true, scratch);
            }
        }
        else
        {
            // Rastgele tabloda hangi parcalarin acildigi sayidan turetilemez.
            // Secimi tek yolundan gecirmek indekslerin benzersiz ve gecerli
            // kalmasini garanti ediyor.
            for (int i = 0; i < openedCarry; i++)
            {
                PaintingProgress.OpenNextPiece(habit, false, scratch);
            }

            for (int i = 0; i < missedCarry; i++)
            {
                PaintingProgress.MissPiece(habit, false, scratch);
            }
        }

        completed = PaintingProgress.IsComplete(habit, sequential);
        return true;
    }

    // Aktif tablo bitmisse galeriye alir. Yerine YENI tablo atamaz -- tablo
    // ekranda tam haliyle kalir, siradaki secim kutusundan gelir (bkz.
    // PaintingChoiceSheet). Biten tablonun id'si finishedId ile doner.
    //
    // IDEMPOTENT: ayni aliskanligin birden fazla goruntusu tamamlandi bildirebilir.
    // Ilk cagri awaitingPaintingChoice'i actigi icin sonraki cagrilar baştaki
    // guard'da false doner -- galeriye ikinci kez eklenmez.
    public static bool TryCompleteActive(
        PaintingLibrary library,
        HabitData habit,
        out string finishedId)
    {
        finishedId = null;

        if (habit.awaitingPaintingChoice)
        {
            return false;
        }

        if (!PaintingProgress.IsComplete(habit, IsSequential(library, habit)))
        {
            return false;
        }

        finishedId = habit.activePaintingId;
        HabitGallery.Record(habit, finishedId, habit.missedPixels);
        habit.awaitingPaintingChoice = true;
        return true;
    }

    // Acilista kayittaki tablo siparislerini denetler. Bir sey degistiyse true
    // doner (cagiran taraf buna gore kaydeder).
    public static bool RepairAll(PaintingLibrary library, PlayerSaveData data)
    {
        if (library == null)
        {
            return false;
        }

        bool changed = false;
        List<HabitData> habits = data.habits;

        for (int i = 0; i < habits.Count; i++)
        {
            HabitData habit = habits[i];

            // Tablosu yok ya da havuzdan kalkmis: yenisini ata.
            if (!library.TryGet(habit.activePaintingId, out PaintingDefinition definition))
            {
                AssignNext(library, habit);
                changed = true;
                continue;
            }

            // canvasSize tablonun parca sayisiyla uyusmuyor (tablo tanimi
            // guncellenmis olabilir): ilerleme anlamsizlasir, hizaliyoruz.
            if (habit.canvasSize != definition.PieceCount)
            {
                habit.canvasSize = definition.PieceCount;
                changed = true;
            }

            // Kayit bitmis bir tabloyla acildi ve bayrak henuz acilmamis:
            // uygulama kutlama sirasinda oldurulmus. Tamamlama akisini simdi
            // calistiriyoruz -- bayrak kontrolu de otomatik gelir, bekleyen bir
            // secim normal bir durumdur ve burada BOZULMAZ.
            if (TryCompleteActive(library, habit, out _))
            {
                changed = true;
            }
        }

        return changed;
    }

}
