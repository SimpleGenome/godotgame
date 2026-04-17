using System;
using Godot;

public partial class TemperatureMap
{
    public static (float[,], ImageTexture) GenerateTemperatureMap(
        int mapSize,
        int seed,
        float baseFrequency,
        float orientation,
        float[,] heightMap,
        float seaLevel,
        float coastThickness,
        float biomeLevel,
        float snowLevel
    )
    {
        var temperatureNoise = new FastNoiseLite
        {
            Seed = seed + 7919,
            Frequency = baseFrequency,
            FractalOctaves = 3,
            FractalLacunarity = 2.0f,
            FractalGain = 0.5f,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin
        };

        float[,] temperatureMap = new float[mapSize, mapSize];
        Image temperatureImage = Image.CreateEmpty(mapSize, mapSize, false, Image.Format.Rgba8);

        // Build a smooth "distance to ocean" field from the height map.
        float[,] distanceToOceanMap = BuildDistanceToOceanMap(heightMap, seaLevel);

        // Tune these
        float maritimeRange = mapSize * 0.05f;      // how far ocean influence reaches inland
        float seaCoolingStrength = 0.25f;            // how much cooler the ocean itself is
        float inlandWarmingStrength = 0.035f;       // slight continental warming inland
        float elevationCoolingStrength = 0.08f;     // general cooling with altitude
        float mountainCoolingStrength = 0.2f;       // extra high-mountain cooling
        float noiseAmount = 0.08f;                  // temperature noise

        float minTemp = float.MaxValue;
        float maxTemp = float.MinValue;

        for (int y = 0; y < mapSize; y++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                float px = x / (float)(mapSize - 1);
                float py = y / (float)(mapSize - 1);

                // Main climate direction
                float gradient = (px * orientation) + (py * (1f - orientation));

                float h = Mathf.Clamp(heightMap[x, y], 0f, 1f);

                // Ocean depth: 1 in deeper ocean, 0 on land
                float oceanDepth01 = Saturate((seaLevel - h) / Mathf.Max(seaLevel, 0.0001f));

                // General land height above sea level
                float landHeight01 = Saturate((h - seaLevel) / Mathf.Max(1f - seaLevel, 0.0001f));

                // Mountain mask ramps up only in higher terrain
                float mountainMask01 = Smooth01(biomeLevel, snowLevel, h);

                // Distance-based maritime influence
                float distanceToOcean = distanceToOceanMap[x, y];
                float maritimeInfluence = Mathf.Exp(-distanceToOcean / Mathf.Max(maritimeRange, 0.0001f));
                maritimeInfluence = Mathf.Clamp(maritimeInfluence, 0f, 1f);

                // Extra shoreline smoothing based on height near sea level
                float shorelineInfluence = 1f - Smooth01(seaLevel, seaLevel + coastThickness, h);
                maritimeInfluence = Mathf.Max(maritimeInfluence, shorelineInfluence * 0.65f);

                // Ocean temperature is only slightly cooler than the latitude gradient,
                // so land/ocean contrast is smoother.
                float seaTemperature = gradient - (seaCoolingStrength * (0.4f + 0.6f * oceanDepth01));

                // Blend land climate toward sea climate near the coast
                float maritimeTemperature = Mathf.Lerp(gradient, seaTemperature, maritimeInfluence);

                // Slight inland warming away from the ocean.
                // This gives you a more realistic continental interior without sharp borders.
                float continentality = 1f - maritimeInfluence;
                float inlandWarming = continentality * inlandWarmingStrength * (1f - mountainMask01) * (1f - oceanDepth01);

                // Smooth elevation cooling everywhere on land
                float elevationCooling = landHeight01 * elevationCoolingStrength;

                // Extra cooling only in high mountains
                float mountainCooling = mountainMask01 * mountainMask01 * mountainCoolingStrength;

                // Small climate variation noise
                float noise = (temperatureNoise.GetNoise2D(x, y) + 1f) * 0.5f;
                float noiseOffset = (noise - 0.5f) * noiseAmount;

                float temperature =
                    maritimeTemperature
                    + inlandWarming
                    - elevationCooling
                    - mountainCooling
                    + noiseOffset;

                temperature = Mathf.Clamp(temperature, 0f, 1f);

                temperatureMap[x, y] = temperature;

                if (temperature < minTemp) minTemp = temperature;
                if (temperature > maxTemp) maxTemp = temperature;
            }
        }

        // Normalize once at the end so the full color range is used smoothly
        float range = Mathf.Max(maxTemp - minTemp, 0.0001f);

        for (int y = 0; y < mapSize; y++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                float t = (temperatureMap[x, y] - minTemp) / range;
                temperatureMap[x, y] = t;
                temperatureImage.SetPixel(x, y, PickTerrainColor(t));
            }
        }

        ImageTexture temperatureTexture = ImageTexture.CreateFromImage(temperatureImage);
        return (temperatureMap, temperatureTexture);
    }

    private static Color PickTerrainColor(float t)
    {
        return new Color(t, 0.0f, 1.0f - t);
    }

    private static float Saturate(float v)
    {
        return Mathf.Clamp(v, 0f, 1f);
    }

    private static float Smooth01(float edge0, float edge1, float x)
    {
        float t = Saturate((x - edge0) / Mathf.Max(edge1 - edge0, 0.0001f));
        return t * t * (3f - 2f * t);
    }

    // Fast 2-pass chamfer distance transform.
    // Ocean pixels (height <= seaLevel) are distance 0.
    private static float[,] BuildDistanceToOceanMap(float[,] heightMap, float seaLevel)
    {
        int width = heightMap.GetLength(0);
        int height = heightMap.GetLength(1);

        float[,] dist = new float[width, height];

        const float INF = 1_000_000f;
        const float ORTHO = 1f;
        const float DIAG = 1.41421356f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                dist[x, y] = heightMap[x, y] <= seaLevel ? 0f : INF;
            }
        }

        // Forward pass
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float best = dist[x, y];

                if (x > 0) best = Mathf.Min(best, dist[x - 1, y] + ORTHO);
                if (y > 0) best = Mathf.Min(best, dist[x, y - 1] + ORTHO);
                if (x > 0 && y > 0) best = Mathf.Min(best, dist[x - 1, y - 1] + DIAG);
                if (x < width - 1 && y > 0) best = Mathf.Min(best, dist[x + 1, y - 1] + DIAG);

                dist[x, y] = best;
            }
        }

        // Backward pass
        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = width - 1; x >= 0; x--)
            {
                float best = dist[x, y];

                if (x < width - 1) best = Mathf.Min(best, dist[x + 1, y] + ORTHO);
                if (y < height - 1) best = Mathf.Min(best, dist[x, y + 1] + ORTHO);
                if (x < width - 1 && y < height - 1) best = Mathf.Min(best, dist[x + 1, y + 1] + DIAG);
                if (x > 0 && y < height - 1) best = Mathf.Min(best, dist[x - 1, y + 1] + DIAG);

                dist[x, y] = best;
            }
        }

        return dist;
    }
}