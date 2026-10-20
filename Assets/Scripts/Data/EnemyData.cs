using UnityEngine;

// Параметри одного типу ворога. Префаб відповідає лише за модель, звуки й ефекти,
// а всі ігрові числа (HP, нагорода, швидкість, урон по базі) живуть тут, щоб баланс правився в одному місці.
[CreateAssetMenu(menuName = "Tower Defense/Enemy", fileName = "Enemy")]
public class EnemyData : ScriptableObject
{
    [SerializeField] private string displayName = "Enemy";
    [Tooltip("Префаб з EnemyHealth, EnemyMovement і NavMeshAgent.")]
    [SerializeField] private GameObject prefab;

    [Header("Параметри")]
    [SerializeField, Min(1)] private int health = 3;
    [Tooltip("Скільки монет отримує гравець за вбивство.")]
    [SerializeField, Min(0)] private int reward = 1;
    [Tooltip("Скільки HP знімає з бази, якщо дійде до неї.")]
    [SerializeField, Min(0)] private int damageToBase = 10;
    [Tooltip("Швидкість руху (NavMeshAgent.speed).")]
    [SerializeField, Min(0.1f)] private float speed = 3.5f;

    public string DisplayName => displayName;
    public GameObject Prefab => prefab;
    public int Health => health;
    public int Reward => reward;
    public int DamageToBase => damageToBase;
    public float Speed => speed;
}
