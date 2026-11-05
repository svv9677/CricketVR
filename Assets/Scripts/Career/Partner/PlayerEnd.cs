/// <summary>Which end the career player is standing at (phase 3). CONTRACT STUB.</summary>
public static class PlayerEnd
{
    public static bool NonStriker { get; private set; }
    public static event System.Action Changed;
    public static void Set(bool nonStriker)
    {
        if (NonStriker == nonStriker) return;
        NonStriker = nonStriker;
        Changed?.Invoke();
    }
}
