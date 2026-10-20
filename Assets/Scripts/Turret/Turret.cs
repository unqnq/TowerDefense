using UnityEngine;

public class Turret : MonoBehaviour
{
    public float range = 3f;
    public float turnSpeed = 5f;
    public float shootDelay = 1f;
    public GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    private float timer;
    [SerializeField] private Transform target;

    private void Awake()
    {
        if (bulletPrefab == null) Debug.LogError("[Turret] Не призначено Bullet Prefab.", this);
        if (firePoint == null) Debug.LogError("[Turret] Не призначено Fire Point.", this);
    }

    private void Update()
    {
        timer -= Time.deltaTime;

        EnemyHealth enemy = FindTarget();
        target = enemy != null ? enemy.transform : null;
        if (enemy == null) return;

        // Обертаємося лише по горизонталі, щоб вежа не нахилялася.
        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            Quaternion rotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Lerp(transform.rotation,
                                                 rotation, turnSpeed * Time.deltaTime);
        }

        if (timer <= 0f)
        {
            Bullet bullet = Bullet.Spawn(bulletPrefab, firePoint.position, firePoint.rotation);
            bullet.TakeForce(enemy);
            timer = shootDelay;
        }
    }

    // Серед ворогів у радіусі обираємо того, кому найменше лишилося до бази.
    private EnemyHealth FindTarget()
    {
        float rangeSqr = range * range;
        EnemyHealth best = null;
        float bestDistance = float.MaxValue;

        var alive = EnemyHealth.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            EnemyHealth enemy = alive[i];
            if ((enemy.transform.position - transform.position).sqrMagnitude > rangeSqr)
                continue;

            float distanceToFinish = enemy.DistanceToFinish;
            if (distanceToFinish < bestDistance)
            {
                bestDistance = distanceToFinish;
                best = enemy;
            }
        }
        return best;
    }
}
