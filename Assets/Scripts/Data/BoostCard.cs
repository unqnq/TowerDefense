using UnityEngine;

public enum BoostEffect
{
    TowerDamage,    // +X% урону всім вежам
    FireRate,       // +X% швидкості стрільби
    TowerRange,     // +X% дальності
    EnemySlow,      // вороги повільніші на X%
    CoinReward,     // +X% монет за вбивство
    InstantCoins    // одразу X монет (тривалість не потрібна)
}

// Бустер-карта, яку гравець обирає на початку рівня (1 з 3).
// Тимчасова: діє Duration секунд ігрового часу від моменту вибору (пауза час не з'їдає).
[CreateAssetMenu(menuName = "Tower Defense/Boost Card", fileName = "Boost")]
public class BoostCard : ScriptableObject
{
    [SerializeField] private string displayName = "Boost";
    [Tooltip("Короткий опис на карті. Латиницею, поки шрифт без кирилиці.")]
    [SerializeField] private string description = "";
    [SerializeField] private BoostEffect effect = BoostEffect.TowerDamage;
    [Tooltip("Для відсотків: 0.5 = +50%. Для InstantCoins: кількість монет.")]
    [SerializeField] private float value = 0.5f;
    [Tooltip("Скільки секунд діє. Для InstantCoins не використовується.")]
    [SerializeField, Min(0f)] private float duration = 45f;
    [SerializeField] private Color color = new Color(0.25f, 0.35f, 0.7f, 1f);

    public string DisplayName => displayName;
    public string Description => description;
    public BoostEffect Effect => effect;
    public float Value => value;
    public float Duration => duration;
    public Color Color => color;
    public bool IsInstant => effect == BoostEffect.InstantCoins;
}
