using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance;

    [Header("Settings")]
    public float transitionTime = 0.4f;
    public float playerSpawnOffset = 1.5f;

    [Header("1. Grid Spacing")]
    public float gridWidth = 32.0f;
    public float gridHeight = 18.0f;

    [Header("2. Playable Size")]
    public float playableWidth = 28.0f;
    public float playableHeight = 18.0f;

    [Header("References")]
    public Camera mainCamera;
    public Transform player;

    private RoomData currentRoomData;
    private Dictionary<string, GameObject> loadedRooms = new Dictionary<string, GameObject>();
    private bool isCoolingDown = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void InitializeFirstRoom(RoomData startRoom, Vector3 position)
    {
        currentRoomData = startRoom;
        SpawnRoom(startRoom, position);
        UpdateNeighborPreload(startRoom);

        if (mainCamera != null)
        {
            mainCamera.transform.position = new Vector3(position.x, position.y, -10f);
        }
        StartCoroutine(StartSpawnProtection());
    }

    IEnumerator StartSpawnProtection()
    {
        isCoolingDown = true;
        yield return new WaitForSeconds(0.5f);
        isCoolingDown = false;
    }

    public void RequestMove(Vector2 direction, RoomData nextRoom, float distanceOverride = 0f)
    {
        if (BossManager.Instance != null && BossManager.Instance.IsBossActive) return;
        if (isCoolingDown) return;

        // 로딩이 안 되어 있으면 방어 코드
        if (!loadedRooms.ContainsKey(nextRoom.roomID))
        {
            SpawnRoom(nextRoom, CalculateRoomPosition(nextRoom));
        }

        StartCoroutine(TransitionRoutine(direction, nextRoom, distanceOverride));
    }

    private IEnumerator TransitionRoutine(Vector2 direction, RoomData nextRoom, float distanceOverride)
    {
        isCoolingDown = true;
        SetPlayerInput(false);

        // ★ [핵심 추가] 목표 방을 "새것"으로 교체합니다. (풀, 몬스터, 보스 리셋)
        RefreshTargetRoom(nextRoom);
        // -----------------------------------------------------------------

        Vector3 startCameraPos = mainCamera.transform.position;
        Vector3 startPlayerPos = player.position;

        // 1. 카메라 목표 계산
        float moveDistance = 0f;
        if (direction.x != 0)
            moveDistance = (distanceOverride > 0) ? distanceOverride : gridWidth;
        else
            moveDistance = (distanceOverride > 0) ? distanceOverride : gridHeight;

        Vector3 moveAmount = new Vector3(direction.x * moveDistance, direction.y * moveDistance, 0);
        Vector3 targetCameraPos = startCameraPos + moveAmount;

        // 2. 플레이어 목표 계산
        Vector3 targetPlayerPos = GetTargetPosition(direction, targetCameraPos);

        // 3. 이동 연출
        float elapsed = 0;
        while (elapsed < transitionTime)
        {
            float t = elapsed / transitionTime;
            mainCamera.transform.position = Vector3.Lerp(startCameraPos, targetCameraPos, t);
            player.position = Vector3.Lerp(startPlayerPos, targetPlayerPos, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 4. 도착 확정
        mainCamera.transform.position = targetCameraPos;
        player.position = targetPlayerPos;

        if (player.TryGetComponent<Rigidbody2D>(out var rb)) rb.linearVelocity = Vector2.zero;

        // 5. 데이터 갱신
        currentRoomData = nextRoom;
        UpdateNeighborPreload(nextRoom);

        SetPlayerInput(true);

        // 엔딩 체크
        if (loadedRooms.ContainsKey(nextRoom.roomID))
        {
            GameObject roomObj = loadedRooms[nextRoom.roomID];
            EndingEvent ending = roomObj.GetComponentInChildren<EndingEvent>(true);
            if (ending != null) ending.PlayEnding();
        }

        yield return new WaitForSeconds(0.1f);
        isCoolingDown = false;
    }

    // ★ [추가된 함수] 목표 방을 파괴하고 그 자리에 다시 생성함
    private void RefreshTargetRoom(RoomData targetRoom)
    {
        if (loadedRooms.ContainsKey(targetRoom.roomID))
        {
            // 1. 기존에 미리 로딩되어 있던 방(헌것)을 찾는다.
            GameObject oldRoom = loadedRooms[targetRoom.roomID];
            Vector3 roomPos = oldRoom.transform.position; // 위치 기억

            // 2. 헌 방을 파괴한다. (잘린 풀, 죽은 몬스터 삭제됨)
            Destroy(oldRoom);
            loadedRooms.Remove(targetRoom.roomID);

            // 3. 프리팹에서 새 방을 생성한다. (모든 게 초기화된 상태)
            SpawnRoom(targetRoom, roomPos);

            // 참고: SpawnRoom 내부에서 loadedRooms에 다시 추가하고, 
            // Instantiate 되면서 Spawner의 Start()가 실행되어 몬스터도 다시 나옵니다.
        }
    }
    private Vector3 GetTargetPosition(Vector2 direction, Vector3 nextRoomCenterPos)
    {
        float currentHalfWidth = playableWidth / 2f;
        float currentHalfHeight = playableHeight / 2f;

        Vector3 targetPos = player.position;

        if (direction == Vector2.up)
            targetPos = new Vector3(player.position.x, nextRoomCenterPos.y - currentHalfHeight + playerSpawnOffset, 0);

        else if (direction == Vector2.down)
            targetPos = new Vector3(player.position.x, nextRoomCenterPos.y + currentHalfHeight - playerSpawnOffset, 0);

        else if (direction == Vector2.right)
            targetPos = new Vector3(nextRoomCenterPos.x - currentHalfWidth + playerSpawnOffset, player.position.y, 0);

        else if (direction == Vector2.left)
            targetPos = new Vector3(nextRoomCenterPos.x + currentHalfWidth - playerSpawnOffset, player.position.y, 0);

        return targetPos;
    }

    private void UpdateNeighborPreload(RoomData current)
    {
        HashSet<string> neighborsToKeep = new HashSet<string>();
        neighborsToKeep.Add(current.roomID);

        RoomData[] neighbors = { current.north, current.south, current.east, current.west };

        foreach (var neighbor in neighbors)
        {
            if (neighbor != null)
            {
                neighborsToKeep.Add(neighbor.roomID);
                if (!loadedRooms.ContainsKey(neighbor.roomID))
                {
                    SpawnRoom(neighbor, CalculateRoomPosition(neighbor));
                }
            }
        }

        List<string> roomsToRemove = new List<string>();
        foreach (var loadedID in loadedRooms.Keys)
        {
            if (!neighborsToKeep.Contains(loadedID)) roomsToRemove.Add(loadedID);
        }

        foreach (var id in roomsToRemove)
        {
            Destroy(loadedRooms[id]);
            loadedRooms.Remove(id);
        }
    }

    private void SpawnRoom(RoomData data, Vector3 position)
    {
        // Instantiate가 일어날 때, 방 프리팹에 붙은 Spawner의 Start()가 자동 실행됨 -> 몬스터 리스폰
        GameObject roomObj = Instantiate(data.roomPrefab, position, Quaternion.identity);
        roomObj.name = data.roomID; // 디버깅 편하게 이름 설정
        loadedRooms.Add(data.roomID, roomObj);
    }

    private Vector3 CalculateRoomPosition(RoomData data)
    {
        return new Vector3(data.roomCoord.x * gridWidth, data.roomCoord.y * gridHeight, 0);
    }

    private void SetPlayerInput(bool active)
    {
        // 플레이어 움직임 멈추는 코드 (필요하면 구현)
        if (player.TryGetComponent<Player>(out var p))
        {
            p.SetCanMove(active);
        }
    }
}