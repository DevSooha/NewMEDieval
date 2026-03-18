using UnityEngine;
using UnityEngine.Playables;

public static class PlayerDeathCleanup
{
    public static void StopAllActivePlayback()
    {
        CancelPlayerTransientState();
        StopAudioSources();
        StopPlayableDirectors();
        StopParticleSystems();
        CleanupActiveOffensives();
    }

    private static void CancelPlayerTransientState()
    {
        PlayerAttackSystem[] attackSystems = Object.FindObjectsByType<PlayerAttackSystem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < attackSystems.Length; i++)
        {
            if (attackSystems[i] == null) continue;
            attackSystems[i].CancelTransientInputState();
        }

        PlayerInteraction[] interactions = Object.FindObjectsByType<PlayerInteraction>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < interactions.Length; i++)
        {
            if (interactions[i] == null) continue;
            interactions[i].ForceCloseCraftingUI();
        }
    }

    private static void StopAudioSources()
    {
        AudioSource[] audioSources = Object.FindObjectsByType<AudioSource>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < audioSources.Length; i++)
        {
            AudioSource source = audioSources[i];
            if (source == null) continue;
            source.Stop();
        }
    }

    private static void StopPlayableDirectors()
    {
        PlayableDirector[] directors = Object.FindObjectsByType<PlayableDirector>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < directors.Length; i++)
        {
            PlayableDirector director = directors[i];
            if (director == null) continue;
            director.Stop();
            director.time = 0d;
        }
    }

    private static void StopParticleSystems()
    {
        ParticleSystem[] particleSystems = Object.FindObjectsByType<ParticleSystem>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem system = particleSystems[i];
            if (system == null) continue;
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private static void CleanupActiveOffensives()
    {
        BossProjectile[] bossProjectiles = Object.FindObjectsByType<BossProjectile>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < bossProjectiles.Length; i++)
        {
            if (bossProjectiles[i] == null) continue;
            bossProjectiles[i].DespawnImmediate();
        }

        StainedSwordProjectile[] stainedSwordProjectiles = Object.FindObjectsByType<StainedSwordProjectile>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < stainedSwordProjectiles.Length; i++)
        {
            if (stainedSwordProjectiles[i] == null) continue;
            stainedSwordProjectiles[i].DespawnImmediate();
        }

        LatentThornHitbox[] latentThorns = Object.FindObjectsByType<LatentThornHitbox>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < latentThorns.Length; i++)
        {
            if (latentThorns[i] == null) continue;
            latentThorns[i].DespawnImmediate();
        }

        CarmaExcisionTrueHitbox[] carmaHitboxes = Object.FindObjectsByType<CarmaExcisionTrueHitbox>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < carmaHitboxes.Length; i++)
        {
            if (carmaHitboxes[i] == null) continue;
            carmaHitboxes[i].DeactivateImmediate();
            DestroyGameObject(carmaHitboxes[i].gameObject);
        }

        BedimmedWall[] bedimmedWalls = Object.FindObjectsByType<BedimmedWall>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < bedimmedWalls.Length; i++)
        {
            if (bedimmedWalls[i] == null) continue;
            bedimmedWalls[i].gameObject.SetActive(false);
        }

        DestroyByComponentType<PotionProjectileController>();
        DestroyByComponentType<PotionAreaHazard>();
        DestroyByComponentType<Bomb>();
        DestroyByComponentType<HandOfTimeProjectile>();
    }

    private static void DestroyByComponentType<T>() where T : Component
    {
        T[] components = Object.FindObjectsByType<T>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null) continue;
            DestroyGameObject(component.gameObject);
        }
    }

    private static void DestroyGameObject(GameObject target)
    {
        if (target == null || !target.scene.IsValid())
        {
            return;
        }

        Object.Destroy(target);
    }
}
