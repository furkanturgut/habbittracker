using UnityEngine;

// Rotus duzeyindeki dev komutlari: stogu elle ver/sifirla, bir aliskanligin
// 7 tiklik dongu sayacini elle oturt.
//
// DevHabitTools/DevPaintingTools ile AYNI "saf cekirdek + sahneli ince sarmal"
// bolunmesi: saf yarilar Tests/RotusTests.cs'te sahnesiz test edilir, sahneli
// yarilar HabbitManager'in internal kapilarindan (bkz. HabbitManager.Rotus.cs)
// gecer ki bildirim (RotusChanged, MarkDirtyDeferred) tek bir yerde kalsin.
public static class DevRotusTools
{
    // --- Saf kisim ---

    public static void SetCount(PlayerSaveData data, int count)
    {
        if (data == null)
        {
            return;
        }

        data.rotusCount = Mathf.Max(0, count);
    }

    public static void Grant(PlayerSaveData data, int amount)
    {
        if (data == null)
        {
            return;
        }

        SetCount(data, data.rotusCount + amount);
    }

    public static void SetProgress(HabitData habit, int progress)
    {
        if (habit == null)
        {
            return;
        }

        habit.rotusProgress = Mathf.Clamp(progress, 0, RotusCycle.CycleLength - 1);
    }

    // --- Sahneli kisim ---

    public static void Grant(HabbitManager manager, int amount)
    {
        if (manager == null)
        {
            return;
        }

        manager.DevGrantRotus(amount);
    }

    public static void SetProgress(HabbitManager manager, HabitData habit, int progress)
    {
        if (manager == null || habit == null)
        {
            return;
        }

        manager.DevSetRotusProgress(habit, progress);
    }
}
