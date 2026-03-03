using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStatusController : MonoBehaviour
{
    private struct TimedInput
    {
        public float time;
        public Vector2 value;
    }

    private Player player;
    private PlayerHealth playerHealth;
    private SpriteRenderer spriteRenderer;

    private readonly Dictionary<StatusEffectType, Coroutine> running = new();
    private readonly Queue<TimedInput> delayedInputs = new();

    private bool inputReversed;
    private float inputDelaySeconds;
    private float speedMultiplier = 1f;
    private bool knockbackImmune;
    private bool stunned;
    [Header("Blind Overlay")]
    [SerializeField, Range(0f, 1f)] private float blindBlackOverlayAlpha = 0.65f;
    [SerializeField, Range(0f, 1f)] private float blindWhiteOverlayAlpha = 0.55f;
    private bool blindOverlayCaptured;
    private Color blindOriginalFadeColor = new Color(0f, 0f, 0f, 0f);

    public bool IsInputReversed => inputReversed;
    public float InputDelaySeconds => inputDelaySeconds;
    public float SpeedMultiplier => speedMultiplier;
    public bool IsKnockbackImmune => knockbackImmune;
    public bool IsStunned => stunned;

    private void Awake()
    {
        player = GetComponent<Player>();
        playerHealth = GetComponent<PlayerHealth>();
        spriteRenderer = GetComponent<SpriteRenderer>();
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

        inputReversed = false;
        inputDelaySeconds = 0f;
        speedMultiplier = 1f;
        knockbackImmune = false;
        stunned = false;

        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = 1f;
            spriteRenderer.color = c;
        }

        RestoreBlindOverlay();
    }

    public void ApplyEffect(StatusEffectSpec effect)
    {
        if (effect == null) return;

        switch (effect.effectType)
        {
            case StatusEffectType.StealthOnly:
                StartOrRefresh(effect.effectType, StealthRoutine(effect.duration, effect.magnitude <= 0f ? 0.3f : effect.magnitude, false));
                break;
            case StatusEffectType.StealthInvulnerable:
                StartOrRefresh(effect.effectType, StealthRoutine(effect.duration, effect.magnitude <= 0f ? 0.3f : effect.magnitude, true));
                break;
            case StatusEffectType.PlayerMoveSpeedMultiplier:
                StartOrRefresh(effect.effectType, SpeedRoutine(effect.duration, effect.magnitude <= 0f ? 2f : effect.magnitude));
                break;
            case StatusEffectType.PlayerInputReverse:
                StartOrRefresh(effect.effectType, InputReverseRoutine(effect.duration));
                break;
            case StatusEffectType.PlayerInputDelay:
                StartOrRefresh(effect.effectType, InputDelayRoutine(effect.duration, effect.magnitude <= 0f ? 2f : effect.magnitude));
                break;
            case StatusEffectType.BlindBlack:
            {
                float alpha = effect.magnitude > 0f
                    ? Mathf.Clamp01(effect.magnitude * blindBlackOverlayAlpha)
                    : blindBlackOverlayAlpha;
                StartOrRefreshBlind(effect.effectType, effect.duration, Color.black, alpha);
                break;
            }
            case StatusEffectType.BlindWhite:
            {
                float alpha = effect.magnitude > 0f
                    ? Mathf.Clamp01(effect.magnitude * blindWhiteOverlayAlpha)
                    : blindWhiteOverlayAlpha;
                StartOrRefreshBlind(effect.effectType, effect.duration, Color.white, alpha);
                break;
            }
            case StatusEffectType.PlayerRedStateContactBurn:
                StartOrRefresh(effect.effectType, RedStateRoutine(effect.duration, effect.magnitude <= 0f ? 50f : effect.magnitude, effect.interval <= 0f ? 0.5f : effect.interval));
                break;
            case StatusEffectType.PlayerKnockbackImmune:
                StartOrRefresh(effect.effectType, KnockbackImmuneRoutine(effect.duration));
                break;
            case StatusEffectType.PlayerStun:
                StartOrRefresh(effect.effectType, StunRoutine(effect.duration));
                break;
        }
    }

    public Vector2 ProcessMovementInput(Vector2 rawInput)
    {
        Vector2 input = rawInput;

        if (inputReversed)
        {
            input = -input;
        }

        if (inputDelaySeconds <= 0f)
        {
            delayedInputs.Clear();
            return input;
        }

        float now = Time.time;
        delayedInputs.Enqueue(new TimedInput { time = now, value = input });

        while (delayedInputs.Count > 0)
        {
            TimedInput first = delayedInputs.Peek();
            if (now - first.time < inputDelaySeconds)
            {
                return Vector2.zero;
            }

            if (delayedInputs.Count == 1)
            {
                return first.value;
            }

            delayedInputs.Dequeue();
            TimedInput second = delayedInputs.Peek();
            if (now - second.time < inputDelaySeconds)
            {
                return first.value;
            }
        }

        return Vector2.zero;
    }

    private void StartOrRefresh(StatusEffectType type, IEnumerator routine)
    {
        if (running.TryGetValue(type, out Coroutine existing) && existing != null)
        {
            StopCoroutine(existing);
        }

        running[type] = StartCoroutine(routine);
    }

    private void StartOrRefreshBlind(StatusEffectType type, float duration, Color color, float overlayAlpha)
    {
        StopEffect(StatusEffectType.BlindBlack);
        StopEffect(StatusEffectType.BlindWhite);
        RestoreBlindOverlay();
        running[type] = StartCoroutine(BlindRoutine(duration, color, type, overlayAlpha));
    }

    private void StopEffect(StatusEffectType type)
    {
        if (!running.TryGetValue(type, out Coroutine existing) || existing == null)
        {
            running.Remove(type);
            return;
        }

        StopCoroutine(existing);
        running.Remove(type);
    }

    private IEnumerator StealthRoutine(float duration, float alpha, bool invulnerable)
    {
        float safe = Mathf.Max(0.05f, duration);

        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = Mathf.Clamp(alpha, 0.05f, 1f);
            spriteRenderer.color = c;
        }

        if (invulnerable && playerHealth != null)
        {
            playerHealth.SetInvulnerable(true);
        }

        yield return new WaitForSeconds(safe);

        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = 1f;
            spriteRenderer.color = c;
        }

        if (playerHealth != null)
        {
            playerHealth.SetInvulnerable(false);
        }

        running.Remove(invulnerable ? StatusEffectType.StealthInvulnerable : StatusEffectType.StealthOnly);
    }

    private IEnumerator SpeedRoutine(float duration, float multiplier)
    {
        float safe = Mathf.Max(0.05f, duration);
        speedMultiplier = Mathf.Max(0.1f, multiplier);

        if (player != null)
        {
            player.ApplySpeedBuff(safe);
        }

        yield return new WaitForSeconds(safe);

        speedMultiplier = 1f;
        running.Remove(StatusEffectType.PlayerMoveSpeedMultiplier);
    }

    private IEnumerator InputReverseRoutine(float duration)
    {
        float safe = Mathf.Max(0.05f, duration);
        inputReversed = true;

        yield return new WaitForSeconds(safe);

        inputReversed = false;
        running.Remove(StatusEffectType.PlayerInputReverse);
    }

    private IEnumerator InputDelayRoutine(float duration, float delaySeconds)
    {
        float safe = Mathf.Max(0.05f, duration);
        inputDelaySeconds = Mathf.Clamp(delaySeconds, 0.05f, 5f);
        delayedInputs.Clear();

        yield return new WaitForSeconds(safe);

        inputDelaySeconds = 0f;
        delayedInputs.Clear();
        running.Remove(StatusEffectType.PlayerInputDelay);
    }

    private IEnumerator BlindRoutine(float duration, Color color, StatusEffectType effectType, float overlayAlpha)
    {
        float safe = Mathf.Max(0.05f, duration);

        if (UIManager.Instance == null || UIManager.Instance.fadeImage == null)
        {
            yield return new WaitForSeconds(safe);
            running.Remove(effectType);
            yield break;
        }

        CaptureBlindOverlay();
        Color target = color;
        target.a = Mathf.Clamp01(overlayAlpha);

        UIManager.Instance.fadeImage.color = target;
        UIManager.Instance.fadeImage.gameObject.SetActive(true);
        UIManager.Instance.fadeImage.transform.SetAsLastSibling();

        yield return new WaitForSeconds(safe);

        RestoreBlindOverlay();
        running.Remove(effectType);
    }

    private void CaptureBlindOverlay()
    {
        if (blindOverlayCaptured) return;
        if (UIManager.Instance == null || UIManager.Instance.fadeImage == null) return;

        blindOriginalFadeColor = UIManager.Instance.fadeImage.color;
        blindOverlayCaptured = true;
    }

    private void RestoreBlindOverlay()
    {
        if (!blindOverlayCaptured) return;

        if (UIManager.Instance != null && UIManager.Instance.fadeImage != null)
        {
            UIManager.Instance.fadeImage.color = blindOriginalFadeColor;
        }

        blindOverlayCaptured = false;
    }

    private IEnumerator RedStateRoutine(float duration, float burnDamage, float tickInterval)
    {
        float safe = Mathf.Max(0.05f, duration);
        float interval = Mathf.Max(0.05f, tickInterval);
        float remaining = safe;
        Color original = spriteRenderer != null ? spriteRenderer.color : Color.white;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(1f, 0.7f, 0.7f, original.a);
        }

        while (remaining > 0f)
        {
            ApplyContactBurn(Mathf.RoundToInt(burnDamage));
            yield return new WaitForSeconds(interval);
            remaining -= interval;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = original;
        }

        running.Remove(StatusEffectType.PlayerRedStateContactBurn);
    }

    private void ApplyContactBurn(int damage)
    {
        if (damage <= 0) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 0.6f);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null) continue;

            EnemyCombat enemy = hit.GetComponent<EnemyCombat>();
            if (enemy == null) enemy = hit.GetComponentInParent<EnemyCombat>();
            if (enemy != null)
            {
                enemy.EnemyTakeDamage(damage);
                continue;
            }

            BossHealth boss = hit.GetComponent<BossHealth>();
            if (boss == null) boss = hit.GetComponentInParent<BossHealth>();
            if (boss != null)
            {
                boss.TakeDamage(damage, ElementType.Fire);
            }
        }
    }

    private IEnumerator KnockbackImmuneRoutine(float duration)
    {
        float safe = Mathf.Max(0.05f, duration);
        knockbackImmune = true;

        yield return new WaitForSeconds(safe);

        knockbackImmune = false;
        running.Remove(StatusEffectType.PlayerKnockbackImmune);
    }

    private IEnumerator StunRoutine(float duration)
    {
        float safe = Mathf.Max(0.05f, duration);
        stunned = true;

        if (player != null)
        {
            player.SetCanMove(false);
        }

        yield return new WaitForSeconds(safe);

        stunned = false;
        if (player != null)
        {
            player.SetCanMove(true);
        }

        running.Remove(StatusEffectType.PlayerStun);
    }
}
