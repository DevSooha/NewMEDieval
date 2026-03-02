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

public enum IngredientShapeType
{
    Fireworks = 1,
    AfterimageBomb = 2,
    Tornado = 3
}

[CreateAssetMenu(
    fileName = "NewItemData",
    menuName = "Inventory/Item Data",
    order = 1)]
public class ItemData : ScriptableObject
{
    [Header("Craft Identity")]
    public string ingredientId;
    public string topName;
    public string bottomName;
    public IngredientShapeType shapeType = IngredientShapeType.Fireworks;

    [Header("Legacy Combat")]
    public int topDamage;
    public int bottomDamage;
    [SerializeField] public Element element;
    [SerializeField] public Sprite topSprite;
    [SerializeField] public Sprite bottomSprite;

    [Header("UI")]
    [TextArea] public string description;
    public Sprite icon;

    [Header("Spawn/Prefab")]
    public GameObject specificPrefab;

    [Header("Inventory")]
    public int maxStack = 99;
    public bool isStackable = true;
    public int quantity;
    public ItemCategory category;

    public string GetIngredientId()
    {
        if (!string.IsNullOrWhiteSpace(ingredientId)) return ingredientId;
        if (!string.IsNullOrWhiteSpace(name)) return name;
        return (topName + bottomName).Replace(" ", "");
    }

    public ElementType GetPrimaryElementType()
    {
        return element switch
        {
            Element.Fire => ElementType.Fire,
            Element.Lightning => ElementType.Electric,
            _ => ElementType.Water
        };
    }
}
