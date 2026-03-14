using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class HandOfTime : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private FateSeverSpearEmitterController emitterCtrl;

    [Header("Projectile Prefabs")]
    [SerializeField] private HandOfTimeProjectile prefab1x3;
    [SerializeField] private HandOfTimeProjectile prefab2x2;

    [Header("Timing")]
    [SerializeField] private float castTime = 0.5f;

    [Header("Speed")]
    [SerializeField] private float speedWorldPerSec = 5f;

    private void Awake()
    {
        if (groundTilemap == null)
        {
            var groundObj = GameObject.FindGameObjectWithTag("Ground");
            if (groundObj != null)
                groundTilemap = groundObj.GetComponentInChildren<Tilemap>();
        }

        if (emitterCtrl == null)
        {
            emitterCtrl = FindObjectOfType<FateSeverSpearEmitterController>();
        }
    }

    public IEnumerator PlayOnce(Transform playerTF)
    {
        yield return new WaitForSeconds(castTime); //이거 기다리는 거

        Debug.Log($"Player world: {playerTF.position}"); //플레이어의 위치를 받아.

        // 플레이어 world 위치 기준 emitter 찾기
        Transform emitterY = emitterCtrl.GetClosestYEmitter(playerTF.position.x); //
        Transform emitterX = emitterCtrl.GetClosestXEmitter(playerTF.position.y); //

        Vector3 spawn2x2 = emitterY.position;//
        Vector3 spawn1x3 = emitterX.position;//

        Debug.Log($"Spawn Y: {spawn2x2}");
        Debug.Log($"Spawn X: {spawn1x3}");

        HandOfTimeProjectile p2 =
            Instantiate(prefab2x2, spawn2x2, prefab2x2.transform.rotation);

        HandOfTimeProjectile p1 =
            Instantiate(prefab1x3, spawn1x3, prefab1x3.transform.rotation);

        p2.BeginFire(
            p2.transform.position + Vector3.down,
            speedWorldPerSec,
            HandOfTimeProjectile.Axis.Vertical
        );

        p1.BeginFire(
            p1.transform.position + Vector3.left,
            speedWorldPerSec,
            HandOfTimeProjectile.Axis.Horizontal
        );
    }
}