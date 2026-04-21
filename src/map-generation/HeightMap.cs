using System;
using Godot;

public partial class HeightMap
{
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
        var continentNoise = new FastNoiseLite
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

        var mountainMaskNoise = new FastNoiseLite
        {
            Seed = seed + 1001,
            Frequency = baseFrequency * 0.55f,
            FractalOctaves = 3,
            FractalLacunarity = 2.0f,
            FractalGain = 0.5f,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin
        };

        var ridgeNoise = new FastNoiseLite
        {
            Seed = seed + 2003,
            Frequency = detailFrequency,
            FractalOctaves = 1,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin
        };

        var warpNoise = new FastNoiseLite
        {
            Seed = seed + 3001,
            Frequency = detailFrequency * 0.35f,
            FractalOctaves = 2,
            FractalLacunarity = 2.0f,
            FractalGain = 0.5f,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin
        };

        var heightCurve = new MonotoneHermiteCurve(
            minValue: -8000f,
            maxValue: 8000f,
            averageValue: 200f,
            runawayThresholdLower: 0.35f,
            runawayThresholdUpper: 0.9f,
            centerSteepness: 1.1f
        );

        float[,] heightMap = new float[mapSize, mapSize];
        Image heightImage = Image.CreateEmpty(mapSize, mapSize, false, Image.Format.Rgba8);

        Random rng = new Random(seed);
        float mountainAngle = (float)(rng.NextDouble() * Mathf.Pi);

        for (int y = 0; y < mapSize; y++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                float px = x / (float)(mapSize - 1);
                float py = y / (float)(mapSize - 1);

                // 1) Base continents / land-sea
                float n1 = (continentNoise.GetNoise2D(x, y) + 1f) * 0.5f;

                float gradient = (px * orientation) + (py * (1f - orientation));
                gradient = 1f - 1.2f * gradient * gradient + 0.5f * gradient * gradient * gradient;

                float continent = (n1 * 0.7f) + (gradient * 0.3f);

                // Softer shaping than height^3
                continent = Mathf.Pow(Mathf.Clamp(continent, 0f, 1f), 1.55f);

                // 2) Inland mask - suppress mountains near coasts
                float inlandMask = Smooth01(biomeLevel - 0.08f, biomeLevel + 0.08f, continent);

                // 3) Broad mountain zones
                float mountainZone = (mountainMaskNoise.GetNoise2D(x, y) + 1f) * 0.5f;
                mountainZone = Smooth01(0.52f, 0.78f, mountainZone);

                // 4) Warped, rotated, stretched coords for long mountain chains
                float warpX = warpNoise.GetNoise2D(x + 137.2f, y - 84.7f) * 90f;
                float warpY = warpNoise.GetNoise2D(x - 211.9f, y + 56.3f) * 90f;

                float wx = x + warpX;
                float wy = y + warpY;

                Rotate(wx, wy, mountainAngle, out float rx, out float ry);

                // Stretch one axis to make long ranges instead of round blobs
                rx *= 0.35f;
                ry *= 1.65f;

                // 5) Ridged mountain detail
                float ridge = RidgedFbm01(ridgeNoise, rx, ry, 5, 2.0f, 0.5f);

                // Sharpen ridges so they form chains
                ridge = Mathf.Pow(ridge, 2.4f);

                // 6) Final mountain contribution
                float mountainStrengthRaw = ridge * mountainZone * inlandMask;
                float mountainStrength = Mathf.Pow(mountainStrengthRaw, 10.0f);
                float height = continent + (mountainStrength * 0.50f);
                height = SteepenUpperRange(height, biomeLevel, 2.3f);
                height = SoftCap(height, 0.96f, 0.30f);

                height = Mathf.Clamp(height, 0f, 1f);

                height = Mathf.Clamp(height, 0f, 1f);
                heightMap[x, y] = height;
            }
        }

        (float minValue, float maxValue) = MapGenTools.FindMinMax(heightMap);

        heightMap = MapGenTools.NormaliseMap(heightMap, minValue, maxValue);

        // Use curve to assign real world units
        heightCurve.EvaluateInPlace(heightMap);

        //Find min / max values
        (minValue, maxValue) = MapGenTools.FindMinMax(heightMap);

        // Normalize to full 0..1 range
        heightMap = MapGenTools.NormaliseMap(heightMap, minValue, maxValue);

        heightCurve.EvaluateInPlace(heightMap);

        (minValue, maxValue) = MapGenTools.FindMinMax(heightMap);

        // Normalize to full 0..1 range
        heightMap = MapGenTools.NormaliseMap(heightMap, minValue, maxValue);


        for (int y = 0; y < mapSize; y++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                heightImage.SetPixel(
                    x,
                    y,
                    PickTerrainColor(heightMap[x, y], seaLevel, coastThickness, biomeLevel, snowLevel)
                );
            }
        }

        ImageTexture heightTexture = ImageTexture.CreateFromImage(heightImage);
        return (heightMap, heightTexture);
    }

    private static float SteepenUpperRange(float h, float startHeight, float steepness)
    {
        if (h <= startHeight)
            return h;

        float t = (h - startHeight) / Mathf.Max(1f - startHeight, 0.0001f);

        // Push mid-high values downward, leaving only the strongest peaks high
        t = Mathf.Pow(t, steepness);

        return startHeight + t * (1f - startHeight);
    }

    private static float RidgedFbm01(
        FastNoiseLite noise,
        float x,
        float y,
        int octaves,
        float lacunarity,
        float gain)
    {
        float sum = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float norm = 0f;

        for (int i = 0; i < octaves; i++)
        {
            float n = noise.GetNoise2D(x * frequency, y * frequency); // [-1, 1]
            float ridge = 1f - Mathf.Abs(n);                          // [0, 1]
            ridge *= ridge;                                           // sharpen each octave a bit

            sum += ridge * amplitude;
            norm += amplitude;

            amplitude *= gain;
            frequency *= lacunarity;
        }

        return norm > 0f ? sum / norm : 0f;
    }

    private static float Smooth01(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp((x - edge0) / Mathf.Max(edge1 - edge0, 0.0001f), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float SoftCap(float value, float start, float strength)
    {
        if (value <= start)
            return value;

        float excess = value - start;
        return start + (excess / (1f + excess * strength));
    }

    private static void Rotate(float x, float y, float angle, out float rx, out float ry)
    {
        float c = Mathf.Cos(angle);
        float s = Mathf.Sin(angle);

        rx = x * c - y * s;
        ry = x * s + y * c;
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

    public static (float[,], ImageTexture) GenerateGradientMagnitudeMap(float[,] heightMap)
    {
        int width = heightMap.GetLength(0);
        int height = heightMap.GetLength(1);

        Image gradientImage = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);

        float[,] gradientMagnitude = new float[width, height];
        float maxMagnitude = 0f;

        // First pass: compute gradient magnitude
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int x0 = Mathf.Max(x - 1, 0);
                int x1 = Mathf.Min(x + 1, width - 1);
                int y0 = Mathf.Max(y - 1, 0);
                int y1 = Mathf.Min(y + 1, height - 1);

                // Central differences
                float dx = (heightMap[x1, y] - heightMap[x0, y]) * 0.5f;
                float dy = (heightMap[x, y1] - heightMap[x, y0]) * 0.5f;

                float magnitude = Mathf.Sqrt(dx * dx + dy * dy);
                gradientMagnitude[x, y] = magnitude;

                if (magnitude > maxMagnitude)
                    maxMagnitude = magnitude;
            }
        }

        maxMagnitude = Mathf.Max(maxMagnitude, 0.0001f);

        // Second pass: write grayscale image
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float g = gradientMagnitude[x, y] / maxMagnitude; // normalize to 0..1
                gradientImage.SetPixel(x, y, new Color(g, g, g, 1f));
            }
        }

        return (gradientMagnitude, ImageTexture.CreateFromImage(gradientImage));
    }

    public static ((float dx, float dy)[,], ImageTexture) GenerateGradientDirectionMap(float[,] heightMap)
    {
        int width = heightMap.GetLength(0);
        int height = heightMap.GetLength(1);

        (float dx, float dy)[,] gradientMap = new (float, float)[width, height];

        Image image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int x0 = Mathf.Max(x - 1, 0);
                int x1 = Mathf.Min(x + 1, width - 1);
                int y0 = Mathf.Max(y - 1, 0);
                int y1 = Mathf.Min(y + 1, height - 1);

                float dx = (heightMap[x1, y] - heightMap[x0, y]) * 0.5f;
                float dy = (heightMap[x, y1] - heightMap[x, y0]) * 0.5f;

                gradientMap[x,y] = (dx, dy);

                dx *= 12;
                dx *= 12;

                // remap from [-1,1] to [0,1] for display
                float r = dx * 0.5f + 0.5f;
                float g = dy * 0.5f + 0.5f;
                float b = 0.0f;

                image.SetPixel(x, y, new Color(r, g, b, 1f));
            }
        }

        return (gradientMap, ImageTexture.CreateFromImage(image));
    }
}