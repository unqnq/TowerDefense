using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState
{
    Playing,
    Paused,
    Won,
    Lost
}

// Єдине місце, яке керує станом гри: перемога, поразка, пауза, рестарт і Time.timeScale.
public class GameManager : MonoBehaviour
{
    [SerializeField] private GameObject losePanel;
    [SerializeField] private GameObject winPanel;
    [Tooltip("Необов'язково. Якщо вказано, перед панеллю поразки показується реклама.")]
    [SerializeField] private AdsManager adsManager;

    public GameState State { get; private set; } = GameState.Playing;

    private bool allWavesSpawned;

    private void Awake()
    {
        Time.timeScale = 1f;
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

    // Для майбутньої кнопки паузи.
    public void Pause()
    {
        if (State != GameState.Playing) return;
        SetState(GameState.Paused);
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        if (State != GameState.Paused) return;
        SetState(GameState.Playing);
        Time.timeScale = 1f;
    }

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
