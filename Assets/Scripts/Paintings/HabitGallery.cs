using System;
using System.Collections.Generic;

// Galeri uyeligi ve delikleri: bir tablo bittiginde habitGallery'ye EKLENIR ve
// varsa yakilan parcalari galleryHoles'a KAYDEDILIR.
//
// PaintingAssignment.TryCompleteActive'in icindeki private AddToGallery'den
// buraya tasindi -- "galeriye nasil girilir" kurali artik delikleri de
// bildigi icin kendi dosyasinda durmasi dogru.
public static class HabitGallery
{
    // Habit'in galerisine paintingId'yi EKLER (yoksa) ve varsa yakilan
    // parcalari kaydeder. Galeriye ikinci kez EKLEMEZ (habitGallery bir
    // UYELIK listesi, tekrar sayaci degil); delikler ise ayni id icin HER
    // CAGRIDA uzerine yazilir -- tik geri alinip tablo farkli deliklerle
    // yeniden bitirilebiliyor, bayat bir liste tutmanin anlami yok.
    public static void Record(HabitData habit, string paintingId, List<int> missedPieces)
    {
        if (string.IsNullOrEmpty(paintingId))
        {
            return;
        }

        if (!Contains(habit, paintingId))
        {
            habit.habitGallery.Add(paintingId);
        }

        RecordHoles(habit, paintingId, missedPieces);
    }

    public static bool Contains(HabitData habit, string paintingId)
    {
        return ListLookup.ContainsOrdinal(habit.habitGallery, paintingId);
    }

    // Bu tablonun delikleri var mi? Deliksiz bitmisse ya da bir girdisi yoksa
    // false doner.
    public static bool TryGetHoles(HabitData habit, string paintingId, out List<int> pieces)
    {
        List<PaintingHoles> holes = habit.galleryHoles;
        for (int i = 0; i < holes.Count; i++)
        {
            if (string.Equals(holes[i].paintingId, paintingId, StringComparison.Ordinal))
            {
                pieces = holes[i].pieces;
                return pieces.Count > 0;
            }
        }

        pieces = null;
        return false;
    }

    // Rotus harcamasi: bitmis bir tablonun ILK deligini kapatir (pieces[0]'i
    // siler). Onarilan parca indeksini, tablo bulunamazsa ya da zaten deliksizse
    // -1 doner. Liste bosalirsa girdinin kendisi silinir -- Record'un "bos liste =
    // delik yok" sozlesmesi boylece korunur, bir sonraki TryGetHoles yine false der.
    public static int RestoreOne(HabitData habit, string paintingId)
    {
        List<PaintingHoles> holes = habit.galleryHoles;
        for (int i = 0; i < holes.Count; i++)
        {
            if (!string.Equals(holes[i].paintingId, paintingId, StringComparison.Ordinal))
            {
                continue;
            }

            List<int> pieces = holes[i].pieces;
            if (pieces.Count == 0)
            {
                return -1;
            }

            int restored = pieces[0];
            pieces.RemoveAt(0);

            if (pieces.Count == 0)
            {
                holes.RemoveAt(i);
            }

            return restored;
        }

        return -1;
    }

    private static void RecordHoles(HabitData habit, string paintingId, List<int> missedPieces)
    {
        if (missedPieces == null || missedPieces.Count == 0)
        {
            // Deliksiz bitti: onceki bir calisma delikli birakmis olabilir
            // (tik geri alinip yeniden bitirildi), o kaydi temizliyoruz.
            RemoveHoles(habit, paintingId);
            return;
        }

        List<PaintingHoles> holes = habit.galleryHoles;
        for (int i = 0; i < holes.Count; i++)
        {
            if (string.Equals(holes[i].paintingId, paintingId, StringComparison.Ordinal))
            {
                holes[i].pieces.Clear();
                holes[i].pieces.AddRange(missedPieces);
                return;
            }
        }

        PaintingHoles entry = new PaintingHoles { paintingId = paintingId };
        entry.pieces.AddRange(missedPieces);
        holes.Add(entry);
    }

    private static void RemoveHoles(HabitData habit, string paintingId)
    {
        List<PaintingHoles> holes = habit.galleryHoles;
        for (int i = 0; i < holes.Count; i++)
        {
            if (string.Equals(holes[i].paintingId, paintingId, StringComparison.Ordinal))
            {
                holes.RemoveAt(i);
                return;
            }
        }
    }
}
