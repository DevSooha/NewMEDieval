using System.Collections;
using UnityEngine;

public class ShaperkeaseCombat : BossCombatBase, IBossDamageModifier
{
    public static ShaperkeaseCombat Instance;

    [Header("Shaperkease Prefabs")]
    public GameObject rayPrefab;
    public GameObject trapProjectilePrefab;

    [Header("Pool Settings")]
    [SerializeField] private int initialRayPoolSize = 15;
    [SerializeField] private int initialTrapPoolSize = 3;

    [Header("Combat Settings")]
    public float patternInterval = 7f;
    private bool isFighting = false;

    [Header("Pattern 1: Bedimmed Wall Settings")]
    public Transform bedimmedWallGroup;
    public Transform targetTransform;
    public float wallSpeed = 5.0f;
    public float safeZoneHalfSize = 2.0f;

    private BedimmedWall[] bedimmedWalls;
    private Vector3[] initialWallPositions;

    [Header("Pattern 3: Masque Illusion Stats")]
    public float trapSpeed = 4f;
    public float trapDuration = 4f;
    public float trapSpawnDistance = 3f;

    private BossProjectilePool rayPool;
    private BossProjectilePool trapPool;

    private SpriteRenderer spriteRenderer;
    private Transform player;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = 0f;
            spriteRenderer.color = c;
        }

        if (bedimmedWallGroup != null)
        {
            bedimmedWalls = bedimmedWallGroup.GetComponentsInChildren<BedimmedWall>(true);
            initialWallPositions = new Vector3[bedimmedWalls.Length];
            for (int i = 0; i < bedimmedWalls.Length; i++)
            {
                initialWallPositions[i] = bedimmedWalls[i].transform.localPosition;
            }
            bedimmedWallGroup.gameObject.SetActive(false);
        }

        if (rayPrefab != null)
        {
            rayPool = new BossProjectilePool(rayPrefab, initialRayPoolSize, transform);
        }

        if (trapProjectilePrefab != null)
        {
            trapPool = new BossProjectilePool(trapProjectilePrefab, initialTrapPoolSize, transform);
        }
    }

    public override void StartBattle()
    {
        Debug.Log("[Shaperkease] StartBattle() 호출됨! 등장 루틴 시작");
        StartCoroutine(AppearRoutine());
    }

    public float ModifyDamageMultiplier(ElementType attackType, float baseMultiplier)
    {
        if (attackType == ElementType.Light || attackType == ElementType.Dark)
        {
            return baseMultiplier * 2f;
        }

        return baseMultiplier;
    }

    IEnumerator AppearRoutine()
    {
        float timer = 0f;
        float appearTime = 1.0f;

        while (timer < appearTime)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, timer / appearTime);
            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = alpha;
                spriteRenderer.color = c;
            }
            yield return null;
        }

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            Debug.Log("[Shaperkease] 플레이어 탐색 완료, 전투 루틴 돌입");
            StartCombat();
        }
        else
        {
            Debug.LogError("[Shaperkease] Player를 찾을 수 없습니다!");
        }
    }

    public void StartCombat()
    {
        if (!isFighting)
        {
            isFighting = true;
            StartCoroutine(CombatLoop());
        }
    }

    public void StopCombat()
    {
        isFighting = false;
        StopAllCoroutines();
    }

    IEnumerator CombatLoop()
    {
        yield return new WaitForSeconds(1.0f);

        while (isFighting)
        {
            if (player == null) yield break;

            Debug.Log("[Shaperkease] 패턴 1, 2, 3 동시 전개!");

            StartCoroutine(Pattern_BedimmedWall());
            StartCoroutine(Pattern_Ray());
            StartCoroutine(Pattern_MasqueIllusion());

            Debug.Log($"[Shaperkease] 모든 패턴 발동 완료. {patternInterval}초 대기 후 재시작합니다.");
            yield return new WaitForSeconds(patternInterval);
        }
    }

    IEnumerator Pattern_BedimmedWall()
    {
        Debug.Log("패턴 1: Bedimmed Wall");
        if (!isFighting || bedimmedWallGroup == null) yield break;

        bedimmedWallGroup.gameObject.SetActive(true);

        if (bedimmedWalls != null)
        {
            for (int i = 0; i < bedimmedWalls.Length; i++)
            {
                if (bedimmedWalls[i] != null)
                {
                    bedimmedWalls[i].transform.localPosition = initialWallPositions[i];
                    bedimmedWalls[i].Activate(targetTransform, wallSpeed, safeZoneHalfSize);
                }
            }
        }

        yield return new WaitForSeconds(2f);

        if (bedimmedWallGroup != null)
            bedimmedWallGroup.gameObject.SetActive(false);
    }

    IEnumerator Pattern_Ray()
    {
        Debug.Log("패턴 2: Ray (Radial)");
        if (!isFighting || rayPool == null || player == null) yield break;

        yield return new WaitForSeconds(0.5f);

        Vector3 dir = player.position - transform.position;
        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        for (int i = 0; i < 6; i++)
        {
            float finalAngle = baseAngle + (i * 60f);
            Quaternion rotation = Quaternion.Euler(0, 0, finalAngle);
            Vector3 spawnPos = transform.position + (rotation * Vector3.right * 2.5f);

            BossProjectile bp = rayPool.Rent();
            if (bp == null) continue;

            bp.transform.position = spawnPos;
            bp.transform.rotation = rotation;
            bp.Setup(ElementType.Light);
        }

        yield return new WaitForSeconds(1.5f);
    }

    IEnumerator Pattern_MasqueIllusion()
    {
        Debug.Log("패턴 3: Masque Illusion 시전 준비!");
        if (!isFighting || trapPool == null || player == null) yield break;

        Vector2 gazeDir = Vector2.right;
        Player playerScript = player.GetComponent<Player>();

        if (playerScript != null && playerScript.LastMoveDirection != Vector2.zero)
        {
            gazeDir = playerScript.LastMoveDirection.normalized;
        }

        Vector3 spawnPos = player.position + (Vector3)(gazeDir * trapSpawnDistance);

        yield return new WaitForSeconds(0.5f);

        BossProjectile bp = trapPool.Rent();
        if (bp == null) yield break;

        bp.transform.position = spawnPos;
        bp.transform.rotation = Quaternion.identity;
        bp.Setup(ElementType.None);

        MasqueIllusionProjectile trapProjectile = bp.GetComponent<MasqueIllusionProjectile>();
        if (trapProjectile != null)
        {
            trapProjectile.InitializeTrap(player);
        }
        else
        {
            Debug.LogError("[Masque Illusion] trapProjectilePrefab에 MasqueIllusionProjectile 스크립트가 없습니다!");
        }
    }
}

