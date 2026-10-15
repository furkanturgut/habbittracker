using System.Collections.Generic;
using UnityEngine;

// Bir aliskanligin aktif tablosundaki ilerleme matematigi.
//
// ILERLEMENIN SAHIBI BURASI, PaintPixels degil: hangi parcanin acilacagina
// burada karar verilip indeks goruntulere gonderiliyor. Sebep, ayni aliskanligin
// ayni anda birden fazla goruntude olabilmesi -- her goruntu kendi secimini
// yapsaydi Random modda iki tablo birbirinden ayrisirdi.
//
// Static ve saf; Random secim disaridan verilen buffer'i kullandigi icin sifir
// tahsis yapar.
//
// KACIRILAN GUNLER: planli oldugu halde yapilmayan gunde "o gun acilacak olan
// parca" habit.missedPixels'e kalici olarak yazilir (bkz. MissedPieceService).
// Tuketilen = acik + yakilmis; "tablo bitti" esigi artik buna bakiyor (bkz.
// IsComplete / HabitData.ConsumedPieceCount). totalPixelsPainted'in ANLAMI
// degismedi -- hala sadece ACIK parca sayisi (bkz. HabitData.OpenedPieceCount);
// yakilan parcalar o sayaci ARTIRMAZ, yalnizca missedPixels'e eklenir.
//
// SEQUENTIAL'DA PARCALAR HER ZAMAN AYNI SIRAYLA TUKETILIR (ac ya da yak, farketmez):
// indeks 0, sonra 1, sonra 2... Yani "tuketilmis" kume HER ZAMAN 0..(N-1) onekidir
// (N = totalPixelsPainted + missedPixels.Count). Bu da siradaki tuketilecek
// parcayi tek bir toplamaya indiriyor -- tarama gerekmiyor (bkz. NextSequentialPiece).
public static class PaintingProgress
{
    // Aktif tabloda kac parca acik? Okuma isini verinin kendisi yapiyor
    // (bkz. HabitData.OpenedPieceCount): hangi alanin gecerli oldugu kuralinin
    // iki ayri yerde yazili olmasi, birini degistirip digerini atlamak demekti.
    public static int GetOpenedCount(HabitData habit, bool sequential)
    {
        return habit.OpenedPieceCount(sequential);
    }

    // Acik + yakilmis. "Tablo bitti" esigi artik buna bakiyor: delikli bir
    // tablo da canvasSize'a ulasinca biter.
    public static int GetConsumedCount(HabitData habit, bool sequential)
    {
        return habit.ConsumedPieceCount(sequential);
    }

    public static bool IsComplete(HabitData habit, bool sequential)
    {
        return habit.canvasSize > 0 && GetConsumedCount(habit, sequential) >= habit.canvasSize;
    }

    // Sequential'da SIRADAKI tuketilecek (acilacak ya da yakilacak) parca.
    // Tuketilen kume her zaman 0..(N-1) oneki oldugu icin bu toplama tek
    // basina yeterli -- ayri bir tarama gerekmiyor. Tuketilecek parca
    // kalmadiysa -1.
    public static int NextSequentialPiece(HabitData habit)
    {
        int next = habit.totalPixelsPainted + habit.missedPixels.Count;
        return next < habit.canvasSize ? next : -1;
    }

    // Acma ve yakmanin PAYLASTIGI secim kurali: hangisi cagirirsa cagirsin ayni
    // parca secilir. Ikisi ayri kurallar olsaydi ilk degisen tarafta sessizce
    // ayrisirdi.
    private static int PickNextPiece(HabitData habit, bool sequential, List<int> scratch)
    {
        if (habit.canvasSize <= 0)
        {
            return -1;
        }

        if (sequential)
        {
            return NextSequentialPiece(habit);
        }

        if (habit.randomOpenedPixels.Count + habit.missedPixels.Count >= habit.canvasSize)
        {
            return -1;
        }

        scratch.Clear();
        for (int i = 0; i < habit.canvasSize; i++)
        {
            if (!ListLookup.Contains(habit.randomOpenedPixels, i) && !ListLookup.Contains(habit.missedPixels, i))
            {
                scratch.Add(i);
            }
        }

        if (scratch.Count == 0)
        {
            return -1;
        }

        return scratch[Random.Range(0, scratch.Count)];
    }

    // Sradaki parcayi SECER ve ACIK olarak isler. Acilan parcanin indeksini
    // doner, acacak parca kalmadiysa -1.
    public static int OpenNextPiece(HabitData habit, bool sequential, List<int> scratch)
    {
        int index = PickNextPiece(habit, sequential, scratch);
        if (index < 0)
        {
            return -1;
        }

        if (sequential)
        {
            habit.totalPixelsPainted++;
        }
        else
        {
            habit.randomOpenedPixels.Add(index);
        }

        return index;
    }

    // "O gun yapilsaydi acilacak olan parca"yi KALICI kapatir. OpenNextPiece
    // ile AYNI secim kuralindan (PickNextPiece) gectigi icin acma ve yakma
    // farkli bir parca secemez. Yakilan parca totalPixelsPainted'i ARTIRMAZ --
    // o sayac sadece acik parcayi sayiyor, tuketilen uzunluk missedPixels.Count
    // ile birlikte okunuyor (bkz. GetConsumedCount).
    public static int MissPiece(HabitData habit, bool sequential, List<int> scratch)
    {
        int index = PickNextPiece(habit, sequential, scratch);
        if (index < 0)
        {
            return -1;
        }

        habit.missedPixels.Add(index);
        return index;
    }

    // Son acilan parcayi geri alir (tik geri alindiginda). Geri alinan indeksi
    // doner, acik parca yoksa -1.
    //
    // Yalnizca ACMAYI geri alir -- cagiran taraf (UncompleteHabit) bunu bir
    // kullanici tikinin geri alinmasinda kullaniyor ve tikler yakma uretmez,
    // yakma yalnizca gun sonunda olusur. Yani geri alinacak son islem her
    // zaman bir ACMA'dir.
    public static int UndoLastPiece(HabitData habit, bool sequential)
    {
        if (sequential)
        {
            if (habit.totalPixelsPainted <= 0)
            {
                return -1;
            }

            habit.totalPixelsPainted--;

            // Azaltilmis sayiyla NextSequentialPiece, az once kapatilan
            // indeksin AYNISINI doner -- tuketilen onek bir kisaldi, siradaki
            // tuketilecek parca tekrar o indeks oldu.
            return NextSequentialPiece(habit);
        }

        int last = habit.randomOpenedPixels.Count - 1;
        if (last < 0)
        {
            return -1;
        }

        int index = habit.randomOpenedPixels[last];
        habit.randomOpenedPixels.RemoveAt(last);
        return index;
    }

    // Tablo degistiginde ilerlemeyi sifirlar. Yakilan parcalar da dahil: yeni
    // tablo temiz baslamali.
    public static void ResetProgress(HabitData habit)
    {
        habit.totalPixelsPainted = 0;
        habit.randomOpenedPixels.Clear();
        habit.missedPixels.Clear();
    }
}
