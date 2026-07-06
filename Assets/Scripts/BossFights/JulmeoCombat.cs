using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class JulmeoCombat : BossCombatBase
{
    public GameObject fireStartEffect;
    public GameObject fireBallPrefab;

    public Transform playerTF;

    private Vector2 moveInput;
    private Vector2 spawnPos;
    
    private bool isSpawnable;
    private bool canMove;

    // QS-12: 공격당 24발 × 웨이브 겹침(수명 3s vs 공격 주기 ~2.9s) 대비
    private const int FireBallPoolInitialSize = 48;

    private BossProjectilePool fireBallPool;
    private Transform fireBallPoolRoot;

    // QS-82: Start()는 SetActive(true) 직후 동기 호출되는 StartBattle()보다 늦게 실행돼
    // canMove=false 가드에 걸린다. Awake는 SetActive 시점에 동기 실행되므로 여기서 초기화.
    void Awake()
    {
        canMove = true;
        EnsureFireBallPool();
    }

    // QS-12: 풀 루트는 보스 자식이 아닌 독립 오브젝트로 둔다.
    // Julmeo는 매 루프 순간이동하므로 보스를 루트로 쓰면 비행 중인 탄이
    // 부모를 따라 끌려가 탄도가 변한다 — 기존 Instantiate(씬 루트)와
    // 동일한 월드 공간을 유지하기 위한 분리.
    private void EnsureFireBallPool()
    {
        if (fireBallPool != null || fireBallPrefab == null) return;

        fireBallPoolRoot = new GameObject("JulmeoFireBallPoolRoot").transform;
        fireBallPool = new BossProjectilePool(fireBallPrefab, FireBallPoolInitialSize, fireBallPoolRoot);
    }

    void OnDestroy()
    {
        if (fireBallPoolRoot != null)
        {
            Destroy(fireBallPoolRoot.gameObject);
        }
    }
    public override void StartBattle()
    {
        if (canMove == false) return;
        StartCoroutine(BattleRoutine());
        Debug.Log("Julmeo spawned & attacking!");
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        StopAllCoroutines();
    }

    IEnumerator BattleRoutine()
    {
        while (canMove)
        {
            if (playerTF == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            playerTF = playerObj?.transform;
        }
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        
        moveInput = new Vector2(horizontal, vertical).normalized;

        Vector2 lastDir = moveInput;
    
        if (moveInput == Vector2.zero)
        {
            spawnPos = playerTF.position + new Vector3(0, -7.0f, 0); 
        }
        else
        {
            spawnPos = playerTF.position + new Vector3(-moveInput.x, -moveInput.y, 0).normalized * 7.0f;
        }
        transform.position = spawnPos;
        yield return new WaitForSeconds(0.5f);

        StartCoroutine(AttackRoutine());
        canMove = true;
        yield return new WaitForSeconds(2.4f);
        }

        
    }

    IEnumerator AttackRoutine() 
    {
        if (playerTF == null) yield break;
        canMove = false;
        Vector2 dir = playerTF.position - transform.position;
        float[] directions = { -90f, -30f, 30f, 90f };
        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        for (int j = 0; j < 2; j++)
        {
            for (int i = 0; i < 3; i++)
            {
                foreach (float angle in directions)
                {
                Quaternion rot = Quaternion.Euler(0, 0, baseAngle + angle);
                Vector3 bulletPos = transform.position + (rot * Vector3.right * 0.5f);

                EnsureFireBallPool();
                BossProjectile fireBall = fireBallPool != null ? fireBallPool.Rent() : null;
                if (fireBall != null)
                {
                    // QS-12 조건: Rent 직후 매번 등록 (재사용 인스턴스는 딕셔너리 덮어쓰기)
                    RegisterBossOffensive(fireBall.gameObject);
                    fireBall.transform.SetPositionAndRotation(bulletPos, rot);
                    fireBall.Setup(ElementType.Water);
                }
                else
                {
                    // 풀 구성 실패(프리팹 미배선 등) 시 기존 경로 그대로 유지
                    GameObject projectile = Instantiate(fireBallPrefab, bulletPos, rot);
                    RegisterBossOffensive(projectile);
                    projectile.GetComponent<BossProjectile>()?.Setup(ElementType.Water);
                }
                }
                yield return new WaitForSeconds(0.1f);
            
            }
            yield return new WaitForSeconds(0.5f);
        }
        
    }

        
    
}
