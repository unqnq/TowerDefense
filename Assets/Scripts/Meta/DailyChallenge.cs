using System;

// Щоденне випробування: рівень + модифікатор, однакові для всіх гравців у той самий день.
// Номер дня рахується від дати UTC, а вибір робиться детермінованим генератором від цього номера,
// тому всі телефони без сервера отримують одне й те саме випробування.
// Обмеження: дата береться з годинника телефона, тож її можна підкрутити (див. пояснення в чаті).
public static class DailyChallenge
{
    private static readonly DateTime Epoch = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Номер сьогоднішнього дня (UTC). Новий день для всіх починається одночасно: о 00:00 UTC (03:00 за Києвом влітку).
    public static int Today => (int)(DateTime.UtcNow.Date - Epoch).TotalDays;

    // Скільки лишилось до наступного випробування.
    public static TimeSpan TimeUntilNext => DateTime.UtcNow.Date.AddDays(1) - DateTime.UtcNow;

    public static bool IsAvailable
    {
        get
        {
            GameConfig config = GameConfig.Instance;
            return config != null && config.levels.Count > 0 && config.dailyModifiers.Count > 0;
        }
    }

    public static int LevelIndex(int day) => Pick(day, 1, GameConfig.Instance.levels.Count);

    public static DailyModifier Modifier(int day)
    {
        var list = GameConfig.Instance.dailyModifiers;
        return list[Pick(day, 2, list.Count)];
    }

    // Власний простий генератор замість System.Random: його результат гарантовано однаковий
    // на всіх платформах і версіях Unity. salt розводить вибір рівня і модифікатора.
    private static int Pick(int day, int salt, int count)
    {
        if (count <= 0) return 0;
        uint x = (uint)(day * 2654435761u) ^ (uint)(salt * 40503u);
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        return (int)(x % (uint)count);
    }
}
