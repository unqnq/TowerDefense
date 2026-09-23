using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject[] enemies;

    [Header("Налаштування хвиль")]
    [SerializeField, Min(1)] private int enemiesPerWave = 5;
    [SerializeField, Min(0f)] private float spawnInterval = 1f;
    [SerializeField, Min(0f)] private float timeBetweenWaves = 5f;

    private void Start()
    {
        StartCoroutine(SpawnWaves());
    }

    private IEnumerator SpawnWaves()
    {
        if (enemies == null || enemies.Length == 0)
        {
            Debug.LogWarning("У масив Enemy Prefabs не додано ворогів!", this);
            yield break;
        }


        for (int waveIndex = 0; waveIndex < enemies.Length; waveIndex++)
        {
            GameObject enemyPrefab = enemies[waveIndex];

            if (enemyPrefab == null)
            {
                continue;
            }

            for (int i = 0; i < enemiesPerWave; i++)
            {
                Instantiate(
                    enemyPrefab,
                    transform.position,
                    transform.rotation
                );

                yield return new WaitForSeconds(spawnInterval);
            }

            if (waveIndex < enemies.Length - 1)
            {
                yield return new WaitForSeconds(timeBetweenWaves);
            }
        }
    }
}
