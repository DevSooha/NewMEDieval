using UnityEngine;

public class BossBattleTrigger : MonoBehaviour
{
    public BossAI linkedBoss;
    private bool hasTriggered = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered) return;

        if (other.CompareTag("Player"))
        {
            hasTriggered = true;

            // 1. 매니저가 있는지 확인
            if (BossManager.Instance == null)
            {
                Debug.LogError("BossManager가 하이어라키에 없습니다! 만들어주세요!");
                return;
            }

            // 2. 보스 연결 (직접 연결된 게 없으면 찾기)
            if (linkedBoss != null)
            {
                BossManager.Instance.bossAI = linkedBoss;
            }
            else
            {
                // 혹시 깜빡하고 연결 안 했으면 코드로라도 찾음
                BossManager.Instance.bossAI = transform.parent.GetComponentInChildren<BossAI>(true);
            }
            // 4. 전투 시작!
            if (BossManager.Instance.bossAI != null)
            {
                BossManager.Instance.StartBossBattle();
            }
        }
    }
}