using UnityEngine;

// Збережений прогрес гравця (PlayerPrefs): зірки за рівні, бонусні зірки, куплені покращення, щоденні випробування.
// Зірки рівня зберігаються за назвою asset рівня, тож зміна порядку рівнів у GameConfig прогрес не ламає.
public static class Progress
{
    private const string StarsPrefix = "stars_";
    private const string UpgradePrefix = "upgrade_";
    private const string DailyPrefix = "daily_done_";
    private const string SpentKey = "stars_spent";
    private const string BonusKey = "stars_bonus";

    // ---------- Зірки рівнів ----------

    public static int GetStars(LevelData level) =>
        level != null ? PlayerPrefs.GetInt(StarsPrefix + level.name, 0) : 0;

    // Зберігає лише покращення результату. Повертає true, якщо це новий рекорд.
    public static bool SaveStars(LevelData level, int stars)
    {
        if (level == null || stars <= GetStars(level)) return false;
        PlayerPrefs.SetInt(StarsPrefix + level.name, stars);
        PlayerPrefs.Save();
        return true;
    }

    // Рівень відкритий, якщо це перший або попередній пройдено хоч на 1 зірку.
    public static bool IsUnlocked(int levelIndex)
    {
        var levels = GameConfig.Instance.levels;
        if (levelIndex <= 0) return true;
        if (levelIndex >= levels.Count) return false;
        return GetStars(levels[levelIndex - 1]) > 0;
    }

    // ---------- Зірки як валюта ----------

    public static int LevelStars
    {
        get
        {
            int total = 0;
            foreach (LevelData level in GameConfig.Instance.levels) total += GetStars(level);
            return total;
        }
    }

    public static int BonusStars => PlayerPrefs.GetInt(BonusKey, 0);
    public static int SpentStars => PlayerPrefs.GetInt(SpentKey, 0);
    public static int AvailableStars => LevelStars + BonusStars - SpentStars;

    // ---------- Покращення ----------

    public static int GetRank(MetaUpgrade upgrade) => PlayerPrefs.GetInt(UpgradePrefix + upgrade.id, 0);

    public static bool CanBuy(MetaUpgrade upgrade) =>
        GetRank(upgrade) < upgrade.maxRank && AvailableStars >= upgrade.costPerRank;

    public static bool TryBuy(MetaUpgrade upgrade)
    {
        if (!CanBuy(upgrade)) return false;
        PlayerPrefs.SetInt(UpgradePrefix + upgrade.id, GetRank(upgrade) + 1);
        PlayerPrefs.SetInt(SpentKey, SpentStars + upgrade.costPerRank);
        PlayerPrefs.Save();
        return true;
    }

    // ---------- Щоденне випробування ----------

    public static bool IsDailyDone(int day) => PlayerPrefs.GetInt(DailyPrefix + day, 0) == 1;

    // Нагорода дається лише за перше проходження за день. Повертає кількість отриманих зірок.
    public static int CompleteDaily(int day)
    {
        if (IsDailyDone(day)) return 0;
        int reward = GameConfig.Instance.dailyStarReward;
        PlayerPrefs.SetInt(DailyPrefix + day, 1);
        PlayerPrefs.SetInt(BonusKey, BonusStars + reward);
        PlayerPrefs.Save();
        return reward;
    }
}
