using UnityEngine;

// Усе, що можна пошкодити: вороги, база, у майбутньому інші цілі.
// Куля і ворог працюють з цим інтерфейсом і не знають конкретних класів.
public interface IDamageable
{
    bool IsDead { get; }
    Transform transform { get; } // реалізується автоматично будь-яким MonoBehaviour
    void TakeDamage(int damage);
}
