using System.Collections.Generic;
using UnityEngine;

// "Tablo havuzu": uygulamadaki butun tablolarin tanimli oldugu tek yer.
//
// Arama yollari acilista bir kez kurulup onbellege aliniyor. Tablo secimi ve id
// cozumleme oyun sirasinda cagrildigi icin bu yollarda LINQ ya da gecici liste
// yok -- hepsi onceden ayrilmis koleksiyonlar uzerinde donuyor.
[CreateAssetMenu(fileName = "PaintingLibrary", menuName = "HabitTracker/Painting Library")]
public class PaintingLibrary : ScriptableObject
{
    [SerializeField] private List<PaintingDefinition> paintings = new List<PaintingDefinition>();

    // id -> tanim. activePaintingId'yi cozmek icin.
    private readonly Dictionary<string, PaintingDefinition> byId =
        new Dictionary<string, PaintingDefinition>(64, System.StringComparer.Ordinal);

    // Parca sayisi -> o boyuttaki tablolar. Yeni tablo secerken tum havuzu
    // taramak yerine dogrudan dogru kovaya bakiliyor.
    private readonly Dictionary<int, List<PaintingDefinition>> bySize =
        new Dictionary<int, List<PaintingDefinition>>(4);

    // Secimde yeniden kullanilan aday buffer'i: her cagride yeni liste ayirmamak
    // icin alan olarak duruyor.
    private readonly List<PaintingDefinition> candidateBuffer = new List<PaintingDefinition>(32);

    private bool indexBuilt;

    // Havuzdaki tanimlarin HAM listesi, Inspector'daki siranin aynisi. Galeri
    // ekrani gibi hepsini gezen taraf icin. Dikkat: index kurulurken atlanan
    // bozuk kayitlar (null tanim, id'siz tanim) burada hala duruyor, cunku bu
    // alan serialize edilmis listenin kendisi -- gezen tarafin null gecmesi gerek.
    public IReadOnlyList<PaintingDefinition> All => paintings;

    private static readonly List<PaintingDefinition> EmptyBucket = new List<PaintingDefinition>(0);

    private void OnEnable()
    {
        // ScriptableObject domain reload sonrasi yeniden yuklenir; index'i burada
        // tazelemek Play/Edit gecislerinde bayat kalmasini onluyor.
        indexBuilt = false;
    }

    // Disaridan cagrilmasi gerekmiyor; her arama once EnsureIndex'e ugrar.
    public void BuildIndex()
    {
        byId.Clear();
        bySize.Clear();

        for (int i = 0; i < paintings.Count; i++)
        {
            PaintingDefinition definition = paintings[i];
            if (definition == null || string.IsNullOrEmpty(definition.id))
            {
                Debug.LogWarning($"PaintingLibrary: {i}. sirada bos ya da id'siz tanim var, atlaniyor.", this);
                continue;
            }

            if (byId.ContainsKey(definition.id))
            {
                // Yinelenen id kayitla oynar: galeriye eklenen id baska bir
                // tabloyu isaret edebilir. Sessiz gecmek yerine bagiriyoruz.
                Debug.LogError($"PaintingLibrary: '{definition.id}' id'si birden fazla tabloda kullanilmis.", this);
                continue;
            }

            byId.Add(definition.id, definition);

            int size = definition.PieceCount;
            if (!bySize.TryGetValue(size, out List<PaintingDefinition> bucket))
            {
                bucket = new List<PaintingDefinition>(16);
                bySize.Add(size, bucket);
            }
            bucket.Add(definition);
        }

        indexBuilt = true;
    }

    private void EnsureIndex()
    {
        if (!indexBuilt)
        {
            BuildIndex();
        }
    }

    public bool TryGet(string paintingId, out PaintingDefinition definition)
    {
        EnsureIndex();

        if (string.IsNullOrEmpty(paintingId))
        {
            definition = null;
            return false;
        }

        return byId.TryGetValue(paintingId, out definition);
    }

    // Formun tablo izgarasi icin: TAM o parca sayisindaki tablolar.
    // PickNext'in "en yakin kovaya dus" dususu burada BILEREK yok -- izgara
    // gostermedigi bir boyutu secili yapmamali. Kova yoksa bos liste doner ve
    // form yalnizca Rastgele karesini gosterir (yani bugunku davranis).
    public IReadOnlyList<PaintingDefinition> ForSize(int pieceCount)
    {
        EnsureIndex();
        return bySize.TryGetValue(pieceCount, out List<PaintingDefinition> bucket)
            ? bucket
            : EmptyBucket;
    }

    // Bir aliskanliga sonraki tabloyu secer: canvasSize'a uyan ve o aliskanligin
    // galerisinde HENUZ olmayan tablolar arasindan rastgele biri.
    //
    // Havuz tukendiyse (hepsi bitirilmis) galeri korunur ve havuz bastan
    // kullanilir -- uygulama "tablo kalmadi" diye durmaz, tablolar tekrar boyanir
    // ama galeriye ikinci kez eklenmez.
    //
    // canvasSize'a uyan hic tablo yoksa en yakin boyuta dusuluyor; CAGIRAN TARAF
    // HabitData.canvasSize'i donen tanimin PieceCount'una gore guncellemeli,
    // yoksa "tablo bitti" esigi hic yakalanmaz.
    public PaintingDefinition PickNext(int canvasSize, List<string> excludeGallery)
    {
        EnsureIndex();

        List<PaintingDefinition> bucket = ResolveBucket(canvasSize);
        if (bucket == null || bucket.Count == 0)
        {
            Debug.LogError($"PaintingLibrary: havuzda hic tablo yok (istenen boyut {canvasSize}).", this);
            return null;
        }

        candidateBuffer.Clear();
        for (int i = 0; i < bucket.Count; i++)
        {
            PaintingDefinition definition = bucket[i];
            if (!ListLookup.ContainsOrdinal(excludeGallery, definition.id))
            {
                candidateBuffer.Add(definition);
            }
        }

        // Hepsi bitirilmis: havuzu bastan ac.
        List<PaintingDefinition> pool = candidateBuffer.Count > 0 ? candidateBuffer : bucket;
        return pool[Random.Range(0, pool.Count)];
    }

    // Istenen boyutta kova yoksa en yakin boyuta dusuyoruz: tablo hic
    // bulunamamasindansa farkli boyutta bir tablo vermek yeglenir.
    private List<PaintingDefinition> ResolveBucket(int canvasSize)
    {
        if (bySize.TryGetValue(canvasSize, out List<PaintingDefinition> exact) && exact.Count > 0)
        {
            return exact;
        }

        List<PaintingDefinition> closest = null;
        int closestDistance = int.MaxValue;

        foreach (KeyValuePair<int, List<PaintingDefinition>> pair in bySize)
        {
            if (pair.Value.Count == 0)
            {
                continue;
            }

            int distance = Mathf.Abs(pair.Key - canvasSize);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = pair.Value;
            }
        }

        if (closest != null)
        {
            Debug.LogWarning($"PaintingLibrary: {canvasSize} parcalik tablo yok, en yakin boyuta dusuluyor.", this);
        }

        return closest;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        indexBuilt = false;
    }
#endif
}
