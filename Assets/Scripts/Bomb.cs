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

    public void ConfigureFromPotionData(PotionData potionData)
    {
        if (potionData == null) return;

        baseDamage = Mathf.Max(1, potionData.damage1 + potionData.damage2);
        bombElement = potionData.element1 switch
        {
            Element.Fire => ElementType.Fire,
            Element.Lightning => ElementType.Electric,
            _ => ElementType.Water
        };
    }

    void Start() { StartCoroutine(ExplodeSequence()); }

    IEnumerator ExplodeSequence()
    {
        yield return new WaitForSeconds(timeToExplode);
        Explode();
    }

    void Explode()
    {
        // 폭발 이펙트 생성
        if (explosionEffect != null) Instantiate(explosionEffect, transform.position, Quaternion.identity);

        // 폭발 범위 감지
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        foreach (var hit in hits)
        {
            // 1. 보스 타격
            BossHealth boss = hit.GetComponent<BossHealth>();
            if (boss != null) boss.TakeDamage(baseDamage, bombElement);

            if (hit.CompareTag("Grass"))
            {
                Destroy(hit.gameObject);
            }
        }

        // 폭탄 자체 삭제
        Destroy(gameObject);
    }
}
