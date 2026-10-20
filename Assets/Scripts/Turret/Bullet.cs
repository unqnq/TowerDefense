using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    // Урон, швидкість і ефекти бере з TowerData вежі, яка вистрілила.
    // Тому одна й та сама куля підходить і звичайній, і гарматній, і крижаній вежі.
    private TowerData data;

    // Куля летить у будь-що, що можна пошкодити, і не знає конкретних класів.
    private IDamageable target;

    // Пул куль: використані кулі ховаються і використовуються знову,
    // замість Instantiate/Destroy на кожен постріл (менше фризів від збирача сміття на телефоні).
    // Окремий пул на кожен префаб, щоб різні вежі не отримували чужі кулі.
    private static readonly Dictionary<GameObject, Stack<Bullet>> pools = new Dictionary<GameObject, Stack<Bullet>>();
    private GameObject sourcePrefab;

    // Тимчасовий список для урону по площі: копія, бо вбиті вороги видаляються з реєстру прямо під час обходу.
    private static readonly List<EnemyHealth> splashBuffer = new List<EnemyHealth>();

    public static Bullet Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!pools.TryGetValue(prefab, out Stack<Bullet> pool))
        {
            pool = new Stack<Bullet>();
            pools[prefab] = pool;
        }

        while (pool.Count > 0)
        {
            Bullet bullet = pool.Pop();
            if (bullet == null) continue; // знищена разом із попередньою сценою

            bullet.transform.SetPositionAndRotation(position, rotation);
            bullet.gameObject.SetActive(true);
            return bullet;
        }

        Bullet created = Instantiate(prefab, position, rotation).GetComponent<Bullet>();
        created.sourcePrefab = prefab;
        return created;
    }

    public void Launch(IDamageable newTarget, TowerData towerData)
    {
        target = newTarget;
        data = towerData;
        Tint.Apply(gameObject, data.Tint); // кулі з пулу могли бути пофарбовані іншою вежею
    }

    private void Update()
    {
        // Ціль зникла або вже мертва: куля більше не потрібна.
        // (Object)target == null ловить і знищений Unity-об'єкт, а не лише порожнє посилання.
        if (target == null || (target as Object) == null || target.IsDead || data == null)
        {
            Release();
            return;
        }

        Transform targetTransform = target.transform;
        transform.position = Vector3.MoveTowards(transform.position,
                                                 targetTransform.position,
                                                 data.BulletSpeed * Time.deltaTime);
        transform.LookAt(targetTransform);

        if (Vector3.Distance(transform.position, targetTransform.position) < 0.1f)
        {
            Hit(targetTransform.position);
            Release();
        }
    }

    private void Hit(Vector3 point)
    {
        if (data.SplashRadius > 0f)
        {
            // Урон по площі: усі живі вороги в радіусі від точки влучання.
            float radiusSqr = data.SplashRadius * data.SplashRadius;
            splashBuffer.Clear();
            var alive = EnemyHealth.Alive;
            for (int i = 0; i < alive.Count; i++)
                if ((alive[i].transform.position - point).sqrMagnitude <= radiusSqr)
                    splashBuffer.Add(alive[i]);

            foreach (EnemyHealth enemy in splashBuffer)
                ApplyHit(enemy);
        }
        else
        {
            ApplyHit(target);
        }

        if (data.HitEffect != null)
            Destroy(Instantiate(data.HitEffect, point, Quaternion.identity), 2f);
    }

    private void ApplyHit(IDamageable victim)
    {
        // Спершу сповільнення, потім урон: якщо ворог помре, сповільнювати вже нікого.
        if (data.Slows && victim is Component component
            && component.TryGetComponent(out EnemyMovement movement))
            movement.ApplySlow(data.SlowFactor, data.SlowDuration);

        if (data.Damage > 0) victim.TakeDamage(data.Damage);
    }

    private void Release()
    {
        target = null;
        data = null;
        gameObject.SetActive(false);
        if (sourcePrefab != null && pools.TryGetValue(sourcePrefab, out Stack<Bullet> pool))
            pool.Push(this);
        else
            Destroy(gameObject); // куля не з пулу (наприклад, поставлена на сцену вручну)
    }
}
