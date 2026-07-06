#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
        Debug.Log("[QA] BugTestConsole 활성 — F4 시각화 / F5 보스처치 / F6~F9 방이동. 자동 감시: 몹 셀 이탈, 보스전 문 개방");
    }

    private void Update()
    {
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
