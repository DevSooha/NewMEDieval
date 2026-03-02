using System;
using System.Collections;
using UnityEngine;

public readonly struct PatternSpawnRequest
{
    public readonly ProjectilePatternType PatternType;
    public readonly int PhaseIndex;
    public readonly float LineAngleDeg;
    public readonly Vector2 Direction;
    public readonly float SpeedUnitsPerSec;
    public readonly float SpawnOffsetUnits;
    public readonly Transform ParentGroup;

    public PatternSpawnRequest(
        ProjectilePatternType patternType,
        int phaseIndex,
        float lineAngleDeg,
        Vector2 direction,
        float speedUnitsPerSec,
        float spawnOffsetUnits,
        Transform parentGroup)
    {
        PatternType = patternType;
        PhaseIndex = phaseIndex;
        LineAngleDeg = lineAngleDeg;
        Direction = direction;
        SpeedUnitsPerSec = speedUnitsPerSec;
        SpawnOffsetUnits = spawnOffsetUnits;
        ParentGroup = parentGroup;
    }
}

public static class ProjectilePatternExecutor
{
    private const float EndInclusiveEpsilon = 0.0001f;
    private const float PixelPerUnit = 32f;

    private const float FireworksIntervalSeconds = 2f;
    private const float FireworksLineSpacingPx = 32f;
    private const int FireworksLineCount = 3;
    private const float FireworksSpeedPxPerFrame = 160f;

    private const float AfterimageIntervalSeconds = 3f;
    private const float AfterimageLineSpacingPx = 64f;
    private const int AfterimageLineCount = 3;
    private const float AfterimageSpeedPxPerFrame = 64f;

    private const float TornadoFireSeconds = 2f;
    private const float TornadoRotateSeconds = 6f;
    private const int TornadoShotsPerLine = 8;
    private const float TornadoLineSpacingPx = 64f;
    private const float TornadoSpeedPxPerFrame = 96f;
    private const float TornadoRotateSpeedDegPerSec = 30f;

    private static readonly float[] FireworksPlusAngles = { 0f, 90f, 180f, -90f };
    private static readonly float[] FireworksXAngles = { 45f, 135f, -135f, -45f };

    private static readonly float[] AfterimageFirstAngles = { 60f, 180f, -120f };
    private static readonly float[] AfterimageSecondAngles = { 0f, 120f, -60f };

    public static float[] GetAfterimageAngles(bool isFirstPhase)
    {
        float[] src = isFirstPhase ? AfterimageFirstAngles : AfterimageSecondAngles;
        float[] copy = new float[src.Length];
        Array.Copy(src, copy, src.Length);
        return copy;
    }

    public static IEnumerator ExecutePhase(
        PotionPhaseSpec phase,
        bool isFirstPhase,
        Transform origin,
        Action<PatternSpawnRequest> spawnProjectile,
        Action<bool, float> setGroupRotationActive = null,
        Transform parentGroup = null,
        ProjectilePatternType? forcedPatternType = null,
        float maxEndTime = float.PositiveInfinity)
    {
        if (phase == null || origin == null || spawnProjectile == null)
        {
            yield break;
        }

        ProjectilePatternType patternType = forcedPatternType ?? phase.patternType;
        int phaseIndex = isFirstPhase ? 1 : 2;
        switch (patternType)
        {
            case ProjectilePatternType.AfterimageBomb:
                yield return ExecuteAfterimageBomb(phase, phaseIndex, isFirstPhase, spawnProjectile, parentGroup, maxEndTime);
                break;
            case ProjectilePatternType.Tornado:
                yield return ExecuteTornado(phase, phaseIndex, isFirstPhase, spawnProjectile, setGroupRotationActive, parentGroup, maxEndTime);
                break;
            default:
                yield return ExecuteFireworks(phase, phaseIndex, isFirstPhase, spawnProjectile, parentGroup, maxEndTime);
                break;
        }
    }

    private static IEnumerator ExecuteFireworks(
        PotionPhaseSpec phase,
        int phaseIndex,
        bool isFirstPhase,
        Action<PatternSpawnRequest> spawnProjectile,
        Transform parentGroup,
        float maxEndTime)
    {
        float[] angles = isFirstPhase ? FireworksPlusAngles : FireworksXAngles;
        float spacing = PixelsToUnits(FireworksLineSpacingPx);
        float speed = ResolveSpeed(phase, FireworksSpeedPxPerFrame);
        float nextEventTime = Time.time;

        while (IsBeforePatternEnd(nextEventTime, maxEndTime))
        {
            if (!TryWaitUntil(nextEventTime, maxEndTime, out float waitToEvent))
            {
                yield break;
            }

            if (waitToEvent > 0f)
            {
                yield return new WaitForSeconds(waitToEvent);
            }

            EmitLineBurst(
                ProjectilePatternType.Fireworks,
                phaseIndex,
                angles,
                FireworksLineCount,
                spacing,
                speed,
                parentGroup,
                spawnProjectile);
            nextEventTime += FireworksIntervalSeconds;
        }
    }

    private static IEnumerator ExecuteAfterimageBomb(
        PotionPhaseSpec phase,
        int phaseIndex,
        bool isFirstPhase,
        Action<PatternSpawnRequest> spawnProjectile,
        Transform parentGroup,
        float maxEndTime)
    {
        float[] angles = isFirstPhase ? AfterimageFirstAngles : AfterimageSecondAngles;
        float spacing = PixelsToUnits(AfterimageLineSpacingPx);
        float speed = ResolveSpeed(phase, AfterimageSpeedPxPerFrame);
        float nextEventTime = Time.time;

        while (IsBeforePatternEnd(nextEventTime, maxEndTime))
        {
            if (!TryWaitUntil(nextEventTime, maxEndTime, out float waitToEvent))
            {
                yield break;
            }

            if (waitToEvent > 0f)
            {
                yield return new WaitForSeconds(waitToEvent);
            }

            EmitLineBurst(
                ProjectilePatternType.AfterimageBomb,
                phaseIndex,
                angles,
                AfterimageLineCount,
                spacing,
                speed,
                parentGroup,
                spawnProjectile);
            nextEventTime += AfterimageIntervalSeconds;
        }
    }

    private static IEnumerator ExecuteTornado(
        PotionPhaseSpec phase,
        int phaseIndex,
        bool isFirstPhase,
        Action<PatternSpawnRequest> spawnProjectile,
        Action<bool, float> setGroupRotationActive,
        Transform parentGroup,
        float maxEndTime)
    {
        float[] baseAngles = isFirstPhase ? AfterimageFirstAngles : AfterimageSecondAngles;
        float speed = ResolveSpeed(phase, TornadoSpeedPxPerFrame);
        float spacing = PixelsToUnits(TornadoLineSpacingPx);
        float shotInterval = TornadoFireSeconds / TornadoShotsPerLine;
        float nextShotTime = Time.time;

        for (int shot = 0; shot < TornadoShotsPerLine && IsBeforePatternEnd(nextShotTime, maxEndTime); shot++)
        {
            if (!TryWaitUntil(nextShotTime, maxEndTime, out float waitToShot))
            {
                yield break;
            }

            if (waitToShot > 0f)
            {
                yield return new WaitForSeconds(waitToShot);
            }

            float spawnOffsetUnits = spacing * shot;
            for (int i = 0; i < baseAngles.Length; i++)
            {
                float lineAngle = baseAngles[i];
                Vector2 direction = DirFromAngleUpClockwise(lineAngle);
                spawnProjectile(new PatternSpawnRequest(
                    ProjectilePatternType.Tornado,
                    phaseIndex,
                    lineAngle,
                    direction,
                    speed,
                    spawnOffsetUnits,
                    parentGroup));
            }
            nextShotTime += shotInterval;
        }

        bool rotating = false;
        if (HasTimeRemaining(maxEndTime) && setGroupRotationActive != null)
        {
            setGroupRotationActive(true, TornadoRotateSpeedDegPerSec);
            rotating = true;
        }

        float rotateElapsed = 0f;
        while (rotateElapsed < TornadoRotateSeconds && HasTimeRemaining(maxEndTime + EndInclusiveEpsilon))
        {
            if (!TryGetWaitSeconds(Mathf.Min(0.05f, TornadoRotateSeconds - rotateElapsed), maxEndTime, out float wait))
            {
                break;
            }

            yield return new WaitForSeconds(wait);
            rotateElapsed += wait;
        }

        if (rotating)
        {
            setGroupRotationActive?.Invoke(false, 0f);
        }
    }

    private static void EmitLineBurst(
        ProjectilePatternType patternType,
        int phaseIndex,
        float[] lineAngles,
        int bulletsPerLine,
        float spacingUnits,
        float speedUnitsPerSec,
        Transform parentGroup,
        Action<PatternSpawnRequest> spawnProjectile)
    {
        for (int i = 0; i < lineAngles.Length; i++)
        {
            float lineAngle = lineAngles[i];
            Vector2 direction = DirFromAngleUpClockwise(lineAngle);
            for (int n = 0; n < bulletsPerLine; n++)
            {
                spawnProjectile(new PatternSpawnRequest(
                    patternType,
                    phaseIndex,
                    lineAngle,
                    direction,
                    speedUnitsPerSec,
                    spacingUnits * n,
                    parentGroup));
            }
        }
    }

    private static bool HasTimeRemaining(float maxEndTime)
    {
        if (float.IsPositiveInfinity(maxEndTime))
        {
            return true;
        }

        return Time.time < maxEndTime - EndInclusiveEpsilon;
    }

    private static bool TryGetWaitSeconds(float requested, float maxEndTime, out float wait)
    {
        wait = Mathf.Max(0f, requested);
        if (wait <= 0f)
        {
            return false;
        }

        if (float.IsPositiveInfinity(maxEndTime))
        {
            return true;
        }

        float remaining = maxEndTime - Time.time;
        if (remaining <= 0f)
        {
            return false;
        }

        wait = Mathf.Min(wait, remaining);
        return wait > 0f;
    }

    private static float PixelsToUnits(float px)
    {
        return px / PixelPerUnit;
    }

    private static float PxPerFrameToUnitsPerSecond(float pxPerFrame)
    {
        // In-game movement integrates with Time.deltaTime, so use world units/sec.
        // Treating design pixels as per-second values avoids one-frame teleporting/flicker.
        return pxPerFrame / PixelPerUnit;
    }

    private static float ResolveSpeed(PotionPhaseSpec phase, float defaultPxPerFrame)
    {
        float specDefault = PxPerFrameToUnitsPerSecond(defaultPxPerFrame);
        if (phase != null && phase.projectileSpeed > 0f)
        {
            return Mathf.Max(phase.projectileSpeed, specDefault);
        }

        return specDefault;
    }

    private static bool TryWaitUntil(float targetTime, float maxEndTime, out float wait)
    {
        wait = 0f;

        if (float.IsPositiveInfinity(maxEndTime))
        {
            wait = Mathf.Max(0f, targetTime - Time.time);
            return true;
        }

        if (Time.time > maxEndTime + EndInclusiveEpsilon)
        {
            return false;
        }

        wait = Mathf.Max(0f, targetTime - Time.time);
        float remaining = maxEndTime - Time.time;
        if (wait - remaining > EndInclusiveEpsilon)
        {
            return false;
        }

        wait = Mathf.Min(wait, Mathf.Max(0f, remaining));
        return true;
    }

    private static bool IsBeforePatternEnd(float time, float maxEndTime)
    {
        if (float.IsPositiveInfinity(maxEndTime))
        {
            return true;
        }

        return time < maxEndTime - EndInclusiveEpsilon;
    }

    private static Vector2 DirFromAngleUpClockwise(float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)).normalized;
    }
}
