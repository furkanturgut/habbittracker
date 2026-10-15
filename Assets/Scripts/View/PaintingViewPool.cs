using System;
using System.Collections.Generic;
using UnityEngine;

// PaintingView havuzu.
//
// Ayni anda ekranda kac tablo gorunebiliyorsa o kadar goruntu yetiyor: liste
// kaydirildikca kartlar geri donusuyor. Detay ekrani icin bir tane fazladan
// ayriliyor, cunku bir aliskanlik ayni anda hem listede hem detayda durabilir ve
// ikisi ayri goruntu ister.
//
// Goruntuler dunyada birbirinden UZAK slotlara yerlestiriliyor: her goruntunun
// kendi kamerasi var ve kameralar birbirinin tablosunu gormemeli. Layer filtresi
// hepsini ayni layer'da tuttugu icin mesafe ayirmanin en ucuz yolu.
[DisallowMultipleComponent]
public class PaintingViewPool : MonoBehaviour
{
    [Tooltip("Ayni anda gorunebilecek maksimum tablo sayisi. Liste ekraninda ayni anda kac kart gorunuyorsa o kadar + detay icin 1.")]
    [SerializeField] private int poolSize = 9;

    [Tooltip("Goruntuler arasindaki dunya mesafesi. Kameralarin birbirinin tablosunu gormemesi icin tablo boyutundan cok daha buyuk olmali.")]
    [SerializeField] private float slotSpacing = 1000f;

    [Tooltip("Liste kartlari icin RenderTexture kenari.")]
    [SerializeField] private int listTextureSize = 256;

    [Tooltip("Detay ekrani icin RenderTexture kenari.")]
    [SerializeField] private int detailTextureSize = 512;

    private readonly List<PaintingView> views = new List<PaintingView>();
    private readonly List<PaintingView> free = new List<PaintingView>();

    // Hangi goruntu hangi aliskanliga bagli? Bir aliskanligin BIRDEN FAZLA
    // goruntusu olabilir (liste karti + detay), o yuzden deger tek bir goruntu
    // degil liste.
    private readonly Dictionary<string, List<PaintingView>> boundByHabit =
        new Dictionary<string, List<PaintingView>>(16, StringComparer.Ordinal);

    // Bosalan liste nesneleri: her bind/unbind'da yeni List ayirmamak icin.
    private readonly Stack<List<PaintingView>> listPool = new Stack<List<PaintingView>>();

    // Goruntu -> hangi aliskanlik icin ALINDIGI. Release'in view.BoundHabitId'ye
    // bakmamasi onemli: baglama basarisiz olursa (tablo bulunamadi) o alan null
    // kalir ve goruntu boundByHabit icinde sonsuza kadar asili kalirdi.
    private readonly Dictionary<PaintingView, string> acquiredFor =
        new Dictionary<PaintingView, string>(16);

    // Havuzdaki TUM goruntuler, bos olanlar dahil ve kuruldugu sirada. Sahne
    // kurulumu ile hata ayiklama icin; "hangisi kime bagli" sorusunun yeri
    // TryGetViews, "bos olan hangisi" sorusunun yeri Acquire.
    public IReadOnlyList<PaintingView> Views => views;

    // Bir goruntunun tablosu tamamlandiginda. HabbitManager buna abone oluyor.
    public event Action<PaintingView> PaintingCompleted;

    private void Awake()
    {
        BuildPool();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < views.Count; i++)
        {
            if (views[i] != null)
            {
                views[i].PaintingCompleted -= HandleViewCompleted;
            }
        }
    }

    private void BuildPool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            // Ilk goruntu detay ekrani icin: daha yuksek cozunurluklu.
            bool detail = i == 0;

            GameObject viewObject = new GameObject(detail ? "PaintingView (Detail)" : "PaintingView " + i);
            // PASIF yaratiliyor: PaintingView.Awake RenderTexture'i kurdugu icin
            // boyutu ondan once yazabilmemiz gerekiyor. Aktiflestirme asagida.
            viewObject.SetActive(false);
            viewObject.transform.SetParent(transform, false);

            PaintingView view = viewObject.AddComponent<PaintingView>();
            view.OverrideTextureSize(detail ? detailTextureSize : listTextureSize);
            // Slotlar birbirinden uzak: her goruntunun kamerasi yalnizca kendi
            // tablosunu gorsun. Yerlestirme goruntunun kendi isi (MoveToSlot),
            // havuz yalnizca hangi slotta duracagina karar veriyor.
            view.MoveToSlot(new Vector3(i * slotSpacing, 0f, 0f));
            viewObject.SetActive(true);

            view.PaintingCompleted += HandleViewCompleted;

            views.Add(view);
            free.Add(view);
        }
    }

    // Bir aliskanlik icin bos goruntu alir. Havuz tukendiyse null doner --
    // cagiran taraf (UI) bunu "bu kart simdilik tablosuz" olarak ele almali.
    public PaintingView Acquire(string habitId)
    {
        if (free.Count == 0)
        {
            Debug.LogWarning(
                "PaintingViewPool: havuz tukendi (" + poolSize + "). " +
                "Ayni anda gorunen tablo sayisi kadar goruntu ayrilmali.",
                this);
            return null;
        }

        int last = free.Count - 1;
        PaintingView view = free[last];
        free.RemoveAt(last);

        if (!boundByHabit.TryGetValue(habitId, out List<PaintingView> bucket))
        {
            bucket = listPool.Count > 0 ? listPool.Pop() : new List<PaintingView>(2);
            boundByHabit.Add(habitId, bucket);
        }

        bucket.Add(view);
        acquiredFor[view] = habitId;
        return view;
    }

    public void Release(PaintingView view)
    {
        if (view == null)
        {
            return;
        }

        if (acquiredFor.TryGetValue(view, out string habitId)
            && boundByHabit.TryGetValue(habitId, out List<PaintingView> bucket))
        {
            bucket.Remove(view);
            if (bucket.Count == 0)
            {
                boundByHabit.Remove(habitId);
                bucket.Clear();
                listPool.Push(bucket);
            }
        }

        acquiredFor.Remove(view);

        view.DetachTarget();
        view.Unbind();

        if (!free.Contains(view))
        {
            free.Add(view);
        }
    }

    // Bir aliskanliga bagli TUM goruntuler. Bir piksel acildiginda hepsine ayni
    // indeks gonderiliyor; liste karti ile detay ekrani boylece senkron kaliyor.
    public bool TryGetViews(string habitId, out List<PaintingView> result)
    {
        return boundByHabit.TryGetValue(habitId, out result);
    }

    private void HandleViewCompleted(PaintingView view)
    {
        PaintingCompleted?.Invoke(view);
    }
}
