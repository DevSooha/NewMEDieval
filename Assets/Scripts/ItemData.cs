using UnityEngine;

public enum ItemCategory
{
    Material,
    Potion
}
public enum Element
{
    Water,
    Fire,
    Lightning
}

[CreateAssetMenu(
    fileName = "NewItemData",
    menuName = "Inventory/Item Data",
    order = 1)]
public class ItemData : ScriptableObject
{
    public string topName;
    public string bottomName;
    public int topDamage;
    public int bottomDamage;
    [SerializeField] public Element element;
    [SerializeField] public SpriteRenderer TopImage;
    [SerializeField] public SpriteRenderer BottomImage;

    public string description;
    public Sprite icon;

    public GameObject specificPrefab;

    public int maxStack = 99;           // �ִ� ��ø ����
    public bool isStackable = true;
    public int quantity;
    public ItemCategory category;
}


