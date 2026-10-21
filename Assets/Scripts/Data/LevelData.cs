using System;
using System.Collections.Generic;
using UnityEngine;

// Група однакових ворогів у хвилі: "5 крабів з інтервалом 1 с".
[Serializable]
public class SpawnGroup
{
    public EnemyData enemy;
    [Min(1)] public int count = 5;
    [Tooltip("Секунд між появою ворогів цієї групи.")]
    [Min(0f)] public float spawnInterval = 1f;
}

// Хвиля складається з кількох груп, які йдуть одна за одною. Так можна робити змішані хвилі.
[Serializable]
public class Wave
{
    public List<SpawnGroup> groups = new List<SpawnGroup>();
    [Tooltip("Пауза після цієї хвилі перед наступною. Для останньої хвилі ігнорується.")]
    [Min(0f)] public float delayAfter = 5f;
}

// Опис рівня: де він грається, з якими ресурсами, які хвилі і як рахуються зірки.
// Список і порядок рівнів задає GameConfig (Assets/Resources/GameConfig).
[CreateAssetMenu(menuName = "Tower Defense/Level", fileName = "Level")]
public class LevelData : ScriptableObject
{
    [Header("Опис")]
    [Tooltip("Назва на карті рівнів. Латиницею, поки шрифт без кирилиці.")]
    [SerializeField] private string displayName = "Level";
    [Tooltip("Сцена з картою цього рівня. Кілька рівнів можуть ділити одну карту з різними хвилями.")]
    [SerializeField] private string sceneName = "Level";

    [Header("Старт")]
    [SerializeField, Min(0)] private int startMoney = 15;
    [SerializeField, Min(1)] private int baseHealth = 100;
    [Tooltip("Необов'язково. Які вежі доступні на рівні. Порожньо = усі вежі з TurretSpawner.")]
    [SerializeField] private TowerData[] allowedTowers;

    [Header("Зірки (частка HP бази, що лишилась)")]
    [Tooltip("1 зірка — будь-яка перемога. 2 зірки — HP бази не менше цієї частки.")]
    [SerializeField, Range(0f, 1f)] private float twoStarsHealth = 0.5f;
    [Tooltip("3 зірки — HP бази не менше цієї частки.")]
    [SerializeField, Range(0f, 1f)] private float threeStarsHealth = 0.9f;

    [Header("Хвилі")]
    [SerializeField] private List<Wave> waves = new List<Wave>();

    public string DisplayName => displayName;
    public string SceneName => sceneName;
    public int StartMoney => startMoney;
    public int BaseHealth => baseHealth;
    public IReadOnlyList<TowerData> AllowedTowers => allowedTowers;
    public IReadOnlyList<Wave> Waves => waves;

    public int StarsFor(float baseHealthFraction)
    {
        if (baseHealthFraction >= threeStarsHealth) return 3;
        if (baseHealthFraction >= twoStarsHealth) return 2;
        return 1;
    }
}
