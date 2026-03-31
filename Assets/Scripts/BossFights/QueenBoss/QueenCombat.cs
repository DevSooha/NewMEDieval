using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class QueenCombat : BossCombatBase, IBossDamageModifier, IBossBattleResetNotifier, IBossStartPositioner
{
    [Header("Boss Core")]
    [SerializeField] private BossHealth bossHealth;

    [Header("Pearl Beam")]
    [SerializeField] private PearlBeamController pearlBeam;

    [Header("Knight Ghost")]
    [SerializeField] private QueenKnightGhost knightGhost;

    [Header("Queen Attack Timing")]
    [SerializeField] private float scepterRaiseDuration = 0.5f;
    [SerializeField] private float beamRepeatDelay = 8f;

    private Transform playerTF;
    private Coroutine queenAttackRoutine;
    private Coroutine deathRoutine;
    private bool isBattleRunning;

    private Transform aut3RespawnPoint;

    private const string QueenDefeatedKey = "QueenDefeated";

    public event Action OnBattleReset;

    protected override bool UseCollisionInvulnerability => false;

    public static bool IsQueenDefeated
    {
        get => PlayerPrefs.GetInt(QueenDefeatedKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(QueenDefeatedKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    private void Awake()
    {
        if (bossHealth == null) bossHealth = GetComponent<BossHealth>();

        if (bossHealth != null)
        {
            bossHealth.maxHP = 3400;
            bossHealth.currentHP = 3400;
            bossHealth.currentElement = ElementType.Light;
        }

        if (pearlBeam == null)
        {
            pearlBeam = GetComponentInChildren<PearlBeamController>(true);
            if (pearlBeam == null)
            {
                Transform roomRoot = transform.root;
                if (roomRoot != null)
                    pearlBeam = roomRoot.GetComponentInChildren<PearlBeamController>(true);
            }
        }

        if (knightGhost != null)
        {
            knightGhost.Setup(
                go => RegisterBossOffensive(go),
                go => UnregisterBossOffensive(go)
            );
        }

    }

    private void OnEnable()
    {
        ResumeBossPresentation();
        RegisterPlayerDeathBaseHandler(HandlePlayerDeath);
        TryResolvePlayerTransform(ref playerTF);
    }

    private void OnDisable()
    {
        UnregisterPlayerDeathBaseHandler(HandlePlayerDeath);

        if (bossHealth != null && bossHealth.currentHP <= 0)
        {
            IsQueenDefeated = true;
        }

        if (queenAttackRoutine != null)
        {
            StopCoroutine(queenAttackRoutine);
            queenAttackRoutine = null;
        }

        if (knightGhost != null)
        {
            knightGhost.StopPattern();
        }

        CleanupOffensivesOnDisable();
    }

    public override void StartBattle()
    {
        ResumeBossPresentation();
        if (isBattleRunning) return;
        if (!TryResolvePlayerTransform(ref playerTF)) return;

        isBattleRunning = true;
        queenAttackRoutine = StartCoroutine(QueenAttackLoop());

        if (knightGhost != null)
        {
            knightGhost.gameObject.SetActive(true);
            knightGhost.StartPattern(playerTF);
        }
    }

    public void SetToPointAImmediate()
    {
        // 여왕은 고정 위치이므로 별도 이동 없음
    }

    public float ModifyDamageMultiplier(ElementType attackType, float baseMultiplier)
    {
        return baseMultiplier;
    }

    private IEnumerator QueenAttackLoop()
    {
        while (isBattleRunning && !IsBossDefeated())
        {
            if (scepterRaiseDuration > 0f)
            {
                yield return new WaitForSeconds(scepterRaiseDuration);
            }

            if (pearlBeam != null && playerTF != null)
            {
                yield return pearlBeam.PlayOnce(playerTF);
            }

            if (beamRepeatDelay > 0f)
            {
                yield return new WaitForSeconds(beamRepeatDelay);
            }
        }
    }

    private bool IsBossDefeated()
    {
        return bossHealth != null && bossHealth.currentHP <= 0;
    }

    private void HandlePlayerDeath()
    {
        CleanupBossPresentationOnPlayerDeath();

        if (knightGhost != null)
        {
            knightGhost.StopPattern();
        }

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

            Transform resolvedRespawn = ResolveAut3RespawnPoint();
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

    private Transform ResolveAut3RespawnPoint()
    {
        if (aut3RespawnPoint != null) return aut3RespawnPoint;

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var rootGameObjects = scene.GetRootGameObjects();

        foreach (var rootGO in rootGameObjects)
        {
            if (rootGO == null) continue;

            var stack = new Stack<Transform>();
            stack.Push(rootGO.transform);

            while (stack.Count > 0)
            {
                var t = stack.Pop();
                if (t == null) continue;

                if (t.name == "PlayerSpawnPoint")
                {
                    Transform cursor = t;
                    while (cursor != null)
                    {
                        if (cursor.name == "aut_3" || cursor.name == "aut3")
                        {
                            aut3RespawnPoint = t;
                            return aut3RespawnPoint;
                        }
                        cursor = cursor.parent;
                    }
                }

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
        ResumeBossPresentation();
        isBattleRunning = false;

        if (queenAttackRoutine != null)
        {
            StopCoroutine(queenAttackRoutine);
            queenAttackRoutine = null;
        }

        if (knightGhost != null)
        {
            knightGhost.ResetState();
        }

        if (bossHealth != null)
        {
            bossHealth.currentHP = bossHealth.maxHP;
        }

        CleanupOffensivesOnBattleReset();

        if (BossManager.Instance != null)
        {
            BossManager.Instance.EndBossBattle();
        }

        gameObject.SetActive(false);
        OnBattleReset?.Invoke();
    }

}
