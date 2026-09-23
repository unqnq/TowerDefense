using System;
using UnityEngine;

public class Turret : MonoBehaviour
{
    public float range = 3f;
    public float turnSpeed = 5f;
    public float shootDelay = 1f;
    public GameObject bulletPrefab;
    private Transform firePoint;
    private float timer;
    [SerializeField] private Transform target;

    private void Start()
    {
        bulletPrefab = Resources.Load<GameObject>("Bullet");
        firePoint = transform.Find("FirePoint");
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        if (enemies.Length == 0) return;
        target = enemies[0].transform;
        foreach (GameObject enemy in enemies)
        {
            float distance = Vector3.Distance(
                enemy.transform.position, transform.position);

            float targetDistance = Vector3.Distance(
                target.position, transform.position);

            if (distance < targetDistance)
            {
                target = enemy.transform;
            }
        }
        if (Vector3.Distance(transform.position, target.position) > range)
            return;

        if (Vector3.Distance(transform.position, target.position) > range) return;
        Vector3 direction = target.position - transform.position;
        Quaternion rotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Lerp(transform.rotation,
                                            rotation, turnSpeed * Time.deltaTime);
        if (timer <= 0f)
        {
            GameObject bullet = Instantiate(bulletPrefab,
                                            firePoint.position,
                                            firePoint.rotation);
            bullet.GetComponent<Bullet>().TakeForce(target);
            timer = shootDelay;
        }
    }
}
