using UnityEngine;

public class BedimmedWall : MonoBehaviour
{
    private Transform targetTransform; // Vector3 대신 Transform 저장
    private float moveSpeed = 0f;
    private float boxHalfSize = 0f;
    private bool isActive = false;

    // 초기화 시 Vector3 center 대신 Transform target을 받음
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
        // 활성화 상태가 아니거나 추적 대상이 사라지면 중단
        if (!isActive || targetTransform == null) return;

        // 실시간 대상 위치 파악
        Vector3 currentTargetPos = targetTransform.position;

        // 1. 이동: 실시간 대상 위치(currentTargetPos)를 향해 이동
        transform.position = Vector3.MoveTowards(transform.position, currentTargetPos, moveSpeed * Time.deltaTime);

        // 2. 사각형 범위 체크 (AABB Check)
        // 대상 오브젝트의 현재 위치를 기준으로 거리 계산
        float diffX = Mathf.Abs(transform.position.x - currentTargetPos.x);
        float diffY = Mathf.Abs(transform.position.y - currentTargetPos.y);

        // 안전지대 안으로 들어오면 소멸
        if (diffX <= boxHalfSize && diffY <= boxHalfSize)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isActive && collision.CompareTag("Player"))
        {
            Debug.Log($"[BedimmedWall] Hit Player: {gameObject.name}");
        }
    }

    private void OnDrawGizmos()
    {
        // 대상이 있을 때만 기즈모를 그림
        if (targetTransform == null) return;

        Gizmos.color = Color.green;
        Vector3 size = new Vector3(boxHalfSize * 2, boxHalfSize * 2, 1f);

        // 대상 오브젝트의 현재 위치에 박스 표시
        Gizmos.DrawWireCube(targetTransform.position, size);
    }
}