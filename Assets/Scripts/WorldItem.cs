using NUnit.Framework.Interfaces;
using System.Collections;
using UnityEngine;

public class WorldItem : MonoBehaviour
{
    //public ItemData itemData;
    public int quantity = 1;

    // ★ 초기화가 안 되어 있으면 줍지 못하게 막는 안전장치
    private bool initialized = true; // 기본값 true (에디터 배치용)

    private bool isPickingUp = false;
    private bool isPlayerInRange = false; // 플레이어가 근처에 있나?
    private Transform playerTransform;    // 플레이어 위치 기억용 (날아가는 연출용)

    Collider2D col;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        // Trigger 체크 필수!
        if (col != null) col.isTrigger = true;
    }

    // 적이 드랍할 때 호출
    //public void Init(ItemData data, int amount)
    //{
    //    itemData = data;
    //    quantity = amount;
    //    initialized = true;
    //}

    public void Init(int amount)
    {
        quantity = amount;
        initialized = true;
    }

    private void Update()
    {
        // 1. 이미 줍는 중이면 패스
        if (isPickingUp) return;

        // 2. 초기화 안 됐으면 패스
        if (!initialized) return;

        // 3. ★ 핵심: 플레이어가 범위 안에 있고 + Z키를 눌렀을 때만 줍기 ★
        if (isPlayerInRange && Input.GetKeyDown(KeyCode.Z))
        {
            TryPickup();
        }
    }

    // 플레이어가 범위 안에 들어옴 (감지 시작)
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerInRange = true;
            playerTransform = collision.transform;
            // Debug.Log("아이템 근처! Z키를 누르세요.");
        }
    }

    // 플레이어가 범위 밖으로 나감 (감지 해제)
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false;
            playerTransform = null;
        }
    }

    void TryPickup()
    {
        //// 1. 아이템 데이터 확인
        //if (itemData == null)
        //{
        //    Debug.LogError($"[오류] {gameObject.name} 아이템에 'Item Data'가 비어있습니다!");
        //    return;
        //}

        //// 2. 인벤토리 연결 확인
        //if (Inventory.Instance == null)
        //{
        //    Debug.LogError("[오류] 씬에 'Inventory'가 없습니다!");
        //    return;
        //}

        // 3. 공격 캔슬 요청 (플레이어가 있다면)
        if (Player.Instance != null)
        {
            Player.Instance.CancelAttack();
        }

        // 4. 줍기 시작
        StartCoroutine(PickupEffect());
    }

    IEnumerator PickupEffect()
    {
        isPickingUp = true;

        // ★ 인벤토리에 아이템 추가
        //bool added = Inventory.Instance.AddItem(itemData, quantity);

        //if (added)
        {
            //Debug.Log($"아이템 획득: {itemData.itemName} ({quantity}개)");

            // --- 날아가는 연출 (선택사항) ---
            float time = 0f;
            float duration = 0.2f; // 짧게 설정
            Vector3 start = transform.position;

            while (time < duration)
            {
                time += Time.deltaTime;
                float t = time / duration;

                // 플레이어 쪽으로 빨려들어가는 연출
                if (playerTransform != null)
                {
                    transform.position = Vector3.Lerp(start, playerTransform.position, t);
                    transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
                }

                yield return null;
            }

            Destroy(gameObject);
            //}
            //else
            //{
            //    Debug.Log("인벤토리가 가득 찼습니다!");
            //    isPickingUp = false; // 다시 주을 수 있게 리셋
            //}
        }
    }
}