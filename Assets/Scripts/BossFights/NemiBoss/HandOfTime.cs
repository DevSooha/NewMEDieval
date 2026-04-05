using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class HandOfTime : MonoBehaviour
{
    private readonly List<GameObject> activeProjectiles = new();

    [Header("Refs")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private FateSeverSpearEmitterController emitterCtrl;

    [Header("Projectile Prefabs (HoT)")]
    [SerializeField] private HandOfTimeProjectile prefab1x3;
    [SerializeField] private HandOfTimeProjectile prefab2x2;

    [Header("Timing")]
    [SerializeField] private float castTime = 0.5f;
    [SerializeField] private float spawnFadeInTime = 0.5f; // 탄막 페이드인 생성 시간

    [Header("Speed — Phase 1")]
    [SerializeField] private float speedWorldPerSec = 5f;

    [Header("Speed — Phase 2 (응용)")]
    [SerializeField] private float advancedSpeedWorldPerSec = 6f;

    [Header("Bedimmed Wall (Phase 2 전용)")]
    [SerializeField] private GameObject bedimmedWallVisualPrefab;
    [SerializeField] private float bedimmedWallSpeed = 4f;
    [SerializeField] private Vector2 bedimmedWallColliderSize = new Vector2(1f, 3f);

    private static readonly Vector3[] bedimmedWallSpawns =
    {
        new(-11.5f,  6.5f, 0f),
        new( 11.5f,  2.5f, 0f),
        new(-11.5f, -2.5f, 0f),
        new( 11.5f, -6.5f, 0f)
    };

    private static readonly Vector2[] bedimmedWallDirections =
    {
        Vector2.right,  // 1번: 오른쪽
        Vector2.left,   // 2번: 왼쪽
        Vector2.right,  // 3번: 오른쪽
        Vector2.left    // 4번: 왼쪽
    };

    private void Awake()
    {
        if (groundTilemap == null)
        {
            var groundObj = GameObject.FindGameObjectWithTag("Ground");
            if (groundObj != null)
                groundTilemap = groundObj.GetComponentInChildren<Tilemap>();
        }

        if (emitterCtrl == null)
        {
            emitterCtrl = FindAnyObjectByType<FateSeverSpearEmitterController>();
        }
    }

    // ===========================
    // Phase 1: 기본 Hand of Time
    // ===========================
    public IEnumerator PlayOnce(Transform playerTF)
    {
        // 0.5초 시전 모션
        yield return new WaitForSeconds(castTime);

        if (playerTF == null) yield break;

        // 탄막 생성 + 페이드인(0.5초)
        SpawnClockProjectiles(playerTF, speedWorldPerSec);

        yield return new WaitForSeconds(spawnFadeInTime);

        // 탄막 발사는 SpawnClockProjectiles 내에서 즉시 BeginFire 호출됨
    }

    // ===========================
    // Phase 2: 응용 Hand of Time
    // ===========================
    public IEnumerator PlayOnceAdvanced(Transform playerTF)
    {
        // 0.5초 시전 모션
        yield return new WaitForSeconds(castTime);

        if (playerTF == null) yield break;

        // 시계 탄막 + Bedimmed Wall 동시 생성
        SpawnClockProjectiles(playerTF, advancedSpeedWorldPerSec);
        SpawnBedimmedWalls();

        yield return new WaitForSeconds(spawnFadeInTime);
    }

    // ===========================
    // 시계 탄막 (짧은축 2x2 + 긴축 1x3) 생성 및 발사
    // ===========================
    private void SpawnClockProjectiles(Transform playerTF, float fireSpeed)
    {
        // 플레이어 world 위치 기준 emitter 찾기 (X/Y 매칭용)
        Transform emitterY = emitterCtrl.GetClosestYEmitter(playerTF.position.x);
        Transform emitterX = emitterCtrl.GetClosestXEmitter(playerTF.position.y);

        // 카메라 끝(뷰포트 경계)에서 스폰
        Camera cam = Camera.main;
        float camTop = cam != null ? cam.ViewportToWorldPoint(new Vector3(0.5f, 1f, cam.nearClipPlane)).y : emitterY.position.y;
        float camRight = cam != null ? cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, cam.nearClipPlane)).x : emitterX.position.x;

        // 2x2: emitter의 X(플레이어 근처), 카메라 상단 끝
        Vector3 spawn2x2 = new Vector3(emitterY.position.x, camTop, 0f);
        // 1x3: 카메라 우측 끝, emitter의 Y(플레이어 근처)
        Vector3 spawn1x3 = new Vector3(camRight, emitterX.position.y, 0f);

        HandOfTimeProjectile p2 =
            Instantiate(prefab2x2, spawn2x2, prefab2x2.transform.rotation);

        HandOfTimeProjectile p1 =
            Instantiate(prefab1x3, spawn1x3, prefab1x3.transform.rotation);

        activeProjectiles.Add(p2.gameObject);
        activeProjectiles.Add(p1.gameObject);

        p2.BeginFire(
            p2.transform.position + Vector3.down,
            fireSpeed,
            HandOfTimeProjectile.Axis.Vertical
        );

        p1.BeginFire(
            p1.transform.position + Vector3.left,
            fireSpeed,
            HandOfTimeProjectile.Axis.Horizontal
        );
    }

    // ===========================
    // Bedimmed Wall 4개 생성 및 발사
    // ===========================
    private void SpawnBedimmedWalls()
    {
        if (bedimmedWallVisualPrefab == null) return;

        Vector3 roomOffset = transform.root.position;

        for (int i = 0; i < bedimmedWallSpawns.Length; i++)
        {
            GameObject wallObj = new GameObject($"NemiBedimmedWall_{i}");
            wallObj.transform.position = bedimmedWallSpawns[i] + roomOffset;

            FinalBossBedimmedWallProjectile wall = wallObj.AddComponent<FinalBossBedimmedWallProjectile>();
            wall.AttachVisualTemplate(bedimmedWallVisualPrefab);

            // 파티클이 World Space라 부모 회전이 무시됨 — 직접 Local Space로 변경 후 비주얼 회전
            float angle = Mathf.Atan2(bedimmedWallDirections[i].y, bedimmedWallDirections[i].x) * Mathf.Rad2Deg + 90f;
            Quaternion targetRot = Quaternion.Euler(0f, 0f, angle);
            // 비주얼 루트(첫 번째 자식)에 직접 회전 + 2배 스케일 적용
            if (wallObj.transform.childCount > 0)
            {
                Transform visual = wallObj.transform.GetChild(0);
                visual.rotation = targetRot;
                visual.localScale = new Vector3(visual.localScale.x * 2f, visual.localScale.y * 2f, visual.localScale.z);
            }

            // 스케일/회전 적용 후 파티클 재시작 (World→Local Space로 변경, looping 활성화)
            foreach (var ps in wallObj.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.loop = true;
                ps.Play(true);
            }

            wall.Launch(
                bedimmedWallDirections[i],
                bedimmedWallSpeed,
                bedimmedWallColliderSize,
                1,
                ElementType.None
            );

            activeProjectiles.Add(wallObj);
        }
    }

    /// <summary>
    /// 모든 활성 투사체 파괴 (정리용)
    /// </summary>
    public void DestroyAllProjectiles()
    {
        foreach (var proj in activeProjectiles)
        {
            if (proj != null)
                Destroy(proj);
        }
        activeProjectiles.Clear();
    }

    private void OnDisable()
    {
        DestroyAllProjectiles();
    }

    private void OnDrawGizmos()
    {
        Vector3 roomOffset = transform.root.position;

        // Bedimmed Wall 스폰 위치 + 이동 방향
        for (int i = 0; i < bedimmedWallSpawns.Length; i++)
        {
            Vector3 spawn = bedimmedWallSpawns[i] + roomOffset;

            Gizmos.color = new Color(0.8f, 0f, 1f, 0.4f);
            Gizmos.DrawCube(spawn, new Vector3(bedimmedWallColliderSize.x, bedimmedWallColliderSize.y, 0f));
            Gizmos.color = new Color(0.8f, 0f, 1f, 0.8f);
            Gizmos.DrawWireCube(spawn, new Vector3(bedimmedWallColliderSize.x, bedimmedWallColliderSize.y, 0f));

            // 이동 방향 화살표
            Vector3 dir = (Vector3)(bedimmedWallDirections[i] * 2f);
            Gizmos.DrawLine(spawn, spawn + dir);
            Gizmos.DrawSphere(spawn + dir, 0.15f);
        }
    }
}
