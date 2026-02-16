using UnityEngine;

// 상속: BossProjectile의 기능(풀링, 데미지 등)을 그대로 물려받음
public class MasqueIllusionProjectile : BossProjectile
{
    [Header("Trap Settings")]
    public float trackingDuration = 4f; // 추적 지속 시간 (기획: 4초)
    public float trapDuration = 3f;     // 구속 제한 시간 (기획: 3초 이내)
    public int requiredEscapeInputs = 5;// 탈출 카운트 (기획: 5회)

    private Transform playerTransform;
    private Player trappedPlayer;
    private bool isTrapping = false;
    private float stateTimer = 0f;
    private int currentInputs = 0;

    // 보스 패턴에서 탄막 생성 시 호출해줄 초기화 함수
    public void InitializeTrap(Transform targetPlayer)
    {
        playerTransform = targetPlayer;
        trappedPlayer = null;
        isTrapping = false;
        stateTimer = 0f;
        currentInputs = 0;

        // 이전 구속 상태에서 남은 부모 관계를 끊고 재사용 상태를 초기화
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
            Debug.Log("탈출 실패! 체력 -1 감소");

            PlayerHealth playerHealth = playerTransform.GetComponent<PlayerHealth>();
            if (playerHealth == null) playerHealth = playerTransform.GetComponentInParent<PlayerHealth>();

            if (playerHealth != null) playerHealth.TakeDamage(damage);

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
                Debug.Log("탈출 성공! 데미지 없이 조작 권한 복구");
                ReleasePlayer();
            }
        }
    }


    private void OnDisable()
    {
        if (isTrapping)
        {
            if (trappedPlayer != null)
            {
                trappedPlayer.SetCanMove(true);
            }
            else if (Player.Instance != null)
            {
                Player.Instance.SetCanMove(true);
            }
        }

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
            // 구버전 구조 호환용 fallback
            Player.Instance.CancelAttack();
            Player.Instance.StopMoving();
            Player.Instance.SetCanMove(false);
        }
    }

    private void ReleasePlayer()
    {
        if (trappedPlayer != null)
        {
            trappedPlayer.SetCanMove(true);
        }
        else if (Player.Instance != null)
        {
            Player.Instance.SetCanMove(true);
        }

        transform.SetParent(null);
        trappedPlayer = null;
        isTrapping = false;

        DestroyProjectile();
    }
}


