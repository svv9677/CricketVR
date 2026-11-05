/// <summary>
/// FNV-1a over a string: the same on Mono and IL2CPP, run after run (string.GetHashCode is not, so
/// it cannot seed anything that has to come out the same next time). Phase 7 seeds its situations
/// and coach goals with it.
/// </summary>
public static class StableHash
{
    public static int Fnv1a(string key)
    {
        unchecked
        {
            uint h = 2166136261u;
            if (key != null)
                for (int i = 0; i < key.Length; i++)
                {
                    h ^= key[i];
                    h *= 16777619u;
                }
            return (int)h;
        }
    }
}
