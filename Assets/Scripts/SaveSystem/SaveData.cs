using System;
using System.Collections.Generic;

/// <summary>
/// 세이브 슬롯 하나에 기록되는 전체 게임 상태.
/// Newtonsoft.Json으로 직렬화된다(Dictionary 지원을 위해 JsonUtility 대신 사용).
/// </summary>
[Serializable]
public class SaveData
{
    public string saveVersion = "1.0";
    public long timestamp;

    /// <summary>마지막으로 사용한 SavePoint의 ID. 로드 시 스폰 위치 결정에 사용.</summary>
    public string lastSavePointId;

    public PlayerData player;

    /// <summary>ISaveable 오브젝트들의 상태 맵. key = ISaveable.SaveId.</summary>
    public Dictionary<string, object> worldStates = new();

    public List<string> defeatedBossIds = new();
}

/// <summary>플레이어 상태 묶음. 새 포맷에서 SaveData.player에 중첩된다.</summary>
[Serializable]
public class PlayerData
{
    /// <summary>세이브 포인트 월드 좌표 = 리스폰 위치.</summary>
    public Vector3Serializable spawnPosition;
    public int currentHP;
    public int maxHP;
    public List<SavedItem> materialItems = new();
    public List<SavedPotion> potionItems = new();
    public List<SavedWeaponSlot> weaponSlots = new();
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
    public bool isEquippedInSlot;
}

[Serializable]
public class SavedWeaponSlot
{
    public int weaponType;
    public int potionIndex;
}
