using UnityEngine;

public class PotionCraft: MonoBehaviour
{
    public enum PotionTemp { Failure, LowTemp, MidTemp, HighTemp }

    public static PotionTemp DeterminePotionType(float gaugeValue)
    {
        if (gaugeValue < 25f)
            return PotionTemp.Failure;
        else if (gaugeValue < 50f)
            return PotionTemp.LowTemp;
        else if (gaugeValue < 75f)
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
