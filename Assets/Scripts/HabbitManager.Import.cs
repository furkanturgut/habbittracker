using System;
using System.Collections.Generic;

// HabbitManager'in ice aktarma (geri yukleme) yuzeyi.
//
// NEDEN AYRI PARTIAL: HabbitManager.cs zaten butce disi -- Rotus.cs/Archive.cs
// ile ayni "partial parca, sinif icinden private uyelere eris" deseni:
// saveSystem, ReleaseViewsFor, ApplyLoadedSave, ApplyMood, pendingMoodSteps,
// CancelPendingFlush hepsi private kalir.
//
// SaveTransfer.TryRead + Sanitize DEPOYA HICBIR SEY YAZILMADAN ONCE calisir --
// basarisiz bir ice aktarma mevcut kaydi HIC ETKILEMEMELI.
public partial class HabbitManager
{
    // json bir onceki disa aktarmanin (ya da elle yapistirilan) zarfi.
    // Basarisizsa false doner ve error Turkce bir aciklama tasir -- mevcut
    // kayda HICBIR SEY dokunulmamis olur. Basarili olursa kayit TAMAMEN
    // DEGISTIRILIR (birlestirme yok -- bkz. EXPORT_IMPORT_PLAN.md).
    public bool TryImportSave(string json, out string error)
    {
        if (!SaveTransfer.TryRead(json, out SaveTransferPackage package, out error))
        {
            return false;
        }

        DateTime now = AppClock.Now;
        SaveTransfer.Sanitize(package.data, now);

        // isPremium pakete GIRMEDI (SaveTransfer.Build'in redaksiyonu) --
        // cihazdaki mevcut deger KORUNUR, pakettekine (hep false) bakilmaz.
        // Boylece odeme yapmis bir kullanici kendi yedegini geri yukleyince
        // premium'unu kaybetmez.
        package.data.settings.isPremium = Settings.isPremium;

        CancelPendingFlush();

        // Goruntuler KAYITTAN ONCE birakilmali -- DeleteAllData'nin ayni
        // gerekcesi: birakilmazsa artik var olmayan aliskanliklara bagli
        // kalip 12'lik havuzu tuketirler.
        List<HabitData> existing = saveSystem.Data.habits;
        for (int i = 0; i < existing.Count; i++)
        {
            ReleaseViewsFor(existing[i].id);
        }

        // Delik listesi (missedPixels/galleryHoles) toptan degisiyor; onbellek
        // yalnizca missedPixels imzasina bakiyor -- AYNI habitId farkli
        // icerikle gelebilir, bayat pisirilmis sprite birakilmamali.
        BurnedPaintingCache.Clear();

        saveSystem.ReplaceWith(package.data);
        ApplyLoadedSave(now);

        if (PainterManager.Instance != null)
        {
            PainterManager.Instance.RestoreMoodPoint(saveSystem.Data.painterMoodPoint);
        }

        // Load()'un aksine burada Start() bir daha calismayacak (oturum zaten
        // acik): ceza hemen harcanmazsa sonsuza kadar askida kalir.
        if (pendingMoodSteps > 0)
        {
            ApplyMood(-pendingMoodSteps);
            pendingMoodSteps = 0;
        }

        // Ice aktarma debounce'a birakilmaz -- aninda diske iner.
        saveSystem.SaveNow();

        SettingsChanged?.Invoke();
        RotusChanged?.Invoke(saveSystem.Data.rotusCount);

        // Yapi EN SON degisir: ekranlar zaten tazelenmis ayar/aynalara karsi
        // yeniden kurulsun (bkz. CLAUDE.md > "HabitListChanged is structure").
        HabitListChanged?.Invoke();

        return true;
    }
}
