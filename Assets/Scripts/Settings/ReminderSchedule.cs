using System;

// Hatirlatmanin "bir sonraki calisma zamani" hesabi -- pakete (com.unity.mobile.notifications)
// BAGIMLI DEGIL, saf statik. ReminderService bunu AppClock.Now ile besler, testler ise
// herhangi bir DateTime ile. Tek kural: sonuc HER ZAMAN simdiden sonra olmali (son-kosul
// invarianti, Android'in alarm mekanizmasi gecmis/esit bir ilk tetiklemeyi ANINDA atesler).
public static class ReminderSchedule
{
    public static bool TryNextFireTime(DateTime now, string hhmm, out DateTime fireTime)
    {
        fireTime = default;

        if (!TimeOfDay.TryParse(hhmm, out int hour, out int minute))
        {
            return false;
        }

        // now.Date: DateTimeKind.Local'i KORUR. "new DateTime(y, m, d, ...)" ile elle
        // kurmak DateTimeKind.Unspecified uretir -- paket yerel duvar saati bekliyor.
        DateTime candidate = now.Date.Add(new TimeSpan(hour, minute, 0));

        // Esitlik de "gecmis" sayilir: ayni anda ates alan bir alarm Android'de ANINDA
        // tetiklenir, bu da kullanicinin yeni actigi hatirlatmanin o saniye gelmesi gibi
        // sasirtici bir davranis olurdu.
        fireTime = candidate > now ? candidate : candidate.AddDays(1);
        return true;
    }
}
