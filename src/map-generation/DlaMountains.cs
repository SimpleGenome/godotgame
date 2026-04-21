using System;
using System.Collections.Generic;
using Godot;

public partial class DlaMountains
{

    public struct DlaNode
    {
        public Vector2I Pos;
        public int Parent;   // -1 for root
        public int Weight;   // computed later

        public DlaNode(Vector2I pos, int parent)
        {
            Pos = pos;
            Parent = parent;
            Weight = 1;
        }
    }

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
        int size = 64;
        var nodes = new List<DlaNode>();
        var cellToNode = new int[size, size];

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                cellToNode[x, y] = -1;

        Vector2I root = new Vector2I(size / 2, size / 2);
        nodes.Add(new DlaNode(root, -1));
        cellToNode[root.X, root.Y] = 0;

        return ();
    }
}