using UnityEngine;

// Inherits pooling/damage behavior from BossProjectile.
public class MasqueIllusionProjectile : BossProjectile
{
    [Header("Trap Settings")]
    public float trackingDuration = 4f;
    public float trapDuration = 3f;
    public int requiredEscapeInputs = 5;

    private Transform playerTransform;
    private Player trappedPlayer;
    private bool isTrapping;
    private float stateTimer;
    private int currentInputs;

    public void InitializeTrap(Transform targetPlayer)
    {
        playerTransform = targetPlayer;
        trappedPlayer = null;
        isTrapping = false;
        stateTimer = 0f;
        currentInputs = 0;

        if (transform.parent != null)
        {
            transform.SetParent(null);
        }

        CancelInvoke(nameof(DestroyProjectile));
        Invoke(nameof(DestroyProjectile), trackingDuration);
    }

    protected override void Update()
    {
        if (isTrapping)
        {
            HandleTrappedState();
        }
        else
        {
            HandleTrackingState();
        }
    }

    private void HandleTrackingState()
    {
        if (playerTransform == null) return;

        transform.position = Vector2.MoveTowards(transform.position, playerTransform.position, speed * Time.deltaTime);
    }

    private void HandleTrappedState()
    {
        if (playerTransform == null)
        {
            ReleasePlayer();
            return;
        }

        stateTimer += Time.deltaTime;

        if (stateTimer >= trapDuration)
        {
            PlayerHealth playerHealth = playerTransform.GetComponent<PlayerHealth>();
            if (playerHealth == null) playerHealth = playerTransform.GetComponentInParent<PlayerHealth>();

            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
            }

            ReleasePlayer();
            return;
        }

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) ||
            Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D) ||
            Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow) ||
            Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            currentInputs++;
            if (currentInputs >= requiredEscapeInputs)
            {
                ReleasePlayer();
            }
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        RestorePlayerControl();
        trappedPlayer = null;
        isTrapping = false;
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Boss")) return;

        if (!isTrapping && other.CompareTag("Player"))
        {
            StartTrapping(other.transform);
        }
    }

    private void StartTrapping(Transform hitPlayer)
    {
        isTrapping = true;
        stateTimer = 0f;

        CancelInvoke(nameof(DestroyProjectile));

        playerTransform = hitPlayer;
        trappedPlayer = hitPlayer.GetComponent<Player>();
        if (trappedPlayer == null) trappedPlayer = hitPlayer.GetComponentInParent<Player>();

        transform.position = hitPlayer.position;
        transform.SetParent(hitPlayer);

        if (trappedPlayer != null)
        {
            trappedPlayer.CancelAttack();
            trappedPlayer.StopMoving();
            trappedPlayer.SetCanMove(false);
        }
        else if (Player.Instance != null)
        {
            Player.Instance.CancelAttack();
            Player.Instance.StopMoving();
            Player.Instance.SetCanMove(false);
        }
    }

    private void ReleasePlayer()
    {
        RestorePlayerControl();

        if (transform.parent != null)
        {
            transform.SetParent(null);
        }

        trappedPlayer = null;
        isTrapping = false;

        DestroyProjectile();
    }

    public override void DespawnImmediate()
    {
        CancelInvoke(nameof(DestroyProjectile));
        RestorePlayerControl();

        if (transform.parent != null)
        {
            transform.SetParent(null);
        }

        trappedPlayer = null;
        isTrapping = false;
        DestroyProjectile();
    }

    private void RestorePlayerControl()
    {
        if (trappedPlayer != null)
        {
            trappedPlayer.SetCanMove(true);
            return;
        }

        if (Player.Instance != null)
        {
            Player.Instance.SetCanMove(true);
        }
    }
}
