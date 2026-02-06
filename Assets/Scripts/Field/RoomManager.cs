using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance;

    public static Vector3? restartPointOverride = null;

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

    [Header("Data Source")]
    // ★ [필수] 0번 방(시작 방)을 여기에 할당해 주세요. 없으면 리스트의 첫 번째를 사용합니다.
    public RoomData startRoomData;
    public List<RoomData> allMapRooms = new List<RoomData>();

    public RoomData currentRoomData;
    private Dictionary<string, GameObject> loadedRooms = new Dictionary<string, GameObject>();
    private bool isCoolingDown = false;
    private bool isTransitioning = false;

    private Vector3 lastSafeEntryPosition;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        loadedRooms = new Dictionary<string, GameObject>();
    }

    void Start()
    {
        loadedRooms.Clear();

        // 1. 플레이어 참조 확보
        if (player == null && Player.Instance != null)
            player = Player.Instance.transform;

        // 2. 만약 참조가 없거나(null), 참조한 객체가 파괴된 상태(MissingReference)라면?
        // (Unity에서 죽은 객체는 null 체크시 true를 반환하지만, 확실하게 하기 위해)
        if (player == null)
        {
            // 태그로 다시 찾기 (씬에 새로 생성된 플레이어가 있을 수 있음)
            GameObject foundPlayer = GameObject.FindGameObjectWithTag("Player");
            if (foundPlayer != null)
            {
                player = foundPlayer.transform;

                // 혹시 비활성화 상태라면 켜주기
                if (!foundPlayer.activeSelf) foundPlayer.SetActive(true);
            }
        }

        if (player == null)
        {
            Debug.LogError("[RoomManager] 치명적 오류: Player를 찾을 수 없습니다!");
            return;
        }

        // =========================================================
        // ★ [핵심 논리 분기] : 재시작 vs 이어하기 vs 새 게임
        // =========================================================

        // 1. 재시작(Restart) 데이터가 있는가? (죽어서 다시 시작한 경우)
        if (restartPointOverride.HasValue)
        {
            Debug.Log("[RoomManager] 재시작 요청 감지! 직전 방 위치로 복구합니다.");

            // 위치 강제 설정
            player.position = restartPointOverride.Value;

            // 데이터 사용 후 초기화 (다음번엔 정상 로드 되도록)
            restartPointOverride = null;

            StartCoroutine(RestoreRoutine());
        }
        // 2. 저장된 위치가 있는가? (게임 껐다 킨 경우)
        else if (Player.Instance.HasSavedPosition)
        {
            Debug.Log("저장된 위치로 복구합니다.");
            StartCoroutine(RestoreRoutine());
        }
        // 3. 쌩 처음 시작
        else
        {
            Debug.Log("새 게임을 시작합니다.");
            StartNewGame();
        }
    }

    // ========================================================================
    // 1. 초기화 및 복구 로직 (Logic: Initialization & Restoration)
    // ========================================================================

    // [기능] 새 게임 시작 (위치 0,0 초기화 및 첫 방 로드)
    private void StartNewGame()
    {
        player.position = Vector3.zero;
        lastSafeEntryPosition = player.position;

        if (player.TryGetComponent<Rigidbody2D>(out var rb)) rb.linearVelocity = Vector2.zero;

        RoomData initialData = startRoomData != null ? startRoomData : (allMapRooms.Count > 0 ? allMapRooms[0] : null);

        if (initialData != null) InitializeFirstRoom(initialData, Vector3.zero);
    }

    // ★ [수정] 방 복구 코루틴 (안전성 강화 버전)
    IEnumerator RestoreRoutine()
    {
        // 1. 씬 로드 직후 객체들이 생성/파괴(정리)되는 과정을 위해 1프레임 대기
        yield return null;

        // ================================================================
        // ★ [핵심 수정] 참조 재연결 (Re-assign)
        // 대기하는 동안, Start에서 잡았던 player 변수가 파괴되었을 수 있습니다.
        // 따라서 살아남은 '진짜' 싱글톤 인스턴스로 변수를 갱신합니다.
        // ================================================================
        if (Player.Instance != null)
        {
            player = Player.Instance.transform;
        }
        else
        {
            // 만약 싱글톤이 끊겼다면 태그로라도 다시 찾습니다.
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) player = found.transform;
        }

        // 2. 재연결 시도 후에도 없으면 진짜 에러 처리
        if (player == null)
        {
            Debug.LogError("[RoomManager] 치명적 오류: 씬 로드 후 Player가 완전히 사라졌습니다.");
            yield break;
        }

        // 3. 플레이어가 죽으면서 꺼져있을 수 있으므로 강제 활성화
        player.gameObject.SetActive(true);

        // 4. 상태 복구 실행
        RefreshRoomState();

        // 5. 복구된 위치를 '안전한 위치'로 기억
        lastSafeEntryPosition = player.position;
    }

    private void RefreshRoomState()
    {
        if (player == null) return;

        Vector2Int gridCoord = CalculateGridCoord(player.position);
        RoomData matchData = GetRoomDataByCoord(gridCoord);

        if (matchData != null)
        {
            currentRoomData = matchData;
            if (!loadedRooms.ContainsKey(matchData.roomID))
            {
                SpawnRoom(matchData, CalculateRoomPosition(matchData));
            }
            UpdateNeighborPreload(matchData);
            SyncCameraToPlayer();
        }
    }

    public void SetRestartPositionToCurrentDoor()
    {
        // 현재 방에 들어왔을 때의 위치(문 앞)를 다음 씬에 넘겨줄 정적 변수에 저장
        restartPointOverride = lastSafeEntryPosition;
    }

    // ========================================================================
    // 2. 외부 호출 API (Public Methods - 기존 코드 호환성 유지)
    // ========================================================================

    public void InitializeFirstRoom(RoomData startRoom, Vector3 position)
    {
        if (currentRoomData != null) return; // 이미 초기화됨

        currentRoomData = startRoom;
        SpawnRoom(startRoom, position);
        UpdateNeighborPreload(startRoom);

        // ★ 재시작 시 카메라가 엉뚱한 곳에 있는 문제 방지
        if (mainCamera != null)
        {
            mainCamera.transform.position = new Vector3(position.x, position.y, -10f);
        }

        StartCoroutine(StartSpawnProtection());
    }

    public void RequestMove(Vector2 direction, RoomData nextRoom, float distanceOverride = 0f)
    {
        if (isTransitioning || (BossManager.Instance != null && BossManager.Instance.IsBossActive)) return;
        if (isCoolingDown) return;

        if (!loadedRooms.ContainsKey(nextRoom.roomID))
        {
            SpawnRoom(nextRoom, CalculateRoomPosition(nextRoom));
        }

        StartCoroutine(TransitionRoutine(direction, nextRoom, distanceOverride));
    }

    public void SyncCameraToPlayer()
    {
        if (player == null) return;
        if (mainCamera == null) mainCamera = Camera.main;

        float targetX = Mathf.Round(player.position.x / gridWidth) * gridWidth;
        float targetY = Mathf.Round(player.position.y / gridHeight) * gridHeight;

        mainCamera.transform.position = new Vector3(targetX, targetY, -10f);
    }

    // 기존 함수 유지 (내부 구현만 변경)
    public void UpdateRoomStateAfterTeleport()
    {
        RefreshRoomState();
    }

    // ========================================================================
    // 3. 내부 로직 및 헬퍼 (Internal Logic & Helpers)
    // ========================================================================

    private IEnumerator TransitionRoutine(Vector2 direction, RoomData nextRoom, float distanceOverride)
    {
        isTransitioning = true;
        isCoolingDown = true;
        SetPlayerInput(false);
        RefreshTargetRoom(nextRoom);

        // ... (카메라/플레이어 이동 로직 기존과 동일) ...
        Vector3 startCameraPos = mainCamera.transform.position;
        Vector3 startPlayerPos = player.position;
        float moveDistance = (distanceOverride > 0) ? distanceOverride : (direction.x != 0 ? gridWidth : gridHeight);
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

        // ★ [핵심 추가] 이동이 완전히 끝난 후, 이 위치(문 앞)를 '안전한 위치'로 갱신
        lastSafeEntryPosition = player.position;

        if (loadedRooms.TryGetValue(nextRoom.roomID, out GameObject roomObj))
        {
            var ending = roomObj.GetComponentInChildren<EndingEvent>(true);
            if (ending != null) ending.PlayEnding();
        }

        yield return new WaitForSeconds(0.3f); // 물리 엔진이 안정화될 충분한 시간
        isTransitioning = false; // 이제서야 다음 이동 허용
        isCoolingDown = false;
    }

    // Helper: 월드 좌표 -> 그리드 좌표 변환
    private Vector2Int CalculateGridCoord(Vector3 worldPos)
    {
        int gridX = Mathf.RoundToInt(worldPos.x / gridWidth);
        int gridY = Mathf.RoundToInt(worldPos.y / gridHeight);
        return new Vector2Int(gridX, gridY);
    }

    // Helper: 그리드 좌표 -> RoomData 찾기
    private RoomData GetRoomDataByCoord(Vector2Int coord)
    {
        foreach (var data in allMapRooms)
        {
            if (data.roomCoord == coord) return data;
        }
        return null;
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
        HashSet<string> neighborsToKeep = new HashSet<string> { current.roomID };
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

    private IEnumerator StartSpawnProtection()
    {
        isCoolingDown = true;
        yield return new WaitForSeconds(0.5f);
        isCoolingDown = false;
    }
}