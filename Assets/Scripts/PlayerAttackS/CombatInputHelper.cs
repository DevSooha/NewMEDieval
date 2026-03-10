using UnityEngine;

internal static class CombatInputHelper
{
    private const KeyCode AttackKey = KeyCode.Z;
    private const int AttackMouseButton = 1;
    private static int consumedAttackInputFrame = -1;

    internal static void ConsumeAttackInputThisFrame()
    {
        consumedAttackInputFrame = Time.frameCount;
    }

    internal static bool IsAttackPressed()
    {
        if (consumedAttackInputFrame == Time.frameCount)
        {
            return false;
        }

        return Input.GetKeyDown(AttackKey) || Input.GetMouseButtonDown(AttackMouseButton);
    }

    internal static bool IsAttackReleased()
    {
        if (consumedAttackInputFrame == Time.frameCount)
        {
            return false;
        }

        return Input.GetKeyUp(AttackKey) || Input.GetMouseButtonUp(AttackMouseButton);
    }
}
