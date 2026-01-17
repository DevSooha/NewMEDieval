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

    // ★ [중요] 여기에 프로젝트의 모든 RoomData(0번, 1번, 2번...)를 드래그해서 넣으세요!
    [Header("Data Source")]
    public List<RoomData> allMapRooms = new List<RoomData>();

    private RoomData currentRoomData;
    private Dictionary<string, GameObject> loadedRooms = new Dictionary<string, GameObject>();
    private bool isCoolingDown = false;

    private void Awake()
    {
        Instance = this;

        loadedRooms = new Dictionary<string, GameObject>();
    }

    void Start()
    {

        loadedRooms.Clear();

        // ★ [핵심 수정] 플레이어가 "저장된 위치"를 가지고 있다면?
        if (Player.Instance != null && Player.Instance.HasSavedPosition)
        {
            Debug.Log("저장된 위치가 있어서 '첫 방 초기화'를 건너뜁니다.");

            player = Player.Instance.transform;

            // 초기화(InitializeFirstRoom) 대신, 내 위치에 맞는 방을 찾아서 복구시킴
            StartCoroutine(RestoreRoomState());
        }
        else
        {
            // 저장된 위치가 없을 때만 (진짜 처음 시작할 때만) 1번 방 생성
            Debug.Log("처음 시작이므로 1번 방을 생성합니다.");

            // 주의: startRoomData 변수가 인스펙터에 연결되어 있어야 함
            // 만약 startRoomData 변수가 없다면 기존에 쓰시던 방식대로 호출하세요.
            // 예: InitializeFirstRoom(allMapRooms[0], Vector3.zero); 
        }
    }

    // ★ 방 복구 코루틴 (핵심 기능 추가됨)
    IEnumerator RestoreRoomState()
    {
        yield return null; // Player 위치가 확정될 때까지 1프레임 대기

        // 1. 플레이어 위치를 기준으로 그리드 좌표 계산 (예: x=32 -> (1, 0))
        int gridX = Mathf.RoundToInt(player.position.x / gridWidth);
        int gridY = Mathf.RoundToInt(player.position.y / gridHeight);
        Vector2Int playerGridCoord = new Vector2Int(gridX, gridY);

        // 2. 전체 방 목록(allMapRooms) 뒤져서 현재 좌표랑 일치하는 방 찾기
        RoomData matchData = null;
        foreach (var data in allMapRooms)
        {
            if (data.roomCoord == playerGridCoord)
            {
                matchData = data;
                break;
            }
        }

        // 3. 맞는 방을 찾았다면 소환!
        if (matchData != null)
        {
            // 이미 로드된 게 아닐 때만 소환
            if (!loadedRooms.ContainsKey(matchData.roomID))
            {
                SpawnRoom(matchData, CalculateRoomPosition(matchData));
                currentRoomData = matchData;
                //UpdateNeighborPreload(matchData); // 이웃 방들도 미리 로딩

                Debug.Log($"[RoomManager] 좌표 {playerGridCoord}에서 {matchData.roomID}번 방을 복구했습니다.");
            }

            // 4. 카메라도 강제로 맞춤
            SyncCameraToPlayer();
        }
        else
        {
            Debug.LogWarning($"[RoomManager] 좌표 {playerGridCoord}에 해당하는 RoomData를 'All Map Rooms' 리스트에서 찾을 수 없습니다!");
        }
    }

    // ... (InitializeFirstRoom, RequestMove 등 나머지 코드는 기존과 동일) ...
    public void InitializeFirstRoom(RoomData startRoom, Vector3 position)
    {
        // 만약 이미 방이 복구되었다면(RestoreRoomState가 성공했다면) 초기화 무시
        if (currentRoomData != null) return;

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
        RefreshTargetRoom(nextRoom);

        Vector3 startCameraPos = mainCamera.transform.position;
        Vector3 startPlayerPos = player.position;

        float moveDistance = 0f;
        if (direction.x != 0)
            moveDistance = (distanceOverride > 0) ? distanceOverride : gridWidth;
        else
            moveDistance = (distanceOverride > 0) ? distanceOverride : gridHeight;

        Vector3 moveAmount = new Vector3(direction.x * moveDistance, direction.y * moveDistance, 0);
        Vector3 targetCameraPos = startCameraPos + moveAmount;

        Vector3 targetPlayerPos = GetTargetPosition(direction, targetCameraPos);

        float elapsed = 0;
        while (elapsed < transitionTime)
        {
            float t = elapsed / transitionTime;
            mainCamera.transform.position = Vector3.Lerp(startCameraPos, targetCameraPos, t);
            player.position = Vector3.Lerp(startPlayerPos, targetPlayerPos, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        mainCamera.transform.position = targetCameraPos;
        player.position = targetPlayerPos;

        if (player.TryGetComponent<Rigidbody2D>(out var rb)) rb.linearVelocity = Vector2.zero;

        currentRoomData = nextRoom;
        UpdateNeighborPreload(nextRoom);

        SetPlayerInput(true);

        if (loadedRooms.ContainsKey(nextRoom.roomID))
        {
            GameObject roomObj = loadedRooms[nextRoom.roomID];
            EndingEvent ending = roomObj.GetComponentInChildren<EndingEvent>(true);
            if (ending != null) ending.PlayEnding();
        }

        yield return new WaitForSeconds(0.1f);
        isCoolingDown = false;
    }

    private void RefreshTargetRoom(RoomData targetRoom)
    {
        if (loadedRooms.ContainsKey(targetRoom.roomID))
        {
            GameObject oldRoom = loadedRooms[targetRoom.roomID];
            Vector3 roomPos = oldRoom.transform.position;

            Destroy(oldRoom);
            loadedRooms.Remove(targetRoom.roomID);

            SpawnRoom(targetRoom, roomPos);
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
        GameObject roomObj = Instantiate(data.roomPrefab, position, Quaternion.identity);
        roomObj.name = data.roomID;
        loadedRooms.Add(data.roomID, roomObj);
    }

    private Vector3 CalculateRoomPosition(RoomData data)
    {
        return new Vector3(data.roomCoord.x * gridWidth, data.roomCoord.y * gridHeight, 0);
    }

    private void SetPlayerInput(bool active)
    {
        if (Player.Instance != null)
        {
            Player.Instance.SetCanMove(active);
        }
    }

    public void SyncCameraToPlayer()
    {
        if (player == null)
        {
            if (Player.Instance != null) player = Player.Instance.transform;
            else return;
        }

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        float targetX = Mathf.Round(player.position.x / gridWidth) * gridWidth;
        float targetY = Mathf.Round(player.position.y / gridHeight) * gridHeight;

        mainCamera.transform.position = new Vector3(targetX, targetY, -10f);
    }


    // 플레이어를 강제 이동시킨 후, 방 상태를 올바르게 고치는 함수
    public void UpdateRoomStateAfterTeleport()
    {
        if (player == null) return;

        // 1. 현재 플레이어 위치의 그리드 좌표 계산
        int gridX = Mathf.RoundToInt(player.position.x / gridWidth);
        int gridY = Mathf.RoundToInt(player.position.y / gridHeight);
        Vector2Int playerGridCoord = new Vector2Int(gridX, gridY);

        // 2. 해당 좌표의 RoomData 찾기
        RoomData matchData = null;
        foreach (var data in allMapRooms)
        {
            if (data.roomCoord == playerGridCoord)
            {
                matchData = data;
                break;
            }
        }

        // 3. 방 데이터가 있다면 강제 갱신
        if (matchData != null)
        {
            currentRoomData = matchData;

            // 만약 그 방이 로딩 안 되어 있다면 생성 (Spawn)
            if (!loadedRooms.ContainsKey(matchData.roomID))
            {
                SpawnRoom(matchData, CalculateRoomPosition(matchData));
            }

            // 주변 방 미리 로딩 (이전 방 등)
            UpdateNeighborPreload(matchData);

            // 카메라 위치 강제 맞춤
            SyncCameraToPlayer();

            Debug.Log($"[RoomManager] 텔레포트 후 {matchData.roomID}번 방으로 상태 갱신 완료");
        }
    }
}

