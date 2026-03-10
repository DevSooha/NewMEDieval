using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class ElectricLaserRay : BossProjectile
{
    [Header("Beam Settings")]
    [SerializeField] private float extendSpeed = 18f;
    [SerializeField] private float maxLength = 10f;
    [SerializeField] private float startDelay = 0.25f;
    [SerializeField] private float laserHitWidth = 1.42f;

    [Header("VFX")]
    [SerializeField] private GameObject startVfxRoot;
    [SerializeField] private GameObject loopVfxRoot;

    private float currentLength;
    private float runtimeMaxLength;
    private float laserLocalOffsetX;
    private bool loopActive;
    private BoxCollider2D damageCollider;
    private Coroutine startToLoopRoutine;

    private void Awake()
    {
        EnsureSetupDependencies();
        SetVfxInactive(startVfxRoot);
        SetVfxInactive(loopVfxRoot);
    }

    public override void Setup(ElementType element)
    {
        // Keep pooled projectile lifecycle from base class.
        base.Setup(ElementType.Electric);

        EnsureSetupDependencies();

        if (startToLoopRoutine != null)
        {
            StopCoroutine(startToLoopRoutine);
            startToLoopRoutine = null;
        }

        currentLength = 0f;
        runtimeMaxLength = maxLength;
        loopActive = false;

        if (startVfxRoot != null && loopVfxRoot != null)
        {
            loopVfxRoot.transform.localPosition = startVfxRoot.transform.localPosition;
            loopVfxRoot.transform.localRotation = startVfxRoot.transform.localRotation;
        }

        if (damageCollider != null)
        {
            laserLocalOffsetX = ResolveLaserLocalOffsetX();
            damageCollider.isTrigger = true;
            damageCollider.size = new Vector2(laserHitWidth, 0.1f);
            damageCollider.offset = new Vector2(laserLocalOffsetX, 0f);
            damageCollider.enabled = false;
        }

        SetVfxInactive(loopVfxRoot);
        RestartVfx(startVfxRoot);
        startToLoopRoutine = StartCoroutine(StartThenLoopRoutine());
    }

    protected override void Update()
    {
        if (!loopActive || damageCollider == null) return;

        if (currentLength < runtimeMaxLength)
        {
            currentLength += extendSpeed * Time.deltaTime;
            currentLength = Mathf.Min(currentLength, runtimeMaxLength);

            damageCollider.size = new Vector2(laserHitWidth, currentLength);
            damageCollider.offset = new Vector2(laserLocalOffsetX, -currentLength / 2f);
        }
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (!loopActive) return;
        if (other.CompareTag("Boss")) return;

        if (other.CompareTag("Player"))
        {
            BossHitResolver.TryApplyBossHit(
                other,
                damage,
                transform.position,
                knockbackDistance,
                knockbackDuration,
                applyKnockbackOnHit,
                -(Vector2)transform.up
            );
        }
        else if (other.CompareTag("Obstacle") || other.CompareTag("Wall"))
        {
            runtimeMaxLength = currentLength;
        }
    }

    protected override void OnDisable()
    {
        if (startToLoopRoutine != null)
        {
            StopCoroutine(startToLoopRoutine);
            startToLoopRoutine = null;
        }

        loopActive = false;

        if (damageCollider != null)
        {
            damageCollider.enabled = false;
        }

        SetVfxInactive(startVfxRoot);
        SetVfxInactive(loopVfxRoot);
        base.OnDisable();
    }

    private IEnumerator StartThenLoopRoutine()
    {
        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        SetVfxInactive(startVfxRoot);
        RestartVfx(loopVfxRoot);

        loopActive = true;
        if (damageCollider != null)
        {
            damageCollider.enabled = true;
        }

        startToLoopRoutine = null;
    }

    private void EnsureSetupDependencies()
    {
        if (startVfxRoot == null)
        {
            Transform t = transform.Find("VFX_ElectricLaser_Start");
            if (t != null) startVfxRoot = t.gameObject;
        }

        if (loopVfxRoot == null)
        {
            Transform t = transform.Find("VFX_ElectricLaser_Loop");
            if (t != null) loopVfxRoot = t.gameObject;
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.simulated = true;

        damageCollider = GetComponent<BoxCollider2D>();
        damageCollider.isTrigger = true;
    }

    private static void RestartVfx(GameObject target)
    {
        if (target == null) return;

        target.SetActive(false);
        target.SetActive(true);

        ParticleSystem[] systems = target.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem ps in systems)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
            ps.Play(true);
        }
    }

    private static void SetVfxInactive(GameObject target)
    {
        if (target == null) return;

        ParticleSystem[] systems = target.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem ps in systems)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
        }

        target.SetActive(false);
    }

    private float ResolveLaserLocalOffsetX()
    {
        if (loopVfxRoot == null) return 0f;

        Transform laserTransform = loopVfxRoot.transform.Find("Laser");
        if (laserTransform == null) return 0f;
        return laserTransform.localPosition.x;
    }
}
