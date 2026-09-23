using UnityEngine;

public class TurretSpawner : MonoBehaviour
{
    [SerializeField] private GameObject turretPref;
    [SerializeField] private Transform turrets;
    [SerializeField] private int cost = 5;

    void Awake()
    {
        turretPref = Resources.Load<GameObject>("Turret");
        turrets = GameObject.Find("Level").transform;
    }

    public void Spawn(Vector3 place)
    {
        if (Bank.instance.TrySpendMoney(cost))
        {
            GameObject temp = Instantiate(turretPref, place, Quaternion.identity);
            temp.transform.SetParent(turrets);
            Bank.instance.SpendMoney(cost);
        }
    }
}
