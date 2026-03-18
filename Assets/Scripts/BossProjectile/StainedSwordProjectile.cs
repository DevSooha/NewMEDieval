using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class StainedSwordProjectile : MonoBehaviour
{
    [SerializeField] private float speedPerSecond = 2f;
    [SerializeField] private float followStartDelay = 0.2f;
    [SerializeField] private float homingDuration = 2.8f;
    [SerializeField] private float fadeOutDuration = 0.1f;
    [SerializeField] private int damage = 1;

    private Transform target;
    private Action<StainedSwordProjectile> onDestroyed;
    private bool isFading;
    private bool canCollide;
    private bool isDestroyed;
    private Collider2D projectileCollider;
    private SpriteRenderer[] spriteRenderers;
    private ParticleSystem[] particleSystems;

    private void Awake()
    {
        projectileCollider = GetComponent<Collider2D>();
        projectileCollider.isTrigger = true;
        projectileCollider.enabled = false;
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
    }

    public void Initialize(Transform followTarget, Action<StainedSwordProjectile> onDestroyedCallback)
    {
        target = followTarget;
        onDestroyed = onDestroyedCallback;
        projectileCollider.enabled = false;
        isFading = false;
        canCollide = false;
        isDestroyed = false;
        gameObject.SetActive(true);

        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer == null) continue;

            Color color = spriteRenderer.color;
            color.a = 1f;
            spriteRenderer.color = color;
        }

        foreach (ParticleSystem particleSystem in particleSystems)
        {
            if (particleSystem == null) continue;
            particleSystem.Clear(true);
            particleSystem.Play(true);
        }

        StopAllCoroutines();
        StartCoroutine(HomingRoutine());
    }

    private IEnumerator HomingRoutine()
    {
        if (followStartDelay > 0f)
        {
            yield return new WaitForSeconds(followStartDelay);
        }

        projectileCollider.enabled = true;
        canCollide = true;

        float elapsed = 0f;

        while (elapsed < homingDuration)
        {
            elapsed += Time.deltaTime;

            if (target != null)
            {
                Vector3 dir = (target.position - transform.position).normalized;
                transform.position += dir * speedPerSecond * Time.deltaTime;
            }

            yield return null;
        }

        yield return FadeOutAndDestroy();
    }

    private IEnumerator FadeOutAndDestroy()
    {
        if (isFading) yield break;
        isFading = true;

        projectileCollider.enabled = false;
        canCollide = false;

        foreach (ParticleSystem particleSystem in particleSystems)
        {
            if (particleSystem == null) continue;
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / fadeOutDuration);

            foreach (SpriteRenderer spriteRenderer in spriteRenderers)
            {
                if (spriteRenderer == null) continue;

                Color color = spriteRenderer.color;
                color.a = alpha;
                spriteRenderer.color = color;
            }

            yield return null;
        }

        DestroyProjectile();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isFading || !canCollide) return;

        if (other.CompareTag("Player"))
        {
            Vector2 fallbackDirection = target != null
                ? ((Vector2)(target.position - transform.position))
                : (Vector2)transform.right;

            BossHitResolver.TryApplyBossHit(
                other,
                damage,
                transform.position,
                BossHitResolver.DefaultKnockbackDistance,
                BossHitResolver.DefaultKnockbackDuration,
                true,
                fallbackDirection
            );

            DestroyProjectile();
        }
        else if (!other.isTrigger && !other.CompareTag("Boss"))
        {
            DestroyProjectile();
        }
    }

    private void DestroyProjectile()
    {
        if (isDestroyed)
        {
            return;
        }

        isDestroyed = true;
        onDestroyed?.Invoke(this);
        onDestroyed = null;
        Destroy(gameObject);
    }

    public void DespawnImmediate()
    {
        StopAllCoroutines();
        isFading = true;
        canCollide = false;

        if (projectileCollider != null)
        {
            projectileCollider.enabled = false;
        }

        foreach (ParticleSystem particleSystem in particleSystems)
        {
            if (particleSystem == null) continue;
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        DestroyProjectile();
    }
}
