using System;
using UnityEngine;

// Згода гравця на персоналізовану рекламу (GDPR). Зберігається в PlayerPrefs.
// Поки гравець не відповів, рекламний SDK не ініціалізується (див. AdsManager).
public static class Consent
{
    private const string Key = "ads_consent"; // 0 — ще не відповів, 1 — згоден, 2 — відмовився

    public static bool HasAnswered => PlayerPrefs.GetInt(Key, 0) != 0;
    public static bool Granted => PlayerPrefs.GetInt(Key, 0) == 1;

    // Викликається після кожної відповіді гравця, зокрема коли він змінює вибір у налаштуваннях.
    public static event Action Changed;

    public static void Set(bool granted)
    {
        PlayerPrefs.SetInt(Key, granted ? 1 : 2);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
