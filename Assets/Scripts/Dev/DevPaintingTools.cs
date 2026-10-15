using System.Collections.Generic;
using UnityEngine;

// Aktif tablonun ilerlemesini elle surmek: doldur, bitir, sifirla, "su kadar
// parca acik olsun".
//
// Hangi kayit alaninin gecerli oldugu (Sequential -> totalPixelsPainted,
// Random -> randomOpenedPixels) kurali burada TEKRARLANMIYOR: secim yine
// PaintingProgress.OpenNextPiece'ten geciyor, boylece indeksler benzersiz ve
// gecerli kaliyor. Ayni gerekce PaintingAssignment.Resize'daki tasima dongusunde
// de var -- ikisi ayni yoldan gectigi icin ayrismalari mumkun degil.
public static class DevPaintingTools
{
    // Rastgele siradaki secim tahsis uretmesin diye alan olarak duruyor; dev
    // yolu sicak degil ama kodun geri kalaniyla ayni kalipta olsun.
    private static readonly List<int> scratch = new List<int>(64);

    public static int OpenedCount(PaintingLibrary library, HabitData habit)
    {
        if (habit == null)
        {
            return 0;
        }

        return PaintingProgress.GetOpenedCount(habit, PaintingAssignment.IsSequential(library, habit));
    }

    // --- Saf kisim: yalnizca veri, sahne yok ---

    // Aktif tabloyu "tam count parca acik" durumuna getirir ve gerceklesen
    // sayiyi doner (istenen sayi tuvalin disindaysa kirpilir).
    public static int SetOpenedPieces(PaintingLibrary library, HabitData habit, int count, List<int> buffer)
    {
        if (habit == null || habit.canvasSize <= 0)
        {
            return 0;
        }

        count = Mathf.Clamp(count, 0, habit.canvasSize);

        bool sequential = PaintingAssignment.IsSequential(library, habit);
        PaintingProgress.ResetProgress(habit);

        if (sequential)
        {
            // Sirali tabloda acik parcalar bastan sayilir: tek sayi yeterli.
            habit.totalPixelsPainted = count;
            return count;
        }

        for (int i = 0; i < count; i++)
        {
            PaintingProgress.OpenNextPiece(habit, false, buffer);
        }

        return habit.randomOpenedPixels.Count;
    }

    // Aktif tabloya "gun kacirildi" gibi count kadar parca yakar (kalici
    // kapanir). PaintingProgress.MissPiece'ten geciyor -- OpenNextPiece ile
    // AYNI secim kuralindan, boylece acma ve yakma hic ayni parcayi secmez.
    public static int MissPieces(PaintingLibrary library, HabitData habit, int count, List<int> buffer)
    {
        if (habit == null)
        {
            return 0;
        }

        bool sequential = PaintingAssignment.IsSequential(library, habit);
        int burned = 0;

        for (int i = 0; i < count; i++)
        {
            if (PaintingProgress.MissPiece(habit, sequential, buffer) < 0)
            {
                break;
            }

            burned++;
        }

        return burned;
    }

    // --- Sahneli kisim: ustune goruntuleri ve kaydi tazeler ---

    public static int MissPieces(HabbitManager manager, HabitData habit, int count)
    {
        if (manager == null || habit == null)
        {
            return 0;
        }

        int burned = MissPieces(manager.Library, habit, count, scratch);
        manager.DevNotifyHabitChanged(habit);
        return burned;
    }

    public static int SetOpenedPieces(HabbitManager manager, HabitData habit, int count)
    {
        if (manager == null || habit == null)
        {
            return 0;
        }

        int applied = SetOpenedPieces(manager.Library, habit, count, scratch);
        manager.DevNotifyHabitChanged(habit);
        return applied;
    }

    // Tabloyu doldurur ama BITIRMEZ: tam halini gormek icin. Tamamlanma akisi
    // (galeri + yeni tablo) bir sonraki tikte ya da acilista RepairAll'da
    // calisir -- ikisi de var olan ve test edilmis yollar.
    public static void Fill(HabbitManager manager, HabitData habit)
    {
        if (habit != null)
        {
            SetOpenedPieces(manager, habit, habit.canvasSize);
        }
    }

    public static void Clear(HabbitManager manager, HabitData habit)
    {
        SetOpenedPieces(manager, habit, 0);
    }

    public static void SetOpenedRatio(HabbitManager manager, HabitData habit, float ratio)
    {
        if (habit != null)
        {
            SetOpenedPieces(manager, habit, Mathf.RoundToInt(Mathf.Clamp01(ratio) * habit.canvasSize));
        }
    }

    // Tabloyu bitirir: galeriye girer, yerine yenisi atanir, PaintingFinished
    // yayinlanir. Doldurma SAF yoldan yapiliyor (goruntuleri iki kez
    // tazelememek icin); haber verme isi CompleteActivePainting'in zaten yaptigi sey.
    public static void FinishPainting(HabbitManager manager, HabitData habit)
    {
        if (manager == null || habit == null)
        {
            return;
        }

        SetOpenedPieces(manager.Library, habit, habit.canvasSize, scratch);
        manager.DevCompleteActivePainting(habit);
    }
}
