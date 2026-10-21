using UnityEngine;
using TMPro;
using UnityEngine.UI;

// База гравця. Отримує урон від ворогів і повідомляє, коли її знищено.
// Що робити після знищення (друге життя, панель поразки), вирішує GameManager.
public class Finish : MonoBehaviour, IDamageable
{
    [Tooltip("Запасне HP, якщо GameConfig порожній. Зазвичай береться з рівня + покращення.")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text healthLabel;
    private float currentHealth;
    private AudioSource audioSource;

    public bool IsDead => currentHealth <= 0f;
    public int MaxHealth => maxHealth;

    // Частка HP, що лишилась: від неї рахуються зірки.
    public float HealthFraction => maxHealth > 0 ? currentHealth / maxHealth : 0f;

    void Awake()
    {
        maxHealth = LevelSession.BaseHealth(maxHealth);
        currentHealth = maxHealth;
        audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        if (healthSlider != null) healthSlider.maxValue = maxHealth;
        UpdateHealthUI();
    }

    public void TakeDamage(float damage)
    {
        if (IsDead) return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        UpdateHealthUI();
        if (audioSource != null) audioSource.Play();
        Haptics.Heavy();

        if (IsDead) GameEvents.RaiseBaseDestroyed();
    }

    // Друге життя: відновлює частку максимального HP.
    public void Heal(float fraction)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + maxHealth * fraction);
        UpdateHealthUI();
    }

    private void UpdateHealthUI()
    {
        int shown = Mathf.CeilToInt(currentHealth);
        if (healthSlider != null) healthSlider.value = shown;
        if (healthLabel != null) healthLabel.text = shown + " / " + maxHealth;
    }
}
