using UnityEngine;

public static class RoomSeasonResolver
{
    public static SeasonType Resolve(RoomData roomData)
    {
        if (roomData == null)
        {
            return SeasonType.Unknown;
        }

        if (roomData.useSeasonOverride && roomData.seasonOverride != SeasonType.Unknown)
        {
            return roomData.seasonOverride;
        }

        SeasonType resolved = ResolveFromName(roomData.roomID);
        if (resolved != SeasonType.Unknown)
        {
            return resolved;
        }

        if (roomData.roomPrefab != null)
        {
            resolved = ResolveFromName(roomData.roomPrefab.name);
            if (resolved != SeasonType.Unknown)
            {
                return resolved;
            }
        }

        return SeasonType.Unknown;
    }

    private static SeasonType ResolveFromName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return SeasonType.Unknown;
        }

        string normalized = value.Trim().ToLowerInvariant();

        if (normalized.StartsWith("spr_"))
        {
            return SeasonType.Spring;
        }

        if (normalized.StartsWith("sum_"))
        {
            return SeasonType.Summer;
        }

        if (normalized.StartsWith("aut_") || normalized.StartsWith("aut"))
        {
            return SeasonType.Autumn;
        }

        if (normalized.StartsWith("win_"))
        {
            return SeasonType.Winter;
        }

        return SeasonType.Unknown;
    }
}
