// Uygulamanin desteklegi diller. Unset == "hic secilmemis" -- WeekStart'in
// aksine burada gercek bir varsayilan YOK: ilk acilista SystemLanguageMap.
// Detect ile doldurulup kaydediliyor (bkz. HabbitManager.ApplyLoadedSave/
// DeleteAllData). Sonradan eklenen alan: saveVersion ARTMADI (bkz.
// AppSettingsData.language ustundeki yorum, PlayerSaveData.cs).
//
// Sira SABIT -- bu bir kayit degeri, enum uyeleri yeniden numaralanirsa eski
// kayitlar farkli bir dile donusur. Yeni dil eklenirken mevcut degerler
// DEGISTIRILMEZ, listenin sonuna eklenir.
public enum AppLanguage
{
    Unset = 0,
    Turkish = 1,
    English = 2,
    French = 3,
    German = 4,
    Italian = 5,
    PortugueseBR = 6,
    Russian = 7,
    Spanish = 8,
}
