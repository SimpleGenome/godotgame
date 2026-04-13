
public partial class MapGenTools
{
    private static System.Random _rng;

    public static void InitRandom(int seed)
    {
        _rng = new System.Random(seed);
    }

    public static float NextRandomFloat()
    {
        return (float)_rng.NextDouble();
    }
}