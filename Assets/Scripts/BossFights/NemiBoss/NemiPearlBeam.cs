using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pearl Beam 필살기 — 10개 고정 좌표에서 홀수/짝수 2단계로 빔을 발사한다.
/// NemiBossCombat에서 yield return ExecutePearlBeamPattern()으로 호출.
/// </summary>
public class NemiPearlBeam : MonoBehaviour
{
    [Header("Pearl Beam Prefab")]
    [SerializeField] private GameObject pearlBeamPrefab; // FX_PearlBeam 프리팹

    [Header("Beam Positions (10 slots)")]
    [Tooltip("(-10,-9), (-8,-9), (-6,-9), ... (8,-9) — 2 world unit 간격")]
    [SerializeField] private Vector2[] beamPositions = new Vector2[]
    {
        new(-8f, -9f), new(-6f, -9f), new(-4f, -9f), new(-2f, -9f), new( 0f, -9f),
        new( 2f, -9f), new( 4f, -9f), new( 6f, -9f), new( 8f, -9f), new(10f, -9f)
    };

    [Header("Timing")]
    [SerializeField] private float magicCircleTime = 0.8f;   // 마법진 생성 VFX 시간
    [SerializeField] private float beamFireTime = 0.3f;       // 빔 발사 VFX 시간
    [SerializeField] private float beamSustainTime = 1.5f;    // 빔 유지 (데미지 활성) 시간

    [Header("Damage")]
    [SerializeField] private int damagePerHit = 1;
    [SerializeField] private float hitInterval = 0.25f;

    [Header("Beam Collider Size")]
    [Tooltip("빔 콜라이더 크기 (x=너비, y=높이). 64px 너비 x 화면 높이")]
    [SerializeField] private Vector2 beamColliderSize = new Vector2(3f, 18f);

    [Header("Beam Visual Scale")]
    [SerializeField] private float laserStartSizeMultiplier = 5f;  // Laser PS startSizeMultiplier — 클수록 파티클 하나가 커짐

    private readonly List<GameObject> activeBeams = new();
    private bool hasExecuted;

    /// <summary>
    /// Phase2 진입 시 딱 1번만 호출. NemiBossCombat에서 yield return으로 사용.
    /// </summary>
    public IEnumerator ExecutePearlBeamPattern()
    {
        if (hasExecuted) yield break;
        hasExecuted = true;

        // 홀수번 (인덱스 0, 2, 4, 6, 8)
        int[] oddIndices  = { 0, 2, 4, 6, 8 };
        // 짝수번 (인덱스 1, 3, 5, 7, 9)
        int[] evenIndices = { 1, 3, 5, 7, 9 };

        // Step 1: 홀수 지점 빔
        yield return FireBeamWave(oddIndices);

        // Step 2: 짝수 지점 빔
        yield return FireBeamWave(evenIndices);
    }

    /// <summary>
    /// 주어진 인덱스 배열의 위치에 빔을 생성 → 마법진 → 발사 → 유지 → 파괴
    /// </summary>
    private IEnumerator FireBeamWave(int[] indices)
    {
        List<GameObject> waveBeams = new();
        List<PearlBeamHitbox> waveHitboxes = new();

        Vector3 roomOffset = transform.root.position;

        // 1) 빔 인스턴스 생성 (마법진 VFX 자동 시작)
        foreach (int idx in indices)
        {
            if (idx < 0 || idx >= beamPositions.Length) continue;

            Vector3 spawnPos = new Vector3(beamPositions[idx].x, beamPositions[idx].y, 0f) + roomOffset;
            GameObject beam = Instantiate(pearlBeamPrefab, spawnPos, Quaternion.Euler(0f, 0f, 180f));
            beam.name = $"PearlBeam_{idx}";

            // Laser PS만 startSizeMultiplier로 파티클 크기 직접 제어
            // (scalingMode:Local + size3D:0 에서 transform.localScale은 X축만 반영되고 World Space에선 불안정)
            foreach (var ps in beam.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (ps.gameObject.name == "Laser")
                {
                    var main = ps.main;
                    main.startSizeMultiplier *= laserStartSizeMultiplier;
                }
                ps.Play(true);
            }

            // 히트박스 컴포넌트 추가 (없으면)
            PearlBeamHitbox hitbox = beam.GetComponent<PearlBeamHitbox>();
            if (hitbox == null)
                hitbox = beam.AddComponent<PearlBeamHitbox>();

            hitbox.Initialize(damagePerHit, hitInterval, beamColliderSize);
            hitbox.SetDamageActive(false);

            waveBeams.Add(beam);
            waveHitboxes.Add(hitbox);
            activeBeams.Add(beam);
        }

        // 2) 0.8초 대기 — 마법진 생성 VFX 재생 중
        yield return new WaitForSeconds(magicCircleTime);

        // 3) 빔 발사 — 콜라이더 활성화
        foreach (var hitbox in waveHitboxes)
        {
            if (hitbox != null)
                hitbox.SetDamageActive(true);
        }

        // 4) 0.3초 — 빔이 화면 상단까지 올라가는 VFX 시간
        yield return new WaitForSeconds(beamFireTime);

        // 5) 0.5초 — 빔 유지, 데미지 판정 활성
        yield return new WaitForSeconds(beamSustainTime);

        // 6) 빔 비활성화 및 파괴
        foreach (var beam in waveBeams)
        {
            if (beam != null)
            {
                activeBeams.Remove(beam);
                Destroy(beam);
            }
        }
    }

    /// <summary>
    /// 외부에서 패턴을 강제 중단할 때 호출 (사망, 리셋 등)
    /// </summary>
    public void StopPattern()
    {
        StopAllCoroutines();
        DestroyAllActiveBeams();
    }

    /// <summary>
    /// 리셋 시 재사용 가능하도록 플래그 초기화
    /// </summary>
    public void ResetState()
    {
        hasExecuted = false;
        DestroyAllActiveBeams();
    }

    private void DestroyAllActiveBeams()
    {
        foreach (var beam in activeBeams)
        {
            if (beam != null)
                Destroy(beam);
        }
        activeBeams.Clear();
    }

    private void OnDisable()
    {
        StopPattern();
    }

    private void OnDrawGizmos()
    {
        Vector3 roomOffset = transform.root.position;

        // 10개 빔 위치 + 콜라이더 크기 시각화
        for (int i = 0; i < beamPositions.Length; i++)
        {
            Vector3 pos = new Vector3(beamPositions[i].x, beamPositions[i].y, 0f) + roomOffset;
            Vector3 size = new Vector3(beamColliderSize.x, beamColliderSize.y, 0f);
            // 콜라이더 오프셋 (위쪽으로 colliderSize.y/2)
            Vector3 center = pos + new Vector3(0f, beamColliderSize.y / 2f, 0f);

            bool isOdd = (i % 2 == 0); // 인덱스 0,2,4,6,8 = 홀수번
            Gizmos.color = isOdd
                ? new Color(0f, 0.8f, 1f, 0.2f)   // 홀수: 시안
                : new Color(1f, 0.5f, 0f, 0.2f);   // 짝수: 주황
            Gizmos.DrawCube(center, size);

            Gizmos.color = isOdd
                ? new Color(0f, 0.8f, 1f, 0.7f)
                : new Color(1f, 0.5f, 0f, 0.7f);
            Gizmos.DrawWireCube(center, size);

            // 스폰 지점 표시
            Gizmos.DrawSphere(pos, 0.15f);
        }
    }
}
