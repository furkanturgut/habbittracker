using System;
using System.Collections.Generic;

// Ayar ekraninin "Hafta baslangici" secimi. Kayit HER ZAMAN Pazartesi-indeksli
// kaliyor (bkz. WeekDays sinifi) -- bu yalnizca GOSTERIM sirasini secer.
public enum WeekStart
{
    Monday = 0,
    Sunday = 1,
}

// Haftanin gunlerinin UI tarafindaki gosterimi ve System.DayOfWeek ile donusumu.
//
// TUZAK: tasarim (ve kullanici) haftaya PAZARTESI'den basliyor -- 0 = Pt ... 6 = Pz.
// System.DayOfWeek ise PAZAR'dan: Sunday = 0, Monday = 1. Iki numaralandirma
// birbirine karisirsa hata SESSIZ olur; aliskanlik bir gun kaymis planlanir ve
// streak yanlis kirilir. Bu yuzden donusum yalnizca burada yapilir -- baska
// hicbir yerde (int)day ya da (DayOfWeek)index yazilmamali.
public static class WeekDays
{
    public const int Count = 7;

    // Kullaniciya gorunen metin: Loc.Tr uzerinden (LOCALIZATION_PLAN.md Faz 5) --
    // mondayIndex 0..6, Strings.tsv'deki weekday.0.short..weekday.6.short
    // anahtarlarina DOGRUDAN eslenir (donusum yok, indeks == anahtar numarasi).
    public static string ShortLabel(int mondayIndex)
    {
        return Loc.Tr("weekday." + mondayIndex + ".short");
    }

    // Kart uzerindeki 18x18 rozetler tek harf gosteriyor.
    public static string Initial(int mondayIndex)
    {
        return Loc.Tr("weekday." + mondayIndex + ".initial");
    }

    public static DayOfWeek ToDayOfWeek(int mondayIndex)
    {
        // Pt(0) -> Monday(1) ... Pz(6) -> Sunday(0)
        return (DayOfWeek)((mondayIndex + 1) % Count);
    }

    public static int ToMondayIndex(DayOfWeek day)
    {
        // Sunday(0) -> 6, Monday(1) -> 0 ...
        return ((int)day + Count - 1) % Count;
    }

    // Ayar ekraninin "Hafta baslangici" secimine gore GOSTERIM yuvasi <->
    // Pazartesi-indeksi donusumu. MoodScale'in SlotForMood/MoodForSlot
    // ikilisinin birebir ayni gerekcesi: KAYIT hep Pazartesi-indeksli kaliyor
    // (HabitScheduling.IsDueOn, streak -- hicbiri bundan haberdar degil),
    // yalnizca EKRANDA hangi rozetin hangi gunu temsil ettigi degisiyor.
    //
    // WeekStart.Monday icin IKISI DE birim fonksiyon (slot == mondayIndex) --
    // bugunku davranis byte-ayni kaliyor. WeekStart.Sunday icin yuva 0 Pazar'i
    // (mondayIndex 6) gosterir, yuva 1..6 Pazartesi..Cumartesi'ye (mondayIndex
    // 0..5) kayar.
    public static int MondayIndexForSlot(int slot, WeekStart start)
    {
        return start == WeekStart.Sunday ? (slot + Count - 1) % Count : slot;
    }

    public static int SlotForMondayIndex(int mondayIndex, WeekStart start)
    {
        return start == WeekStart.Sunday ? (mondayIndex + 1) % Count : mondayIndex;
    }

    public static string LabelForSlot(int slot, WeekStart start)
    {
        return ShortLabel(MondayIndexForSlot(slot, start));
    }

    public static string InitialForSlot(int slot, WeekStart start)
    {
        return Initial(MondayIndexForSlot(slot, start));
    }

    // O gun secili mi? BOS liste "her gun" demektir -- HabitScheduling.IsScheduledOn
    // ile ayni sozlesme, cunku rozetler o kuralin gorsel karsiligi.
    public static bool IsSelected(List<DayOfWeek> frequency, int mondayIndex)
    {
        if (frequency == null || frequency.Count == 0)
        {
            return true;
        }

        DayOfWeek day = ToDayOfWeek(mondayIndex);
        for (int i = 0; i < frequency.Count; i++)
        {
            if (frequency[i] == day)
            {
                return true;
            }
        }

        return false;
    }
}
