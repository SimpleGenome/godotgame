using System;
using Godot;

// hillStart behavior: change 0.45f to 0.35f if you want more smaller hills to matter
// localShadowRange: reduce max from 0.24f to 0.18f if shadows still feel too long
// DirectionalBlurMap(... acrossRadius: 3 ...): raise to 4 if you want shadows to fan wider
// leewardSlope * 0.22f: raise toward 0.30f if you want the lee side to pop harder right behind ridges

public partial class HumidityMap
{
    public static (float[,], ImageTexture) GenerateHumidityMap(
        int mapSize,
        int seed,
        float baseFrequency,
        Vector2 windDirection,
        float[,] heightMap,
        float[,] temperatureMap,
        float seaLevel,
        float coastThickness,
        float biomeLevel,
        float snowLevel
    )
    {
        Vector2 windDir = NormalizeWindDirection(windDirection);

        var broadNoise = new FastNoiseLite
        {
            Seed = seed + 8891,
            Frequency = baseFrequency * 0.50f,
            FractalOctaves = 2,
            FractalLacunarity = 2.0f,
            FractalGain = 0.5f,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth
        };

        var detailNoise = new FastNoiseLite
        {
            Seed = seed + 104729,
            Frequency = baseFrequency * 1.5f,
            FractalOctaves = 3,
            FractalLacunarity = 2.0f,
            FractalGain = 0.45f,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex
        };

        float[,] humidityMap = new float[mapSize, mapSize];
        Image humidityImage = Image.CreateEmpty(mapSize, mapSize, false, Image.Format.Rgba8);

        float[,] distanceToWaterMap = BuildDistanceToWaterMap(heightMap, seaLevel);

        var (windwardLiftMap, rainShadowMap) = BuildOrographicMaps(
            heightMap,
            seaLevel,
            biomeLevel,
            snowLevel,
            windDir
        );

        // Main tuning knobs.
        // These are intentionally a bit exaggerated so biome differences survive Voronoi averaging.
        float baseMoistureReach = mapSize * 0.24f;
        float warmAirReachBonus = mapSize * 0.12f;
        float interiorHumidityFloor = 0.04f;
        float coastalBoostStrength = 0.10f;
        float continentalDryingStrength = 0.08f;
        float lowlandElevationDryingStrength = 0.05f;
        float mountainDryingStrength = 0.24f;
        float coldAirDryingStrength = 0.08f;
        float windwardBoostStrength = 0.24f;
        float rainShadowStrength = 0.40f;
        float broadNoiseAmount = 0.04f;
        float detailNoiseAmount = 0.015f;

        for (int y = 0; y < mapSize; y++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                float h = Saturate(heightMap[x, y]);
                float temp01 = Saturate(temperatureMap[x, y]);

                bool isWater = h <= seaLevel;

                float waterDepth01 = Saturate((seaLevel - h) / Mathf.Max(seaLevel, 0.0001f));
                float landHeight01 = Saturate((h - seaLevel) / Mathf.Max(1f - seaLevel, 0.0001f));

                float distanceToWater = distanceToWaterMap[x, y];
                float moistureReach = baseMoistureReach + warmAirReachBonus * temp01;
                float maritimeInfluence = Mathf.Exp(-distanceToWater / Mathf.Max(moistureReach, 0.0001f));
                maritimeInfluence = Saturate(maritimeInfluence);

                float shorelineBoost = 1f - Smooth01(seaLevel, seaLevel + coastThickness, h);
                maritimeInfluence = Mathf.Max(maritimeInfluence, shorelineBoost * 0.90f);

                float humidityCapacity = Mathf.Lerp(0.75f, 1.10f, temp01);

                float lowlandDrying = landHeight01 * lowlandElevationDryingStrength;

                float mountainStart = Mathf.Lerp(biomeLevel, snowLevel, 0.35f);
                mountainStart = Mathf.Clamp(mountainStart, seaLevel + 0.02f, snowLevel);

                float mountainMask = Smooth01(mountainStart, snowLevel, h);
                float mountainDrying = mountainMask * mountainMask * mountainDryingStrength;

                float coldAirDrying = (1f - temp01) * (1f - maritimeInfluence * 0.35f) * coldAirDryingStrength;

                float humidity;

                if (isWater)
                {
                    humidity = 0.90f + 0.08f * temp01 + 0.04f * waterDepth01;
                }
                else
                {
                    float inlandBase = interiorHumidityFloor + 0.10f * temp01;

                    float maritimeCore = Mathf.Pow(maritimeInfluence, 0.85f);
                    float waterFedHumidity = maritimeCore * (0.40f + 0.20f * temp01) * humidityCapacity;

                    float coastalBonus = shorelineBoost * coastalBoostStrength * humidityCapacity;

                    float continentality = 1f - maritimeInfluence;
                    float continentalDrying =
                        continentality * continentality *
                        continentalDryingStrength *
                        (0.85f + 0.15f * (1f - temp01));

                    float windwardBoost =
                        windwardLiftMap[x, y]
                        * maritimeCore
                        * humidityCapacity
                        * windwardBoostStrength;

                    float rainShadowDrying =
                        Mathf.Pow(rainShadowMap[x, y], 0.85f)
                        * (0.85f + 0.15f * continentality)
                        * rainShadowStrength;

                    humidity =
                        inlandBase
                        + waterFedHumidity
                        + coastalBonus
                        + windwardBoost
                        - continentalDrying
                        - lowlandDrying
                        - mountainDrying
                        - coldAirDrying
                        - rainShadowDrying;
                }

                float n1 = (broadNoise.GetNoise2D(x, y) + 1f) * 0.5f;
                float n2 = (detailNoise.GetNoise2D(x, y) + 1f) * 0.5f;
                float noiseOffset =
                    (n1 - 0.5f) * broadNoiseAmount +
                    (n2 - 0.5f) * detailNoiseAmount;

                float noiseScale = isWater ? 0.25f : 1.0f;

                humidityMap[x, y] = Saturate(humidity + noiseOffset * noiseScale);
            }
        }

        humidityMap = BlurMap(humidityMap, 2);

        for (int y = 0; y < mapSize; y++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                float h = Saturate(heightMap[x, y]);
                float temp01 = Saturate(temperatureMap[x, y]);

                if (h <= seaLevel)
                {
                    float waterDepth01 = Saturate((seaLevel - h) / Mathf.Max(seaLevel, 0.0001f));
                    float waterTarget = Saturate(0.90f + 0.08f * temp01 + 0.04f * waterDepth01);
                    humidityMap[x, y] = Mathf.Max(humidityMap[x, y], waterTarget);
                }
                else
                {
                    float v = Saturate(humidityMap[x, y]);

                    v += windwardLiftMap[x, y] * 0.05f;
                    v -= rainShadowMap[x, y] * 0.08f;
                    v = Saturate(v);

                    v = ApplyLandHumidityContrast(v);

                    humidityMap[x, y] = Mathf.Min(v, 0.88f);
                }
            }
        }

        for (int y = 0; y < mapSize; y++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                float t = Saturate(humidityMap[x, y]);
                humidityMap[x, y] = t;
                humidityImage.SetPixel(x, y, PickTerrainColor(t));
            }
        }

        ImageTexture humidityTexture = ImageTexture.CreateFromImage(humidityImage);
        return (humidityMap, humidityTexture);
    }

    private static (float[,], float[,]) BuildOrographicMaps(
        float[,] heightMap,
        float seaLevel,
        float biomeLevel,
        float snowLevel,
        Vector2 windDir
    )
    {
        int width = heightMap.GetLength(0);
        int height = heightMap.GetLength(1);

        float[,] windwardLift = new float[width, height];
        float[,] rainShadow = new float[width, height];

        float maxDim = Mathf.Max(width, height);

        // Lower threshold so hills and uplands contribute a little.
        float hillStart = Mathf.Lerp(seaLevel, biomeLevel, 0.35f);
        hillStart = Mathf.Clamp(hillStart, seaLevel + 0.01f, snowLevel - 0.02f);

        // Higher threshold still used for "true mountain" behavior.
        float mountainStart = Mathf.Lerp(biomeLevel, snowLevel, 0.35f);
        mountainStart = Mathf.Clamp(mountainStart, seaLevel + 0.02f, snowLevel);

        // Coarser stepping than before to keep cost reasonable.
        float sampleSpacing = 2.5f;
        float maxSearchDistance = maxDim * 0.24f;
        int maxSteps = Mathf.Max(8, Mathf.CeilToInt(maxSearchDistance / sampleSpacing));

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float h = Saturate(heightMap[x, y]);
                bool isWater = h <= seaLevel;

                float orographicMask = Smooth01(hillStart, snowLevel, h);
                float mountainMask = Smooth01(mountainStart, snowLevel, h);

                float hUpwind = SampleMapBilinear(heightMap, x - windDir.X * 1.25f, y - windDir.Y * 1.25f);
                float hDownwind = SampleMapBilinear(heightMap, x + windDir.X * 1.25f, y + windDir.Y * 1.25f);

                // Positive = terrain rising in the wind direction -> windward face.
                float slopeAlongWind = (hDownwind - hUpwind) * 0.5f;

                float localWindwardLift =
                    Mathf.Pow(Saturate(slopeAlongWind * 8.5f), 0.90f) *
                    Mathf.Lerp(0.20f, 1.00f, orographicMask);

                float barrierAccum = 0f;
                float barrierPeak = 0f;

                for (int step = 1; step <= maxSteps; step++)
                {
                    float d = step * sampleSpacing;
                    float sx = x - windDir.X * d;
                    float sy = y - windDir.Y * d;

                    if (sx < 0f || sy < 0f || sx >= width - 1 || sy >= height - 1)
                        break;

                    float upwindHeight = SampleMapBilinear(heightMap, sx, sy);

                    float upwindOrographicMask = Smooth01(hillStart, snowLevel, upwindHeight);
                    float upwindMountainMask = Smooth01(mountainStart, snowLevel, upwindHeight);

                    // A small ridge can shadow a bit; a big mountain more strongly.
                    float relativeBarrier = Saturate((upwindHeight - h + 0.01f) * 1.65f);
                    float barrierStrength =
                        relativeBarrier *
                        Mathf.Lerp(upwindOrographicMask * 0.55f, upwindOrographicMask, upwindMountainMask);

                    // Higher barriers cast longer shadows. Lower hills cast shorter ones.
                    float localShadowRange = Mathf.Lerp(
                        maxDim * 0.05f,
                        maxDim * 0.18f,
                        upwindOrographicMask
                    );

                    float distanceFade = Mathf.Exp(-d / Mathf.Max(localShadowRange, 0.0001f));
                    float contribution = barrierStrength * distanceFade;

                    // Favor accumulated upwind terrain more than a single sharp peak.
                    barrierAccum += contribution * 0.18f;
                    barrierPeak = Mathf.Max(barrierPeak, contribution);
                }

                barrierAccum = Saturate(barrierAccum);
                barrierPeak = Saturate(barrierPeak);

                // Strongest lee drying should be close to the lee slope / immediate descent.
                float leewardSlope =
                    Mathf.Pow(Saturate(-slopeAlongWind * 8.5f), 0.90f) *
                    Mathf.Lerp(0.25f, 1.00f, orographicMask);

                float localRainShadow =
                    Mathf.Clamp(
                        barrierAccum * 0.78f +
                        barrierPeak * 0.22f +
                        leewardSlope * 0.3f,
                        0f,
                        1f
                    );

                // Don’t let the same windward face be classified as strongly shadowed.
                localRainShadow *= 1f - localWindwardLift * 0.75f;

                // Optional visual damping over water so oceans don't inherit strong dry streaks.
                if (isWater)
                {
                    localWindwardLift *= 0.10f;
                    localRainShadow *= 0.25f;
                }

                windwardLift[x, y] = localWindwardLift;
                rainShadow[x, y] = localRainShadow;
            }
        }

        // Windward should stay fairly local.
        windwardLift = DirectionalBlurMap(
            windwardLift,
            windDir,
            alongRadius: 1,
            acrossRadius: 1,
            alongStep: 1.0f,
            acrossStep: 1.0f,
            iterations: 1
        );

        // Rain shadow should spread and blend, especially sideways relative to wind.
        rainShadow = DirectionalBlurMap(
            rainShadow,
            windDir,
            alongRadius: 1,
            acrossRadius: 3,
            alongStep: 1.25f,
            acrossStep: 1.2f,
            iterations: 2
        );

        // Small extra general blur to soften edges.
        rainShadow = BlurMap(rainShadow, 1);

        return (windwardLift, rainShadow);
    }

    private static float[,] DirectionalBlurMap(
        float[,] source,
        Vector2 windDir,
        int alongRadius,
        int acrossRadius,
        float alongStep,
        float acrossStep,
        int iterations
    )
    {
        int width = source.GetLength(0);
        int height = source.GetLength(1);

        Vector2 dir = NormalizeWindDirection(windDir);
        Vector2 perp = new Vector2(-dir.Y, dir.X);

        float[,] current = source;

        for (int pass = 0; pass < iterations; pass++)
        {
            float[,] next = new float[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0f;
                    float weightSum = 0f;

                    for (int a = -alongRadius; a <= alongRadius; a++)
                    {
                        for (int c = -acrossRadius; c <= acrossRadius; c++)
                        {
                            float sx = x + dir.X * (a * alongStep) + perp.X * (c * acrossStep);
                            float sy = y + dir.Y * (a * alongStep) + perp.Y * (c * acrossStep);

                            float alongWeight = 1f / (1f + Mathf.Abs(a) * 1.15f);
                            float acrossWeight = 1f / (1f + Mathf.Abs(c) * 0.65f);
                            float weight = alongWeight * acrossWeight;

                            sum += SampleMapBilinear(current, sx, sy) * weight;
                            weightSum += weight;
                        }
                    }

                    next[x, y] = sum / Mathf.Max(weightSum, 0.0001f);
                }
            }

            current = next;
        }

        return current;
    }

    private static float ApplyLandHumidityContrast(float v)
    {
        v = Saturate(v);

        if (v < 0.5f)
            return 0.5f * Mathf.Pow(v * 2f, 1.28f);

        return 1f - 0.5f * Mathf.Pow((1f - v) * 2f, 1.28f);
    }

    private static Color PickTerrainColor(float t)
    {
        float r = Mathf.Lerp(0.95f, 0.25f, t);
        float g = Mathf.Lerp(0.90f, 0.55f, t);
        float b = Mathf.Lerp(0.70f, 1.00f, t);
        return new Color(r, g, b, 1f);
    }

    private static Vector2 NormalizeWindDirection(Vector2 dir)
    {
        if (dir.LengthSquared() < 0.000001f)
            return Vector2.Right;

        return dir.Normalized();
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

    private static float[,] BlurMap(float[,] source, int iterations)
    {
        int width = source.GetLength(0);
        int height = source.GetLength(1);

        float[,] current = source;

        for (int pass = 0; pass < iterations; pass++)
        {
            float[,] next = new float[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum =
                        GetClamped(current, x - 1, y - 1) * 1f +
                        GetClamped(current, x, y - 1) * 2f +
                        GetClamped(current, x + 1, y - 1) * 1f +

                        GetClamped(current, x - 1, y) * 2f +
                        GetClamped(current, x, y) * 4f +
                        GetClamped(current, x + 1, y) * 2f +

                        GetClamped(current, x - 1, y + 1) * 1f +
                        GetClamped(current, x, y + 1) * 2f +
                        GetClamped(current, x + 1, y + 1) * 1f;

                    next[x, y] = sum / 16f;
                }
            }

            current = next;
        }

        return current;
    }

    private static float GetClamped(float[,] map, int x, int y)
    {
        int width = map.GetLength(0);
        int height = map.GetLength(1);

        x = Mathf.Clamp(x, 0, width - 1);
        y = Mathf.Clamp(y, 0, height - 1);

        return map[x, y];
    }

    private static float SampleMapBilinear(float[,] map, float x, float y)
    {
        int width = map.GetLength(0);
        int height = map.GetLength(1);

        x = Mathf.Clamp(x, 0f, width - 1f);
        y = Mathf.Clamp(y, 0f, height - 1f);

        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        int x1 = Mathf.Min(x0 + 1, width - 1);
        int y1 = Mathf.Min(y0 + 1, height - 1);

        float tx = x - x0;
        float ty = y - y0;

        float a = Mathf.Lerp(map[x0, y0], map[x1, y0], tx);
        float b = Mathf.Lerp(map[x0, y1], map[x1, y1], tx);

        return Mathf.Lerp(a, b, ty);
    }

    private static float[,] BuildDistanceToWaterMap(float[,] heightMap, float seaLevel)
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