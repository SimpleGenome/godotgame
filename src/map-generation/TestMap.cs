using System;
using Godot;

public partial class TestMap
{
	public int BorderPadding = 1;
	public static int startingSize = 32;

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

		DlaNode[,] coarseNodes = new DlaNode[startingSize, startingSize];

		int size = coarseNodes.GetLength(0);

		coarseNodes[size / 2, size / 2] = new DlaNode
		{
			Occupied = true,
			Value = 1,
			Parent = new Vector2I(-1, -1)
		};

		GrowDla(coarseNodes, rng);

		float[,] grid = new float[startingSize, startingSize];

		Texture2D gridTexture = CreateSharpTextureFromNodes(coarseNodes);

		return (grid, gridTexture);
	}

	private Texture2D CreateSharpTextureFromNodes(DlaNode[,] nodes)
	{
		int textureSize = nodes.GetLength(0);

		float[,] textureGrid = new float[textureSize, textureSize];

		int max = nodes[textureSize / 2, textureSize / 2].Value;

		for (int y = 0; y < textureSize; y++)
		{
			for (int x = 0; x < textureSize; x++)
			{
				// textureGrid[x, y] = 1.0f - (1.0f / (1.0f + nodes[x, y].Value));
				textureGrid[x, y] = 1.0f / max * nodes[x, y].Value;
			}
		}
		// normalise range (currently 0.5 - <1)
		// Normalize(textureGrid);

		Texture2D nodeTexture = MapGenTools.MapToGreyscaleTexture(textureGrid);

		return nodeTexture;
	}

	private void Normalize(float[,] map)
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
		int placed = 1;
		int size = nodes.GetLength(0);
		int targetPlaced = size * size / 6;

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

		while (current.X != -1 && current.Y != -1)
		{
			nodes[current.X, current.Y].Value += 1;
			current = nodes[current.X, current.Y].Parent;

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
}
