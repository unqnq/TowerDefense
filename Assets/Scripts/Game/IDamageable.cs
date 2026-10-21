using UnityEngine;

// Усе, що можна пошкодити: вороги, база, у майбутньому інші цілі.
// Куля і ворог працюють з цим інтерфейсом і не знають конкретних класів.
// Урон дробовий, щоб покращення на +10% мали ефект навіть для веж з уроном 1.
public interface IDamageable
{
    bool IsDead { get; }
    Transform transform { get; } // реалізується автоматично будь-яким MonoBehaviour
    void TakeDamage(float damage);
}
