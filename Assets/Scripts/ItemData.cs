using UnityEngine;

public enum ItemCategory
{
    Material,
    Potion
}
[CreateAssetMenu(
    fileName = "NewItemData",
    menuName = "Inventory/Item Data",
    order = 1)]
public class ItemData : ScriptableObject
{
    public string itemName;
    public string description;
    public Sprite icon;

    public GameObject specificPrefab;

    public int maxStack = 99;           // 최대 중첩 개수
    public bool isStackable = true;
    public ItemCategory category;
}


