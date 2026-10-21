using TMPro;
using UnityEngine;

public class Bank : MonoBehaviour
{
    [Tooltip("Скільки грошей у гравця на початку рівня.")]
    [SerializeField] private int startMoney = 15;
    [SerializeField] private TMP_Text moneyText;

    private int money;
    public int Money => money;

    // Куди летять монети з убитих ворогів, якщо в сцені немає іконки MoneyImage.
    public RectTransform MoneyLabel => moneyText != null ? moneyText.rectTransform : null;

    void Awake()
    {
        // Стартові гроші з рівня + покращення з дерева; startMoney — запасне значення без GameConfig.
        money = LevelSession.StartMoney(startMoney);
        if (moneyText == null)
            Debug.LogError("[Bank] Не призначено Money Text в інспекторі.", this);
        UpdateUI();
    }

    // Банк сам слухає, коли вбивають ворога. Ворогу не потрібно знати про банк.
    void OnEnable() => GameEvents.EnemyKilled += AddMoney;
    void OnDisable() => GameEvents.EnemyKilled -= AddMoney;

    public void AddMoney(int amount)
    {
        money += amount;
        UpdateUI();
    }

    public bool CanAfford(int amount) => money >= amount;

    // Списує гроші, якщо їх вистачає. Повертає true, якщо покупка відбулася.
    public bool TrySpendMoney(int amount)
    {
        if (!CanAfford(amount)) return false;

        money -= amount;
        UpdateUI();
        return true;
    }

    void UpdateUI()
    {
        if (moneyText != null) moneyText.text = money.ToString();
    }
}
