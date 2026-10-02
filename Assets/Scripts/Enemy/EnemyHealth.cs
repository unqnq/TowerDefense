using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [SerializeField] private int health = 3;
    [SerializeField] private int reward = 3;
    [SerializeField] private GameObject effect;
    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void TakeDamage(int damage)
    {
        health -= damage;
        if (health <= 0)
        {
            audioSource.Play();
            Destroy(Instantiate(effect, transform.position, Quaternion.identity), 1f);
            Destroy(gameObject, 0.6f);
            Bank.instance.AddMoney(reward);
        }
    }
}
