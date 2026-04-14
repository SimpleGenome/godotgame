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
        PromoteClusterBiomes(result);
        // --- EXAMPLE OF BIOME POST PROCESSING ---
        // BiomePostProcessHelper.ReduceIsolatedBiomeCells(
        //     result,
        //     BiomeRulesHelper.BiomeType.Volcanic,
        //     BiomeRulesHelper.BiomeType.Mountains
        // );
        ApplyBiomeColorsToCells(result, biomeDefs);

        Image image = CreateBiomeImageWithBorders(result);
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
            GD.PushWarning("Could not place all initial biome seeds cleanly. Continuing with partial starter seeding.");
        }

        while (true)
        {
            int nextCell = FindLowestEntropyCell(possibilities);

            if (nextCell == -1)
                break;

            List<BiomeRulesHelper.BiomeType> validOptions = GetValidBiomesForCell(
                nextCell,
                result,
                possibilities,
                biomeDefs
            );

            if (validOptions.Count == 0)
            {
                possibilities[nextCell] = new HashSet<BiomeRulesHelper.BiomeType>(allBiomes);
                validOptions = GetValidBiomesForCell(nextCell, result, possibilities, biomeDefs);

                if (validOptions.Count == 0)
                {
                    BiomeRulesHelper.BiomeType fallback = allBiomes[rng.Next(allBiomes.Count)];
                    possibilities[nextCell].Clear();
                    possibilities[nextCell].Add(fallback);
                }
                else
                {
                    List<float> fallbackWeights = GetWeightsForBiomes(
                        nextCell,
                        validOptions,
                        result,
                        possibilities,
                        biomeDefs
                    );

                    BiomeRulesHelper.BiomeType chosenFallback =
                        ChooseWeightedBiome(validOptions, fallbackWeights, rng);

                    possibilities[nextCell].Clear();
                    possibilities[nextCell].Add(chosenFallback);
                }
            }
            else
            {
                List<float> weights = GetWeightsForBiomes(
                    nextCell,
                    validOptions,
                    result,
                    possibilities,
                    biomeDefs
                );

                BiomeRulesHelper.BiomeType chosen =
                    ChooseWeightedBiome(validOptions, weights, rng);

                possibilities[nextCell].Clear();
                possibilities[nextCell].Add(chosen);
            }

            Queue<int> queue = new Queue<int>();
            queue.Enqueue(nextCell);
            Propagate(result.CellNeighbors, possibilities, biomeDefs, queue);
        }

        for (int cellId = 0; cellId < cellCount; cellId++)
        {
            if (possibilities[cellId].Count == 0)
            {
                result.CellBiomes[cellId] = allBiomes[rng.Next(allBiomes.Count)];
            }
            else if (possibilities[cellId].Count == 1)
            {
                result.CellBiomes[cellId] = possibilities[cellId].First();
            }
            else
            {
                List<BiomeRulesHelper.BiomeType> validOptions = GetValidBiomesForCell(
                    cellId,
                    result,
                    possibilities,
                    biomeDefs
                );

                if (validOptions.Count == 0)
                {
                    result.CellBiomes[cellId] = possibilities[cellId].First();
                }
                else
                {
                    List<float> weights = GetWeightsForBiomes(
                        cellId,
                        validOptions,
                        result,
                        possibilities,
                        biomeDefs
                    );

                    result.CellBiomes[cellId] = ChooseWeightedBiome(validOptions, weights, rng);
                }
            }
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

        candidates = KeepLargestCandidateCells(result.CellMap, candidates, 0.35f, 8);

        if (placedStarterBiomes.Count == 0)
        {
            ShuffleList(candidates, rng);
            return candidates;
        }

        candidates.Sort((a, b) =>
        {
            long distA = GetMinDistanceSqToPlacedSeeds(result.CellMap, a, placedStarterBiomes.Keys);
            long distB = GetMinDistanceSqToPlacedSeeds(result.CellMap, b, placedStarterBiomes.Keys);

            int cmp = distB.CompareTo(distA);
            if (cmp != 0)
                return cmp;

            int sizeA = GetCellArea(result.CellMap, a);
            int sizeB = GetCellArea(result.CellMap, b);

            cmp = sizeB.CompareTo(sizeA);
            if (cmp != 0)
                return cmp;

            return a.CompareTo(b);
        });

        return candidates;
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

            bool candidateAllowsNeighbor = biomeDefs[candidateBiome].AllowedNeighbors.Contains(neighborBiome);
            bool neighborAllowsCandidate = biomeDefs[neighborBiome].AllowedNeighbors.Contains(candidateBiome);

            if (!candidateAllowsNeighbor || !neighborAllowsCandidate)
                return false;
        }

        return true;
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

    private static List<BiomeRulesHelper.BiomeType> GetValidBiomesForCell(
        int cellId,
        BiomeCellResult result,
        Dictionary<int, HashSet<BiomeRulesHelper.BiomeType>> possibilities,
        Dictionary<BiomeRulesHelper.BiomeType, BiomeRulesHelper.BiomeDefinition> biomeDefs)
    {
        List<BiomeRulesHelper.BiomeType> valid = new();

        foreach (BiomeRulesHelper.BiomeType biome in possibilities[cellId])
        {
            if (CanPlaceBiomeHere(cellId, biome, result, possibilities, biomeDefs))
                valid.Add(biome);
        }

        return valid;
    }

    private static bool CanPlaceBiomeHere(
        int cellId,
        BiomeRulesHelper.BiomeType biome,
        BiomeCellResult result,
        Dictionary<int, HashSet<BiomeRulesHelper.BiomeType>> possibilities,
        Dictionary<BiomeRulesHelper.BiomeType, BiomeRulesHelper.BiomeDefinition> biomeDefs)
    {
        BiomeRulesHelper.BiomeDefinition def = biomeDefs[biome];
        int sameNeighborCount = 0;

        foreach (int neighborId in result.CellNeighbors[cellId])
        {
            if (possibilities[neighborId].Count != 1)
                continue;

            BiomeRulesHelper.BiomeType neighborBiome = possibilities[neighborId].First();

            if (!def.AllowedNeighbors.Contains(neighborBiome))
                return false;

            if (neighborBiome == biome)
                sameNeighborCount++;
        }

        if (def.MaxSameNeighbors.HasValue && sameNeighborCount > def.MaxSameNeighbors.Value)
            return false;

        return true;
    }

    private static List<float> GetWeightsForBiomes(
        int cellId,
        List<BiomeRulesHelper.BiomeType> biomes,
        BiomeCellResult result,
        Dictionary<int, HashSet<BiomeRulesHelper.BiomeType>> possibilities,
        Dictionary<BiomeRulesHelper.BiomeType, BiomeRulesHelper.BiomeDefinition> biomeDefs)
    {
        List<float> weights = new();

        foreach (BiomeRulesHelper.BiomeType biome in biomes)
            weights.Add(GetPlacementWeight(cellId, biome, result, possibilities, biomeDefs));

        return weights;
    }

    private static float GetPlacementWeight(
        int cellId,
        BiomeRulesHelper.BiomeType biome,
        BiomeCellResult result,
        Dictionary<int, HashSet<BiomeRulesHelper.BiomeType>> possibilities,
        Dictionary<BiomeRulesHelper.BiomeType, BiomeRulesHelper.BiomeDefinition> biomeDefs)
    {
        BiomeRulesHelper.BiomeDefinition def = biomeDefs[biome];
        float weight = def.BaseWeight;
        int sameNeighborCount = 0;

        foreach (int neighborId in result.CellNeighbors[cellId])
        {
            if (possibilities[neighborId].Count != 1)
                continue;

            BiomeRulesHelper.BiomeType neighborBiome = possibilities[neighborId].First();

            if (neighborBiome == biome)
                sameNeighborCount++;

            if (def.NeighborWeightModifiers.TryGetValue(neighborBiome, out float modifier))
                weight *= modifier;
        }

        weight *= 1.0f + (sameNeighborCount * def.WeightPerSameNeighbor);

        return Mathf.Max(weight, 0.0001f);
    }

    private static BiomeRulesHelper.BiomeType ChooseWeightedBiome(
        List<BiomeRulesHelper.BiomeType> validBiomes,
        List<float> weights,
        Random rng)
    {
        float total = 0.0f;

        for (int i = 0; i < weights.Count; i++)
            total += weights[i];

        float roll = (float)rng.NextDouble() * total;
        float cumulative = 0.0f;

        for (int i = 0; i < validBiomes.Count; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
                return validBiomes[i];
        }

        return validBiomes[validBiomes.Count - 1];
    }

    private static void PromoteClusterBiomes(BiomeCellResult result)
    {
        List<BiomeRulesHelper.ClusterPromotionRule> promotionRules =
            BiomeRulesHelper.GetClusterPromotionRules();

        Dictionary<int, BiomeRulesHelper.BiomeType> originalBiomes =
            new Dictionary<int, BiomeRulesHelper.BiomeType>(result.CellBiomes);

        foreach (BiomeRulesHelper.ClusterPromotionRule rule in promotionRules)
        {
            foreach (var pair in originalBiomes)
            {
                int cellId = pair.Key;
                BiomeRulesHelper.BiomeType biome = pair.Value;

                if (biome != rule.BaseBiome)
                    continue;

                if (rule.PromotedBiome == BiomeRulesHelper.BiomeType.DeepOcean)
                {
                    if (IsDeepOceanCandidate(
                        cellId,
                        result.CellNeighbors,
                        originalBiomes,
                        rule.RequiredSameBiomeNeighbors
                    ))
                    {
                        result.CellBiomes[cellId] = rule.PromotedBiome;
                    }
                    continue;
                }

                int matchingNeighbors = CountMatchingNeighbors(
                    cellId,
                    rule.BaseBiome,
                    result.CellNeighbors,
                    originalBiomes
                );

                if (matchingNeighbors >= rule.RequiredSameBiomeNeighbors)
                    result.CellBiomes[cellId] = rule.PromotedBiome;
            }
        }
    }

    private static bool IsDeepOceanCandidate(
    int cellId,
    Dictionary<int, HashSet<int>> neighborGraph,
    Dictionary<int, BiomeRulesHelper.BiomeType> biomeMap,
    int requiredOceanNeighbors)
    {
        int oceanLikeNeighbors = 0;

        foreach (int neighborId in neighborGraph[cellId])
        {
            if (!biomeMap.TryGetValue(neighborId, out BiomeRulesHelper.BiomeType neighborBiome))
                return false;

            if (neighborBiome == BiomeRulesHelper.BiomeType.Ocean)
            {
                oceanLikeNeighbors++;
            }
            else
            {
                return false;
            }
        }

        return oceanLikeNeighbors >= requiredOceanNeighbors;
    }

    private static int CountMatchingNeighbors(
        int cellId,
        BiomeRulesHelper.BiomeType targetBiome,
        Dictionary<int, HashSet<int>> neighborGraph,
        Dictionary<int, BiomeRulesHelper.BiomeType> biomeMap)
    {
        int count = 0;

        foreach (int neighborId in neighborGraph[cellId])
        {
            if (biomeMap.TryGetValue(neighborId, out BiomeRulesHelper.BiomeType neighborBiome) &&
                neighborBiome == targetBiome)
            {
                count++;
            }
        }

        return count;
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

    private static Image CreateBiomeImageWithBorders(BiomeCellResult result)
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
            return sizeB.CompareTo(sizeA);
        });

        int keepCount = Mathf.Max(minimumToKeep, Mathf.CeilToInt(sorted.Count * topFraction));
        keepCount = Mathf.Min(keepCount, sorted.Count);

        return sorted.GetRange(0, keepCount);
    }

    private static int GetCellArea(CellNoiseHelper.CellMapData cellMap, int cellId)
    {
        return cellMap.Cells[cellId].Pixels.Count;
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

    private static void ShuffleList<T>(List<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}