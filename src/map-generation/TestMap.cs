using System;
using System.Numerics;
using Godot;

public partial class TestMap
{
	public int BorderPadding = 1;
	public static int startingSize = 16;
	private int upscaleValue = 2;
	private int targetSize = 1024;

	private struct DlaNode
	{
		public bool Occupied;
		public Vector2I Parent;   // (-1, -1) for root
		public int Value;         // your accumulated subtree/count value
	}


	public (float[,], Texture2D) GenerateMountainOverlay(
		int seed = 0
	)
	{
		Random rng = new Random(seed);

		DlaNode[,] dlaNodes = new DlaNode[startingSize, startingSize];

		int size = dlaNodes.GetLength(0);

		dlaNodes[size / 2, size / 2] = new DlaNode
		{
			Occupied = true,
			Value = 1,
			Parent = new Vector2I(-1, -1)
		};

		/* 
		Grow DLA

		create node heightmap

		--- LOOP ---

			upscale and blur heightmap

			upscale nodes

			Grow DLA

			create node height map

			merge height maps
		*/

		GrowDla(dlaNodes, rng);

		float[,] heightMap = CreateHeightMapFromNodes(dlaNodes);
		float[,] bilinearUpscaledHeightMap;
		float[,] convolutedHeightMap;
		float[,] upscaledNodeHeightMap;

		for (int i = 0; i < 5; i++)
		{
			// --- bilinear interpolation upscale ---
			bilinearUpscaledHeightMap = UpscaleLinear(heightMap, upscaleValue);

			// --- soft blur - Convolution weighted average of all neighbours ---
			convolutedHeightMap = ConvoluteMapRepeated(bilinearUpscaledHeightMap, 8);

			// --- Create upscaled DLA Node map ---
			dlaNodes = UpscaleDlaNodeMap(dlaNodes, upscaleValue);

			// --- Add new pixels to the upscaled DLA node map ---
			GrowDla(dlaNodes, rng);

			// --- Create height map for the upscaled and expanded DLA node map ---
			upscaledNodeHeightMap = CreateHeightMapFromNodes(dlaNodes);

			// --- Merge Convoluted Bilinear Interp Upscale + Upscaled DLA Map ---
			heightMap = MergeUpscaledMaps(convolutedHeightMap, upscaledNodeHeightMap, 0.05f);
		}

		Normalize(heightMap);

		// --- Create Texture2D to display ---
		Texture2D combinedMapTexture = CreateTextureFromMap(heightMap);

		return (heightMap, combinedMapTexture);
	}

	public static float[,] MergeUpscaledMaps(float[,] blurMap, float[,] nodeMap, float ratio)
	{
		int size = blurMap.GetLength(0);
		float[,] result = new float[size, size];

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				result[x, y] = (blurMap[x, y] * (1 - ratio)) + (nodeMap[x, y] * ratio);
			}
		}
		// Normalize(result);
		return result;
	}

	public static float[,] ConvoluteMapWeighted(float[,] map)
	{
		if (map == null)
			throw new ArgumentNullException(nameof(map));

		int size = map.GetLength(0);

		if (size != map.GetLength(1))
			throw new ArgumentException("ConvoluteMapWeighted currently expects a square map.", nameof(map));

		float[,] convolutedMap = new float[size, size];

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float total = map[x, y] * 1f;
				float weightTotal = 1f;

				foreach (Vector2I n in Neighbors4)
				{
					int nx = x + n.X;
					int ny = y + n.Y;

					if (nx < 0 || ny < 0 || nx >= size || ny >= size)
					{
						continue;
					}

					total += map[nx, ny];
					weightTotal += 1f;
				}

				convolutedMap[x, y] = total / weightTotal;
			}
		}

		return convolutedMap;
	}

	public static float[,] ConvoluteMapRepeated(float[,] map, int iterations)
	{
		if (map == null)
			throw new ArgumentNullException(nameof(map));

		if (iterations < 0)
			throw new ArgumentOutOfRangeException(nameof(iterations), "Iterations cannot be negative.");

		float[,] currentMap = map;

		for (int i = 0; i < iterations; i++)
		{
			currentMap = ConvoluteMapWeighted(currentMap);
		}

		return currentMap;
	}

	public static float[,] UpscaleLinear(float[,] map, int upscaleValue)
	{
		if (map == null)
			throw new ArgumentNullException(nameof(map));

		if (upscaleValue < 1)
			throw new ArgumentOutOfRangeException(nameof(upscaleValue), "Upscale value must be 1 or greater.");

		int oldWidth = map.GetLength(0);
		int oldHeight = map.GetLength(1);

		if (oldWidth == 0 || oldHeight == 0)
			throw new ArgumentException("Map must not be empty.", nameof(map));

		int newWidth = oldWidth * upscaleValue;
		int newHeight = oldHeight * upscaleValue;

		float[,] result = new float[newWidth, newHeight];

		for (int x = 0; x < newWidth; x++)
		{
			for (int y = 0; y < newHeight; y++)
			{
				float sourceX = oldWidth == 1
					? 0f
					: x * (oldWidth - 1f) / (newWidth - 1f);

				float sourceY = oldHeight == 1
					? 0f
					: y * (oldHeight - 1f) / (newHeight - 1f);

				int x0 = Mathf.FloorToInt(sourceX);
				int y0 = Mathf.FloorToInt(sourceY);

				int x1 = Math.Min(x0 + 1, oldWidth - 1);
				int y1 = Math.Min(y0 + 1, oldHeight - 1);

				float tx = sourceX - x0;
				float ty = sourceY - y0;

				float bottomLeft = map[x0, y0];
				float bottomRight = map[x1, y0];
				float topLeft = map[x0, y1];
				float topRight = map[x1, y1];

				float bottom = Lerp(bottomLeft, bottomRight, tx);
				float top = Lerp(topLeft, topRight, tx);

				result[x, y] = Lerp(bottom, top, ty);
			}
		}

		return result;
	}

	private static float Lerp(float a, float b, float t)
	{
		return a + (b - a) * t;
	}

	private float[,] CreateHeightMapFromNodes(DlaNode[,] nodes)
	{
		int size = nodes.GetLength(0);

		float[,] textureGrid = new float[size, size];

		// only used if normalising
		int max = nodes[size / 2, size / 2].Value;

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				textureGrid[x, y] = 1.0f - (1.0f / (1.0f + nodes[x, y].Value));
				// textureGrid[x, y] = 1.0f / max * nodes[x, y].Value;
			}
		}

		// normalise range (currently 0.5 - <1)
		// Normalize(textureGrid);

		return textureGrid;
	}

	private Texture2D CreateTextureFromMap(float[,] map)
	{
		Texture2D mapTexture = MapGenTools.MapToGreyscaleTexture(map);

		return mapTexture;
	}

	private static void Normalize(float[,] map)
	{
		int s = map.GetLength(0);

		float minV = float.MaxValue;
		float maxV = float.MinValue;
		bool foundNonZero = false;

		for (int y = 0; y < s; y++)
		{
			for (int x = 0; x < s; x++)
			{
				float v = map[x, y];
				if (v > 1e-6f)
				{
					minV = Math.Min(minV, v);
					maxV = Math.Max(maxV, v);
					foundNonZero = true;
				}
			}
		}

		if (!foundNonZero)
		{
			for (int y = 0; y < s; y++)
				for (int x = 0; x < s; x++)
					map[x, y] = 0f;
			return;
		}

		float range = maxV - minV;
		if (range < 1e-6f)
		{
			for (int y = 0; y < s; y++)
			{
				for (int x = 0; x < s; x++)
				{
					if (map[x, y] > 1e-6f)
						map[x, y] = 1f;
					else
						map[x, y] = 0f;
				}
			}
			return;
		}

		for (int y = 0; y < s; y++)
		{
			for (int x = 0; x < s; x++)
			{
				if (map[x, y] > 1e-6f)
					map[x, y] = (map[x, y] - minV) / range;
				else
					map[x, y] = 0f;
			}
		}
	}

	private void GrowDla(DlaNode[,] nodes, Random rng)
	{
		int placed = CountOccupied(nodes);
		int size = nodes.GetLength(0);
		int targetPlaced = size * size / 3
		;

		while (placed < targetPlaced)
		{
			Vector2I walker = SpawnWalkerStart(nodes, size, rng);
			int steps = 0;
			int maxSteps = size * size * 4;

			while (steps < maxSteps)
			{
				if (
					!nodes[walker.X, walker.Y].Occupied &&
					TryFindTouchingParent(walker, size, nodes, out Vector2I parent)
				)
				{
					nodes[walker.X, walker.Y] = new DlaNode
					{
						Occupied = true,
						Parent = parent,
						Value = 1
					};

					AddToAncestorChain(parent, nodes);

					placed++;
					break;
				}

				walker += RandomStep8(rng);
				walker = ClampToBounds(walker, size);
				steps++;
			}
		}
	}

	private static int CountOccupied(DlaNode[,] nodes)
	{
		int count = 0;
		int width = nodes.GetLength(0);
		int height = nodes.GetLength(1);

		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				if (nodes[x, y].Occupied)
					count++;
			}
		}

		return count;
	}

	private static Vector2I RandomStep8(Random rng)
	{
		int dx, dy;
		do
		{
			dx = rng.Next(-1, 2);
			dy = rng.Next(-1, 2);
		}
		while (dx == 0 && dy == 0);

		return new Vector2I(dx, dy);
	}

	private Vector2I ClampToBounds(Vector2I p, int size)
	{
		return new Vector2I(
			Mathf.Clamp(p.X, BorderPadding, size - 1 - BorderPadding),
			Mathf.Clamp(p.Y, BorderPadding, size - 1 - BorderPadding)
		);
	}

	private static Vector2I SpawnWalkerStart(DlaNode[,] nodes, int size, Random rng)
	{
		int walkPosX;
		int walkPosY;
		do
		{
			walkPosX = rng.Next(0, size);
			walkPosY = rng.Next(0, size);
		}
		while (nodes[walkPosX, walkPosY].Occupied == true);

		return new Vector2I(walkPosX, walkPosY);
	}

	private static bool TryFindTouchingParent(
		Vector2I walker,
		int size,
		DlaNode[,] nodes,
		out Vector2I parent)
	{
		if (nodes[walker.X, walker.Y].Occupied)
		{
			parent = new Vector2I(-1, -1);
			return false;
		}

		foreach (Vector2I d in Neighbors4)
		{
			int nx = walker.X + d.X;
			int ny = walker.Y + d.Y;

			if (nx < 0 || ny < 0 || nx >= size || ny >= size)
				continue;

			if (nodes[nx, ny].Occupied)
			{
				parent = new Vector2I(nx, ny);
				return true;
			}
		}

		parent = new Vector2I(-1, -1);
		return false;
	}

	private static void AddToAncestorChain(Vector2I current, DlaNode[,] nodes)
	{
		int guard = 0;
		int maxGuard = nodes.GetLength(0) * nodes.GetLength(1);

		int nodeLevel = 1;

		while (current.X != -1 && current.Y != -1)
		{
			if (nodes[current.X, current.Y].Value <= nodeLevel)
			{
				nodes[current.X, current.Y].Value += 1;
			}
			current = nodes[current.X, current.Y].Parent;

			nodeLevel++;
			guard++;
			if (guard > maxGuard)
				throw new Exception("Parent chain cycle detected.");
		}
	}

	private static readonly Vector2I[] Neighbors8 =
	{
		new Vector2I(-1, -1), new Vector2I(0, -1), new Vector2I(1, -1),
		new Vector2I(-1,  0),                      new Vector2I(1,  0),
		new Vector2I(-1,  1), new Vector2I(0,  1), new Vector2I(1,  1),
	};

	private static readonly Vector2I[] Neighbors4 =
	{
								new Vector2I(0, -1),
		new Vector2I(-1,  0),                           new Vector2I(1,  0),
								new Vector2I(0,  1)
	};

	private static DlaNode[,] UpscaleDlaNodeMap(DlaNode[,] nodes, int upscaleValue)
	{
		if (nodes == null)
			throw new ArgumentNullException(nameof(nodes));

		if (upscaleValue < 1)
			throw new ArgumentOutOfRangeException(nameof(upscaleValue));

		int oldWidth = nodes.GetLength(0);
		int oldHeight = nodes.GetLength(1);

		int newWidth = oldWidth * upscaleValue;
		int newHeight = oldHeight * upscaleValue;

		DlaNode[,] result = new DlaNode[newWidth, newHeight];

		// First pass: place original occupied nodes in their new scaled positions.
		for (int y = 0; y < oldHeight; y++)
		{
			for (int x = 0; x < oldWidth; x++)
			{
				if (!nodes[x, y].Occupied)
					continue;

				Vector2I newPos = new Vector2I(
					x * upscaleValue,
					y * upscaleValue
				);

				result[newPos.X, newPos.Y] = new DlaNode
				{
					Occupied = true,
					Value = 1, // temporary; recalculated later
					Parent = new Vector2I(-1, -1)
				};
			}
		}

		// Second pass: reconnect nodes to their parents,
		// inserting 1-pixel-thick bridge nodes between them.
		for (int y = 0; y < oldHeight; y++)
		{
			for (int x = 0; x < oldWidth; x++)
			{
				if (!nodes[x, y].Occupied)
					continue;

				Vector2I oldParent = nodes[x, y].Parent;

				Vector2I newPos = new Vector2I(
					x * upscaleValue,
					y * upscaleValue
				);

				// Root node.
				if (oldParent.X == -1 && oldParent.Y == -1)
				{
					SetNodeParent(result, newPos, new Vector2I(-1, -1));
					continue;
				}

				Vector2I newParent = new Vector2I(
					oldParent.X * upscaleValue,
					oldParent.Y * upscaleValue
				);

				Vector2I current = newPos;

				while (current != newParent)
				{
					Vector2I next = StepTowards4(current, newParent);

					EnsureNodeExists(result, current);
					EnsureNodeExists(result, next);

					result[current.X, current.Y].Parent = next;

					current = next;
				}
			}
		}

		RecalculateDlaValues(result);

		return result;
	}

	private static Vector2I StepTowards4(Vector2I from, Vector2I to)
	{
		if (from.X < to.X)
			return new Vector2I(from.X + 1, from.Y);

		if (from.X > to.X)
			return new Vector2I(from.X - 1, from.Y);

		if (from.Y < to.Y)
			return new Vector2I(from.X, from.Y + 1);

		if (from.Y > to.Y)
			return new Vector2I(from.X, from.Y - 1);

		return from;
	}

	private static void EnsureNodeExists(DlaNode[,] nodes, Vector2I pos)
	{
		if (nodes[pos.X, pos.Y].Occupied)
			return;

		nodes[pos.X, pos.Y] = new DlaNode
		{
			Occupied = true,
			Value = 1,
			Parent = new Vector2I(-1, -1)
		};
	}

	private static void SetNodeParent(DlaNode[,] nodes, Vector2I pos, Vector2I parent)
	{
		DlaNode node = nodes[pos.X, pos.Y];

		node.Occupied = true;
		node.Value = Math.Max(1, node.Value);
		node.Parent = parent;

		nodes[pos.X, pos.Y] = node;
	}

	private static void RecalculateDlaValues(DlaNode[,] nodes)
	{
		int width = nodes.GetLength(0);
		int height = nodes.GetLength(1);

		bool[,] hasChild = new bool[width, height];

		// Reset values and find which nodes have children.
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				if (!nodes[x, y].Occupied)
					continue;

				DlaNode node = nodes[x, y];
				node.Value = 1;
				nodes[x, y] = node;

				Vector2I parent = node.Parent;

				if (parent.X != -1 && parent.Y != -1)
				{
					hasChild[parent.X, parent.Y] = true;
				}
			}
		}

		// Leaves are occupied nodes with no children.
		// Walk from each leaf up to the root and assign increasing values.
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				if (!nodes[x, y].Occupied)
					continue;

				if (hasChild[x, y])
					continue;

				PropagateValueUpFromLeaf(nodes, new Vector2I(x, y));
			}
		}
	}

	private static void PropagateValueUpFromLeaf(DlaNode[,] nodes, Vector2I leaf)
	{
		int width = nodes.GetLength(0);
		int height = nodes.GetLength(1);

		Vector2I current = leaf;
		int value = 1;

		int guard = 0;
		int maxGuard = width * height;

		while (current.X != -1 && current.Y != -1)
		{
			DlaNode node = nodes[current.X, current.Y];

			if (node.Value < value)
			{
				node.Value = value;
				nodes[current.X, current.Y] = node;
			}

			current = node.Parent;
			value++;

			guard++;
			if (guard > maxGuard)
				throw new Exception("Parent chain cycle detected while recalculating DLA values.");
		}
	}
}
