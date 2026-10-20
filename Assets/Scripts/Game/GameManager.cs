using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState
{
    Playing,
    Paused,
    Won,
    Lost
}

// Єдине місце, яке керує станом гри: перемога, поразка, пауза, швидкість x2, рестарт і Time.timeScale.
public class GameManager : MonoBehaviour
{
    [SerializeField] private GameObject losePanel;
    [SerializeField] private GameObject winPanel;
    [Tooltip("Необов'язково. Якщо вказано, перед панеллю поразки показується реклама.")]
    [SerializeField] private AdsManager adsManager;

    [Tooltip("Множник швидкості для кнопки x2.")]
    [SerializeField, Min(1f)] private float fastSpeed = 2f;

    // Вибір x1/x2 зберігається між рівнями і запусками: хто любить грати швидко, не вмикає щоразу.
    private const string FastSpeedKey = "GameSpeedFast";

    public GameState State { get; private set; } = GameState.Playing;
    public bool IsFast { get; private set; }

    private bool allWavesSpawned;

    private void Awake()
    {
        IsFast = PlayerPrefs.GetInt(FastSpeedKey, 0) == 1;
        ApplyTimeScale();
        if (losePanel != null) losePanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);

        if (losePanel == null) Debug.LogError("[GameManager] Не призначено Lose Panel.", this);
        if (winPanel == null) Debug.LogError("[GameManager] Не призначено Win Panel.", this);
    }

    private void OnEnable()
    {
        GameEvents.BaseDestroyed += Lose;
        GameEvents.AllWavesSpawned += OnAllWavesSpawned;
    }

    private void OnDisable()
    {
        GameEvents.BaseDestroyed -= Lose;
        GameEvents.AllWavesSpawned -= OnAllWavesSpawned;
    }

    private void Update()
    {
        // Перемога: усі хвилі випущено і живих ворогів не лишилося.
        if (State == GameState.Playing && allWavesSpawned && EnemyHealth.Alive.Count == 0)
            Win();
    }

    private void OnAllWavesSpawned()
    {
        allWavesSpawned = true;
    }

    private void Win()
    {
        if (!SetState(GameState.Won)) return;
        Haptics.Medium();
        Time.timeScale = 0f;
        if (winPanel != null) winPanel.SetActive(true);
    }

    private void Lose()
    {
        if (!SetState(GameState.Lost)) return;
        Time.timeScale = 0f;

        if (adsManager != null)
            adsManager.ShowInterstitial(ShowLosePanel);
        else
            ShowLosePanel();
    }

    private void ShowLosePanel()
    {
        if (losePanel != null) losePanel.SetActive(true);
    }

    public void Pause()
    {
        if (State != GameState.Playing) return;
        SetState(GameState.Paused);
        ApplyTimeScale();
    }

    public void Resume()
    {
        if (State != GameState.Paused) return;
        SetState(GameState.Playing);
        ApplyTimeScale();
    }

    public void TogglePause()
    {
        if (State == GameState.Playing) Pause();
        else Resume();
    }

    public void ToggleSpeed()
    {
        IsFast = !IsFast;
        PlayerPrefs.SetInt(FastSpeedKey, IsFast ? 1 : 0);
        ApplyTimeScale();
    }

    // Час іде лише під час гри; на паузі, перемозі й поразці стоїть.
    private void ApplyTimeScale()
    {
        Time.timeScale = State == GameState.Playing ? (IsFast ? fastSpeed : 1f) : 0f;
    }

    // Автопауза: гравець згорнув гру (дзвінок, повідомлення, кнопка "додому").
    // Повертаючись, він бачить паузу, а не базу, яку вже розібрали вороги.
    private void OnApplicationPause(bool paused)
    {
        if (paused) Pause();
    }

#if !UNITY_EDITOR
    // На Android фокус губиться ще й коли гравець витягує шторку сповіщень.
    // У редакторі вимкнено, інакше гра ставала б на паузу від кожного кліку в інше вікно.
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) Pause();
    }
#endif

    // Кнопки Restart на панелях перемоги і поразки.
    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Перемога і поразка остаточні: з них не можна перейти в інший стан.
    private bool SetState(GameState newState)
    {
        if (State == GameState.Won || State == GameState.Lost) return false;
        State = newState;
        GameEvents.RaiseStateChanged(newState);
        return true;
    }
}
