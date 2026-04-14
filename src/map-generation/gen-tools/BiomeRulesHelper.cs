using Godot;
using System.Collections.Generic;

public static class BiomeRulesHelper
{
    public enum BiomeType
    {
        // Base biomes used during WFC
        Desert,
        Forest,
        Grassland,
        Mountains,
        Swamp,
        Lake,
        Wasteland,
        Tundra,
        Ocean,
        Coast,
        Hills,
        Jungle,
        Taiga,
        Snow,
        Badlands,
        Volcanic,
        Marsh,
        Savanna,

        // Cluster / interior variants added in post-process
        DeepOcean,
        DeepDesert,
        GreatMountain,
        AncientWoodland,
        Heartland,
        Mire,
        DeepJungle,
        Glacier
    }

    public class BiomeDefinition
    {
        public BiomeType Type;
        public string DisplayName;
        public Color Color;

        // Hard adjacency
        public HashSet<BiomeType> AllowedNeighbors = new();

        // Soft weighting
        public float BaseWeight = 1.0f;
        public float WeightPerSameNeighbor = 0.0f;
        public Dictionary<BiomeType, float> NeighborWeightModifiers = new();

        // Hard local shape limits
        public int? MaxSameNeighbors = null;

        // Cluster info
        public bool IsClusterVariant = false;
        public BiomeType? ParentBiome = null;
    }

    public class ClusterPromotionRule
    {
        public BiomeType BaseBiome;
        public BiomeType PromotedBiome;
        public int RequiredSameBiomeNeighbors;
    }

    public static Dictionary<BiomeType, BiomeDefinition> CreateBiomeDefinitions()
    {
        Dictionary<BiomeType, BiomeDefinition> biomes = new();

        AddBiome(biomes, BiomeType.Desert, "Desert", new Color(0.90f, 0.78f, 0.40f), 0.80f, 0.35f);
        AddBiome(biomes, BiomeType.Forest, "Forest", new Color(0.18f, 0.55f, 0.22f), 1.30f, 0.45f);
        AddBiome(biomes, BiomeType.Grassland, "Grassland", new Color(0.45f, 0.72f, 0.30f), 1.60f, 0.40f);
        AddBiome(biomes, BiomeType.Mountains, "Mountains", new Color(0.55f, 0.55f, 0.60f), 0.70f, 0.65f);
        AddBiome(biomes, BiomeType.Swamp, "Swamp", new Color(0.22f, 0.36f, 0.26f), 0.55f, 0.35f);
        AddBiome(biomes, BiomeType.Lake, "Lake", new Color(0.20f, 0.50f, 0.85f), 0.40f, 0.30f, maxSameNeighbors: 2);
        AddBiome(biomes, BiomeType.Wasteland, "Wasteland", new Color(0.42f, 0.34f, 0.34f), 0.28f, 0.20f);
        AddBiome(biomes, BiomeType.Tundra, "Tundra", new Color(0.78f, 0.86f, 0.92f), 0.45f, 0.40f);
        AddBiome(biomes, BiomeType.Ocean, "Ocean", new Color(0.08f, 0.24f, 0.62f), 0.4f, 1.30f);
        AddBiome(biomes, BiomeType.Coast, "Coast", new Color(0.82f, 0.84f, 0.62f), 0.55f, 0.15f, maxSameNeighbors: 3);
        AddBiome(biomes, BiomeType.Hills, "Hills", new Color(0.52f, 0.62f, 0.36f), 1.00f, 0.30f);
        AddBiome(biomes, BiomeType.Jungle, "Jungle", new Color(0.10f, 0.45f, 0.18f), 0.45f, 0.50f);
        AddBiome(biomes, BiomeType.Taiga, "Taiga", new Color(0.28f, 0.46f, 0.34f), 0.65f, 0.45f);
        AddBiome(biomes, BiomeType.Snow, "Snow", new Color(0.94f, 0.97f, 1.00f), 0.35f, 0.55f);
        AddBiome(biomes, BiomeType.Badlands, "Badlands", new Color(0.69f, 0.42f, 0.26f), 0.40f, 0.30f);
        AddBiome(biomes, BiomeType.Volcanic, "Volcanic", new Color(0.24f, 0.22f, 0.24f), 0.10f, 0.25f, maxSameNeighbors: 2);
        AddBiome(biomes, BiomeType.Marsh, "Marsh", new Color(0.32f, 0.48f, 0.30f), 0.45f, 0.35f);
        AddBiome(biomes, BiomeType.Savanna, "Savanna", new Color(0.72f, 0.68f, 0.30f), 0.75f, 0.35f);

        AddClusterBiome(biomes, BiomeType.DeepOcean, "Deep Ocean", new Color(0.03f, 0.10f, 0.36f), BiomeType.Ocean);
        AddClusterBiome(biomes, BiomeType.DeepDesert, "Deep Desert", new Color(0.82f, 0.64f, 0.25f), BiomeType.Desert);
        AddClusterBiome(biomes, BiomeType.GreatMountain, "Great Mountain", new Color(0.40f, 0.40f, 0.45f), BiomeType.Mountains);
        AddClusterBiome(biomes, BiomeType.AncientWoodland, "Ancient Woodland", new Color(0.10f, 0.36f, 0.14f), BiomeType.Forest);
        AddClusterBiome(biomes, BiomeType.Heartland, "Heartland", new Color(0.56f, 0.80f, 0.36f), BiomeType.Grassland);
        AddClusterBiome(biomes, BiomeType.Mire, "Mire", new Color(0.16f, 0.27f, 0.18f), BiomeType.Swamp);
        AddClusterBiome(biomes, BiomeType.DeepJungle, "Deep Jungle", new Color(0.06f, 0.30f, 0.10f), BiomeType.Jungle);
        AddClusterBiome(biomes, BiomeType.Glacier, "Glacier", new Color(0.82f, 0.92f, 0.98f), BiomeType.Snow);

        // Adjacency
        LinkBiomes(biomes, BiomeType.Ocean, BiomeType.Ocean, BiomeType.DeepOcean, BiomeType.Coast, BiomeType.Marsh, BiomeType.Swamp);
        LinkBiomes(biomes, BiomeType.Coast, BiomeType.Lake, BiomeType.Swamp, BiomeType.Marsh, BiomeType.Grassland, BiomeType.Forest, BiomeType.Jungle, BiomeType.Desert, BiomeType.Savanna);

        LinkBiomes(biomes, BiomeType.Lake, BiomeType.Lake, BiomeType.Grassland, BiomeType.Forest, BiomeType.Swamp, BiomeType.Marsh, BiomeType.Hills, BiomeType.Mountains, BiomeType.Tundra, BiomeType.Snow, BiomeType.Jungle, BiomeType.Coast);
        LinkBiomes(biomes, BiomeType.Swamp, BiomeType.Swamp, BiomeType.Marsh, BiomeType.Forest, BiomeType.Grassland, BiomeType.Lake, BiomeType.Jungle, BiomeType.Coast);
        LinkBiomes(biomes, BiomeType.Marsh, BiomeType.Marsh, BiomeType.Swamp, BiomeType.Grassland, BiomeType.Lake, BiomeType.Forest, BiomeType.Coast, BiomeType.Jungle, BiomeType.Tundra);

        LinkBiomes(biomes, BiomeType.Grassland, BiomeType.Grassland, BiomeType.Forest, BiomeType.Hills, BiomeType.Desert, BiomeType.Savanna, BiomeType.Mountains, BiomeType.Badlands, BiomeType.Swamp, BiomeType.Marsh, BiomeType.Taiga, BiomeType.Coast, BiomeType.Lake);
        LinkBiomes(biomes, BiomeType.Forest, BiomeType.Forest, BiomeType.Hills, BiomeType.Mountains, BiomeType.Grassland, BiomeType.Swamp, BiomeType.Marsh, BiomeType.Lake, BiomeType.Taiga, BiomeType.Jungle, BiomeType.Coast, BiomeType.Savanna);
        LinkBiomes(biomes, BiomeType.Hills, BiomeType.Hills, BiomeType.Mountains, BiomeType.Grassland, BiomeType.Forest, BiomeType.Lake, BiomeType.Badlands, BiomeType.Taiga, BiomeType.Savanna, BiomeType.Jungle);

        LinkBiomes(biomes, BiomeType.Desert, BiomeType.Desert, BiomeType.Savanna, BiomeType.Badlands, BiomeType.Wasteland, BiomeType.Volcanic, BiomeType.Grassland, BiomeType.Coast);
        LinkBiomes(biomes, BiomeType.Savanna, BiomeType.Savanna, BiomeType.Grassland, BiomeType.Forest, BiomeType.Desert, BiomeType.Jungle, BiomeType.Hills, BiomeType.Badlands, BiomeType.Coast);
        LinkBiomes(biomes, BiomeType.Badlands, BiomeType.Badlands, BiomeType.Desert, BiomeType.Wasteland, BiomeType.Hills, BiomeType.Mountains, BiomeType.Volcanic, BiomeType.Grassland, BiomeType.Savanna);
        LinkBiomes(biomes, BiomeType.Wasteland, BiomeType.Wasteland, BiomeType.Badlands, BiomeType.Desert, BiomeType.Mountains, BiomeType.Volcanic);

        LinkBiomes(biomes, BiomeType.Tundra, BiomeType.Tundra, BiomeType.Snow, BiomeType.Taiga, BiomeType.Mountains, BiomeType.Lake, BiomeType.Marsh);
        LinkBiomes(biomes, BiomeType.Snow, BiomeType.Snow, BiomeType.Tundra, BiomeType.Mountains, BiomeType.Taiga, BiomeType.Lake);
        LinkBiomes(biomes, BiomeType.Taiga, BiomeType.Taiga, BiomeType.Forest, BiomeType.Tundra, BiomeType.Snow, BiomeType.Hills, BiomeType.Mountains, BiomeType.Grassland);

        LinkBiomes(biomes, BiomeType.Mountains, BiomeType.Mountains, BiomeType.Hills, BiomeType.Forest, BiomeType.Grassland, BiomeType.Taiga, BiomeType.Tundra, BiomeType.Snow, BiomeType.Badlands, BiomeType.Wasteland, BiomeType.Volcanic, BiomeType.Lake);
        LinkBiomes(biomes, BiomeType.Volcanic, BiomeType.Volcanic, BiomeType.Mountains, BiomeType.Wasteland, BiomeType.Badlands, BiomeType.Desert);

        LinkBiomes(biomes, BiomeType.Jungle, BiomeType.Jungle, BiomeType.Forest, BiomeType.Swamp, BiomeType.Marsh, BiomeType.Lake, BiomeType.Coast, BiomeType.Savanna, BiomeType.Hills);

        // Soft weight nudges
        AddNeighborModifier(biomes, BiomeType.Mountains, BiomeType.Hills, 1.75f);
        AddNeighborModifier(biomes, BiomeType.Mountains, BiomeType.Mountains, 1.50f);
        AddNeighborModifier(biomes, BiomeType.Hills, BiomeType.Mountains, 1.60f);
        AddNeighborModifier(biomes, BiomeType.Hills, BiomeType.Grassland, 1.25f);

        AddNeighborModifier(biomes, BiomeType.Forest, BiomeType.Lake, 1.35f);
        AddNeighborModifier(biomes, BiomeType.Forest, BiomeType.Marsh, 1.20f);
        AddNeighborModifier(biomes, BiomeType.Forest, BiomeType.Grassland, 1.20f);

        AddNeighborModifier(biomes, BiomeType.Jungle, BiomeType.Coast, 1.45f);
        AddNeighborModifier(biomes, BiomeType.Jungle, BiomeType.Swamp, 1.35f);
        AddNeighborModifier(biomes, BiomeType.Jungle, BiomeType.Lake, 1.30f);

        AddNeighborModifier(biomes, BiomeType.Swamp, BiomeType.Lake, 1.80f);
        AddNeighborModifier(biomes, BiomeType.Swamp, BiomeType.Marsh, 1.35f);
        AddNeighborModifier(biomes, BiomeType.Marsh, BiomeType.Swamp, 1.45f);
        AddNeighborModifier(biomes, BiomeType.Marsh, BiomeType.Coast, 1.20f);

        AddNeighborModifier(biomes, BiomeType.Desert, BiomeType.Savanna, 1.35f);
        AddNeighborModifier(biomes, BiomeType.Desert, BiomeType.Badlands, 1.25f);
        AddNeighborModifier(biomes, BiomeType.Savanna, BiomeType.Desert, 1.40f);
        AddNeighborModifier(biomes, BiomeType.Badlands, BiomeType.Desert, 1.25f);

        AddNeighborModifier(biomes, BiomeType.Ocean, BiomeType.Ocean, 1.50f);
        AddNeighborModifier(biomes, BiomeType.Coast, BiomeType.Ocean, 1.50f);
        AddNeighborModifier(biomes, BiomeType.Coast, BiomeType.Grassland, 1.20f);

        AddNeighborModifier(biomes, BiomeType.Taiga, BiomeType.Snow, 1.40f);
        AddNeighborModifier(biomes, BiomeType.Taiga, BiomeType.Tundra, 1.35f);
        AddNeighborModifier(biomes, BiomeType.Snow, BiomeType.Tundra, 1.60f);
        AddNeighborModifier(biomes, BiomeType.Tundra, BiomeType.Snow, 1.40f);

        AddNeighborModifier(biomes, BiomeType.Volcanic, BiomeType.Mountains, 2.30f);
        AddNeighborModifier(biomes, BiomeType.Volcanic, BiomeType.Badlands, 1.30f);

        InheritClusterNeighbors(biomes);

        SetExactNeighbors(
            biomes,
            BiomeType.DeepOcean,
            BiomeType.DeepOcean,
            BiomeType.Ocean
        );

        return biomes;
    }

    private static void SetExactNeighbors(
        Dictionary<BiomeType, BiomeDefinition> biomes,
        BiomeType biome,
        params BiomeType[] neighbors)
    {
        biomes[biome].AllowedNeighbors.Clear();

        foreach (BiomeType neighbor in neighbors)
            biomes[biome].AllowedNeighbors.Add(neighbor);
    }

    public static List<BiomeType> GetAllBiomeTypes()
    {
        return new List<BiomeType>
        {
            BiomeType.Desert,
            BiomeType.Forest,
            BiomeType.Grassland,
            BiomeType.Mountains,
            BiomeType.Swamp,
            BiomeType.Lake,
            BiomeType.Wasteland,
            BiomeType.Tundra,
            BiomeType.Ocean,
            BiomeType.Coast,
            BiomeType.Hills,
            BiomeType.Jungle,
            BiomeType.Taiga,
            BiomeType.Snow,
            BiomeType.Badlands,
            BiomeType.Volcanic,
            BiomeType.Marsh,
            BiomeType.Savanna
        };
    }

    public static List<ClusterPromotionRule> GetClusterPromotionRules()
    {
        return new List<ClusterPromotionRule>
        {
            new ClusterPromotionRule { BaseBiome = BiomeType.Ocean,     PromotedBiome = BiomeType.DeepOcean,       RequiredSameBiomeNeighbors = 4 },
            new ClusterPromotionRule { BaseBiome = BiomeType.Desert,    PromotedBiome = BiomeType.DeepDesert,      RequiredSameBiomeNeighbors = 3 },
            new ClusterPromotionRule { BaseBiome = BiomeType.Mountains, PromotedBiome = BiomeType.GreatMountain,   RequiredSameBiomeNeighbors = 3 },
            new ClusterPromotionRule { BaseBiome = BiomeType.Forest,    PromotedBiome = BiomeType.AncientWoodland, RequiredSameBiomeNeighbors = 3 },
            new ClusterPromotionRule { BaseBiome = BiomeType.Grassland, PromotedBiome = BiomeType.Heartland,       RequiredSameBiomeNeighbors = 3 },
            new ClusterPromotionRule { BaseBiome = BiomeType.Swamp,     PromotedBiome = BiomeType.Mire,            RequiredSameBiomeNeighbors = 3 },
            new ClusterPromotionRule { BaseBiome = BiomeType.Jungle,    PromotedBiome = BiomeType.DeepJungle,      RequiredSameBiomeNeighbors = 3 },
            new ClusterPromotionRule { BaseBiome = BiomeType.Snow,      PromotedBiome = BiomeType.Glacier,         RequiredSameBiomeNeighbors = 3 }
        };
    }

    private static void AddBiome(
        Dictionary<BiomeType, BiomeDefinition> biomes,
        BiomeType type,
        string displayName,
        Color color,
        float baseWeight,
        float weightPerSameNeighbor,
        int? maxSameNeighbors = null)
    {
        biomes[type] = new BiomeDefinition
        {
            Type = type,
            DisplayName = displayName,
            Color = color,
            BaseWeight = baseWeight,
            WeightPerSameNeighbor = weightPerSameNeighbor,
            MaxSameNeighbors = maxSameNeighbors
        };
    }

    private static void AddClusterBiome(
        Dictionary<BiomeType, BiomeDefinition> biomes,
        BiomeType type,
        string displayName,
        Color color,
        BiomeType parentBiome)
    {
        biomes[type] = new BiomeDefinition
        {
            Type = type,
            DisplayName = displayName,
            Color = color,
            IsClusterVariant = true,
            ParentBiome = parentBiome
        };
    }

    private static void LinkBiomes(
        Dictionary<BiomeType, BiomeDefinition> biomes,
        BiomeType biome,
        params BiomeType[] neighbors)
    {
        foreach (BiomeType neighbor in neighbors)
        {
            biomes[biome].AllowedNeighbors.Add(neighbor);
            biomes[neighbor].AllowedNeighbors.Add(biome);
        }
    }

    private static void AddNeighborModifier(
        Dictionary<BiomeType, BiomeDefinition> biomes,
        BiomeType biome,
        BiomeType neighbor,
        float multiplier)
    {
        biomes[biome].NeighborWeightModifiers[neighbor] = multiplier;
    }

    private static void InheritClusterNeighbors(Dictionary<BiomeType, BiomeDefinition> biomes)
    {
        foreach (BiomeDefinition biome in biomes.Values)
        {
            if (!biome.IsClusterVariant || biome.ParentBiome == null)
                continue;

            BiomeType parent = biome.ParentBiome.Value;

            biome.AllowedNeighbors.Add(biome.Type);
            biome.AllowedNeighbors.Add(parent);
            biomes[parent].AllowedNeighbors.Add(biome.Type);

            foreach (BiomeType neighbor in biomes[parent].AllowedNeighbors)
            {
                biome.AllowedNeighbors.Add(neighbor);
                biomes[neighbor].AllowedNeighbors.Add(biome.Type);
            }
        }
    }
}