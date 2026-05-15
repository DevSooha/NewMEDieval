using UnityEngine;

/// <summary>
/// ISaveable을 구현하는 MonoBehaviour의 추상 기반 클래스.
/// _saveId 필드에 에디터에서 한 번 부여한 GUID가 저장되며,
/// 오브젝트 이름 변경이나 이동에도 ID가 유지된다.
/// </summary>
public abstract class SaveableBehaviour : MonoBehaviour, ISaveable
{
    [SerializeField] private string _saveId;

    public string SaveId => _saveId;

    public abstract object CaptureState();
    public abstract void RestoreState(object state);

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        if (string.IsNullOrEmpty(_saveId))
        {
            _saveId = System.Guid.NewGuid().ToString();
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
