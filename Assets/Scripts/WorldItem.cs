using System.Collections;
using UnityEngine;

public class WorldItem : MonoBehaviour
{
    public ItemData itemData;
    public int quantity = 1;

    private bool initialized = false;

    private bool isPickingUp = false;
    private Transform playerTransform;
    private Collider2D col;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void Start()
    {
        if (!initialized && itemData != null)
        {
            Init(itemData, quantity);
        }
    }

    public void Init(ItemData data, int amount)
    {
        itemData = data;
        quantity = amount;
        initialized = true;
    }

    // PlayerInteraction에서 호출
    public void Pickup()
    {

        if (isPickingUp || !initialized)
        {
            Debug.Log("[WorldItem] 줍기 실패");
            return;
        }

        Inventory inventory = FindFirstObjectByType<Inventory>();

        if (inventory != null)
        {
            if (itemData == null)
            {
                Destroy(gameObject);
                return;
            }

            bool added = inventory.AddItem(itemData, quantity);

            if (added)
            {
                StartCoroutine(PickupEffect());
            }
        }
        else
        {
            StartCoroutine(PickupEffect());
        }
    }

    IEnumerator PickupEffect()
    {
        isPickingUp = true;

        if (Player.Instance != null)
        {
            playerTransform = Player.Instance.transform;
            Player.Instance.CancelAttack();
        }

        // --- 날아가는 연출 ---
        float time = 0f;
        float duration = 0.3f;
        Vector3 startPos = transform.position;
        Vector3 startScale = transform.localScale;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            t = t * t;

            if (playerTransform != null)
            {
                transform.position = Vector3.Lerp(startPos, playerTransform.position + Vector3.up * 0.5f, t);
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            }
            yield return null;
        }

        Destroy(gameObject);

        if (Player.Instance != null) Player.Instance.OnInteractionFinished();
    }
}