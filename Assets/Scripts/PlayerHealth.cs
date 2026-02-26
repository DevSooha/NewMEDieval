using System;
using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHP = 6;
    [SerializeField] private float hitInvulnerableDuration = 0.8f;
    [SerializeField] private float hitBlinkInterval = 0.16f;

    private int currentHP;
    private bool isInvulnerable;
    private Coroutine invulnerableRoutine;

    public static Action<int, int> OnHealthChanged;
    public static Action OnPlayerDeath;

    public int MaxHP => maxHP;
    public int CurrentHP => currentHP;
    public bool IsInvulnerable => isInvulnerable;

    private void Awake()
    {
        currentHP = maxHP;
    }

    private void Start()
    {
        NotifyHealthChanged();
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        if (isInvulnerable) return;

        currentHP -= amount;
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);

        NotifyHealthChanged();

        if (currentHP <= 0)
        {
            Die();
            return;
        }

        TriggerHitInvulnerability();
    }

    public void TriggerHitInvulnerability()
    {
        if (hitInvulnerableDuration <= 0f) return;
        SetInvulnerableForSeconds(hitInvulnerableDuration);
    }

    public void SetInvulnerableForSeconds(float duration)
    {
        if (duration <= 0f) return;

        if (invulnerableRoutine != null)
        {
            StopCoroutine(invulnerableRoutine);
            invulnerableRoutine = null;
        }

        invulnerableRoutine = StartCoroutine(InvulnerableRoutine(duration));
    }

    public void SetInvulnerable(bool value)
    {
        isInvulnerable = value;
    }

    public void Resurrect()
    {
        currentHP = maxHP;
        SetInvulnerable(false);

        if (invulnerableRoutine != null)
        {
            StopCoroutine(invulnerableRoutine);
            invulnerableRoutine = null;
        }

        NotifyHealthChanged();
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;

        currentHP += amount;
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);

        NotifyHealthChanged();
    }

    private IEnumerator InvulnerableRoutine(float duration)
    {
        isInvulnerable = true;

        Player player = GetComponent<Player>();
        if (player != null)
        {
            player.StartBlink(duration, hitBlinkInterval);
        }

        yield return new WaitForSeconds(duration);

        isInvulnerable = false;
        invulnerableRoutine = null;
    }

    private void NotifyHealthChanged()
    {
        OnHealthChanged?.Invoke(currentHP, maxHP);
    }

    private void Die()
    {
        OnPlayerDeath?.Invoke();
        gameObject.SetActive(false);
    }
}

