using System.Collections;
using UnityEngine;

public class InstantBossBattleTrigger : MonoBehaviour
{
    [SerializeField] private BossCombatBase bossCombat;
    [SerializeField] private Transform bossPositionA;

    private bool hasTriggered;
    private IBossBattleResetNotifier resetNotifier;
    private IBossStartPositioner startPositioner;

    private void Start()
    {
        if (bossCombat != null)
        {
            bossCombat.gameObject.SetActive(false);

            resetNotifier = bossCombat as IBossBattleResetNotifier;
            if (resetNotifier != null)
            {
                resetNotifier.OnBattleReset += HandleBattleReset;
            }

            startPositioner = bossCombat as IBossStartPositioner;
        }
    }

    private void OnDestroy()
    {
        if (resetNotifier != null)
        {
            resetNotifier.OnBattleReset -= HandleBattleReset;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered) return;
        if (!other.CompareTag("Player") || other.isTrigger) return;

        hasTriggered = true;
        StartCoroutine(BeginBattleRoutine(other));
    }

    private void Update()
    {
        if (!hasTriggered) return;
        if (BossManager.Instance == null || !BossManager.Instance.IsBossActive) return;

        if (bossCombat == null || !bossCombat.gameObject.activeInHierarchy)
        {
            BossManager.Instance.EndBossBattle();
        }
    }

    private IEnumerator BeginBattleRoutine(Collider2D playerCollider)
    {
        Player playerScript = playerCollider.GetComponent<Player>();
        Rigidbody2D rb = playerCollider.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (playerScript != null)
        {
            playerScript.enabled = false;
        }

        if (BossManager.Instance != null)
        {
            BossManager.Instance.NotifyBossStart();
        }

        if (bossCombat != null)
        {
            bossCombat.gameObject.SetActive(true);

            if (bossPositionA != null)
            {
                bossCombat.transform.position = bossPositionA.position;
            }
            else if (startPositioner != null)
            {
                startPositioner.SetToPointAImmediate();
            }
        }

        yield return new WaitForSeconds(0.1f);

        if (playerScript != null)
        {
            playerScript.enabled = true;
        }

        if (bossCombat != null)
        {
            bossCombat.StartBattle();
        }
    }

    private void HandleBattleReset()
    {
        hasTriggered = false;
    }
}
