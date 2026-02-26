using UnityEngine;
using System.Collections; // 코루틴 사용을 위해 추가

public class TestGameStarter : MonoBehaviour
{
    [Header("시작 설정")]
    public RoomData startingRoom;
    public Transform player;

    private IEnumerator Start()
    {
        if (startingRoom == null)
        {
            Debug.LogError("GameStarter: 시작할 방 데이터가 비어있습니다!");
            yield break;
        }

        // 1. RoomManager가 먼저 초기화되고 방을 생성할 시간을 주기 위해 1프레임 대기
        // (RoomManager.Start()가 실행되어 플레이어 위치를 0,0,0으로 리셋하는 것을 방지)
        yield return null;

        // RoomManager가 아직 방을 생성하지 않았다면 생성 요청
        if (RoomManager.Instance.currentRoomData == null)
        {
            RoomManager.Instance.InitializeFirstRoom(startingRoom, Vector3.zero);
        }

        // 2. 방 생성 직후 오브젝트들이 활성화되고 검색 가능한 상태가 되도록 안전하게 한 번 더 대기
        yield return null;

        if (player != null)
        {
            MovePlayerToSpawnPoint();
        }
    }

    private void MovePlayerToSpawnPoint()
    {
        // 기본 위치 설정
        Vector3 spawnPos = new Vector3(0, -2, 0);
        GameObject spawnPointObj = null;

        // 1. 활성화된 오브젝트 중에서 우선 검색 (빠름)
        spawnPointObj = GameObject.Find("PlayerSpawnPoint");

        // 2. 못 찾았다면 비활성화된 오브젝트까지 포함하여 전역 검색 (느리지만 확실함)
        if (spawnPointObj == null)
        {
            Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in allTransforms)
            {
                if (t.name == "PlayerSpawnPoint")
                {
                    spawnPointObj = t.gameObject;
                    break;
                }
            }
        }

        if (spawnPointObj != null)
        {
            spawnPos = spawnPointObj.transform.position;
            Debug.Log($"[TestGameStarter] 스폰 포인트 발견: {spawnPos}");
        }
        else
        {
            Debug.LogWarning("[TestGameStarter] 'PlayerSpawnPoint'를 찾을 수 없어 기본 위치(0, -2, 0)로 이동합니다.");
        }

        player.position = spawnPos;

        // 플레이어 이동 후 카메라 동기화 (RoomManager가 초기화될 때 카메라가 엉뚱한 곳에 있을 수 있음)
        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.SyncCameraToPlayer();
        }
    }
}