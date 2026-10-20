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

// Опис рівня: які хвилі і в якому порядку. Пізніше сюди ж ляжуть стартові гроші, HP бази, пороги зірок.
[CreateAssetMenu(menuName = "Tower Defense/Level", fileName = "Level")]
public class LevelData : ScriptableObject
{
    [SerializeField] private List<Wave> waves = new List<Wave>();

    public IReadOnlyList<Wave> Waves => waves;
}
