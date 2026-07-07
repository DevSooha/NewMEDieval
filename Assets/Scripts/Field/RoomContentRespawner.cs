using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// QS-80: 채집방 재입장 리스폰 (1차 적용 범위: spr_3 전용 allowlist — 오너 정책 2026-07-07).
/// 방 프리팹은 수정 금지 대상이라 RoomManager.SpawnRoom이 런타임 AddComponent로 주입한다.
///
/// - 몬스터: 방 생성 직후 베이크된 EnemyCombat들을 비활성 템플릿으로 복제해 두고,
///   재입장 시 잔존 개체를 정리한 뒤 템플릿에서 초기 배치 그대로 재생성한다
///   (초기 스폰이 100% 고정 배치이므로 재입장도 100% — 오너 정책 3).
/// - 아이템: Spawner.SpawnFromCSV() 재호출 — CSV 확률로 재롤, 내부 ClearPreviousSpawns가
///   잔존 아이템을 정리한다.
/// - 방을 떠났다가 다시 현재 방이 될 때만 1회 리롤한다. 체류 중 반복 생성 금지(정책 4).
/// - 보스 요소 보유 방은 allowlist에 있어도 부착하지 않는다(정책 5).
/// - 처치/획득 상태는 저장하지 않는다(정책 6) — 이 클래스는 세션 메모리로만 동작.
/// </summary>
public class RoomContentRespawner : MonoBehaviour
{
    // 1차 적용 범위. 확장은 오너 승인 후 항목 추가 (전체 방 일괄 적용 금지).
    private static readonly HashSet<string> respawnRoomIds = new HashSet<string> { "spr_3" };

    private string roomId;
    private readonly List<GameObject> enemyTemplates = new List<GameObject>();
    private readonly List<Vector3> enemyLocalPositions = new List<Vector3>();
    private Transform templateRoot;
    private Spawner[] itemSpawners;
    private bool hasLeftRoom;
    private bool templatesCaptured;

    /// <summary>RoomManager.SpawnRoom 전용 — allowlist/보스 가드를 통과한 방에만 부착.</summary>
    public static void TryAttach(string roomId, GameObject roomObj)
    {
        if (roomObj == null || !respawnRoomIds.Contains(roomId)) return;
        if (roomObj.GetComponentInChildren<BossHealth>(true) != null) return;
        if (roomObj.GetComponentInChildren<BossBattleTrigger>(true) != null) return;
        if (roomObj.GetComponent<RoomContentRespawner>() != null) return;

        RoomContentRespawner respawner = roomObj.AddComponent<RoomContentRespawner>();
        respawner.roomId = roomId;
    }

    private void Start()
    {
        // 베이크 몬스터의 Awake가 끝난 뒤(방 Instantiate 직후 프레임) 템플릿을 캡처한다.
        CaptureTemplates();
        itemSpawners = GetComponentsInChildren<Spawner>(true);
        RoomManager.OnPlayerEnteredRoom += HandlePlayerEnteredRoom;
    }

    private void OnDestroy()
    {
        RoomManager.OnPlayerEnteredRoom -= HandlePlayerEnteredRoom;
    }

    private void HandlePlayerEnteredRoom(string enteredRoomId, GameObject enteredRoomObj)
    {
        if (enteredRoomId != roomId)
        {
            hasLeftRoom = true;
            return;
        }

        // 최초 진입(초기 콘텐츠 그대로) 또는 체류 중 재발화는 리롤하지 않는다.
        if (!hasLeftRoom) return;

        hasLeftRoom = false;
        Reroll();
    }

    private void CaptureTemplates()
    {
        if (templatesCaptured) return;
        templatesCaptured = true;

        // 비활성 루트 아래에 복제해 클론의 Awake/Start를 재생성 시점까지 지연시킨다.
        GameObject rootObj = new GameObject("RespawnTemplates");
        templateRoot = rootObj.transform;
        templateRoot.SetParent(transform, false);
        rootObj.SetActive(false);

        foreach (EnemyCombat enemy in GetComponentsInChildren<EnemyCombat>(true))
        {
            if (enemy == null || enemy.transform.IsChildOf(templateRoot)) continue;

            enemyLocalPositions.Add(transform.InverseTransformPoint(enemy.transform.position));
            GameObject template = Instantiate(enemy.gameObject, templateRoot);
            template.name = enemy.gameObject.name;
            enemyTemplates.Add(template);
        }
    }

    private void Reroll()
    {
        // 잔존 몬스터 정리 (살아남은 원본 + 이전 리롤 생성분, 템플릿 제외)
        foreach (EnemyCombat enemy in GetComponentsInChildren<EnemyCombat>(true))
        {
            if (enemy == null || enemy.transform.IsChildOf(templateRoot)) continue;
            Destroy(enemy.gameObject);
        }

        // 템플릿에서 초기 배치 그대로 재생성
        for (int i = 0; i < enemyTemplates.Count; i++)
        {
            if (enemyTemplates[i] == null) continue;

            GameObject clone = Instantiate(enemyTemplates[i], transform);
            clone.transform.localPosition = enemyLocalPositions[i];
            clone.name = enemyTemplates[i].name;
            clone.SetActive(true);
        }

        // 아이템 재롤 — SpawnFromCSV 내부의 ClearPreviousSpawns가 잔존 아이템을 정리한다.
        int spawnerCount = 0;
        if (itemSpawners != null)
        {
            foreach (Spawner spawner in itemSpawners)
            {
                if (spawner == null) continue;
                spawner.SpawnFromCSV();
                spawnerCount++;
            }
        }

        Debug.Log($"[Respawn] {roomId} 재입장 리롤: 몬스터 {enemyTemplates.Count}기, 아이템 스포너 {spawnerCount}개");
    }
}
