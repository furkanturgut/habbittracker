using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

// Sahnedeki ressam karakteri: dokunmaya tepki verir, moda gore animator
// controller'ini degistirir. Puanin sahibi degil -- puan PainterManager'da,
// burasi yalnizca gorsel karsiligi.
[RequireComponent(typeof(Collider2D))]
public class Painter : MonoBehaviour
{
    public PainterManager.PainterMoods currentMood = PainterManager.PainterMoods.Happy;

    [Header("Tiklama")]
    [Tooltip("Tiklaninca tetiklenecek Animator trigger adi (painterController: hit / paint).")]
    [SerializeField] private string clickTriggerName = "hit";
    [SerializeField] private string paintTriggerName = "paint";
    [Tooltip("Kapaliyken tiklama tamamen yok sayilir (ornegin menu acikken).")]
    [SerializeField] private bool clickEnabled = true;
    [Tooltip("Bos birakilirsa Camera.main kullanilir.")]
    [SerializeField] private Camera inputCamera;

    [Header("Vurus Efekti")]
    [Tooltip("Hit tetiklendiginde dokunulan noktada beliren kisa ofke patlamasi.")]
    [SerializeField] private AngerBurstVfx angerVfxPrefab;

    // Mood basina bir controller. FormerlySerializedAs SART: bu alanlar buyuk
    // harfle basliyordu ve sahnedeki atamalar o adlara yazilmis durumda --
    // eski ad belirtilmezse Unity atamalari bulamaz ve controller'lar bosalir.
    [Header("Mood Animasyon Controller Overridelari")]
    [Tooltip("Mood switchlerinde dogru animasyon calismasi icin controller atamalari")]
    [FormerlySerializedAs("UltraRichAnimator")]
    [SerializeField] private AnimatorOverrideController ultraRichAnimator;
    [FormerlySerializedAs("RichAnimator")]
    [SerializeField] private AnimatorOverrideController richAnimator;
    [FormerlySerializedAs("HappyAnimator")]
    [SerializeField] private AnimatorOverrideController happyAnimator;
    [SerializeField] private RuntimeAnimatorController normalAnimator;
    [FormerlySerializedAs("SadAnimator")]
    [SerializeField] private AnimatorOverrideController sadAnimator;
    [FormerlySerializedAs("StarveAnimator")]
    [SerializeField] private AnimatorOverrideController starveAnimator;

    private Animator anim;
    private Collider2D bodyCollider;
    private Camera cachedCamera;
    private int clickTriggerHash;
    private int paintTriggerHash;

    // Basma painter uzerinde mi baslamisti? Mobilde ekrani suruklerken parmagin
    // painterin uzerinden gecmesi tiklama sayilmasin diye takip ediliyor.
    private bool pressStartedOnPainter;

    // IsPointerOnPainter'in birakma anindaki overlap sorgusunda urettigi dunya
    // noktasi: HandleClick VFX'i TAM bu noktada dogursun diye saklaniyor,
    // ayni noktayi ikinci kez hesaplamak (ve kameradan farkli bir karede
    // sapmasina izin vermek) yerine.
    private Vector3 lastTapWorldPoint;

    private void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        bodyCollider = GetComponent<Collider2D>();
        clickTriggerHash = Animator.StringToHash(clickTriggerName);
        paintTriggerHash = Animator.StringToHash(paintTriggerName);
    }

    private void Start()
    {
        // Manager sahneler arasi yasiyor, painter yasamiyor: her sahne acilisinda
        // kendimizi tanitiyoruz. Awake'te degil Start'ta, cunku Awake sirasi
        // garanti degil -- Start calistiginda manager'in Awake'i kesin bitmistir.
        if (PainterManager.Instance != null)
        {
            PainterManager.Instance.RegisterPainter(this);
        }
    }

    private void Update()
    {
        if (!clickEnabled)
        {
            return;
        }

        // Pointer.current hem Mouse hem Touchscreen'i kapsar; PC testinde mouse,
        // telefonda dokunmatik ayni kod yolundan gecer.
        Pointer pointer = Pointer.current;
        if (pointer == null)
        {
            return;
        }

        if (pointer.press.wasPressedThisFrame)
        {
            pressStartedOnPainter = !IsPointerOverUI() && IsPointerOnPainter(pointer);
        }
        else if (pointer.press.wasReleasedThisFrame)
        {
            // Basma ve birakma ayni collider uzerindeyse gercek bir tap sayilir.
            if (pressStartedOnPainter && IsPointerOnPainter(pointer))
            {
                OnPainterClicked();
            }

            pressStartedOnPainter = false;
        }
    }

    // Painter'a tiklandiginda cagrilir. Disaridan (test, buton, debug) elle de
    // cagrilabilir.
    public void OnPainterClicked()
    {
        SetTrigger(clickTriggerHash);
        HandleClick();
    }

    // Boyama animasyonunu oynatir. PainterManager mood yukseldiginde cagiriyor.
    public void MakePainting()
    {
        SetTrigger(paintTriggerHash);
        HandlePainting();
    }

    // Moda karsilik gelen animator controller'a gecer.
    public void SwitchMood(PainterManager.PainterMoods mood)
    {
        currentMood = mood;

        RuntimeAnimatorController controller = ResolveController(mood);
        if (anim != null && controller != null)
        {
            anim.runtimeAnimatorController = controller;
        }
    }

    // Tiklamayi acar/kapatir: menu ya da dialog acikken painter'a dokunmak tepki
    // vermesin diye. Property degil metot olmasi bilincli -- Unity Inspector'unda
    // bir Button'un onClick'ine yalnizca metotlar baglanabiliyor.
    //
    // Kapatirken yarim kalmis basma da temizleniyor: parmak painter uzerinde
    // basiliyken menu acilirsa, menu kapandiginda o eski basma birakma anini
    // gecerli bir tap sayardi.
    public void SetClickEnabled(bool value)
    {
        clickEnabled = value;
        if (!value)
        {
            pressStartedOnPainter = false;
        }
    }

    // Tiklama tepkisi: iki katmanli ses (tokat + tepki) ve dokunulan noktada
    // kisa bir ofke patlamasi. Animasyon zaten tetiklendi (OnPainterClicked).
    private void HandleClick()
    {
        SfxPlayer.Play(SfxCue.Slap);
        SfxPlayer.Play(SfxCue.SlapReaction);

        // VFX Studio kokune (ressamin kendi ebeveyni) parentlanir ki
        // StudioStage'in tasarim-pikseli olcegini devralsin -- Painter'in
        // KENDI transform'una parentlamak, StudioPixelSprite'in ressama ozel
        // 160x'lik (zoom*sourcePixelsPerUnit) olcegini de devralip VFX'i
        // dev buyutude gosterirdi.
        Transform vfxParent = transform.parent != null ? transform.parent : transform;
        AngerBurstVfx.SpawnAt(angerVfxPrefab, lastTapWorldPoint, vfxParent);
    }

    // Boyama tepkisi buraya: ses, partikul.
    private void HandlePainting()
    {
    }

    private RuntimeAnimatorController ResolveController(PainterManager.PainterMoods mood)
    {
        switch (mood)
        {
            case PainterManager.PainterMoods.UltraRich: return ultraRichAnimator;
            case PainterManager.PainterMoods.Rich: return richAnimator;
            case PainterManager.PainterMoods.Happy: return happyAnimator;
            case PainterManager.PainterMoods.Normal: return normalAnimator;
            case PainterManager.PainterMoods.Sad: return sadAnimator;
            case PainterManager.PainterMoods.Starve: return starveAnimator;
            default: return normalAnimator;
        }
    }

    private void SetTrigger(int triggerHash)
    {
        if (anim != null)
        {
            anim.SetTrigger(triggerHash);
        }
    }

    private bool IsPointerOnPainter(Pointer pointer)
    {
        Camera cam = ResolveCamera();
        if (cam == null || bodyCollider == null)
        {
            return false;
        }

        Vector2 screenPosition = pointer.position.ReadValue();
        Vector3 screenPoint = new Vector3(screenPosition.x, screenPosition.y,
            Mathf.Abs(cam.transform.position.z - transform.position.z));
        Vector3 worldPoint = cam.ScreenToWorldPoint(screenPoint);

        // Tek collider sorgusu: Physics2D raycast ayarlarindan (trigger/layer)
        // bagimsiz calisir ve tahsis yapmaz.
        bool hit = bodyCollider.OverlapPoint(worldPoint);
        if (hit)
        {
            // Birakma anindaki noktayi sakliyoruz: basma anindaki cagri da ayni
            // metottan gecer ama o noktayi OnPainterClicked hic okumaz, bu
            // yuzden basmada ustune yazilmasi zararsiz -- gecerli bir tap
            // sayilan son cagri her zaman birakma anidir.
            lastTapWorldPoint = worldPoint;
        }

        return hit;
    }

    private Camera ResolveCamera()
    {
        if (inputCamera != null)
        {
            return inputCamera;
        }

        // Painter, manager ile birlikte sahne degisimine dayanmadigi icin eski
        // kamera referansi olu kalabilir; null olunca yeniden bulunur.
        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }

        return cachedCamera;
    }

    private static bool IsPointerOverUI()
    {
        // UI (buton, panel) tiklamasi painter'a gecmesin. Sahnede EventSystem
        // yoksa kontrol atlanir.
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
