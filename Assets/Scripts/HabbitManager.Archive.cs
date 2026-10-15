using System;
using System.Collections.Generic;

// HabbitManager'in arsiv yuzeyi.
//
// NEDEN AYRI PARTIAL: HabbitManager.cs zaten 1175+ satir -- butcenin iki
// katindan fazla. HabbitManager.Rotus.cs'teki AYNI "partial parca, sinif
// icinden private uyelere eris" deseni burada da gecerli: habitsById,
// saveSystem, TryAwardTodayMood/RollBackTodayMood, MarkDirtyDeferred,
// ReleaseViewsFor, RefreshDayIfNeeded private kalir.
//
// Mood el degistirmesi CreateHabit/DeleteHabit ile AYNI kural: arsivlemek
// eksik bir aliskanligi listeden cikarmak gibi gunu mesru sekilde kapatabilir
// (TryAwardTodayMood), arsivden cikarmak yeni bir planli aliskanlik eklemek
// gibi gunu "artik tamamlanmamis" yapabilir (RollBackTodayMood). Ikisi de
// lastMoodSettledDate damgasi sayesinde zaten tekrara kapali.
public partial class HabbitManager
{
    public int ArchivedCount
    {
        get
        {
            int count = 0;
            List<HabitData> habits = saveSystem.Data.habits;

            for (int i = 0; i < habits.Count; i++)
            {
                if (habits[i].isArchived)
                {
                    count++;
                }
            }

            return count;
        }
    }

    // Arsiv durumunu degistirir. Zaten istenen durumdaysa ya da id
    // cozulemiyorsa false doner.
    public bool SetArchived(string habitId, bool archived)
    {
        if (!habitsById.TryGetValue(habitId, out HabitData habit) || habit.isArchived == archived)
        {
            return false;
        }

        // Tarihi tazelemeden arsivlemek, gece yarisini gecmis bir oturumda
        // dunku gune yazardi -- CreateHabit/UpdateHabit ile ayni gerekce.
        RefreshDayIfNeeded();

        DateTime now = AppClock.Now;

        if (archived)
        {
            HabitArchive.Archive(habit);

            // Karo Atolye'den kalkiyor: havuzdaki goruntu geri donmeli, yoksa
            // havuz sizar (HabitTileGrid zaten kapattigi karonun goruntusunu
            // birakiyor, bu emniyet kemeri).
            ReleaseViewsFor(habitId);

            // Eksik olan arsivlenince geriye kalan planli aliskanliklarin
            // hepsi tikliyse gun artik gercekten tamamlandi -- DeleteHabit ile
            // ayni satir, ayni gerekce.
            TryAwardTodayMood(now);
        }
        else
        {
            HabitArchive.Unarchive(habit, now);

            // Yeni bir planli aliskanlik geri donduyse gunun odulu artik
            // mesru degil -- CreateHabit ile ayni satir, ayni gerekce.
            if (HabitScheduling.IsDueOn(habit, now) && !habit.isCompletedToday)
            {
                RollBackTodayMood(now);
            }
        }

        MarkDirtyDeferred();

        // Ikisi de sart: HabitChanged tek karti/karoyu tazeler, ama
        // WorkshopScreen.HandleHabitChanged bir karo EKLEYEMEZ/CIKARAMAZ --
        // yapisal degisim HabitListChanged'le bildirilmeli (02'deki arsiv
        // bolumu, Atolye'deki karo izgarasi, elmas sayaclari).
        HabitChanged?.Invoke(habit);
        HabitListChanged?.Invoke();

        return true;
    }
}
