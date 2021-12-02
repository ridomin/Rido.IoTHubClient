namespace pnp_memmon;

internal static class RidCounter
{
    static int counter = 0;
    public static int Current => counter;
    public static int NextValue() => Interlocked.Increment(ref counter);
    public static void Reset() => counter = 0;
}

