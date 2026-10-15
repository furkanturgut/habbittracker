using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Bir Image uzerinde rastgele bir kare dizisini donduren genel oynatici.
//
// FlameSprite ile AYNI dongu seklini kullanir (unscaled time, elapsed'i
// sifirlamadan dusme, OnEnable/OnDisable), ama FlameSprite'in aksine alev'e
// OZEL degil: kareler UiTheme'den degil, dogrudan cagiran tarafindan
// gecirilir. FlameSprite'i genisletmek yerine ayri bir bilesen olmasinin
// sebebi: FlameSprite'in kare suresi private const ve kareleri
// UiTheme.FlameFrames'ten kendisi cozuyor -- rotus gibi alevle ilgisiz bir
// kullanim icin o varsayimlari sokmek, alev oynaticisini gereksiz
// genellestirirdi.
[DisallowMultipleComponent]
public class SpriteFrameLoop : MonoBehaviour
{
    [SerializeField] private Image image;

    private Sprite[] frames;
    private float frameSeconds = 0.1f;
    private Coroutine routine;

    private void Awake()
    {
        if (image == null)
        {
            image = GetComponent<Image>();
        }
    }

    private void OnEnable()
    {
        // Kapaninca coroutine durur, acilinca MEVCUT kareden devam eder --
        // FlameSprite.OnEnable'in ayni gerekcesi.
        StartLoop();
    }

    private void OnDisable()
    {
        StopLoop();
    }

    // Ayni dizi + sure zaten uygulanmissa dokunmaz (FlameSprite.SetTier'in
    // idempotentlik kuralinin aynisi) -- aksi halde her tazeleme kareyi 0'a
    // sardirir ve dongu donmus gibi gorunur.
    public void Show(Sprite[] newFrames, float newFrameSeconds)
    {
        if (ReferenceEquals(frames, newFrames) && Mathf.Approximately(frameSeconds, newFrameSeconds))
        {
            return;
        }

        frames = newFrames;
        frameSeconds = newFrameSeconds;

        if (image != null)
        {
            bool ready = frames != null && frames.Length > 0;
            image.enabled = ready;

            if (ready)
            {
                image.sprite = frames[0];
            }
        }

        StartLoop();
    }

    public void Stop()
    {
        frames = null;
        StopLoop();

        if (image != null)
        {
            image.enabled = false;
        }
    }

    private void StartLoop()
    {
        StopLoop();

        if (!isActiveAndEnabled || frames == null || frames.Length <= 1 || image == null)
        {
            return;
        }

        routine = StartCoroutine(Animate());
    }

    private void StopLoop()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    // unscaledDeltaTime: FlameSprite'in ayni karari -- UI animasyonlari
    // Time.timeScale'den bagimsiz kalmali.
    private IEnumerator Animate()
    {
        float elapsed = 0f;
        int frame = 0;

        while (true)
        {
            elapsed += Time.unscaledDeltaTime;

            if (elapsed >= frameSeconds)
            {
                elapsed -= frameSeconds * Mathf.Floor(elapsed / frameSeconds);

                frame = (frame + 1) % frames.Length;
                image.sprite = frames[frame];
            }

            yield return null;
        }
    }
}
