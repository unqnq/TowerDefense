using System;
using UnityEngine;
using Unity.Services.LevelPlay;

// Сервіс реклами на Unity LevelPlay (пакет Ads Mediation 9.x).
// Він лише показує рекламу і повідомляє, коли вона закінчилась.
// Що робити до і після реклами (панель поразки тощо), вирішує той, хто її викликав (GameManager).
//
// Налаштування:
// 1. Вписати в інспекторі Android App Key та Ad Unit ID з кабінету LevelPlay.
// 2. Ads Mediation > Network Manager: встановити адаптери потрібних рекламних мереж.
//
// У редакторі справжня реклама не показується: якщо ввімкнено simulateAdsInEditor,
// реклама імітується (одразу «додивилися»), щоб перевіряти логіку гри.
public class AdsManager : MonoBehaviour
{
    [Header("Unity LevelPlay")]
    [SerializeField] private string androidAppKey = "";
    [SerializeField] private string interstitialAdUnitId = "";
    [SerializeField] private string rewardedAdUnitId = "";
    [Tooltip("Увімкнути Integration Test Suite LevelPlay (лише для тестових збірок!).")]
    [SerializeField] private bool enableTestSuite = false;

    [Header("Нагорода")]
    [SerializeField] private Bank bank;
    [SerializeField] private int rewardedMoney = 5;

    [Header("Тестування в редакторі")]
    [Tooltip("У редакторі імітувати успішний перегляд реклами.")]
    [SerializeField] private bool simulateAdsInEditor = true;

    private LevelPlayInterstitialAd interstitialAd;
    private LevelPlayRewardedAd rewardedAd;

    private bool adInProgress;
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
        interstitialAd?.DestroyAd();
        rewardedAd?.DestroyAd();
    }

    // ---------- Публічні методи ----------

    // Повноекранна реклама. onFinished викликається завжди: після показу або якщо реклама недоступна.
    public void ShowInterstitial(Action onFinished)
    {
        if (UseSimulation || interstitialAd == null || !interstitialAd.IsAdReady())
        {
            onFinished?.Invoke();
            return;
        }

        onInterstitialFinished = onFinished;
        BeginAd();
        interstitialAd.ShowAd();
    }

    // Кнопка "+монети за рекламу".
    public void ShowRewardedAd()
    {
        if (adInProgress) return;
        ShowRewarded(() => bank.AddMoney(rewardedMoney));
    }

    // ---------- Ініціалізація ----------

    private void InitSdk()
    {
        if (Application.isEditor) return; // у редакторі SDK рекламу не показує
        if (string.IsNullOrEmpty(androidAppKey))
        {
            Debug.LogWarning("[AdsManager] App Key не вказано, реклама вимкнена.");
            return;
        }

        if (enableTestSuite)
            LevelPlay.SetMetaData("is_test_suite", "enable");

        // Слухачі реєструються до виклику Init (вимога SDK).
        LevelPlay.OnInitSuccess += HandleInitSuccess;
        LevelPlay.OnInitFailed += HandleInitFailed;
        LevelPlay.Init(androidAppKey);
    }

    private void HandleInitSuccess(LevelPlayConfiguration config)
    {
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

        if (enableTestSuite)
            LevelPlay.LaunchTestSuite();
    }

    private void HandleInitFailed(LevelPlayInitError error)
    {
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
    private void ShowRewarded(Action onReward)
    {
        if (UseSimulation)
        {
            Debug.Log("[AdsManager] Імітація rewarded-реклами: нагороду видано.");
            onReward?.Invoke();
            return;
        }

        if (rewardedAd == null || !rewardedAd.IsAdReady())
        {
            Debug.Log("[AdsManager] Rewarded-реклама ще не готова.");
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

    // ---------- Пауза гри на час реклами ----------

    // Під час реклами гра стоїть і звук вимкнено, щоб вороги не йшли до бази.
    // Після реклами повертаємо той темп, що був до неї (після поразки це 0, гра так і лишиться на паузі).
    private void BeginAd()
    {
        adInProgress = true;
        savedTimeScale = Time.timeScale;
        savedVolume = AudioListener.volume;
        Time.timeScale = 0f;
        AudioListener.volume = 0f;
    }

    private void EndAd()
    {
        if (!adInProgress) return;
        adInProgress = false;
        AudioListener.volume = savedVolume;
        Time.timeScale = savedTimeScale;
    }
}
