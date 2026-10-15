// Mod olceginin EKRAN sirasi ve slot eslemesi.
//
// PainterManager.PainterMoods enum'u en IYIDEN kotuye sirali (UltraRich = 0 ...
// Starve = 5); gostergede ise soldan saga KOTUDEN iyiye gitmesi gerekiyor.
// Donusumun tek evi burasi -- WeekDays'in Pazartesi/Pazar donusumu icin tek yer
// olmasiyla ayni gerekce: iki ayri yerde elle yazilsa biri duzeltilip digeri
// unutulurdu ve hata sessiz olurdu (yanlis daire yanar, hicbir sey patlamaz).
//
// Saf mantik: MonoBehaviour yok, sahne yok, Screen.* okumasi yok.
// Tests/HabitUiLogicTests bu dosyayi dogruluyor.
public static class MoodScale
{
    // Soldan saga gosterim sirasi: en kotu modla baslar, en iyi modla biter.
    public static readonly PainterManager.PainterMoods[] Order =
    {
        PainterManager.PainterMoods.Starve,
        PainterManager.PainterMoods.Sad,
        PainterManager.PainterMoods.Normal,
        PainterManager.PainterMoods.Happy,
        PainterManager.PainterMoods.Rich,
        PainterManager.PainterMoods.UltraRich
    };

    // Enum'a yeni bir mod eklenip Order'a eklenmezse buraya dusuyoruz --
    // MoodPresentation.Label'in switch varsayilaniyla ayni davranis: en notr mod.
    //
    // Order'dan SONRA tanimli olmasi sart: statik alan baslaticilari bildirim
    // sirasiyla calisiyor ve IndexOf Order'i okuyor.
    public static readonly int DefaultSlot = IndexOf(PainterManager.PainterMoods.Normal);

    // Daire sayisi tablodan TURETILIYOR, 6 diye sabit yazilmiyor: enum buyurse
    // satir da kendiliginden buyuyor. PainterManager'in MaxMoodPoint'i MoodTable
    // uzunlugundan turetmesiyle ayni tasarim karari.
    public static int Count => Order.Length;

    public static int SlotForMood(PainterManager.PainterMoods mood)
    {
        int index = IndexOf(mood);
        return index >= 0 ? index : DefaultSlot;
    }

    public static PainterManager.PainterMoods MoodForSlot(int slot)
    {
        if (slot < 0 || slot >= Order.Length)
        {
            return PainterManager.PainterMoods.Normal;
        }

        return Order[slot];
    }

    // Tahsissiz dogrusal arama: dizi alti elemanli ve bu yol her mod
    // degisiminde calisiyor. LINQ'in First/IndexOf'u burada gereksiz cop uretir.
    private static int IndexOf(PainterManager.PainterMoods mood)
    {
        for (int i = 0; i < Order.Length; i++)
        {
            if (Order[i] == mood)
            {
                return i;
            }
        }

        return -1;
    }
}
