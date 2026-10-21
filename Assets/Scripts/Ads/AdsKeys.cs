using UnityEngine;

// Ключі LevelPlay (App Key і Ad Unit ID) окремо від сцени і коду.
// Файл Assets/Resources/AdsKeys.asset додано в .gitignore, тож ключі не потрапляють у публічний репозиторій.
//
// Як створити на новому комп'ютері (файл не приходить з git):
//   ПКМ у папці Assets/Resources → Create → Tower Defense → Ads Keys,
//   назвати рівно "AdsKeys" і вписати значення з кабінету LevelPlay (Apps і Ad units).
// Без цього файлу гра працює, але без реклами (у редакторі реклама все одно імітується).
[CreateAssetMenu(menuName = "Tower Defense/Ads Keys", fileName = "AdsKeys")]
public class AdsKeys : ScriptableObject
{
    private const string ResourcePath = "AdsKeys";

    [Tooltip("LevelPlay → Apps → TowerDefense → App Key.")]
    [SerializeField] private string androidAppKey = "";
    [Tooltip("LevelPlay → Ad units → ID interstitial-блоку. Порожньо — реклами між рівнями немає.")]
    [SerializeField] private string interstitialAdUnitId = "";
    [Tooltip("LevelPlay → Ad units → ID rewarded-блоку.")]
    [SerializeField] private string rewardedAdUnitId = "";

    public string AndroidAppKey => androidAppKey;
    public string InterstitialAdUnitId => interstitialAdUnitId;
    public string RewardedAdUnitId => rewardedAdUnitId;

    // null, якщо файлу з ключами немає.
    public static AdsKeys Load() => Resources.Load<AdsKeys>(ResourcePath);
}
