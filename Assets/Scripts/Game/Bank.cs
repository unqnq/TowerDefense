using TMPro;
using UnityEngine;

public class Bank : MonoBehaviour
{
    public static Bank instance;
    [SerializeField] private int money;
    [SerializeField] private TMP_Text moneyText;

    void Awake()
    {
        if(instance == null)
        {
            instance = this;
        }

        money = 15;
        moneyText = GameObject.Find("MoneyText").GetComponent<TMP_Text>();
        UpdateUI();
    }

    public void AddMoney(int amount)
    {
        money += amount;
        UpdateUI();
    }

    public bool TrySpendMoney(int amount)
    {
        if (money - amount >= 0) return true;
        return false;
    }

    public void SpendMoney(int amount)
    {
        money -= amount;
        UpdateUI();
    }

    void UpdateUI()
    {
        moneyText.text = money.ToString();
    }
}
