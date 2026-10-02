using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class Finish : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text healthLabel;
    [SerializeField] private GameObject losePanel;
    [SerializeField] private int currentHealth;
    private AudioSource audioSource;

    void Start()
    {
        currentHealth = maxHealth;
        healthSlider.maxValue = maxHealth;
        losePanel.SetActive(false);
        UpdateHealthUI();
        audioSource = GetComponent<AudioSource>();
    }
    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth < 0) currentHealth = 0;
        UpdateHealthUI();
        audioSource.Play();
        if (currentHealth == 0) Lose();
    }
    private void UpdateHealthUI()
    {
        healthSlider.value = currentHealth;
        healthLabel.text = currentHealth + " / " + maxHealth;
    }
    private void Lose()
    {
        losePanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Restart()
    {
        SceneManager.LoadScene(0);
    }
}