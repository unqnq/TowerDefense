using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    // Реєстр живих ворогів. Вежі беруть цілі звідси, GameManager перевіряє перемогу.
    private static readonly List<EnemyHealth> alive = new List<EnemyHealth>();
    public static IReadOnlyList<EnemyHealth> Alive => alive;

    [SerializeField] private int health = 3;
    [SerializeField] private int reward = 3;
    [SerializeField] private GameObject effect;
    [Tooltip("Скільки секунд тіло ворога ще лишається на сцені після смерті (щоб дограв звук).")]
    [SerializeField] private float destroyDelay = 0.6f;

    private AudioSource audioSource;
    private EnemyMovement movement;

    public bool IsDead { get; private set; }

    // Скільки ворогу лишилося йти до бази (для вибору цілі вежею).
    public float DistanceToFinish => movement != null ? movement.DistanceToFinish : float.MaxValue;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        movement = GetComponent<EnemyMovement>();
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
