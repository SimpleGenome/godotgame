using System;
using System.Collections.Generic;
using Godot;

public partial class HeightMap
{
    // to drive real gameplay terrain.

    public static (float[,], ImageTexture) GenerateHeightMap(
        int mapSize,
        int seed,
        float baseFrequency,
        float detailFrequency,
        float orientation,
        float seaLevel,
        float coastThickness,
        float biomeLevel,
        float snowLevel
    )
    {
        var baseNoise = new FastNoiseLite
        {
            Seed = seed,
            Frequency = baseFrequency,
            FractalOctaves = 5,
            FractalLacunarity = 2.0f,
            FractalGain = 0.5f,
            DomainWarpEnabled = true,
            DomainWarpAmplitude = 35.0f,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin
        };

        float[,] heightMap = new float[mapSize, mapSize];

        Image heightImage = Image.CreateEmpty(mapSize, mapSize, false, Image.Format.Rgba8);

        float highestPixel = 0.5f;


        for (int y = 0; y < mapSize; y++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                float n1 = baseNoise.GetNoise2D(x, y);

                n1 = (n1 + 1f) * 0.5f;

                float pixelXDistance = x / (float)(mapSize - 1);
                float pixelYDistance = y / (float)(mapSize - 1);

                float gradient = (pixelXDistance * orientation) + (pixelYDistance * (1f - orientation));
                gradient = 1f - 1.2f * gradient * gradient + 0.5f * gradient * gradient * gradient; ;
                float height = (n1 * 0.7f) + (gradient * 0.3f);

                height *= height * height;

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
                List<float> heightIncreaseScales = [0.1f, 0.3f, 0.5f, 0.65f];
                foreach (var heightLevel in heightIncreaseScales)
                {
                    if (heightMap[x, y] >= heightLevel)
                    {
                        float fullHeightDiff = 1.0f - heightMap[x, y];
                        float cutoffHeight = heightMap[x, y] - heightLevel;
                        heightMap[x, y] += fullHeightDiff * cutoffHeight;
                    }
                }
                heightMap[x, y] = ReduceBelowCutoff(1.0f, heightMap[x, y], 0.35f);
                heightImage.SetPixel(x, y, PickTerrainColor(heightMap[x, y], seaLevel, coastThickness, biomeLevel, snowLevel));
            }
        }

        ImageTexture heightTexture = ImageTexture.CreateFromImage(heightImage);

        return (heightMap, heightTexture);
    }

    public static float ReduceBelowCutoff(float cutoff, float height, float minimum)
    {
        if (cutoff <= 0.0 || cutoff > 1.0)
            throw new ArgumentOutOfRangeException(nameof(cutoff), "cutoff must be > 0 and < 1.");

        if (height < 0.0 || height > 1.0)
            throw new ArgumentOutOfRangeException(nameof(height), "height must be between 0 and 1.");

        if (minimum < 0.0 || minimum >= cutoff)
            throw new ArgumentOutOfRangeException(nameof(minimum), "minimum must be >= 0 and < cutoff.");

        // Outside the affected range, leave unchanged
        if (height <= minimum || height >= cutoff)
            return height;

        float range = cutoff - minimum;
        float t = (height - minimum) / range;   // 0 = minimum, 1 = cutoff

        // SmoothStep helper
        static float SmoothStep(float x) => (float)(x * x * (3.0 - 2.0 * x));

        // Soft shoulder near the cutoff, then steeper reduction below it.
        // Peak effect is slightly below the cutoff, not exactly at it.
        float reduction = (float)(SmoothStep(t * t) * Math.Sqrt(1.0 - t));

        float adjustedT = t - reduction;

        // Clamp to valid range just in case of floating-point edge cases
        adjustedT = (float)Math.Max(0.0, Math.Min(1.0, adjustedT));

        return minimum + adjustedT * range;
    }

    private static Color PickTerrainColor(float h, float seaLevel, float coastThickness, float biomeLevel, float snowLevel)
    {
        if (h < seaLevel)
            return new Color(0.1f + (h * 0.5f), 0.2f + (h * 0.6f), 0.8f);
        if (h < seaLevel + coastThickness)
            return new Color(0.7f + (h * 0.25f), 0.60f + (h * 0.4f), 0.45f + (h * 0.25f));
        if (h < biomeLevel)
            return new Color(0.15f + (h * 0.15f), 0.30f + (h * 0.2f), 0.15f + (h * 0.15f));
        if (h < snowLevel)
            return new Color(h - 0.2f, h - 0.2f, h - 0.2f);
        return new Color(h, h, h);
    }
}