using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public int saveVersion = 1;
    public string saveTimestamp;

    // Player location
    public string lastBonfireId;
    public string currentRoomId;
    public float bonfirePosX;
    public float bonfirePosY;

    // Player stats
    public int currentHP;
    public int maxHP;

    // Inventory
    public List<SavedItem> materialItems = new();
    public List<SavedPotion> potionItems = new();

    // Weapon slots
    public List<SavedWeaponSlot> weaponSlots = new();

    // Boss defeat
    public List<string> defeatedBossIds = new();

    // World flags (Phase 2 extensibility)
    public List<string> worldFlags = new();
}

[Serializable]
public class SavedItem
{
    public string ingredientId;
    public int quantity;
}

[Serializable]
public class SavedPotion
{
    public string displayName;
    public int temperature;
    public int damage1;
    public int damage2;
    public int bulletType1;
    public int bulletType2;
    public string phase0IngredientId;
    public string phase1IngredientId;
    public int quantity;
    public bool isCraftedRuntime;
    public string potionAssetName;
}

[Serializable]
public class SavedWeaponSlot
{
    public int weaponType;
    public int potionIndex;
}
