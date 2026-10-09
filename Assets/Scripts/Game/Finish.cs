using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class Finish : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text healthLabel;
    [SerializeField] private int currentHealth;
    private AudioSource audioSource;
    private CrazyAdsManager adsManager;

    void Start()
    {
        currentHealth = maxHealth;
        healthSlider.maxValue = maxHealth;
        UpdateHealthUI();
        audioSource = GetComponent<AudioSource>();
        adsManager = FindAnyObjectByType<CrazyAdsManager>();
        adsManager.BeforeLeaving();
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
        Time.timeScale = 0f;
        adsManager.OnLose();
    }

    public void Restart()
    {
        SceneManager.LoadScene("Level");
        adsManager.BeforeLeaving();
    }
}