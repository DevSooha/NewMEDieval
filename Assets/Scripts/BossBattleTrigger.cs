using System.Collections;
using UnityEngine;

public class BossBattleTrigger : MonoBehaviour
{
    [Header("Settings")]
    public Transform startPositionTF;

    [Header("Exit Settings")]
    public Vector2 pushDirection = Vector2.down;
    public float pushDistance = 3.0f;

    [Header("Boss Prefabs")]
    public GameObject rolietPrefab;        
    public GameObject threeWitchPrefab;   

    private bool hasTriggered = false;
    private Transform playerTransform;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
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
                "Fight the boss?",
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
            SpawnBoss();
        }
    }

    public void CancelBattle()
    {
        Debug.Log("No 선택: 취소");
        Time.timeScale = 1;

        if (playerTransform != null)
        {
            Vector3 pushDir = new Vector3(pushDirection.x, pushDirection.y, 0).normalized;
            Vector3 targetPos = playerTransform.position + (pushDir * pushDistance);

            StartCoroutine(ForceMoveRoutine(targetPos, false));
        }
        else
        {
            hasTriggered = false;
        }
    }

    IEnumerator ForceMoveRoutine(Vector3 targetPos, bool isBossStart)
    {
        Rigidbody2D rb = playerTransform.GetComponent<Rigidbody2D>();
        var moveScript = playerTransform.GetComponent<Player>();

        if (rb != null)
        {
            rb.WakeUp();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
        }
        if (moveScript != null) moveScript.enabled = false;

        float duration = 0.2f;
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
            SpawnBoss();
        }
        else
        {
            if (RoomManager.Instance != null) RoomManager.Instance.UpdateRoomStateAfterTeleport();
            yield return new WaitForSeconds(0.5f);
            hasTriggered = false;
        }
    }

    void SpawnBoss()
    {
        RoomData currentRoom = RoomManager.Instance?.currentRoomData;
        if (currentRoom == null) {
            Debug.LogError("[BossTrigger] No current room data!");
            return;
        }

        Debug.Log($"[BossTrigger] Spawning boss for room: {currentRoom.roomID}");

        if (currentRoom.roomID == "sum_3")
        {
            if (rolietPrefab != null)
            { 
                RolietCombat roliet = rolietPrefab.GetComponent<RolietCombat>();
                    roliet.StartBattle();
                    Debug.Log("[BossTrigger] Roliet spawned & attacking!");
                
            }
        }
        else if (currentRoom.roomID == "spr_4")
        {
            if (threeWitchPrefab != null)
            {
                ThreeWitchCombat witch = threeWitchPrefab.GetComponent<ThreeWitchCombat>();
                if (witch != null) {
                    witch.StartBattle();  
                    Debug.Log("[BossTrigger] ThreeWitch spawned!");
                }
            }
        }
        else
        {
            Debug.LogWarning($"No boss configured for room: {currentRoom.roomID}");
        }
    }
}
