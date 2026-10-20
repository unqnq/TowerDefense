using System;
using UnityEngine;

// Коротка вібрація для ігрових подій. Викликається звідусіль: Haptics.Light() / Medium() / Heavy().
// На Android тривалість задається точно через системний Vibrator (Handheld.Vibrate вміє лише одну довгу вібрацію).
// На iOS доступна тільки стандартна вібрація, у редакторі й на ПК нічого не відбувається.
// Гравець може вимкнути вібрацію (кнопка на екрані паузи), вибір зберігається.
public static class Haptics
{
    private const string EnabledKey = "VibrationEnabled";

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(EnabledKey, 1) == 1;
        set => PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
    }

    // Будівництво, продаж: ледь відчутний "клік".
    public static void Light() => Vibrate(15);

    // Merge, перемога: помітний відгук.
    public static void Medium() => Vibrate(35);

    // Удар по базі, поразка: сильний сигнал "щось не так".
    public static void Heavy() => Vibrate(70);

#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject vibrator;
    private static int sdkVersion;
    private static bool initFailed;

    private static void Vibrate(long milliseconds)
    {
        if (!Enabled || initFailed) return;
        try
        {
            if (vibrator == null)
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    sdkVersion = version.GetStatic<int>("SDK_INT");
            }

            if (sdkVersion >= 26)
            {
                // VibrationEffect.createOneShot(ms, DEFAULT_AMPLITUDE = -1)
                using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, -1))
                    vibrator.Call("vibrate", effect);
            }
            else
            {
                vibrator.Call("vibrate", milliseconds);
            }
        }
        catch (Exception e)
        {
            // На пристрої без вібромотора або з обмеженнями не спамимо помилками щокадру.
            initFailed = true;
            Debug.LogWarning("[Haptics] Вібрація недоступна: " + e.Message);
        }
    }
#elif UNITY_IOS && !UNITY_EDITOR
    private static void Vibrate(long milliseconds)
    {
        if (Enabled) Handheld.Vibrate();
    }
#else
    private static void Vibrate(long milliseconds) { }
#endif

    // Ніколи не викликається. Unity додає дозвіл android.permission.VIBRATE у маніфест лише тоді,
    // коли бачить у коді Handheld.Vibrate, тож це посилання потрібне, щоб вібрація взагалі працювала.
    private static void RequestVibratePermission() => Handheld.Vibrate();
}
