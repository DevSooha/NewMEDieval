using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 플레이어 사망 시 씬 전체의 진행 중인 연출/공격 오브젝트를 일괄 정지·제거하는 정적 유틸.
/// PlayerHealth.Die()에서만 호출된다 (MonoBehaviour 수명주기 없음).
/// </summary>
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
        // 사망 순간 열려 있던 UI/입력 상태가 부활 후까지 남지 않도록 비활성 오브젝트까지 포함해 정리
        ForEachComponent<PlayerAttackSystem>(FindObjectsInactive.Include,
            system => system.CancelTransientInputState());

        ForEachComponent<PlayerInteraction>(FindObjectsInactive.Include,
            interaction => interaction.ForceCloseCraftingUI());
    }

    private static void StopAudioSources()
    {
        ForEachComponent<AudioSource>(FindObjectsInactive.Exclude,
            source => source.Stop());
    }

    private static void StopPlayableDirectors()
    {
        ForEachComponent<PlayableDirector>(FindObjectsInactive.Exclude,
            director =>
            {
                director.Stop();
                director.time = 0d;
            });
    }

    private static void StopParticleSystems()
    {
        ForEachComponent<ParticleSystem>(FindObjectsInactive.Exclude,
            system => system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear));
    }

    private static void CleanupActiveOffensives()
    {
        // 풀링되는 투사체는 Destroy가 아닌 DespawnImmediate(풀 반환)로 정리해야 풀이 오염되지 않는다
        ForEachComponent<BossProjectile>(FindObjectsInactive.Exclude,
            projectile => projectile.DespawnImmediate());

        ForEachComponent<StainedSwordProjectile>(FindObjectsInactive.Exclude,
            projectile => projectile.DespawnImmediate());

        ForEachComponent<LatentThornHitbox>(FindObjectsInactive.Exclude,
            thorn => thorn.DespawnImmediate());

        // Carma 히트박스는 즉시 비활성화 후 파괴까지 필요 (재사용되지 않는 일회성 오브젝트)
        ForEachComponent<CarmaExcisionTrueHitbox>(FindObjectsInactive.Exclude,
            hitbox =>
            {
                hitbox.DeactivateImmediate();
                DestroyGameObject(hitbox.gameObject);
            });

        // BedimmedWall은 보스가 재사용하므로 파괴하지 않고 끄기만 한다
        ForEachComponent<BedimmedWall>(FindObjectsInactive.Exclude,
            wall => wall.gameObject.SetActive(false));

        DestroyByComponentType<PotionProjectileController>();
        DestroyByComponentType<PotionAreaHazard>();
        DestroyByComponentType<Bomb>();
        DestroyByComponentType<HandOfTimeProjectile>();
    }

    private static void ForEachComponent<T>(FindObjectsInactive inactive, System.Action<T> action) where T : Component
    {
        T[] components = Object.FindObjectsByType<T>(inactive, FindObjectsSortMode.None);

        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
            {
                continue;
            }

            action(components[i]);
        }
    }

    private static void DestroyByComponentType<T>() where T : Component
    {
        ForEachComponent<T>(FindObjectsInactive.Exclude,
            component => DestroyGameObject(component.gameObject));
    }

    private static void DestroyGameObject(GameObject target)
    {
        // 씬에 속하지 않은 오브젝트(프리팹 에셋 등)는 Destroy 대상이 아니다
        if (target == null || !target.scene.IsValid())
        {
            return;
        }

        Object.Destroy(target);
    }
}
