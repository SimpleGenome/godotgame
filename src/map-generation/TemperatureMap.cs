using System;
using System.Collections.Generic;
using Godot;

public partial class TemperatureMap
{
    // to drive real gameplay terrain.

    public static (float[,], ImageTexture) GenerateTemperatureMap(
        int mapSize,
        int seed,
        float baseFrequency,
        float detailFrequency,
        float orientation,
        float[,] heightMap
    )
    {
        // Big shapes
        var baseNoise = new FastNoiseLite
        {
            Seed = seed,
            Frequency = 0.001f,
            FractalOctaves = 5,
            FractalLacunarity = 2.0f,
            FractalGain = 0.5f,
            DomainWarpEnabled = true,
            DomainWarpAmplitude = 35.0f,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin
        };

        float[,] temperatureMap = new float[mapSize, mapSize];

        Image temperatureImage = Image.CreateEmpty(mapSize, mapSize, false, Image.Format.Rgba8);

        float hottestPixel = 0.5f;
        GD.Print("Orientation in gen temp map: " + orientation);

        for (int y = 0; y < mapSize; y++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                // float n1 = baseNoise.GetNoise2D(x, y);

                // n1 = (n1 + 1f) * 0.5f;

                float pixelXDistance = x / (float)(mapSize - 1);
                float pixelYDistance = y / (float)(mapSize - 1);

                float gradient = (pixelXDistance * orientation) + (pixelYDistance * (1f - orientation));
                float temperature = (gradient * 0.5f) + ((1 - heightMap[x, y]) * 0.5f);

                temperature = Mathf.Clamp(temperature, 0f, 1f);

                if (hottestPixel < temperature)
                {
                    hottestPixel = temperature;
                }

                temperatureMap[x, y] = temperature;
            }
        }

        for (int y = 0; y < mapSize; y++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                float scale = 1 / hottestPixel;
                // temperatureMap[x, y] *= scale;
                temperatureImage.SetPixel(x, y, PickTerrainColor(temperatureMap[x, y]));
            }
        }


        ImageTexture heightTexture = ImageTexture.CreateFromImage(temperatureImage);

        return (temperatureMap, heightTexture);
    }

    private static Color PickTerrainColor(float t)
    {
        return new Color(t, 0.0f, 1.0f - t);
    }
}