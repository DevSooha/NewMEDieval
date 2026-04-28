using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveManager : Singleton<SaveManager>
{
    private const int CurrentSaveVersion = 1;

    private string SaveFilePath => Path.Combine(Application.persistentDataPath, "save.json");

    private SaveData pendingLoadData;

    protected override void Awake()
    {
        base.Awake();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public bool HasSaveFile()
    {
        return File.Exists(SaveFilePath);
    }

    public void Save(string bonfireId, string roomId, Vector2 bonfireWorldPos)
    {
        SaveData data = new SaveData
        {
            saveVersion = CurrentSaveVersion,
            saveTimestamp = DateTime.Now.ToString("o"),
            lastBonfireId = bonfireId,
            currentRoomId = roomId,
            bonfirePosX = bonfireWorldPos.x,
            bonfirePosY = bonfireWorldPos.y
        };

        // Player stats
        PlayerHealth health = Player.Instance != null
            ? Player.Instance.GetComponent<PlayerHealth>()
            : null;
        if (health != null)
        {
            data.currentHP = health.CurrentHP;
            data.maxHP = health.MaxHP;
        }

        // Materials
        data.materialItems = new List<SavedItem>();
        if (Inventory.Instance != null)
        {
            foreach (Item item in Inventory.Instance.MaterialItems)
            {
                if (item?.data == null) continue;
                data.materialItems.Add(new SavedItem
                {
                    ingredientId = item.data.GetIngredientId(),
                    quantity = item.quantity
                });
            }
        }

        // Potions
        data.potionItems = new List<SavedPotion>();
        if (Inventory.Instance != null)
        {
            foreach (Potion potion in Inventory.Instance.PotionItems)
            {
                if (potion?.data == null) continue;
                SavedPotion sp = new SavedPotion
                {
                    displayName = potion.data.GetDisplayName(),
                    temperature = (int)potion.data.temperature,
                    damage1 = potion.data.damage1,
                    damage2 = potion.data.damage2,
                    bulletType1 = (int)potion.data.bulletType1,
                    bulletType2 = (int)potion.data.bulletType2,
                    quantity = potion.quantity,
                    isCraftedRuntime = potion.data.isCraftedRuntimeData
                };

                PotionPhaseSpec phase0 = potion.data.GetPhase(0);
                PotionPhaseSpec phase1 = potion.data.GetPhase(1);
                sp.phase0IngredientId = phase0?.ingredientId ?? string.Empty;
                sp.phase1IngredientId = phase1?.ingredientId ?? string.Empty;

                if (!potion.data.isCraftedRuntimeData)
                {
                    sp.potionAssetName = potion.data.name;
                }

                data.potionItems.Add(sp);
            }
        }

        // Weapon slots
        data.weaponSlots = new List<SavedWeaponSlot>();
        PlayerAttackSystem attackSystem = Player.Instance != null
            ? Player.Instance.GetComponent<PlayerAttackSystem>()
            : null;
        if (attackSystem != null)
        {
            foreach (WeaponSlot slot in attackSystem.slots)
            {
                int potionIndex = -1;
                if (slot.equippedPotion != null && Inventory.Instance != null)
                {
                    potionIndex = Inventory.Instance.PotionItems.IndexOf(slot.equippedPotion);
                }

                data.weaponSlots.Add(new SavedWeaponSlot
                {
                    weaponType = (int)slot.type,
                    potionIndex = potionIndex
                });
            }
        }

        // Boss defeat
        data.defeatedBossIds = BossDefeatTracker.Instance != null
            ? BossDefeatTracker.Instance.GetDefeatedBossIds()
            : new List<string>();

        // World flags (reserved for Phase 2)
        data.worldFlags = new List<string>();

        // Write to file
        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SaveFilePath, json);
            Debug.Log($"[SaveManager] Game saved to {SaveFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Save failed: {e.Message}");
        }
    }

    public SaveData Load()
    {
        if (!HasSaveFile()) return null;

        try
        {
            string json = File.ReadAllText(SaveFilePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            if (data == null)
            {
                Debug.LogWarning("[SaveManager] Failed to parse save file.");
                return null;
            }

            if (data.saveVersion != CurrentSaveVersion)
            {
                Debug.LogWarning($"[SaveManager] Save version mismatch: file={data.saveVersion}, current={CurrentSaveVersion}");
            }

            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Load failed: {e.Message}");
            return null;
        }
    }

    public void DeleteSave()
    {
        try
        {
            if (File.Exists(SaveFilePath))
            {
                File.Delete(SaveFilePath);
                Debug.Log("[SaveManager] Save file deleted.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Delete failed: {e.Message}");
        }
    }

    public void ApplyLoadedData(SaveData data)
    {
        if (data == null) return;

        // Boss defeat tracker
        if (BossDefeatTracker.Instance != null)
        {
            BossDefeatTracker.Instance.RestoreFromSave(data.defeatedBossIds);
        }

        // Set restart point for RoomManager
        RoomManager.restartPointOverride = new Vector3(data.bonfirePosX, data.bonfirePosY, 0f);

        // Store data for deferred application after scene load
        pendingLoadData = data;

        // Apply immediately if Player already exists
        if (Player.Instance != null)
        {
            ApplyPlayerState(data);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (pendingLoadData == null) return;
        if (scene.name != "Field") return;

        // Defer one frame to let Player.Awake() finish
        StartCoroutine(ApplyAfterFrame());
    }

    private System.Collections.IEnumerator ApplyAfterFrame()
    {
        yield return null;

        if (pendingLoadData != null && Player.Instance != null)
        {
            ApplyPlayerState(pendingLoadData);
            pendingLoadData = null;
        }
    }

    private void ApplyPlayerState(SaveData data)
    {
        // HP
        PlayerHealth health = Player.Instance.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.Resurrect();
        }

        // Position
        Player.Instance.SetSavedPosition(new Vector3(data.bonfirePosX, data.bonfirePosY, 0f));

        // Inventory
        RestoreInventory(data);

        // Weapon slots
        RestoreWeaponSlots(data);
    }

    private void RestoreInventory(SaveData data)
    {
        if (Inventory.Instance == null) return;

        // Clear current inventory
        List<Item> materials = Inventory.Instance.MaterialItems;
        while (materials.Count > 0)
        {
            Inventory.Instance.RemoveItemCompletely(materials[0]);
        }

        List<Potion> potions = Inventory.Instance.PotionItems;
        while (potions.Count > 0)
        {
            Inventory.Instance.RemovePotionCompletely(potions[0]);
        }

        // Restore materials
        ItemData[] allItems = Resources.LoadAll<ItemData>("ItemData");
        foreach (SavedItem saved in data.materialItems)
        {
            ItemData matchedData = null;
            foreach (ItemData itemData in allItems)
            {
                if (itemData.GetIngredientId() == saved.ingredientId)
                {
                    matchedData = itemData;
                    break;
                }
            }

            if (matchedData != null)
            {
                Inventory.Instance.AddItem(matchedData, saved.quantity);
            }
            else
            {
                Debug.LogWarning($"[SaveManager] Item not found: {saved.ingredientId}");
            }
        }

        // Restore potions
        foreach (SavedPotion saved in data.potionItems)
        {
            PotionData potionData;

            if (!saved.isCraftedRuntime && !string.IsNullOrEmpty(saved.potionAssetName))
            {
                potionData = Resources.Load<PotionData>(saved.potionAssetName);
                if (potionData == null)
                {
                    Debug.LogWarning($"[SaveManager] Potion asset not found: {saved.potionAssetName}");
                    continue;
                }
            }
            else
            {
                potionData = ScriptableObject.CreateInstance<PotionData>();
                potionData.isCraftedRuntimeData = true;
                potionData.craftedDisplayName = saved.displayName;
                potionData.temperature = (PotionTemperature)saved.temperature;
                potionData.damage1 = saved.damage1;
                potionData.damage2 = saved.damage2;
                potionData.bulletType1 = (BulletType)saved.bulletType1;
                potionData.bulletType2 = (BulletType)saved.bulletType2;

                if (!string.IsNullOrEmpty(saved.phase0IngredientId))
                {
                    potionData.phase1 = new PotionPhaseSpec { ingredientId = saved.phase0IngredientId };
                }
                if (!string.IsNullOrEmpty(saved.phase1IngredientId))
                {
                    potionData.phase2 = new PotionPhaseSpec { ingredientId = saved.phase1IngredientId };
                }
            }

            Inventory.Instance.AddPotion(potionData, saved.quantity);
        }

        Inventory.Instance.NotifyChanged();
    }

    private void RestoreWeaponSlots(SaveData data)
    {
        PlayerAttackSystem attackSystem = Player.Instance.GetComponent<PlayerAttackSystem>();
        if (attackSystem == null || data.weaponSlots == null) return;

        attackSystem.slots.Clear();

        foreach (SavedWeaponSlot saved in data.weaponSlots)
        {
            WeaponSlot slot = new WeaponSlot
            {
                type = (WeaponType)saved.weaponType
            };

            if (saved.potionIndex >= 0 && saved.potionIndex < Inventory.Instance.PotionItems.Count)
            {
                slot.equippedPotion = Inventory.Instance.PotionItems[saved.potionIndex];
            }

            attackSystem.slots.Add(slot);
        }
    }
}
