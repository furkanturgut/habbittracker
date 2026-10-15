using System;
using System.Collections.Generic;
using UnityEngine;

// Bir aliskanligin tablosunu, yakilan parcalari pisirilmis halde veren onbellek.
//
// NEDEN ONBELLEK: BurnedPaintingArt.Build butun tabloyu okuyup yeniden yaziyor,
// oysa tile'lar kaydirma boyunca surekli yeniden baglaniyor. Pisirilen gorsel ise
// yalnizca missedPixels degisince degisiyor -- pratikte gunde en fazla bir kez.
//
// NEDEN STATIC: ayni kopyayi UC cizici istiyor (HabitTile, HabitCard,
// PaintingView) ve hicbirinin HabbitManager'a erisimi yok; uretilen Texture2D de
// havuzdaki tek bir goruntuden uzun yasamak zorunda. Burada KURAL YOK -- gorunumun
// tamami BurnedPaintingArt'ta, burada sadece omur yonetimi var.
public static class BurnedPaintingCache
{
    private sealed class Entry
    {
        public string PaintingId;
        public int Signature;
        public Texture2D Texture;
        public Sprite Sprite;
    }

    // Galeri: BITMIS bir tablonun delikleri ARTIK DEGISMIYOR (galleryHoles'a
    // dondu) ve ayni aliskanligin -- ya da FARKLI iki aliskanligin -- birkac
    // bitmis tablosu AYNI ANDA ekranda olabiliyor. Ustteki habit-basina-tek-
    // kayit sozluk bunu tutamaz: iki aliskanlik AYNI tablo id'sini FARKLI
    // deliklerle bitirmis olabilir, habit-basina anahtarlamak gene thrash
    // uretirdi. Bu yuzden anahtar "habitId::paintingId" -- kayit habit'in
    // ILERLEMESINE bagli degil, yalnizca Clear()/Forget() ile gider.
    private sealed class FinishedEntry
    {
        public int Signature;
        public Texture2D Texture;
        public Sprite Sprite;
    }

    private const string FinishedKeySeparator = "::";

    private static readonly Dictionary<string, Entry> entries =
        new Dictionary<string, Entry>(16, StringComparer.Ordinal);

    private static readonly Dictionary<string, FinishedEntry> finishedEntries =
        new Dictionary<string, FinishedEntry>(16, StringComparer.Ordinal);

    // Deligi olmayan tabloda tanimin KENDI sprite'i donuyor: yaygin durum bu ve
    // hicbir sey tahsis etmiyor.
    public static Sprite SpriteFor(PaintingDefinition definition, HabitData habit)
    {
        if (definition == null || definition.sprite == null || habit == null)
        {
            return definition != null ? definition.sprite : null;
        }

        if (habit.missedPixels == null || habit.missedPixels.Count == 0)
        {
            return definition.sprite;
        }

        int signature = SignatureOf(habit.missedPixels);

        if (entries.TryGetValue(habit.id, out Entry entry))
        {
            if (entry.Sprite != null
                && entry.Signature == signature
                && string.Equals(entry.PaintingId, definition.id, StringComparison.Ordinal))
            {
                return entry.Sprite;
            }

            Release(entry);
        }
        else
        {
            entry = new Entry();
            entries[habit.id] = entry;
        }

        PaintingPieceState state = PaintingPieceState.For(definition, habit);
        Sprite sprite = BurnedPaintingArt.Build(definition.sprite, state, definition.PieceCount);

        if (sprite == null)
        {
            // Texture okunabilir degil (bkz. BurnedPaintingArt.Build). Cagiran
            // eski gorunume duser; bos kaydi birakmiyoruz ki her baglamada
            // yeniden denemeyi ucuz tutalim ama bayat sprite da tutmayalim.
            entries.Remove(habit.id);
            return definition.sprite;
        }

        entry.PaintingId = definition.id;
        entry.Signature = signature;
        entry.Sprite = sprite;
        entry.Texture = sprite.texture;

        return sprite;
    }

    // Galeri: BITMIS bir tablonun pisirilmis sprite'i. habitId'nin ayri bir
    // parametre olmasinin sebebi yukarida (bkz. FinishedEntry) -- ayni tablo
    // id'si iki aliskanlikta farkli deliklerle bitmis olabilir.
    public static Sprite SpriteFor(PaintingDefinition definition, string habitId, List<int> holes)
    {
        if (definition == null || definition.sprite == null)
        {
            return definition != null ? definition.sprite : null;
        }

        if (holes == null || holes.Count == 0)
        {
            return definition.sprite;
        }

        string key = FinishedKey(habitId, definition.id);
        int signature = SignatureOf(holes);

        if (finishedEntries.TryGetValue(key, out FinishedEntry entry))
        {
            if (entry.Sprite != null && entry.Signature == signature)
            {
                return entry.Sprite;
            }

            Release(entry);
        }
        else
        {
            entry = new FinishedEntry();
            finishedEntries[key] = entry;
        }

        PaintingPieceState state = PaintingPieceState.ForFinished(definition.PieceCount, holes);
        Sprite sprite = BurnedPaintingArt.Build(definition.sprite, state, definition.PieceCount);

        if (sprite == null)
        {
            finishedEntries.Remove(key);
            return definition.sprite;
        }

        entry.Signature = signature;
        entry.Sprite = sprite;
        entry.Texture = sprite.texture;

        return sprite;
    }

    // Aliskanlik silinince kaydi da gider; yoksa oturum boyunca bir texture asili
    // kalirdi.
    public static void Forget(string habitId)
    {
        if (string.IsNullOrEmpty(habitId))
        {
            return;
        }

        if (entries.TryGetValue(habitId, out Entry entry))
        {
            Release(entry);
            entries.Remove(habitId);
        }

        ForgetFinished(habitId);
    }

    // HabbitManager.OnDestroy cagiriyor: Texture2D cop toplayici ile gitmiyor.
    public static void Clear()
    {
        foreach (KeyValuePair<string, Entry> pair in entries)
        {
            Release(pair.Value);
        }

        entries.Clear();

        foreach (KeyValuePair<string, FinishedEntry> pair in finishedEntries)
        {
            Release(pair.Value);
        }

        finishedEntries.Clear();
    }

    private static string FinishedKey(string habitId, string paintingId)
    {
        return habitId + FinishedKeySeparator + paintingId;
    }

    // "habitId::paintingId" anahtari dogrudan aranamadigi icin (habitId'ye ait
    // TUM bitmis kayitlari silmek gerekiyor) butun sozluk taraniyor. Boyut
    // kucuk: ayni anda ekranda gorunen bitmis tablo sayisiyla sinirli.
    private static void ForgetFinished(string habitId)
    {
        string prefix = habitId + FinishedKeySeparator;
        List<string> toRemove = null;

        foreach (KeyValuePair<string, FinishedEntry> pair in finishedEntries)
        {
            if (pair.Key.StartsWith(prefix, StringComparison.Ordinal))
            {
                if (toRemove == null)
                {
                    toRemove = new List<string>();
                }

                toRemove.Add(pair.Key);
            }
        }

        if (toRemove == null)
        {
            return;
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            Release(finishedEntries[toRemove[i]]);
            finishedEntries.Remove(toRemove[i]);
        }
    }

    // missedPixels sirali gelmeyebilir ve Resize onu KUCULTEBILIR, bu yuzden
    // sayinin kendisi yetmiyor; sira bagimsiz bir imza gerekiyor.
    private static int SignatureOf(List<int> missedPixels)
    {
        unchecked
        {
            int signature = missedPixels.Count * 397;

            for (int i = 0; i < missedPixels.Count; i++)
            {
                signature += (missedPixels[i] + 1) * 31;
            }

            return signature;
        }
    }

    private static void Release(Entry entry)
    {
        Destroy(entry.Sprite);
        Destroy(entry.Texture);

        entry.Sprite = null;
        entry.Texture = null;
        entry.PaintingId = null;
        entry.Signature = 0;
    }

    private static void Release(FinishedEntry entry)
    {
        Destroy(entry.Sprite);
        Destroy(entry.Texture);

        entry.Sprite = null;
        entry.Texture = null;
        entry.Signature = 0;
    }

    private static void Destroy(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        // EditMode testleri Play mode'da degil: orada Destroy kuyruga atar ve
        // hicbir zaman islenmez.
        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(target);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
