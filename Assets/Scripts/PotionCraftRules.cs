public enum CraftTemperatureBand
{
    Failure,
    Low,
    Mid,
    High
}

public static class PotionCraftRules
{
    public const float FailMaxRatio = 1f / 7f;
    public const float LowMaxRatio = 3f / 7f;
    public const float MidMaxRatio = 6f / 7f;

    public static CraftTemperatureBand DetermineBand(float gaugeValue)
    {
        float normalized = gaugeValue / 100f;
        if (normalized < FailMaxRatio) return CraftTemperatureBand.Failure;
        if (normalized < LowMaxRatio) return CraftTemperatureBand.Low;
        if (normalized < MidMaxRatio) return CraftTemperatureBand.Mid;
        return CraftTemperatureBand.High;
    }

    public static string GetPotionName(CraftTemperatureBand band)
    {
        return band switch
        {
            CraftTemperatureBand.Failure => "FAILED",
            CraftTemperatureBand.Low => "LOW TEMP POTION",
            CraftTemperatureBand.Mid => "MID TEMP POTION",
            CraftTemperatureBand.High => "HIGH TEMP POTION",
            _ => "Unknown"
        };
    }

    public static PotionTemperature ToPotionTemperature(CraftTemperatureBand band)
    {
        return band switch
        {
            CraftTemperatureBand.Low => PotionTemperature.Low,
            CraftTemperatureBand.Mid => PotionTemperature.Mid,
            CraftTemperatureBand.High => PotionTemperature.High,
            _ => PotionTemperature.Failure
        };
    }
}
