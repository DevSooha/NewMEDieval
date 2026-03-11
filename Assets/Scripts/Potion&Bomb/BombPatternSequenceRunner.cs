using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal sealed class BombPatternExecutionContext
{
    public BombPatternExecutionContext(
        PotionPhaseSpec phase1,
        PotionPhaseSpec phase2,
        int bombInstanceId,
        Func<PotionPhaseSpec> buildFallbackPhase,
        Action<ProjectilePatternType, PotionPhaseSpec, int, Action<PotionProjectileController>> spawnProjectilePattern)
    {
        Phase1 = phase1;
        Phase2 = phase2;
        BombInstanceId = bombInstanceId;
        BuildFallbackPhase = buildFallbackPhase;
        SpawnProjectilePattern = spawnProjectilePattern;
    }

    public PotionPhaseSpec Phase1 { get; }
    public PotionPhaseSpec Phase2 { get; }
    public int BombInstanceId { get; }
    public Func<PotionPhaseSpec> BuildFallbackPhase { get; }
    public Action<ProjectilePatternType, PotionPhaseSpec, int, Action<PotionProjectileController>> SpawnProjectilePattern { get; }
}

internal static class BombPatternSequenceRunner
{
    private const float FireworksStepDelaySeconds = 2f;
    private const float AfterimageFirstShotTime = 0f;
    private const float AfterimageSecondShotTime = 3f;
    private const float AfterimageThirdShotTime = 6f;
    private const float AfterimageExplosionDelaySeconds = 8f;

    public static IEnumerator Run(BombPatternExecutionContext context)
    {
        PotionPhaseSpec drivingPhase = context.Phase1 ?? context.Phase2 ?? context.BuildFallbackPhase();
        switch (drivingPhase.patternType)
        {
            case ProjectilePatternType.Fireworks:
                yield return RunFireworks(context);
                break;

            case ProjectilePatternType.AfterimageBomb:
                yield return RunAfterimageBomb(context);
                break;

            default:
            {
                int defaultPhaseIndex = context.Phase1 != null ? 1 : (context.Phase2 != null ? 2 : 1);
                context.SpawnProjectilePattern(
                    drivingPhase.patternType,
                    drivingPhase,
                    defaultPhaseIndex,
                    null);
                break;
            }
        }
    }

    private static IEnumerator RunFireworks(BombPatternExecutionContext context)
    {
        context.SpawnProjectilePattern(ProjectilePatternType.Fireworks, ResolveMaterialPhase(context.Phase1, context.Phase2, 1), 1, null);
        yield return new WaitForSeconds(FireworksStepDelaySeconds);

        context.SpawnProjectilePattern(ProjectilePatternType.Fireworks, ResolveMaterialPhase(context.Phase1, context.Phase2, 2), 2, null);
        yield return new WaitForSeconds(FireworksStepDelaySeconds);

        context.SpawnProjectilePattern(ProjectilePatternType.Fireworks, ResolveMaterialPhase(context.Phase1, context.Phase2, 1), 1, null);
        yield return new WaitForSeconds(FireworksStepDelaySeconds);

        context.SpawnProjectilePattern(ProjectilePatternType.Fireworks, ResolveMaterialPhase(context.Phase1, context.Phase2, 2), 2, null);
    }

    private static IEnumerator RunAfterimageBomb(BombPatternExecutionContext context)
    {
        List<PotionProjectileController> trackedProjectiles = new();
        Action<PotionProjectileController> registerProjectile = controller =>
        {
            if (controller != null && !trackedProjectiles.Contains(controller))
            {
                trackedProjectiles.Add(controller);
            }
        };

        context.SpawnProjectilePattern(
            ProjectilePatternType.AfterimageBomb,
            ResolveMaterialPhase(context.Phase1, context.Phase2, 1),
            1,
            registerProjectile);

        float waitToSecond = Mathf.Max(0f, AfterimageSecondShotTime - AfterimageFirstShotTime);
        if (waitToSecond > 0f)
        {
            yield return new WaitForSeconds(waitToSecond);
        }

        context.SpawnProjectilePattern(
            ProjectilePatternType.AfterimageBomb,
            ResolveMaterialPhase(context.Phase1, context.Phase2, 2),
            2,
            registerProjectile);

        float waitToThird = Mathf.Max(0f, AfterimageThirdShotTime - AfterimageSecondShotTime);
        if (waitToThird > 0f)
        {
            yield return new WaitForSeconds(waitToThird);
        }

        context.SpawnProjectilePattern(
            ProjectilePatternType.AfterimageBomb,
            ResolveMaterialPhase(context.Phase1, context.Phase2, 1),
            1,
            registerProjectile);

        float waitToExplosion = Mathf.Max(0f, AfterimageExplosionDelaySeconds - AfterimageThirdShotTime);
        if (waitToExplosion > 0f)
        {
            yield return new WaitForSeconds(waitToExplosion);
        }

        BombAfterimageExplosionHelper.ExplodeRemaining(
            trackedProjectiles,
            context.BombInstanceId,
            context.BuildFallbackPhase);
    }

    private static PotionPhaseSpec ResolveMaterialPhase(PotionPhaseSpec phase1, PotionPhaseSpec phase2, int materialIndex)
    {
        if (materialIndex == 2)
        {
            return phase2 ?? phase1;
        }

        return phase1 ?? phase2;
    }
}

internal static class BombAfterimageExplosionHelper
{
    private const float PixelsPerUnit = 32f;
    private const float AfterimageExplosionSizePx = 64f;
    private const float AfterimageExplosionLifetimeSeconds = 0.08f;

    public static void ExplodeRemaining(
        IReadOnlyList<PotionProjectileController> trackedProjectiles,
        int bombInstanceId,
        Func<PotionPhaseSpec> buildFallbackPhase)
    {
        if (trackedProjectiles == null || trackedProjectiles.Count == 0)
        {
            return;
        }

        Camera cam = Camera.main;
        float explosionSizeUnits = AfterimageExplosionSizePx / Mathf.Max(1f, PixelsPerUnit);

        for (int i = 0; i < trackedProjectiles.Count; i++)
        {
            PotionProjectileController projectile = trackedProjectiles[i];
            if (projectile == null)
            {
                continue;
            }

            if (cam != null && !IsOnScreen(cam, projectile.transform.position))
            {
                continue;
            }

            SpawnExplosion(
                projectile.transform.position,
                BuildExplosionSpec(projectile.PhaseSpec, buildFallbackPhase),
                projectile.PhaseIndex,
                explosionSizeUnits,
                bombInstanceId);

            UnityEngine.Object.Destroy(projectile.gameObject);
        }
    }

    private static bool IsOnScreen(Camera cam, Vector3 position)
    {
        Vector3 viewport = cam.WorldToViewportPoint(position);
        return viewport.z > 0f
               && viewport.x >= 0f && viewport.x <= 1f
               && viewport.y >= 0f && viewport.y <= 1f;
    }

    private static void SpawnExplosion(
        Vector3 worldPosition,
        PotionPhaseSpec sourcePhase,
        int phaseIndex,
        float explosionSizeUnits,
        int bombInstanceId)
    {
        GameObject hazardObject = new GameObject("AfterimageExplosionHazard");
        hazardObject.transform.position = worldPosition;

        PotionAreaHazard hazard = hazardObject.AddComponent<PotionAreaHazard>();
        hazard.Init(
            sourcePhase,
            new Vector2(explosionSizeUnits, explosionSizeUnits),
            AfterimageExplosionLifetimeSeconds,
            bombInstanceId,
            phaseIndex);
    }

    private static PotionPhaseSpec BuildExplosionSpec(PotionPhaseSpec sourcePhase, Func<PotionPhaseSpec> buildFallbackPhase)
    {
        PotionPhaseSpec source = sourcePhase ?? buildFallbackPhase();
        PotionPhaseSpec explosion = new PotionPhaseSpec
        {
            ingredientId = source.ingredientId,
            patternType = ProjectilePatternType.AfterimageBomb,
            useCardinalDirections = source.useCardinalDirections,
            duration = source.duration,
            fireInterval = source.fireInterval,
            projectileSpeed = source.projectileSpeed,
            rotationSpeedDegPerSec = source.rotationSpeedDegPerSec,
            baseDamage = source.baseDamage,
            primaryElement = source.primaryElement,
            subElement = source.subElement,
            damageTarget = DamageTargetType.Both,
            healsPlayerOnSelfHit = source.healsPlayerOnSelfHit,
            ignoreSelfHitPenalty = source.ignoreSelfHitPenalty
        };

        CopyEffects(source.onPlayerHitEffects, explosion.onPlayerHitEffects);
        CopyEffects(source.onEnemyHitEffects, explosion.onEnemyHitEffects);
        return explosion;
    }

    private static void CopyEffects(List<StatusEffectSpec> source, List<StatusEffectSpec> destination)
    {
        if (source == null || destination == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            StatusEffectSpec effect = source[i];
            if (effect == null)
            {
                continue;
            }

            destination.Add(new StatusEffectSpec
            {
                effectType = effect.effectType,
                duration = effect.duration,
                magnitude = effect.magnitude,
                interval = effect.interval
            });
        }
    }
}
