using UnityEngine;

public class PotionCraft : MonoBehaviour
{
    public enum PotionTemp { Failure, LowTemp, MidTemp, HighTemp }

    public static PotionTemp DeterminePotionType(float gaugeValue)
    {
        CraftTemperatureBand band = PotionCraftRules.DetermineBand(gaugeValue);
        return band switch
        {
            CraftTemperatureBand.Low => PotionTemp.LowTemp,
            CraftTemperatureBand.Mid => PotionTemp.MidTemp,
            CraftTemperatureBand.High => PotionTemp.HighTemp,
            _ => PotionTemp.Failure
        };
    }

    public static void CreatePotion(PotionTemp temp)
    {
        Debug.Log($"[PotionCraft] Craft result: {GetPotionName(temp)}");
    }

    public static string GetPotionName(PotionTemp type)
    {
        CraftTemperatureBand band = type switch
        {
            PotionTemp.LowTemp => CraftTemperatureBand.Low,
            PotionTemp.MidTemp => CraftTemperatureBand.Mid,
            PotionTemp.HighTemp => CraftTemperatureBand.High,
            _ => CraftTemperatureBand.Failure
        };

        return PotionCraftRules.GetPotionName(band);
    }
}
