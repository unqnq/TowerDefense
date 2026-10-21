using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Що зараз грається: який рівень, чи це щоденне випробування, і переходи між сценами.
// Статичний клас, бо має пережити завантаження сцени (меню → рівень → меню).
// Якщо сцену рівня запустити прямо з редактора, грається перший рівень з GameConfig.
public static class LevelSession
{
    private static int levelIndex = -1;

    public static bool IsDaily { get; private set; }
    public static int DailyDay { get; private set; }
    public static DailyModifier Daily { get; private set; }

    // Скільки рівнів завершено з моменту останньої interstitial-реклами.
    public static int LevelsSinceAd { get; set; }

    public static int LevelIndex
    {
        get
        {
            if (levelIndex < 0) levelIndex = 0;
            return levelIndex;
        }
    }

    public static LevelData Level
    {
        get
        {
            GameConfig config = GameConfig.Instance;
            if (config == null || config.levels.Count == 0) return null;
            return config.levels[Mathf.Clamp(LevelIndex, 0, config.levels.Count - 1)];
        }
    }

    public static bool HasNextLevel =>
        !IsDaily && GameConfig.Instance != null && LevelIndex + 1 < GameConfig.Instance.levels.Count;

    // ---------- Переходи ----------

    public static void PlayLevel(int index)
    {
        levelIndex = index;
        IsDaily = false;
        Daily = null;
        LoadLevelScene();
    }

    public static void PlayDaily()
    {
        DailyDay = DailyChallenge.Today;
        levelIndex = DailyChallenge.LevelIndex(DailyDay);
        Daily = DailyChallenge.Modifier(DailyDay);
        IsDaily = true;
        LoadLevelScene();
    }

    public static void PlayNextLevel() => PlayLevel(LevelIndex + 1);

    public static void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

    public static void GoToMenu()
    {
        IsDaily = false;
        Daily = null;
        SceneManager.LoadScene(GameConfig.Instance.menuScene);
    }

    private static void LoadLevelScene()
    {
        LevelData level = Level;
        SceneManager.LoadScene(level != null ? level.SceneName : "Level");
    }

    // ---------- Параметри поточного рівня з урахуванням покращень і випробування ----------

    public static int StartMoney(int fallback)
    {
        float money = Level != null ? Level.StartMoney : fallback;
        if (IsDaily) money *= Daily.startMoney;
        return Mathf.RoundToInt(money + GameModifiers.UpgradeValue(UpgradeType.StartMoney));
    }

    public static int BaseHealth(int fallback)
    {
        float health = Level != null ? Level.BaseHealth : fallback;
        return Mathf.RoundToInt(health * (1f + GameModifiers.UpgradeValue(UpgradeType.BaseHealth)));
    }

    // Вежі, доступні на рівні: обмеження випробування важливіше за обмеження рівня.
    public static List<TowerData> FilterTowers(IReadOnlyList<TowerData> all)
    {
        IReadOnlyList<TowerData> allowed = null;
        if (IsDaily && Daily.allowedTowers != null && Daily.allowedTowers.Length > 0) allowed = Daily.allowedTowers;
        else if (Level != null && Level.AllowedTowers != null && Level.AllowedTowers.Count > 0) allowed = Level.AllowedTowers;

        var result = new List<TowerData>();
        foreach (TowerData tower in all)
            if (tower != null && (allowed == null || Contains(allowed, tower)))
                result.Add(tower);
        return result.Count > 0 ? result : new List<TowerData>(all);
    }

    private static bool Contains(IReadOnlyList<TowerData> list, TowerData tower)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == tower) return true;
        return false;
    }
}
