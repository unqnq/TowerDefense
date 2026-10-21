using System;
using System.Collections.Generic;
using UnityEngine;

public enum UpgradeType
{
    StartMoney,   // +X монет на старті кожного рівня
    BaseHealth,   // +X% HP бази
    TowerDamage,  // +X% урону веж
    TowerRange,   // +X% дальності веж
    FireRate      // +X% швидкості стрільби
}

// Одна гілка дерева постійних покращень, які купуються за зірки.
[Serializable]
public class MetaUpgrade
{
    [Tooltip("Ключ збереження. Не змінюйте після релізу, інакше гравці втратять куплені рівні.")]
    public string id = "upgrade";
    public string displayName = "Upgrade";
    [Tooltip("Опис одного рівня, {0} підставляється значення. Наприклад: \"+{0} coins at start\".")]
    public string description = "+{0}";
    public UpgradeType type;
    [Tooltip("Ефект за кожен рівень: для відсотків 0.1 = +10%, для StartMoney — монети.")]
    public float valuePerRank = 0.1f;
    [Min(1)] public int maxRank = 3;
    [Tooltip("Скільки зірок коштує кожен рівень.")]
    [Min(1)] public int costPerRank = 2;
}

// Модифікатор щоденного випробування: що в ньому особливого.
[Serializable]
public class DailyModifier
{
    public string displayName = "Daily";
    public string description = "";
    [Min(0.1f)] public float enemyHealth = 1f;
    [Min(0.1f)] public float enemySpeed = 1f;
    [Min(0f)] public float startMoney = 1f;
    [Tooltip("Порожньо = усі вежі.")]
    public TowerData[] allowedTowers;
}

// Головні налаштування гри. Лежить у Resources, тому доступний з будь-якої сцени без посилань.
[CreateAssetMenu(menuName = "Tower Defense/Game Config", fileName = "GameConfig")]
public class GameConfig : ScriptableObject
{
    private static GameConfig instance;
    public static GameConfig Instance => instance != null ? instance : (instance = Resources.Load<GameConfig>("GameConfig"));

    [Header("Сцени")]
    public string menuScene = "MainMenu";

    [Header("Рівні (у порядку проходження)")]
    public List<LevelData> levels = new List<LevelData>();

    [Header("Бустер-карти")]
    public List<BoostCard> boostCards = new List<BoostCard>();
    [Tooltip("Скільки карт пропонувати на вибір.")]
    [Min(1)] public int boostChoices = 3;

    [Header("Дерево покращень")]
    public List<MetaUpgrade> upgrades = new List<MetaUpgrade>();

    [Header("Щоденне випробування")]
    public List<DailyModifier> dailyModifiers = new List<DailyModifier>();
    [Tooltip("Скільки бонусних зірок дає перше проходження випробування за день.")]
    [Min(0)] public int dailyStarReward = 2;

    [Header("Конфіденційність")]
    [Tooltip("Посилання на Privacy Policy. Потрібне для Google Play і вікна згоди. Порожньо — посилання у вікні не показується.")]
    public string privacyPolicyUrl = "";

    [Header("Реклама")]
    [Tooltip("Interstitial показується раз на стільки завершених рівнів (при виході з рівня).")]
    [Min(1)] public int interstitialEveryLevels = 3;
    [Tooltip("Яку частку HP бази відновлює \"друге життя\".")]
    [Range(0.1f, 1f)] public float secondLifeHealth = 0.5f;
}
