using System;
using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHP = 6;
    [SerializeField] private float defaultHitInvulnerableDuration = 0.8f;
    [SerializeField] private float normalMonsterHitInvulnerableDuration = 0.4f;
    [SerializeField] private float bossHitInvulnerableDuration = 0.8f;
    [SerializeField] private float hitBlinkInterval = 0.16f;

    private int currentHP;
    private int temporaryMaxHpBonus;
    private bool isInvulnerable;
    private Coroutine invulnerableRoutine;

    public static Action<int, int> OnHealthChanged;
    public static Action OnPlayerDeath;

    public int MaxHP => maxHP;
    public int CurrentHP => currentHP;
    public int EffectiveMaxHp => maxHP + temporaryMaxHpBonus;
    public bool IsInvulnerable => isInvulnerable;
    public float NormalMonsterHitInvulnerableDuration => normalMonsterHitInvulnerableDuration;
    public float BossHitInvulnerableDuration => bossHitInvulnerableDuration;

    private void Awake()
    {
        currentHP = maxHP;
        CombatTargetHitbox.EnsureForPlayer(this);
    }

    private void Start()
    {
        NotifyHealthChanged();
    }

    public void TakeDamage(int amount)
    {
        TryTakeDamage(amount, defaultHitInvulnerableDuration);
    }

    public bool TryTakeDamage(int amount, float invulnerableDuration)
    {
        if (amount <= 0) return false;
        if (isInvulnerable) return false;

        currentHP -= amount;
        currentHP = Mathf.Clamp(currentHP, 0, EffectiveMaxHp);

        NotifyHealthChanged();

        if (currentHP <= 0)
        {
            Die();
            return true;
        }

        TriggerHitInvulnerability(invulnerableDuration);
        return true;
    }

    public void TriggerHitInvulnerability()
    {
        TriggerHitInvulnerability(defaultHitInvulnerableDuration);
    }

    public void TriggerHitInvulnerability(float duration)
    {
        if (duration <= 0f) return;
        SetInvulnerableForSeconds(duration);
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
        temporaryMaxHpBonus = 0;
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
        currentHP = Mathf.Clamp(currentHP, 0, EffectiveMaxHp);

        NotifyHealthChanged();
    }

    public void HealWithOvercap(int amount, int bonusMaxHp)
    {
        if (amount <= 0) return;

        temporaryMaxHpBonus = Mathf.Max(temporaryMaxHpBonus, Mathf.Max(0, bonusMaxHp));
        currentHP += amount;
        currentHP = Mathf.Clamp(currentHP, 0, EffectiveMaxHp);
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
        OnHealthChanged?.Invoke(currentHP, EffectiveMaxHp);
    }

    private void Die()
    {
        PlayerDeathCleanup.StopAllActivePlayback();
        OnPlayerDeath?.Invoke();
        gameObject.SetActive(false);
    }
}

