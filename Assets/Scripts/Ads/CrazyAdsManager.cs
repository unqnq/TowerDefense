using UnityEngine;
using CrazyGames;

public class CrazyAdsManager : MonoBehaviour
{
    [SerializeField] private GameObject banner;
    [SerializeField] private GameObject gameOverPanel;

    private bool lost;
    private float savedVolume;

    private bool SDKReady =>
    CrazySDK.IsAvailable && CrazySDK.IsInitialized;

    private void Awake()
    {
        banner.SetActive(false);
        gameOverPanel.SetActive(false);
    }

    // Викликай один раз, коли гравець програв.
    public void OnLose()
    {
        if (lost) return;
        lost = true;

        Time.timeScale = 0f;
        savedVolume = AudioListener.volume;

        HideBanner();

        if (!SDKReady)
        {
            FinishAd();
            return;
        }

        CrazySDK.Game.GameplayStop();

        CrazySDK.Ad.RequestAd(
       CrazyAdType.Midgame,

       // Реклама почалася.
       () => AudioListener.volume = 0f,

        // Реклама недоступна або сталася помилка.
        error => FinishAd(),

        // Реклама завершилася.
        () => FinishAd()
        );
    }

    private void FinishAd()
    {
        AudioListener.volume = savedVolume;

        // Після реклами залишаємо гру на паузі.
        Time.timeScale = 0f;
        gameOverPanel.SetActive(true);

        if (SDKReady)
        {
            banner.SetActive(true);
            CrazySDK.Banner.RefreshBanners();
        }
    }
    public void HideBanner()
    {
        banner.SetActive(false);

        if (SDKReady)
            CrazySDK.Banner.RefreshBanners();
    }

    // Викликай перед перезапуском рівня або виходом у меню.
    public void BeforeLeaving()
    {
        HideBanner();
        Time.timeScale = 1f;
    }

    public void ShowRewardedAd()
    {
        if (!SDKReady) return;

        CrazySDK.Ad.RequestAd(
            CrazyAdType.Rewarded,

            // Реклама почалася.
            () => Debug.Log("Реклама почалася"),

            // Помилка — монети не видаємо.
            error => Debug.Log("Реклама недоступна"),

            // Реклама успішно завершилася.
            () => Bank.instance.AddMoney(5)
        );
    }
}
