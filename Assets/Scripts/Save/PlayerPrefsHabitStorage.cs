using UnityEngine;

// PlayerPrefs uzerine kurulu depo.
//
// SetString ara katmana yazar, Save() diske indirir. Ikisini ayirmak onemli:
// Save() senkron I/O yapar (iOS'ta NSUserDefaults plist'i, Android'de
// SharedPreferences commit'i) ve her tik atmada cagirilirsa gorunur bir
// takilma uretir.
//
// Sinir: PlayerPrefs atomik degil ve bozulma kurtarmasi yok. Bu boyuttaki bir
// kayit icin kabul edilebilir; degilse IHabitStorage'in dosya tabanli bir
// gerceklemesi yazilir.
public class PlayerPrefsHabitStorage : IHabitStorage
{
    private const string DefaultKey = "habit_save_v1";

    private readonly string key;

    public PlayerPrefsHabitStorage(string key = DefaultKey)
    {
        this.key = key;
    }

    public string Load()
    {
        return PlayerPrefs.GetString(key, null);
    }

    public void Write(string json)
    {
        PlayerPrefs.SetString(key, json);
    }

    public void Flush()
    {
        PlayerPrefs.Save();
    }

    public void Delete()
    {
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }
}
