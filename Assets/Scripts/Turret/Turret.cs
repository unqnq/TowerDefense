using UnityEngine;

// Поведінка вежі: пошук цілі, поворот, стрільба. Усі числа беруться з TowerData,
// тож різні типи веж можуть використовувати один і той самий скрипт.
public class Turret : MonoBehaviour
{
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform target;

    private float timer;
    private Vector3 originalScale;

    public TowerData Data { get; private set; }
    public int Level { get; private set; } = 1;

    // Скільки гравець загалом заплатив за вежу (усі вежі, які злилися в цю). Від цього рахується ціна продажу.
    public int TotalSpent { get; private set; }

    private void Awake()
    {
        originalScale = transform.localScale;
        if (firePoint == null) Debug.LogError("[Turret] Не призначено Fire Point.", this);
    }

    // Коротке "надування" після merge, щоб гравець побачив, що вежа покращилась.
    public void PlayMergePop()
    {
        StopAllCoroutines();
        StartCoroutine(MergePop());
    }

    private System.Collections.IEnumerator MergePop()
    {
        const float duration = 0.25f;
        Vector3 baseScale = originalScale;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = 1f + 0.35f * Mathf.Sin(t / duration * Mathf.PI);
            transform.localScale = baseScale * k;
            yield return null;
        }
        transform.localScale = baseScale;
    }

    // Викликає TurretSpawner після створення вежі і після merge.
    public void Init(TowerData data, int totalSpent, int level)
    {
        Data = data;
        TotalSpent = totalSpent;
        Level = level;
        Tint.Apply(gameObject, data.Tint);

        if (data.BulletPrefab == null)
            Debug.LogError($"[Turret] У TowerData '{data.name}' не призначено Bullet Prefab.", data);
    }

    private void Update()
    {
        if (Data == null || Data.BulletPrefab == null) return;

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
                                                 rotation, Data.TurnSpeed * Time.deltaTime);
        }

        if (timer <= 0f)
        {
            Bullet bullet = Bullet.Spawn(Data.BulletPrefab, firePoint.position, firePoint.rotation);
            bullet.Launch(enemy, Data);
            timer = Data.ShootDelay;
        }
    }

    // Серед ворогів у радіусі обираємо того, кому найменше лишилося до бази.
    private EnemyHealth FindTarget()
    {
        float rangeSqr = Data.Range * Data.Range;
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
