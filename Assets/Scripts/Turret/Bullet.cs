using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 10f;
    public int damage = 1;

    // Куля летить у будь-що, що можна пошкодити, і не знає конкретних класів.
    private IDamageable target;

    // Пул куль: використані кулі ховаються і використовуються знову,
    // замість Instantiate/Destroy на кожен постріл (менше фризів від збирача сміття на телефоні).
    private static readonly Stack<Bullet> pool = new Stack<Bullet>();

    public static Bullet Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        while (pool.Count > 0)
        {
            Bullet bullet = pool.Pop();
            if (bullet == null) continue; // знищена разом із попередньою сценою

            bullet.transform.SetPositionAndRotation(position, rotation);
            bullet.gameObject.SetActive(true);
            return bullet;
        }
        return Instantiate(prefab, position, rotation).GetComponent<Bullet>();
    }

    public void TakeForce(IDamageable newTarget)
    {
        target = newTarget;
    }

    private void Update()
    {
        // Ціль зникла або вже мертва: куля більше не потрібна.
        // (Object)target == null ловить і знищений Unity-об'єкт, а не лише порожнє посилання.
        if (target == null || (target as Object) == null || target.IsDead)
        {
            Release();
            return;
        }

        Transform targetTransform = target.transform;
        transform.position = Vector3.MoveTowards(transform.position,
                                                 targetTransform.position,
                                                 speed * Time.deltaTime);
        transform.LookAt(targetTransform);

        if (Vector3.Distance(transform.position, targetTransform.position) < 0.1f)
        {
            target.TakeDamage(damage);
            Release();
        }
    }

    private void Release()
    {
        target = null;
        gameObject.SetActive(false);
        pool.Push(this);
    }
}
