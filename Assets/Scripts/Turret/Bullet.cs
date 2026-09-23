using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 10f;
    public int damage = 1;

    [SerializeField] private Transform target;

    public void TakeForce(Transform enemy)
    {
        target = enemy;
        return;
    }

    private void Update()
    {
        if (!target)
        {
            Destroy(gameObject);
            return;
        }
        transform.position = Vector3.MoveTowards(transform.position,
                                                target.position,
                                                speed * Time.deltaTime);
        transform.LookAt(target);

        if (Vector3.Distance(transform.position, target.position) < 0.1f)
        {
            target.GetComponent<EnemyHealth>().TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}