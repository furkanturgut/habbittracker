using System;
using System.Collections;
using UnityEngine;
#if HABIT_NOTIFICATIONS
using Unity.Notifications;
#endif

// Yerel bildirim planlama (REMINDER_PLAN.md Faz 3). Paket cagrilari HABIT_NOTIFICATIONS
// define'i arkasinda (bkz. HabitTracker.Runtime.asmdef versionDefines) -- paket
// cozulmemisse ya da bu kod Editor'de calisiyorsa eski Debug.Log davranisina duser.
//
// Bildirim basligi/govdesi ve kanal adi artik Loc.Tr uzerinden geliyor
// (LOCALIZATION_PLAN.md Faz 4) -- dosyanin kendisi artik literal Turkce
// metin tasimiyor, ASCII kalabilir.
public static class ReminderService
{
    // Sabit kimlik: yeniden planlama eskisini DEGISTIRIR, yigmaz.
    public const int NotificationId = 1001;

#if HABIT_NOTIFICATIONS
    private const string ChannelId = "habit_reminders";

    private static bool initialized;

    // Tembel idempotent kurulum. Editor'de paket hic cagrilmaz -- gercek
    // cihaz davranisi (izin diyalogu, planlama) Editor'de dogrulanamaz, bu
    // yuzden izin yalnizca "toggle'dan istenir" karari burada da gecerli
    // kalsin diye false donup cagiranlarin kendi dusus yoluna birakiyoruz.
    private static bool Initialize()
    {
        if (Application.isEditor)
        {
            return false;
        }

        if (initialized)
        {
            return true;
        }

        NotificationCenterArgs args = NotificationCenterArgs.Default;
        args.AndroidChannelId = ChannelId;
        args.AndroidChannelName = Loc.Tr("reminder.channel_name");
        args.AndroidChannelDescription = Loc.Tr("reminder.channel_description");

        NotificationCenter.Initialize(args);
        initialized = true;
        return true;
    }
#endif

    public static void Schedule(bool on, string time)
    {
        Schedule(on, time, AppClock.Now);
    }

    // Saf asiri-yukleme: "simdi" disaridan geliyor (test edilebilir). Uretim
    // cagirici yukarisi -- Schedule(bool, string) -- bunu AppClock.Now ile besler.
    public static void Schedule(bool on, string time, DateTime now)
    {
        if (!on)
        {
            Cancel();
            return;
        }

        if (!ReminderSchedule.TryNextFireTime(now, time, out DateTime fireTime))
        {
            return;
        }

#if HABIT_NOTIFICATIONS
        if (!Initialize())
        {
            Debug.Log("ReminderService.Schedule: Editor'de gercek bildirim planlanmadi. fireTime=" + fireTime);
            return;
        }

        NotificationCenter.CancelScheduledNotification(NotificationId);

        Notification notification = new Notification
        {
            Identifier = NotificationId,
            Title = string.Format(Loc.Tr("reminder.notification_title"), AppSettings.PainterName),
            Text = Loc.Tr("reminder.notification_text"),
            ShowInForeground = false,
        };

        NotificationCenter.ScheduleNotification(notification,
            new NotificationDateTimeSchedule(fireTime, NotificationRepeatInterval.Daily));
#else
        Debug.Log("ReminderService.Schedule: yerel bildirim plani henuz yok (paket cozulmemis). on=" + on + " time=" + time + " fireTime=" + fireTime);
#endif
    }

    public static void Cancel()
    {
#if HABIT_NOTIFICATIONS
        if (!Initialize())
        {
            return;
        }

        NotificationCenter.CancelScheduledNotification(NotificationId);
#else
        Debug.Log("ReminderService.Cancel: yerel bildirim plani henuz yok (paket cozulmemis).");
#endif
    }

    // Teslim edilmis bildirimleri temizler -- Android dokunma kendiliginden kapatmaz.
    public static void ClearDelivered()
    {
#if HABIT_NOTIFICATIONS
        if (!Initialize())
        {
            return;
        }

        NotificationCenter.CancelAllDeliveredNotifications();
#endif
    }

    // Donus tipinde PAKET TIPI YOK (bool) -- HabitTracker.Tests bu dosyayi
    // (dolayli) referans etmeden gorebilsin diye. Editor'de Initialize() daima
    // false dondugu icin izin "verilmis" kabul edilir -- mutlu yolu Play
    // mode'da test edebilmek icin.
    public static IEnumerator RequestPermission(Action<bool> done)
    {
#if HABIT_NOTIFICATIONS
        if (!Initialize())
        {
            done?.Invoke(true);
            yield break;
        }

        NotificationsPermissionRequest request = NotificationCenter.RequestPermission();
        yield return request;

        done?.Invoke(request.Status == NotificationsPermissionStatus.Granted);
#else
        done?.Invoke(true);
        yield break;
#endif
    }
}
