using UnityEngine;

// Параметри одного типу і рівня вежі. Новий тип вежі = новий asset (ПКМ у Project → Create → Tower Defense → Tower),
// код при цьому не змінюється. Вежа з'являється в меню будівництва, якщо додати її в TurretSpawner.
// Рівні вежі — це ланцюжок assets: поле Next Level вказує на рівень після merge (BasicTower → BasicTower2 → ...).
// Поля приватні з геттерами: під час гри код лише читає asset і не може випадково зіпсувати його значення.
[CreateAssetMenu(menuName = "Tower Defense/Tower", fileName = "Tower")]
public class TowerData : ScriptableObject
{
    [Header("Опис")]
    [Tooltip("Назва в меню. Краще латиницею: поточні шрифти можуть не мати кирилиці.")]
    [SerializeField] private string displayName = "Tower";
    [Tooltip("Необов'язково. Іконка на кнопці в меню будівництва.")]
    [SerializeField] private Sprite icon;
    [Tooltip("Колір моделі і кулі. Білий = без змін. Тимчасовий спосіб відрізнити вежі, поки немає окремих моделей.")]
    [SerializeField] private Color tint = Color.white;
    [Tooltip("Множник розміру моделі відносно префаба. Вищі рівні більші, щоб прокачку було видно.")]
    [SerializeField, Min(0.1f)] private float scale = 1f;

    [Header("Ціна")]
    [Tooltip("Ціна будівництва. Важлива лише для першого рівня: наступні рівні отримуються через merge двох однакових веж.")]
    [SerializeField, Min(0)] private int cost = 5;
    [Tooltip("Рівень, який вийде після merge двох таких веж. Порожньо = максимальний рівень.")]
    [SerializeField] private TowerData nextLevel;

    [Header("Префаби")]
    [Tooltip("Модель вежі з компонентом Turret. Якщо в наступного рівня інша модель, вежа заміниться при покращенні.")]
    [SerializeField] private GameObject prefab;
    [Tooltip("Куля з компонентом Bullet.")]
    [SerializeField] private GameObject bulletPrefab;
    [Tooltip("Необов'язково. Ефект у точці влучання (вибух, лід).")]
    [SerializeField] private GameObject hitEffect;

    [Header("Бій")]
    [SerializeField, Min(0)] private int damage = 1;
    [SerializeField, Min(0f)] private float range = 3f;
    [Tooltip("Секунд між пострілами.")]
    [SerializeField, Min(0.01f)] private float shootDelay = 1f;
    [SerializeField, Min(0f)] private float turnSpeed = 5f;
    [SerializeField, Min(0.1f)] private float bulletSpeed = 10f;

    [Header("Ефекти")]
    [Tooltip("Радіус урону по площі. 0 = б'є лише ціль.")]
    [SerializeField, Min(0f)] private float splashRadius = 0f;
    [Tooltip("Множник швидкості ворога після влучання. 1 = без сповільнення, 0.5 = удвічі повільніше.")]
    [SerializeField, Range(0.1f, 1f)] private float slowFactor = 1f;
    [Tooltip("Скільки секунд триває сповільнення.")]
    [SerializeField, Min(0f)] private float slowDuration = 0f;

    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public Color Tint => tint;
    public float Scale => scale;
    public int Cost => cost;
    public TowerData NextLevel => nextLevel;
    public GameObject Prefab => prefab;
    public GameObject BulletPrefab => bulletPrefab;
    public GameObject HitEffect => hitEffect;
    public int Damage => damage;
    public float Range => range;
    public float ShootDelay => shootDelay;
    public float TurnSpeed => turnSpeed;
    public float BulletSpeed => bulletSpeed;
    public float SplashRadius => splashRadius;
    public float SlowFactor => slowFactor;
    public float SlowDuration => slowDuration;
    public bool Slows => slowFactor < 1f && slowDuration > 0f;
}
