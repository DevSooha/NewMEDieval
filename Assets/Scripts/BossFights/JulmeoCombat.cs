using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class JulmeoCombat : MonoBehaviour
{
    public GameObject fireStartEffect;
    public GameObject fireBallPrefab;

    public Transform playerTF;

    private Vector2 moveInput;
    private Vector2 spawnPos;
    
    private bool isSpawnable;
    private bool canMove;

    void Start()
    {
        canMove = true;
    }
    public void StartBattle()
    {
        if (canMove == false) return;
        StartCoroutine(BattleRoutine());
        Debug.Log("Julmeo spawned & attacking!");
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

                GameObject projectile = Instantiate(fireBallPrefab, bulletPos, rot);
                projectile.GetComponent<BossProjectile>()?.Setup(ElementType.Water);                
                }
                yield return new WaitForSeconds(0.1f);
            
            }
            yield return new WaitForSeconds(0.5f);
        }
        
    }

        
    
}
