using System;

// HabbitManager'in rotus (yanik parca onarimi) yuzeyi.
//
// NEDEN AYRI PARTIAL: HabbitManager.cs zaten 1150+ satir -- butcenin iki
// katindan fazla. Dev/HabbitManager.Dev.cs'teki ayni "partial parca, sinif
// icinden private uyelere eris" deseni burada da gecerli: RebindViewsFor,
// MarkDirtyDeferred, saveSystem private kalir, HabbitManager.cs yalnizca
// CompleteHabit/UncompleteHabit/Load/DeleteAllData'daki birkac satirla buyur.
//
// Odul/geri-alma cifti TryAwardTodayMood/RollBackTodayMood ile AYNI sekil:
// odul yalnizca stamp/sayac uzerinden verilir, geri alma da ayni sayaci
// tersine cevirir -- ikinci bir "zaten odullendirildi mi" kontrolune gerek
// yok cunku RotusCycle'in kendisi (rotusProgress) o damgadir.
public partial class HabbitManager
{
    public int RotusCount => saveSystem.Data.rotusCount;

    // Sayac degisti (gosterim: pill, rozet, cip hepsi buna tepki verir).
    public event Action<int> RotusChanged;

    // Tam donguyu doldurup yeni bir rotus kazanildi (toast icin).
    public event Action<HabitData> RotusEarned;

    // Bir rotus harcanip bir parca onarildi: habit, paintingId, onarilan parca indeksi.
    public event Action<HabitData, string, int> PieceRestored;

    // Bir rotus harcar: stoktan biri duser, belirtilen bitmis tablonun ILK
    // deligi kapanir. Stok 0 ise ya da o tabloda delik yoksa false doner ve
    // hicbir sey degismez.
    public bool UseRotus(string habitId, string paintingId)
    {
        if (saveSystem.Data.rotusCount <= 0)
        {
            return false;
        }

        if (!TryGetHabit(habitId, out HabitData habit))
        {
            return false;
        }

        int restoredPiece = HabitGallery.RestoreOne(habit, paintingId);
        if (restoredPiece < 0)
        {
            return false;
        }

        saveSystem.Data.rotusCount--;

        // Delik listesi degisti: onbellekteki baked kompozit artik bayat.
        // Imza-tabanli oldugu icin bir sonraki SpriteFor kendiliginden yeniden
        // pisirirdi, ama harcanan bir sarf malzemesinde aninda garanti istiyoruz.
        BurnedPaintingCache.Forget(habitId);

        MarkDirtyDeferred();
        FlushNow();

        RotusState.Apply(saveSystem.Data.rotusCount);
        RotusChanged?.Invoke(saveSystem.Data.rotusCount);
        PieceRestored?.Invoke(habit, paintingId, restoredPiece);
        HabitChanged?.Invoke(habit);

        HapticFeedback.Play(HapticCue.Medium);
        SfxPlayer.Play(SfxCue.RotusApplied);

        return true;
    }

    // CompleteHabit'in cagirdigi odul yarisi: donguyu bir ilerletir, dolduysa
    // stogu artirir. Donen deger "bu tikte yeni bir rotus kazanildi mi".
    private bool TryAwardRotus(HabitData habit)
    {
        if (!RotusCycle.Advance(habit))
        {
            return false;
        }

        saveSystem.Data.rotusCount++;
        RotusState.Apply(saveSystem.Data.rotusCount);
        RotusChanged?.Invoke(saveSystem.Data.rotusCount);

        SfxPlayer.Play(SfxCue.RotusEarned);

        return true;
    }

    // UncompleteHabit'in cagirdigi geri-alma yarisi: donguyu bir geri alir.
    // Sarmayla odul geri alinmissa VE o odul hala stokta duruyorsa sayac bir
    // azalir -- arada harcanmis olabilir, stok asla eksiye inmez.
    private void RollBackRotus(HabitData habit)
    {
        if (!RotusCycle.RollBack(habit))
        {
            return;
        }

        if (saveSystem.Data.rotusCount <= 0)
        {
            return;
        }

        saveSystem.Data.rotusCount--;
        RotusState.Apply(saveSystem.Data.rotusCount);
        RotusChanged?.Invoke(saveSystem.Data.rotusCount);
    }

    // --- Dev kapilari (bkz. Dev/HabbitManager.Dev.cs'in ayni gerekcesi) ---

    internal void DevGrantRotus(int amount)
    {
        if (amount == 0)
        {
            return;
        }

        DevRotusTools.Grant(saveSystem.Data, amount);
        MarkDirtyDeferred();

        RotusState.Apply(saveSystem.Data.rotusCount);
        RotusChanged?.Invoke(saveSystem.Data.rotusCount);
    }

    internal void DevSetRotusProgress(HabitData habit, int progress)
    {
        DevRotusTools.SetProgress(habit, progress);
        MarkDirtyDeferred();
    }
}
