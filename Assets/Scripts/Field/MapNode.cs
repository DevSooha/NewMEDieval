using UnityEngine;
using TMPro;

public class MapNode : MonoBehaviour
{
    public enum Direction { North, South, East, West }

    [Header("방 이동 설정")]
    public Direction moveDirection;
    public RoomData nextRoom;
    public float overrideDistance = 0f;

    [Header("차단 메시지 설정")]
    public string defaultBlockMessage = "The path is blocked.";
    public string lockedMessage = "You cannot flee!";

    private BoxCollider2D myCollider;

    private void Awake()
    {
        myCollider = GetComponent<BoxCollider2D>();
    }

    // ★ [핵심] 매 프레임 문 상태를 결정합니다.
    private void Update()
    {
        if (myCollider == null) return;

        // 1. 연결된 방이 아예 없으면 -> 무조건 벽
        if (nextRoom == null)
        {
            myCollider.isTrigger = false;
        }
        // 2. 보스전 중이면 -> 무조건 벽 (딱딱하게 막힘)
        else if (BossManager.Instance != null && BossManager.Instance.IsBossActive)
        {
            myCollider.isTrigger = false;
        }
        // 3. 그 외(평소) -> 문 (지나갈 수 있음 -> Trigger 발동)
        else
        {
            myCollider.isTrigger = true;
        }
    }

    // ★ 벽(isTrigger=false)일 때 부딪히면 실행됨
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            // 방이 없어서 막힌 경우
            if (nextRoom == null)
            {
                ShowMessage(defaultBlockMessage);
            }
            // 보스전이라서 막힌 경우
            else if (BossManager.Instance != null && BossManager.Instance.IsBossActive)
            {
                ShowMessage(lockedMessage);
            }
        }
    }

    // ★ 문(isTrigger=true)일 때 겹치면 실행됨 (이동 로직)
    private void OnTriggerEnter2D(Collider2D other) { TryEnterRoom(other); }
    private void OnTriggerStay2D(Collider2D other) { TryEnterRoom(other); }

    private void TryEnterRoom(Collider2D other)
    {
        if (other.CompareTag("Player") && other is CapsuleCollider2D)
        {
            float inputX = Input.GetAxisRaw("Horizontal");
            float inputY = Input.GetAxisRaw("Vertical");

            // 문 방향으로 밀고 있을 때만 이동
            if (!IsPushingTowardsDoor(inputX, inputY)) return;

            // 이동 실행
            Vector2 dirVector = GetDirectionVector();
            RoomManager.Instance.RequestMove(dirVector, nextRoom, overrideDistance);
        }
    }

    private bool IsPushingTowardsDoor(float x, float y)
    {
        switch (moveDirection)
        {
            case Direction.North: return y > 0.5f;
            case Direction.South: return y < -0.5f;
            case Direction.East: return x > 0.5f;
            case Direction.West: return x < -0.5f;
            default: return false;
        }
    }

    private Vector2 GetDirectionVector()
    {
        switch (moveDirection)
        {
            case Direction.North: return Vector2.up;
            case Direction.South: return Vector2.down;
            case Direction.East: return Vector2.right;
            case Direction.West: return Vector2.left;
            default: return Vector2.zero;
        }
    }

    private void ShowMessage(string message)
    {
        if (UIManager.Instance != null) UIManager.Instance.ShowWarning(message);
    }
}