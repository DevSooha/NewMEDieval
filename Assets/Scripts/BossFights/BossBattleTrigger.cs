using System.Collections;
using UnityEngine;

public class BossBattleTrigger : MonoBehaviour
{
    [Header("1. Boss Link (Drag & Drop here)")]
    [Tooltip("Assign the boss object that exists as a child of this room prefab.")]
    public BossCombatBase assignedBoss;

    [Header("2. Blockade Settings")]
    [Tooltip("Wall/door objects that block exits during boss battle.")]
    [SerializeField] private GameObject blockadeParent;

    [Header("3. Player Position Settings")]
    [Tooltip("Forced player position when the boss battle starts.")]
    public Transform startPositionTF;

    [Header("4. Cancel/Exit Settings")]
    public Vector2 pushDirection = Vector2.down;
    public float pushDistance = 3.0f;

    private bool hasTriggered = false;
    private Transform playerTransform;

    private void Start()
    {
        ResolveAssignedBossIfNeeded();

        if (assignedBoss != null)
            assignedBoss.gameObject.SetActive(false);

        SetBlockades(false);

        if (BossManager.Instance != null)
        {
            BossManager.Instance.OnBossBattleEnded += OnBattleEnded;
        }
    }

    private void OnDisable()
    {
        if (Time.timeScale == 0f)
        {
            Time.timeScale = 1f;
        }
    }

    private void OnDestroy()
    {
        if (BossManager.Instance != null)
        {
            BossManager.Instance.OnBossBattleEnded -= OnBattleEnded;
        }
    }

    private void OnBattleEnded()
    {
        if (hasTriggered)
        {
            SetBlockades(false);
            Debug.Log("[BossTrigger] Boss defeated. Reopening blockades.");
        }
    }

    private void SetBlockades(bool isActive)
    {
        if (blockadeParent == null) return;

        foreach (Transform child in blockadeParent.transform)
        {
            child.gameObject.SetActive(isActive);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered || (BossManager.Instance != null && BossManager.Instance.IsBossActive)) return;

        if (other.CompareTag("Player"))
        {
            if (other.isTrigger) return;

            hasTriggered = true;
            playerTransform = other.transform;

            Rigidbody2D rb = playerTransform.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;

            Time.timeScale = 0f;

            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowSelectPanel(
                    "Fight the boss?",
                    "Yes", StartBossSequence,
                    "No", CancelBattle
                );
            }
            else
            {
                Debug.LogWarning("UIManager is missing. Starting battle immediately.");
                StartBossSequence();
            }
        }
    }

    public void StartBossSequence()
    {
        Time.timeScale = 1f;

        if (startPositionTF != null)
        {
            StartCoroutine(ForceMoveRoutine(startPositionTF.position, true));
        }
        else
        {
            ActivateBossBattle();
        }
    }

    public void CancelBattle()
    {
        Time.timeScale = 1f;

        if (playerTransform != null)
        {
            Vector3 pushDir = new Vector3(pushDirection.x, pushDirection.y, 0f).normalized;
            Vector3 targetPos = playerTransform.position + (pushDir * pushDistance);
            StartCoroutine(ForceMoveRoutine(targetPos, false));
        }
        else
        {
            hasTriggered = false;
        }
    }

    private IEnumerator ForceMoveRoutine(Vector3 targetPos, bool isBossStart)
    {
        if (playerTransform == null) yield break;

        Rigidbody2D rb = playerTransform.GetComponent<Rigidbody2D>();
        Player moveScript = playerTransform.GetComponent<Player>();

        if (rb != null)
        {
            rb.WakeUp();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
        }

        if (moveScript != null) moveScript.enabled = false;

        float duration = 0.5f;
        float elapsed = 0f;
        Vector3 startPos = playerTransform.position;

        while (elapsed < duration)
        {
            playerTransform.position = Vector3.Lerp(startPos, targetPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        playerTransform.position = targetPos;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.WakeUp();
        }

        if (moveScript != null) moveScript.enabled = true;

        if (isBossStart)
        {
            ActivateBossBattle();
        }
        else
        {
            hasTriggered = false;
        }
    }

    private void ActivateBossBattle()
    {
        SetBlockades(true);
        ResolveAssignedBossIfNeeded();

        if (assignedBoss != null)
        {
            assignedBoss.gameObject.SetActive(true);
            assignedBoss.StartBattle();

            if (BossManager.Instance != null)
            {
                BossManager.Instance.NotifyBossStart();
            }
        }
        else
        {
            Debug.LogError($"[BossTrigger] Assigned boss is missing on {gameObject.name}");
            SetBlockades(false);
        }
    }

    private void ResolveAssignedBossIfNeeded()
    {
        if (assignedBoss != null) return;

        // 1) Try local children first (inactive included).
        assignedBoss = GetComponentInChildren<BossCombatBase>(true);

        // 2) If not found, search the room root hierarchy (sibling branches included).
        if (assignedBoss == null)
        {
            Transform roomRoot = transform.root;
            if (roomRoot != null)
            {
                BossCombatBase[] bosses = roomRoot.GetComponentsInChildren<BossCombatBase>(true);
                if (bosses != null && bosses.Length > 0)
                {
                    assignedBoss = bosses[0];
                }
            }
        }

        if (assignedBoss != null)
        {
            Debug.LogWarning($"[BossTrigger] assignedBoss was empty on {gameObject.name}. Auto-resolved to {assignedBoss.name}.");
        }
        else
        {
            Debug.LogError($"[BossTrigger] assignedBoss unresolved on {gameObject.name} (root: {transform.root.name}).");
        }
    }
}
