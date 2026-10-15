using System;
using System.Collections.Generic;

// Ekle / duzenle formunun durumu. MonoBehaviour DEGIL: alan dogrulamasi ve gun
// donusumu sahne kurmadan test edilebilsin diye.
//
// Gunler burada PAZARTESI TABANLI indeks olarak duruyor (UI boyle gosteriyor);
// kayda yazilirken WeekDays uzerinden DayOfWeek'e ceviriliyor.
public class HabitForm
{
    public string Title = string.Empty;
    public string HabitType = HabitTypes.Build;
    public string Category = HabitCategories.Default;
    public int CanvasSize = HabitData.DefaultCanvasSize;
    public bool HasEndDate;
    public string EndDate = string.Empty;

    // Bos = Rastgele (bugunku davranis: PickNext karar verir).
    public string PaintingId = string.Empty;

    // Kucuk ve her zaman sirali tutuluyor (bkz. ToggleDay): kayda yazarken
    // ayrica siralamak gerekmiyor.
    public readonly List<int> Days = new List<int>(WeekDays.Count);

    public string TrimmedTitle => Title == null ? string.Empty : Title.Trim();

    // Bitis tarihi ACIKKEN metin cozulemezse kaydetmeye izin vermiyoruz: kayda
    // gecersiz bir tarih yazmak IsActiveOn'u sessizce "hep aktif"e dondururdu.
    public bool IsValid =>
        TrimmedTitle.Length > 0
        && (!HasEndDate || HabitScheduling.TryParse(EndDate, out _));

    public void Reset()
    {
        Title = string.Empty;
        HabitType = HabitTypes.Build;
        Category = HabitCategories.Default;
        CanvasSize = HabitData.DefaultCanvasSize;
        HasEndDate = false;
        EndDate = string.Empty;
        PaintingId = string.Empty;
        Days.Clear();
    }

    public void LoadFrom(HabitData habit)
    {
        Title = habit.title;
        HabitType = string.IsNullOrEmpty(habit.habitType) ? HabitTypes.Build : habit.habitType;
        Category = habit.categoryGroup;
        CanvasSize = habit.canvasSize;
        HasEndDate = habit.hasEndDate;
        EndDate = habit.endDate == null ? string.Empty : habit.endDate;

        // Duzenleme ekraninda tablo izgarasi gizli (bkz. HabitSheet.Open) --
        // bayat bir id'yi burada tutmak, ekranda hic gorunmeyen bir secimi
        // sessizce kayda tasirdi.
        PaintingId = string.Empty;

        Days.Clear();
        if (habit.frequency == null)
        {
            return;
        }

        for (int i = 0; i < habit.frequency.Count; i++)
        {
            AddDay(WeekDays.ToMondayIndex(habit.frequency[i]));
        }
    }

    public bool IsDaySelected(int mondayIndex)
    {
        return ListLookup.Contains(Days, mondayIndex);
    }

    public void ToggleDay(int mondayIndex)
    {
        if (!Days.Remove(mondayIndex))
        {
            AddDay(mondayIndex);
        }
    }

    // Tuval boyutu degisince tablo secimi DUSUYOR: secili tablonun parca
    // sayisi artik uyusmuyor ve izgara da yeni boyuta gore yeniden
    // filtreleniyor, yani ekranda gorunmeyen bir tabloyu secili tutmak olurdu.
    public void SetCanvasSize(int size)
    {
        if (size == CanvasSize)
        {
            return;
        }

        CanvasSize = size;
        PaintingId = string.Empty;
    }

    // Kayda gidecek listeyi doldurur. BOS SECIM BOS KALIR -- yedi gunu tek tek
    // yazmak degil, listeyi bos birakmak "her gun" demek (HabitScheduling).
    // Doldurursak kullanici formu tekrar actiginda "hic gun secilmemis" ile
    // "yedi gun de secilmis" ayirt edilemezdi.
    public void FillFrequency(List<DayOfWeek> result)
    {
        result.Clear();
        for (int i = 0; i < Days.Count; i++)
        {
            result.Add(WeekDays.ToDayOfWeek(Days[i]));
        }
    }

    public string DaysHint()
    {
        return Days.Count == 0
            ? Loc.Tr("habit_sheet.days_hint")
            : string.Format(Loc.Tr("habit_sheet.days_hint_selected"), Days.Count);
    }

    // Sirali ekleme: liste en fazla yedi elemanli, dogrusal arama yeterli.
    private void AddDay(int mondayIndex)
    {
        if (mondayIndex < 0 || mondayIndex >= WeekDays.Count || ListLookup.Contains(Days, mondayIndex))
        {
            return;
        }

        int at = Days.Count;
        for (int i = 0; i < Days.Count; i++)
        {
            if (Days[i] > mondayIndex)
            {
                at = i;
                break;
            }
        }

        Days.Insert(at, mondayIndex);
    }
}
