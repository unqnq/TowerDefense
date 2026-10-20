using System;

// Центральні ігрові події. Скрипти повідомляють про те, що сталося,
// і не знають, хто на це реагує. Так ворог не залежить від банку, а база від реклами.
// Підписуйтеся в OnEnable і відписуйтеся в OnDisable, щоб після перезапуску сцени не лишалось "мертвих" підписок.
public static class GameEvents
{
    // Ворога вбито. Параметр: нагорода.
    public static event Action<int> EnemyKilled;

    // Базу знищено (HP = 0).
    public static event Action BaseDestroyed;

    // Спавнер випустив останнього ворога останньої хвилі.
    public static event Action AllWavesSpawned;

    // Змінився стан гри (Playing / Paused / Won / Lost).
    public static event Action<GameState> StateChanged;

    public static void RaiseEnemyKilled(int reward) => EnemyKilled?.Invoke(reward);
    public static void RaiseBaseDestroyed() => BaseDestroyed?.Invoke();
    public static void RaiseAllWavesSpawned() => AllWavesSpawned?.Invoke();
    public static void RaiseStateChanged(GameState state) => StateChanged?.Invoke(state);
}
