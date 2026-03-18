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
    private bool isFighting;

    [Header("Pattern 1: Bedimmed Wall Settings")]
    public Transform bedimmedWallGroup;
    public Transform targetTransform;
    public float wallSpeed = 5.0f;
    public float safeZoneHalfSize = 2.0f;

    private BedimmedWall[] bedimmedWalls;
    private Vector3[] initialWallPositions;

    [Header("Pattern 2: Ray Settings")]
    [SerializeField] private float raySpawnOffsetFromBoss = 2.5f;

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
        StartCoroutine(AppearRoutine());
    }

    private void OnDisable()
    {
        StopCombat();

        if (bedimmedWallGroup != null)
        {
            bedimmedWallGroup.gameObject.SetActive(false);
        }

        CleanupOffensivesOnDisable();
    }

    public float ModifyDamageMultiplier(ElementType attackType, float baseMultiplier)
    {
        if (attackType == ElementType.Light || attackType == ElementType.Dark)
        {
            return baseMultiplier * 2f;
        }

        return baseMultiplier;
    }

    private IEnumerator AppearRoutine()
    {
        float appearTime = 1.0f;
        yield return FadeSpriteAlpha(spriteRenderer, appearTime, 0f, 1f);

        if (TryResolvePlayerTransform(ref player))
        {
            StartCombat();
        }
        else
        {
            Debug.LogError("[Shaperkease] Player not found.");
        }
    }

    public void StartCombat()
    {
        if (isFighting)
        {
            return;
        }

        isFighting = true;
        StartCoroutine(CombatLoop());
    }

    public void StopCombat()
    {
        isFighting = false;
        StopAllCoroutines();
    }

    private IEnumerator CombatLoop()
    {
        yield return new WaitForSeconds(1.0f);

        while (isFighting)
        {
            if (player == null) yield break;

            StartCoroutine(Pattern_BedimmedWall());
            StartCoroutine(Pattern_Ray());
            StartCoroutine(Pattern_MasqueIllusion());

            yield return new WaitForSeconds(patternInterval);
        }
    }

    private IEnumerator Pattern_BedimmedWall()
    {
        if (!isFighting || bedimmedWallGroup == null) yield break;

        bedimmedWallGroup.gameObject.SetActive(true);

        if (bedimmedWalls != null)
        {
            for (int i = 0; i < bedimmedWalls.Length; i++)
            {
                if (bedimmedWalls[i] == null) continue;

                bedimmedWalls[i].transform.localPosition = initialWallPositions[i];
                bedimmedWalls[i].Activate(targetTransform, wallSpeed, safeZoneHalfSize);
            }
        }

        yield return new WaitForSeconds(2f);

        if (bedimmedWallGroup != null)
        {
            bedimmedWallGroup.gameObject.SetActive(false);
        }
    }

    private IEnumerator Pattern_Ray()
    {
        if (!isFighting || rayPool == null || player == null) yield break;

        yield return new WaitForSeconds(0.5f);
        float[] fixedAngles = { 0f, 60f, 120f, 180f, 240f, 300f };

        for (int i = 0; i < fixedAngles.Length; i++)
        {
            Vector3 fireDir = Quaternion.Euler(0f, 0f, fixedAngles[i]) * Vector3.right;
            Vector3 spawnPos = transform.position + (fireDir * raySpawnOffsetFromBoss);
            Quaternion rotation = Quaternion.FromToRotation(Vector3.down, fireDir);

            BossProjectile bp = rayPool.Rent();
            if (bp == null) continue;

            RegisterBossOffensive(bp.gameObject);
            bp.transform.position = spawnPos;
            bp.transform.rotation = rotation;
            bp.Setup(ElementType.Light);
        }

        yield return new WaitForSeconds(1.5f);
    }

    private IEnumerator Pattern_MasqueIllusion()
    {
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

        RegisterBossOffensive(bp.gameObject);
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
            Debug.LogError("[Masque Illusion] Missing MasqueIllusionProjectile component.");
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        DrawRaySpawnPreview();

        Transform previewTarget = targetTransform != null ? targetTransform : player;
        if (previewTarget == null)
        {
            return;
        }

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.85f);
        Gizmos.DrawWireSphere(previewTarget.position, trapSpawnDistance);

        Vector3 previewDirection = Vector3.right;
        Vector3 previewSpawnPosition = previewTarget.position + (previewDirection * trapSpawnDistance);

        Gizmos.color = new Color(0.1f, 1f, 0.5f, 0.9f);
        Gizmos.DrawLine(previewTarget.position, previewSpawnPosition);
        Gizmos.DrawWireSphere(previewSpawnPosition, 0.3f);
    }

    private void DrawRaySpawnPreview()
    {
        float[] fixedAngles = { 0f, 60f, 120f, 180f, 240f, 300f };

        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.9f);
        for (int i = 0; i < fixedAngles.Length; i++)
        {
            Vector3 fireDir = Quaternion.Euler(0f, 0f, fixedAngles[i]) * Vector3.right;
            Vector3 spawnPos = transform.position + (fireDir * raySpawnOffsetFromBoss);

            Gizmos.DrawLine(transform.position, spawnPos);
            Gizmos.DrawWireSphere(spawnPos, 0.2f);
        }
    }
#endif
}
