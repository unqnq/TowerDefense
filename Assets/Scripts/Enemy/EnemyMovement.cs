using UnityEngine;
using UnityEngine.AI;

public class EnemyMovement : MonoBehaviour
{
    private NavMeshAgent agent;
    private Finish finish;
    [SerializeField] private GameObject particle;
    [SerializeField] private int damage;

    // Залишок шляху до бази по NavMesh. Поки шлях ще не пораховано, беремо пряму відстань.
    public float DistanceToFinish
    {
        get
        {
            if (agent != null && agent.isOnNavMesh && !agent.pathPending && agent.hasPath
                && !float.IsInfinity(agent.remainingDistance))
                return agent.remainingDistance;

            return finish != null
                ? Vector3.Distance(transform.position, finish.transform.position)
                : float.MaxValue;
        }
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        // Рухає ворога NavMeshAgent. Динамічний Rigidbody з ним конфліктує (гравітація, поштовхи),
        // тому робимо його kinematic. Тригер з базою при цьому працює як раніше.
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    // Викликає спавнер одразу після створення: передає базу, до якої треба йти.
    public void Init(Finish target)
    {
        finish = target;
    }

    private void Start()
    {
        // Запасний варіант для ворога, поставленого на сцену вручну, а не спавнером.
        // Пошук за типом, а не за іменем, тож перейменування об'єкта нічого не зламає.
        if (finish == null)
            finish = FindAnyObjectByType<Finish>();

        if (finish == null)
        {
            Debug.LogError("[EnemyMovement] На сцені немає бази (Finish).", this);
            return;
        }

        // Маршрут задаємо в Start: на цей момент агент уже точно стоїть на NavMesh.
        agent.SetDestination(finish.transform.position);
    }

    void OnTriggerEnter(Collider other)
    {
        // Перевіряємо компонент, а не тег.
        if (!other.TryGetComponent(out Finish reachedBase)) return;

        Vector3 basePos = reachedBase.transform.position;
        GameObject temp = Instantiate(particle, basePos + Vector3.up, Quaternion.identity);
        Destroy(temp, 3f);
        Destroy(gameObject);
        reachedBase.TakeDamage(damage);
    }
}
