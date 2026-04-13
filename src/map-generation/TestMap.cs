using Godot;

public partial class TestMap
{
    // to drive real gameplay terrain.

    public static (float[,], ImageTexture) GenerateHeightMap(
        int mapSize,
        int seed,
        float baseFrequency,
        float detailFrequency
    )
    {
        // Big shapes
        var baseNoise = new FastNoiseLite
        {
            Seed = seed,
            Frequency = baseFrequency,
            FractalOctaves = 5,
            FractalLacunarity = 2.0f,
            FractalGain = 0.5f,
            DomainWarpEnabled = true,
            DomainWarpAmplitude = 35.0f
        };

        // Smaller detail on top
        var detailNoise = new FastNoiseLite
        {
            Seed = seed + 1000,
            Frequency = detailFrequency,
            FractalOctaves = 3,
            FractalLacunarity = 2.0f,
            FractalGain = 0.5f
        };

        float[,] heightMap = new float[mapSize, mapSize];

        Image heightImage = Image.CreateEmpty(mapSize, mapSize, false, Image.Format.Rgba8);

        Vector2 center = new Vector2(mapSize / 2f, mapSize / 2f);
        float maxDistance = center.Length();

        float highestPixel = 0.5f;

        float orientation = MapGenTools.NextRandomFloat();
        GD.Print($"orientation: {orientation}");

        for (int y = 0; y < mapSize; y++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                // 1) Sample two layers of noise.
                float n1 = baseNoise.GetNoise2D(x, y);
                float n2 = detailNoise.GetNoise2D(x, y);

                // 2) Remap from [-1, 1] to [0, 1].
                n1 = (n1 + 1f) * 0.5f;
                n2 = (n2 + 1f) * 0.5f;

                // 3) Blend the layers.
                float height = n1 * 0.8f + n2 * 0.2f;
                // height *= height;

                // if (height >= 0.5)
                //     height *= 1.2f;

                // 4) Apply an island-style falloff:
                //    center = more land, edges = more water.
                // Vector2 pixelLocation = new Vector2(x, y);
                // float distance01 = pixelLocation.DistanceTo(center) / maxDistance;
                // float falloff = Mathf.SmoothStep(0f, 1f, distance01);

                //calculate height based on position and gradient
                float pixelXDistance = x / (float)mapSize;
                float pixelYDistance = y / (float)mapSize;
                height = ((pixelXDistance * orientation) + (pixelYDistance * (1 - orientation))) * height;

                // float islandMask = 1f - falloff;
                // islandMask *= islandMask; // stronger push toward water at the edges

                // height *= islandMask;

                // // Small center boost so islands don't vanish too easily.
                // height += (1f - distance01) * 0.10f;

                height = Mathf.Clamp(height, 0f, 1f);
                heightMap[x, y] = height;
                if (highestPixel < height)
                {
                    highestPixel = height;
                }
            }
        }


        for (int y = 0; y < mapSize; y++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                float scale = 1 / highestPixel;
                heightMap[x, y] *= scale;
                heightImage.SetPixel(x, y, PickTerrainColor(heightMap[x, y]));
            }
        }

        ImageTexture heightTexture = ImageTexture.CreateFromImage(heightImage);

        return (heightMap, heightTexture);
    }

    private static Color PickTerrainColor(float h)
    {
        if (h < 0.3f)
            return new Color(0.1f, 0.3f, 0.8f);
        return new Color(h, h, h);
    }
}