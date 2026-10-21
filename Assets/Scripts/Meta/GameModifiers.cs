using System.Collections.Generic;
using UnityEngine;

// Усі множники, що впливають на бій, в одному місці:
//  - постійні покращення (дерево за зірки),
//  - модифікатор щоденного випробування,
//  - активні бустер-карти (тимчасові).
// Вежі й вороги питають множник у момент використання, тож закінчення бустера діє одразу.
public static class GameModifiers
{
    private class ActiveBoost
    {
        public BoostCard card;
        public float timeLeft;
    }

    private static readonly List<ActiveBoost> boosts = new List<ActiveBoost>();

    // Скидається на початку кожного рівня (GameManager.Awake).
    public static void ResetForLevel() => boosts.Clear();

    // Ігровий час: пауза і x2 впливають на тривалість так само, як на все інше.
    public static void Tick(float deltaTime)
    {
        for (int i = boosts.Count - 1; i >= 0; i--)
        {
            boosts[i].timeLeft -= deltaTime;
            if (boosts[i].timeLeft <= 0f) boosts.RemoveAt(i);
        }
    }

    public static void Activate(BoostCard card)
    {
        if (card == null || card.IsInstant) return; // миттєві карти обробляє той, хто їх застосовує
        boosts.Add(new ActiveBoost { card = card, timeLeft = card.Duration });
    }

    // Для HUD: який бустер діє і скільки йому лишилось (перший з активних).
    public static bool TryGetActiveBoost(out BoostCard card, out float timeLeft)
    {
        if (boosts.Count == 0)
        {
            card = null;
            timeLeft = 0f;
            return false;
        }
        card = boosts[0].card;
        timeLeft = boosts[0].timeLeft;
        return true;
    }

    // ---------- Множники ----------

    public static float TowerDamage => 1f + UpgradeValue(UpgradeType.TowerDamage) + BoostValue(BoostEffect.TowerDamage);
    public static float FireRate => 1f + UpgradeValue(UpgradeType.FireRate) + BoostValue(BoostEffect.FireRate);
    public static float TowerRange => 1f + UpgradeValue(UpgradeType.TowerRange) + BoostValue(BoostEffect.TowerRange);
    public static float CoinReward => 1f + BoostValue(BoostEffect.CoinReward);

    public static float EnemySpeed
    {
        get
        {
            float daily = LevelSession.IsDaily ? LevelSession.Daily.enemySpeed : 1f;
            return daily * Mathf.Max(0.1f, 1f - BoostValue(BoostEffect.EnemySlow));
        }
    }

    public static float EnemyHealth => LevelSession.IsDaily ? LevelSession.Daily.enemyHealth : 1f;

    // Сумарний ефект покращення з дерева: рівень × значення за рівень.
    public static float UpgradeValue(UpgradeType type)
    {
        GameConfig config = GameConfig.Instance;
        if (config == null) return 0f;
        float total = 0f;
        foreach (MetaUpgrade upgrade in config.upgrades)
            if (upgrade.type == type) total += Progress.GetRank(upgrade) * upgrade.valuePerRank;
        return total;
    }

    private static float BoostValue(BoostEffect effect)
    {
        float total = 0f;
        foreach (ActiveBoost boost in boosts)
            if (boost.card.Effect == effect) total += boost.card.Value;
        return total;
    }
}
