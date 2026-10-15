using System;

// Rotus stogunun salt-okunur aynasi. AppSettings/AppClock ile AYNI gerekce:
// galeri tarafindaki yaprak bilesenler (rozet, cip, pill) HabbitManager'i
// gormeden -- havuzlanmis, sahne baglami tasimayan bilesenler -- stogu
// okuyabilsin. Yazan TEK yer HabbitManager; buradaki Apply disinda hicbir
// yerden yazilmaz.
public static class RotusState
{
    private static int count;

    public static int Count => count;

    public static event Action Changed;

    public static void Apply(int newCount)
    {
        if (count == newCount)
        {
            return;
        }

        count = newCount;
        Changed?.Invoke();
    }
}
