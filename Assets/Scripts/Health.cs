using UnityEngine;
using System;

public class Health : MonoBehaviour
{
    public float maxHealth = 100f;
    public bool destroyOnDeath = true;

    public event Action<Health, float> OnDamaged;
    public event Action<Health> OnDied;
    public static event Action<Health> AnyEntityDied;

    [SerializeField] private float currentHealth;

    public float CurrentHealth => currentHealth;
    public bool IsDead => currentHealth <= 0f;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        OnDamaged?.Invoke(this, amount);

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (IsDead)
        {
            return;
        }

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
    }

    public void RestoreForSave(float savedHealth)
    {
        bool wasAlive = !IsDead;
        currentHealth = Mathf.Clamp(savedHealth, 0f, maxHealth);
        if (wasAlive && IsDead)
        {
            Die();
        }
    }

    private void Die()
    {
        OnDied?.Invoke(this);
        AnyEntityDied?.Invoke(this);

        if (destroyOnDeath)
        {
            Destroy(gameObject);
        }
    }
}
