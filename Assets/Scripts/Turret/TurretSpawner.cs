using System.Collections.Generic;
using UnityEngine;

// Економіка веж: будівництво, merge і продаж (оплата + створення/заміна/знищення).
public class TurretSpawner : MonoBehaviour
{
    [Tooltip("Вежі першого рівня, які можна будувати на цьому рівні. У такому ж порядку вони йдуть у меню.")]
    [SerializeField] private TowerData[] towers;
    [Tooltip("Куди складати створені вежі в ієрархії.")]
    [SerializeField] private Transform turrets;
    [SerializeField] private Bank bank;
    [Tooltip("Яку частку вкладених грошей (усі вежі, що злилися в цю) повертає продаж.")]
    [SerializeField, Range(0f, 1f)] private float sellRefund = 0.7f;

    public IReadOnlyList<TowerData> Towers => towers;

    void Awake()
    {
        if (towers == null || towers.Length == 0) Debug.LogError("[TurretSpawner] Не додано жодної TowerData.", this);
        if (bank == null) Debug.LogError("[TurretSpawner] Не призначено Bank.", this);
    }

    public bool CanAfford(TowerData data) => data != null && bank.CanAfford(data.Cost);

    // Злити можна лише однакові вежі: той самий тип і рівень (тобто той самий asset) і не максимальний рівень.
    public bool CanMerge(Turret source, Turret target) =>
        source != null && target != null && source != target
        && source.Data == target.Data && target.Data.NextLevel != null;

    public int GetSellPrice(Turret turret) => Mathf.FloorToInt(turret.TotalSpent * sellRefund);

    // Повертає створену вежу або null, якщо грошей не вистачило чи дані некоректні.
    public Turret Spawn(TowerData data, Vector3 place)
    {
        if (data == null || data.Prefab == null)
        {
            Debug.LogError("[TurretSpawner] TowerData порожня або без префаба.", this);
            return null;
        }
        if (!bank.TrySpendMoney(data.Cost)) return null;

        return Create(data, place, Quaternion.identity, data.Cost, 1);
    }

    // Зливає source у target: target стає наступного рівня, source зникає. Безкоштовно — ціна merge це друга вежа.
    // Повертає вежу-результат (той самий target або новий об'єкт, якщо в рівня інша модель) або null.
    public Turret Merge(Turret source, Turret target)
    {
        if (!CanMerge(source, target)) return null;

        TowerData next = target.Data.NextLevel;
        int spent = source.TotalSpent + target.TotalSpent;
        int level = target.Level + 1;
        Vector3 oldScale = target.transform.localScale; // з цього розміру вежа "виросте"
        Destroy(source.gameObject);

        Turret merged;
        if (next.Prefab == null || next.Prefab == target.Data.Prefab)
        {
            // Та сама модель: просто оновлюємо параметри.
            target.Init(next, spent, level);
            merged = target;
        }
        else
        {
            // Інша модель: ставимо нову на місце target з тим самим поворотом.
            merged = Create(next, target.transform.position, target.transform.rotation, spent, level);
            Destroy(target.gameObject);
        }

        if (merged != null) merged.PlayMergePop(oldScale);
        return merged;
    }

    public void Sell(Turret turret)
    {
        if (turret == null) return;
        bank.AddMoney(GetSellPrice(turret));
        Destroy(turret.gameObject);
    }

    private Turret Create(TowerData data, Vector3 position, Quaternion rotation, int spent, int level)
    {
        GameObject turretObject = Instantiate(data.Prefab, position, rotation, turrets);
        Turret turret = turretObject.GetComponent<Turret>();
        if (turret != null) turret.Init(data, spent, level);
        else Debug.LogError($"[TurretSpawner] На префабі '{data.Prefab.name}' немає компонента Turret.", data.Prefab);
        return turret;
    }
}
