using System.Collections;
using UnityEngine;

// Випускає ворогів за описом рівня (LevelData): хвилі, групи в хвилях, інтервали.
// Між хвилями гравець може викликати наступну раніше і отримати бонус за кожну пропущену секунду.
public class EnemySpawner : MonoBehaviour
{
    [Tooltip("Опис хвиль цього рівня.")]
    [SerializeField] private LevelData level;
    [Tooltip("База, до якої йтимуть вороги.")]
    [SerializeField] private Finish finish;

    [Header("Наступна хвиля")]
    [Tooltip("Пауза перед першою хвилею, щоб встигнути збудувати вежі. Кнопкою Start її можна пропустити (без бонусу).")]
    [SerializeField, Min(0f)] private float firstWaveDelay = 10f;
    [Tooltip("Скільки монет дається за кожну пропущену секунду очікування, якщо викликати хвилю раніше.")]
    [SerializeField, Min(0f)] private float bonusPerSecond = 0.5f;

    private Bank bank;
    private bool skipRequested;
    private bool bonusAllowed;

    // Номер поточної хвилі (з 1). 0 — перша хвиля ще не почалася.
    public int CurrentWave { get; private set; }
    public int WaveCount => level != null ? level.Waves.Count : 0;

    // Іде відлік до наступної хвилі — можна натиснути "Наступна хвиля".
    public bool IsWaiting { get; private set; }
    public float TimeToNextWave { get; private set; }
    public int NextWaveBonus => bonusAllowed ? Mathf.FloorToInt(TimeToNextWave * bonusPerSecond) : 0;

    private void Awake()
    {
        bank = FindAnyObjectByType<Bank>();
    }

    private void Start()
    {
        StartCoroutine(SpawnWaves());
    }

    // Кнопка "Наступна хвиля": обриває відлік і нараховує бонус за час, що лишався.
    public void CallNextWave()
    {
        if (!IsWaiting || skipRequested) return;

        int bonus = NextWaveBonus;
        if (bonus > 0 && bank != null) bank.AddMoney(bonus);
        skipRequested = true;
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

        // Перед першою хвилею бонусу немає: ворогів ще нема, тож ранній старт нічим не ризикує.
        yield return WaitForNextWave(firstWaveDelay, false);

        for (int waveIndex = 0; waveIndex < level.Waves.Count; waveIndex++)
        {
            CurrentWave = waveIndex + 1;
            GameEvents.RaiseWaveStarted(CurrentWave, WaveCount);
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
                yield return WaitForNextWave(wave.delayAfter, true);
        }

        // Усі вороги випущені. Перемогу зарахує GameManager, коли помре останній.
        GameEvents.RaiseAllWavesSpawned();
    }

    // Як WaitForSeconds, але відлік видно ззовні і його можна обірвати кнопкою.
    private IEnumerator WaitForNextWave(float seconds, bool withBonus)
    {
        IsWaiting = true;
        bonusAllowed = withBonus;
        skipRequested = false;
        TimeToNextWave = seconds;

        while (TimeToNextWave > 0f && !skipRequested)
        {
            yield return null;
            TimeToNextWave -= Time.deltaTime;
        }

        TimeToNextWave = 0f;
        IsWaiting = false;
        skipRequested = false;
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
