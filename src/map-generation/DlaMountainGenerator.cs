using Godot;
using System;
using System.Collections.Generic;

public partial class DlaMountainGenerator : RefCounted
{
	public enum SeedLayout
	{
		CenterSingle,
		CenterLineHorizontal,
		CenterLineVertical,
		Diagonal,
		Ring4
	}

	public enum WalkerSpawnMode
	{
		BoundsBox,
		RingAroundCenter
	}

	private struct DlaNode
	{
		public Vector2I Pos;
		public int Parent;   // -1 = root seed
		public int Weight;   // computed after growth

		public DlaNode(Vector2I pos, int parent)
		{
			Pos = pos;
			Parent = parent;
			Weight = 1;
		}
	}

	// Public tuning knobs
	public int BaseResolution = 64;
	public int FinalResolution = 512;
	public int Seed = 12345;

	public SeedLayout InitialSeedLayoutMode = SeedLayout.CenterLineHorizontal;
	public WalkerSpawnMode ParticleSpawnMode = WalkerSpawnMode.BoundsBox;

	public float BranchJitter = 0.15f;
	public float WeightGain = 0.18f;
	public int BorderPadding = 2;

	// Optional post-shaping
	public bool ApplyRadialFalloff = false;
	public float RadialFalloffStrength = 1.25f;

	private static readonly Vector2I[] Neighbors8 =
	{
		new Vector2I(-1, -1), new Vector2I(0, -1), new Vector2I(1, -1),
		new Vector2I(-1,  0),                      new Vector2I(1,  0),
		new Vector2I(-1,  1), new Vector2I(0,  1), new Vector2I(1,  1),
	};

	public ImageTexture GenerateTexture(int? finalResolutionOverride = null, int? seedOverride = null)
	{
		int finalResolution = finalResolutionOverride ?? FinalResolution;
		int seedValue = seedOverride ?? Seed;

		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)Math.Max(seedValue, 1);

		int size = BaseResolution;
		size = Mathf.Clamp(size, 16, finalResolution);

		List<DlaNode> nodes = new List<DlaNode>();
		int[,] cellToNode = CreateCellMap(size);

		PlaceInitialSeeds(nodes, cellToNode, size);

		GrowDla(nodes, cellToNode, particlesToAdd: 220, maxWalkerSteps: 500, rng);
		AssignWeights(nodes);

		float[,] accumulatedHeight = RasterizeWeightedGraph(nodes, size);

		while (size < finalResolution)
		{
			int nextSize = Math.Min(size * 2, finalResolution);

			nodes = UpscaleGraph(nodes, nextSize, rng, BranchJitter);
			cellToNode = BuildCellMap(nodes, nextSize);

			int newParticles = nextSize switch
			{
				<= 128 => 320,
				<= 256 => 520,
				<= 512 => 900,
				_ => 1400
			};

			int walkerSteps = nextSize switch
			{
				<= 128 => 700,
				<= 256 => 1000,
				<= 512 => 1400,
				_ => 1800
			};

			GrowDla(nodes, cellToNode, newParticles, walkerSteps, rng);
			AssignWeights(nodes);

			float[,] ridgeMap = RasterizeWeightedGraph(nodes, nextSize);

			accumulatedHeight = BilinearUpscale(accumulatedHeight, nextSize);
			accumulatedHeight = SmallBlur(accumulatedHeight);

			float detailGain = nextSize switch
			{
				<= 128 => 0.95f,
				<= 256 => 0.75f,
				<= 512 => 0.55f,
				_ => 0.40f
			};

			AddScaled(accumulatedHeight, ridgeMap, detailGain);

			size = nextSize;
		}

		if (ApplyRadialFalloff)
			ApplyRadialMask(accumulatedHeight, RadialFalloffStrength);

		Normalize(accumulatedHeight);
		return FloatMapToTexture(accumulatedHeight);
	}

	// =====================================================================
	// ===== SEED PLACEMENT / WALKER SPAWN: TWEAK THIS SECTION ============
	// =====================================================================
	//
	// This is the main section to play with if you want different mountain
	// layouts.
	//
	// 1) PlaceInitialSeeds(...) changes the fixed starting ridge anchors.
	// 2) SpawnWalkerStart(...) changes where new DLA particles begin.
	//
	// A few useful ideas:
	// - CenterSingle: one massif
	// - CenterLineHorizontal: mountain range
	// - CenterLineVertical: vertical range
	// - Diagonal: diagonal chain
	// - Ring4: clustered peaks
	//
	// You can also add your own seed layouts here very easily.
	// =====================================================================

	private void PlaceInitialSeeds(List<DlaNode> nodes, int[,] cellToNode, int size)
	{
		int cx = size / 2;
		int cy = size / 2;

		List<Vector2I> seeds = new List<Vector2I>();

		switch (InitialSeedLayoutMode)
		{
			case SeedLayout.CenterSingle:
				seeds.Add(new Vector2I(cx, cy));
				break;

			case SeedLayout.CenterLineHorizontal:
				seeds.Add(new Vector2I(cx - 3, cy));
				seeds.Add(new Vector2I(cx - 1, cy));
				seeds.Add(new Vector2I(cx + 1, cy));
				seeds.Add(new Vector2I(cx + 3, cy));
				break;

			case SeedLayout.CenterLineVertical:
				seeds.Add(new Vector2I(cx, cy - 3));
				seeds.Add(new Vector2I(cx, cy - 1));
				seeds.Add(new Vector2I(cx, cy + 1));
				seeds.Add(new Vector2I(cx, cy + 3));
				break;

			case SeedLayout.Diagonal:
				seeds.Add(new Vector2I(cx - 3, cy - 3));
				seeds.Add(new Vector2I(cx - 1, cy - 1));
				seeds.Add(new Vector2I(cx + 1, cy + 1));
				seeds.Add(new Vector2I(cx + 3, cy + 3));
				break;

			case SeedLayout.Ring4:
				seeds.Add(new Vector2I(cx - 4, cy));
				seeds.Add(new Vector2I(cx + 4, cy));
				seeds.Add(new Vector2I(cx, cy - 4));
				seeds.Add(new Vector2I(cx, cy + 4));
				break;
		}

		for (int i = 0; i < seeds.Count; i++)
		{
			Vector2I p = ClampToBounds(seeds[i], size);
			if (cellToNode[p.X, p.Y] >= 0)
				continue;

			int nodeIndex = nodes.Count;
			nodes.Add(new DlaNode(p, -1));
			cellToNode[p.X, p.Y] = nodeIndex;
		}
	}

	private Vector2I SpawnWalkerStart(List<DlaNode> nodes, int size, RandomNumberGenerator rng)
	{
		if (ParticleSpawnMode == WalkerSpawnMode.RingAroundCenter)
		{
			float angle = rng.RandfRange(0.0f, Mathf.Tau);
			float radius = size * 0.22f;

			int cx = size / 2;
			int cy = size / 2;

			Vector2I p = new Vector2I(
				Mathf.RoundToInt(cx + Mathf.Cos(angle) * radius),
				Mathf.RoundToInt(cy + Mathf.Sin(angle) * radius)
			);

			return ClampToBounds(p, size);
		}

		// BoundsBox mode: spawn around the current occupied region.
		(int minX, int minY, int maxX, int maxY) = ComputeBounds(nodes);

		int margin = Math.Max(6, size / 12);

		minX = Mathf.Clamp(minX - margin, 1, size - 2);
		minY = Mathf.Clamp(minY - margin, 1, size - 2);
		maxX = Mathf.Clamp(maxX + margin, 1, size - 2);
		maxY = Mathf.Clamp(maxY + margin, 1, size - 2);

		int side = rng.RandiRange(0, 3);

		return side switch
		{
			0 => new Vector2I(rng.RandiRange(minX, maxX), minY),
			1 => new Vector2I(rng.RandiRange(minX, maxX), maxY),
			2 => new Vector2I(minX, rng.RandiRange(minY, maxY)),
			_ => new Vector2I(maxX, rng.RandiRange(minY, maxY)),
		};
	}

	// =====================================================================

	private void GrowDla(List<DlaNode> nodes, int[,] cellToNode, int particlesToAdd, int maxWalkerSteps, RandomNumberGenerator rng)
	{
		int size = cellToNode.GetLength(0);
		int placed = 0;
		int attempts = 0;
		int maxAttempts = particlesToAdd * 30;

		while (placed < particlesToAdd && attempts < maxAttempts)
		{
			attempts++;

			Vector2I p = SpawnWalkerStart(nodes, size, rng);
			bool stuck = false;

			for (int step = 0; step < maxWalkerSteps; step++)
			{
				if (cellToNode[p.X, p.Y] < 0 && TryFindTouchingParent(p, cellToNode, out int parent))
				{
					int nodeIndex = nodes.Count;
					nodes.Add(new DlaNode(p, parent));
					cellToNode[p.X, p.Y] = nodeIndex;
					placed++;
					stuck = true;
					break;
				}

				p += RandomStep8(rng);
				p = ClampToBounds(p, size);
			}

			if (!stuck)
			{
				// failed walker; try another
			}
		}
	}

	private bool TryFindTouchingParent(Vector2I p, int[,] cellToNode, out int parent)
	{
		int size = cellToNode.GetLength(0);

		foreach (Vector2I d in Neighbors8)
		{
			int nx = p.X + d.X;
			int ny = p.Y + d.Y;

			if (nx < 0 || ny < 0 || nx >= size || ny >= size)
				continue;

			int idx = cellToNode[nx, ny];
			if (idx >= 0)
			{
				parent = idx;
				return true;
			}
		}

		parent = -1;
		return false;
	}

	private Vector2I RandomStep8(RandomNumberGenerator rng)
	{
		int dx = rng.RandiRange(-1, 1);
		int dy = rng.RandiRange(-1, 1);

		if (dx == 0 && dy == 0)
			dx = 1;

		return new Vector2I(dx, dy);
	}

	private void AssignWeights(List<DlaNode> nodes)
	{
		for (int i = 0; i < nodes.Count; i++)
		{
			DlaNode n = nodes[i];
			n.Weight = 1;
			nodes[i] = n;
		}

		for (int i = nodes.Count - 1; i >= 0; i--)
		{
			DlaNode child = nodes[i];
			if (child.Parent >= 0)
			{
				DlaNode parent = nodes[child.Parent];
				parent.Weight = Math.Max(parent.Weight, child.Weight + 1);
				nodes[child.Parent] = parent;
			}
		}
	}

	private float[,] RasterizeWeightedGraph(List<DlaNode> nodes, int size)
	{
		float[,] map = new float[size, size];

		for (int i = 0; i < nodes.Count; i++)
		{
			DlaNode node = nodes[i];
			if (node.Parent < 0)
				continue;

			DlaNode parent = nodes[node.Parent];
			float h = WeightToHeight(node.Weight);
			DrawLineMax(map, parent.Pos, node.Pos, h);
		}

		return map;
	}

	private float WeightToHeight(int weight)
	{
		return 1.0f - Mathf.Exp(-WeightGain * weight);
	}

	private void DrawLineMax(float[,] map, Vector2I a, Vector2I b, float value)
	{
		int x0 = a.X;
		int y0 = a.Y;
		int x1 = b.X;
		int y1 = b.Y;

		int dx = Math.Abs(x1 - x0);
		int sx = x0 < x1 ? 1 : -1;
		int dy = -Math.Abs(y1 - y0);
		int sy = y0 < y1 ? 1 : -1;
		int err = dx + dy;

		int width = map.GetLength(0);
		int height = map.GetLength(1);

		while (true)
		{
			if (x0 >= 0 && y0 >= 0 && x0 < width && y0 < height)
				map[x0, y0] = Math.Max(map[x0, y0], value);

			if (x0 == x1 && y0 == y1)
				break;

			int e2 = err * 2;
			if (e2 >= dy)
			{
				err += dy;
				x0 += sx;
			}
			if (e2 <= dx)
			{
				err += dx;
				y0 += sy;
			}
		}
	}

	private List<DlaNode> UpscaleGraph(List<DlaNode> src, int newSize, RandomNumberGenerator rng, float jitter)
	{
		List<DlaNode> dst = new List<DlaNode>();
		Dictionary<int, int> oldToNew = new Dictionary<int, int>();

		// Add scaled roots first
		for (int i = 0; i < src.Count; i++)
		{
			if (src[i].Parent >= 0)
				continue;

			Vector2I rootPos = ClampToBounds(src[i].Pos * 2, newSize);
			int idx = dst.Count;
			dst.Add(new DlaNode(rootPos, -1));
			oldToNew[i] = idx;
		}

		// Replace each old segment with two segments via a jittered midpoint
		for (int i = 0; i < src.Count; i++)
		{
			DlaNode sChild = src[i];
			if (sChild.Parent < 0)
				continue;

			Vector2 a = src[sChild.Parent].Pos * 2;
			Vector2 b = sChild.Pos * 2;

			Vector2 mid = (a + b) * 0.5f;
			Vector2 dir = b - a;
			Vector2 perp = dir.Length() > 0.001f
				? new Vector2(-dir.Y, dir.X).Normalized()
				: Vector2.Zero;

			mid += perp * rng.RandfRange(-1.0f, 1.0f) * dir.Length() * jitter;

			Vector2I midI = ClampToBounds(
				new Vector2I(Mathf.RoundToInt(mid.X), Mathf.RoundToInt(mid.Y)),
				newSize
			);

			Vector2I childI = ClampToBounds(
				new Vector2I(Mathf.RoundToInt(b.X), Mathf.RoundToInt(b.Y)),
				newSize
			);

			int parentNew = oldToNew[sChild.Parent];

			int midIndex = dst.Count;
			dst.Add(new DlaNode(midI, parentNew));

			int childIndex = dst.Count;
			dst.Add(new DlaNode(childI, midIndex));

			oldToNew[i] = childIndex;
		}

		return dst;
	}

	private int[,] BuildCellMap(List<DlaNode> nodes, int size)
	{
		int[,] map = CreateCellMap(size);

		for (int i = 0; i < nodes.Count; i++)
		{
			Vector2I p = ClampToBounds(nodes[i].Pos, size);
			DlaNode n = nodes[i];
			n.Pos = p;
			nodes[i] = n;

			if (map[p.X, p.Y] < 0)
				map[p.X, p.Y] = i;
		}

		return map;
	}

	private int[,] CreateCellMap(int size)
	{
		int[,] map = new int[size, size];
		for (int y = 0; y < size; y++)
		for (int x = 0; x < size; x++)
			map[x, y] = -1;
		return map;
	}

	private (int minX, int minY, int maxX, int maxY) ComputeBounds(List<DlaNode> nodes)
	{
		int minX = int.MaxValue;
		int minY = int.MaxValue;
		int maxX = int.MinValue;
		int maxY = int.MinValue;

		for (int i = 0; i < nodes.Count; i++)
		{
			Vector2I p = nodes[i].Pos;
			minX = Math.Min(minX, p.X);
			minY = Math.Min(minY, p.Y);
			maxX = Math.Max(maxX, p.X);
			maxY = Math.Max(maxY, p.Y);
		}

		return (minX, minY, maxX, maxY);
	}

	private Vector2I ClampToBounds(Vector2I p, int size)
	{
		return new Vector2I(
			Mathf.Clamp(p.X, BorderPadding, size - 1 - BorderPadding),
			Mathf.Clamp(p.Y, BorderPadding, size - 1 - BorderPadding)
		);
	}

	private float[,] BilinearUpscale(float[,] src, int newSize)
	{
		int oldW = src.GetLength(0);
		int oldH = src.GetLength(1);

		if (oldW == newSize && oldH == newSize)
			return src;

		float[,] dst = new float[newSize, newSize];

		for (int y = 0; y < newSize; y++)
		{
			float gy = (newSize == 1) ? 0f : ((float)y / (newSize - 1)) * (oldH - 1);
			int y0 = Mathf.FloorToInt(gy);
			int y1 = Math.Min(y0 + 1, oldH - 1);
			float ty = gy - y0;

			for (int x = 0; x < newSize; x++)
			{
				float gx = (newSize == 1) ? 0f : ((float)x / (newSize - 1)) * (oldW - 1);
				int x0 = Mathf.FloorToInt(gx);
				int x1 = Math.Min(x0 + 1, oldW - 1);
				float tx = gx - x0;

				float a = Mathf.Lerp(src[x0, y0], src[x1, y0], tx);
				float b = Mathf.Lerp(src[x0, y1], src[x1, y1], tx);
				dst[x, y] = Mathf.Lerp(a, b, ty);
			}
		}

		return dst;
	}

	private float[,] SmallBlur(float[,] src)
	{
		int w = src.GetLength(0);
		int h = src.GetLength(1);
		float[,] dst = new float[w, h];

		int[] k = { 1, 2, 1 };

		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				float sum = 0f;
				float weight = 0f;

				for (int ky = -1; ky <= 1; ky++)
				{
					int sy = Mathf.Clamp(y + ky, 0, h - 1);

					for (int kx = -1; kx <= 1; kx++)
					{
						int sx = Mathf.Clamp(x + kx, 0, w - 1);
						float wgt = k[kx + 1] * k[ky + 1];
						sum += src[sx, sy] * wgt;
						weight += wgt;
					}
				}

				dst[x, y] = sum / weight;
			}
		}

		return dst;
	}

	private void AddScaled(float[,] baseMap, float[,] addMap, float gain)
	{
		int w = baseMap.GetLength(0);
		int h = baseMap.GetLength(1);

		for (int y = 0; y < h; y++)
		for (int x = 0; x < w; x++)
			baseMap[x, y] += addMap[x, y] * gain;
	}

	private void Normalize(float[,] map)
	{
		int w = map.GetLength(0);
		int h = map.GetLength(1);

		float minV = float.MaxValue;
		float maxV = float.MinValue;

		for (int y = 0; y < h; y++)
		for (int x = 0; x < w; x++)
		{
			float v = map[x, y];
			minV = Math.Min(minV, v);
			maxV = Math.Max(maxV, v);
		}

		float range = maxV - minV;
		if (range < 1e-6f)
		{
			for (int y = 0; y < h; y++)
			for (int x = 0; x < w; x++)
				map[x, y] = 0f;
			return;
		}

		for (int y = 0; y < h; y++)
		for (int x = 0; x < w; x++)
			map[x, y] = (map[x, y] - minV) / range;
	}

	private void ApplyRadialMask(float[,] map, float strength)
	{
		int w = map.GetLength(0);
		int h = map.GetLength(1);

		Vector2 center = new Vector2((w - 1) * 0.5f, (h - 1) * 0.5f);
		float maxDist = center.Length();

		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				float d = new Vector2(x, y).DistanceTo(center) / maxDist;
				float mask = Mathf.Clamp(1.0f - Mathf.Pow(d, strength), 0.0f, 1.0f);
				map[x, y] *= mask;
			}
		}
	}

	private ImageTexture FloatMapToTexture(float[,] map)
	{
		int w = map.GetLength(0);
		int h = map.GetLength(1);

		Image img = Image.Create(w, h, false, Image.Format.Rgba8);

		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				float v = Mathf.Clamp(map[x, y], 0.0f, 1.0f);
				img.SetPixel(x, y, new Color(v, v, v, 1.0f));
			}
		}

		return ImageTexture.CreateFromImage(img);
	}
}
