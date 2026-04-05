using System.Collections.Generic;

public class BossDefeatTracker : Singleton<BossDefeatTracker>
{
    private readonly HashSet<string> defeatedBossIds = new();

    public void MarkDefeated(string bossId)
    {
        if (!string.IsNullOrEmpty(bossId))
            defeatedBossIds.Add(bossId);
    }

    public bool IsDefeated(string bossId)
    {
        return !string.IsNullOrEmpty(bossId) && defeatedBossIds.Contains(bossId);
    }

    public List<string> GetDefeatedBossIds()
    {
        return new List<string>(defeatedBossIds);
    }

    public void RestoreFromSave(List<string> ids)
    {
        defeatedBossIds.Clear();
        if (ids != null)
        {
            foreach (string id in ids)
            {
                if (!string.IsNullOrEmpty(id))
                    defeatedBossIds.Add(id);
            }
        }
    }

    public void ClearAll()
    {
        defeatedBossIds.Clear();
    }
}
