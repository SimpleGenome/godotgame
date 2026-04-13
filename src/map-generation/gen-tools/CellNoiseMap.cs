using Godot;
using System;
using System.Collections.Generic;

public static class CellNoiseHelper
{
    public class CellData
    {
        public int Id;

        // Random seed point for this cell on the map
        public Vector2I Site;

        // Display color for this cell
        public Color Color;

        // Every pixel position that belongs to this cell
        public List<Vector2I> Pixels = new();
    }

    public class CellMapData
    {
        public int Width;
        public int Height;

        // For each [x, y], stores the ID of the owning cell
        public int[,] CellIdMap;

        // Full list of generated cells
        public List<CellData> Cells = new();
    }

    /// <summary>
    /// Generates a Voronoi-style cell map and a bordered texture.
    ///
    /// Parameters:
    /// width     - map width, for example 1024
    /// height    - map height, for example 1024
    /// cellCount - number of random cells to generate
    /// seed      - deterministic random seed
    ///
    /// Returns:
    /// cellMap    - contains Cells and CellIdMap
    /// cellTexture - ImageTexture showing the colored cells with black borders
    /// </summary>
    public static (CellMapData cellMap, ImageTexture cellTexture) GenerateCellMapAndTexture(
        int width,
        int height,
        int cellCount,
        int seed)
    {
        CellMapData cellMap = GenerateCellMap(width, height, cellCount, seed);
        Image image = CreateCellImageWithBorders(cellMap);
        ImageTexture texture = ImageTexture.CreateFromImage(image);

        return (cellMap, texture);
    }

    /// <summary>
    /// Generates the cell ownership data only.
    /// </summary>
    public static CellMapData GenerateCellMap(
        int width,
        int height,
        int cellCount,
        int seed)
    {
        CellMapData map = new CellMapData
        {
            Width = width,
            Height = height,
            CellIdMap = new int[width, height]
        };

        Random rng = new Random(seed);

        // Create random cell sites
        for (int i = 0; i < cellCount; i++)
        {
            CellData cell = new CellData
            {
                Id = i,
                Site = new Vector2I(
                    rng.Next(0, width),
                    rng.Next(0, height)
                ),
                Color = new Color(
                    (float)rng.NextDouble(),
                    (float)rng.NextDouble(),
                    (float)rng.NextDouble()
                )
            };

            map.Cells.Add(cell);
        }

        // Assign every pixel to the nearest cell site
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2I pixel = new Vector2I(x, y);
                int nearestCellId = FindNearestCell(pixel, map.Cells);

                map.CellIdMap[x, y] = nearestCellId;
                map.Cells[nearestCellId].Pixels.Add(pixel);
            }
        }

        return map;
    }

    /// <summary>
    /// Builds an image with black borders between cells.
    /// </summary>
    public static Image CreateCellImageWithBorders(CellMapData cellMap)
    {
        Image image = Image.CreateEmpty(cellMap.Width, cellMap.Height, false, Image.Format.Rgba8);

        for (int y = 0; y < cellMap.Height; y++)
        {
            for (int x = 0; x < cellMap.Width; x++)
            {
                int id = cellMap.CellIdMap[x, y];
                Color color = cellMap.Cells[id].Color;

                bool border = false;

                if (x > 0 && cellMap.CellIdMap[x - 1, y] != id) border = true;
                if (x < cellMap.Width - 1 && cellMap.CellIdMap[x + 1, y] != id) border = true;
                if (y > 0 && cellMap.CellIdMap[x, y - 1] != id) border = true;
                if (y < cellMap.Height - 1 && cellMap.CellIdMap[x, y + 1] != id) border = true;

                image.SetPixel(x, y, border ? Colors.Black : color);
            }
        }

        return image;
    }

    private static int FindNearestCell(Vector2I pixel, List<CellData> cells)
    {
        int bestId = 0;
        long bestDistSq = long.MaxValue;

        for (int i = 0; i < cells.Count; i++)
        {
            int dx = pixel.X - cells[i].Site.X;
            int dy = pixel.Y - cells[i].Site.Y;
            long distSq = (long)dx * dx + (long)dy * dy;

            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestId = i;
            }
        }

        return bestId;
    }
}