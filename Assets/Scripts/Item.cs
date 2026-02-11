[System.Serializable]
public class Item
{
    public ItemData data;
    public int quantity;
    public Item(ItemData data, int quantity = 1)
    {
        this.data = data;
        this.quantity = quantity;
    }
}
