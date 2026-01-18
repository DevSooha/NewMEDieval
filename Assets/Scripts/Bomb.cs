using System.Collections;
using UnityEngine;

public class Bomb : MonoBehaviour
{
    [Header("Bomb Settings")]
    public ElementType bombElement = ElementType.Water;
    public int baseDamage = 200;
    public float timeToExplode = 2.0f;
    public float explosionRadius = 1.5f;
    public GameObject explosionEffect;

    void Start() { StartCoroutine(ExplodeSequence()); }

    IEnumerator ExplodeSequence()
    {
        yield return new WaitForSeconds(timeToExplode);
        Explode();
    }

    void Explode()
    {
        // Æø¹ß ÀÌÆåÆ® »ý¼º
        if (explosionEffect != null) Instantiate(explosionEffect, transform.position, Quaternion.identity);

        // Æø¹ß ¹üÀ§ °¨Áö
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        foreach (var hit in hits)
        {
            // 1. º¸½º Å¸°Ý
            BossHealth boss = hit.GetComponent<BossHealth>();
            if (boss != null) boss.TakeDamage(baseDamage, bombElement);

            if (hit.CompareTag("Grass"))
            {
                Destroy(hit.gameObject);
            }
        }

        // ÆøÅº ÀÚÃ¼ »èÁ¦
        Destroy(gameObject);
    }
}