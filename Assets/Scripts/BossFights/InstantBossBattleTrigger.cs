using System.Collections;
using UnityEngine;

public class InstantBossBattleTrigger : MonoBehaviour
{
    [SerializeField] private KnightCombat knightCombat;
    [SerializeField] private Transform bossPositionA;

    private bool hasTriggered;

    private void Start()
    {
        if (knightCombat != null)
        {
            knightCombat.gameObject.SetActive(false);
            knightCombat.OnBattleReset += HandleBattleReset;
        }
    }

    private void OnDestroy()
    {
        if (knightCombat != null)
        {
            knightCombat.OnBattleReset -= HandleBattleReset;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered) return;
        if (!other.CompareTag("Player") || other.isTrigger) return;

        hasTriggered = true;
        StartCoroutine(BeginBattleRoutine(other));
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

        if (knightCombat != null)
        {
            knightCombat.gameObject.SetActive(true);

            if (bossPositionA != null)
            {
                knightCombat.transform.position = bossPositionA.position;
            }
            else
            {
                knightCombat.SetToPointAImmediate();
            }
        }

        yield return new WaitForSeconds(0.1f);

        if (playerScript != null)
        {
            playerScript.enabled = true;
        }

        if (knightCombat != null)
        {
            knightCombat.StartBattle();
        }
    }

    private void HandleBattleReset()
    {
        hasTriggered = false;
    }
}
