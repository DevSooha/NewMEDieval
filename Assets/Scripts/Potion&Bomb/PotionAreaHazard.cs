using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class PotionAreaHazard : MonoBehaviour
{
    private static readonly Dictionary<int, float> SharedNextTickTimeByTarget = new Dictionary<int, float>();
    private static readonly Collider2D[] OverlapResults = new Collider2D[16];

    [SerializeField] private float offscreenMargin = 0.2f;
    [SerializeField] private bool drawHazardGizmo = true;
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
    private Camera cachedCamera;

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
        cachedCamera = Camera.main;
    }

    public void Init(PotionPhaseSpec spec, Vector2 sizeUnits, float durationSeconds)
    {
        Init(spec, sizeUnits, durationSeconds, 0f, 0, 0, 0f);
    }

    public void Init(PotionPhaseSpec spec, Vector2 sizeUnits, float durationSeconds, int sourceBombInstanceId, int sourcePhaseIndex)
    {
        Init(spec, sizeUnits, durationSeconds, 0f, sourceBombInstanceId, sourcePhaseIndex, 0f);
    }

    public void Init(
        PotionPhaseSpec spec,
        Vector2 sizeUnits,
        float durationSeconds,
        int sourceBombInstanceId,
        int sourcePhaseIndex,
        float rotationDegrees)
    {
        Init(spec, sizeUnits, durationSeconds, 0f, sourceBombInstanceId, sourcePhaseIndex, rotationDegrees);
    }

    public void Init(
        PotionPhaseSpec spec,
        Vector2 sizeUnits,
        float durationSeconds,
        float tickInterval,
        int sourceBombInstanceId,
        int sourcePhaseIndex)
    {
        Init(spec, sizeUnits, durationSeconds, tickInterval, sourceBombInstanceId, sourcePhaseIndex, 0f);
    }

    public void Init(
        PotionPhaseSpec spec,
        Vector2 sizeUnits,
        float durationSeconds,
        float tickInterval,
        int sourceBombInstanceId,
        int sourcePhaseIndex,
        float rotationDegrees)
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
        transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
        triggerCollider.enabled = true;

        ApplyImmediateTicksForCurrentOverlaps();

        StopAllCoroutines();
        StartCoroutine(LifetimeRoutine(Mathf.Max(0.05f, durationSeconds)));
    }

    private IEnumerator LifetimeRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        triggerCollider.enabled = false;
        Destroy(gameObject);
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        if (!IsOffscreen())
        {
            return;
        }

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
            ApplyImmediateTick(other, colliderId);
            return;
        }

        if (!enteredTargets.Add(colliderId))
        {
            return;
        }

        PotionHitResolver.TryResolveAreaHit(phaseSpec, other, gameObject.GetInstanceID(), transform.position);
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

        if (!CanApplySharedTick(colliderId))
        {
            return;
        }

        if (PotionHitResolver.TryResolveAreaHit(phaseSpec, other, gameObject.GetInstanceID(), transform.position))
        {
            nextTickTimeByTarget[colliderId] = Time.time + tickIntervalSeconds;
            SharedNextTickTimeByTarget[colliderId] = Time.time + tickIntervalSeconds;
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

    private void ApplyImmediateTicksForCurrentOverlaps()
    {
        if (!initialized || phaseSpec == null || !usesPeriodicTicks || triggerCollider == null)
        {
            return;
        }

        ContactFilter2D filter = ContactFilter2D.noFilter;
        filter.useTriggers = true;

        int overlapCount = triggerCollider.Overlap(filter, OverlapResults);
        for (int i = 0; i < overlapCount; i++)
        {
            Collider2D other = OverlapResults[i];
            if (other == null)
            {
                continue;
            }

            int colliderId = other.attachedRigidbody != null
                ? other.attachedRigidbody.gameObject.GetInstanceID()
                : other.gameObject.GetInstanceID();
            ApplyImmediateTick(other, colliderId);
        }
    }

    private void ApplyImmediateTick(Collider2D other, int colliderId)
    {
        if (other == null)
        {
            return;
        }

        if (CanApplySharedTick(colliderId)
            && PotionHitResolver.TryResolveAreaHit(phaseSpec, other, gameObject.GetInstanceID(), transform.position))
        {
            float nextTickTime = Time.time + tickIntervalSeconds;
            nextTickTimeByTarget[colliderId] = nextTickTime;
            SharedNextTickTimeByTarget[colliderId] = nextTickTime;
            return;
        }

        nextTickTimeByTarget[colliderId] = Time.time + tickIntervalSeconds;
    }

    private bool IsOffscreen()
    {
        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }

        if (cachedCamera == null)
        {
            return false;
        }

        Vector3 viewport = cachedCamera.WorldToViewportPoint(transform.position);
        if (viewport.z < 0f)
        {
            return true;
        }

        return viewport.x < -offscreenMargin
            || viewport.x > 1f + offscreenMargin
            || viewport.y < -offscreenMargin
            || viewport.y > 1f + offscreenMargin;
    }

    private bool CanApplySharedTick(int colliderId)
    {
        if (!SharedNextTickTimeByTarget.TryGetValue(colliderId, out float sharedNextTickTime))
        {
            return true;
        }

        return Time.time >= sharedNextTickTime;
    }

    private void OnDrawGizmos()
    {
        DrawHazardGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        DrawHazardGizmo();
    }

    private void DrawHazardGizmo()
    {
        if (!drawHazardGizmo)
        {
            return;
        }

        BoxCollider2D box = triggerCollider != null ? triggerCollider : GetComponent<BoxCollider2D>();
        if (box == null)
        {
            return;
        }

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = box.enabled && gameObject.activeInHierarchy
            ? new Color(1f, 0.45f, 0.1f, 0.85f)
            : new Color(1f, 0.8f, 0.2f, 0.6f);
        Gizmos.DrawWireCube(box.offset, box.size);
        Gizmos.matrix = previousMatrix;
    }
}
