using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class QueenCombat : BossCombatBase
{
    [Header("References")]
    [SerializeField] private PearlBeamController pearlBeam;

    [Header("Ground Tilemap (optional)")]
    [SerializeField] private Tilemap groundTilemap;

    [Header("Knockback (override default)")]
    [SerializeField] private float knockbackForce = 12f;
    [SerializeField] private float knockbackStunTime = 0.2f;
    [SerializeField] private float kbProxyOffset = 3f;

    [Header("Boss Invincible + Flicker")]
    [SerializeField] private float invincibleTime = 0.8f;
    [SerializeField] private float flickerInterval = 0.08f;

    [Header("Pattern Loop")]
    [SerializeField] private float repeatDelay = 1.8f;

    private Coroutine battleRoutine;
    private Coroutine invRoutine;

    private Transform playerTF;
    private WaitForSeconds repeatWait;

    private Transform kbProxy;
    private Collider2D bossCol;
    private SpriteRenderer[] spriteRenderers;

    private bool isInvincible = false;

    private float nextContactAllowedTime = 0f;
    private const float contactCooldown = 0.05f;

    private void Awake()
    {
        repeatWait = new WaitForSeconds(repeatDelay);

        kbProxy = new GameObject("KB_Proxy").transform;
        kbProxy.hideFlags = HideFlags.HideInHierarchy;

        bossCol = GetComponent<Collider2D>();

        if (groundTilemap == null)
        {
            var groundObj = GameObject.FindGameObjectWithTag("Ground");
            if (groundObj != null)
                groundTilemap = groundObj.GetComponentInChildren<Tilemap>();
        }

        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void OnDestroy()
    {
        if (kbProxy != null) Destroy(kbProxy.gameObject);
    }

    private void OnValidate()
    {
        repeatWait = new WaitForSeconds(repeatDelay);
    }

    public override void StartBattle()
    {
        if (battleRoutine != null) return;

        if (playerTF == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null) return;
            playerTF = playerObj.transform;
        }

        if (pearlBeam == null)
            pearlBeam = FindAnyObjectByType<PearlBeamController>();

        battleRoutine = StartCoroutine(BattleLoop());
    }

    private IEnumerator BattleLoop()
    {
        while (true)
        {
            while (pearlBeam == null)
            {
                pearlBeam = FindAnyObjectByType<PearlBeamController>();
                yield return null;
            }

            yield return pearlBeam.PlayOnce(playerTF);
            yield return repeatWait;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponent<Player>();
        if (player == null) return;

        TryHandleContact(player);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        var player = collision.collider.GetComponent<Player>();
        if (player == null) return;

        TryHandleContact(player);
    }

    private void TryHandleContact(Player player)
    {
        if (player == null) return;

        if (Time.time < nextContactAllowedTime) return;
        nextContactAllowedTime = Time.time + contactCooldown;

        if (isInvincible) return;

        // ✅ 넉백 방향: 보스 표면의 가장 가까운 점 -> 플레이어
        Vector2 bossPoint = bossCol != null
            ? bossCol.ClosestPoint(player.transform.position)
            : (Vector2)transform.position;

        Vector2 knockDir = (Vector2)player.transform.position - bossPoint;

        if (knockDir.sqrMagnitude < 0.0001f)
            knockDir = (Vector2)player.transform.position - (Vector2)transform.position;

        knockDir = knockDir.normalized;

        // ✅ 프록시는 "sender"로만 쓰는 용도
        // player.KnockBack(sender, ...)가 sender->player 방향을 쓴다고 가정하고
        // sender를 플레이어 기준 보스쪽에 위치시킴
        float offset = Mathf.Max(1.5f, kbProxyOffset);
        kbProxy.position = (Vector2)player.transform.position - knockDir * offset;

        Debug.Log($"[QueenCombat] Contact -> Knockback. inv={isInvincible}");

        // ✅ BossCombatBase의 Knockback() 사용
        // (플레이어 스크립트 안 건드릴 때 가장 안전)
        Knockback(player, kbProxy, knockbackForce, knockbackStunTime);

        // ✅ 보스 무적/깜빡
        if (invRoutine != null) StopCoroutine(invRoutine);
        invRoutine = StartCoroutine(InvincibleFlickerRoutine(invincibleTime));
    }

    private IEnumerator InvincibleFlickerRoutine(float duration)
    {
        isInvincible = true;

        float t = 0f;
        bool visible = true;

        while (t < duration)
        {
            visible = !visible;
            SetBossVisible(visible);

            yield return new WaitForSeconds(flickerInterval);
            t += flickerInterval;
        }

        RestoreBossVisible();
        isInvincible = false;
        invRoutine = null;
    }

    private void SetBossVisible(bool visible)
    {
        if (spriteRenderers == null) return;

        float a = visible ? 1f : 0f;
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            var sr = spriteRenderers[i];
            if (sr == null) continue;

            var c = sr.color;
            c.a = a;
            sr.color = c;
        }
    }

    private void RestoreBossVisible() => SetBossVisible(true);

    public void OnBeamHit(Player player, Transform beamOrigin)
    {
        // 빔 데미지 로직만(원하면 추가)
    }

    private void OnDisable()
    {
        if (invRoutine != null)
        {
            StopCoroutine(invRoutine);
            invRoutine = null;
        }

        isInvincible = false;
        RestoreBossVisible();

        if (battleRoutine != null)
        {
            StopCoroutine(battleRoutine);
            battleRoutine = null;
        }
    }
}