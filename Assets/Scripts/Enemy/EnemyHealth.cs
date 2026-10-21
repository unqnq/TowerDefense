using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    // Реєстр живих ворогів. Вежі беруть цілі звідси, GameManager перевіряє перемогу.
    private static readonly List<EnemyHealth> alive = new List<EnemyHealth>();
    public static IReadOnlyList<EnemyHealth> Alive => alive;

    // Префаб відповідає лише за вигляд і звук. HP, нагорода і здібності приходять з EnemyData через Init.
    [SerializeField] private GameObject effect;
    [Tooltip("Скільки секунд тіло ворога ще лишається на сцені після смерті (щоб дограв звук).")]
    [SerializeField] private float destroyDelay = 0.6f;

    private float health = 1f;
    private float maxHealth = 1f;
    private int reward;
    private float armor;
    private int revivesLeft;
    private float reviveHealth;
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
        maxHealth = data.Health * GameModifiers.EnemyHealth; // щоденне випробування може посилити ворогів
        health = maxHealth;
        reward = data.Reward;
        armor = data.Armor;
        revivesLeft = data.Revives;
        reviveHealth = data.ReviveHealth;

        Tint.Apply(gameObject, data.Tint);
        transform.localScale *= data.Scale;
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

    public void TakeDamage(float damage)
    {
        // Мертвий ворог більше не отримує урон, тож нагорода нараховується рівно один раз.
        if (IsDead || damage <= 0f) return;

        // Броня поглинає частину кожного влучання, але не більше 80%: слабкі вежі все одно корисні.
        if (armor > 0f) damage = Mathf.Max(damage - armor, damage * 0.2f);

        health -= damage;
        if (health > 0f) return;

        if (revivesLeft > 0) Revive();
        else Die();
    }

    // "Дев'ять життів": ворог встає з частиною HP. Ефект смерті показуємо, щоб гравець помітив.
    private void Revive()
    {
        revivesLeft--;
        health = maxHealth * reviveHealth;
        if (effect != null)
            Destroy(Instantiate(effect, transform.position, Quaternion.identity), 1f);
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
        // Бустер "монети за вбивство" множить нагороду саме в момент смерті.
        GameEvents.RaiseEnemyKilled(Mathf.RoundToInt(reward * GameModifiers.CoinReward));
        Destroy(gameObject, destroyDelay);
    }
}
