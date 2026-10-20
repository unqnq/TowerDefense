using UnityEngine;

public class TurretSpawner : MonoBehaviour
{
    [SerializeField] private GameObject turretPref;
    [Tooltip("Куди складати створені вежі в ієрархії.")]
    [SerializeField] private Transform turrets;
    [SerializeField] private Bank bank;
    [SerializeField] private int cost = 5;

    void Awake()
    {
        if (turretPref == null) Debug.LogError("[TurretSpawner] Не призначено Turret Pref.", this);
        if (bank == null) Debug.LogError("[TurretSpawner] Не призначено Bank.", this);
    }

    // Повертає true, якщо вежу поставлено (грошей вистачило).
    public bool Spawn(Vector3 place)
    {
        if (!bank.TrySpendMoney(cost)) return false;

        Instantiate(turretPref, place, Quaternion.identity, turrets);
        return true;
    }
}
