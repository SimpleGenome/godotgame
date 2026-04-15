using Godot;

public partial class HeightMap
{
	// to drive real gameplay terrain.
	public static (float[,], ImageTexture) GenerateHeightMap(
		int mapSize,
		int seed,
		float baseFrequency,
		float detailFrequency,
		float seaLevel,
		float coastThickness,
		float biomeLevel,
		float snowLevel
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

		Vector2 center = new Vector2((mapSize - 1) / 2f, (mapSize - 1) / 2f);
		float maxDistance = center.Length();

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
				float height = n1 * 0.75f + n2 * 0.25f;

				// 4) Apply an island-style falloff:
				//    center = more land, edges = more water.
				Vector2 p = new Vector2(x, y);
				float distance01 = p.DistanceTo(center) / maxDistance;
				float falloff = Mathf.SmoothStep(0f, 1f, distance01);

				float islandMask = 1f - falloff;
				islandMask *= islandMask; // stronger push toward water at the edges

				height *= islandMask;

				// Small center boost so islands don't vanish too easily.
				height += (1f - distance01) * 0.10f;

				height = Mathf.Clamp(height, 0f, 1f);
				heightMap[x, y] = height;

				heightImage.SetPixel(x, y, PickTerrainColor(height));
			}
		}

		ImageTexture heightTexture = ImageTexture.CreateFromImage(heightImage);

		return (heightMap, heightTexture);
	}

	private static Color PickTerrainColor(float h)
	{
		if (h < 0.1f)
			return new Color(0.05f, 0.12f, 0.22f); // deep water

		if (h < 0.15f)
			return new Color(0.10f, 0.24f, 0.42f); // shallow water

		if (h < 0.18f)
			return new Color(0.82f, 0.76f, 0.52f); // beach

		if (h < 0.28)
			return new Color(0.18f, 0.52f, 0.24f); // grassland

		if (h < 0.45f)
			return new Color(0.12f, 0.38f, 0.18f); // forest / hills

		return new Color(0.55f, 0.55f, 0.58f); // mountain
	}
}
