using System;
using System.Collections;
using TMPro;
using UnityEngine;
using Unity.Services.LevelPlay;

// Сервіс реклами на Unity LevelPlay (пакет Ads Mediation 9.x).
// Він лише показує рекламу і повідомляє, коли вона закінчилась.
// Що робити до і після реклами (панель поразки тощо), вирішує той, хто її викликав (GameManager).
//
// Налаштування:
// 1. Вписати App Key та Ad Unit ID у файл Assets/Resources/AdsKeys (див. AdsKeys.cs). Його немає в git.
// 2. Ads Mediation > Network Manager: встановити адаптери потрібних рекламних мереж.
//
// Конфіденційність: SDK ініціалізується лише після того, як гравець відповів у вікні згоди (Consent),
// і перед ініціалізацією отримує цю відповідь. Зміна рішення пізніше одразу передається в SDK.
//
// SDK ініціалізується один раз за запуск гри. AdsManager живе в сцені рівня і створюється заново
// з кожним рівнем, тому стан ініціалізації статичний, а рекламні об'єкти створюються для кожного рівня.
//
// У редакторі справжня реклама не показується: якщо ввімкнено simulateAdsInEditor,
// замість неї на кілька секунд з'являється заглушка "AD", а гра так само стоїть на паузі,
// як і зі справжньою рекламою. Так можна перевірити всю логіку без білду на телефон.
public class AdsManager : MonoBehaviour
{
    [Header("Unity LevelPlay")]
    [Tooltip("Увімкнути Integration Test Suite LevelPlay (лише для тестових збірок!).")]
    [SerializeField] private bool enableTestSuite = false;

    [Header("Нагорода")]
    [SerializeField] private Bank bank;
    [SerializeField] private int rewardedMoney = 5;

    [Header("Тестування в редакторі")]
    [Tooltip("У редакторі імітувати успішний перегляд реклами.")]
    [SerializeField] private bool simulateAdsInEditor = true;
    [Tooltip("Скільки секунд триває імітація реклами в редакторі.")]
    [SerializeField, Min(0f)] private float simulatedAdSeconds = 2f;

    // Ключі з Assets/Resources/AdsKeys (не в git). Порожні, якщо файлу немає.
    private string androidAppKey = "";
    private string interstitialAdUnitId = "";
    private string rewardedAdUnitId = "";

    private static bool sdkInitStarted;
    private static bool sdkReady;

    private LevelPlayInterstitialAd interstitialAd;
    private LevelPlayRewardedAd rewardedAd;

    private bool adInProgress;

    // Зараз іде реклама: GameManager не ставить автопаузу через втрату фокусу і не чіпає timeScale.
    public static bool IsShowingAd { get; private set; }

    // Реклама закрилась. GameManager після цього сам виставляє правильну швидкість гри.
    public static event Action AdFinished;

    // Rewarded-реклама ще не завантажилась — HUD показує гравцю повідомлення.
    public static event Action RewardedUnavailable;

    private GameObject simulationOverlay;
    private TMP_Text simulationText;
    private float savedVolume;
    private float savedTimeScale = 1f;

    // Що зробити після закриття поточної реклами.
    private Action onInterstitialFinished;
    private Action onRewarded;

    private bool UseSimulation => Application.isEditor && simulateAdsInEditor;

    private void Awake()
    {
        if (bank == null) Debug.LogError("[AdsManager] Не призначено Bank.", this);
        InitSdk();
    }

    private void OnDestroy()
    {
        LevelPlay.OnInitSuccess -= HandleInitSuccess;
        LevelPlay.OnInitFailed -= HandleInitFailed;
        Consent.Changed -= OnConsentChanged;
        interstitialAd?.DestroyAd();
        IsShowingAd = false;
        rewardedAd?.DestroyAd();
    }

    // ---------- Публічні методи ----------

    // Повноекранна реклама. onFinished викликається завжди: після показу або якщо реклама недоступна.
    public void ShowInterstitial(Action onFinished)
    {
        // Інша реклама вже йде: не показуємо другу, але й не блокуємо перехід, що чекає на колбек.
        if (adInProgress)
        {
            onFinished?.Invoke();
            return;
        }
        if (UseSimulation)
        {
            StartCoroutine(SimulateAd("Interstitial", null, onFinished));
            return;
        }
        if (interstitialAd == null || !interstitialAd.IsAdReady())
        {
            onFinished?.Invoke();
            return;
        }

        onInterstitialFinished = onFinished;
        BeginAd();
        interstitialAd.ShowAd();
    }

    public bool IsRewardedAvailable => UseSimulation || (rewardedAd != null && rewardedAd.IsAdReady());

    // Кнопка "+монети за рекламу".
    public void ShowRewardedAd()
    {
        if (adInProgress) return;
        // Пауза і звук на час реклами — у BeginAd, монети — лише якщо гравець додивився.
        ShowRewarded(() =>
        {
            bank.AddMoney(rewardedMoney);
            Haptics.Light();
        });
    }

    // ---------- Ініціалізація ----------

    private void LoadKeys()
    {
        AdsKeys keys = AdsKeys.Load();
        if (keys == null)
        {
            Debug.LogWarning("[AdsManager] Немає Assets/Resources/AdsKeys — реклама вимкнена. Див. AdsKeys.cs.");
            return;
        }
        androidAppKey = keys.AndroidAppKey;
        interstitialAdUnitId = keys.InterstitialAdUnitId;
        rewardedAdUnitId = keys.RewardedAdUnitId;
    }

    private void InitSdk()
    {
        if (Application.isEditor) return; // у редакторі SDK рекламу не показує
        LoadKeys();
        if (string.IsNullOrEmpty(androidAppKey))
        {
            Debug.LogWarning("[AdsManager] App Key не вказано, реклама вимкнена.");
            return;
        }

        Consent.Changed += OnConsentChanged;

        // SDK вже готовий з попереднього рівня: лише створюємо рекламні об'єкти для цієї сцени.
        if (sdkReady)
        {
            CreateAds();
            return;
        }

        // Слухачі реєструються до виклику Init (вимога SDK). Якщо ініціалізація ще триває
        // з попередньої сцени, цей екземпляр теж дочекається OnInitSuccess.
        LevelPlay.OnInitSuccess += HandleInitSuccess;
        LevelPlay.OnInitFailed += HandleInitFailed;

        // Без відповіді гравця SDK не запускаємо. Запустимо, щойно він відповість (OnConsentChanged).
        if (Consent.HasAnswered) StartSdk();
    }

    private void StartSdk()
    {
        if (sdkInitStarted) return;
        sdkInitStarted = true;

        ApplyPrivacy(); // до Init, як вимагає документація LevelPlay
        if (enableTestSuite)
            LevelPlay.SetMetaData("is_test_suite", "enable");
        LevelPlay.Init(androidAppKey);
    }

    // Гравець відповів уперше або змінив рішення в меню Privacy.
    private void OnConsentChanged()
    {
        if (sdkInitStarted) ApplyPrivacy(); // SDK уже працює — просто оновлюємо згоду
        else StartSdk();
    }

    // Згода → персоналізована реклама. Відмова → неперсоналізована і заборона "продажу" даних (закони США).
    private static void ApplyPrivacy()
    {
        LevelPlayPrivacySettings.SetGDPRConsent(Consent.Granted);
        LevelPlayPrivacySettings.SetCCPA(!Consent.Granted);
    }

    private void HandleInitSuccess(LevelPlayConfiguration config)
    {
        bool firstTime = !sdkReady;
        sdkReady = true;
        CreateAds();
        if (enableTestSuite && firstTime)
            LevelPlay.LaunchTestSuite();
    }

    private void CreateAds()
    {
        if (interstitialAd != null || rewardedAd != null) return;

        // Рекламні об'єкти створюються лише після успішної ініціалізації.
        if (!string.IsNullOrEmpty(interstitialAdUnitId))
        {
            interstitialAd = new LevelPlayInterstitialAd(interstitialAdUnitId);
            interstitialAd.OnAdClosed += _ => FinishInterstitial();
            interstitialAd.OnAdDisplayFailed += (_, error) =>
            {
                Debug.LogWarning("[AdsManager] Interstitial не показалася: " + error);
                FinishInterstitial();
            };
            interstitialAd.OnAdLoadFailed += error =>
                Debug.LogWarning("[AdsManager] Interstitial не завантажилась: " + error);
            interstitialAd.LoadAd();
        }

        if (!string.IsNullOrEmpty(rewardedAdUnitId))
        {
            rewardedAd = new LevelPlayRewardedAd(rewardedAdUnitId);
            // Нагорода може прийти навіть після OnAdClosed, тому видаємо її лише тут.
            rewardedAd.OnAdRewarded += (_, _) =>
            {
                onRewarded?.Invoke();
                onRewarded = null;
            };
            rewardedAd.OnAdClosed += _ => FinishRewarded();
            rewardedAd.OnAdDisplayFailed += (_, error) =>
            {
                Debug.LogWarning("[AdsManager] Rewarded не показалася: " + error);
                FinishRewarded();
            };
            rewardedAd.OnAdLoadFailed += error =>
                Debug.LogWarning("[AdsManager] Rewarded не завантажилась: " + error);
            rewardedAd.LoadAd();
        }
    }

    private void HandleInitFailed(LevelPlayInitError error)
    {
        sdkInitStarted = false; // дозволяємо повторну спробу на наступному рівні
        Debug.LogWarning("[AdsManager] Ініціалізація LevelPlay не вдалася: " + error);
    }

    // ---------- Interstitial ----------

    private void FinishInterstitial()
    {
        EndAd();
        var callback = onInterstitialFinished;
        onInterstitialFinished = null;
        callback?.Invoke();
        interstitialAd.LoadAd(); // одразу готуємо наступну
    }

    // ---------- Rewarded ----------

    // onReward викликається лише якщо гравець додивився рекламу.
    // onUnavailable — якщо реклама ще не завантажилась (щоб показати гравцю повідомлення).
    public void ShowRewarded(Action onReward, Action onUnavailable = null)
    {
        if (adInProgress) return;

        if (UseSimulation)
        {
            StartCoroutine(SimulateAd("Rewarded", onReward, null));
            return;
        }

        if (rewardedAd == null || !rewardedAd.IsAdReady())
        {
            Debug.Log("[AdsManager] Rewarded-реклама ще не готова.");
            RewardedUnavailable?.Invoke();
            onUnavailable?.Invoke();
            return;
        }

        onRewarded = onReward;
        BeginAd();
        rewardedAd.ShowAd();
    }

    private void FinishRewarded()
    {
        EndAd();
        rewardedAd.LoadAd();
    }

    // ---------- Імітація в редакторі ----------

    // Поводиться як справжня реклама: гра на паузі, звук вимкнено, нагорода — після "перегляду".
    private IEnumerator SimulateAd(string kind, Action onReward, Action onFinished)
    {
        BeginAd();
        ShowSimulationOverlay(kind);
        yield return new WaitForSecondsRealtime(simulatedAdSeconds); // timeScale = 0, тож рахуємо реальний час

        if (simulationOverlay != null) simulationOverlay.SetActive(false);
        Debug.Log($"[AdsManager] Імітація реклами ({kind}) завершена.");
        onReward?.Invoke();
        EndAd();
        onFinished?.Invoke();
    }

    private void ShowSimulationOverlay(string kind)
    {
        if (simulationOverlay == null)
        {
            RectTransform canvas = UiFactory.CreateCanvas("SimulatedAdCanvas", transform, 100);
            RectTransform overlay = UiFactory.CreateOverlay("SimulatedAd", canvas, new Color(0.05f, 0.05f, 0.08f, 0.95f));
            simulationText = UiFactory.CreateText(overlay, null, 70f);
            simulationOverlay = overlay.gameObject;
        }
        simulationText.text = $"AD\n<size=50%>{kind} (simulated in editor)</size>";
        simulationOverlay.SetActive(true);
    }

    // ---------- Пауза гри на час реклами ----------

    // Під час реклами гра стоїть і звук вимкнено, щоб вороги не йшли до бази.
    // Після реклами повертаємо той темп, що був до неї (після поразки це 0, гра так і лишиться на паузі).
    private void BeginAd()
    {
        adInProgress = true;
        IsShowingAd = true;
        savedTimeScale = Time.timeScale;
        savedVolume = AudioListener.volume;
        Time.timeScale = 0f;
        AudioListener.volume = 0f;
    }

    private void EndAd()
    {
        if (!adInProgress) return;
        adInProgress = false;
        IsShowingAd = false;
        AudioListener.volume = savedVolume;
        Time.timeScale = savedTimeScale;
        AdFinished?.Invoke();
    }
}
