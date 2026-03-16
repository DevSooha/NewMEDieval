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
    // ??[?꾩닔] 0�?�???�옉 �?????�???좊떦??二쇱�?? ??�쑝�??�ъ뒪?몄쓽 �?踰덉?�瑜??????�땲??
    public RoomData startRoomData;
    public List<RoomData> allMapRooms = new List<RoomData>();

    [Header("Debug / Stability")]
    public bool debugLogs = true;

    [Tooltip("true�?硫??�쭊 諛⑹??Destroy??? ??��?SetActive(false) 泥섎???�땲??")]
    public bool deactivateFarRoomsInsteadOfDestroy = true;

    public RoomData currentRoomData;
    private Dictionary<string, GameObject> loadedRooms = new Dictionary<string, GameObject>();
    private Dictionary<string, Transform> roomSpawnPoints = new Dictionary<string, Transform>();
    private bool isCoolingDown = false;
    private bool isTransitioning = false;

    public bool CanProcessMoveRequest => !isTransitioning && !isCoolingDown && (BossManager.Instance == null || !BossManager.Instance.IsBossActive);

    private Vector3 lastSafeEntryPosition;

    // ???�??꾩뿉??'??諛⑹? ???꾩튂'??湲곗�??�꽌 roomCoord ??�닔/以묐???�줈 ?명븳 0,0 ??�룿/寃�?�??以꾩??
    private Dictionary<string, Vector3> runtimeRoomPositions = new Dictionary<string, Vector3>();

    private const float overlapEpsilon = 0.1f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        loadedRooms = new Dictionary<string, GameObject>();
        roomSpawnPoints = new Dictionary<string, Transform>();
        runtimeRoomPositions = new Dictionary<string, Vector3>();
    }

    void Start()
    {
        if (UIManager.Instance != null)
        {
            StartCoroutine(UIManager.Instance.FadeIn(0.5f));
        }

        loadedRooms.Clear();
        roomSpawnPoints.Clear();
        runtimeRoomPositions.Clear();

        EnsureMainCamera();

        // 1. ???��??�뼱 李몄???뺣낫
        ResolvePlayerReference(activateIfFoundViaTag: true);

        // 2. 留뚯�?李몄?�媛? ??�굅??null), 李몄???媛앹껜媛? ???��???곹깭(MissingReference)??�㈃?
        // (Unity?�?�� 二쎌? 媛앹�??null 泥댄�??true??諛섑????�? ?뺤떎??�쾶 ??�린 ?꾪빐)
        

        if (player == null)
        {
            Debug.LogError("[RoomManager] 移섎�????�쪟: Player??李얠??????�뒿??�떎!");
            return;
        }
        if (mainCamera == null)
        {
            Debug.LogError("[RoomManager] Camera reference is missing and Camera.main was not found.");
            return;
        }

        // =========================================================
        // ??[???�� ??�━ ?�꾧�? : ?????vs ??�뼱??�린 vs ??寃뚯??
        // =========================================================

        // 1. ?????Restart) ?곗씠?�? ??�뒗媛? (二쎌�????�떆 ??�옉??寃쎌??
        if (restartPointOverride.HasValue)
        {
            DLog("??????붿껌 媛먯?! 吏곸??�??꾩튂�?蹂듦???�땲??");

            // ?꾩튂 媛뺤????�젙
            player.position = restartPointOverride.Value;

            // ?곗씠?????????�덇�??(??�쓬踰덉�??뺤긽 濡쒕�???�룄�?
            restartPointOverride = null;

            StartCoroutine(RestoreRoutine());
        }
        // 2. ???λ�??꾩튂媛 ??�뒗媛? (寃뚯???�먮????寃쎌??
        else if (Player.Instance.HasSavedPosition)
        {
            DLog("???λ�??꾩튂�?蹂듦???�땲??");
            StartCoroutine(RestoreRoutine());
        }
        // 3. ??泥섏????�옉
        else
        {
            DLog("??寃뚯?????�옉??�땲??");
            StartNewGame();
        }
    }

    // ========================================================================
    // 1. ?�덇�??�?蹂듦??濡쒖�?(Logic: Initialization & Restoration)
    // ========================================================================

    // [湲곕?? ??寃뚯????�옉 (?꾩튂 0,0 ?�덇�??�?�?�?濡쒕�?
    private void StartNewGame()
    {
        RoomData initialData = startRoomData != null ? startRoomData : (allMapRooms.Count > 0 ? allMapRooms[0] : null);

        Vector3 startPos = initialData != null ? CalculateRoomPosition(initialData) : Vector3.zero;

        player.position = startPos;
        lastSafeEntryPosition = player.position;

        ZeroPlayerVelocity();

        if (initialData != null)
        {
            // ????�옉 諛⑹? "position ?몄옄" 湲곗???�줈 ?�????꾩튂 ?뺤젙
            InitializeFirstRoom(initialData, startPos);
        }
    }

    // ??[??�젙] �?蹂듦???�붾�??(??�쟾??媛뺥??踰꾩??
    IEnumerator RestoreRoutine()
    {
        // �����ϸ� ��� ó���ϰ�, �ʿ� �� �� �����Ӹ� ���
        // 1. ??濡쒕�?吏곹??媛앹�??�씠 ??�꽦/???��(?뺣━)??�뒗 ?�쇱????꾪빐 1?꾨젅????�?
        // ================================================================
        // ??[???�� ??�젙] 李몄????뿰寃?(Re-assign)
        // ??湲고�????�븞, Start?�?�� ??�븯??player 蹂??? ???��??��???????�뒿??�떎.
        // ?곕씪????�븘??? '吏꾩�? ?�????몄뒪??�뒪�?蹂??? 媛깆???�땲??
        // ================================================================
        ResolvePlayerReference(activateIfFoundViaTag: false);

        // Player�� ���� �غ���� �ʾҴٸ� �� �����Ӹ� ����ϰ� ��õ� (�ּ� ���)
        if (player == null)
        {
            yield return null;

            ResolvePlayerReference(activateIfFoundViaTag: false);
        }
        // 2. ??뿰寃???�룄 ?꾩뿉????�쑝�?吏꾩�??�?�� 泥섎??
        if (player == null)
        {
            Debug.LogError("[RoomManager] 移섎�????�쪟: ??濡쒕�???Player媛 ?꾩쟾?????��???��??�떎.");
            yield break;
        }

        // 3. ???��??�뼱媛 二쎌?�硫?�꽌 ?�쇱�??�쓣 ????�쑝誘�?媛뺤????�꽦??
        player.gameObject.SetActive(true);

        // 4. ?곹깭 蹂듦????�뻾
        RefreshRoomState();

        // 5. 蹂듦????꾩튂??'??�쟾???꾩튂'�?湲곗�?
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

            Vector3 intendedPos = GetIntendedRoomPosition(matchData, null, Vector2.zero, 0f);

            if (!loadedRooms.ContainsKey(matchData.roomID))
            {
                DLog($"RefreshRoomState: spawn current [{matchData.roomID}] gridCoord={gridCoord} intendedPos={intendedPos} roomCoord=({matchData.roomCoord.x},{matchData.roomCoord.y})");
                SpawnRoom(matchData, intendedPos);
            }
            else
            {
                // ?뱀???�쇱�??�쑝�??�쒖�?
                loadedRooms[matchData.roomID].SetActive(true);
            }

            UpdateNeighborPreload(matchData);
            SyncCameraToPlayer();
        }
        else
        {
            DWarn($"RefreshRoomState: No RoomData matched for playerPos={player.position} gridCoord={gridCoord}. (allMapRooms??roomCoord媛 ??�젣 諛곗??? ?�덉?�移?�븷 ????�쓬)");
        }
    }

    public void SetRestartPositionToCurrentDoor()
    {
        // ?꾩옱 諛⑹�???�뼱?붿쓣 ???�� ?꾩튂(????????�쓬 ??�???�꺼�??뺤쟻 蹂??�뿉 ????
        restartPointOverride = lastSafeEntryPosition;
    }

    // ========================================================================
    // 2. ?�? ?몄텧 API (Public Methods - 湲곗???�붾�??명솚???�?)
    // ========================================================================

    public void InitializeFirstRoom(RoomData startRoom, Vector3 position)
    {
        if (currentRoomData != null) return; // ??�? ?�덇�?붾맖

        currentRoomData = startRoom;
        if (SoundManager.instance != null && currentRoomData != null)
     {
        SoundManager.instance.OnSceneLoaded(currentRoomData);
     }

        // ????�옉 �??꾩튂???�???湲곗???�줈 ?뺤젙 (roomCoord媛 ??�닔??�???�옉?? ??�젙)
        runtimeRoomPositions[startRoom.roomID] = position;

        SpawnRoom(startRoom, position);
        UpdateNeighborPreload(startRoom);

        // ?????????移�?�??? ??�슧???�녹�???�뒗 ?�몄??諛⑹?
        if (mainCamera != null)
        {
            mainCamera.transform.position = new Vector3(position.x, position.y, -10f);
        }

        StartCoroutine(StartSpawnProtection());
    }

    public void RequestMove(Vector2 direction, RoomData nextRoom, float distanceOverride = 0f)
    {
        bool hasActiveBossInScene = HasActiveBossInScene();

        // Fail-safe: clear stale boss-lock state if no active boss object exists.
        if (BossManager.Instance != null && BossManager.Instance.IsBossActive)
        {
            if (!hasActiveBossInScene)
            {
                Debug.LogWarning("[RoomManager] Boss lock was active without an active Boss-tag object. Auto-ending boss battle.");
                BossManager.Instance.EndBossBattle();
            }
        }

        if (isTransitioning || (BossManager.Instance != null && BossManager.Instance.IsBossActive) || hasActiveBossInScene) return;
        if (isCoolingDown) return;
        if (nextRoom == null) return;
        EnsureMainCamera();
        if (mainCamera == null) return;

        // ???꾩옱 �??�????꾩튂 蹂댁??
        EnsureRuntimePositionForCurrentRoom();

        // ????�쓬 諛⑹? roomCoord ????"?꾩옱 �??꾩튂 + 諛⑺�???�줈 ?곗꽑 諛곗??
        Vector3 intendedNextPos = GetIntendedRoomPosition(nextRoom, currentRoomData, direction, distanceOverride);

        if (!loadedRooms.ContainsKey(nextRoom.roomID))
        {
            DLog($"RequestMove: [{currentRoomData.roomID}] -> [{nextRoom.roomID}] dir={direction} distOverride={distanceOverride} intendedNextPos={intendedNextPos} nextRoomCoord=({nextRoom.roomCoord.x},{nextRoom.roomCoord.y})");
            SpawnRoom(nextRoom, intendedNextPos);
        }
        else
        {
            // ??�? ??�룿??�엳?붾뜲 ?�쇱�??�쓣 ????�쑝???�쒖�?
            loadedRooms[nextRoom.roomID].SetActive(true);

            // ?�????꾩튂 �?��?�媛? ??�쑝�??�붽?
            if (!runtimeRoomPositions.ContainsKey(nextRoom.roomID))
                runtimeRoomPositions[nextRoom.roomID] = loadedRooms[nextRoom.roomID].transform.position;

            DLog($"RequestMove: nextRoom already loaded [{nextRoom.roomID}] at {loadedRooms[nextRoom.roomID].transform.position}");
        }

        StartCoroutine(TransitionRoutine(direction, nextRoom, distanceOverride));
    }

    private bool HasActiveBossInScene()
    {
        GameObject[] activeBosses = GameObject.FindGameObjectsWithTag("Boss");
        if (activeBosses != null && activeBosses.Length > 0)
        {
            return true;
        }

        BossCombatBase[] activeBossCombats = FindObjectsByType<BossCombatBase>(FindObjectsSortMode.None);
        return activeBossCombats != null && activeBossCombats.Length > 0;
    }

    public void SyncCameraToPlayer()
    {
        if (player == null) return;
        EnsureMainCamera();

        float targetX = Mathf.Round(player.position.x / gridWidth) * gridWidth;
        float targetY = Mathf.Round(player.position.y / gridHeight) * gridHeight;

        mainCamera.transform.position = new Vector3(targetX, targetY, -10f);
    }

    // 湲곗????�닔 ?�? (??�? ?�ы쁽�?蹂�?
    public void UpdateRoomStateAfterTeleport()
    {
        RefreshRoomState();
    }

    public Transform GetSpawnPointForCurrentRoom(string spawnPointName)
    {
        if (currentRoomData == null) return null;
        return GetSpawnPointForRoom(currentRoomData.roomID, spawnPointName);
    }

    public Transform GetSpawnPointForRoom(string roomID, string spawnPointName)
    {
        if (string.IsNullOrEmpty(roomID) || string.IsNullOrEmpty(spawnPointName)) return null;

        if (roomSpawnPoints.TryGetValue(roomID, out var cached) && cached != null)
            return cached;

        if (loadedRooms.TryGetValue(roomID, out var roomObj) && roomObj != null)
        {
            var found = FindSpawnPointInRoom(roomObj.transform, spawnPointName);
            if (found != null) roomSpawnPoints[roomID] = found;
            return found;
        }

        return null;
    }

    private void CacheRoomSpawnPoint(GameObject roomObj, string roomID)
    {
        if (roomObj == null || string.IsNullOrEmpty(roomID)) return;

        var found = FindSpawnPointInRoom(roomObj.transform, "PlayerSpawnPoint");
        if (found != null) roomSpawnPoints[roomID] = found;
    }

    private Transform FindSpawnPointInRoom(Transform root, string spawnPointName)
    {
        if (root == null) return null;

        var stack = new Stack<Transform>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var t = stack.Pop();
            if (t == null) continue;

            if (t.name == spawnPointName)
                return t;

            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (child != null) stack.Push(child);
            }
        }

        return null;
    }

    // ========================================================================
    // 3. ??�? 濡쒖�?�?????(Internal Logic & Helpers)
    // ========================================================================

    private IEnumerator TransitionRoutine(Vector2 direction, RoomData nextRoom, float distanceOverride)
    {
        EnsureMainCamera();
        if (mainCamera == null) yield break;

        isTransitioning = true;
        isCoolingDown = true;
        SetPlayerInput(false);

        // ????�??�몄????�? 濡쒕�??�뼱 ??�떎�? ?�ы봽??�떆?????�� "?꾩옱 ???λ�??꾩튂"??蹂댁??
        RefreshTargetRoom(nextRoom);

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

        ZeroPlayerVelocity();

        currentRoomData = nextRoom;

        if (SoundManager.instance != null && currentRoomData != null)
    {
        SoundManager.instance.OnSceneLoaded(currentRoomData);
    }

        // ????��??꾨즺 ???�????꾩튂?? ??�뻾 留ㅼ�???�?�???�툕??�듃 ?꾩튂????�꽑??�줈 ????        // (媛???? ??�Ⅸ ??�젙 ????�뒗 寃쎌?? �???�툕??�듃??移�?�??�줈 ?뺤젹??�㈃ ?몃뱶 ?�쒕???�?�媛? ??�??
        Vector3 settledRoomPos = new Vector3(targetCameraPos.x, targetCameraPos.y, 0f);
        if (loadedRooms.TryGetValue(currentRoomData.roomID, out var curObj) && curObj != null)
        {
            Vector3 objPos = curObj.transform.position;
            if (Vector3.Distance(objPos, settledRoomPos) > 0.01f)
            {
                DWarn($"Room object position mismatch: [{currentRoomData.roomID}] objPos={objPos} intended={settledRoomPos}. Aligning camera/player to room object.");
                Vector3 delta = objPos - settledRoomPos;
                mainCamera.transform.position += delta;
                player.position += delta;
                settledRoomPos = objPos;

                ZeroPlayerVelocity();
            }
        }
        runtimeRoomPositions[currentRoomData.roomID] = settledRoomPos;

        UpdateNeighborPreload(nextRoom);
        SetPlayerInput(true);

        // ??[???�� ?�붽?] ??��???꾩쟾????�궃 ?? ???꾩튂(??????'??�쟾???꾩튂'�?媛깆??
        lastSafeEntryPosition = player.position;

        if (loadedRooms.TryGetValue(nextRoom.roomID, out GameObject roomObj))
        {
            var ending = roomObj.GetComponentInChildren<EndingEvent>(true);
            if (ending != null) ending.PlayEnding();
        }

        yield return new WaitForSeconds(0.3f); // ?�쇰???붿쭊????�젙?붾맆 ?�⑸?????�컙
        isTransitioning = false; // ??�젣??�빞 ??�쓬 ??��???�슜
        isCoolingDown = false;
    }

    // Helper: ?붾뱶 ?�뚰�?-> 洹몃????�뚰�?蹂??
    private Vector2Int CalculateGridCoord(Vector3 worldPos)
    {
        int gridX = Mathf.RoundToInt(worldPos.x / gridWidth);
        int gridY = Mathf.RoundToInt(worldPos.y / gridHeight);
        return new Vector2Int(gridX, gridY);
    }

    // Helper: 洹몃????�뚰�?-> RoomData 李얘�?
    private RoomData GetRoomDataByCoord(Vector2Int coord)
    {
        foreach (var data in allMapRooms)
        {
            int roomX = Mathf.RoundToInt(data.roomCoord.x);
            int roomY = Mathf.RoundToInt(data.roomCoord.y);
            if (roomX == coord.x && roomY == coord.y) return data;
        }
        return null;
    }

    private void RefreshTargetRoom(RoomData targetRoom)
    {
        if (targetRoom == null) return;

        if (loadedRooms.ContainsKey(targetRoom.roomID))
        {
            GameObject oldRoom = loadedRooms[targetRoom.roomID];
            Vector3 roomPos = oldRoom.transform.position;

            Destroy(oldRoom);
            loadedRooms.Remove(targetRoom.roomID);
            roomSpawnPoints.Remove(targetRoom.roomID);

            // ???�ы봽??�떆??��???�씪 ?꾩튂�??????+ ?�????꾩튂???�?
            runtimeRoomPositions[targetRoom.roomID] = roomPos;

            DLog($"RefreshTargetRoom: destroyed & respawn [{targetRoom.roomID}] at preservedPos={roomPos}");
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
        if (current == null) return;

        // ???꾩옱 �??�????꾩튂 蹂댁??(??�썐 ?�꾩�?湲곗?)
        if (!runtimeRoomPositions.ContainsKey(current.roomID))
        {
            Vector3 fallback = CalculateRoomPosition(current);
            runtimeRoomPositions[current.roomID] = fallback;
            DWarn($"UpdateNeighborPreload: current [{current.roomID}] runtimePos missing -> fallback to roomCoord pos={fallback}");
        }

        HashSet<string> neighborsToKeep = new HashSet<string> { current.roomID };

        // ??諛⑺�???�??�꽌 ??�썐??"?�??湲곕�??꾩튂"�???�룿
        (RoomData room, Vector2 dir)[] neighbors =
        {
            (current.north, Vector2.up),
            (current.south, Vector2.down),
            (current.east,  Vector2.right),
            (current.west,  Vector2.left),
        };

        foreach (var (neighbor, dir) in neighbors)
        {
            if (neighbor == null) continue;

            neighborsToKeep.Add(neighbor.roomID);

            if (!loadedRooms.ContainsKey(neighbor.roomID))
            {
                Vector3 intended = GetIntendedRoomPosition(neighbor, current, dir, 0f);
                DLog($"Preload neighbor: current=[{current.roomID}] -> neighbor=[{neighbor.roomID}] dir={dir} intendedPos={intended} neighborCoord=({neighbor.roomCoord.x},{neighbor.roomCoord.y})");
                SpawnRoom(neighbor, intended);
            }
            else
            {
                // ??�? 濡쒕�??諛⑹?�硫?keep ???곸씠???�쒖�?
                loadedRooms[neighbor.roomID].SetActive(true);

                if (!runtimeRoomPositions.ContainsKey(neighbor.roomID))
                    runtimeRoomPositions[neighbor.roomID] = loadedRooms[neighbor.roomID].transform.position;
            }
        }

        // ??硫??�쭊 �?泥섎?? Destroy or Deactivate
        List<string> roomsToHandle = new List<string>();
        foreach (var loadedID in loadedRooms.Keys)
        {
            if (!neighborsToKeep.Contains(loadedID))
                roomsToHandle.Add(loadedID);
        }

        foreach (var id in roomsToHandle)
        {
            if (!loadedRooms.TryGetValue(id, out var obj) || obj == null) continue;

            if (deactivateFarRoomsInsteadOfDestroy)
            {
                obj.SetActive(false);
                DLog($"Far room handled: Deactivate [{id}] pos={obj.transform.position}");
            }
            else
            {
                DLog($"Far room handled: Destroy [{id}] pos={obj.transform.position}");
                Destroy(obj);
                loadedRooms.Remove(id);
                roomSpawnPoints.Remove(id);
                // runtimeRoomPositions???�? (??�떆 ????媛숈? ?�?���???�룿??�쾶)
            }
        }

        // keep ????�??뱀???�쇱�??�쑝�??�쒖�?(以묎�????��??깊솕??�?���???�뒗 ?�??�뒪 諛⑹?)
        foreach (var id in neighborsToKeep)
        {
            if (loadedRooms.TryGetValue(id, out var obj) && obj != null && !obj.activeSelf)
            {
                obj.SetActive(true);
                DLog($"Keep room re-activated [{id}]");
            }
        }
    }

    private void SpawnRoom(RoomData data, Vector3 position)
    {
        if (data == null)
        {
            DWarn("SpawnRoom called with null RoomData");
            return;
        }

        // 以묐????�룿 諛⑹?
        if (loadedRooms.ContainsKey(data.roomID))
        {
            DWarn($"SpawnRoom skip: already loaded [{data.roomID}] at {loadedRooms[data.roomID].transform.position}");
            return;
        }

        // 寃�?�?泥댄�?(??�? ??�룿??諛⑷????�씪 ?꾩튂�?寃쎄??
        foreach (var kv in loadedRooms)
        {
            GameObject other = kv.Value;
            if (other == null) continue;

            if (Vector3.Distance(other.transform.position, position) < overlapEpsilon)
            {
                DWarn($"OVERLAP DETECTED: spawn [{data.roomID}] at {position} but [{kv.Key}] already at {other.transform.position}. (roomCoord 以묐??誘몄�??媛??");
                break;
            }
        }

        GameObject roomObj = Instantiate(data.roomPrefab, position, Quaternion.identity);
        roomObj.name = data.roomID;
        loadedRooms.Add(data.roomID, roomObj);
        CacheRoomSpawnPoint(roomObj, data.roomID);

        // ?�????꾩튂 ?뺤젙 ????
        runtimeRoomPositions[data.roomID] = position;

        bool coordLooksDefault = Mathf.Approximately(data.roomCoord.x, 0f) && Mathf.Approximately(data.roomCoord.y, 0f);
        if (coordLooksDefault && data != startRoomData)
        {
            DWarn($"Spawned [{data.roomID}] with roomCoord (0,0). pos={position} (??諛⑹??0,0????�뒗 ?�?��??????�쓬)");
        }
        else
        {
            DLog($"Spawned [{data.roomID}] coord=({data.roomCoord.x},{data.roomCoord.y}) pos={position}");
        }
    }

    private Vector3 CalculateRoomPosition(RoomData data)
    {
        return new Vector3(data.roomCoord.x * gridWidth, data.roomCoord.y * gridHeight, 0);
    }

    // ?????��: roomCoord蹂�???"?�???꾩옱�?諛⑺�?" 湲곕�???곗꽑??�꽌 ??�룄 ?꾩튂??留뚮�??
    private Vector3 GetIntendedRoomPosition(RoomData room, RoomData fromRoom, Vector2 fromDir, float distOverride)
    {
        if (room == null) return Vector3.zero;

        // 1) ?�???�?��?�媛? ??�쑝�?理쒖???
        if (runtimeRoomPositions.TryGetValue(room.roomID, out var cached))
            return cached;

        // 2) fromRoom????��? fromRoom???�????꾩튂媛 ??�쑝�??�??湲곕�??�줈 諛곗??
        if (fromRoom != null && runtimeRoomPositions.TryGetValue(fromRoom.roomID, out var fromPos))
        {
            float dist = (distOverride > 0f)
                ? distOverride
                : (fromDir.x != 0 ? gridWidth : gridHeight);

            Vector3 move = new Vector3(fromDir.x * dist, fromDir.y * dist, 0f);
            return fromPos + move;
        }

        // 3) fallback: roomCoord
        return CalculateRoomPosition(room);
    }

    private void EnsureRuntimePositionForCurrentRoom()
    {
        if (currentRoomData == null) return;

        if (runtimeRoomPositions.ContainsKey(currentRoomData.roomID))
            return;

        // 濡쒕�????�툕??�듃媛 ??�쑝�?�??꾩튂??�?��??
        if (loadedRooms.TryGetValue(currentRoomData.roomID, out var obj) && obj != null)
        {
            runtimeRoomPositions[currentRoomData.roomID] = obj.transform.position;
            DWarn($"EnsureRuntimePosition: cached current [{currentRoomData.roomID}] from GameObject pos={obj.transform.position}");
            return;
        }

        // ??�쑝�?roomCoord fallback
        Vector3 fallback = CalculateRoomPosition(currentRoomData);
        runtimeRoomPositions[currentRoomData.roomID] = fallback;
        DWarn($"EnsureRuntimePosition: cached current [{currentRoomData.roomID}] from roomCoord fallback pos={fallback}");
    }

    private void SetPlayerInput(bool active)
    {
        if (Player.Instance != null)
        {
            Player.Instance.SetCanMove(active);
        }
    }
    private void EnsureMainCamera()
    {
        if (mainCamera == null) mainCamera = Camera.main;
    }
    private void ResolvePlayerReference(bool activateIfFoundViaTag)
    {
        if (player == null && Player.Instance != null)
        {
            player = Player.Instance.transform;
        }
        if (player != null) return;
        GameObject foundPlayer = GameObject.FindGameObjectWithTag("Player");
        if (foundPlayer == null) return;
        player = foundPlayer.transform;
        if (activateIfFoundViaTag && !foundPlayer.activeSelf)
        {
            foundPlayer.SetActive(true);
        }
    }
    private void ZeroPlayerVelocity()
    {
        if (player != null && player.TryGetComponent<Rigidbody2D>(out var rb))
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private IEnumerator StartSpawnProtection()
    {
        isCoolingDown = true;
        yield return new WaitForSeconds(0.5f);
        isCoolingDown = false;
    }

    // -------------------------
    // Debug helpers
    // -------------------------
    private void DLog(string msg)
    {
        if (debugLogs) Debug.Log($"[RoomManager] {msg}");
    }

    private void DWarn(string msg)
    {
        if (debugLogs) Debug.LogWarning($"[RoomManager] {msg}");
    }
}

