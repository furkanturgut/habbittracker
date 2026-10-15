// Kaydin nereye yazildigini soyutlar. Su an PlayerPrefs; ileride
// Application.persistentDataPath altinda bir dosyaya (atomik yazma + bozulma
// kurtarmasi ile) gecmek gerekirse HabbitManager'a hic dokunmadan baska bir
// gerceklemesi takilabilir.
public interface IHabitStorage
{
    // Kayit yoksa null ya da bos string doner.
    string Load();

    // Ara katmana yazar; diske inmesi garanti DEGIL.
    void Write(string json);

    // Ara katmani diske indirir. Senkron I/O oldugu icin Write'tan ayri tutuluyor:
    // her degisiklikte degil, yalnizca guvenli noktalarda (arka plana alma, cikis)
    // cagriliyor.
    void Flush();

    void Delete();
}
