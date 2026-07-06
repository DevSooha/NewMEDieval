using System.Collections;
using UnityEngine;

/// <summary>
/// 필드에 떨어져 있는 아이템. 플레이어가 상호작용하면 인벤토리에 추가되고 흡수 연출 후 파괴된다.
/// (드롭 → 픽업 → 인벤토리 갱신 흐름의 마지막 단계)
/// </summary>
public class WorldItem : MonoBehaviour
{
    public ItemData itemData;
    public int quantity = 1;

    private bool initialized = false;
    private bool isPickingUp = false;
    private Transform playerTransform;
    private Collider2D col;

    private const float pickupEffectDuration = 0.12f;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void Start()
    {
        // 씬에 미리 배치된 아이템은 Init 없이 시작하므로 인스펙터 값으로 자체 초기화
        if (!initialized && itemData != null)
        {
            Init(itemData, quantity);
        }
    }

    /// <summary>스포너/드롭 로직이 호출하는 초기화. 아이콘 스프라이트까지 적용한다.</summary>
    public void Init(ItemData data, int amount)
    {
        itemData = data;
        quantity = amount;
        initialized = true;

        if (itemData != null && itemData.icon != null)
        {
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.sprite = itemData.icon;
            }
        }
    }

    /// <summary>
    /// 아이템을 인벤토리에 넣고 흡수 연출을 시작한다.
    /// </summary>
    /// <returns>픽업에 성공하면 true (인벤토리가 없거나 가득 차면 false 가능)</returns>
    public bool Pickup()
    {
        if (isPickingUp || !initialized)
        {
            Debug.Log("[WorldItem] Pickup failed");
            return false;
        }

        // Inventory.Instance는 씬에 없을 때 새 싱글톤을 자동 생성하므로 사용하지 않는다.
        // 여기서는 "씬에 인벤토리가 실제로 있는지"를 판정 조건으로 쓴다.
        Inventory inventory = FindFirstObjectByType<Inventory>();

        // 인벤토리가 없는 씬(테스트 씬 등)에서는 연출만 하고 소멸
        if (inventory == null)
        {
            StartCoroutine(PickupEffect());
            return true;
        }

        // 데이터가 비어 있는 잘못된 아이템은 조용히 제거
        if (itemData == null)
        {
            Destroy(gameObject);
            return true;
        }

        bool added = inventory.AddItem(itemData, quantity);
        if (!added)
        {
            return false;
        }

        StartCoroutine(PickupEffect());
        return true;
    }

    // 플레이어 쪽으로 빨려 들어가며 축소되는 흡수 연출. 완료 후 상호작용 종료를 알린다.
    IEnumerator PickupEffect()
    {
        isPickingUp = true;

        if (Player.Instance != null)
        {
            playerTransform = Player.Instance.transform;
            // 픽업 도중 공격 입력이 씹혀 어색해지는 것 방지
            Player.Instance.CancelAttack();
        }

        float time = 0f;
        Vector3 startPos = transform.position;
        Vector3 startScale = transform.localScale;

        while (time < pickupEffectDuration)
        {
            time += Time.deltaTime;
            float t = time / pickupEffectDuration;
            t = t * t; // ease-in: 끝으로 갈수록 빨라지는 흡수감

            if (playerTransform != null)
            {
                transform.position = Vector3.Lerp(startPos, playerTransform.position + Vector3.up * 0.5f, t);
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            }
            yield return null;
        }

        Destroy(gameObject);

        if (Player.Instance != null)
        {
            Player.Instance.OnInteractionFinished();
        }
    }
}
