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
    private const float FireworksPhase1FirstShotTime = 0f;
    private const float FireworksPhase2FirstShotTime = 2f;
    private const float FireworksPhase1SecondShotTime = 4f;
    private const float FireworksPhase2SecondShotTime = 6f;
    private const float AfterimagePhase1FirstShotTime = 0f;
    private const float AfterimagePhase2FirstShotTime = 3f;
    private const float AfterimagePhase1SecondShotTime = 6f;
    private const float DefaultPhase1ShotTime = 0f;
    private const float DefaultPhase2ShotTime = 2f;
    private const float AfterimageExplosionDelaySeconds = 8f;

    private readonly struct ScheduledSpawn
    {
        public ScheduledSpawn(
            float timeSeconds,
            ProjectilePatternType patternType,
            PotionPhaseSpec phase,
            int phaseIndex,
            Action<PotionProjectileController> onProjectileSpawn)
        {
            TimeSeconds = timeSeconds;
            PatternType = patternType;
            Phase = phase;
            PhaseIndex = phaseIndex;
            OnProjectileSpawn = onProjectileSpawn;
        }

        public float TimeSeconds { get; }
        public ProjectilePatternType PatternType { get; }
        public PotionPhaseSpec Phase { get; }
        public int PhaseIndex { get; }
        public Action<PotionProjectileController> OnProjectileSpawn { get; }
    }

    public static IEnumerator Run(BombPatternExecutionContext context)
    {
        List<PotionProjectileController> trackedAfterimageProjectiles = new();
        Action<PotionProjectileController> registerAfterimageProjectile = controller =>
        {
            if (controller != null && !trackedAfterimageProjectiles.Contains(controller))
            {
                trackedAfterimageProjectiles.Add(controller);
            }
        };

        List<ScheduledSpawn> schedule = BuildSchedule(context, registerAfterimageProjectile);
        if (schedule.Count == 0)
        {
            PotionPhaseSpec fallbackPhase = context.BuildFallbackPhase();
            schedule.Add(new ScheduledSpawn(
                DefaultPhase1ShotTime,
                fallbackPhase.patternType,
                fallbackPhase,
                1,
                fallbackPhase.patternType == ProjectilePatternType.AfterimageBomb ? registerAfterimageProjectile : null));
        }

        schedule.Sort((left, right) => left.TimeSeconds.CompareTo(right.TimeSeconds));

        float elapsed = 0f;
        for (int i = 0; i < schedule.Count; i++)
        {
            ScheduledSpawn spawn = schedule[i];
            float wait = Mathf.Max(0f, spawn.TimeSeconds - elapsed);
            if (wait > 0f)
            {
                yield return new WaitForSeconds(wait);
                elapsed = spawn.TimeSeconds;
            }

            context.SpawnProjectilePattern(
                spawn.PatternType,
                spawn.Phase,
                spawn.PhaseIndex,
                spawn.OnProjectileSpawn);
        }

        if (trackedAfterimageProjectiles.Count > 0)
        {
            float waitToExplosion = Mathf.Max(0f, AfterimageExplosionDelaySeconds - elapsed);
            if (waitToExplosion > 0f)
            {
                yield return new WaitForSeconds(waitToExplosion);
            }

            BombAfterimageExplosionHelper.ExplodeRemaining(
                trackedAfterimageProjectiles,
                context.BombInstanceId,
                context.BuildFallbackPhase);
        }
    }

    private static List<ScheduledSpawn> BuildSchedule(
        BombPatternExecutionContext context,
        Action<PotionProjectileController> registerAfterimageProjectile)
    {
        List<ScheduledSpawn> schedule = new();
        AddPhaseSchedule(schedule, context.Phase1, 1, registerAfterimageProjectile);
        AddPhaseSchedule(schedule, context.Phase2, 2, registerAfterimageProjectile);
        return schedule;
    }

    private static void AddPhaseSchedule(
        List<ScheduledSpawn> schedule,
        PotionPhaseSpec phase,
        int phaseIndex,
        Action<PotionProjectileController> registerAfterimageProjectile)
    {
        if (schedule == null || phase == null)
        {
            return;
        }

        Action<PotionProjectileController> onProjectileSpawn =
            phase.patternType == ProjectilePatternType.AfterimageBomb ? registerAfterimageProjectile : null;

        switch (phase.patternType)
        {
            case ProjectilePatternType.Fireworks:
                schedule.Add(new ScheduledSpawn(
                    (phaseIndex == 1 ? FireworksPhase1FirstShotTime : FireworksPhase2FirstShotTime) + Mathf.Max(0f, phase.initialSpawnDelay),
                    phase.patternType,
                    phase,
                    phaseIndex,
                    onProjectileSpawn));
                schedule.Add(new ScheduledSpawn(
                    (phaseIndex == 1 ? FireworksPhase1SecondShotTime : FireworksPhase2SecondShotTime) + Mathf.Max(0f, phase.initialSpawnDelay),
                    phase.patternType,
                    phase,
                    phaseIndex,
                    onProjectileSpawn));
                return;

            case ProjectilePatternType.AfterimageBomb:
                schedule.Add(new ScheduledSpawn(
                    phaseIndex == 1 ? AfterimagePhase1FirstShotTime : AfterimagePhase2FirstShotTime,
                    phase.patternType,
                    phase,
                    phaseIndex,
                    onProjectileSpawn));
                if (phaseIndex == 1)
                {
                    schedule.Add(new ScheduledSpawn(
                        AfterimagePhase1SecondShotTime,
                        phase.patternType,
                        phase,
                        phaseIndex,
                        onProjectileSpawn));
                }
                return;

            default:
                schedule.Add(new ScheduledSpawn(
                    phaseIndex == 1 ? DefaultPhase1ShotTime : DefaultPhase2ShotTime,
                    phase.patternType,
                    phase,
                    phaseIndex,
                    onProjectileSpawn));
                return;
        }
    }
}

internal static class BombAfterimageExplosionHelper
{
    private const float PixelsPerUnit = 32f;
    private const float AfterimageExplosionSizePx = 64f;
    private const float AfterimageFieldDurationSeconds = 2f;
    private const float AfterimageFieldDamageIntervalSeconds = 0.5f;
    private const int AfterimageFieldDamagePerTick = 50;

    public static void ExplodeRemaining(
        IReadOnlyList<PotionProjectileController> trackedProjectiles,
        int bombInstanceId,
        Func<PotionPhaseSpec> buildFallbackPhase)
    {
        if (trackedProjectiles == null || trackedProjectiles.Count == 0)
        {
            return;
        }

        float explosionSizeUnits = AfterimageExplosionSizePx / Mathf.Max(1f, PixelsPerUnit);

        for (int i = 0; i < trackedProjectiles.Count; i++)
        {
            PotionProjectileController projectile = trackedProjectiles[i];
            if (projectile == null)
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
            AfterimageFieldDurationSeconds,
            AfterimageFieldDamageIntervalSeconds,
            bombInstanceId,
            phaseIndex);
    }

    private static PotionPhaseSpec BuildExplosionSpec(PotionPhaseSpec sourcePhase, Func<PotionPhaseSpec> buildFallbackPhase)
    {
        PotionPhaseSpec source = sourcePhase ?? buildFallbackPhase();
        return new PotionPhaseSpec
        {
            ingredientId = source.ingredientId,
            temperature = source.temperature,
            patternType = ProjectilePatternType.AfterimageBomb,
            useCardinalDirections = source.useCardinalDirections,
            duration = source.duration,
            initialSpawnDelay = source.initialSpawnDelay,
            fireInterval = source.fireInterval,
            projectileSpeed = source.projectileSpeed,
            rotationSpeedDegPerSec = source.rotationSpeedDegPerSec,
            orbitAngularSpeedDegPerSec = source.orbitAngularSpeedDegPerSec,
            baseDamage = AfterimageFieldDamagePerTick,
            primaryElement = ElementType.None,
            subElement = ElementType.None,
            damageTarget = DamageTargetType.Both,
            healsPlayerOnSelfHit = false,
            ignoreSelfHitPenalty = false
        };
    }
}
