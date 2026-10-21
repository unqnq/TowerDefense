using UnityEngine;

// Параметри одного типу ворога. Префаб відповідає лише за модель, звуки й ефекти,
// а всі ігрові числа (HP, нагорода, швидкість, урон по базі, здібності) живуть тут.
// Один префаб може мати кілька EnemyData: наприклад, звичайний краб і броньований краб.
[CreateAssetMenu(menuName = "Tower Defense/Enemy", fileName = "Enemy")]
public class EnemyData : ScriptableObject
{
    [SerializeField] private string displayName = "Enemy";
    [Tooltip("Префаб з EnemyHealth, EnemyMovement і NavMeshAgent.")]
    [SerializeField] private GameObject prefab;
    [Tooltip("Колір моделі. Білий = без змін. Допомагає відрізнити посиленого ворога від звичайного.")]
    [SerializeField] private Color tint = Color.white;
    [Tooltip("Множник розміру моделі.")]
    [SerializeField, Min(0.1f)] private float scale = 1f;

    [Header("Параметри")]
    [SerializeField, Min(1)] private int health = 3;
    [Tooltip("Скільки монет отримує гравець за вбивство.")]
    [SerializeField, Min(0)] private int reward = 1;
    [Tooltip("Скільки HP знімає з бази, якщо дійде до неї.")]
    [SerializeField, Min(0)] private int damageToBase = 10;
    [Tooltip("Швидкість руху (NavMeshAgent.speed).")]
    [SerializeField, Min(0.1f)] private float speed = 3.5f;

    [Header("Здібність: броня")]
    [Tooltip("Скільки урону поглинає кожне влучання. Ворог завжди отримує щонайменше 20% урону.")]
    [SerializeField, Min(0f)] private float armor = 0f;

    [Header("Здібність: воскресіння")]
    [Tooltip("Скільки разів ворог встає після смерті.")]
    [SerializeField, Min(0)] private int revives = 0;
    [Tooltip("З якою часткою HP він встає.")]
    [SerializeField, Range(0.1f, 1f)] private float reviveHealth = 0.5f;

    [Header("Здібність: ривок")]
    [Tooltip("Як часто ворог робить ривок (секунд). 0 = без ривків.")]
    [SerializeField, Min(0f)] private float dashEvery = 0f;
    [SerializeField, Min(0f)] private float dashDuration = 0.6f;
    [Tooltip("У скільки разів швидшає під час ривка.")]
    [SerializeField, Min(1f)] private float dashSpeed = 2.5f;

    public string DisplayName => displayName;
    public GameObject Prefab => prefab;
    public Color Tint => tint;
    public float Scale => scale;
    public int Health => health;
    public int Reward => reward;
    public int DamageToBase => damageToBase;
    public float Speed => speed;
    public float Armor => armor;
    public int Revives => revives;
    public float ReviveHealth => reviveHealth;
    public float DashEvery => dashEvery;
    public float DashDuration => dashDuration;
    public float DashSpeed => dashSpeed;
}
