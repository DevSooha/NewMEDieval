/// <summary>
/// 씬에 고유한 ID를 가지며 자신의 상태를 저장·복원할 수 있는 오브젝트 계약.
/// SaveId는 에디터에서 미리 부여된 GUID여야 한다(오브젝트 이동·리네임에 무관).
/// </summary>
public interface ISaveable
{
    /// <summary>씬 전체에서 유일한 식별자. 에디터 GUID 기반.</summary>
    string SaveId { get; }

    /// <summary>현재 상태를 직렬화 가능한 객체로 반환한다.</summary>
    object CaptureState();

    /// <summary>CaptureState가 반환했던 객체를 받아 상태를 복원한다.</summary>
    void RestoreState(object state);
}
