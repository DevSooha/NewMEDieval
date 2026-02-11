using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class KnightCombat : BossCombatBase, IBossDamageModifier
{
    private enum SwingDirection
    {
        Left = -1,
        Right = 1
    }

    [Header("Boss Core")]
    [SerializeField] private BossHealth bossHealth;
    [SerializeField] private Animator bossAnimator;

    [Header("Anchor Points")]
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;

    [Header("Pattern Settings")]
    [SerializeField] private float sideOffset = 1f;
    [SerializeField] private float teleportDuration = 0.1f;
    [SerializeField] private float swordSwingDuration = 0.5f;

    [Header("Stained Sword")]
    [SerializeField] private StainedSwordProjectile stainedSwordProjectilePrefab;
    [SerializeField] private float projectileSpawnOffset = 2f;

    [Header("Latent Thorn")]
    [SerializeField] private PlayableDirector thornTimeline;
    [SerializeField] private Transform thornPositionsParent;
    [SerializeField] private LatentThornHitbox thornHitboxPrefab;

    private readonly List<Transform> thornSpawnPoints = new();
    private readonly List<LatentThornHitbox> thornHitboxes = new();
    private readonly List<StainedSwordProjectile> spawnedProjectiles = new();

    private Transform aut1RespawnPoint;


    private Transform playerTransform;
    private Coroutine battleLoopRoutine;
    private Coroutine deathRoutine;
    private bool isBattleRunning;
    private SwingDirection lastSwingDirection = SwingDirection.Left;

    public event Action OnBattleReset;

    private void Awake()
    {
        if (bossHealth == null) bossHealth = GetComponent<BossHealth>();

        if (bossHealth != null)
        {
            bossHealth.maxHP = 5000;
            bossHealth.currentHP = 5000;
            bossHealth.currentElement = ElementType.None;
        }

        CacheThornSpawnPoints();
        BuildThornHitboxes();
        ResolvePlayerTransform();
    }

    private void OnEnable()
    {
        PlayerHealth.OnPlayerDeath += HandlePlayerDeath;
        ResolvePlayerTransform();
    }

    private void OnDisable()
    {
        PlayerHealth.OnPlayerDeath -= HandlePlayerDeath;
    }

    public override void StartBattle()
    {
        if (isBattleRunning) return;
        if (!ResolvePlayerTransform()) return;

        isBattleRunning = true;
        battleLoopRoutine = StartCoroutine(BattleLoop());
    }

    public void SetToPointAImmediate()
    {
        if (pointA != null)
        {
            transform.position = pointA.position;
        }
    }

    public float ModifyDamageMultiplier(ElementType attackType, float baseMultiplier)
    {
        return attackType == ElementType.Light ? baseMultiplier * 2f : baseMultiplier;
    }

    private IEnumerator BattleLoop()
    {
        yield return RunPattern(isLeftPattern: true);

        while (isBattleRunning)
        {
            yield return RunPattern(isLeftPattern: false);
            yield return RunPattern(isLeftPattern: true);
        }
    }

    private IEnumerator RunPattern(bool isLeftPattern)
    {
        if (!ResolvePlayerTransform()) yield break;

        Vector3 sidePosition = playerTransform.position + (isLeftPattern ? Vector3.left : Vector3.right) * sideOffset;
        yield return TeleportSmooth(sidePosition, teleportDuration);

        lastSwingDirection = isLeftPattern ? SwingDirection.Left : SwingDirection.Right;
        FaceDirection(lastSwingDirection == SwingDirection.Left ? Vector2.left : Vector2.right);
        yield return SwingSword(swordSwingDuration);

        yield return new WaitForSeconds(0.2f);
        SpawnStainedSword();

        yield return new WaitForSeconds(1f);

        Transform returnPoint = isLeftPattern ? pointA : pointB;
        if (returnPoint != null)
        {
            yield return TeleportSmooth(returnPoint.position, teleportDuration);
        }

        yield return SwingSword(swordSwingDuration);

        if (thornTimeline != null)
        {
            thornTimeline.Stop();
            thornTimeline.time = 0;
            thornTimeline.Play();
        }

        yield return HandleLatentThorn();
        yield return new WaitForSeconds(4f);
    }

    private bool ResolvePlayerTransform()
    {
        if (playerTransform != null) return true;

        if (Player.Instance != null)
        {
            playerTransform = Player.Instance.transform;
        }
        else
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                playerTransform = playerObject.transform;
            }
        }

        return playerTransform != null;
    }

    private IEnumerator TeleportSmooth(Vector3 targetPosition, float duration)
    {
        Vector3 start = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, targetPosition, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        transform.position = targetPosition;
    }

    private IEnumerator SwingSword(float duration)
    {
        if (bossAnimator != null)
        {
            bossAnimator.SetTrigger("IsAttack");
        }

        yield return new WaitForSeconds(duration);
    }

    private IEnumerator HandleLatentThorn()
    {
        yield return new WaitForSeconds(0.5f);

        EnsureThornSetup();

        foreach (LatentThornHitbox thorn in thornHitboxes)
        {
            if (thorn != null)
            {
                thorn.ActivateForSeconds(2.1f);
            }
        }

        yield return new WaitForSeconds(2.1f);
    }

    private void SpawnStainedSword()
    {
        if (stainedSwordProjectilePrefab == null) return;
        if (!ResolvePlayerTransform()) return;

        Vector3 swingOffset = Vector3.right * ((int)lastSwingDirection * projectileSpawnOffset);
        Vector3 spawnPosition = transform.position + swingOffset;

        StainedSwordProjectile projectile = Instantiate(stainedSwordProjectilePrefab, spawnPosition, Quaternion.identity);
        projectile.Initialize(playerTransform, HandleProjectileDestroyed);
        spawnedProjectiles.Add(projectile);
    }

    private void HandleProjectileDestroyed(StainedSwordProjectile projectile)
    {
        spawnedProjectiles.Remove(projectile);
    }

    private void FaceDirection(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) < 0.01f) return;

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(direction.x);
        transform.localScale = scale;
    }

    private void HandlePlayerDeath()
    {
        if (deathRoutine != null) return;
        deathRoutine = StartCoroutine(HandlePlayerDeathRoutine());
    }

    private IEnumerator HandlePlayerDeathRoutine()
    {
        if (UIManager.Instance != null)
        {
            yield return UIManager.Instance.FadeOut(0.5f);
        }

        yield return null;

        Transform player = Player.Instance != null ? Player.Instance.transform : null;
        if (player != null)
        {
            player.gameObject.SetActive(true);

            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.Resurrect();
            }

            Transform resolvedRespawn = ResolveAut1RespawnPoint();
            if (resolvedRespawn != null)
            {
                player.position = resolvedRespawn.position;
            }

            if (player.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
            {
                rb.linearVelocity = Vector2.zero;
            }

            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.UpdateRoomStateAfterTeleport();
                RoomManager.Instance.SyncCameraToPlayer();
            }
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideSelectPanel();
            yield return UIManager.Instance.FadeIn(0.1f);
        }

        ResetBattleForRetry();
        deathRoutine = null;
    }

    private Transform ResolveAut1RespawnPoint()
    {
        if (aut1RespawnPoint != null) return aut1RespawnPoint;

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var rootGameObjects = scene.GetRootGameObjects();

        foreach (var rootGO in rootGameObjects)
        {
            if (rootGO == null) continue;

            // 깊이 우선 탐색 스택으로 하위 트리를 순회
            var stack = new Stack<Transform>();
            stack.Push(rootGO.transform);

            while (stack.Count > 0)
            {
                var t = stack.Pop();
                if (t == null) continue;

                if (t.name == "PlayerSpawnPoint")
                {
                    // 찾은 노드에서부터 부모를 타고 올라가며 aut_1이 있는지 확인
                    Transform cursor = t;
                    while (cursor != null)
                    {
                        if (cursor.name == "aut_1")
                        {
                            aut1RespawnPoint = t;
                            return aut1RespawnPoint;
                        }
                        cursor = cursor.parent;
                    }
                }

                // 자식들을 스택에 추가
                // foreach (Transform child in t) 가 IEnumerator를 사용하므로 null 안정성 유지
                for (int i = 0; i < t.childCount; i++)
                {
                    var child = t.GetChild(i);
                    if (child != null)
                    {
                        stack.Push(child);
                    }
                }
            }
        }

        return null;
    }

    private void ResetBattleForRetry()
    {
        isBattleRunning = false;

        if (battleLoopRoutine != null)
        {
            StopCoroutine(battleLoopRoutine);
            battleLoopRoutine = null;
        }

        foreach (StainedSwordProjectile projectile in spawnedProjectiles)
        {
            if (projectile != null)
            {
                Destroy(projectile.gameObject);
            }
        }
        spawnedProjectiles.Clear();

        foreach (LatentThornHitbox thorn in thornHitboxes)
        {
            if (thorn != null)
            {
                thorn.ResetState();
            }
        }

        if (bossHealth != null)
        {
            bossHealth.currentHP = bossHealth.maxHP;
        }

        SetToPointAImmediate();

        if (BossManager.Instance != null)
        {
            BossManager.Instance.EndBossBattle();
        }

        gameObject.SetActive(false);
        OnBattleReset?.Invoke();
    }

    private void CacheThornSpawnPoints()
    {
        thornSpawnPoints.Clear();

        if (thornPositionsParent == null) return;

        foreach (Transform child in thornPositionsParent)
        {
            thornSpawnPoints.Add(child);
        }
    }

    private void BuildThornHitboxes()
    {
        foreach (LatentThornHitbox thorn in thornHitboxes)
        {
            if (thorn != null)
            {
                Destroy(thorn.gameObject);
            }
        }

        thornHitboxes.Clear();

        if (thornHitboxPrefab == null) return;

        foreach (Transform spawnPoint in thornSpawnPoints)
        {
            if (spawnPoint == null) continue;

            LatentThornHitbox thorn = Instantiate(thornHitboxPrefab, spawnPoint.position, Quaternion.identity, transform);
            thorn.ResetState();
            thornHitboxes.Add(thorn);
        }
    }

    private void EnsureThornSetup()
    {
        if (thornSpawnPoints.Count == 0)
        {
            CacheThornSpawnPoints();
        }

        if (thornHitboxes.Count != thornSpawnPoints.Count)
        {
            BuildThornHitboxes();
        }

        for (int i = 0; i < thornHitboxes.Count; i++)
        {
            if (thornHitboxes[i] != null && thornSpawnPoints[i] != null)
            {
                thornHitboxes[i].transform.position = thornSpawnPoints[i].position;
                thornHitboxes[i].gameObject.SetActive(true);
            }
        }
    }
}
