using Godot;
using System.Collections.Generic;
using System.Linq;

public static class BiomePostProcessHelper
{
    public static int ScoreMap(CellBiomeWfcHelper.BiomeCellResult result)
    {
        int score = 0;

        if (HasBiomeConnectedOppositeEdges(result, BiomeRulesHelper.BiomeType.Mountains))
            score += 150;

        if (HasBiomeConnectedOppositeEdges(result, BiomeRulesHelper.BiomeType.Ocean))
            score += 100;

        score -= CountSingleCellRegions(result, BiomeRulesHelper.BiomeType.Volcanic) * 25;
        score -= CountSingleCellRegions(result, BiomeRulesHelper.BiomeType.Wasteland) * 12;

        return score;
    }

    public static bool HasBiomeConnectedOppositeEdges(
        CellBiomeWfcHelper.BiomeCellResult result,
        BiomeRulesHelper.BiomeType biome)
    {
        HashSet<int> left = new();
        HashSet<int> right = new();

        foreach (CellNoiseHelper.CellData cell in result.CellMap.Cells)
        {
            if (!result.CellBiomes.TryGetValue(cell.Id, out BiomeRulesHelper.BiomeType cellBiome))
                continue;

            if (cellBiome != biome)
                continue;

            foreach (Vector2I pixel in cell.Pixels)
            {
                if (pixel.X == 0)
                    left.Add(cell.Id);

                if (pixel.X == result.CellMap.Width - 1)
                    right.Add(cell.Id);
            }
        }

        if (left.Count == 0 || right.Count == 0)
            return false;

        Queue<int> queue = new();
        HashSet<int> visited = new(left);

        foreach (int start in left)
            queue.Enqueue(start);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();

            if (right.Contains(current))
                return true;

            foreach (int neighbor in result.CellNeighbors[current])
            {
                if (visited.Contains(neighbor))
                    continue;

                if (result.CellBiomes.TryGetValue(neighbor, out BiomeRulesHelper.BiomeType neighborBiome) &&
                    neighborBiome == biome)
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        return false;
    }

    public static int CountSingleCellRegions(
        CellBiomeWfcHelper.BiomeCellResult result,
        BiomeRulesHelper.BiomeType biome)
    {
        int count = 0;

        foreach (var pair in result.CellBiomes)
        {
            if (pair.Value != biome)
                continue;

            int sameNeighbors = 0;

            foreach (int neighbor in result.CellNeighbors[pair.Key])
            {
                if (result.CellBiomes.TryGetValue(neighbor, out BiomeRulesHelper.BiomeType neighborBiome) &&
                    neighborBiome == biome)
                {
                    sameNeighbors++;
                }
            }

            if (sameNeighbors == 0)
                count++;
        }

        return count;
    }

    public static void ReduceIsolatedBiomeCells(
        CellBiomeWfcHelper.BiomeCellResult result,
        BiomeRulesHelper.BiomeType targetBiome,
        BiomeRulesHelper.BiomeType replacementBiome)
    {
        List<int> toReplace = new();

        foreach (var pair in result.CellBiomes)
        {
            if (pair.Value != targetBiome)
                continue;

            int sameNeighbors = 0;

            foreach (int neighbor in result.CellNeighbors[pair.Key])
            {
                if (result.CellBiomes.TryGetValue(neighbor, out BiomeRulesHelper.BiomeType neighborBiome) &&
                    neighborBiome == targetBiome)
                {
                    sameNeighbors++;
                }
            }

            if (sameNeighbors == 0)
                toReplace.Add(pair.Key);
        }

        foreach (int cellId in toReplace)
            result.CellBiomes[cellId] = replacementBiome;
    }
}