using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FinalBossHandOfTimeBurstController : MonoBehaviour
{
    private enum VerticalMove
    {
        Up = 0,
        Down = 1
    }

    [SerializeField] private FinalBossBedimmedWallProjectile projectilePrefab;
    [SerializeField] private Transform groupsParent;
    [SerializeField] private VerticalMove[] groupDirections = new VerticalMove[10];
    [SerializeField] private Vector2 fallbackHitboxSize = Vector2.one;
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float pairInterval = 1f;
    [SerializeField] private float projectileSpeed = 5f;

    private static readonly (int a, int b)[] FireOrder =
    {
        (7, 4),  // 8/5
        (6, 8),  // 7/9
        (0, 3),  // 1/4
        (5, 1),  // 6/2
        (9, 2)   // 10/3
    };

    private const int RequiredGroupCount = 10;

    private void Awake()
    {
        SetLayoutObjectsActive(false);
    }

    public void SetLayoutObjectsActive(bool active)
    {
        if (groupsParent == null) return;

        for (int i = 0; i < groupsParent.childCount; i++)
        {
            Transform group = groupsParent.GetChild(i);
            if (group != null && group.gameObject.activeSelf != active)
            {
                group.gameObject.SetActive(active);
            }
        }
    }

    public IEnumerator ExecutePattern(Transform playerTransform, int damage, List<GameObject> spawnedObjects)
    {
        if (!HasValidGroups())
        {
            Debug.LogWarning("FinalBossHandOfTimeBurstController: groupsParent needs at least 10 child groups.");
            yield break;
        }

        SetLayoutObjectsActive(true);

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            ApplySpawnDamage(playerTransform, damage);
            yield return null;
        }

        foreach (var pair in FireOrder)
        {
            SpawnGroupProjectiles(pair.a, damage, spawnedObjects);
            SpawnGroupProjectiles(pair.b, damage, spawnedObjects);
            SetGroupActive(pair.a, false);
            SetGroupActive(pair.b, false);
            yield return new WaitForSeconds(pairInterval);
        }

        SetLayoutObjectsActive(false);
    }

    private bool HasValidGroups()
    {
        return groupsParent != null && groupsParent.childCount >= RequiredGroupCount;
    }

    private void ApplySpawnDamage(Transform playerTransform, int damage)
    {
        if (playerTransform == null || groupsParent == null) return;

        for (int groupIndex = 0; groupIndex < RequiredGroupCount; groupIndex++)
        {
            Transform groupRoot = GetGroupRoot(groupIndex);
            if (groupRoot == null) continue;

            foreach (Transform slot in groupRoot)
            {
                if (slot == null) continue;
                Vector2 hitboxSize = ResolveHitboxSize(slot);
                TryDamagePlayerAtArea(playerTransform, slot.position, hitboxSize, damage);
            }
        }
    }

    private void SpawnGroupProjectiles(int groupIndex, int damage, List<GameObject> spawnedObjects)
    {
        Transform groupRoot = GetGroupRoot(groupIndex);
        if (groupRoot == null) return;

        Vector2 direction = ResolveMoveDirection(groupIndex);

        foreach (Transform slot in groupRoot)
        {
            if (slot == null) continue;

            Vector2 hitboxSize = ResolveHitboxSize(slot);
            FinalBossBedimmedWallProjectile projectile = CreateProjectile(slot.position, slot);
            projectile.Launch(direction, projectileSpeed, hitboxSize, damage, ElementType.None);
            spawnedObjects?.Add(projectile.gameObject);
        }
    }

    private Transform GetGroupRoot(int groupIndex)
    {
        if (groupsParent == null) return null;
        if (groupIndex < 0 || groupIndex >= groupsParent.childCount) return null;
        return groupsParent.GetChild(groupIndex);
    }

    private Vector2 ResolveMoveDirection(int groupIndex)
    {
        VerticalMove move = VerticalMove.Down;
        if (groupDirections != null && groupIndex >= 0 && groupIndex < groupDirections.Length)
        {
            move = groupDirections[groupIndex];
        }

        return move == VerticalMove.Up ? Vector2.up : Vector2.down;
    }

    private Vector2 ResolveHitboxSize(Transform slot)
    {
        if (slot == null)
        {
            return Vector2.one;
        }

        BoxCollider2D collider = slot.GetComponent<BoxCollider2D>();
        if (collider != null)
        {
            Vector3 scale = collider.transform.lossyScale;
            float x = Mathf.Abs(collider.size.x * scale.x);
            float y = Mathf.Abs(collider.size.y * scale.y);
            if (x > 0.01f && y > 0.01f)
            {
                return new Vector2(x, y);
            }
        }

        Vector3 slotScale = slot.lossyScale;
        Vector2 fromScale = new Vector2(Mathf.Abs(slotScale.x), Mathf.Abs(slotScale.y));
        if (fromScale.x > 0.01f && fromScale.y > 0.01f)
        {
            return fromScale;
        }

        return new Vector2(Mathf.Max(0.01f, fallbackHitboxSize.x), Mathf.Max(0.01f, fallbackHitboxSize.y));
    }

    private void SetGroupActive(int groupIndex, bool active)
    {
        Transform groupRoot = GetGroupRoot(groupIndex);
        if (groupRoot != null && groupRoot.gameObject.activeSelf != active)
        {
            groupRoot.gameObject.SetActive(active);
        }
    }

    private FinalBossBedimmedWallProjectile CreateProjectile(Vector2 position, Transform visualTemplate)
    {
        FinalBossBedimmedWallProjectile projectile;
        if (projectilePrefab != null)
        {
            projectile = Instantiate(projectilePrefab, position, Quaternion.identity);
        }
        else
        {
            GameObject go = new GameObject("HandOfTimeBurstProjectile");
            go.transform.position = position;
            go.AddComponent<BoxCollider2D>();
            projectile = go.AddComponent<FinalBossBedimmedWallProjectile>();
        }

        if (visualTemplate != null && !ProjectileAlreadyHasVisuals(projectile))
        {
            projectile.AttachVisualTemplate(visualTemplate.gameObject);
        }

        return projectile;
    }

    private static bool ProjectileAlreadyHasVisuals(FinalBossBedimmedWallProjectile projectile)
    {
        return projectile != null && projectile.GetComponentsInChildren<ParticleSystem>(true).Length > 0;
    }

    private static void TryDamagePlayerAtArea(Transform playerTransform, Vector2 center, Vector2 size, int damage)
    {
        if (playerTransform == null) return;

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f);
        foreach (Collider2D hit in hits)
        {
            if (hit == null || !hit.CompareTag("Player")) continue;
            BossHitResolver.TryApplyBossHit(hit, damage, center);
            break;
        }
    }
}
