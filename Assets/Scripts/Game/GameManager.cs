using System;
using UnityEngine;

public enum GameState
{
    Playing,
    Paused,
    Won,
    Lost
}

// Єдине місце, яке керує станом гри: перемога, поразка, друге життя, пауза, швидкість x2,
// переходи між рівнями і Time.timeScale.
public class GameManager : MonoBehaviour
{
    [Tooltip("Необов'язково. Старі панелі зі сцени. Якщо на сцені є GameHud, використовується його екран результату.")]
    [SerializeField] private GameObject losePanel;
    [SerializeField] private GameObject winPanel;
    [Tooltip("Реклама: друге життя і interstitial між рівнями.")]
    [SerializeField] private AdsManager adsManager;

    [Tooltip("Множник швидкості для кнопки x2.")]
    [SerializeField, Min(1f)] private float fastSpeed = 2f;

    // Вибір x1/x2 зберігається між рівнями і запусками: хто любить грати швидко, не вмикає щоразу.
    private const string FastSpeedKey = "GameSpeedFast";

    public GameState State { get; private set; } = GameState.Playing;
    public bool IsFast { get; private set; }

    // Гра "заморожена" вікном поверх неї (вибір бустера), але це не пауза: HUD паузи не показується.
    public bool IsHeld { get; private set; }

    // Результат рівня для екрана перемоги.
    public int Stars { get; private set; }
    public bool NewRecord { get; private set; }
    public int DailyReward { get; private set; }

    // Друге життя можна взяти лише раз за рівень.
    public bool SecondLifeUsed { get; private set; }
    public bool CanOfferSecondLife => State == GameState.Lost && !SecondLifeUsed && adsManager != null;
    public AdsManager Ads => adsManager;

    private bool allWavesSpawned;
    private Finish finish;
    private bool useLegacyPanels;

    private void Awake()
    {
        if (adsManager == null) adsManager = FindAnyObjectByType<AdsManager>();
        finish = FindAnyObjectByType<Finish>();
        useLegacyPanels = FindAnyObjectByType<GameHud>() == null;

        GameModifiers.ResetForLevel();
        IsFast = PlayerPrefs.GetInt(FastSpeedKey, 0) == 1;
        ApplyTimeScale();
        if (losePanel != null) losePanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);
    }

    private void OnEnable()
    {
        GameEvents.BaseDestroyed += Lose;
        GameEvents.AllWavesSpawned += OnAllWavesSpawned;
        AdsManager.AdFinished += ApplyTimeScale;
    }

    private void OnDisable()
    {
        GameEvents.BaseDestroyed -= Lose;
        GameEvents.AllWavesSpawned -= OnAllWavesSpawned;
        AdsManager.AdFinished -= ApplyTimeScale;
    }

    private void Update()
    {
        GameModifiers.Tick(Time.deltaTime);

        // Перемога: усі хвилі випущено і живих ворогів не лишилося.
        if (State == GameState.Playing && allWavesSpawned && EnemyHealth.Alive.Count == 0)
            Win();
    }

    private void OnAllWavesSpawned()
    {
        allWavesSpawned = true;
    }

    // ---------- Перемога і поразка ----------

    private void Win()
    {
        if (!SetState(GameState.Won)) return;
        ApplyTimeScale();
        Haptics.Medium();

        LevelData level = LevelSession.Level;
        Stars = level != null && finish != null ? level.StarsFor(finish.HealthFraction) : 1;
        if (LevelSession.IsDaily)
            DailyReward = Progress.CompleteDaily(LevelSession.DailyDay);
        else
            NewRecord = Progress.SaveStars(level, Stars);

        GameEvents.RaiseLevelFinished(true);
        if (useLegacyPanels && winPanel != null) winPanel.SetActive(true);
    }

    private void Lose()
    {
        if (!SetState(GameState.Lost)) return;
        ApplyTimeScale();
        GameEvents.RaiseLevelFinished(false);
        if (useLegacyPanels && losePanel != null) losePanel.SetActive(true);
    }

    // Кнопка "Second chance": після перегляду реклами база відновлює частину HP і гра триває.
    public void RequestSecondLife(Action onUnavailable)
    {
        if (!CanOfferSecondLife) return;
        adsManager.ShowRewarded(Revive, onUnavailable);
    }

    private void Revive()
    {
        if (State != GameState.Lost || SecondLifeUsed) return;
        SecondLifeUsed = true;
        if (finish != null) finish.Heal(GameConfig.Instance != null ? GameConfig.Instance.secondLifeHealth : 0.5f);

        // Єдиний дозволений вихід із поразки, тому повз SetState.
        State = GameState.Playing;
        GameEvents.RaiseStateChanged(State);
        ApplyTimeScale();
    }

    // ---------- Пауза, швидкість, заморожування ----------

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

    public void Hold(bool hold)
    {
        IsHeld = hold;
        ApplyTimeScale();
    }

    // Час іде лише під час гри; на паузі, перемозі, поразці, вікні бустера і рекламі стоїть.
    private void ApplyTimeScale()
    {
        if (AdsManager.IsShowingAd) return; // під час реклами timeScale тримає AdsManager
        bool running = State == GameState.Playing && !IsHeld;
        Time.timeScale = running ? (IsFast ? fastSpeed : 1f) : 0f;
    }

    // Автопауза: гравець згорнув гру (дзвінок, повідомлення, кнопка "додому").
    // Під час реклами не спрацьовує: реклама теж забирає фокус.
    private void OnApplicationPause(bool paused)
    {
        if (paused && !AdsManager.IsShowingAd) Pause();
    }

#if !UNITY_EDITOR
    // На Android фокус губиться ще й коли гравець витягує шторку сповіщень.
    // У редакторі вимкнено, інакше гра ставала б на паузу від кожного кліку в інше вікно.
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && !AdsManager.IsShowingAd) Pause();
    }
#endif

    // ---------- Переходи ----------
    // Interstitial показується лише при виході з рівня і не частіше, ніж раз на N рівнів.

    public void Restart() => Leave(LevelSession.Restart);
    public void NextLevel() => Leave(LevelSession.PlayNextLevel);
    public void ToMenu() => Leave(LevelSession.GoToMenu);

    private void Leave(Action load)
    {
        bool finished = State == GameState.Won || State == GameState.Lost;
        if (finished) LevelSession.LevelsSinceAd++;

        int every = GameConfig.Instance != null ? GameConfig.Instance.interstitialEveryLevels : 3;
        if (finished && adsManager != null && LevelSession.LevelsSinceAd >= every)
        {
            LevelSession.LevelsSinceAd = 0;
            adsManager.ShowInterstitial(() => LoadScene(load));
        }
        else
        {
            LoadScene(load);
        }
    }

    private static void LoadScene(Action load)
    {
        Time.timeScale = 1f;
        load();
    }

    // Перемога і поразка остаточні: з них не можна перейти в інший стан (крім другого життя).
    private bool SetState(GameState newState)
    {
        if (State == GameState.Won || State == GameState.Lost) return false;
        State = newState;
        GameEvents.RaiseStateChanged(newState);
        return true;
    }
}
