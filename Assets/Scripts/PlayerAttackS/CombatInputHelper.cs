using UnityEngine;

/// <summary>공격 입력의 감지·소비를 단일 진입점으로 캡슐화하는 정적 헬퍼.</summary>
internal static class CombatInputHelper
{
    private const KeyCode AttackKey = KeyCode.Z;
    private const string AttackDownActionId = "attack_down";
    private const string AttackUpActionId = "attack_up";
    private const int AttackMouseButton = 1;  // 우클릭
    private static int consumedAttackInputFrame = -1;

    // 두 메서드가 동일하게 읽으므로 프로퍼티로 추출해 변경점을 단일화
    private static bool IsInputConsumedThisFrame
        => consumedAttackInputFrame == Time.frameCount;

    /// <summary>
    /// 이 프레임의 공격 입력을 소비 완료로 표시한다.
    /// 이후 같은 프레임 내 <see cref="IsAttackPressed"/>·<see cref="IsAttackReleased"/> 호출은 false를 반환한다.
    /// </summary>
    internal static void ConsumeAttackInputThisFrame()
    {
        consumedAttackInputFrame = Time.frameCount;
    }

    /// <summary>이 프레임에 공격 버튼이 눌렸는지 반환한다.</summary>
    /// <param name="status">null이 아니면 스턴 등 상태 이상 필터링을 거친다.</param>
    internal static bool IsAttackPressed(PlayerStatusController status = null)
    {
        if (IsInputConsumedThisFrame)
        {
            return false;
        }

        bool rawPressed = Input.GetKeyDown(AttackKey) || Input.GetMouseButtonDown(AttackMouseButton);
        return status != null
            ? status.ProcessActionButtonDown(AttackDownActionId, rawPressed)
            : rawPressed;
    }

    /// <summary>이 프레임에 공격 버튼이 떼어졌는지 반환한다.</summary>
    /// <param name="status">null이 아니면 스턴 등 상태 이상 필터링을 거친다.</param>
    internal static bool IsAttackReleased(PlayerStatusController status = null)
    {
        if (IsInputConsumedThisFrame)
        {
            return false;
        }

        bool rawReleased = Input.GetKeyUp(AttackKey) || Input.GetMouseButtonUp(AttackMouseButton);
        return status != null
            ? status.ProcessActionButtonUp(AttackUpActionId, rawReleased)
            : rawReleased;
    }
}
