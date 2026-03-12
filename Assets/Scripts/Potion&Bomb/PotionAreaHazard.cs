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
    private readonly Dictionary<int, float> nextTickTimeByTarget = new Dictionary<int, float>();
    private int sourceBombId;
    private int phaseIndex;
    private bool usesPeriodicTicks;
    private float tickIntervalSeconds;

    public int SourceBombId => sourceBombId;
    public int PhaseIndex => phaseIndex;

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
        Init(spec, sizeUnits, durationSeconds, 0, 0);
    }

    public void Init(PotionPhaseSpec spec, Vector2 sizeUnits, float durationSeconds, int sourceBombInstanceId, int sourcePhaseIndex)
    {
        Init(spec, sizeUnits, durationSeconds, 0f, sourceBombInstanceId, sourcePhaseIndex);
    }

    public void Init(
        PotionPhaseSpec spec,
        Vector2 sizeUnits,
        float durationSeconds,
        float tickInterval,
        int sourceBombInstanceId,
        int sourcePhaseIndex)
    {
        phaseSpec = spec;
        enteredTargets.Clear();
        nextTickTimeByTarget.Clear();
        initialized = true;
        sourceBombId = sourceBombInstanceId;
        phaseIndex = sourcePhaseIndex;
        tickIntervalSeconds = Mathf.Max(0f, tickInterval);
        usesPeriodicTicks = tickIntervalSeconds > 0f;

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
        if (usesPeriodicTicks)
        {
            nextTickTimeByTarget[colliderId] = Time.time + tickIntervalSeconds;
            return;
        }

        if (!enteredTargets.Add(colliderId))
        {
            return;
        }

        PotionHitResolver.TryResolveAreaHit(phaseSpec, other, gameObject.GetInstanceID());
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!initialized || phaseSpec == null || other == null || !usesPeriodicTicks)
        {
            return;
        }

        int colliderId = other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject.GetInstanceID()
            : other.gameObject.GetInstanceID();

        if (!nextTickTimeByTarget.TryGetValue(colliderId, out float nextTickTime))
        {
            nextTickTimeByTarget[colliderId] = Time.time + tickIntervalSeconds;
            return;
        }

        if (Time.time < nextTickTime)
        {
            return;
        }

        if (PotionHitResolver.TryResolveAreaHit(phaseSpec, other, gameObject.GetInstanceID()))
        {
            nextTickTimeByTarget[colliderId] = Time.time + tickIntervalSeconds;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        int colliderId = other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject.GetInstanceID()
            : other.gameObject.GetInstanceID();

        nextTickTimeByTarget.Remove(colliderId);
    }
}
