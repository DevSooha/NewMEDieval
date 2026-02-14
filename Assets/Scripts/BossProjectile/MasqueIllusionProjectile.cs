using UnityEngine;

// 상속: BossProjectile의 기능(풀링, 데미지 등)을 그대로 물려받음
public class MasqueIllusionProjectile : BossProjectile
{
    [Header("Trap Settings")]
    public float trackingDuration = 4f; // 추적 지속 시간 (기획: 4초)
    public float trapDuration = 3f;     // 구속 제한 시간 (기획: 3초 이내)
    public int requiredEscapeInputs = 5;// 탈출 카운트 (기획: 5회)

    private Transform playerTransform;
    private bool isTrapping = false;
    private float stateTimer = 0f;
    private int currentInputs = 0;

    // 보스 패턴에서 탄막 생성 시 호출해줄 초기화 함수
    public void InitializeTrap(Transform targetPlayer)
    {
        playerTransform = targetPlayer;
        isTrapping = false;
        stateTimer = 0f;
        currentInputs = 0;

        // 부모(BossProjectile)의 Setup에서 3초 뒤 파괴를 예약하므로,
        // 기획서의 지속 시간인 4초로 취소 후 재예약합니다.
        CancelInvoke(nameof(DestroyProjectile));
        Invoke(nameof(DestroyProjectile), trackingDuration);
    }

    // 부모의 '직진 이동'을 덮어쓰고 '추적 이동'으로 변경
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

        // 플레이어 방향으로 추적 이동 (속도는 부모의 speed 변수 사용)
        transform.position = Vector2.MoveTowards(transform.position, playerTransform.position, speed * Time.deltaTime);
    }

    private void HandleTrappedState()
    {
        stateTimer += Time.deltaTime;

        // 3초 경과 -> 탈출 실패: 데미지 적용 후 해제
        if (stateTimer >= trapDuration)
        {
            Debug.Log("탈출 실패! 체력 -1 감소");

            PlayerHealth playerHealth = playerTransform.GetComponent<PlayerHealth>();
            if (playerHealth == null) playerHealth = playerTransform.GetComponentInParent<PlayerHealth>();

            // 부모의 damage(1) 변수를 사용하여 데미지 적용
            if (playerHealth != null) playerHealth.TakeDamage(damage);

            ReleasePlayer();
            return;
        }

        // WASD 상좌하우 연타 감지 (1회당 1카운트)
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

    // 부모의 '즉시 데미지' 충돌을 덮어쓰고 '구속 미니게임 시작'으로 변경
    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Boss")) return;

        // 플레이어 충돌 시 구속 시작
        if (!isTrapping && other.CompareTag("Player"))
        {
            StartTrapping(other.transform);
        }
    }

    private void StartTrapping(Transform hitPlayer)
    {
        isTrapping = true;
        stateTimer = 0f; // 3초 제한시간 카운트를 위해 타이머 초기화

        // 구속 중에는 타이머에 의해 자연 파괴되지 않도록 Invoke 취소
        CancelInvoke(nameof(DestroyProjectile));

        // 플레이어 위치에 붙이기
        transform.position = hitPlayer.position;
        transform.SetParent(hitPlayer);

        // --- 수정된 부분: 플레이어 조작 봉인 ---
        if (Player.Instance != null)
        {
            Player.Instance.CancelAttack();    // 진행 중인 공격 모션 취소
            Player.Instance.StopMoving();      // 걷기 애니메이션 강제 정지
            Player.Instance.SetCanMove(false); // 상태를 Stunned로 변경하여 조작 불가 상태로 만듦
        }
    }

    private void ReleasePlayer()
    {
        // --- 수정된 부분: 플레이어 조작 복구 ---
        if (Player.Instance != null)
        {
            Player.Instance.SetCanMove(true); // 상태를 Idle로 변경하여 다시 조작 가능하게 만듦
        }

        // [중요] 오브젝트 풀링을 사용 중이므로 플레이어 자식에서 분리해주어야 
        // 다음번 재사용될 때 엉뚱한 위치에 스폰되는 버그를 막을 수 있습니다.
        transform.SetParent(null);

        // 부모(BossProjectile)에 있는 풀 반환 로직(숨기기 + 반납) 실행
        DestroyProjectile();
    }
}