using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QueenKnightGhost : MonoBehaviour
{
    private const string AttackLeftFullPath = "Base Layer.Attack_L";

    [Header("Components")]
    [SerializeField] private Animator ghostAnimator;
    [SerializeField] private SpriteRenderer ghostRenderer;

    [Header("Stained Glass Shard")]
    [SerializeField] private StainedSwordProjectile shardPrefab;
    [SerializeField] private float shardSpeed = 2f;
    [SerializeField] private float shardHomingDuration = 4.2f;
    [SerializeField] private float shardSpawnDelay = 0.2f;
    [SerializeField] private float postShardDelay = 2f;

    [Header("Sword")]
    [SerializeField] private float swordSwingDuration = 0.5f;
    [SerializeField] private float swordHitRangeTiles = 1f;
    [SerializeField] private int swordDamage = 1;
    [SerializeField] private int swordHitFrame = 11;
    [SerializeField] private float swordAnimationFps = 30f;

    [Header("Positioning")]
    [SerializeField] private float sideOffset = 1f;
    [SerializeField] private float initialDelay = 0.1f;

    private Transform playerTransform;
    private Coroutine patternRoutine;
    private StainedSwordProjectile activeProjectile;
    private readonly List<StainedSwordProjectile> spawnedProjectiles = new();

    private Action<GameObject> registerOffensive;
    private Action<GameObject> unregisterOffensive;

    private void Awake()
    {
        if (ghostAnimator == null) ghostAnimator = GetComponent<Animator>();
        if (ghostRenderer == null) ghostRenderer = GetComponent<SpriteRenderer>();
    }

    public void Setup(Action<GameObject> onRegister, Action<GameObject> onUnregister)
    {
        registerOffensive = onRegister;
        unregisterOffensive = onUnregister;
    }

    public void StartPattern(Transform player)
    {
        playerTransform = player;
        SetVisible(false);
        StopPattern();
        patternRoutine = StartCoroutine(PatternLoop());
    }

    public void StopPattern()
    {
        if (patternRoutine != null)
        {
            StopCoroutine(patternRoutine);
            patternRoutine = null;
        }

        CleanupProjectiles();
        SetVisible(false);
    }

    public void ResetState()
    {
        StopPattern();
        gameObject.SetActive(false);
    }

    private IEnumerator PatternLoop()
    {
        yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            if (playerTransform == null) yield break;

            TeleportToPlayerSide();
            SetVisible(true);

            yield return SwingSword();

            SetVisible(false);

            yield return new WaitForSeconds(shardSpawnDelay);

            StainedSwordProjectile shard = SpawnShard();

            if (shard != null)
            {
                yield return WaitForProjectileToDespawn(shard);
            }

            yield return new WaitForSeconds(postShardDelay);
        }
    }

    private void TeleportToPlayerSide()
    {
        if (playerTransform == null) return;
        transform.position = playerTransform.position + Vector3.left * sideOffset;
    }

    private IEnumerator SwingSword()
    {
        PlayAttackAnimation();

        float safeFps = Mathf.Max(1f, swordAnimationFps);
        float hitCheckDelay = Mathf.Clamp(swordHitFrame / safeFps, 0f, swordSwingDuration);

        if (hitCheckDelay > 0f)
        {
            yield return new WaitForSeconds(hitCheckDelay);
        }

        TryApplySwordDamage();

        float remain = swordSwingDuration - hitCheckDelay;
        if (remain > 0f)
        {
            yield return new WaitForSeconds(remain);
        }

        StopAttackAnimation();
    }

    private void PlayAttackAnimation()
    {
        if (ghostAnimator == null) return;
        ghostAnimator.speed = 1f;
        ghostAnimator.Play(AttackLeftFullPath, 0, 0f);
    }

    private void StopAttackAnimation()
    {
        if (ghostAnimator == null) return;
        ghostAnimator.speed = 0f;
    }

    private void TryApplySwordDamage()
    {
        if (playerTransform == null) return;

        Vector3 delta = playerTransform.position - transform.position;
        float hitRange = Mathf.Max(0f, swordHitRangeTiles);

        if (delta.x < 0f || delta.x > hitRange)
        {
            return;
        }

        BossHitResolver.TryApplyBossHit(
            playerTransform,
            swordDamage,
            transform.position
        );
    }

    private StainedSwordProjectile SpawnShard()
    {
        if (shardPrefab == null || playerTransform == null) return null;

        Vector3 spawnPosition = transform.position;

        StainedSwordProjectile projectile = Instantiate(shardPrefab, spawnPosition, Quaternion.identity);
        projectile.Configure(shardSpeed, shardHomingDuration, ignoreObstaclesOverride: true);
        projectile.Initialize(playerTransform, HandleShardDestroyed);

        registerOffensive?.Invoke(projectile.gameObject);
        spawnedProjectiles.Add(projectile);
        activeProjectile = projectile;
        return projectile;
    }

    private void HandleShardDestroyed(StainedSwordProjectile shard)
    {
        spawnedProjectiles.Remove(shard);
        if (activeProjectile == shard) activeProjectile = null;

        if (shard != null)
        {
            unregisterOffensive?.Invoke(shard.gameObject);
        }
    }

    private IEnumerator WaitForProjectileToDespawn(StainedSwordProjectile projectile)
    {
        const float maxWaitSeconds = 10f;
        float elapsed = 0f;

        while (projectile != null && elapsed < maxWaitSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void CleanupProjectiles()
    {
        foreach (StainedSwordProjectile projectile in spawnedProjectiles)
        {
            if (projectile != null)
            {
                unregisterOffensive?.Invoke(projectile.gameObject);
                Destroy(projectile.gameObject);
            }
        }
        spawnedProjectiles.Clear();
        activeProjectile = null;
    }

    private void SetVisible(bool visible)
    {
        if (ghostRenderer == null) return;

        Color c = ghostRenderer.color;
        c.a = visible ? 1f : 0f;
        ghostRenderer.color = c;
    }
}
