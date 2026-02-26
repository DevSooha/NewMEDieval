using UnityEngine;


public enum BulletType
{
    Fireworks,
    Bomb,
    Spiral
}
public enum PotionEffect
{
    None,
    HealingBullets,
    Stealth,
    PlayerSpeed2X,
    EnemySpeed2X,
    EnemyStun,
    BulletSpeedDown
}
[CreateAssetMenu(fileName = "PotionData", menuName = "Data/Potion")]
public class PotionData : ScriptableObject
{
    public string potionName;
    public int damage1;
    public int damage2;
    public int effectTime;
    public BulletType bulletType1;
    public BulletType bulletType2;
    public PotionEffect potionEffect1;
    public PotionEffect potionEffect2;
    public Element element1;
    public Element element2;
    public Sprite topIMG;
    public Sprite bottomIMG;

    public int maxStack = 20; 
    public bool isStackable = true;
}
public class Potion
{
    public PotionData data;
    public int quantity;
    public Potion(PotionData data, int quantity = 1)
    {
        this.data = data;
        this.quantity = quantity;
    }
}
