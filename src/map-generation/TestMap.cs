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

	DlaNode[,] coarseNodes = new DlaNode[startingSize, startingSize];

	public (float[,], Texture2D) GenerateMountainOverlay(
		int seed = 0
	)
	{
		Random rng = new Random(seed);
		float[,] grid = new float[startingSize, startingSize];

		int size = grid.GetLength(0);

		grid[size / 2, size / 2] = 1.0f;


		GrowDla(grid, rng);

		Texture2D gridTexture = MapGenTools.MapToGreyscaleTexture(grid, size);

		return (grid, gridTexture);
	}

	private void GrowDla(float[,] grid, Random rng)
	{
		int placed = 1;

		int size = grid.GetLength(0);

		float targetPlaced = grid.GetLength(0) * grid.GetLength(0) / 4;

		while (placed < targetPlaced)
		{
			Vector2I p = SpawnWalkerStart(grid, size, rng);

			while (true)
			{
				if (TryFindTouchingParent(p, size, grid))
				{
					placed++;
					grid[p.X, p.Y] = 1.0f;
					GD.Print($"Walker stuck at x:{p.X} y:{p.Y}");
					break;
				}

				p += RandomStep8(rng);
				p = ClampToBounds(p, size);
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

	private static Vector2I SpawnWalkerStart(float[,] grid, int size, Random rng)
	{
		int walkPosX;
		int walkPosY;
		do
		{
			walkPosX = rng.Next(0, size);
			walkPosY = rng.Next(0, size);
		}
		while (grid[walkPosX, walkPosY] == 1.0f);

		return new Vector2I(walkPosX, walkPosY);
	}

	private static bool TryFindTouchingParent(Vector2I p, int size, float[,] grid)
	{
		foreach (Vector2I d in Neighbors4)
		{
			int nx = p.X + d.X;
			int ny = p.Y + d.Y;

			if (nx < 0 || ny < 0 || nx >= size || ny >= size)
				continue;

			if (grid[nx, ny] == 1.0f)
			{
				return true;
			}
		}

		return false;
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
