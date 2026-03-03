using System;
using UnityEngine;

public class GrantAllItemDataToInventoryTest : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Inventory inventory;

    [Header("Grant Settings")]
    [SerializeField] private string resourcesFolder = "ItemData";
    [SerializeField] private int amountPerItem = 20;
    [SerializeField] private bool includePotionCategory = false;
    [SerializeField] private bool grantOnStart = true;
    [SerializeField] private bool restrictAutoGrantToTestScenes = true;
    [SerializeField] private bool oneShotOnStart = true;
    [SerializeField] private KeyCode grantHotkey = KeyCode.F7;

    private bool grantedOnStart;

    private void Start()
    {
        ResolveInventory();

        if (grantOnStart)
        {
            if (CanRunAutoGrantInCurrentScene())
            {
                GrantAll();
                grantedOnStart = true;
            }
            else
            {
                Debug.Log($"[GrantAllItemDataToInventoryTest] Auto-grant blocked in non-test scene: {gameObject.scene.name}");
            }
        }
    }

    private void Update()
    {
        if (!Input.GetKeyDown(grantHotkey)) return;

        ResolveInventory();

        if (oneShotOnStart && grantedOnStart)
        {
            Debug.Log("[GrantAllItemDataToInventoryTest] oneShotOnStart=true, start grant already executed.");
            return;
        }

        GrantAll();
    }

    [ContextMenu("Grant All ItemData To Inventory")]
    public void GrantAll()
    {
        ResolveInventory();
        if (inventory == null)
        {
            Debug.LogWarning("[GrantAllItemDataToInventoryTest] Inventory reference is missing.");
            return;
        }

        ItemData[] allItemData = Resources.LoadAll<ItemData>(resourcesFolder);
        if (allItemData == null || allItemData.Length == 0)
        {
            Debug.LogWarning($"[GrantAllItemDataToInventoryTest] No ItemData found in Resources/{resourcesFolder}");
            return;
        }

        int grantedCount = 0;

        for (int i = 0; i < allItemData.Length; i++)
        {
            ItemData data = allItemData[i];
            if (data == null) continue;

            if (!includePotionCategory && data.category == ItemCategory.Potion)
            {
                continue;
            }

            int amount = Mathf.Max(1, amountPerItem);
            inventory.AddItem(data, amount);
            grantedCount++;
        }

        Debug.Log($"[GrantAllItemDataToInventoryTest] Granted {grantedCount} ItemData entries x{Mathf.Max(1, amountPerItem)}");
    }

    private void ResolveInventory()
    {
        if (inventory != null) return;

        inventory = Inventory.Instance;
        if (inventory != null) return;

        inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
    }

    private bool CanRunAutoGrantInCurrentScene()
    {
        if (!restrictAutoGrantToTestScenes)
        {
            return true;
        }

        string sceneName = gameObject.scene.name;
        return !string.IsNullOrWhiteSpace(sceneName)
               && sceneName.IndexOf("test", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
