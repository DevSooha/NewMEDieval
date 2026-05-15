using UnityEngine;

/// <summary>
/// 캠프파이어(세이브 포인트). SavePoint를 상속하며,
/// 기존 씬/프리팹에 직렬화된 bonfireId 값을 savePointId로 자동 마이그레이션한다.
/// </summary>
public class Bonfire : SavePoint
{
#if UNITY_EDITOR
    // 기존 씬·프리팹 직렬화 호환을 위해 필드명 유지.
    // OnValidate에서 savePointId로 복사 후 비워진다.
    [SerializeField, HideInInspector] private string bonfireId;

    protected override void OnValidate()
    {
        if (!string.IsNullOrEmpty(bonfireId) && string.IsNullOrEmpty(savePointId))
        {
            savePointId = bonfireId;
            bonfireId = string.Empty;
            UnityEditor.EditorUtility.SetDirty(this);
        }
        base.OnValidate();
    }
#endif
}
