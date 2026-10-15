using System;
using System.Collections.Generic;
using UnityEngine;

// Kaydin serialize/deserialize ve "ne zaman yazilir" sorumlulugu.
//
// JsonUtility.ToJson bir string, PlayerPrefs.SetString bir kopya uretir -- bu
// yol dogasi geregi TAM GC-free olamaz. Onun yerine su garanti ediliyor:
//   * Acilista tek okuma + tek FromJsonOverwrite.
//   * Yazma istekleri BIRLESTIRILIR: MarkDirty sadece bayrak kaldirir, gercek
//     serialize yalnizca FlushIfDirty'de olur. Art arda on tik tek yazmaya doner.
//   * Kirli degilken FlushIfDirty hicbir sey yapmaz: sifir is, sifir tahsis.
public class HabitSaveSystem
{
    private readonly IHabitStorage storage;

    private bool dirty;

    // Kayit nesnesi bir kez ayrilip uzerine yaziliyor (FromJsonOverwrite): her
    // yuklemede yeni bir nesne grafigi kurmaktansa var olani tazelemek hem daha
    // az cop uretir hem de disaridaki referanslari gecerli birakir.
    public PlayerSaveData Data { get; private set; } = new PlayerSaveData();

    public bool IsDirty => dirty;

    public HabitSaveSystem(IHabitStorage storage)
    {
        this.storage = storage;
    }

    // Diskten okur. Kayit yoksa ya da bozuksa bos ama gecerli bir kayitla doner --
    // cagiran tarafin null kontrolu yapmasi gerekmiyor. Kaydin yeni mi
    // olusturuldugunu doner (ilk acilis tespiti icin).
    public bool Load()
    {
        string json = storage.Load();

        if (string.IsNullOrEmpty(json))
        {
            ResetToEmpty();
            return true;
        }

        if (!ApplyJson(json))
        {
            ResetToEmpty();
            return true;
        }

        return false;
    }

    // Data'nin YERINE koyar -- ice aktarmanin "tamamen degistir" semantigi.
    // ResetToEmpty() ONCE cagriliyor: JsonUtility.ToJson bos listeleri yazmaz,
    // yani incoming'in habits'i bos/null ise FromJsonOverwrite o alana hic
    // DOKUNMAZ -- onceden temizlenmezse 5 aliskanlikli bir cihaza bos bir
    // yedek yuklemek hicbir seyi SILMEZ. Data nesne kimligi KORUNUR
    // (disaridaki referanslar gecerli kalir): ikisi de AYNI nesneyi
    // mutasyona ugratiyor.
    public void ReplaceWith(PlayerSaveData incoming)
    {
        ResetToEmpty();
        ApplyJson(JsonUtility.ToJson(incoming));
        dirty = true;
    }

    // Load() ve ReplaceWith'in PAYLASTIGI govde: biri disk/pano kaynakli
    // guvenilmez JSON'u, digeri kendi ToJson ciktisini ayristiriyor ama ikisi
    // de ayni onarim pass'inden gecmeli. Basarisizsa false doner, cagiran
    // taraf ResetToEmpty'e duser.
    private bool ApplyJson(string json)
    {
        try
        {
            JsonUtility.FromJsonOverwrite(json, Data);
        }
        catch (Exception exception)
        {
            // Bozuk kayitla acilmak, hic acilmamaktan iyidir. Kullanicinin verisi
            // gider ama uygulama calisir; sessiz gecmemek icin log.
            Debug.LogError($"HabitSaveSystem: kayit okunamadi, sifirdan baslaniyor. {exception.Message}");
            return false;
        }

        if (Data.habits == null)
        {
            Data.habits = new List<HabitData>();
        }

        RepairHabits();
        RepairSettings();
        return true;
    }

    // Bir sonraki flush'ta yazilmak uzere isaretler. Cagirmasi bedava.
    public void MarkDirty()
    {
        dirty = true;
    }

    // Kirliyse serialize edip yazar. flushToDisk yalnizca guvenli noktalarda
    // (arka plana alma, cikis) true verilmeli: senkron I/O.
    public void FlushIfDirty(bool flushToDisk)
    {
        if (!dirty)
        {
            return;
        }

        string json = JsonUtility.ToJson(Data);
        storage.Write(json);

        if (flushToDisk)
        {
            storage.Flush();
        }

        dirty = false;
    }

    // Kirli olmasa bile yazar ve diske indirir.
    public void SaveNow()
    {
        dirty = true;
        FlushIfDirty(true);
    }

    public void DeleteAll()
    {
        storage.Delete();
        ResetToEmpty();
        dirty = false;
    }

    private void ResetToEmpty()
    {
        Data.saveVersion = PlayerSaveData.CurrentSaveVersion;
        Data.lastLoginDate = null;
        Data.painterMoodPoint = PainterManager.DefaultMoodPoint;
        Data.lastMoodSettledDate = null;
        Data.rotusCount = 0;
        Data.settings = new AppSettingsData();

        if (Data.habits == null)
        {
            Data.habits = new List<HabitData>();
        }
        else
        {
            Data.habits.Clear();
        }
    }

    // JsonUtility bos bir listeyi JSON'a yazmaz ve okurken de alana dokunmaz;
    // eski ya da elle duzenlenmis bir kayitta liste alanlari null gelebilir.
    // Bunlari burada bir kez onarmak, ilerideki her kullanim noktasindan null
    // kontrolu kaldiriyor -- YENI List<> alanlari buraya da eklenmeli.
    private void RepairHabits()
    {
        // Elle duzenlenmis bir kayit sayaci eksiye dusurebilir; bu, rotus
        // donguyu erisilmez bir borca cevirirdi.
        if (Data.rotusCount < 0)
        {
            Data.rotusCount = 0;
        }

        for (int i = 0; i < Data.habits.Count; i++)
        {
            HabitData habit = Data.habits[i];

            if (habit == null)
            {
                Data.habits.RemoveAt(i);
                i--;
                continue;
            }

            if (habit.frequency == null)
            {
                habit.frequency = new List<DayOfWeek>();
            }

            if (habit.randomOpenedPixels == null)
            {
                habit.randomOpenedPixels = new List<int>();
            }

            if (habit.habitGallery == null)
            {
                habit.habitGallery = new List<string>();
            }

            if (habit.missedPixels == null)
            {
                habit.missedPixels = new List<int>();
            }

            if (habit.galleryHoles == null)
            {
                habit.galleryHoles = new List<PaintingHoles>();
            }

            for (int h = 0; h < habit.galleryHoles.Count; h++)
            {
                if (habit.galleryHoles[h] == null)
                {
                    habit.galleryHoles.RemoveAt(h);
                    h--;
                    continue;
                }

                if (habit.galleryHoles[h].pieces == null)
                {
                    habit.galleryHoles[h].pieces = new List<int>();
                }
            }

            if (habit.canvasSize <= 0)
            {
                habit.canvasSize = HabitData.DefaultCanvasSize;
            }

            if (habit.rotusProgress < 0 || habit.rotusProgress >= RotusCycle.CycleLength)
            {
                habit.rotusProgress = Mathf.Clamp(habit.rotusProgress, 0, RotusCycle.CycleLength - 1);
            }
        }
    }

    // RepairHabits'in ayar tarafindaki ikizi: eski bir kayitta (bu alan hic
    // yokken yazilmis) ya da elle bozulmus bir JSON'da settings null gelebilir,
    // ya da ic alanlari bos/gecersiz olabilir. Hicbiri uygulamayi kilitlememeli --
    // her biri kendi uretim-esdegeri varsayilanina duser.
    private void RepairSettings()
    {
        if (Data.settings == null)
        {
            Data.settings = new AppSettingsData();
            return;
        }

        if (string.IsNullOrWhiteSpace(Data.settings.painterName))
        {
            Data.settings.painterName = AppSettingsData.DefaultPainterName;
        }

        if (!TimeOfDay.IsValid(Data.settings.reminderTime))
        {
            Data.settings.reminderTime = AppSettingsData.DefaultReminderTime;
        }

        RepairCustomCategories();
        RepairFilterTokens();
    }

    // Eski kayitta hic yoktu (null), elle duzenlenmis bir JSON'da gecersiz/
    // yinelenen/sinir-disi ad tasiyabilir. Her biri kendi kuralindan (bkz.
    // HabitCategories.NormalizeCustom) gecirilip elenir -- UI'dan gecmeyen
    // bir deger de uygulamayi kilitlememeli.
    private void RepairCustomCategories()
    {
        if (Data.settings.customCategories == null)
        {
            Data.settings.customCategories = new List<string>();
            return;
        }

        List<string> custom = Data.settings.customCategories;
        List<string> cleaned = new List<string>(custom.Count);

        for (int i = 0; i < custom.Count; i++)
        {
            if (cleaned.Count >= HabitCategories.MaxCustom)
            {
                break;
            }

            string normalized = HabitCategories.NormalizeCustom(custom[i], cleaned);
            if (normalized.Length > 0)
            {
                cleaned.Add(normalized);
            }
        }

        Data.settings.customCategories = cleaned;
    }

    // RepairCustomCategories'in filtre tarafindaki ikizi. Bos liste MESRU bir
    // durum degil (Tumu her zaman var olmali), bu yuzden null VE bos ayni
    // sekilde varsayilana duser -- HabitFilters.Collect zaten gecersiz/
    // yinelenen/asiri token'i kendi basina elese de, kayittaki listeyi hic
    // dokunulmadan tasimak yerine burada bir kez temizlemek daha tutarli.
    private void RepairFilterTokens()
    {
        if (Data.settings.filterTokens == null || Data.settings.filterTokens.Count == 0)
        {
            Data.settings.filterTokens = new List<string>(HabitFilters.DefaultTokens);
        }
    }
}
