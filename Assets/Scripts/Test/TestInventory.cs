using UnityEngine;

public class TestInventory : MonoBehaviour
{
    [SerializeField] private Inventory inventory;
    [SerializeField] private ItemData testItem1;
    [SerializeField] private ItemData testItem2;
    
    private void Start()
    {
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            inventory.AddItem(testItem1, 100);
            Debug.Log("아이템 추가!");
        }
        if (Input.GetKeyDown(KeyCode.F))
        {
            inventory.AddItem(testItem1, 100);
            Debug.Log("아이템 추가!");
        }
    }
}

