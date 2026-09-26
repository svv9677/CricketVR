using UnityEngine;

/// <summary>
/// The call: the fielder going for the ball shouts for it - "Mine!", or "Keeper's!" from the
/// keeper - so everyone else, and the batter, knows whose it is. Clips are in
/// Resources/Sounds/Calls (mine_*, keeper_*), rendered with the macOS voices by
/// Tools/render_calls.sh.
/// </summary>
public static class FieldingCalls
{
    private static AudioClip[] mine, keeper;

    public static void Call(IFielder who)
    {
        if (who is not Component c)
            return;
        if (mine == null)
        {
            AudioClip[] all = Resources.LoadAll<AudioClip>("Sounds/Calls");
            mine = System.Array.FindAll(all, a => a.name.StartsWith("mine_"));
            keeper = System.Array.FindAll(all, a => a.name.StartsWith("keeper_"));
        }
        AudioClip[] takes = who.IsKeeper && keeper.Length > 0 ? keeper : mine;
        if (takes.Length == 0)
            return;
        // Rare - one call a shot - so a one-off source is fine. At head height, where a voice is.
        AudioSource.PlayClipAtPoint(takes[Random.Range(0, takes.Length)], c.transform.position + Vector3.up * 1.6f, 1f);
    }
}
