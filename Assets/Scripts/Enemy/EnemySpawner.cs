using System.Collections;
using UnityEngine;

// Випускає ворогів за описом рівня (LevelData): хвилі, групи в хвилях, інтервали.
public class EnemySpawner : MonoBehaviour
{
    [Tooltip("Опис хвиль цього рівня.")]
    [SerializeField] private LevelData level;
    [Tooltip("База, до якої йтимуть вороги.")]
    [SerializeField] private Finish finish;

    // Для майбутнього лічильника хвиль в інтерфейсі і кнопки "Наступна хвиля".
    public int CurrentWave { get; private set; }
    public int WaveCount => level != null ? level.Waves.Count : 0;

    private void Start()
    {
        StartCoroutine(SpawnWaves());
    }

    private IEnumerator SpawnWaves()
    {
        if (level == null || level.Waves.Count == 0)
        {
            Debug.LogWarning("[EnemySpawner] Не призначено Level або в ньому немає хвиль.", this);
            yield break;
        }
        if (finish == null)
        {
            Debug.LogError("[EnemySpawner] Не призначено Finish.", this);
            yield break;
        }

        for (int waveIndex = 0; waveIndex < level.Waves.Count; waveIndex++)
        {
            CurrentWave = waveIndex + 1;
            Wave wave = level.Waves[waveIndex];

            foreach (SpawnGroup group in wave.groups)
            {
                if (group.enemy == null || group.enemy.Prefab == null)
                {
                    Debug.LogWarning($"[EnemySpawner] Хвиля {CurrentWave}: група без ворога або без префаба, пропускаю.", this);
                    continue;
                }

                for (int i = 0; i < group.count; i++)
                {
                    Spawn(group.enemy);
                    yield return new WaitForSeconds(group.spawnInterval);
                }
            }

            if (waveIndex < level.Waves.Count - 1)
                yield return new WaitForSeconds(wave.delayAfter);
        }

        // Усі вороги випущені. Перемогу зарахує GameManager, коли помре останній.
        GameEvents.RaiseAllWavesSpawned();
    }

    private void Spawn(EnemyData data)
    {
        GameObject enemy = Instantiate(data.Prefab, transform.position, transform.rotation);

        if (enemy.TryGetComponent(out EnemyHealth health))
            health.Init(data);
        if (enemy.TryGetComponent(out EnemyMovement movement))
            movement.Init(finish, data);
    }
}
