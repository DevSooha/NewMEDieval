using System.Collections;
using UnityEngine;

public class BossBattleTrigger : MonoBehaviour
{
    [Header("Settings")]
    public Transform startPositionTF;

    [Header("Exit Settings")]
    public Vector2 pushDirection = Vector2.down; // (0, -1) 확인 필수
    public float pushDistance = 3.0f;

    [Header("References")]
    public ThreeWitchCombat linkedBoss;

    private bool hasTriggered = false;
    private Transform playerTransform;

    // 디버그용 선 그리기
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red; // 빨간색으로 변경 (눈에 잘 띄게)
        Vector3 direction = new Vector3(pushDirection.x, pushDirection.y, 0).normalized;
        Gizmos.DrawLine(transform.position, transform.position + direction * pushDistance);
        Gizmos.DrawWireSphere(transform.position + direction * pushDistance, 0.5f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered) return;

        if (other.CompareTag("Player"))
        {
            if (other.isTrigger) return;
            hasTriggered = true;
            playerTransform = other.transform;

            Rigidbody2D rb = playerTransform.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;

            Time.timeScale = 0;

            UIManager.Instance.ShowSelectPanel(
                "Fight the Three Witches?",
                "Yes", StartBossSequence,
                "No", CancelBattle
            );
        }
    }

    public void StartBossSequence()
    {
        Debug.Log("Yes 선택: 보스전 시작");
        Time.timeScale = 1;

        if (startPositionTF != null)
        {
            StartCoroutine(ForceMoveRoutine(startPositionTF.position, true));
        }
        else
        {
            // 위치가 없으면 그냥 보스전 시작
            TriggerBossBattleLogic();
        }
    }

    public void CancelBattle()
    {
        Debug.Log("No 선택: 나가기");
        Time.timeScale = 1;

        if (playerTransform != null)
        {
            // 현재 플레이어 위치 기준 뒤로 이동
            Vector3 pushDir = new Vector3(pushDirection.x, pushDirection.y, 0).normalized;
            Vector3 targetPos = playerTransform.position + (pushDir * pushDistance);

            StartCoroutine(ForceMoveRoutine(targetPos, false));
        }
        else
        {
            hasTriggered = false; // 예외 처리
        }
    }

    // ★ 핵심 수정: 이름 변경 및 물리 깨우기 추가
    IEnumerator ForceMoveRoutine(Vector3 targetPos, bool isBossStart)
    {
        // 1. 혹시 모를 물리 수면 상태 깨우기
        Rigidbody2D rb = playerTransform.GetComponent<Rigidbody2D>();
        var moveScript = playerTransform.GetComponent<Player>();

        if (rb != null)
        {
            rb.WakeUp(); // ★ 야! 일어나! (이게 없으면 씹힐 수 있음)
            rb.bodyType = RigidbodyType2D.Kinematic; // 물리 끄기
            rb.linearVelocity = Vector2.zero;
        }
        if (moveScript != null) moveScript.enabled = false;

        // 2. 이동 (Time.unscaledDeltaTime 대신 deltaTime 사용. 위에서 TimeScale=1 했으므로 안전)
        float duration = 0.2f; // 아주 빠르게 이동
        float elapsed = 0f;
        Vector3 startPos = playerTransform.position;

        while (elapsed < duration)
        {
            playerTransform.position = Vector3.Lerp(startPos, targetPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 3. 도착 확정
        playerTransform.position = targetPos;

        // 4. 복구
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.WakeUp(); // 다시 한번 깨우기
        }
        if (moveScript != null) moveScript.enabled = true;

        // 5. 후처리
        if (isBossStart)
        {
            TriggerBossBattleLogic();
        }
        else
        {
            // 취소(Cancel)인 경우
            if (RoomManager.Instance != null) RoomManager.Instance.UpdateRoomStateAfterTeleport();

            // 트리거 재사용 대기
            yield return new WaitForSeconds(0.5f);
            hasTriggered = false;
        }
    }

    void TriggerBossBattleLogic()
    {
        if (BossManager.Instance != null)
        {
            if (linkedBoss != null) BossManager.Instance.threeWitchCombat = linkedBoss;
            else BossManager.Instance.threeWitchCombat = transform.parent.GetComponentInChildren<ThreeWitchCombat>(true);
            BossManager.Instance.StartBossBattle();
        }
    }
}