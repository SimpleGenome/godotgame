using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class CellBiomeWfcHelper
{
    public class BiomeCellResult
    {
        public CellNoiseHelper.CellMapData CellMap;
        public Dictionary<int, BiomeRulesHelper.BiomeType> CellBiomes = new();
        public Dictionary<int, HashSet<int>> CellNeighbors = new();
    }

    /// <summary>
    /// Runs WFC on the generated cells and returns:
    /// - updated biome data
    /// - a biome-colored texture with borders
    ///
    /// Parameters:
    /// cellMap   - the generated Voronoi/cell map from CellNoiseHelper
    /// seed      - deterministic seed for biome assignment
    ///
    /// Returns:
    /// biomeResult - biome assignment for each cell
    /// biomeTexture - texture colored by biome
    /// </summary>
    public static (BiomeCellResult biomeResult, ImageTexture biomeTexture) GenerateBiomesAndTexture(
        CellNoiseHelper.CellMapData cellMap,
        int seed)
    {
        Dictionary<BiomeRulesHelper.BiomeType, BiomeRulesHelper.BiomeDefinition> biomeDefs =
            BiomeRulesHelper.CreateBiomeDefinitions();

        BiomeCellResult result = new BiomeCellResult
        {
            CellMap = cellMap,
            CellNeighbors = BuildCellNeighborGraph(cellMap)
        };

        RunWaveFunctionCollapse(result, biomeDefs, seed);
        ApplyBiomeColorsToCells(result, biomeDefs);

        Image image = CreateBiomeImageWithBorders(result, biomeDefs);
        ImageTexture texture = ImageTexture.CreateFromImage(image);

        return (result, texture);
    }

    private static Dictionary<int, HashSet<int>> BuildCellNeighborGraph(CellNoiseHelper.CellMapData cellMap)
    {
        Dictionary<int, HashSet<int>> neighbors = new();

        for (int i = 0; i < cellMap.Cells.Count; i++)
            neighbors[i] = new HashSet<int>();

        for (int y = 0; y < cellMap.Height; y++)
        {
            for (int x = 0; x < cellMap.Width; x++)
            {
                int currentId = cellMap.CellIdMap[x, y];

                if (x + 1 < cellMap.Width)
                {
                    int rightId = cellMap.CellIdMap[x + 1, y];
                    if (rightId != currentId)
                    {
                        neighbors[currentId].Add(rightId);
                        neighbors[rightId].Add(currentId);
                    }
                }

                if (y + 1 < cellMap.Height)
                {
                    int downId = cellMap.CellIdMap[x, y + 1];
                    if (downId != currentId)
                    {
                        neighbors[currentId].Add(downId);
                        neighbors[downId].Add(currentId);
                    }
                }
            }
        }

        return neighbors;
    }

    private static void RunWaveFunctionCollapse(
    BiomeCellResult result,
    Dictionary<BiomeRulesHelper.BiomeType, BiomeRulesHelper.BiomeDefinition> biomeDefs,
    int seed)
    {
        Random rng = new Random(seed);

        List<BiomeRulesHelper.BiomeType> allBiomes = BiomeRulesHelper.GetAllBiomeTypes();
        int cellCount = result.CellMap.Cells.Count;

        Dictionary<int, HashSet<BiomeRulesHelper.BiomeType>> possibilities = new();

        for (int cellId = 0; cellId < cellCount; cellId++)
            possibilities[cellId] = new HashSet<BiomeRulesHelper.BiomeType>(allBiomes);

        if (cellCount < allBiomes.Count)
        {
            GD.PushWarning(
                $"There are only {cellCount} cells but {allBiomes.Count} biomes. " +
                "Not every biome can be guaranteed an initial seed."
            );
        }

        // Place the most restrictive biomes first.
        List<BiomeRulesHelper.BiomeType> biomeSeedOrder = allBiomes
            .OrderBy(b => biomeDefs[b].AllowedNeighbors.Count)
            .ThenBy(b => (int)b)
            .Take(Math.Min(cellCount, allBiomes.Count))
            .ToList();

        Dictionary<int, BiomeRulesHelper.BiomeType> placedStarterBiomes = new();

        bool seededSuccessfully = PlaceInitialBiomeSeeds(
            result,
            biomeDefs,
            rng,
            biomeSeedOrder,
            ref possibilities,
            placedStarterBiomes
        );

        if (!seededSuccessfully)
        {
            GD.PushWarning(
                "Could not place all initial biome seeds without contradiction. " +
                "Continuing with the valid seeds that were placed."
            );
        }

        while (true)
        {
            int nextCell = FindLowestEntropyCell(possibilities);

            if (nextCell == -1)
                break; // all collapsed

            List<BiomeRulesHelper.BiomeType> options = possibilities[nextCell].ToList();
            BiomeRulesHelper.BiomeType chosen = options[rng.Next(options.Count)];

            possibilities[nextCell].Clear();
            possibilities[nextCell].Add(chosen);

            Queue<int> queue = new Queue<int>();
            queue.Enqueue(nextCell);

            bool success = Propagate(result.CellNeighbors, possibilities, biomeDefs, queue);

            if (!success)
            {
                possibilities[nextCell] = GetValidBiomesFromCollapsedNeighbors(
                    nextCell,
                    result.CellNeighbors,
                    possibilities,
                    biomeDefs,
                    allBiomes
                );

                if (possibilities[nextCell].Count == 0)
                    possibilities[nextCell] = new HashSet<BiomeRulesHelper.BiomeType>(allBiomes);

                List<BiomeRulesHelper.BiomeType> retryOptions = possibilities[nextCell].ToList();
                BiomeRulesHelper.BiomeType retryChosen = retryOptions[rng.Next(retryOptions.Count)];

                possibilities[nextCell].Clear();
                possibilities[nextCell].Add(retryChosen);

                queue.Enqueue(nextCell);
                Propagate(result.CellNeighbors, possibilities, biomeDefs, queue);
            }
        }

        for (int cellId = 0; cellId < cellCount; cellId++)
        {
            if (possibilities[cellId].Count == 0)
            {
                result.CellBiomes[cellId] = allBiomes[rng.Next(allBiomes.Count)];
            }
            else
            {
                result.CellBiomes[cellId] = possibilities[cellId].First();
            }
        }
    }

    private static bool Propagate(
        Dictionary<int, HashSet<int>> neighborGraph,
        Dictionary<int, HashSet<BiomeRulesHelper.BiomeType>> possibilities,
        Dictionary<BiomeRulesHelper.BiomeType, BiomeRulesHelper.BiomeDefinition> biomeDefs,
        Queue<int> queue)
    {
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            HashSet<BiomeRulesHelper.BiomeType> currentPossible = possibilities[current];

            foreach (int neighbor in neighborGraph[current])
            {
                HashSet<BiomeRulesHelper.BiomeType> allowedForNeighbor = new();

                foreach (BiomeRulesHelper.BiomeType currentBiome in currentPossible)
                {
                    foreach (BiomeRulesHelper.BiomeType allowed in biomeDefs[currentBiome].AllowedNeighbors)
                        allowedForNeighbor.Add(allowed);
                }

                int beforeCount = possibilities[neighbor].Count;
                possibilities[neighbor].IntersectWith(allowedForNeighbor);

                if (possibilities[neighbor].Count == 0)
                    return false;

                if (possibilities[neighbor].Count < beforeCount)
                    queue.Enqueue(neighbor);
            }
        }

        return true;
    }

    private static int FindLowestEntropyCell(
        Dictionary<int, HashSet<BiomeRulesHelper.BiomeType>> possibilities)
    {
        int bestCell = -1;
        int bestCount = int.MaxValue;

        foreach (var pair in possibilities)
        {
            int count = pair.Value.Count;

            if (count > 1 && count < bestCount)
            {
                bestCount = count;
                bestCell = pair.Key;
            }
        }

        return bestCell;
    }

    private static HashSet<BiomeRulesHelper.BiomeType> GetValidBiomesFromCollapsedNeighbors(
        int cellId,
        Dictionary<int, HashSet<int>> neighborGraph,
        Dictionary<int, HashSet<BiomeRulesHelper.BiomeType>> possibilities,
        Dictionary<BiomeRulesHelper.BiomeType, BiomeRulesHelper.BiomeDefinition> biomeDefs,
        List<BiomeRulesHelper.BiomeType> allBiomes)
    {
        HashSet<BiomeRulesHelper.BiomeType> valid = new(allBiomes);

        foreach (int neighborId in neighborGraph[cellId])
        {
            if (possibilities[neighborId].Count == 1)
            {
                BiomeRulesHelper.BiomeType neighborBiome = possibilities[neighborId].First();

                valid.IntersectWith(biomeDefs[neighborBiome].AllowedNeighbors);
            }
        }

        return valid;
    }

    private static void ApplyBiomeColorsToCells(
        BiomeCellResult result,
        Dictionary<BiomeRulesHelper.BiomeType, BiomeRulesHelper.BiomeDefinition> biomeDefs)
    {
        foreach (CellNoiseHelper.CellData cell in result.CellMap.Cells)
        {
            BiomeRulesHelper.BiomeType biome = result.CellBiomes[cell.Id];
            cell.Color = biomeDefs[biome].Color;
        }
    }

    private static Image CreateBiomeImageWithBorders(
        BiomeCellResult result,
        Dictionary<BiomeRulesHelper.BiomeType, BiomeRulesHelper.BiomeDefinition> biomeDefs)
    {
        CellNoiseHelper.CellMapData cellMap = result.CellMap;

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

    private static void ShuffleList<T>(List<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static bool PlaceInitialBiomeSeeds(
    BiomeCellResult result,
    Dictionary<BiomeRulesHelper.BiomeType, BiomeRulesHelper.BiomeDefinition> biomeDefs,
    Random rng,
    List<BiomeRulesHelper.BiomeType> biomeSeedOrder,
    ref Dictionary<int, HashSet<BiomeRulesHelper.BiomeType>> possibilities,
    Dictionary<int, BiomeRulesHelper.BiomeType> placedStarterBiomes)
    {
        foreach (BiomeRulesHelper.BiomeType biome in biomeSeedOrder)
        {
            List<int> candidateCells = GetStarterCandidateOrder(
                result,
                possibilities,
                placedStarterBiomes,
                biome,
                rng
            );

            bool placedThisBiome = false;

            foreach (int candidateCellId in candidateCells)
            {
                if (!IsStarterPlacementCompatible(
                        candidateCellId,
                        biome,
                        placedStarterBiomes,
                        result.CellNeighbors,
                        biomeDefs))
                {
                    continue;
                }

                Dictionary<int, HashSet<BiomeRulesHelper.BiomeType>> testPossibilities =
                    ClonePossibilities(possibilities);

                testPossibilities[candidateCellId].Clear();
                testPossibilities[candidateCellId].Add(biome);

                Queue<int> queue = new Queue<int>();
                queue.Enqueue(candidateCellId);

                // Propagate immediately after placing this starter biome.
                if (!Propagate(result.CellNeighbors, testPossibilities, biomeDefs, queue))
                    continue;

                possibilities = testPossibilities;
                placedStarterBiomes[candidateCellId] = biome;
                placedThisBiome = true;
                break;
            }

            if (!placedThisBiome)
                return false;
        }

        return true;
    }

    private static List<int> GetStarterCandidateOrder(
        BiomeCellResult result,
        Dictionary<int, HashSet<BiomeRulesHelper.BiomeType>> possibilities,
        Dictionary<int, BiomeRulesHelper.BiomeType> placedStarterBiomes,
        BiomeRulesHelper.BiomeType biome,
        Random rng)
    {
        List<int> candidates = new();

        for (int cellId = 0; cellId < result.CellMap.Cells.Count; cellId++)
        {
            if (placedStarterBiomes.ContainsKey(cellId))
                continue;

            if (!possibilities[cellId].Contains(biome))
                continue;

            candidates.Add(cellId);
        }

        if (candidates.Count == 0)
            return candidates;

        // Prefer larger cells first so restrictive biomes have room to expand.
        // Keep only the top fraction of largest candidates.
        candidates = KeepLargestCandidateCells(result.CellMap, candidates, 0.35f, 8);

        // First seed: choose among large cells, randomized deterministically.
        if (placedStarterBiomes.Count == 0)
        {
            ShuffleList(candidates, rng);
            return candidates;
        }

        // Later seeds:
        // 1. prefer distance from existing starter seeds
        // 2. prefer larger cells when distance is similar
        candidates.Sort((a, b) =>
        {
            long distA = GetMinDistanceSqToPlacedSeeds(result.CellMap, a, placedStarterBiomes.Keys);
            long distB = GetMinDistanceSqToPlacedSeeds(result.CellMap, b, placedStarterBiomes.Keys);

            int cmp = distB.CompareTo(distA); // farther first
            if (cmp != 0)
                return cmp;

            int sizeA = GetCellArea(result.CellMap, a);
            int sizeB = GetCellArea(result.CellMap, b);

            cmp = sizeB.CompareTo(sizeA); // larger first
            if (cmp != 0)
                return cmp;

            return a.CompareTo(b);
        });

        return candidates;
    }


    private static List<int> KeepLargestCandidateCells(
    CellNoiseHelper.CellMapData cellMap,
    List<int> candidates,
    float topFraction,
    int minimumToKeep)
    {
        List<int> sorted = new(candidates);

        sorted.Sort((a, b) =>
        {
            int sizeA = GetCellArea(cellMap, a);
            int sizeB = GetCellArea(cellMap, b);
            return sizeB.CompareTo(sizeA); // larger first
        });

        int keepCount = Mathf.Max(minimumToKeep, Mathf.CeilToInt(sorted.Count * topFraction));
        keepCount = Mathf.Min(keepCount, sorted.Count);

        if (keepCount <= 0)
            return new List<int>();

        return sorted.GetRange(0, keepCount);
    }

    private static int GetCellArea(CellNoiseHelper.CellMapData cellMap, int cellId)
    {
        return cellMap.Cells[cellId].Pixels.Count;
    }

    private static bool IsStarterPlacementCompatible(
        int candidateCellId,
        BiomeRulesHelper.BiomeType candidateBiome,
        Dictionary<int, BiomeRulesHelper.BiomeType> placedStarterBiomes,
        Dictionary<int, HashSet<int>> neighborGraph,
        Dictionary<BiomeRulesHelper.BiomeType, BiomeRulesHelper.BiomeDefinition> biomeDefs)
    {
        foreach (int neighborId in neighborGraph[candidateCellId])
        {
            if (!placedStarterBiomes.TryGetValue(neighborId, out BiomeRulesHelper.BiomeType neighborBiome))
                continue;

            bool candidateAllowsNeighbor =
                biomeDefs[candidateBiome].AllowedNeighbors.Contains(neighborBiome);

            bool neighborAllowsCandidate =
                biomeDefs[neighborBiome].AllowedNeighbors.Contains(candidateBiome);

            if (!candidateAllowsNeighbor || !neighborAllowsCandidate)
                return false;
        }

        return true;
    }

    private static long GetMinDistanceSqToPlacedSeeds(
        CellNoiseHelper.CellMapData cellMap,
        int cellId,
        IEnumerable<int> placedSeedCellIds)
    {
        Vector2I site = cellMap.Cells[cellId].Site;
        long bestDistSq = long.MaxValue;

        foreach (int placedSeedId in placedSeedCellIds)
        {
            Vector2I otherSite = cellMap.Cells[placedSeedId].Site;

            int dx = site.X - otherSite.X;
            int dy = site.Y - otherSite.Y;
            long distSq = (long)dx * dx + (long)dy * dy;

            if (distSq < bestDistSq)
                bestDistSq = distSq;
        }

        return bestDistSq == long.MaxValue ? 0 : bestDistSq;
    }

    private static Dictionary<int, HashSet<BiomeRulesHelper.BiomeType>> ClonePossibilities(
        Dictionary<int, HashSet<BiomeRulesHelper.BiomeType>> source)
    {
        Dictionary<int, HashSet<BiomeRulesHelper.BiomeType>> clone = new();

        foreach (var pair in source)
            clone[pair.Key] = new HashSet<BiomeRulesHelper.BiomeType>(pair.Value);

        return clone;
    }
}