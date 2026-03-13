using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class PlayerAttackSystem
{
    private const int MaxBombStacks = 3;

    void HandleBombInput()
    {
        if (!isCharging && GetCurrentBombAmmoCount() <= 0)
        {
            if (enableAttackDiagnostics && IsAttackPressed())
            {
                LogAttackWarning("Bomb input ignored: no ammo in current potion slot.");
            }
            return;
        }

        if (IsAttackPressed() && !isCharging)
        {
            isCharging = true;
            chargeStartTime = Time.time;
            currentStack = 0;
            ClearMarkers();
            if (playerMovement != null) playerMovement.SetCanMove(false);
            if (chargeRoutine != null)
            {
                StopCoroutine(chargeRoutine);
            }
            chargeRoutine = StartCoroutine(ChargeRoutine());
        }

        if (IsAttackReleased() && isCharging)
        {
            isCharging = false;
            if (chargeRoutine != null)
            {
                StopCoroutine(chargeRoutine);
                chargeRoutine = null;
            }
            if (playerMovement != null) playerMovement.SetCanMove(true);

            float duration = Time.time - chargeStartTime;
            int ammoBeforeSpawn = GetCurrentBombAmmoCount();
            int maxCount = GetSpawnableStackLimit(duration, allowShortPressSingleBomb: true);
            if (enableAttackDiagnostics)
            {
                Debug.Log($"[AttackSystem] Bomb release | hold={duration:0.00}s | targetStack={maxCount} | ammo={ammoBeforeSpawn}", this);
            }
            int spawnedCount = SpawnBombsByStack(maxCount);

            if (spawnedCount > 0)
            {
                UseAmmo(spawnedCount);
            }
            else if (maxCount > 0 && enableAttackDiagnostics)
            {
                LogAttackWarning($"Bomb release produced no spawn. charge={duration:0.00}s targetStack={maxCount}");
            }

            ClearMarkers();
        }
    }

    IEnumerator ChargeRoutine()
    {
        while (isCharging)
        {
            float t = Time.time - chargeStartTime;
            int nextTargetStack = GetSpawnableStackLimit(t);

            while (currentStack < nextTargetStack)
            {
                currentStack++;
                ShowStackMarker(currentStack);
            }

            while (currentStack > nextTargetStack)
            {
                HideLastStackMarker();
                currentStack--;
            }

            yield return null;
        }
    }

    int GetChargeStackByTime(float elapsed)
    {
        if (elapsed >= bombThirdStackThreshold) return MaxBombStacks;
        if (elapsed >= bombSecondStackThreshold) return 2;
        if (elapsed >= bombShortPressThreshold) return 1;
        return 0;
    }

    int GetReachableStackLimit()
    {
        int reachable = 0;
        for (int i = 1; i <= MaxBombStacks; i++)
        {
            if (CanPlaceBombAtDistance(i))
            {
                reachable = i;
            }
            else
            {
                break;
            }
        }
        return reachable;
    }

    int GetCurrentBombAmmoCount()
    {
        if (slots == null || slots.Count == 0)
        {
            return 0;
        }

        WeaponSlot slot = slots[0];
        if (slot == null || slot.type != WeaponType.PotionBomb)
        {
            return 0;
        }

        if (slot.equippedPotion != null)
        {
            return Mathf.Max(0, slot.equippedPotion.quantity);
        }

        return Mathf.Max(0, slot.count);
    }

    int GetSpawnableStackLimit(float elapsed, bool allowShortPressSingleBomb = false)
    {
        int targetByTime = allowShortPressSingleBomb && elapsed < bombShortPressThreshold
            ? 1
            : GetChargeStackByTime(elapsed);
        int ammoCap = GetCurrentBombAmmoCount();
        return Mathf.Clamp(Mathf.Min(targetByTime, ammoCap, MaxBombStacks), 0, MaxBombStacks);
    }

    void ShowStackMarker(int stackIndex)
    {
        if (stackMarkerPrefab == null)
        {
            LogAttackWarning("stackMarkerPrefab is null. Cannot show charge marker.");
            return;
        }

        if (!TryGetPlacementPositionAtDistance(stackIndex, out Vector2 spawnPos))
        {
            LogAttackWarning($"Cannot place stack marker at distance {stackIndex}: no valid ground tile.");
            return;
        }

        GameObject marker = Instantiate(stackMarkerPrefab, spawnPos, Quaternion.identity);
        ConfigureStackMarker(marker, stackIndex);
        activeMarkers.Add(marker);
    }

    void HideLastStackMarker()
    {
        if (activeMarkers.Count == 0)
        {
            return;
        }

        int lastIndex = activeMarkers.Count - 1;
        GameObject marker = activeMarkers[lastIndex];
        if (marker != null)
        {
            Destroy(marker);
        }

        activeMarkers.RemoveAt(lastIndex);
    }

    void ClearMarkers()
    {
        foreach (GameObject marker in activeMarkers)
        {
            if (marker != null) Destroy(marker);
        }
        activeMarkers.Clear();
    }

    bool SpawnBombAt(int distance)
    {
        if (slots.Count == 0) return false;
        if (slots[0].type != WeaponType.PotionBomb) return false;

        if (!TryGetPlacementPositionAtDistance(distance, out Vector2 spawnPos))
        {
            LogAttackWarning($"Bomb spawn blocked at stack {distance}: no valid ground tile.");
            return false;
        }

        if (IsBombPlacementBlocked(spawnPos))
        {
            LogAttackWarning($"Bomb spawn blocked at stack {distance}: obstacle around ({spawnPos.x:0.00}, {spawnPos.y:0.00}).");
            return false;
        }

        WeaponSlot slot = slots[0];
        GameObject prefabToUse = slot.specificPrefab != null ? slot.specificPrefab : defaultBombPrefab;
        if (prefabToUse == null)
        {
            LogAttackWarning($"Bomb spawn failed at stack {distance}: bomb prefab is null.");
            return false;
        }

        GameObject bombObj = Instantiate(prefabToUse, spawnPos, Quaternion.identity);
        if (bombObj == null)
        {
            LogAttackWarning($"Bomb instantiate failed at stack {distance}.");
            return false;
        }

        FieldSceneScaleUtility.ApplyIfNeeded(bombObj);

        Bomb bomb = bombObj.GetComponent<Bomb>();
        if (bomb != null && slot.equippedPotion != null && slot.equippedPotion.data != null)
        {
            bomb.ConfigureFromPotionData(slot.equippedPotion.data);
        }
        else if (enableAttackDiagnostics && bomb == null)
        {
            LogAttackWarning($"Spawned prefab '{prefabToUse.name}' has no Bomb component.");
        }

        return true;
    }

    int SpawnBombsByStack(int maxCount)
    {
        if (maxCount <= 0)
        {
            return 0;
        }

        int clampedMaxCount = Mathf.Clamp(maxCount, 0, MaxBombStacks);
        int spawnedCount = 0;
        for (int i = 1; i <= clampedMaxCount; i++)
        {
            if (!SpawnBombAt(i))
            {
                break;
            }

            spawnedCount++;
        }

        return spawnedCount;
    }

    bool CanPlaceBombAtDistance(int distance)
    {
        if (!TryGetPlacementPositionAtDistance(distance, out Vector2 spawnPos))
        {
            return false;
        }

        return !IsBombPlacementBlocked(spawnPos);
    }

    private bool TryGetPlacementPositionAtDistance(int distance, out Vector2 placementPos)
    {
        placementPos = transform.position;

        if (distance <= 0)
        {
            return false;
        }

        Vector2 direction = GetAimDirection();
        if (direction.sqrMagnitude <= 0.0001f && playerMovement != null)
        {
            direction = playerMovement.LastMoveDirection;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector2.down;
        }

        placementPos = (Vector2)transform.position + (direction.normalized * tileSize * distance);
        return true;
    }

    bool IsBombPlacementBlocked(Vector2 pos)
    {
        if (bombBlockLayer.value == 0)
        {
            return false;
        }

        float radius = Mathf.Max(0.05f, bombBlockCheckRadius);
        Collider2D[] hits = Physics2D.OverlapCircleAll(pos, radius, bombBlockLayer);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null) continue;
            if (hit.isTrigger) continue;
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            if (IsEnemyMonsterBossHit(hit)) continue;
            Tilemap hitTilemap = hit.GetComponentInParent<Tilemap>();
            if (hitTilemap != null && IsGroundTilemap(hitTilemap)) continue;
            return true;
        }

        return false;
    }

    private static bool IsGroundTilemap(Tilemap tilemap)
    {
        if (tilemap == null)
        {
            return false;
        }

        GameObject owner = tilemap.gameObject;
        if (owner == null)
        {
            return false;
        }

        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        int wallLayer = LayerMask.NameToLayer("Wall");
        int ownerLayer = owner.layer;
        if ((obstacleLayer >= 0 && ownerLayer == obstacleLayer)
            || (wallLayer >= 0 && ownerLayer == wallLayer))
        {
            return false;
        }

        string objectName = tilemap.name;
        if (!string.IsNullOrEmpty(objectName)
            && (objectName.IndexOf("wall", StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("obstacle", StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("collision", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            return false;
        }

        string tagName = owner.tag;
        if (!string.IsNullOrEmpty(tagName)
            && (tagName.IndexOf("obstacle", StringComparison.OrdinalIgnoreCase) >= 0
                || tagName.IndexOf("wall", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            return false;
        }

        if (owner.CompareTag("Ground"))
        {
            return true;
        }

        if (HasIdentityInHierarchy(tilemap.transform, "Ground", "ground")
            || HasIdentityInHierarchy(tilemap.transform, "Floor", "floor"))
        {
            return true;
        }

        // Fallback: allow generic tilemaps so room-specific names (e.g., spr_*) still work.
        return owner.GetComponent<TilemapRenderer>() != null;
    }

    private static bool IsEnemyMonsterBossHit(Collider2D hit)
    {
        if (hit == null)
        {
            return false;
        }

        if (hit.GetComponentInParent<EnemyCombat>() != null)
        {
            return true;
        }

        if (hit.GetComponentInParent<BossHealth>() != null)
        {
            return true;
        }

        Transform t = hit.transform;
        return HasIdentityInHierarchy(t, "Enemy", "enemy")
            || HasIdentityInHierarchy(t, "Monster", "monster")
            || HasIdentityInHierarchy(t, "Boss", "boss");
    }

    private static bool HasIdentityInHierarchy(Transform transformNode, string tagKeyword, string nameKeyword)
    {
        Transform current = transformNode;
        while (current != null)
        {
            if (!string.IsNullOrEmpty(tagKeyword))
            {
                string tagName = current.tag;
                if (!string.IsNullOrEmpty(tagName)
                    && tagName.IndexOf(tagKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(nameKeyword))
            {
                string objectName = current.name;
                if (!string.IsNullOrEmpty(objectName)
                    && objectName.IndexOf(nameKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            current = current.parent;
        }

        return false;
    }

    private void ConfigureStackMarker(GameObject marker, int stackIndex)
    {
        if (marker == null)
        {
            return;
        }

        if (!forceStackMarkerSorting)
        {
            return;
        }

        string resolvedLayerName = ResolveSortingLayerName(stackMarkerSortingLayerName, fallback: "Objects");
        int baseOrder = stackMarkerSortingOrder + Mathf.Max(0, stackIndex - 1);

        SpriteRenderer[] spriteRenderers = marker.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.sortingLayerName = resolvedLayerName;
            renderer.sortingOrder = baseOrder;
            renderer.enabled = true;
        }

        ParticleSystemRenderer[] particleRenderers = marker.GetComponentsInChildren<ParticleSystemRenderer>(true);
        for (int i = 0; i < particleRenderers.Length; i++)
        {
            ParticleSystemRenderer renderer = particleRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.sortingLayerName = resolvedLayerName;
            renderer.sortingOrder = baseOrder;
            renderer.enabled = true;
        }

        if (spriteRenderers.Length == 0 && particleRenderers.Length == 0)
        {
            LogAttackWarning($"Stack marker '{marker.name}' has no renderer components.");
        }
    }

    private string ResolveSortingLayerName(string requested, string fallback)
    {
        string requestedName = string.IsNullOrWhiteSpace(requested) ? fallback : requested;
        int requestedId = SortingLayer.NameToID(requestedName);
        if (requestedId != 0 || string.Equals(requestedName, "Default", StringComparison.Ordinal))
        {
            return requestedName;
        }

        int fallbackId = SortingLayer.NameToID(fallback);
        if (fallbackId != 0 || string.Equals(fallback, "Default", StringComparison.Ordinal))
        {
            LogAttackWarning($"Sorting layer '{requestedName}' not found. Falling back to '{fallback}'.");
            return fallback;
        }

        LogAttackWarning($"Sorting layer '{requestedName}' not found. Falling back to 'Default'.");
        return "Default";
    }

    private void LogAttackWarning(string message)
    {
        if (!enableAttackDiagnostics)
        {
            return;
        }

        Debug.LogWarning($"[AttackSystem] {message}", this);
    }
}
