using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(BoxCollider2D))]
public class PearlBeamController : MonoBehaviour
{
    [SerializeField] private Tilemap groundTilemap;
    private Transform playerTF;

    [SerializeField] private Transform[] emitters = new Transform[13];

    [SerializeField] private int laneStartLeftX = -12;
    [SerializeField] private int laneStep = 2;
    [SerializeField] private int laneCount = 13;

    [SerializeField] private float startVfxTime = 0.3f;

    private int lockedLaneIndex = -1;
    private int lockedLaneLeftX = 0;

    [Header("MagicCircle Line (Cell Y)")]
    [SerializeField] private int magicCircleY = -9;

    [Header("VFX")]
    [SerializeField] private GameObject vfxStart;

    private Vector3Int lockedPlayerCell;

    [SerializeField] private float fireDelay = 0.2f;

    [Header("Beam Range (Cell Y)")]
    [SerializeField] private int beamYStart = -8;
    [SerializeField] private int beamYEnd = 10;

    private BoxCollider2D col;

    [Header("Loop VFX")]
    [SerializeField] private GameObject vfxLoop;
    [SerializeField] private bool loopLengthOnX = true;
    [SerializeField] private Transform loopVisualRoot;
    [SerializeField] private float sustainTime = 1.0f;

    [Header("Damage")]
    [SerializeField] private int damagePerHit = 1;

    [Header("Hit Tick")]
    [SerializeField] private float hitInterval = 0.25f;
    private float nextHitTime = 0f;

    [Header("Damage Blocker (Flower)")]
    [SerializeField] private LayerMask flowerMask;   // Flower 타일맵/콜라이더 레이어
    [SerializeField] private float blockerSkin = 0.02f;

    // ✅ Player 스크립트 수정 없이 넉백 유도용(가짜 sender)
    [Header("Knockback (No Player Script Change)")]
    [SerializeField] private float knockbackForce = 8f;
    [SerializeField] private float knockbackStunTime = 0.15f;
    [SerializeField] private float knockbackSenderOffsetX = 1.0f; // 더미 sender를 플레이어 옆에 둘 거리(월드)

    private Transform knockbackSenderDummy;

    private void Awake()
    {
        col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.enabled = false;

        if (groundTilemap == null)
        {
            GameObject groundObj = GameObject.FindGameObjectWithTag("Ground");
            if (groundObj != null)
                groundTilemap = groundObj.GetComponentInChildren<Tilemap>();
        }

        // ✅ 더미 sender 생성
        var go = new GameObject("[PearlBeam]KnockbackSenderDummy");
        go.hideFlags = HideFlags.HideInHierarchy;
        knockbackSenderDummy = go.transform;
    }

    private void OnDestroy()
    {
        if (knockbackSenderDummy != null)
            Destroy(knockbackSenderDummy.gameObject);
    }

    public IEnumerator PlayOnce(Transform player)
    {
        playerTF = player;
        if (groundTilemap == null || playerTF == null) yield break;

        yield return new WaitForSeconds(0.5f);

        lockedPlayerCell = groundTilemap.WorldToCell(playerTF.position);
        yield return new WaitForSeconds(0.8f);

        yield return StartCoroutine(secondStage());
    }

    private IEnumerator secondStage()
    {
        int px = lockedPlayerCell.x;

        int laneIndex = Mathf.FloorToInt((px - laneStartLeftX) / (float)laneStep);
        laneIndex = Mathf.Clamp(laneIndex, 0, laneCount - 1);

        lockedLaneIndex = laneIndex;
        lockedLaneLeftX = laneStartLeftX + laneIndex * laneStep;

        Transform chosenEmitter = emitters[laneIndex];
        if (chosenEmitter == null) yield break;

        yield return StartCoroutine(FireRoutine(chosenEmitter, laneIndex, lockedLaneLeftX));
    }

    private IEnumerator FireRoutine(Transform emitter, int laneIndex, int laneLeftX)
    {
        Vector3 a = groundTilemap.GetCellCenterWorld(new Vector3Int(laneLeftX, magicCircleY, 0));
        Vector3 b = groundTilemap.GetCellCenterWorld(new Vector3Int(laneLeftX + 1, magicCircleY, 0));
        Vector3 anchorPos = (a + b) * 0.5f;

        transform.position = anchorPos;

        if (vfxStart != null)
        {
            vfxStart.transform.SetParent(null, true);
            vfxStart.transform.position = anchorPos;
            vfxStart.transform.rotation = Quaternion.identity;

            vfxStart.SetActive(false);
            vfxStart.SetActive(true);

            var pss = vfxStart.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in pss)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Clear(true);
                ps.Play(true);
            }

            yield return new WaitForSeconds(startVfxTime);

            vfxStart.SetActive(false);
        }

        yield return new WaitForSeconds(fireDelay);

        float heightWorld = SetupBeamTransformAndCollider_AnchorAtMagicY(laneLeftX);
        PlayLoopVfx(heightWorld);

        nextHitTime = 0f;
        col.enabled = true;

        yield return new WaitForSeconds(sustainTime);

        StopLoopVfx();
        col.enabled = false;
    }

    private float SetupBeamTransformAndCollider_AnchorAtMagicY(int laneLeftX)
    {
        if (groundTilemap == null || col == null) return 0f;

        float cellW = groundTilemap.cellSize.x;
        float cellH = groundTilemap.cellSize.y;

        int minY = Mathf.Min(beamYStart, beamYEnd);
        int maxY = Mathf.Max(beamYStart, beamYEnd);

        Vector3 a = groundTilemap.GetCellCenterWorld(new Vector3Int(laneLeftX, magicCircleY, 0));
        Vector3 b = groundTilemap.GetCellCenterWorld(new Vector3Int(laneLeftX + 1, magicCircleY, 0));
        float centerX = (a.x + b.x) * 0.5f;
        float baseY = (a.y + b.y) * 0.5f;

        Vector3 yMinCenter = groundTilemap.GetCellCenterWorld(new Vector3Int(laneLeftX, minY, 0));
        Vector3 yMaxCenter = groundTilemap.GetCellCenterWorld(new Vector3Int(laneLeftX, maxY, 0));

        float fullHeight = Mathf.Abs(yMaxCenter.y - yMinCenter.y) + cellH;

        transform.position = new Vector3(centerX, baseY, 0f);

        float damageHeight = ComputeDamageHeightUntilFlower(transform.position, fullHeight);

        col.size = new Vector2(2f * cellW, damageHeight);
        col.offset = new Vector2(0f, damageHeight * 0.5f);

        return fullHeight;
    }

    private void TryHit(Collider2D other)
    {
        if (col == null || !col.enabled) return;
        if (!other.CompareTag("Player")) return;

        if (Time.time < nextHitTime) return;

        var player = other.GetComponent<Player>();
        if (player == null) return;

        nextHitTime = Time.time + hitInterval;

        // ✅ 셀 Y로 방향 결정
        int hitCellY = groundTilemap != null
            ? groundTilemap.WorldToCell(other.transform.position).y
            : Mathf.RoundToInt(other.transform.position.y);

        bool knockRight = (hitCellY >= -9 && hitCellY <= 0);
        bool knockLeft = (hitCellY > 0);

        // ✅ 데미지/피격은 기존 2파라미터만 호출 (3파라미터/ApplyKnockback 사용 안함)
        BossHitResolver.TryApplyBossHit(other, damagePerHit, transform.position);

        // ✅ 넉백(플레이어 스크립트 수정 없이) : 더미 sender 위치로 방향 유도
        if (knockbackSenderDummy != null && (knockRight || knockLeft))
        {
            Vector3 p = player.transform.position;

            // 오른쪽 넉백 = sender를 플레이어 왼쪽에 둔다
            // 왼쪽  넉백 = sender를 플레이어 오른쪽에 둔다
            float senderX = p.x + (knockLeft ? +knockbackSenderOffsetX : -knockbackSenderOffsetX);

            knockbackSenderDummy.position = new Vector3(senderX, p.y, p.z);
            PlayerStatusController status = player.GetComponent<PlayerStatusController>();
            if (status == null || !status.IsKnockbackImmune)
            {
                player.KnockBack(knockbackSenderDummy, knockbackForce, knockbackStunTime);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other) => TryHit(other);
    private void OnTriggerStay2D(Collider2D other) => TryHit(other);

    private void PlayLoopVfx(float heightWorld)
    {
        if (vfxLoop == null || loopVisualRoot == null) return;

        vfxLoop.transform.position = transform.position;
        loopVisualRoot.localRotation = Quaternion.Euler(0f, 0f, 90f);

        Vector3 s = loopVisualRoot.localScale;
        loopVisualRoot.localScale = loopLengthOnX
            ? new Vector3(heightWorld, s.y, s.z)
            : new Vector3(s.x, heightWorld, s.z);

        vfxLoop.SetActive(false);
        vfxLoop.SetActive(true);

        var pss = vfxLoop.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in pss)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
            ps.Play(true);
        }
    }

    private void StopLoopVfx()
    {
        if (vfxLoop == null) return;

        var pss = vfxLoop.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in pss)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        vfxLoop.SetActive(false);
    }

    private Vector3 GetLaneAnchorWorld(int laneLeftX)
    {
        Vector3 a = groundTilemap.GetCellCenterWorld(new Vector3Int(laneLeftX, magicCircleY, 0));
        Vector3 b = groundTilemap.GetCellCenterWorld(new Vector3Int(laneLeftX + 1, magicCircleY, 0));
        return (a + b) * 0.5f;
    }

    private float ComputeDamageHeightUntilFlower(Vector3 origin, float fullHeight)
    {
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.up, fullHeight, flowerMask);
        if (hit.collider == null)
        {
            Debug.Log("[PearlBeam] No flower hit by raycast");
            return fullHeight;
        }

        Debug.Log($"[PearlBeam] Flower blocks at distance={hit.distance:F2} hit={hit.collider.name}");
        return Mathf.Max(0f, hit.distance - blockerSkin);
    }
}
