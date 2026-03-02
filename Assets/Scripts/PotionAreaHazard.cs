using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class PotionAreaHazard : MonoBehaviour
{
    private BoxCollider2D triggerCollider;
    private Rigidbody2D body;
    private PotionPhaseSpec phaseSpec;
    private bool initialized;
    private readonly HashSet<int> enteredTargets = new HashSet<int>();

    private void Awake()
    {
        triggerCollider = GetComponent<BoxCollider2D>();
        triggerCollider.isTrigger = true;
        triggerCollider.enabled = false;

        body = GetComponent<Rigidbody2D>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody2D>();
        }

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.simulated = true;
    }

    public void Init(PotionPhaseSpec spec, Vector2 sizeUnits, float durationSeconds)
    {
        phaseSpec = spec;
        enteredTargets.Clear();
        initialized = true;

        triggerCollider.size = new Vector2(
            Mathf.Max(0.05f, sizeUnits.x),
            Mathf.Max(0.05f, sizeUnits.y));
        triggerCollider.enabled = true;

        StopAllCoroutines();
        StartCoroutine(LifetimeRoutine(Mathf.Max(0.05f, durationSeconds)));
    }

    private IEnumerator LifetimeRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        triggerCollider.enabled = false;
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!initialized || phaseSpec == null || other == null)
        {
            return;
        }

        int colliderId = other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject.GetInstanceID()
            : other.gameObject.GetInstanceID();
        if (!enteredTargets.Add(colliderId))
        {
            return;
        }

        PotionHitResolver.TryResolveAreaHit(phaseSpec, other);
    }
}
