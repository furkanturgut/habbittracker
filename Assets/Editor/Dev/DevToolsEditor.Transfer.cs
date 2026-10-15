using UnityEditor;
using UnityEngine;

// DevToolsEditor.cs'in disa/ice aktarma bolumu -- ana dosya 500 satir
// sinirina dayandigi icin ayri partial parca (DevToolsEditor.Rotus.cs'in ayni
// gerekcesi).
//
// IKI SATIR CIFTI, BILEREK:
//   * "secici olmadan" -- pano + cihazdaki en yeni kurtarma dosyasi. Hicbir
//     diyalog acmaz, en hizli round-trip.
//   * "secici ile" -- Ayarlar ekranindaki GERCEK akis. Editor'de gercek
//     Windows Kaydet/Ac diyaloglarini acar, yani Android/iOS'a hic cikmadan
//     tum yol denenebilir.
public partial class DevToolsEditor
{
    private void DrawTransfer(DevTools tools)
    {
        Section("Yedek (dışa/içe aktar)");

        // Hepsi uretim yolundan geciyor (SaveExportService, NativeFileBridge,
        // HabbitManager.TryImportSave) -- dev katmani kural sahibi degil.
        EditorGUILayout.LabelField("Seçici olmadan", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Panoya + kurtarma kopyası"))
        {
            SaveExportService.ExportResult result = tools.ExportSave();
            Debug.Log(result.Ok
                ? "DevTools: " + result.HabitCount + " alıskanlık yedeklendi -> " + result.FileName
                : "DevTools: disa aktarma basarisiz. " + result.Error);
        }

        if (GUILayout.Button("En yeni yedekten geri yükle")
            && EditorUtility.DisplayDialog(
                "Yedekten geri yükle",
                "Mevcut kayıt, cihazdaki en yeni yedek dosyasıyla değiştirilecek. Emin misin?",
                "Geri yükle",
                "Vazgeç"))
        {
            bool ok = tools.ImportNewestBackup(out string error);
            Debug.Log(ok ? "DevTools: yedek geri yuklendi." : "DevTools: ice aktarma basarisiz. " + error);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("Sistem seçicisi ile", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Dosyaya kaydet..."))
        {
            tools.ExportSaveViaPicker();
        }

        if (GUILayout.Button("Dosyadan geri yükle..."))
        {
            // Onay kutusu YOK: secicinin kendisi bir onay adimi ve Vazgec
            // hicbir sey yapmiyor. (Ayarlar ekranindaki akista ikinci bir
            // ConfirmSheet var -- orada veri disaridan geliyor ve ozet
            // gosteriliyor; burada gelistirici kendi dosyasini seciyor.)
            tools.ImportSaveViaPicker();
        }

        EditorGUILayout.EndHorizontal();

        // Editor'de secici SENKRON (modal diyalog), yani buraya gelindiginde
        // ice aktarma bitmis oluyor -- Inspector'i hemen tazeleyebiliyoruz.
        if (GUI.changed)
        {
            SyncAfterCommand();
        }
    }
}
