using System.Collections.Generic;

// Secim kutusunun listesi: bir aliskanligin tuval boyutuna uyan, henuz
// GALERISINDE olmayan tablolar. Kitapliktan bagimsiz asiri yuk sahne
// kurmadan test edilebiliyor.
//
// PaintingLibrary.PickNext'teki "hepsi bitirilmis, havuzu bastan ac" kurali
// burada da aynen uygulaniyor: filtre sonucu bossa butun kova yaziliyor.
public static class PaintingChoices
{
    public static void Collect(
        IReadOnlyList<PaintingDefinition> bucket,
        List<string> gallery,
        List<PaintingDefinition> buffer)
    {
        buffer.Clear();

        if (bucket == null)
        {
            return;
        }

        for (int i = 0; i < bucket.Count; i++)
        {
            PaintingDefinition definition = bucket[i];
            if (!ListLookup.ContainsOrdinal(gallery, definition.id))
            {
                buffer.Add(definition);
            }
        }

        if (buffer.Count == 0)
        {
            for (int i = 0; i < bucket.Count; i++)
            {
                buffer.Add(bucket[i]);
            }
        }
    }

    public static void Collect(
        PaintingLibrary library,
        HabitData habit,
        List<PaintingDefinition> buffer)
    {
        buffer.Clear();

        if (library == null || habit == null)
        {
            return;
        }

        Collect(library.ForSize(habit.canvasSize), habit.habitGallery, buffer);
    }
}
