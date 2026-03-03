using UnityEngine;

public class PotionCraft: MonoBehaviour
{
    public enum PotionTemp { Failure, LowTemp, MidTemp, HighTemp }

    public static PotionTemp DeterminePotionType(float gaugeValue)
    {
        float failMax = 100f * (1f / 7f);
        float lowMax = 100f * (3f / 7f);
        float midMax = 100f * (6f / 7f);

        if (gaugeValue < failMax)
            return PotionTemp.Failure;
        else if (gaugeValue < lowMax)
            return PotionTemp.LowTemp;
        else if (gaugeValue < midMax)
            return PotionTemp.MidTemp;
        else
            return PotionTemp.HighTemp;
    }

    public static void CreatePotion(PotionTemp temp)
    {
        switch(temp)
        {
            case PotionTemp.Failure:
                Debug.Log("포션 제작 실패!");
                break;
            case PotionTemp.LowTemp:
                Debug.Log("저온 포션 생성!");
                break;
            case PotionTemp.MidTemp:
                Debug.Log("중온 포션 생성!");
                break;
            case PotionTemp.HighTemp:
                Debug.Log("고온 포션 생성!");
                break;
        }
    }

    public static string GetPotionName(PotionTemp type)
{
    return type switch
    {
        PotionTemp.Failure => "FAILED",
        PotionTemp.LowTemp => "LOW TEMP POTION",
        PotionTemp.MidTemp => "MID TEMP POTION",
        PotionTemp.HighTemp => "HIGH TEMP POTION",
        _ => "Unknown"
    };
}
}
