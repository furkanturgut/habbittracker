using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// DevTools'un Inspector yuzu: anahtar, canli ozet ve komut butonlari.
//
// Burada KURAL YOK -- her buton DevTools'un bir metodunu cagiriyor. Editor
// tarafi mood tablosunu, gun donusunu ya da ilerleme matematigini bilmiyor;
// gormesi gereken tek sinif DevTools (o da isi dev servislerine devrediyor).
//
// Alan yazma kurali: dayOffset SerializedProperty uzerinden degistiriliyor
// (undo ve sahne kirlenmesi bedava gelsin diye), DevTools'un alani kendi
// yazdigi durumlarda ise hemen serializedObject.Update() ile geri okunuyor.
// Aksi halde metodun sonundaki ApplyModifiedProperties bayat degeri geri yazardi.
[CustomEditor(typeof(DevTools))]
public partial class DevToolsEditor : Editor
{
    private const string AllTargetsLabel = "Tümü";

    private SerializedProperty devModeProp;
    private SerializedProperty dayOffsetProp;
    private SerializedProperty managerProp;

    // Panelin kendi gecici durumu: bilesene serialize EDILMIYOR, cunku bunlar
    // sahnenin verisi degil, panelin o anki secimi.
    private int targetIndex;
    private float fillRatio = 1f;
    private int streakCurrent = 7;
    private int streakLongest = 7;
    private int missedDays = 1;
    private string jumpDate = string.Empty;

    private readonly List<string> targetNames = new List<string>(16);

    // Canli ozet oynatirken kendiliginden tazelensin.
    public override bool RequiresConstantRepaint()
    {
        return ((DevTools)target).IsLive;
    }

    private void OnEnable()
    {
        devModeProp = serializedObject.FindProperty("devMode");
        dayOffsetProp = serializedObject.FindProperty("dayOffset");
        managerProp = serializedObject.FindProperty("manager");
        jumpDate = HabitScheduling.ToKey(AppClock.Today);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DevTools tools = (DevTools)target;

        DrawSwitch(tools);

        if (!devModeProp.boolValue)
        {
            EditorGUILayout.HelpBox(
                "Dev mode kapalı. Bu bileşen hiçbir şey yapmıyor: saat gerçek tarihte, komutlar reddediliyor.",
                MessageType.None);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        DrawClock(tools);

        if (!tools.IsLive)
        {
            EditorGUILayout.HelpBox(
                "Komutlar yalnızca Play modunda çalışır: kaydı HabbitManager kendi Awake'inde yüklüyor. "
                + "Gün kaydırmasını şimdi yazabilirsin, Play'e girerken uygulanır.",
                MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        DrawStatus(tools);
        DrawMood(tools);
        DrawToday(tools);
        DrawPainting(tools);
        DrawRotus(tools);
        DrawData(tools);
        DrawTransfer(tools);

        serializedObject.ApplyModifiedProperties();
    }

    // -----------------------------------------------------------------------
    // Anahtar
    // -----------------------------------------------------------------------

    private void DrawSwitch(DevTools tools)
    {
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(devModeProp, new GUIContent("Dev Mode"));
        if (EditorGUI.EndChangeCheck())
        {
            // Anahtar kapatilinca saat gercege donmeli, acilinca kaydirma geri
            // gelmeli: ikisi de ApplyClock'un isi.
            ApplyClock(tools);
        }

        EditorGUILayout.PropertyField(managerProp, new GUIContent("Habbit Manager"));

        if (managerProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("HabbitManager bağlı değil.", MessageType.Warning);
            if (GUILayout.Button("Sahnedeki HabbitManager'i bagla"))
            {
                managerProp.objectReferenceValue = FindFirstObjectByType<HabbitManager>();
            }
        }
    }

    // -----------------------------------------------------------------------
    // Tarih
    // -----------------------------------------------------------------------

    private void DrawClock(DevTools tools)
    {
        Section("Tarih");

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(dayOffsetProp, new GUIContent("Gün kaydırması"));
        if (EditorGUI.EndChangeCheck())
        {
            ApplyClock(tools);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("-7"))
        {
            Shift(tools, -7);
        }

        if (GUILayout.Button("-1"))
        {
            Shift(tools, -1);
        }

        if (GUILayout.Button("+1"))
        {
            Shift(tools, 1);
        }

        if (GUILayout.Button("+7"))
        {
            Shift(tools, 7);
        }

        if (GUILayout.Button("+30"))
        {
            Shift(tools, 30);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        jumpDate = EditorGUILayout.TextField("Tarihe git", jumpDate);
        GUI.enabled = HabitScheduling.TryParse(jumpDate, out DateTime target);
        if (GUILayout.Button("Git", GUILayout.Width(50)))
        {
            tools.GoToDate(target);
            SyncAfterCommand();
        }

        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Gerçek tarihe dön"))
        {
            tools.ResetClock();
            SyncAfterCommand();
        }
    }

    // -----------------------------------------------------------------------
    // Canli ozet
    // -----------------------------------------------------------------------

    private void DrawStatus(DevTools tools)
    {
        Section("Durum");

        PlayerSaveData data = tools.Manager.Data;
        DateTime now = AppClock.Now;

        EditorGUILayout.LabelField("Simüle tarih", DateLabel(now));
        EditorGUILayout.LabelField("Gerçek tarih", DateLabel(AppClock.RealNow));
        EditorGUILayout.LabelField(
            "Kaydırma",
            AppClock.IsShifted ? tools.DayOffset.ToString("+#;-#;0") + " gün" : "yok");
        EditorGUILayout.LabelField(
            "Son giriş / mood günü",
            OrDash(data.lastLoginDate) + "  ·  " + OrDash(data.lastMoodSettledDate));

        PainterManager painter = PainterManager.Instance;
        string moodText = painter != null
            ? painter.MoodPoint + "/" + PainterManager.MaxMoodPoint + "  ·  " + MoodPresentation.Label(painter.CurrentMood)
            : "PainterManager sahnede yok";
        EditorGUILayout.LabelField("Mood", moodText + "  (kayıt: " + data.painterMoodPoint + ")");

        DailyProgress.Count(data, now, out int done, out int total);
        EditorGUILayout.LabelField("Bugün tamamlanan", done + "/" + total);

        IReadOnlyList<HabitData> habits = tools.Manager.Habits;
        if (habits.Count == 0)
        {
            EditorGUILayout.LabelField("Alışkanlıklar", "yok");
            return;
        }

        for (int i = 0; i < habits.Count; i++)
        {
            HabitData habit = habits[i];
            EditorGUILayout.LabelField(
                (habit.isCompletedToday ? "+ " : "·  ") + habit.title,
                tools.OpenedPieces(habit) + "/" + habit.canvasSize
                + "  ·  seri " + habit.currentStreak + " (en iyi " + habit.longestStreak + ")"
                + (HabitScheduling.IsDueOn(habit, now) ? string.Empty : "  ·  bugün planlı değil"));
        }
    }

    // -----------------------------------------------------------------------
    // Mood
    // -----------------------------------------------------------------------

    private void DrawMood(DevTools tools)
    {
        Section("Mood");

        PainterManager painter = PainterManager.Instance;
        if (painter == null)
        {
            EditorGUILayout.HelpBox("PainterManager sahnede yok; mood değiştirilemez.", MessageType.Warning);
            return;
        }

        EditorGUI.BeginChangeCheck();
        int point = EditorGUILayout.IntSlider(
            "Mood puanı",
            painter.MoodPoint,
            PainterManager.MinMoodPoint,
            PainterManager.MaxMoodPoint);
        if (EditorGUI.EndChangeCheck())
        {
            tools.SetMoodPoint(point);
        }

        // Dugmelerin sirasi MoodScale'den geliyor (kotuden iyiye): panel ile
        // ekrandaki mood satiri ayni sirada okunsun.
        EditorGUILayout.BeginHorizontal();
        for (int slot = 0; slot < MoodScale.Count; slot++)
        {
            PainterManager.PainterMoods mood = MoodScale.MoodForSlot(slot);
            if (GUILayout.Button(MoodPresentation.Label(mood)))
            {
                tools.SetMood(mood);
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("-1 (ceza yolu)"))
        {
            tools.StepMood(-1);
        }

        if (GUILayout.Button("+1 (ödül yolu)"))
        {
            tools.StepMood(1);
        }

        EditorGUILayout.EndHorizontal();
    }

    // -----------------------------------------------------------------------
    // Bugun
    // -----------------------------------------------------------------------

    private void DrawToday(DevTools tools)
    {
        Section("Bugün");

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Tümünü tikle"))
        {
            tools.CompleteAllDue();
        }

        if (GUILayout.Button("Tikleri kaldır"))
        {
            tools.UncompleteAll();
        }

        EditorGUILayout.EndHorizontal();
    }

    // -----------------------------------------------------------------------
    // Tablo ve seri
    // -----------------------------------------------------------------------

    private void DrawPainting(DevTools tools)
    {
        Section("Tablo ve seri");

        IReadOnlyList<HabitData> habits = tools.Manager.Habits;
        if (habits.Count == 0)
        {
            EditorGUILayout.HelpBox("Listede alışkanlık yok. Aşağıdan örnek veri ekleyebilirsin.", MessageType.Info);
            return;
        }

        targetIndex = EditorGUILayout.Popup("Hedef", Mathf.Clamp(targetIndex, 0, habits.Count), BuildTargetNames(habits));
        fillRatio = EditorGUILayout.Slider("Açık parça oranı", fillRatio, 0f, 1f);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Oranı uygula"))
        {
            ForEachTarget(habits, habit => tools.SetPaintingRatio(habit, fillRatio));
        }

        if (GUILayout.Button("Doldur"))
        {
            ForEachTarget(habits, tools.FillPainting);
        }

        if (GUILayout.Button("Sıfırla"))
        {
            ForEachTarget(habits, tools.ClearPainting);
        }

        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Tabloyu bitir (galeriye at + yenisini ata)"))
        {
            ForEachTarget(habits, tools.FinishPainting);
        }

        EditorGUILayout.BeginHorizontal();
        missedDays = EditorGUILayout.IntField("Kaçırılan gün", missedDays);
        if (GUILayout.Button("Parça yak (gün kaçırıldı)", GUILayout.Width(170)))
        {
            ForEachTarget(habits, habit => tools.MissPainting(habit, missedDays));
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        streakCurrent = EditorGUILayout.IntField("Seri", streakCurrent);
        streakLongest = EditorGUILayout.IntField("En iyi", streakLongest);
        if (GUILayout.Button("Yaz", GUILayout.Width(50)))
        {
            ForEachTarget(habits, habit => tools.SetStreak(habit, streakCurrent, streakLongest));
        }

        EditorGUILayout.EndHorizontal();
    }

    // -----------------------------------------------------------------------
    // Veri
    // -----------------------------------------------------------------------

    private void DrawData(DevTools tools)
    {
        Section("Veri");

        if (GUILayout.Button("Örnek alışkanlıkları ekle"))
        {
            Debug.Log("DevTools: " + tools.SeedSampleHabits() + " ornek aliskanlik eklendi.");
        }

        if (GUILayout.Button("Kaydı sil (sıfırdan başla)")
            && EditorUtility.DisplayDialog(
                "Kaydı sil",
                "Bütün alışkanlıklar ve ilerleme silinecek. Emin misin?",
                "Sil",
                "Vazgeç"))
        {
            tools.DeleteAllData();
        }

    }

    // -----------------------------------------------------------------------
    // Yardimcilar
    // -----------------------------------------------------------------------

    // Hedef "Tumu" ise listenin tamami, degilse tek aliskanlik. Dongu burada
    // duruyor cunku "tumu" panelin bir SECIMI; DevTools'a ikinci bir komut
    // kumesi (...All) eklemek calisma yuzeyini iki katina cikarirdi.
    private void ForEachTarget(IReadOnlyList<HabitData> habits, Action<HabitData> action)
    {
        if (targetIndex > 0)
        {
            action(habits[targetIndex - 1]);
            return;
        }

        // Kopya uzerinde geziliyor: komutlar tablo bitirip olay yayabiliyor ve
        // abonelerin listeyi degistirmesi mumkun.
        HabitData[] snapshot = new HabitData[habits.Count];
        for (int i = 0; i < habits.Count; i++)
        {
            snapshot[i] = habits[i];
        }

        for (int i = 0; i < snapshot.Length; i++)
        {
            action(snapshot[i]);
        }
    }

    private string[] BuildTargetNames(IReadOnlyList<HabitData> habits)
    {
        targetNames.Clear();
        targetNames.Add(AllTargetsLabel);
        for (int i = 0; i < habits.Count; i++)
        {
            targetNames.Add(habits[i].title);
        }

        return targetNames.ToArray();
    }

    private void Shift(DevTools tools, int days)
    {
        tools.ShiftDays(days);
        SyncAfterCommand();
    }

    // dayOffset'i once SerializedProperty'den bilesene indirir, sonra bilesenin
    // kirpmis olabilecegi degeri geri okur.
    private void ApplyClock(DevTools tools)
    {
        serializedObject.ApplyModifiedProperties();
        tools.ApplyClock();
        serializedObject.Update();
    }

    // Alani DevTools'un kendisi yazdiginda: serialize edilmis kopyayi tazele ve
    // sahneyi kirli isaretle (SerializedProperty yolu atlandi).
    private void SyncAfterCommand()
    {
        serializedObject.Update();
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(target);
        }
    }

    private static void Section(string title)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    // Turkce-sabit kaliyor: bu Inspector GUI'si yalnizca gelistiricinin kendi
    // makinesinde goruluyor (LOCALIZATION_PLAN.md kapsami DISINDA), WeekDays'in
    // artik Loc.Tr'a baglanan ShortLabel'ina bagimli olmasina gerek yok --
    // Loc burada (sahne/Play modu olmadan) initialized olmayabilir.
    private static readonly string[] DevShortLabels = { "Pt", "Sa", "Ça", "Pe", "Cu", "Ct", "Pz" };

    private static string DateLabel(DateTime date)
    {
        return HabitScheduling.ToKey(date)
            + " (" + DevShortLabels[WeekDays.ToMondayIndex(date.DayOfWeek)] + ")";
    }

    private static string OrDash(string value)
    {
        return string.IsNullOrEmpty(value) ? "-" : value;
    }
}
