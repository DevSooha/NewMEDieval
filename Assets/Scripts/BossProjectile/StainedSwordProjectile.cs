using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class StainedSwordProjectile : MonoBehaviour
{
    [SerializeField] private float speedPerSecond = 2f;
    [SerializeField] private float homingDuration = 2.8f;
    [SerializeField] private float fadeOutDuration = 0.1f;
    [SerializeField] private int damage = 1;

    private Transform target;
    private Action<StainedSwordProjectile> onDestroyed;
    private bool isFading;
    private Collider2D projectileCollider;
    private SpriteRenderer[] spriteRenderers;

    public void Initialize(Transform followTarget, Action<StainedSwordProjectile> onDestroyedCallback)
    {
        target = followTarget;
        onDestroyed = onDestroyedCallback;

        projectileCollider = GetComponent<Collider2D>();
        projectileCollider.isTrigger = true;

        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        StopAllCoroutines();
        StartCoroutine(HomingRoutine());
    }

    private IEnumerator HomingRoutine()
    {
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

        if (projectileCollider != null)
        {
            projectileCollider.enabled = false;
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
        if (isFading) return;

        if (other.CompareTag("Player"))
        {
            PlayerHealth health = other.GetComponent<PlayerHealth>();
            if (health == null) health = other.GetComponentInParent<PlayerHealth>();

            if (health != null)
            {
                health.TakeDamage(damage);
            }

            DestroyProjectile();
        }
        else if (!other.isTrigger && !other.CompareTag("Boss"))
        {
            DestroyProjectile();
        }
    }

    private void DestroyProjectile()
    {
        onDestroyed?.Invoke(this);
        Destroy(gameObject);
    }
}
