package com.habittracker;

import android.app.Activity;
import android.app.Fragment;
import android.app.FragmentManager;
import android.content.ContentResolver;
import android.content.Intent;
import android.database.Cursor;
import android.net.Uri;
import android.os.Bundle;
import android.provider.OpenableColumns;
import android.util.Log;

import com.unity3d.player.UnityPlayer;

import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;

// Sistem dosya secicisinin Android yarisi. C# tarafi: NativeFileBridge.cs.
//
// NEDEN BIR FRAGMENT: startActivityForResult'un SONUCU onActivityResult'a
// geliyor ve Unity'nin kendi Activity'si bunu C#'a iletmiyor -- saf JNI ile
// sonuc alinamaz. Gorunmez bir Fragment manifeste YAZILMIYOR, calisma aninda
// ekleniyor; yani bu kopru ozel bir AndroidManifest.xml gerektirmiyor.
//
// NEDEN ACTION_CREATE_DOCUMENT / ACTION_OPEN_DOCUMENT: ikisi de hicbir izin ve
// FileProvider gerektirmiyor (secici URI'yi kendisi veriyor). Paylasim
// (ACTION_SEND) isteseydik FileProvider + provider_paths.xml + manifest
// gerekirdi.
//
// BU SINIF JSON TASIMAZ, DOSYA KOPYALAR: kaydetmede C#'in verdigi yerel
// dosyayi secilen URI'ye, secmede secilen URI'yi C#'in verdigi yerel yola.
//
// Hata metinleri KISA ASCII JETONLARI ("write"/"read"/"nopicker") --
// kullaniciya gorunen Turkce cumleler C# tarafinda (NativeFileBridge.
// TranslateError). Boylece bu dosyanin karakter kodlamasi hic sorun olmuyor.
public class NativeFileBridge
{
    private static final String TAG = "NativeFileBridge";

    // C# tarafindaki NativeFileBridge.ReceiverObjectName / ResultMethodName
    // ile BIREBIR ayni olmak zorunda: UnitySendMessage nesneyi ADINA gore
    // buluyor, uyusmazlarsa mesaj SESSIZCE kaybolur.
    private static final String RECEIVER_OBJECT = "NativeFileBridge";
    private static final String RECEIVER_METHOD = "OnNativeFileResult";

    private static final String FRAGMENT_TAG = "HabitNativeFileFragment";

    private static final int REQUEST_SAVE = 51001;
    private static final int REQUEST_PICK = 51002;

    private static final char SEPARATOR = '|';

    private static final int COPY_BUFFER = 16 * 1024;

    // ACTION_CREATE_DOCUMENT: sistem "Kaydet" ekrani acilir, kullanici klasoru
    // ve adi secer.
    public static void saveFile(final Activity activity, final String sourcePath, final String fileName)
    {
        Intent intent = new Intent(Intent.ACTION_CREATE_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType("application/json");
        intent.putExtra(Intent.EXTRA_TITLE, fileName);

        launch(activity, intent, REQUEST_SAVE, sourcePath, fileName);
    }

    // ACTION_OPEN_DOCUMENT. Tip "*/*" BILEREK: bircok dosya yoneticisi kati
    // "application/json" altinda .json dosyasini hic gostermiyor.
    // EXTRA_MIME_TYPES yine de bir ipucu olarak gidiyor.
    public static void pickFile(final Activity activity, final String destPath)
    {
        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType("*/*");
        intent.putExtra(Intent.EXTRA_MIME_TYPES, new String[] {
            "application/json",
            "text/plain",
            "application/octet-stream"
        });

        launch(activity, intent, REQUEST_PICK, destPath, null);
    }

    private static void launch(
        final Activity activity, final Intent intent, final int requestCode,
        final String localPath, final String suggestedName)
    {
        if (activity == null)
        {
            send("error", "", "nopicker", "");
            return;
        }

        // Fragment islemleri ve startActivityForResult UI thread'inde olmak
        // zorunda; bu metod Unity'nin ana thread'inden cagriliyor ama
        // garantiye aliyoruz.
        activity.runOnUiThread(new Runnable()
        {
            @Override
            public void run()
            {
                try
                {
                    FragmentManager manager = activity.getFragmentManager();

                    // Yarida kalmis bir onceki parca varsa temizle -- C# tarafi
                    // tek bekleyen cagri kuralini zaten uyguluyor, bu ikinci
                    // emniyet.
                    Fragment stale = manager.findFragmentByTag(FRAGMENT_TAG);
                    if (stale != null)
                    {
                        manager.beginTransaction().remove(stale).commitAllowingStateLoss();
                        manager.executePendingTransactions();
                    }

                    ResultFragment fragment = new ResultFragment();
                    fragment.configure(intent, requestCode, localPath, suggestedName);

                    manager.beginTransaction().add(fragment, FRAGMENT_TAG).commitAllowingStateLoss();
                }
                catch (Throwable error)
                {
                    Log.e(TAG, "secici baslatilamadi", error);
                    send("error", "", "nopicker", "");
                }
            }
        });
    }

    // Payload: "<outcome>|<displayName>|<error>|<path>" -- yol EN SONDA, cunku
    // C# tarafi alan sayisi vererek boluyor ve son alan ayracli da olsa ham
    // kaliyor (Android'de '|' yasal bir dosya adi karakteri).
    private static void send(String outcome, String displayName, String error, String path)
    {
        StringBuilder payload = new StringBuilder();
        payload.append(outcome).append(SEPARATOR);
        payload.append(strip(displayName)).append(SEPARATOR);
        payload.append(strip(error)).append(SEPARATOR);
        payload.append(path == null ? "" : path);

        try
        {
            UnityPlayer.UnitySendMessage(RECEIVER_OBJECT, RECEIVER_METHOD, payload.toString());
        }
        catch (Throwable unityError)
        {
            Log.e(TAG, "UnitySendMessage basarisiz", unityError);
        }
    }

    // Son alan disindaki her alandan ayrac atiliyor, yoksa C# tarafindaki
    // bolme kayar.
    private static String strip(String value)
    {
        if (value == null)
        {
            return "";
        }

        return value.replace(SEPARATOR, ' ');
    }

    private static void copy(InputStream input, OutputStream output) throws Exception
    {
        byte[] buffer = new byte[COPY_BUFFER];
        int read;

        while ((read = input.read(buffer)) > 0)
        {
            output.write(buffer, 0, read);
        }

        output.flush();
    }

    private static void closeQuietly(Object stream)
    {
        if (stream == null)
        {
            return;
        }

        try
        {
            if (stream instanceof InputStream)
            {
                ((InputStream)stream).close();
            }
            else if (stream instanceof OutputStream)
            {
                ((OutputStream)stream).close();
            }
        }
        catch (Throwable ignored)
        {
        }
    }

    // DISPLAY_NAME yoksa/okunamazsa null doner -- cagiran kendi varsayilanina
    // duser. C# tarafi bu adi ayrica SanitizeFileName'den geciriyor.
    private static String displayNameOf(ContentResolver resolver, Uri uri)
    {
        Cursor cursor = null;

        try
        {
            cursor = resolver.query(uri, new String[] { OpenableColumns.DISPLAY_NAME }, null, null, null);
            if (cursor != null && cursor.moveToFirst())
            {
                int column = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME);
                if (column >= 0)
                {
                    return cursor.getString(column);
                }
            }
        }
        catch (Throwable ignored)
        {
        }
        finally
        {
            if (cursor != null)
            {
                try
                {
                    cursor.close();
                }
                catch (Throwable ignored)
                {
                }
            }
        }

        return null;
    }

    // Gorunmez, UI'si olmayan parca. Intent'i onCreate'te firlatiyor ve
    // sonucu aldiktan sonra kendini kaldiriyor.
    public static class ResultFragment extends Fragment
    {
        private Intent intent;
        private int requestCode;
        private String localPath;
        private String suggestedName;
        private boolean launched;

        public void configure(Intent intent, int requestCode, String localPath, String suggestedName)
        {
            this.intent = intent;
            this.requestCode = requestCode;
            this.localPath = localPath;
            this.suggestedName = suggestedName;
        }

        @Override
        public void onCreate(Bundle savedInstanceState)
        {
            super.onCreate(savedInstanceState);

            if (launched)
            {
                return;
            }

            // Sistem parcayi kendi basina yeniden kurduysa (surec olumu)
            // intent null olur -- cagrilmayi bekleyen bir C# geri cagrisi
            // yoksa da zararsiz, varsa hata olarak kapanir.
            if (intent == null)
            {
                launched = true;
                send("error", "", "nopicker", "");
                detach();
                return;
            }

            launched = true;

            try
            {
                startActivityForResult(intent, requestCode);
            }
            catch (Throwable error)
            {
                Log.e(TAG, "startActivityForResult basarisiz", error);
                send("error", "", "nopicker", "");
                detach();
            }
        }

        @Override
        public void onActivityResult(int code, int resultCode, Intent data)
        {
            super.onActivityResult(code, resultCode, data);

            if (code != requestCode)
            {
                return;
            }

            try
            {
                if (resultCode != Activity.RESULT_OK || data == null || data.getData() == null)
                {
                    send("cancel", "", "", "");
                    return;
                }

                if (requestCode == REQUEST_SAVE)
                {
                    handleSave(data.getData());
                }
                else
                {
                    handlePick(data.getData());
                }
            }
            finally
            {
                detach();
            }
        }

        // Yerel kurtarma kopyasini kullanicinin sectigi URI'ye yazar.
        private void handleSave(Uri uri)
        {
            ContentResolver resolver = getActivity().getContentResolver();
            InputStream input = null;
            OutputStream output = null;

            try
            {
                input = new FileInputStream(localPath);
                // "wt" = truncate: kullanici VAR OLAN bir dosyayi secip uzerine
                // yazdirirsa artik baytlar kalmasin ("w" bazi saglayicilarda
                // kirpmiyor).
                output = resolver.openOutputStream(uri, "wt");

                if (output == null)
                {
                    send("error", "", "write", "");
                    return;
                }

                copy(input, output);

                String shown = displayNameOf(resolver, uri);
                if (shown == null)
                {
                    shown = suggestedName;
                }

                send("ok", shown, "", uri.toString());
            }
            catch (Throwable error)
            {
                Log.e(TAG, "kaydetme basarisiz", error);
                send("error", "", "write", "");
            }
            finally
            {
                closeQuietly(input);
                closeQuietly(output);
            }
        }

        // Secilen URI'nin icerigini C#'in verdigi yerel yola yazar; C# o
        // dosyayi okuyup SaveTransfer.TryRead'e veriyor.
        private void handlePick(Uri uri)
        {
            ContentResolver resolver = getActivity().getContentResolver();
            InputStream input = null;
            OutputStream output = null;

            try
            {
                input = resolver.openInputStream(uri);

                if (input == null)
                {
                    send("error", "", "read", "");
                    return;
                }

                output = new FileOutputStream(localPath);
                copy(input, output);

                String shown = displayNameOf(resolver, uri);

                // Yol C#'in verdigi YEREL hedef, URI degil -- C# File.ReadAllText
                // ile bunu okuyacak.
                send("ok", shown, "", localPath);
            }
            catch (Throwable error)
            {
                Log.e(TAG, "secilen dosya okunamadi", error);
                send("error", "", "read", "");
            }
            finally
            {
                closeQuietly(input);
                closeQuietly(output);
            }
        }

        private void detach()
        {
            try
            {
                getFragmentManager().beginTransaction().remove(this).commitAllowingStateLoss();
            }
            catch (Throwable ignored)
            {
            }
        }
    }
}
