using UnityEngine;

public class BedimmedWall : MonoBehaviour
{
    [Header("Hit Settings")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float knockbackDistance = 1f;
    [SerializeField] private float knockbackDuration = 0.2f;

    private Transform targetTransform; // Vector3 대신 Transform 저장
    private float moveSpeed = 0f;
    private float boxHalfSize = 0f;
    private bool isActive = false;

    // �ʱ�ȭ �� Vector3 center ��� Transform target�� ����
    public void Activate(Transform target, float speed, float safeZoneSize)
    {
        targetTransform = target;
        moveSpeed = speed;
        boxHalfSize = safeZoneSize;
        isActive = true;
        gameObject.SetActive(true);
    }

    void Update()
    {
        // Ȱ��ȭ ���°� �ƴϰų� ���� ����� ������� �ߴ�
        if (!isActive || targetTransform == null) return;

        // �ǽð� ��� ��ġ �ľ�
        Vector3 currentTargetPos = targetTransform.position;

        // 1. �̵�: �ǽð� ��� ��ġ(currentTargetPos)�� ���� �̵�
        transform.position = Vector3.MoveTowards(transform.position, currentTargetPos, moveSpeed * Time.deltaTime);

        // 2. �簢�� ���� üũ (AABB Check)
        // ��� ������Ʈ�� ���� ��ġ�� �������� �Ÿ� ���
        float diffX = Mathf.Abs(transform.position.x - currentTargetPos.x);
        float diffY = Mathf.Abs(transform.position.y - currentTargetPos.y);

        // �������� ������ ������ �Ҹ�
        if (diffX <= boxHalfSize && diffY <= boxHalfSize)
        {
            isActive = false;
            gameObject.SetActive(false);
        }
    }


    private void OnDisable()
    {
        isActive = false;
        targetTransform = null;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isActive && collision.CompareTag("Player"))
        {
            BossHitResolver.TryApplyBossHit(
                collision,
                damage,
                transform.position,
                knockbackDistance,
                knockbackDuration
            );
        }
    }

    private void OnDrawGizmos()
    {
        // ����� ���� ���� ����� �׸�
        if (targetTransform == null) return;

        Gizmos.color = Color.green;
        Vector3 size = new Vector3(boxHalfSize * 2, boxHalfSize * 2, 1f);

        // ��� ������Ʈ�� ���� ��ġ�� �ڽ� ǥ��
        Gizmos.DrawWireCube(targetTransform.position, size);
    }
}
