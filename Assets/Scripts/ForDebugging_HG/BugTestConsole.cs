#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BUG-2~6 플레이테스트 보조 콘솔 (에디터/개발 빌드 전용).
/// 씬/프리팹 배치 없이 RuntimeInitializeOnLoadMethod로 자동 생성되며,
/// 릴리즈 빌드에서는 파일 전체가 컴파일에서 제외된다.
///
/// 조작:
///   F4      히트박스/투사체 콜라이더 시각화 토글 (빨강=정밀 히트박스, 청록=본체, 노랑=투사체)
///   F5      활성 보스 즉시 처치 (BossHealth.TakeDamage 공식 경로 — 페이즈/문 개방 체인 유지)
///   F6~F9   방 이동 북/남/서/동 (RoomManager.RequestMove 공식 경로 — 보스 잠금/쿨다운 검사 통과)
///
/// 자동 감시 (키 없음, 1초 폴링, 상태 변화 시 1회만 경고):
///   - 몬스터 방 셀 이탈 → BUG-3/4 회귀 의심 경고
///   - 보스전 중 열린 문(MapNode isTrigger=true) → BUG-2 회귀 의심 경고
/// </summary>
public class BugTestConsole : MonoBehaviour
{
    private static BugTestConsole instance;

    private const float watchInterval = 1f;
    private const float bossLockGraceSeconds = 1f; // 전투 시작 직후 isTrigger 전환 프레임 오탐 방지
    private const float cellEscapeToleranceSqr = 0.04f; // 0.2유닛 여유

    private bool colliderOverlay;
    private float nextWatchTime;
    private float bossActiveSince = -1f;
    private readonly HashSet<int> escapedMonsterIds = new HashSet<int>();
    private readonly HashSet<int> openDoorIds = new HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;

        GameObject go = new GameObject("BugTestConsole (DEV)");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<BugTestConsole>();
        Debug.Log("[QA] BugTestConsole 활성 — F1 연결/보스방 / F2 보스방 자동이동 / F4 시각화 / F5 보스처치 / F6~F9 방이동. 자동 감시: 몹 셀 이탈, 보스전 문 개방");
    }

    private Coroutine autoRouteRoutine;
    private int bossRoomCycleIndex;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1)) LogRoomConnections();
        if (Input.GetKeyDown(KeyCode.F2)) AutoRouteToNextBossRoom();
        if (Input.GetKeyDown(KeyCode.F3)) DamageActiveBosses();
        if (Input.GetKeyDown(KeyCode.F4)) colliderOverlay = !colliderOverlay;
        if (Input.GetKeyDown(KeyCode.F5)) KillActiveBosses();
        if (Input.GetKeyDown(KeyCode.F6)) MoveRoom(Vector2.up);
        if (Input.GetKeyDown(KeyCode.F7)) MoveRoom(Vector2.down);
        if (Input.GetKeyDown(KeyCode.F8)) MoveRoom(Vector2.left);
        if (Input.GetKeyDown(KeyCode.F9)) MoveRoom(Vector2.right);

        if (Time.unscaledTime >= nextWatchTime)
        {
            nextWatchTime = Time.unscaledTime + watchInterval;
            WatchMonsterCellEscape();
            WatchBossDoorLock();
            WatchPlayerPosition();
        }
    }

    // ── F3: 보스 강데미지 (페이즈 전환 테스트 — 즉사 아님) ──────────────────

    private void DamageActiveBosses()
    {
        BossHealth[] bosses = FindObjectsByType<BossHealth>(FindObjectsSortMode.None);
        if (bosses.Length == 0)
        {
            Debug.Log("[QA] 활성 보스 없음");
            return;
        }

        foreach (BossHealth boss in bosses)
        {
            if (boss == null || boss.CurrentHP <= 0) continue;

            if (boss.IsInvulnerable)
            {
                Debug.LogWarning($"[QA] {boss.bossName} 무적 상태 — 데미지 생략");
                continue;
            }

            // 공식 데미지 경로 사용 - 페이즈 핸들러(OnBossHpChanged)가 정상 동작한다.
            // maxHP의 25%씩 깎아 페이즈 경계를 단계적으로 넘겨볼 수 있게 한다.
            int damage = Mathf.Max(1, boss.MaxHP / 4);
            int before = boss.CurrentHP;
            boss.TakeDamage(damage, ElementType.None);
            Debug.Log($"[QA] 보스 강데미지: {boss.bossName} HP {before} -> {boss.CurrentHP} (요청 {damage})");
        }
    }

    // ── 자동 감시: 플레이어 비정상 위치 (방 셀 불일치 / 지면 밖) ────────────

    private bool playerPositionWarned;

    private void WatchPlayerPosition()
    {
        Player player = Player.Instance;
        RoomManager rm = RoomManager.Instance;
        if (player == null || !player.gameObject.activeInHierarchy || rm == null || rm.mainCamera == null)
        {
            playerPositionWarned = false;
            return;
        }

        // 전환/쿨다운/보스전 중에는 일시적 불일치가 정상이므로 감시하지 않는다
        if (!rm.CanProcessMoveRequest) return;

        // 카메라는 항상 현재 방 중심에 스냅되므로 플레이어 셀과의 비교로 갈라짐을 감지
        Vector2 playerPos = player.transform.position;
        Vector2 cameraPos = rm.mainCamera.transform.position;
        bool cellMismatch =
            Mathf.RoundToInt(playerPos.x / rm.gridWidth) != Mathf.RoundToInt(cameraPos.x / rm.gridWidth) ||
            Mathf.RoundToInt(playerPos.y / rm.gridHeight) != Mathf.RoundToInt(cameraPos.y / rm.gridHeight);

        bool offGround = !player.IsOnWalkableGround();

        if (cellMismatch || offGround)
        {
            if (!playerPositionWarned)
            {
                playerPositionWarned = true;
                Debug.LogWarning($"[QA] 플레이어 비정상 위치 감지: pos={playerPos} 셀불일치={cellMismatch} 지면밖={offGround} (전환 갈라짐/벽 바깥 갇힘 의심)");
            }
        }
        else
        {
            playerPositionWarned = false;
        }
    }

    // ── F5: 보스 즉시 처치 ──────────────────────────────────────────────────

    private void KillActiveBosses()
    {
        BossHealth[] bosses = FindObjectsByType<BossHealth>(FindObjectsSortMode.None);
        if (bosses.Length == 0)
        {
            Debug.Log("[QA] 활성 보스 없음");
            return;
        }

        foreach (BossHealth boss in bosses)
        {
            if (boss == null || boss.CurrentHP <= 0) continue;

            if (boss.IsInvulnerable)
            {
                Debug.LogWarning($"[QA] {boss.bossName} 무적 상태 — 처치 생략 (페이즈 연출 중일 수 있음)");
                continue;
            }

            int before = boss.CurrentHP;
            // 공식 데미지 경로 사용: 페이즈 핸들러/Die/문 개방 체인이 그대로 동작한다.
            // 원소 배율로 데미지가 깎여도 확실히 0 이하가 되도록 여유 배수를 준다.
            boss.TakeDamage(Mathf.Max(1, boss.CurrentHP) * 10, ElementType.None);
            Debug.Log($"[QA] 보스 처치: {boss.bossName} HP {before} -> {boss.CurrentHP}");
        }
    }

    // ── F6~F9: 방 이동 보조 ────────────────────────────────────────────────

    private void MoveRoom(Vector2 direction)
    {
        RoomManager rm = RoomManager.Instance;
        if (rm == null || rm.currentRoomData == null)
        {
            Debug.Log("[QA] RoomManager 또는 currentRoomData 없음 — 방 이동 불가");
            return;
        }

        RoomData next =
            direction == Vector2.up ? rm.currentRoomData.north :
            direction == Vector2.down ? rm.currentRoomData.south :
            direction == Vector2.left ? rm.currentRoomData.west :
            rm.currentRoomData.east;

        if (next == null)
        {
            Debug.Log($"[QA] [{rm.currentRoomData.roomID}] {direction} 방향 이웃 없음");
            return;
        }

        // 공식 전환 경로 — 보스 잠금/전환 중/쿨다운이면 내부에서 거부된다.
        rm.RequestMove(direction, next);
    }

    // ── F1: 현재 방 연결 + 보스방 목록 출력 ────────────────────────────────

    private void LogRoomConnections()
    {
        RoomManager rm = RoomManager.Instance;
        if (rm == null || rm.currentRoomData == null)
        {
            Debug.Log("[QA] RoomManager 또는 currentRoomData 없음");
            return;
        }

        RoomData cur = rm.currentRoomData;
        Debug.Log($"[QA] 현재 방 [{cur.roomID}] coord=({cur.roomCoord.x},{cur.roomCoord.y}) | " +
                  $"북={RoomLabel(cur.north)} 남={RoomLabel(cur.south)} 동={RoomLabel(cur.east)} 서={RoomLabel(cur.west)}");

        List<RoomData> bossRooms = GetBossRooms(rm);
        if (bossRooms.Count == 0)
        {
            Debug.Log("[QA] BossBattleTrigger를 가진 보스방 없음");
            return;
        }

        var sb = new System.Text.StringBuilder("[QA] 보스방 목록: ");
        foreach (RoomData room in bossRooms)
        {
            bool reachable = room == cur || FindPath(cur, room) != null;
            sb.Append($"{room.roomID}({room.roomCoord.x},{room.roomCoord.y}){(reachable ? "" : "[경로없음]")}  ");
        }
        Debug.Log(sb.ToString());
    }

    private static string RoomLabel(RoomData room) => room != null ? room.roomID : "-";

    private static List<RoomData> GetBossRooms(RoomManager rm)
    {
        List<RoomData> result = new List<RoomData>();
        foreach (RoomData room in rm.allMapRooms)
        {
            if (room == null || room.roomPrefab == null || result.Contains(room)) continue;
            if (room.roomPrefab.GetComponentInChildren<BossBattleTrigger>(true) != null)
            {
                result.Add(room);
            }
        }
        return result;
    }

    // ── F2: 다음 보스방으로 자동 이동 (공식 RequestMove 다단 경로) ──────────
    // 임의 좌표 텔레포트는 RoomManager 상태/트리거/세이브 위치와 어긋나
    // 카메라-플레이어 갈라짐을 재발시킬 수 있어 넣지 않는다.

    private void AutoRouteToNextBossRoom()
    {
        RoomManager rm = RoomManager.Instance;
        if (rm == null || rm.currentRoomData == null)
        {
            Debug.Log("[QA] RoomManager 또는 currentRoomData 없음");
            return;
        }

        List<RoomData> bossRooms = GetBossRooms(rm);
        if (bossRooms.Count == 0)
        {
            Debug.Log("[QA] 보스방 없음");
            return;
        }

        RoomData target = bossRooms[bossRoomCycleIndex % bossRooms.Count];
        bossRoomCycleIndex++;

        if (target == rm.currentRoomData)
        {
            Debug.Log($"[QA] 이미 보스방 [{target.roomID}] — F2 다시 누르면 다음 보스방");
            return;
        }

        List<(Vector2 dir, RoomData room)> path = FindPath(rm.currentRoomData, target);
        if (path == null)
        {
            Debug.LogWarning($"[QA] [{target.roomID}]까지 이웃 그래프 경로 없음 — F2 다시 누르면 다음 보스방");
            return;
        }

        if (autoRouteRoutine != null) StopCoroutine(autoRouteRoutine);
        Debug.Log($"[QA] 보스방 [{target.roomID}]로 자동 이동 시작 ({path.Count}단계)");
        autoRouteRoutine = StartCoroutine(AutoRouteRoutine(rm, path, target));
    }

    private IEnumerator AutoRouteRoutine(RoomManager rm, List<(Vector2 dir, RoomData room)> path, RoomData target)
    {
        foreach ((Vector2 dir, RoomData next) in path)
        {
            float waitUntil = Time.unscaledTime + 8f;
            while (!rm.CanProcessMoveRequest)
            {
                if (BossManager.Instance != null && BossManager.Instance.IsBossActive)
                {
                    Debug.LogWarning("[QA] 자동 이동 중단: 보스전 진행 중");
                    yield break;
                }
                if (Time.unscaledTime > waitUntil)
                {
                    Debug.LogWarning("[QA] 자동 이동 중단: 전환 대기 시간 초과");
                    yield break;
                }
                yield return null;
            }

            rm.RequestMove(dir, next);
            yield return null;

            waitUntil = Time.unscaledTime + 8f;
            while (!rm.CanProcessMoveRequest && Time.unscaledTime < waitUntil)
            {
                yield return null;
            }

            if (rm.currentRoomData != next)
            {
                Debug.LogWarning($"[QA] 자동 이동 중단: [{next.roomID}] 진입 실패 (전환 롤백/차단 의심)");
                yield break;
            }
        }

        Debug.Log($"[QA] 보스방 [{target.roomID}] 도착");
        autoRouteRoutine = null;
    }

    // 이웃 그래프 BFS 경로 탐색 (방향 포함). 경로 없으면 null.
    private static List<(Vector2 dir, RoomData room)> FindPath(RoomData start, RoomData goal)
    {
        var cameFrom = new Dictionary<RoomData, (RoomData from, Vector2 dir)>();
        var seen = new HashSet<RoomData> { start };
        var queue = new Queue<RoomData>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            RoomData cur = queue.Dequeue();
            if (cur == goal) break;

            var neighbors = new (RoomData room, Vector2 dir)[]
            {
                (cur.north, Vector2.up),
                (cur.south, Vector2.down),
                (cur.east,  Vector2.right),
                (cur.west,  Vector2.left),
            };

            foreach ((RoomData room, Vector2 dir) in neighbors)
            {
                if (room == null || !seen.Add(room)) continue;
                cameFrom[room] = (cur, dir);
                queue.Enqueue(room);
            }
        }

        if (!cameFrom.ContainsKey(goal)) return null;

        var path = new List<(Vector2 dir, RoomData room)>();
        RoomData node = goal;
        while (node != start)
        {
            (RoomData from, Vector2 dir) = cameFrom[node];
            path.Add((dir, node));
            node = from;
        }
        path.Reverse();
        return path;
    }

    // ── 자동 감시: 몬스터 방 셀 이탈 (BUG-3/4 회귀) ─────────────────────────

    private void WatchMonsterCellEscape()
    {
        EnemyCombat[] enemies = FindObjectsByType<EnemyCombat>(FindObjectsSortMode.None);
        foreach (EnemyCombat enemy in enemies)
        {
            if (enemy == null || enemy.IsDead) continue;

            Vector2 pos = enemy.transform.position;
            Vector2 clamped = EnemyStatusController.ClampToRoomCell(pos, pos);
            bool escaped = (clamped - pos).sqrMagnitude > cellEscapeToleranceSqr;
            int id = enemy.GetInstanceID();

            if (escaped)
            {
                if (escapedMonsterIds.Add(id))
                    Debug.LogWarning($"[QA] 몬스터 방 셀 이탈 감지: {enemy.name} pos={pos} (BUG-3/4 회귀 의심)");
            }
            else
            {
                escapedMonsterIds.Remove(id);
            }
        }
    }

    // ── 자동 감시: 보스전 중 열린 문 (BUG-2 회귀) ───────────────────────────

    private void WatchBossDoorLock()
    {
        bool bossActive = BossManager.Instance != null && BossManager.Instance.IsBossActive;
        if (!bossActive)
        {
            bossActiveSince = -1f;
            openDoorIds.Clear();
            return;
        }

        if (bossActiveSince < 0f) bossActiveSince = Time.unscaledTime;
        if (Time.unscaledTime - bossActiveSince < bossLockGraceSeconds) return;

        MapNode[] doors = FindObjectsByType<MapNode>(FindObjectsSortMode.None);
        foreach (MapNode door in doors)
        {
            if (door == null || door.nextRoom == null) continue; // nextRoom 없는 노드는 항상 solid 벽

            BoxCollider2D col = door.GetComponent<BoxCollider2D>();
            if (col == null) continue;

            int id = door.GetInstanceID();
            if (col.isTrigger)
            {
                if (openDoorIds.Add(id))
                    Debug.LogWarning($"[QA] 보스전 중 열린 문 감지: {door.name} pos={door.transform.position} (BUG-2 회귀 의심)");
            }
            else
            {
                openDoorIds.Remove(id);
            }
        }
    }

    // ── F4: 히트박스/투사체 콜라이더 시각화 (BUG-5 스침 판정 확인) ──────────

    private void OnGUI()
    {
        if (!colliderOverlay) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        if (Player.Instance != null)
        {
            foreach (Collider2D col in Player.Instance.GetComponentsInChildren<Collider2D>(false))
            {
                if (col == null || !col.enabled) continue;
                bool precise = col.GetComponent<CombatTargetHitbox>() != null;
                DrawWorldBounds(cam, col.bounds, precise ? Color.red : Color.cyan);
            }
        }

        foreach (BossProjectile projectile in FindObjectsByType<BossProjectile>(FindObjectsSortMode.None))
        {
            DrawColliderBounds(cam, projectile, Color.yellow);
        }

        foreach (StainedSwordProjectile projectile in FindObjectsByType<StainedSwordProjectile>(FindObjectsSortMode.None))
        {
            DrawColliderBounds(cam, projectile, Color.yellow);
        }
    }

    private static void DrawColliderBounds(Camera cam, Component owner, Color color)
    {
        if (owner == null) return;
        Collider2D col = owner.GetComponent<Collider2D>();
        if (col == null || !col.enabled) return;
        DrawWorldBounds(cam, col.bounds, color);
    }

    private static void DrawWorldBounds(Camera cam, Bounds bounds, Color color)
    {
        Vector3 min = cam.WorldToScreenPoint(bounds.min);
        Vector3 max = cam.WorldToScreenPoint(bounds.max);

        float x = Mathf.Min(min.x, max.x);
        float y = Screen.height - Mathf.Max(min.y, max.y); // GUI 좌표는 상단 원점
        float w = Mathf.Abs(max.x - min.x);
        float h = Mathf.Abs(max.y - min.y);

        DrawRectOutline(new Rect(x, y, w, h), color, 2f);
    }

    private static void DrawRectOutline(Rect rect, Color color, float thickness)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
#endif
