using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(BoxCollider2D))]
public class PearlBeamController : MonoBehaviour
{
    [Header("Tilemap")]
    [SerializeField] private Tilemap groundTilemap;

    [Header("Lane Settings (Cell X)")]
    [SerializeField] private int laneCount = 13;
    [SerializeField] private int laneStartLeftX = -12; // 첫 레인의 왼쪽 셀 X
    [SerializeField] private int laneStep = 2;         // 레인 간격 (2칸)

    [Header("Beam Y Range (Cell Y)")]
    [SerializeField] private int beamYStart = -8;
    [SerializeField] private int beamYEnd = 10;

    [Header("Timing")]
    [SerializeField] private float chargeTime = 0.8f;   // 준비
    [SerializeField] private float fireDelay = 0.5f;    // 발사 직전 텀
    [SerializeField] private float sustainTime = 3.5f;  // 유지
    [SerializeField] private float delayTime = 1.8f;    // 다음 공격까지 대기

    [Header("VFX")]
    [SerializeField] private GameObject vfxStart;
    [SerializeField] private GameObject vfxLoop;

    private Transform playerTF;
    private BoxCollider2D col;

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

        SetBeamActive(false);
    }

    public void Begin(Transform player)
    {
        playerTF = player;
        Debug.Log($"[PearlBeam] Begin called. player={playerTF?.name}");

        Debug.Log($"[PearlBeam] refs: tilemap={(groundTilemap!=null)}, startVFX={(vfxStart!=null)}, loopVFX={(vfxLoop!=null)}");

        StopAllCoroutines();
        StartCoroutine(BeamRoutine());
    }

    private IEnumerator BeamRoutine()
    {
        Debug.Log("[PearlBeam] BeamRoutine started");

        while (true)
        {
            if (playerTF == null)
            {
                Debug.LogError("[PearlBeam] playerTF is null -> stop");
                yield break;
            }

            Debug.Log("[PearlBeam] charge wait...");
            yield return new WaitForSeconds(chargeTime);

            Debug.Log("[PearlBeam] UpdateBeamPosition()");
            UpdateBeamPosition();

            Debug.Log("[PearlBeam] Start VFX ON");
            if (vfxStart != null) vfxStart.SetActive(true);

            yield return new WaitForSeconds(fireDelay);

            Debug.Log("[PearlBeam] Start VFX OFF, Beam ON");
            if (vfxStart != null) vfxStart.SetActive(false);
            SetBeamActive(true);

            Debug.Log("[PearlBeam] sustain...");
            yield return new WaitForSeconds(sustainTime);

            Debug.Log("[PearlBeam] Beam OFF, delay...");
            SetBeamActive(false);

            yield return new WaitForSeconds(delayTime);
        }
    }

    private void UpdateBeamPosition()
    {
        if (groundTilemap == null) return;

        Vector3Int playerCell = groundTilemap.WorldToCell(playerTF.position);
        int px = playerCell.x;

        int laneIndex = Mathf.FloorToInt((px - laneStartLeftX) / (float)laneStep);
        laneIndex = Mathf.Clamp(laneIndex, 0, laneCount - 1);

        int laneLeftX = laneStartLeftX + laneStep * laneIndex;

        float cellW = groundTilemap.cellSize.x;
        float cellH = groundTilemap.cellSize.y;

        // 빔 중심 X 계산 (2칸 중앙)
        Vector3 leftCell = groundTilemap.GetCellCenterWorld(new Vector3Int(laneLeftX, beamYStart, 0));
        Vector3 rightCell = groundTilemap.GetCellCenterWorld(new Vector3Int(laneLeftX + 1, beamYStart, 0));

        float centerX = (leftCell.x + rightCell.x) * 0.5f;

        // Y 중앙
        float heightWorld = (beamYEnd - beamYStart) * cellH;
        float centerY = (beamYStart + beamYEnd) * 0.5f;

        transform.position = new Vector3(centerX, centerY, 0);

        // 콜라이더 크기 = 정확히 2칸
        col.size = new Vector2(2f * cellW, heightWorld);
        col.offset = Vector2.zero;

        // VFX 스케일 맞추기
        if (vfxLoop != null)
        {
            vfxLoop.transform.localScale = new Vector3(2f * cellW, heightWorld, 1f);
            vfxLoop.transform.localPosition = Vector3.zero;
        }
    }

    private void SetBeamActive(bool active)
    {
        if (col != null) col.enabled = active;

        Debug.Log($"[PearlBeam] SetBeamActive({active}) loopVFX={(vfxLoop!=null)}");
        if (vfxLoop != null) vfxLoop.SetActive(active);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!col.enabled) return;

        if (other.CompareTag("Player"))
        {
            PlayerHealth hp = other.GetComponent<PlayerHealth>();
            if (hp == null)
                hp = other.GetComponentInParent<PlayerHealth>();

            if (hp != null)
            {
                hp.TakeDamage(1);
            }
        }
    }
}