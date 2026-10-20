using UnityEngine;
using TMPro;
using UnityEngine.UI;

// База гравця. Отримує урон від ворогів і повідомляє, коли її знищено.
// Що робити після знищення (реклама, панель поразки), вирішує GameManager.
public class Finish : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text healthLabel;
    [SerializeField] private int currentHealth;
    private AudioSource audioSource;

    public bool IsDead => currentHealth <= 0;

    void Start()
    {
        currentHealth = maxHealth;
        healthSlider.maxValue = maxHealth;
        UpdateHealthUI();
        audioSource = GetComponent<AudioSource>();
    }

    public void TakeDamage(int damage)
    {
        if (IsDead) return;

        currentHealth -= damage;
        if (currentHealth < 0) currentHealth = 0;
        UpdateHealthUI();
        if (audioSource != null) audioSource.Play();

        if (IsDead) GameEvents.RaiseBaseDestroyed();
    }

    private void UpdateHealthUI()
    {
        healthSlider.value = currentHealth;
        healthLabel.text = currentHealth + " / " + maxHealth;
    }
}
