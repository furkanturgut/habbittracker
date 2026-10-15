using UnityEditor;
using UnityEngine;
#if HABIT_NOTIFICATIONS
using Unity.Notifications;
#endif

// Editor menusundeki tek seferlik bakim komutlari.
public static class HabitTrackerMenu
{
    [MenuItem("HabitTracker/Delete Saved Player Data")]
    public static void DeleteSave()
    {
        new PlayerPrefsHabitStorage().Delete();
        Debug.Log("HabitTracker: kayitli oyuncu verisi silindi.");
    }

    // Dev panelini sahneye takar. Bilesen HabbitManager'in yanina (HabitSystem)
    // gidiyor: ikisi ayni GameObject'te oldugu surece baglanti kaybolmaz ve
    // DevTools'un DefaultExecutionOrder'i saati yoneticinin Awake'inden once
    // kurar.
    //
    // Sahne BILEREK kaydedilmiyor -- PainterHabitsBuilder ile ayni kalip:
    // degisikligi gorup kendisi kaydetmek kullanicinin karari.
    [MenuItem("HabitTracker/Add Dev Tools")]
    public static void AddDevTools()
    {
        HabbitManager manager = Object.FindFirstObjectByType<HabbitManager>();
        if (manager == null)
        {
            Debug.LogError("HabitTracker: sahnede HabbitManager yok, dev panel eklenemedi. SampleScene acik mi?");
            return;
        }

        DevTools tools = Object.FindFirstObjectByType<DevTools>();
        if (tools == null)
        {
            tools = Undo.AddComponent<DevTools>(manager.gameObject);
            Debug.Log("HabitTracker: dev panel '" + manager.gameObject.name + "' uzerine eklendi.", tools);
        }
        else
        {
            Debug.Log("HabitTracker: dev panel zaten sahnede.", tools);
        }

        UiWiring.Set(tools, "manager", manager);

        Selection.activeGameObject = tools.gameObject;
        EditorGUIUtility.PingObject(tools);
    }

#if HABIT_NOTIFICATIONS
    // Reminder (Faz 1) icin Project Settings > Mobile Notifications panelini
    // GUI'den elle gezmek yerine burada sabitliyoruz -- her degerin NEDEN bu
    // sekilde oldugu REMINDER_PLAN.md'nin "Ikinci tur incelemede..." bolumunde:
    // reschedule-on-restart ve exact-alarm paketin varsayilaniyla zaten KAPALI,
    // iOS launch-izni varsayilan ACIK oldugu icin bunu elle KAPATMAK gerekiyor.
    [MenuItem("HabitTracker/Configure Mobile Notification Settings")]
    public static void ConfigureNotificationSettings()
    {
        NotificationSettings.AndroidSettings.RescheduleOnDeviceRestart = false;
        NotificationSettings.AndroidSettings.ExactSchedulingOption = (AndroidExactSchedulingOption)0;
        NotificationSettings.iOSSettings.RequestAuthorizationOnAppLaunch = false;

        Debug.Log("HabitTracker: Mobile Notifications proje ayarlari yazildi -- "
            + "Android Reschedule-on-Restart KAPALI, Android Exact-Alarm KAPALI, "
            + "iOS Request-Authorization-on-Launch KAPALI. "
            + "ProjectSettings/NotificationsSettings.asset'in Plastic'e eklenmesi gerekiyor.");
    }
#endif
}
