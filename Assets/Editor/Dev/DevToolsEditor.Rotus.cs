using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// DevToolsEditor.cs'in rotus bolumu -- ana dosya 500 satir sinirina
// dayandigi icin ayri partial parca (HabbitManager.Rotus.cs'in ayni
// gerekcesi). ForEachTarget/Section gibi ozel yardimcilara ayni sinifin
// parcasi oldugu icin dogrudan erisiyor.
public partial class DevToolsEditor
{
    private void DrawRotus(DevTools tools)
    {
        Section("Rötuş");

        EditorGUILayout.LabelField("Stok", tools.Manager.RotusCount.ToString());

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+1"))
        {
            tools.GrantRotus(1);
        }

        if (GUILayout.Button("Sıfırla"))
        {
            tools.ResetRotus();
        }

        EditorGUILayout.EndHorizontal();

        IReadOnlyList<HabitData> habits = tools.Manager.Habits;
        if (habits.Count == 0)
        {
            return;
        }

        // "Tablo ve seri" bolumunun targetIndex'ini paylasiyor -- ikisi de
        // ayni "hedef aliskanlik" secimine bakiyor.
        if (GUILayout.Button("Döngüyü doldur (sıradaki tik rötuş verir)"))
        {
            ForEachTarget(habits, tools.FillRotusCycle);
        }
    }
}
