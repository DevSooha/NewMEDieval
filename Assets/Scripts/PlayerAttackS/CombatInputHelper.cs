using UnityEngine;

internal static class CombatInputHelper
{
    private const KeyCode AttackKey = KeyCode.Z;
    private const string AttackDownActionId = "attack_down";
    private const string AttackUpActionId = "attack_up";
    private const int AttackMouseButton = 1;
    private static int consumedAttackInputFrame = -1;

    internal static void ConsumeAttackInputThisFrame()
    {
        consumedAttackInputFrame = Time.frameCount;
    }

    internal static bool IsAttackPressed(PlayerStatusController status = null)
    {
        if (consumedAttackInputFrame == Time.frameCount)
        {
            return false;
        }

        bool rawPressed = Input.GetKeyDown(AttackKey) || Input.GetMouseButtonDown(AttackMouseButton);
        return status != null
            ? status.ProcessActionButtonDown(AttackDownActionId, rawPressed)
            : rawPressed;
    }

    internal static bool IsAttackReleased(PlayerStatusController status = null)
    {
        if (consumedAttackInputFrame == Time.frameCount)
        {
            return false;
        }

        bool rawReleased = Input.GetKeyUp(AttackKey) || Input.GetMouseButtonUp(AttackMouseButton);
        return status != null
            ? status.ProcessActionButtonUp(AttackUpActionId, rawReleased)
            : rawReleased;
    }
}
