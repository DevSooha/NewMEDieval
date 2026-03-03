using UnityEngine;

public class MapNode : MonoBehaviour
{
    public enum Direction { North, South, East, West }

    [Header("�� �̵� ����")]
    public Direction moveDirection;
    public RoomData nextRoom;
    public float overrideDistance = 0f;

    [Header("���� �޽��� ����")]
    public string defaultBlockMessage = "The path is blocked.";
    public string lockedMessage = "You cannot flee!";
    public string unlockedMessage = "Now you can proceed.";

    private BoxCollider2D myCollider;

    private float nextBlockedMessageTime;
    private float nextBossScanTime;
    private bool cachedHasActiveBoss;

    private const float blockedMessageCooldown = 0.6f;
    private const float bossScanInterval = 0.2f;

    private void Awake()
    {
        myCollider = GetComponent<BoxCollider2D>();
    }

    // �� ������ �� ���¸� ����
    private void Update()
    {
        if (myCollider == null) return;

        bool isBossActive = IsBossBattleLocked();

        // ����� ���� ���ų� ������ ���̸� ������ ���
        myCollider.isTrigger = nextRoom != null && !isBossActive;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryShowBlockedMessage(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryShowBlockedMessage(collision);
    }

    // ��(isTrigger=true)�� �� ��ġ�� �����
    private void OnTriggerEnter2D(Collider2D other) { TryEnterRoom(other); }
    private void OnTriggerStay2D(Collider2D other) { TryEnterRoom(other); }

    private void TryEnterRoom(Collider2D other)
    {
        if (!other.CompareTag("Player") || !IsPlayerBodyCollider(other)) return;

        if (UIManager.DialogueActive || UIManager.SelectionActive) return;
        if (RoomManager.Instance == null) return;

        if (IsBossBattleLocked())
        {
            TryShowBlockedMessage(lockedMessage);
            return;
        }

        float inputX = Input.GetAxisRaw("Horizontal");
        float inputY = Input.GetAxisRaw("Vertical");

        // �� �������� �а� ���� ���� �̵�
        if (!IsPushingTowardsDoor(inputX, inputY)) return;

        Vector2 dirVector = GetDirectionVector();
        RoomManager.Instance.RequestMove(dirVector, nextRoom, overrideDistance);
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

    private bool IsBossBattleLocked()
    {
        if (BossManager.Instance != null && BossManager.Instance.IsBossActive)
        {
            return true;
        }

        if (Time.time >= nextBossScanTime)
        {
            GameObject[] activeBosses = GameObject.FindGameObjectsWithTag("Boss");
            bool hasTaggedBoss = activeBosses != null && activeBosses.Length > 0;

            if (hasTaggedBoss)
            {
                cachedHasActiveBoss = true;
            }
            else
            {
                BossCombatBase[] activeBossCombats = FindObjectsByType<BossCombatBase>(FindObjectsSortMode.None);
                cachedHasActiveBoss = activeBossCombats != null && activeBossCombats.Length > 0;
            }

            nextBossScanTime = Time.time + bossScanInterval;
        }

        return cachedHasActiveBoss;
    }

    private bool IsPlayerBodyCollider(Collider2D col)
    {
        return col is CapsuleCollider2D;
    }

    private void TryShowBlockedMessage(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;
        if (!IsPlayerBodyCollider(collision.otherCollider)) return;

        if (nextRoom == null)
        {
            TryShowBlockedMessage(defaultBlockMessage);
            return;
        }

        if (IsBossBattleLocked())
        {
            TryShowBlockedMessage(lockedMessage);
        }
    }

    private void TryShowBlockedMessage(string message)
    {
        if (Time.unscaledTime < nextBlockedMessageTime) return;
        nextBlockedMessageTime = Time.unscaledTime + blockedMessageCooldown;
        ShowMessage(message);
    }

    private void ShowMessage(string message)
    {
        if (UIManager.Instance != null) UIManager.Instance.ShowWarning(message);
    }
}
