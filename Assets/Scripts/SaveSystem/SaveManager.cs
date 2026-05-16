using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 세이브/로드를 담당하는 싱글톤 매니저.
/// Newtonsoft.Json을 사용해 Dictionary와 ISaveable 타입 정보를 보존한다.
/// 슬롯은 현재 0번만 사용하지만 API는 슬롯 인덱스를 받도록 열어 두었다.
/// </summary>
public class SaveManager : Singleton<SaveManager>
{
    // ── 직렬화 설정 ──────────────────────────────────────────────────────────
    // TypeNameHandling.Auto: worldStates 딕셔너리 값에 $type을 삽입해 역직렬화 시 원래 타입 복원
    private readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.Auto,
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
    };

    private string SaveDirectory => Path.Combine(Application.persistentDataPath, "saves");
    private string GetSavePath(int slotIndex) => Path.Combine(SaveDirectory, $"slot_{slotIndex}.json");

    private SaveData pendingLoadData;

    // ISaveable 복원을 위해 pendingLoadData는 씬 로드 후까지 보존해야 한다.
    // 사망 재시작 경로에서 Player 상태를 이미 적용했으면 ApplyPendingState에서 재적용을 막는 플래그.
    private bool pendingPlayerStateApplied;

    private const string FieldSceneName = "FIeld";

    private ItemData[] _cachedAllItems;
    private Dictionary<string, ItemData> _itemLookup;

    // ── 생명주기 ─────────────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        RoomManager.OnRoomSystemReady -= ApplyPendingState;
    }

    // ── Public API (새 포맷) ──────────────────────────────────────────────────

    public bool HasSave(int slotIndex = 0) => File.Exists(GetSavePath(slotIndex));

    /// <summary>현재 게임 상태를 지정 슬롯에 저장한다.</summary>
    public void SaveGame(string savePointId, Vector2 savePointWorldPos, int slotIndex = 0)
    {
        SaveData data = BuildSaveData(savePointId, savePointWorldPos);
        WriteToFile(data, slotIndex);
    }

    /// <summary>지정 슬롯을 로드해 SaveData를 반환한다. 없으면 null.</summary>
    public SaveData LoadGame(int slotIndex = 0)
    {
        string path = GetSavePath(slotIndex);
        if (!File.Exists(path)) return null;

        try
        {
            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<SaveData>(json, _jsonSettings);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Load failed: {e.Message}");
            return null;
        }
    }

    /// <summary>지정 슬롯 파일을 삭제한다.</summary>
    public void DeleteSave(int slotIndex = 0)
    {
        try
        {
            string path = GetSavePath(slotIndex);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Delete failed: {e.Message}");
        }
    }

    // ── 로드 데이터 적용 ──────────────────────────────────────────────────────

    /// <summary>
    /// 씬 로드 전에 호출해 데이터를 예약한다.
    /// Player가 이미 존재하면 플레이어 상태를 즉시 적용하고(사망 재시작 경로),
    /// ISaveable 복원은 씬 로드 후 방 프리로드 완료 후에 수행한다.
    /// </summary>
    public void ApplyLoadedData(SaveData data)
    {
        if (data == null) return;

        if (BossDefeatTracker.Instance != null)
            BossDefeatTracker.Instance.RestoreFromSave(data.defeatedBossIds ?? new List<string>());

        RoomManager.restartPointOverride = data.player.spawnPosition.ToVector3();

        pendingLoadData = data;
        pendingPlayerStateApplied = false;

        // 사망 재시작 경로: Player 싱글톤이 이미 씬에 존재함
        if (Player.Instance != null)
        {
            ApplyPlayerState(data);
            pendingPlayerStateApplied = true;
        }
    }

    // ── 씬 로드 타이밍 처리 ───────────────────────────────────────────────────

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (pendingLoadData == null) return;
        if (scene.name != FieldSceneName) return;

        // RoomManager.OnRoomSystemReady가 발화될 때 방과 ISaveable 오브젝트가 모두 준비됨이 보장된다.
        RoomManager.OnRoomSystemReady -= ApplyPendingState;
        RoomManager.OnRoomSystemReady += ApplyPendingState;
    }

    private void ApplyPendingState()
    {
        RoomManager.OnRoomSystemReady -= ApplyPendingState;

        if (pendingLoadData == null) return;

        if (!pendingPlayerStateApplied && Player.Instance != null)
            ApplyPlayerState(pendingLoadData);

        if (pendingLoadData.worldStates != null && pendingLoadData.worldStates.Count > 0)
            RestoreWorldStates(pendingLoadData.worldStates);

        pendingLoadData = null;
    }

    // ── 세이브 데이터 빌드 ────────────────────────────────────────────────────

    private SaveData BuildSaveData(string savePointId, Vector2 savePointWorldPos)
    {
        return new SaveData
        {
            saveVersion = "1.0",
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            lastSavePointId = savePointId,
            player = BuildPlayerData(new Vector3(savePointWorldPos.x, savePointWorldPos.y, 0f)),
            defeatedBossIds = BossDefeatTracker.Instance?.GetDefeatedBossIds() ?? new List<string>(),
            worldStates = CaptureWorldStates(),
        };
    }

    private PlayerData BuildPlayerData(Vector3 spawnPos)
    {
        PlayerData pd = new PlayerData { spawnPosition = spawnPos };

        PlayerHealth health = Player.Instance?.GetComponent<PlayerHealth>();
        if (health != null)
        {
            pd.currentHP = health.CurrentHP;
            pd.maxHP = health.MaxHP;
        }

        if (Inventory.Instance != null)
        {
            foreach (Item item in Inventory.Instance.MaterialItems)
            {
                if (item?.data == null) continue;
                pd.materialItems.Add(new SavedItem
                {
                    ingredientId = item.data.GetIngredientId(),
                    quantity = item.quantity,
                });
            }

            foreach (Potion potion in Inventory.Instance.PotionItems)
            {
                if (potion?.data == null) continue;
                pd.potionItems.Add(BuildSavedPotion(potion, isEquipped: false));
            }
        }

        PlayerAttackSystem attackSystem = Player.Instance?.GetComponent<PlayerAttackSystem>();
        if (attackSystem != null)
        {
            foreach (WeaponSlot slot in attackSystem.slots)
            {
                int potionIndex = -1;
                if (slot.equippedPotion?.data != null)
                {
                    pd.potionItems.Add(BuildSavedPotion(slot.equippedPotion, isEquipped: true));
                    potionIndex = pd.potionItems.Count - 1;
                }
                pd.weaponSlots.Add(new SavedWeaponSlot
                {
                    weaponType = (int)slot.type,
                    potionIndex = potionIndex,
                });
            }
        }

        return pd;
    }

    private Dictionary<string, object> CaptureWorldStates()
    {
        var states = new Dictionary<string, object>();

        foreach (ISaveable saveable in FindObjectsByType<ISaveable>(FindObjectsSortMode.None))
        {
            if (string.IsNullOrEmpty(saveable.SaveId))
            {
                Debug.LogWarning($"[SaveManager] ISaveable on '{((MonoBehaviour)saveable).gameObject.name}'에 SaveId가 없어 건너뜁니다.");
                continue;
            }
            states[saveable.SaveId] = saveable.CaptureState();
        }

        return states;
    }

    private void RestoreWorldStates(Dictionary<string, object> worldStates)
    {
        foreach (ISaveable saveable in FindObjectsByType<ISaveable>(FindObjectsSortMode.None))
        {
            if (worldStates.TryGetValue(saveable.SaveId, out object state))
                saveable.RestoreState(state);
        }
    }

    // ── 파일 I/O ──────────────────────────────────────────────────────────────

    private void WriteToFile(SaveData data, int slotIndex)
    {
        try
        {
            Directory.CreateDirectory(SaveDirectory);
            string json = JsonConvert.SerializeObject(data, _jsonSettings);
            File.WriteAllText(GetSavePath(slotIndex), json);
            Debug.Log($"[SaveManager] slot_{slotIndex}에 저장 완료.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Save failed: {e.Message}");
        }
    }

    // ── 플레이어 상태 적용 ────────────────────────────────────────────────────

    private void ApplyPlayerState(SaveData data)
    {
        PlayerHealth health = Player.Instance.GetComponent<PlayerHealth>();
        health?.Resurrect();

        Player.Instance.SetSavedPosition(data.player.spawnPosition.ToVector3());

        RestoreInventory(data.player.materialItems, data.player.potionItems);
        RestoreWeaponSlots(data.player.weaponSlots, data.player.potionItems);
    }

    // ── 인벤토리·무기 복원 ────────────────────────────────────────────────────

    private void RestoreInventory(List<SavedItem> savedItems, List<SavedPotion> savedPotions)
    {
        if (Inventory.Instance == null) return;

        // 기존 인벤토리 전체 초기화
        List<Item> materials = Inventory.Instance.MaterialItems;
        while (materials.Count > 0) Inventory.Instance.RemoveItemCompletely(materials[0]);

        List<Potion> potions = Inventory.Instance.PotionItems;
        while (potions.Count > 0) Inventory.Instance.RemovePotionCompletely(potions[0]);

        // 재료 복원
        if (savedItems != null)
        {
            if (_cachedAllItems == null)
            {
                _cachedAllItems = Resources.LoadAll<ItemData>("ItemData");
                _itemLookup = new Dictionary<string, ItemData>(_cachedAllItems.Length);
                foreach (ItemData d in _cachedAllItems) _itemLookup[d.GetIngredientId()] = d;
            }
            foreach (SavedItem saved in savedItems)
            {
                if (_itemLookup.TryGetValue(saved.ingredientId, out ItemData match))
                    Inventory.Instance.AddItem(match, saved.quantity);
                else
                    Debug.LogWarning($"[SaveManager] 아이템을 찾을 수 없음: {saved.ingredientId}");
            }
        }

        // 포션 복원 (슬롯 장착 포션은 RestoreWeaponSlots에서 처리)
        if (savedPotions != null)
        {
            foreach (SavedPotion saved in savedPotions)
            {
                if (saved.isEquippedInSlot) continue;
                Potion rebuilt = RebuildPotion(saved);
                if (rebuilt != null) Inventory.Instance.AddPotion(rebuilt.data, rebuilt.quantity);
            }
        }

        Inventory.Instance.NotifyChanged();
    }

    private void RestoreWeaponSlots(List<SavedWeaponSlot> savedSlots, List<SavedPotion> savedPotions)
    {
        PlayerAttackSystem attackSystem = Player.Instance?.GetComponent<PlayerAttackSystem>();
        if (attackSystem == null || savedSlots == null) return;

        attackSystem.slots.Clear();
        foreach (SavedWeaponSlot saved in savedSlots)
        {
            WeaponSlot slot = new WeaponSlot { type = (WeaponType)saved.weaponType };

            if (saved.potionIndex >= 0 && savedPotions != null && saved.potionIndex < savedPotions.Count)
                slot.equippedPotion = RebuildPotion(savedPotions[saved.potionIndex]);

            attackSystem.slots.Add(slot);
        }
    }

    // ── 포션 DTO 변환 ─────────────────────────────────────────────────────────

    private static SavedPotion BuildSavedPotion(Potion potion, bool isEquipped)
    {
        SavedPotion sp = new SavedPotion
        {
            displayName = potion.data.GetDisplayName(),
            temperature = (int)potion.data.temperature,
            damage1 = potion.data.damage1,
            damage2 = potion.data.damage2,
            bulletType1 = (int)potion.data.bulletType1,
            bulletType2 = (int)potion.data.bulletType2,
            quantity = potion.quantity,
            isCraftedRuntime = potion.data.isCraftedRuntimeData,
            isEquippedInSlot = isEquipped,
        };

        PotionPhaseSpec phase0 = potion.data.GetPhase(0);
        PotionPhaseSpec phase1 = potion.data.GetPhase(1);
        sp.phase0IngredientId = phase0?.ingredientId ?? string.Empty;
        sp.phase1IngredientId = phase1?.ingredientId ?? string.Empty;

        if (!potion.data.isCraftedRuntimeData)
            sp.potionAssetName = potion.data.name;

        return sp;
    }

    private static Potion RebuildPotion(SavedPotion saved)
    {
        if (saved == null) return null;

        PotionData potionData;

        if (!saved.isCraftedRuntime && !string.IsNullOrEmpty(saved.potionAssetName))
        {
            potionData = Resources.Load<PotionData>(saved.potionAssetName);
            if (potionData == null)
            {
                Debug.LogWarning($"[SaveManager] 포션 에셋을 찾을 수 없음: {saved.potionAssetName}");
                return null;
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
                potionData.phase1 = new PotionPhaseSpec { ingredientId = saved.phase0IngredientId };
            if (!string.IsNullOrEmpty(saved.phase1IngredientId))
                potionData.phase2 = new PotionPhaseSpec { ingredientId = saved.phase1IngredientId };
        }

        return new Potion(potionData, saved.quantity);
    }
}
