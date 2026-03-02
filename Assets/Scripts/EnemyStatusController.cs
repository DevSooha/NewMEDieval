using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyStatusController : MonoBehaviour
{
    private EnemyCombat enemyCombat;
    private EnemyMovement enemyMovement;
    private Rigidbody2D rb;

    private readonly Dictionary<StatusEffectType, Coroutine> running = new();

    private bool stunned;
    private float speedMultiplier = 1f;

    public bool IsStunned => stunned;
    public float SpeedMultiplier => speedMultiplier;

    private void Awake()
    {
        enemyCombat = GetComponent<EnemyCombat>();
        if (enemyCombat == null) enemyCombat = GetComponentInParent<EnemyCombat>();

        enemyMovement = GetComponent<EnemyMovement>();
        if (enemyMovement == null) enemyMovement = GetComponentInParent<EnemyMovement>();

        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = GetComponentInParent<Rigidbody2D>();
    }

    private void OnDisable()
    {
        foreach (KeyValuePair<StatusEffectType, Coroutine> pair in running)
        {
            if (pair.Value != null)
            {
                StopCoroutine(pair.Value);
            }
        }

        running.Clear();
        stunned = false;
        speedMultiplier = 1f;
    }

    public void ApplyEffect(StatusEffectSpec effect)
    {
        if (effect == null) return;

        switch (effect.effectType)
        {
            case StatusEffectType.EnemyStun:
                StartOrRefresh(effect.effectType, StunRoutine(effect.duration));
                break;
            case StatusEffectType.EnemyMoveSpeedMultiplier:
                StartOrRefresh(effect.effectType, SpeedRoutine(effect.duration, effect.magnitude <= 0f ? 1f : effect.magnitude));
                break;
            case StatusEffectType.EnemyKnockback:
                ApplyKnockback(effect.magnitude);
                break;
            case StatusEffectType.PoisonDot:
                StartOrRefresh(effect.effectType, PoisonRoutine(effect.duration, effect.magnitude, effect.interval));
                break;
        }
    }

    private void StartOrRefresh(StatusEffectType type, IEnumerator routine)
    {
        if (running.TryGetValue(type, out Coroutine existing) && existing != null)
        {
            StopCoroutine(existing);
        }

        running[type] = StartCoroutine(routine);
    }

    private IEnumerator StunRoutine(float duration)
    {
        float safe = Mathf.Max(0.05f, duration);
        stunned = true;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        yield return new WaitForSeconds(safe);
        stunned = false;
        running.Remove(StatusEffectType.EnemyStun);
    }

    private IEnumerator SpeedRoutine(float duration, float multiplier)
    {
        float safe = Mathf.Max(0.05f, duration);
        speedMultiplier = Mathf.Max(0.1f, multiplier);

        yield return new WaitForSeconds(safe);
        speedMultiplier = 1f;
        running.Remove(StatusEffectType.EnemyMoveSpeedMultiplier);
    }

    private IEnumerator PoisonRoutine(float duration, float percentPerTick, float interval)
    {
        if (enemyCombat == null)
        {
            running.Remove(StatusEffectType.PoisonDot);
            yield break;
        }

        float remaining = Mathf.Max(0.05f, duration);
        float tick = Mathf.Max(0.1f, interval <= 0f ? 2f : interval);
        float percent = Mathf.Max(0f, percentPerTick);

        while (remaining > 0f && !enemyCombat.IsDead)
        {
            int damage = Mathf.RoundToInt(enemyCombat.CurrentHealth * (percent / 100f));
            if (damage > 0)
            {
                enemyCombat.EnemyTakeDamage(damage);
            }

            yield return new WaitForSeconds(tick);
            remaining -= tick;
        }

        running.Remove(StatusEffectType.PoisonDot);
    }

    private void ApplyKnockback(float distance)
    {
        if (rb == null || distance <= 0f) return;

        Vector2 dir = Vector2.right;
        if (Player.Instance != null)
        {
            dir = ((Vector2)transform.position - (Vector2)Player.Instance.transform.position).normalized;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        }

        rb.MovePosition(rb.position + dir * (distance / 64f));
    }
}
