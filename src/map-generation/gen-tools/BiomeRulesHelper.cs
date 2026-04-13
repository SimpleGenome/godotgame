using Godot;
using System.Collections.Generic;

public static class BiomeRulesHelper
{
    public enum BiomeType
    {
        SunfireDesert,
        EmeraldForest,
        WhisperingGrassland,
        Stormpeaks,
        MoonlitSwamp,
        CrystalLake,
        AshenWastes,
        FrostveilTundra
    }

    public class BiomeDefinition
    {
        public BiomeType Type;
        public string DisplayName;
        public Color Color;

        // Which biomes are allowed next to this one
        public HashSet<BiomeType> AllowedNeighbors = new();
    }

    public static Dictionary<BiomeType, BiomeDefinition> CreateBiomeDefinitions()
    {
        Dictionary<BiomeType, BiomeDefinition> biomes = new();

        biomes[BiomeType.SunfireDesert] = new BiomeDefinition
        {
            Type = BiomeType.SunfireDesert,
            DisplayName = "Sunfire Desert",
            Color = new Color(0.90f, 0.78f, 0.40f)
        };

        biomes[BiomeType.EmeraldForest] = new BiomeDefinition
        {
            Type = BiomeType.EmeraldForest,
            DisplayName = "Emerald Forest",
            Color = new Color(0.18f, 0.55f, 0.22f)
        };

        biomes[BiomeType.WhisperingGrassland] = new BiomeDefinition
        {
            Type = BiomeType.WhisperingGrassland,
            DisplayName = "Whispering Grassland",
            Color = new Color(0.45f, 0.72f, 0.30f)
        };

        biomes[BiomeType.Stormpeaks] = new BiomeDefinition
        {
            Type = BiomeType.Stormpeaks,
            DisplayName = "Stormpeaks",
            Color = new Color(0.55f, 0.55f, 0.60f)
        };

        biomes[BiomeType.MoonlitSwamp] = new BiomeDefinition
        {
            Type = BiomeType.MoonlitSwamp,
            DisplayName = "Moonlit Swamp",
            Color = new Color(0.22f, 0.36f, 0.26f)
        };

        biomes[BiomeType.CrystalLake] = new BiomeDefinition
        {
            Type = BiomeType.CrystalLake,
            DisplayName = "Crystal Lake",
            Color = new Color(0.20f, 0.50f, 0.85f)
        };

        biomes[BiomeType.AshenWastes] = new BiomeDefinition
        {
            Type = BiomeType.AshenWastes,
            DisplayName = "Ashen Wastes",
            Color = new Color(0.42f, 0.34f, 0.34f)
        };

        biomes[BiomeType.FrostveilTundra] = new BiomeDefinition
        {
            Type = BiomeType.FrostveilTundra,
            DisplayName = "Frostveil Tundra",
            Color = new Color(0.78f, 0.86f, 0.92f)
        };

        // Adjacency rules
        SetNeighbors(biomes, BiomeType.SunfireDesert,
            BiomeType.SunfireDesert,
            BiomeType.WhisperingGrassland,
            BiomeType.AshenWastes
        );

        SetNeighbors(biomes, BiomeType.EmeraldForest,
            BiomeType.EmeraldForest,
            BiomeType.WhisperingGrassland,
            BiomeType.MoonlitSwamp,
            BiomeType.CrystalLake,
            BiomeType.Stormpeaks
        );

        SetNeighbors(biomes, BiomeType.WhisperingGrassland,
            BiomeType.WhisperingGrassland,
            BiomeType.EmeraldForest,
            BiomeType.SunfireDesert,
            BiomeType.CrystalLake,
            BiomeType.Stormpeaks,
            BiomeType.MoonlitSwamp
        );

        SetNeighbors(biomes, BiomeType.Stormpeaks,
            BiomeType.Stormpeaks,
            BiomeType.EmeraldForest,
            BiomeType.WhisperingGrassland,
            BiomeType.FrostveilTundra,
            BiomeType.AshenWastes
        );

        SetNeighbors(biomes, BiomeType.MoonlitSwamp,
            BiomeType.MoonlitSwamp,
            BiomeType.EmeraldForest,
            BiomeType.WhisperingGrassland,
            BiomeType.CrystalLake
        );

        SetNeighbors(biomes, BiomeType.CrystalLake,
            BiomeType.CrystalLake,
            BiomeType.WhisperingGrassland,
            BiomeType.EmeraldForest,
            BiomeType.MoonlitSwamp,
            BiomeType.FrostveilTundra
        );

        SetNeighbors(biomes, BiomeType.AshenWastes,
            BiomeType.AshenWastes,
            BiomeType.SunfireDesert,
            BiomeType.Stormpeaks
        );

        SetNeighbors(biomes, BiomeType.FrostveilTundra,
            BiomeType.FrostveilTundra,
            BiomeType.Stormpeaks,
            BiomeType.CrystalLake
        );

        return biomes;
    }

    public static List<BiomeType> GetAllBiomeTypes()
    {
        return new List<BiomeType>
        {
            BiomeType.SunfireDesert,
            BiomeType.EmeraldForest,
            BiomeType.WhisperingGrassland,
            BiomeType.Stormpeaks,
            BiomeType.MoonlitSwamp,
            BiomeType.CrystalLake,
            BiomeType.AshenWastes,
            BiomeType.FrostveilTundra
        };
    }

    private static void SetNeighbors(
        Dictionary<BiomeType, BiomeDefinition> biomes,
        BiomeType biome,
        params BiomeType[] allowed)
    {
        foreach (BiomeType neighbor in allowed)
            biomes[biome].AllowedNeighbors.Add(neighbor);
    }
}