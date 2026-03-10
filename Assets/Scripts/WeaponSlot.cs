using UnityEngine;

public enum WeaponType { None, Melee, PotionBomb }

[System.Serializable]
public class WeaponSlot
{
    public WeaponType type;
    public GameObject specificPrefab;
    public int count = -1;
    public Potion equippedPotion;
}