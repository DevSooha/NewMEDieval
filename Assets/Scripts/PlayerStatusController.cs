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
    private readonly Dictionary<StatusEffectType, float> effectEndTimes = new();
    private readonly Dictionary<StatusEffectType, float> effectMagnitudes = new();
    private readonly Dictionary<StatusEffectType, float> effectIntervals = new();
    private readonly Queue<TimedInput> delayedInputs = new();
    private readonly Dictionary<string, Queue<float>> delayedButtonDowns = new();
    private readonly Dictionary<string, Queue<float>> delayedButtonUps = new();

    private bool inputReversed;
    private float inputDelaySeconds;
    private float speedMultiplier = 1f;
    private bool knockbackImmune;
    private bool stunned;
    [Header("Blind Overlay")]
    [SerializeField, Range(0f, 1f)] private float blindBlackOverlayAlpha = 0.65f;
    [SerializeField, Range(0f, 1f)] private float blindWhiteOverlayAlpha = 0.55f;
    [SerializeField, Min(0.05f)] private float blindTransitionDuration = 0.5f;
    private bool blindOverlayCaptured;
    private Color blindOriginalFadeColor = new Color(0f, 0f, 0f, 0f);

    public bool IsInputReversed => inputReversed;
    public float InputDelaySeconds => inputDelaySeconds;
    public float SpeedMultiplier => speedMultiplier;
    public bool IsKnockbackImmune => knockbackImmune;
    public bool IsStunned => stunned;
    public bool IsStealthActive =>
        IsEffectRunning(StatusEffectType.StealthOnly) ||
        IsEffectRunning(StatusEffectType.StealthInvulnerable);

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
        effectEndTimes.Clear();
        effectMagnitudes.Clear();
        effectIntervals.Clear();

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
        delayedButtonDowns.Clear();
        delayedButtonUps.Clear();
    }

    public void ApplyEffect(StatusEffectSpec effect)
    {
        if (effect == null) return;

        switch (effect.effectType)
        {
            case StatusEffectType.StealthOnly:
                RefreshEffect(effect.effectType, effect.duration, effect.magnitude <= 0f ? 0.3f : effect.magnitude);
                StartIfNeeded(effect.effectType, StealthRoutine(effect.effectType, false));
                break;
            case StatusEffectType.StealthInvulnerable:
                RefreshEffect(effect.effectType, effect.duration, effect.magnitude <= 0f ? 0.3f : effect.magnitude);
                StartIfNeeded(effect.effectType, StealthRoutine(effect.effectType, true));
                break;
            case StatusEffectType.PlayerMoveSpeedMultiplier:
                RefreshEffect(effect.effectType, effect.duration, effect.magnitude <= 0f ? 2f : effect.magnitude);
                StartIfNeeded(effect.effectType, SpeedRoutine());
                break;
            case StatusEffectType.PlayerInputReverse:
                RefreshEffect(effect.effectType, effect.duration);
                StartIfNeeded(effect.effectType, InputReverseRoutine());
                break;
            case StatusEffectType.PlayerInputDelay:
                RefreshEffect(effect.effectType, effect.duration, effect.magnitude <= 0f ? 2f : effect.magnitude);
                StartIfNeeded(effect.effectType, InputDelayRoutine());
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
                RefreshEffect(effect.effectType, effect.duration, effect.magnitude <= 0f ? 50f : effect.magnitude, effect.interval <= 0f ? 0.5f : effect.interval);
                StartIfNeeded(effect.effectType, RedStateRoutine());
                break;
            case StatusEffectType.PlayerKnockbackImmune:
                RefreshEffect(effect.effectType, effect.duration);
                StartIfNeeded(effect.effectType, KnockbackImmuneRoutine());
                break;
            case StatusEffectType.PlayerStun:
                RefreshEffect(effect.effectType, effect.duration);
                StartIfNeeded(effect.effectType, StunRoutine());
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

    public bool ProcessActionButtonDown(string actionId, bool rawPressed)
    {
        return ProcessActionButton(actionId, rawPressed, delayedButtonDowns);
    }

    public bool ProcessActionButtonUp(string actionId, bool rawReleased)
    {
        return ProcessActionButton(actionId, rawReleased, delayedButtonUps);
    }

    private bool ProcessActionButton(string actionId, bool rawTriggered, Dictionary<string, Queue<float>> buffer)
    {
        if (string.IsNullOrWhiteSpace(actionId))
        {
            return rawTriggered;
        }

        if (inputDelaySeconds <= 0f)
        {
            if (buffer != null)
            {
                buffer.Clear();
            }
            return rawTriggered;
        }

        if (buffer == null)
        {
            return false;
        }

        if (!buffer.TryGetValue(actionId, out Queue<float> queuedTimes))
        {
            queuedTimes = new Queue<float>();
            buffer[actionId] = queuedTimes;
        }

        if (rawTriggered)
        {
            queuedTimes.Enqueue(Time.time);
        }

        if (queuedTimes.Count <= 0)
        {
            return false;
        }

        if (Time.time - queuedTimes.Peek() < inputDelaySeconds)
        {
            return false;
        }

        queuedTimes.Dequeue();
        return true;
    }

    private void RefreshEffect(StatusEffectType type, float duration, float magnitude = 0f, float interval = 0f)
    {
        effectEndTimes[type] = Time.time + Mathf.Max(0.05f, duration);
        effectMagnitudes[type] = magnitude;
        effectIntervals[type] = interval;
    }

    private void StartIfNeeded(StatusEffectType type, IEnumerator routine)
    {
        if (running.TryGetValue(type, out Coroutine existing) && existing != null)
        {
            return;
        }

        running[type] = StartCoroutine(routine);
    }

    private bool IsEffectRunning(StatusEffectType type)
    {
        return running.TryGetValue(type, out Coroutine routine) && routine != null;
    }

    private bool IsEffectActive(StatusEffectType type)
    {
        return effectEndTimes.TryGetValue(type, out float endTime) && Time.time < endTime;
    }

    private float GetEffectMagnitude(StatusEffectType type, float fallback = 0f)
    {
        return effectMagnitudes.TryGetValue(type, out float magnitude) ? magnitude : fallback;
    }

    private float GetEffectInterval(StatusEffectType type, float fallback = 0f)
    {
        return effectIntervals.TryGetValue(type, out float interval) ? interval : fallback;
    }

    private float GetRemainingEffectDuration(StatusEffectType type)
    {
        if (!effectEndTimes.TryGetValue(type, out float endTime))
        {
            return 0f;
        }

        return Mathf.Max(0f, endTime - Time.time);
    }

    private void ClearEffect(StatusEffectType type)
    {
        running.Remove(type);
        effectEndTimes.Remove(type);
        effectMagnitudes.Remove(type);
        effectIntervals.Remove(type);
    }

    private void StartOrRefreshBlind(StatusEffectType type, float duration, Color color, float overlayAlpha)
    {
        StatusEffectType otherType = type == StatusEffectType.BlindBlack
            ? StatusEffectType.BlindWhite
            : StatusEffectType.BlindBlack;

        StopEffect(otherType);
        RefreshEffect(type, duration, overlayAlpha);
        StartIfNeeded(type, BlindRoutine(type, color));
    }

    private void StopEffect(StatusEffectType type)
    {
        if (!running.TryGetValue(type, out Coroutine existing) || existing == null)
        {
            ClearEffect(type);
            return;
        }

        StopCoroutine(existing);
        if (type == StatusEffectType.BlindBlack || type == StatusEffectType.BlindWhite)
        {
            RestoreBlindOverlay();
        }
        ClearEffect(type);
    }

    private IEnumerator StealthRoutine(StatusEffectType type, bool invulnerable)
    {
        if (invulnerable && playerHealth != null)
        {
            playerHealth.SetInvulnerable(true);
        }

        while (IsEffectActive(type))
        {
            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = Mathf.Clamp(GetEffectMagnitude(type, 0.3f), 0.05f, 1f);
                spriteRenderer.color = c;
            }

            yield return null;
        }

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

        ClearEffect(type);
    }

    private IEnumerator SpeedRoutine()
    {
        float lastAppliedEndTime = -1f;
        while (IsEffectActive(StatusEffectType.PlayerMoveSpeedMultiplier))
        {
            speedMultiplier = Mathf.Max(0.1f, GetEffectMagnitude(StatusEffectType.PlayerMoveSpeedMultiplier, 2f));

            if (player != null && effectEndTimes.TryGetValue(StatusEffectType.PlayerMoveSpeedMultiplier, out float endTime))
            {
                if (!Mathf.Approximately(lastAppliedEndTime, endTime))
                {
                    player.ApplySpeedBuff(Mathf.Max(0.05f, endTime - Time.time));
                    lastAppliedEndTime = endTime;
                }
            }

            yield return null;
        }

        speedMultiplier = 1f;
        ClearEffect(StatusEffectType.PlayerMoveSpeedMultiplier);
    }

    private IEnumerator InputReverseRoutine()
    {
        inputReversed = true;

        while (IsEffectActive(StatusEffectType.PlayerInputReverse))
        {
            yield return null;
        }

        inputReversed = false;
        ClearEffect(StatusEffectType.PlayerInputReverse);
    }

    private IEnumerator InputDelayRoutine()
    {
        delayedInputs.Clear();

        while (IsEffectActive(StatusEffectType.PlayerInputDelay))
        {
            inputDelaySeconds = Mathf.Clamp(GetEffectMagnitude(StatusEffectType.PlayerInputDelay, 2f), 0.05f, 5f);
            yield return null;
        }

        inputDelaySeconds = 0f;
        delayedInputs.Clear();
        delayedButtonDowns.Clear();
        delayedButtonUps.Clear();
        ClearEffect(StatusEffectType.PlayerInputDelay);
    }

    private IEnumerator BlindRoutine(StatusEffectType effectType, Color color)
    {
        if (UIManager.Instance == null || UIManager.Instance.fadeImage == null)
        {
            yield return new WaitForSeconds(GetRemainingEffectDuration(effectType));
            ClearEffect(effectType);
            yield break;
        }

        CaptureBlindOverlay();
        Color start = blindOriginalFadeColor;
        if (blindOverlayCaptured)
        {
            start = UIManager.Instance.fadeImage.color;
        }

        Color target = color;
        target.a = Mathf.Clamp01(GetEffectMagnitude(effectType));

        float transition = Mathf.Max(0.05f, blindTransitionDuration);
        UIManager.Instance.fadeImage.gameObject.SetActive(true);
        UIManager.Instance.fadeImage.transform.SetAsLastSibling();

        float elapsed = 0f;
        while (elapsed < transition)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / transition);
            UIManager.Instance.fadeImage.color = Color.Lerp(start, target, t);
            yield return null;
        }

        UIManager.Instance.fadeImage.color = target;

        while (IsEffectActive(effectType))
        {
            target.a = Mathf.Clamp01(GetEffectMagnitude(effectType));
            UIManager.Instance.fadeImage.color = target;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < transition)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / transition);
            UIManager.Instance.fadeImage.color = Color.Lerp(target, start, t);
            yield return null;
        }

        RestoreBlindOverlay();
        ClearEffect(effectType);
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

    private IEnumerator RedStateRoutine()
    {
        Color original = spriteRenderer != null ? spriteRenderer.color : Color.white;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(1f, 0.7f, 0.7f, original.a);
        }

        while (IsEffectActive(StatusEffectType.PlayerRedStateContactBurn))
        {
            float interval = Mathf.Max(0.05f, GetEffectInterval(StatusEffectType.PlayerRedStateContactBurn, 0.5f));
            ApplyContactBurn(Mathf.RoundToInt(GetEffectMagnitude(StatusEffectType.PlayerRedStateContactBurn, 50f)));
            yield return new WaitForSeconds(interval);
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = original;
        }

        ClearEffect(StatusEffectType.PlayerRedStateContactBurn);
    }

    private void ApplyContactBurn(int damage)
    {
        if (damage <= 0) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 0.6f);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null) continue;

            if (CombatTargetHitbox.TryGetEnemyCombat(hit, out EnemyCombat enemy))
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

    private IEnumerator KnockbackImmuneRoutine()
    {
        knockbackImmune = true;

        while (IsEffectActive(StatusEffectType.PlayerKnockbackImmune))
        {
            yield return null;
        }

        knockbackImmune = false;
        ClearEffect(StatusEffectType.PlayerKnockbackImmune);
    }

    private IEnumerator StunRoutine()
    {
        stunned = true;

        if (player != null)
        {
            player.SetCanMove(false);
        }

        while (IsEffectActive(StatusEffectType.PlayerStun))
        {
            yield return null;
        }

        stunned = false;
        if (player != null)
        {
            player.SetCanMove(true);
        }

        ClearEffect(StatusEffectType.PlayerStun);
    }
}
