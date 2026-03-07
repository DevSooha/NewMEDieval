using UnityEngine;

public static class BombProjectilePatternSpawner
{
    private const int FireworksPatternIndex = 1;
    private const int FireworksBulletCountPerDirection = 3;
    private const float FireworksSpacingPx = 32f;
    private const float FireworksPixelsPerUnit = 32f;

    public static void Spawn(
        ProjectilePatternType patternType,
        PotionPhaseSpec phase,
        GameObject projectilePrefab,
        Transform hitOwner,
        Vector3 explosionCenter,
        Vector2 baseDirection,
        int sourceBombId,
        int phaseIndex,
        float projectileSpeed,
        float projectileLifetime)
    {
        if (phase == null)
        {
            return;
        }

        switch (patternType)
        {
            case ProjectilePatternType.Fireworks:
                SpawnPattern1Fireworks(
                    phase,
                    projectilePrefab,
                    hitOwner,
                    explosionCenter,
                    sourceBombId,
                    phaseIndex,
                    projectileSpeed,
                    projectileLifetime);
                return;

            case ProjectilePatternType.AfterimageBomb:
                // Pattern #2 placeholder: implement dedicated behavior later.
                SpawnSingleLinear(
                    patternType,
                    phase,
                    projectilePrefab,
                    hitOwner,
                    explosionCenter,
                    baseDirection,
                    sourceBombId,
                    phaseIndex,
                    projectileSpeed,
                    projectileLifetime);
                return;

            case ProjectilePatternType.Tornado:
                // Pattern #3 placeholder: implement dedicated behavior later.
                SpawnSingleLinear(
                    patternType,
                    phase,
                    projectilePrefab,
                    hitOwner,
                    explosionCenter,
                    baseDirection,
                    sourceBombId,
                    phaseIndex,
                    projectileSpeed,
                    projectileLifetime);
                return;

            default:
                SpawnSingleLinear(
                    patternType,
                    phase,
                    projectilePrefab,
                    hitOwner,
                    explosionCenter,
                    baseDirection,
                    sourceBombId,
                    phaseIndex,
                    projectileSpeed,
                    projectileLifetime);
                return;
        }
    }

    private static void SpawnPattern1Fireworks(
        PotionPhaseSpec phase,
        GameObject projectilePrefab,
        Transform hitOwner,
        Vector3 explosionCenter,
        int sourceBombId,
        int phaseIndex,
        float projectileSpeed,
        float projectileLifetime)
    {
        bool useCardinal = phaseIndex % 2 == 0;
        Vector2[] directions = GetFireworksDirections(useCardinal);
        float spacingUnits = FireworksSpacingPx / Mathf.Max(1f, FireworksPixelsPerUnit);

        for (int d = 0; d < directions.Length; d++)
        {
            Vector2 direction = directions[d];
            for (int i = 0; i < FireworksBulletCountPerDirection; i++)
            {
                Vector3 spawnPos = explosionCenter - (Vector3)(direction * spacingUnits * i);
                SpawnProjectile(
                    ProjectilePatternType.Fireworks,
                    phase,
                    projectilePrefab,
                    hitOwner,
                    spawnPos,
                    direction,
                    sourceBombId,
                    phaseIndex,
                    projectileSpeed,
                    projectileLifetime);
            }
        }

        string directionLabel = useCardinal ? "Cardinal" : "Diagonal";
        Debug.Log($"[BombPattern {FireworksPatternIndex}] Fireworks spawn | phaseIndex={phaseIndex} | direction={directionLabel} | count={directions.Length * FireworksBulletCountPerDirection}");
    }

    private static void SpawnSingleLinear(
        ProjectilePatternType patternType,
        PotionPhaseSpec phase,
        GameObject projectilePrefab,
        Transform hitOwner,
        Vector3 explosionCenter,
        Vector2 baseDirection,
        int sourceBombId,
        int phaseIndex,
        float projectileSpeed,
        float projectileLifetime)
    {
        Vector2 direction = baseDirection.sqrMagnitude > 0.0001f ? baseDirection.normalized : Vector2.up;
        if (phaseIndex == 2)
        {
            direction = -direction;
        }

        SpawnProjectile(
            patternType,
            phase,
            projectilePrefab,
            hitOwner,
            explosionCenter,
            direction,
            sourceBombId,
            phaseIndex,
            projectileSpeed,
            projectileLifetime);
    }

    private static void SpawnProjectile(
        ProjectilePatternType patternType,
        PotionPhaseSpec phase,
        GameObject projectilePrefab,
        Transform hitOwner,
        Vector3 spawnPosition,
        Vector2 direction,
        int sourceBombId,
        int phaseIndex,
        float projectileSpeed,
        float projectileLifetime)
    {
        GameObject projectileObj = projectilePrefab != null
            ? Object.Instantiate(projectilePrefab, spawnPosition, Quaternion.identity)
            : new GameObject("PotionPatternProjectile");

        if (projectilePrefab == null)
        {
            projectileObj.transform.position = spawnPosition;
        }

        PotionProjectileController controller = projectileObj.GetComponent<PotionProjectileController>();
        if (controller == null)
        {
            controller = projectileObj.AddComponent<PotionProjectileController>();
        }

        float lineAngleDeg = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        controller.Init(
            hitOwner,
            phase,
            direction,
            projectileSpeed,
            projectileLifetime,
            0f,
            null,
            projectilePrefab == null,
            false,
            sourceBombId,
            phaseIndex,
            patternType,
            lineAngleDeg);
    }

    private static Vector2[] GetFireworksDirections(bool useCardinal)
    {
        if (useCardinal)
        {
            return new[]
            {
                Vector2.up,
                Vector2.down,
                Vector2.left,
                Vector2.right
            };
        }

        return new[]
        {
            new Vector2(1f, 1f).normalized,
            new Vector2(-1f, -1f).normalized,
            new Vector2(-1f, 1f).normalized,
            new Vector2(1f, -1f).normalized
        };
    }
}
