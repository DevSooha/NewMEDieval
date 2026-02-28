using System.Collections;
using UnityEngine;

public class FinalBossEntryTrigger : MonoBehaviour
{
    [SerializeField] private FinalBossCombat finalBossCombat;
    [SerializeField] private Transform bossSpawnPoint;
    [SerializeField] private bool isSecondRunDebug;

    private bool hasTriggered;
    private IBossBattleResetNotifier resetNotifier;

    private void Start()
    {
        if (finalBossCombat == null)
        {
            finalBossCombat = GetComponentInChildren<FinalBossCombat>(true);
            if (finalBossCombat == null)
            {
                finalBossCombat = transform.root.GetComponentInChildren<FinalBossCombat>(true);
            }
        }

        if (finalBossCombat != null)
        {
            finalBossCombat.gameObject.SetActive(true);
            finalBossCombat.PrepareIdleState(bossSpawnPoint);
            resetNotifier = finalBossCombat as IBossBattleResetNotifier;
            if (resetNotifier != null)
            {
                resetNotifier.OnBattleReset += HandleBattleReset;
            }
        }
    }

    private void OnDestroy()
    {
        if (resetNotifier != null)
        {
            resetNotifier.OnBattleReset -= HandleBattleReset;
        }
    }

    private void Update()
    {
        if (!hasTriggered) return;
        if (BossManager.Instance == null || !BossManager.Instance.IsBossActive) return;

        if (finalBossCombat == null)
        {
            BossManager.Instance.EndBossBattle();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered) return;
        if (BossManager.Instance != null && BossManager.Instance.IsBossActive) return;
        if (!isSecondRunDebug) return;
        if (!other.CompareTag("Player") || other.isTrigger) return;

        hasTriggered = true;
        StartCoroutine(BeginBattleRoutine(other));
    }

    private IEnumerator BeginBattleRoutine(Collider2D playerCollider)
    {
        if (finalBossCombat == null) yield break;

        Player playerScript = playerCollider.GetComponent<Player>();
        Rigidbody2D rb = playerCollider.GetComponent<Rigidbody2D>();

        if (rb != null) rb.linearVelocity = Vector2.zero;
        if (playerScript != null) playerScript.enabled = false;

        if (BossManager.Instance != null)
        {
            BossManager.Instance.NotifyBossStart();
        }

        finalBossCombat.gameObject.SetActive(true);
        finalBossCombat.PrepareIdleState(bossSpawnPoint);

        yield return new WaitForSeconds(0.1f);

        if (playerScript != null) playerScript.enabled = true;
        finalBossCombat.StartBattle();
    }

    private void HandleBattleReset()
    {
        hasTriggered = false;
        if (finalBossCombat != null)
        {
            finalBossCombat.PrepareIdleState(bossSpawnPoint);
        }
    }
}
