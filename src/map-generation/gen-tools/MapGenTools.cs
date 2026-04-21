using Godot;

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

    public static float[,] NormaliseMap(float[,] map, float minValue, float maxValue)
    {
        int width = map.GetLength(0);
        int height = map.GetLength(1);

        float[,] normalisedMap = new float[width, height];

        float range = Mathf.Max(maxValue - minValue, 0.0001f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float value = (map[x, y] - minValue) / range;
                normalisedMap[x, y] = Mathf.Clamp(value, 0f, 1f);
            }
        }

        return normalisedMap;
    }

    public static (float, float) FindMinMax(float[,] map)
    {
        int width = map.GetLength(0);
        int height = map.GetLength(1);

        float minValue = float.MaxValue;
        float maxValue = float.MinValue;

        // Find min and max
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float value = map[x, y];

                if (value < minValue) minValue = value;
                if (value > maxValue) maxValue = value;
            }
        }
        return (minValue, maxValue);
    }

    public static Texture2D MapToGreyscaleTexture(float[,] map, int mapSize)
    {
        Image mapImage = Image.CreateEmpty(mapSize, mapSize, false, Image.Format.Rgba8);

        for (int y = 0; y < mapSize; y++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                mapImage.SetPixel(
                    x,
                    y,
                    PickTerrainColorGreyScale(map[x, y])
                );
            }
        }
        ImageTexture mapTexture = ImageTexture.CreateFromImage(mapImage);
        return mapTexture;
    }

    private static Color PickTerrainColorGreyScale(float v)
    {
        return new Color(v, v, v);
    }

    private static Color PickTerrainColorHeightMap(float h)
    {
        if (h < 0.4f)
            return new Color(0.1f + (h * 0.5f), 0.2f + (h * 0.6f), 0.8f);
        if (h < 0.42f)
            return new Color(0.7f + (h * 0.25f), 0.60f + (h * 0.4f), 0.45f + (h * 0.25f));
        if (h < 0.75f)
            return new Color(0.15f + (h * 0.15f), 0.30f + (h * 0.2f), 0.15f + (h * 0.15f));
        if (h < 0.9f)
            return new Color(h - 0.2f, h - 0.2f, h - 0.2f);
        return new Color(h, h, h);
    }
}