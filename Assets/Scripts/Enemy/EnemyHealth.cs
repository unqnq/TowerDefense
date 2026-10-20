using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    // Реєстр живих ворогів. Вежі беруть цілі звідси, GameManager перевіряє перемогу.
    private static readonly List<EnemyHealth> alive = new List<EnemyHealth>();
    public static IReadOnlyList<EnemyHealth> Alive => alive;

    // Префаб відповідає лише за вигляд і звук. HP і нагорода приходять з EnemyData через Init.
    [SerializeField] private GameObject effect;
    [Tooltip("Скільки секунд тіло ворога ще лишається на сцені після смерті (щоб дограв звук).")]
    [SerializeField] private float destroyDelay = 0.6f;

    private int health = 1;
    private int reward;
    private AudioSource audioSource;
    private EnemyMovement movement;

    public EnemyData Data { get; private set; }
    public bool IsDead { get; private set; }

    // Скільки ворогу лишилося йти до бази (для вибору цілі вежею).
    public float DistanceToFinish => movement != null ? movement.DistanceToFinish : float.MaxValue;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        movement = GetComponent<EnemyMovement>();
    }

    // Викликає спавнер одразу після створення ворога.
    public void Init(EnemyData data)
    {
        Data = data;
        health = data.Health;
        reward = data.Reward;
    }

    private void Start()
    {
        if (Data == null)
            Debug.LogWarning("[EnemyHealth] Ворог створений без EnemyData (не через спавнер), HP = 1.", this);
    }

    private void OnEnable()
    {
        if (!IsDead) alive.Add(this);
    }

    private void OnDisable()
    {
        alive.Remove(this);
    }

    public void TakeDamage(int damage)
    {
        // Мертвий ворог більше не отримує урон, тож нагорода нараховується рівно один раз.
        if (IsDead) return;

        health -= damage;
        if (health <= 0) Die();
    }

    private void Die()
    {
        IsDead = true;
        alive.Remove(this); // вежі одразу перестають у нього цілитися

        // Зупиняємо ворога і вимикаємо колайдери, щоб "мертвий" не дійшов до бази.
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        foreach (Collider col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        if (audioSource != null) audioSource.Play();
        if (effect != null)
            Destroy(Instantiate(effect, transform.position, Quaternion.identity), 1f);

        // Повідомляємо про вбивство. Гроші нарахує банк, бо він підписаний на цю подію.
        GameEvents.RaiseEnemyKilled(reward);
        Destroy(gameObject, destroyDelay);
    }
}
