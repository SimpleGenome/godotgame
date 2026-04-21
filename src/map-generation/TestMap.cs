using System;
using System.Collections.Generic;
using Godot;

public static class TestMap
{
    private class RangeSpine
    {
        public List<Vec2> Points;
        public float Strength;
        public float RadiusScale;
        public bool IsMain;
    }

    public static (float[,], Texture2D) GenerateMountainOverlay(
        int mapSize,
        int seed = 0,
        float overlayStrength = 1.0f)
    {
        if (mapSize <= 0)
            throw new ArgumentException("Width and height must be greater than zero.");

        Random rng = new Random(seed);
        float[,] overlay = new float[mapSize, mapSize];

        int rangeCount = rng.Next(1, 4); // 1..3 ranges

        for (int i = 0; i < rangeCount; i++)
        {
            float[,] range = GenerateSingleRangeOverlay(mapSize, mapSize, rng);
            MergeMax(overlay, range);
        }

        Normalize01(overlay);

        for (int y = 0; y < mapSize; y++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                overlay[x, y] = Saturate(MathF.Pow(overlay[x, y], 1.05f) * overlayStrength);
            }
        }

        Texture2D overlayTexture = MapGenTools.MapToGreyscaleTexture(overlay, mapSize);

        return (overlay, overlayTexture);
    }

    public static void AddOverlayToHeightMap(float[,] baseHeightMap, float[,] overlay, float strength = 0.35f)
    {
        int width = baseHeightMap.GetLength(0);
        int height = baseHeightMap.GetLength(1);

        if (overlay.GetLength(0) != width || overlay.GetLength(1) != height)
            throw new ArgumentException("Overlay dimensions must match base height map.");

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float baseH = baseHeightMap[x, y];
                float mountain = overlay[x, y];

                float add = mountain * strength * (1.0f - baseH);
                baseHeightMap[x, y] = Saturate(baseH + add);
            }
        }
    }

    private static float[,] GenerateSingleRangeOverlay(int width, int height, Random rng)
    {
        float minDim = Math.Min(width, height);

        List<RangeSpine> spinesFull = CreateRangeSpines(width, height, rng);

        int lowW = Math.Max(160, width / 2);
        int lowH = Math.Max(160, height / 2);

        List<RangeSpine> spinesLow = ScaleSpines(spinesFull, width, height, lowW, lowH);

        float baseCorridorRadiusLow = RandomRange(rng, Math.Min(lowW, lowH) * 0.06f, Math.Min(lowW, lowH) * 0.11f);

        Corridor corridor = BuildCorridor(spinesLow, baseCorridorRadiusLow);

        DlaGraph graph = RunSplineBoundDla(
            corridor,
            lowW,
            lowH,
            rng,
            targetFillRatio: 0.22f,
            maxWalkSteps: Math.Max(lowW, lowH) * 28,
            spawnAttemptsPerParticle: 192);

        // Low-res connected ridge network from DLA.
        float[,] ridgeLow = BuildWeightedRidgeMap(graph);

        // Slight dilation helps remove dotted appearance.
        ridgeLow = MaxFilter3x3(ridgeLow);

        // Upscale the DLA ridge network.
        float[,] ridgeDetailFull = UpscaleWithSmallBlur(ridgeLow, width, height);

        // Full-res main spines rendered as thin ridge lines, not glow.
        float[,] spineFull = BuildSpineSkeletonMap(width, height, spinesFull);

        // Merge both into one connected ridge skeleton.
        float[,] ridgeSkeleton = MaxCombine(ridgeDetailFull, spineFull);
        Normalize01(ridgeSkeleton);

        // Turn the skeleton into a mountain mass.
        float[,] result = BuildMountainFromSkeleton(ridgeSkeleton);

        Normalize01(result);
        return result;
    }

    private static float[,] BuildSpineSkeletonMap(int width, int height, List<RangeSpine> spines)
    {
        float[,] map = new float[width, height];

        for (int s = 0; s < spines.Count; s++)
        {
            List<Vec2> pts = spines[s].Points;
            if (pts == null || pts.Count < 2)
                continue;

            float radius = spines[s].IsMain ? 1.35f : 1.0f;
            float value = spines[s].IsMain ? 1.0f : 0.88f;

            for (int i = 0; i < pts.Count - 1; i++)
            {
                DrawThickValueLine(
                    map,
                    (int)MathF.Round(pts[i].X),
                    (int)MathF.Round(pts[i].Y),
                    (int)MathF.Round(pts[i + 1].X),
                    (int)MathF.Round(pts[i + 1].Y),
                    radius,
                    value);
            }
        }

        return map;
    }

    private static float[,] CloneMap(float[,] src)
    {
        int width = src.GetLength(0);
        int height = src.GetLength(1);
        float[,] dst = new float[width, height];

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                dst[x, y] = src[x, y];

        return dst;
    }

    private static float[,] BlurRepeated(float[,] src, int count)
    {
        float[,] current = src;
        for (int i = 0; i < count; i++)
            current = Blur3x3(current);
        return current;
    }

    private static float[,] MaxCombine(float[,] a, float[,] b)
    {
        int width = a.GetLength(0);
        int height = a.GetLength(1);
        float[,] result = new float[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                result[x, y] = Math.Max(a[x, y], b[x, y]);
            }
        }

        return result;
    }

    private static float[,] MaxFilter3x3(float[,] src)
    {
        int width = src.GetLength(0);
        int height = src.GetLength(1);
        float[,] dst = new float[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float maxV = 0f;

                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int sx = Clamp(x + ox, 0, width - 1);
                        int sy = Clamp(y + oy, 0, height - 1);
                        if (src[sx, sy] > maxV)
                            maxV = src[sx, sy];
                    }
                }

                dst[x, y] = maxV;
            }
        }

        return dst;
    }

    private static void DrawThickValueLine(float[,] map, int x0, int y0, int x1, int y1, float radius, float value)
    {
        RasterizeLine(new Vec2(x0, y0), new Vec2(x1, y1), map.GetLength(0), map.GetLength(1), (x, y) =>
        {
            StampDiskMax(map, x, y, radius, value);
        });
    }

    private static void StampDiskMax(float[,] map, int cx, int cy, float radius, float value)
    {
        int width = map.GetLength(0);
        int height = map.GetLength(1);

        int r = Math.Max(1, (int)MathF.Ceiling(radius));
        float rSq = radius * radius;

        for (int y = cy - r; y <= cy + r; y++)
        {
            if (y < 0 || y >= height)
                continue;

            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || x >= width)
                    continue;

                float dx = x - cx;
                float dy = y - cy;
                if (dx * dx + dy * dy > rSq)
                    continue;

                if (value > map[x, y])
                    map[x, y] = value;
            }
        }
    }

    private static float[,] BuildMountainFromSkeleton(float[,] skeleton)
    {
        int width = skeleton.GetLength(0);
        int height = skeleton.GetLength(1);

        float[,] crest = CloneMap(skeleton);
        float[,] near = BlurRepeated(CloneMap(skeleton), 2);
        float[,] mid = BlurRepeated(CloneMap(skeleton), 5);
        float[,] far = BlurRepeated(CloneMap(skeleton), 9);

        float[,] result = new float[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float c = crest[x, y];
                float n = near[x, y];
                float m = mid[x, y];
                float f = far[x, y];

                // Important: use the SAME skeleton at multiple scales.
                // Do not normalize near/mid/far independently.
                float v = MathF.Max(
                    c * 1.00f,
                    MathF.Max(
                        n * 0.78f,
                        MathF.Max(
                            m * 0.46f,
                            f * 0.20f)));

                result[x, y] = Saturate(v);
            }
        }

        return result;
    }

    private static List<RangeSpine> ScaleSpines(List<RangeSpine> spines, int srcW, int srcH, int dstW, int dstH)
    {
        List<RangeSpine> result = new List<RangeSpine>();

        float sx = srcW > 1 ? (dstW - 1) / (float)(srcW - 1) : 0f;
        float sy = srcH > 1 ? (dstH - 1) / (float)(srcH - 1) : 0f;

        for (int i = 0; i < spines.Count; i++)
        {
            List<Vec2> pts = new List<Vec2>();

            for (int j = 0; j < spines[i].Points.Count; j++)
            {
                float x = spines[i].Points[j].X * sx;
                float y = spines[i].Points[j].Y * sy;

                x = x < 0 ? 0 : (x > dstW - 1 ? dstW - 1 : x);
                y = y < 0 ? 0 : (y > dstH - 1 ? dstH - 1 : y);

                pts.Add(new Vec2(x, y));
            }

            result.Add(new RangeSpine
            {
                Points = pts,
                Strength = spines[i].Strength,
                RadiusScale = spines[i].RadiusScale,
                IsMain = spines[i].IsMain
            });
        }

        return result;
    }

    // ---------------------------------------------------------------------
    // Spine generation
    // ---------------------------------------------------------------------

    private static List<RangeSpine> CreateRangeSpines(int width, int height, Random rng)
    {
        List<RangeSpine> result = new List<RangeSpine>();

        List<Vec2> main = CreateAngularSpine(width, height, rng);
        result.Add(new RangeSpine
        {
            Points = main,
            Strength = 1.0f,
            RadiusScale = 1.0f,
            IsMain = true
        });

        // 45% chance of one split
        if (rng.NextDouble() < 0.45)
        {
            List<Vec2> split = CreateSplitSpine(main, width, height, rng);
            if (split != null && split.Count >= 4)
            {
                result.Add(new RangeSpine
                {
                    Points = split,
                    Strength = 0.85f,
                    RadiusScale = 0.82f,
                    IsMain = false
                });
            }
        }

        // Small chance of a second split
        if (rng.NextDouble() < 0.18)
        {
            List<Vec2> split2 = CreateSplitSpine(main, width, height, rng);
            if (split2 != null && split2.Count >= 4)
            {
                result.Add(new RangeSpine
                {
                    Points = split2,
                    Strength = 0.72f,
                    RadiusScale = 0.72f,
                    IsMain = false
                });
            }
        }

        return result;
    }

    private static List<Vec2> CreateAngularSpine(int width, int height, Random rng)
    {
        float minDim = Math.Min(width, height);

        Vec2 start;
        Vec2 end;
        int attempts = 0;

        do
        {
            start = RandomEndpoint(width, height, rng);
            end = RandomEndpoint(width, height, rng);
            attempts++;
        }
        while (Distance(start, end) < minDim * 0.32f && attempts < 20);

        float totalLength = Distance(start, end);
        int segmentCount = rng.Next(8, 16);
        float baseStep = totalLength / segmentCount;

        List<Vec2> points = new List<Vec2>();
        points.Add(start);

        Vec2 p = start;
        Vec2 dir = Normalize(end - start);

        for (int i = 1; i < segmentCount; i++)
        {
            float t = i / (float)segmentCount;

            Vec2 toEnd = Normalize(end - p);
            Vec2 side = Perp(dir);

            // Larger directional changes near the middle, less near the end.
            float bendStrength = Lerp(0.55f, 0.18f, t);
            float sideAmount = RandomRange(rng, -bendStrength, bendStrength);

            // Occasional stronger kink.
            if (rng.NextDouble() < 0.22)
                sideAmount *= 1.8f;

            Vec2 candidateDir = Normalize(
                dir * 0.45f +
                toEnd * 0.90f +
                side * sideAmount);

            float step = baseStep * RandomRange(rng, 0.82f, 1.18f);

            p += candidateDir * step;
            p = ClampToBounds(p, width, height);

            points.Add(p);
            dir = candidateDir;
        }

        points.Add(end);

        // One light smoothing pass only.
        points = ChaikinSubdivide(points, 1);

        // Resample for even spacing so stamping and DLA seeding behave nicely.
        return ResamplePolyline(points, Math.Max(3f, minDim * 0.012f));
    }

    private static List<Vec2> CreateSplitSpine(List<Vec2> parent, int width, int height, Random rng)
    {
        if (parent == null || parent.Count < 8)
            return null;

        float parentLength = PolylineLength(parent);
        if (parentLength <= 0.001f)
            return null;

        int splitIndex = rng.Next(parent.Count / 4, (parent.Count * 3) / 4);
        Vec2 origin = parent[splitIndex];

        Vec2 tangent = GetTangent(parent, splitIndex);
        Vec2 normal = Perp(tangent);
        if (rng.NextDouble() < 0.5)
            normal = normal * -1f;

        float targetLength = parentLength * RandomRange(rng, 0.25f, 0.55f);
        int segmentCount = rng.Next(5, 10);
        float baseStep = targetLength / segmentCount;

        List<Vec2> points = new List<Vec2>();
        points.Add(origin);

        Vec2 p = origin;
        Vec2 dir = Normalize(tangent * RandomRange(rng, 0.15f, 0.45f) + normal * RandomRange(rng, 0.85f, 1.15f));

        for (int i = 0; i < segmentCount; i++)
        {
            float t = i / (float)Math.Max(1, segmentCount - 1);

            Vec2 side = Perp(dir);
            float bend = RandomRange(rng, -0.45f, 0.45f);

            // Branches tend to fan away from the parent but still wander.
            Vec2 newDir = Normalize(
                dir * 0.75f +
                normal * 0.35f +
                side * bend * 0.35f);

            float step = baseStep * Lerp(1.0f, 0.72f, t) * RandomRange(rng, 0.9f, 1.15f);

            p += newDir * step;

            if (p.X < 0 || p.Y < 0 || p.X >= width || p.Y >= height)
                break;

            points.Add(p);
            dir = newDir;
        }

        if (points.Count < 4)
            return null;

        points = ChaikinSubdivide(points, 1);
        return ResamplePolyline(points, Math.Max(3f, Math.Min(width, height) * 0.010f));
    }

    private static Vec2 ClampToBounds(Vec2 p, int width, int height)
    {
        float x = p.X < 0 ? 0 : (p.X > width - 1 ? width - 1 : p.X);
        float y = p.Y < 0 ? 0 : (p.Y > height - 1 ? height - 1 : p.Y);
        return new Vec2(x, y);
    }

    private static List<Vec2> ChaikinSubdivide(List<Vec2> input, int passes)
    {
        List<Vec2> current = input;

        for (int pass = 0; pass < passes; pass++)
        {
            if (current.Count < 2)
                return current;

            List<Vec2> next = new List<Vec2>();
            next.Add(current[0]);

            for (int i = 0; i < current.Count - 1; i++)
            {
                Vec2 a = current[i];
                Vec2 b = current[i + 1];

                Vec2 q = new Vec2(
                    a.X * 0.75f + b.X * 0.25f,
                    a.Y * 0.75f + b.Y * 0.25f);

                Vec2 r = new Vec2(
                    a.X * 0.25f + b.X * 0.75f,
                    a.Y * 0.25f + b.Y * 0.75f);

                next.Add(q);
                next.Add(r);
            }

            next.Add(current[current.Count - 1]);
            current = next;
        }

        return current;
    }

    private static float PolylineLength(List<Vec2> points)
    {
        float len = 0f;
        for (int i = 1; i < points.Count; i++)
            len += Distance(points[i - 1], points[i]);
        return len;
    }

    private static List<Vec2> ResamplePolyline(List<Vec2> points, float spacing)
    {
        List<Vec2> result = new List<Vec2>();
        if (points == null || points.Count == 0)
            return result;

        result.Add(points[0]);

        float accumulated = 0f;
        Vec2 prev = points[0];

        for (int i = 1; i < points.Count; i++)
        {
            Vec2 curr = points[i];
            float segLen = Distance(prev, curr);

            if (segLen <= 0.0001f)
            {
                prev = curr;
                continue;
            }

            Vec2 dir = Normalize(curr - prev);

            while (accumulated + segLen >= spacing)
            {
                float remain = spacing - accumulated;
                prev += dir * remain;
                result.Add(prev);
                segLen -= remain;
                accumulated = 0f;
            }

            accumulated += segLen;
            prev = curr;
        }

        if (result.Count == 0 || Distance(result[result.Count - 1], points[points.Count - 1]) > 1f)
            result.Add(points[points.Count - 1]);

        return result;
    }

    private static Vec2 RandomEndpoint(int width, int height, Random rng)
    {
        if (rng.NextDouble() < 0.55)
        {
            int edge = rng.Next(4);
            switch (edge)
            {
                case 0: return new Vec2(RandomRange(rng, 0, width - 1), 0);
                case 1: return new Vec2(width - 1, RandomRange(rng, 0, height - 1));
                case 2: return new Vec2(RandomRange(rng, 0, width - 1), height - 1);
                default: return new Vec2(0, RandomRange(rng, 0, height - 1));
            }
        }

        return new Vec2(
            RandomRange(rng, width * 0.1f, width * 0.9f),
            RandomRange(rng, height * 0.1f, height * 0.9f));
    }

    // ---------------------------------------------------------------------
    // Corridor
    // ---------------------------------------------------------------------

    private static Corridor BuildCorridor(List<RangeSpine> spines, float baseRadius)
    {
        List<Vec2> samples = new List<Vec2>();
        List<float> radii = new List<float>();

        for (int s = 0; s < spines.Count; s++)
        {
            List<Vec2> spine = spines[s].Points;
            int count = spine.Count;

            for (int i = 0; i < count; i++)
            {
                float t = count <= 1 ? 0f : i / (float)(count - 1);

                float taper = 0.42f + 0.58f * MathF.Pow(MathF.Sin(t * MathF.PI), 0.8f);
                float r = baseRadius * taper * spines[s].RadiusScale;

                samples.Add(spine[i]);
                radii.Add(r);
            }
        }

        return new Corridor
        {
            Spine = samples,
            Radii = radii.ToArray()
        };
    }

    private static float[,] BuildSpineSetMap(int width, int height, List<RangeSpine> spines, float baseRadius, bool normalizeWithMaxStamp)
    {
        float[,] map = new float[width, height];

        for (int s = 0; s < spines.Count; s++)
        {
            List<Vec2> spine = spines[s].Points;
            int count = spine.Count;

            for (int i = 0; i < count; i++)
            {
                float t = count <= 1 ? 0f : i / (float)(count - 1);
                float taper = 0.42f + 0.58f * MathF.Pow(MathF.Sin(t * MathF.PI), 0.8f);

                float radius = baseRadius * taper * spines[s].RadiusScale;
                float amp = spines[s].Strength;

                if (normalizeWithMaxStamp)
                    StampGaussianMax(map, spine[i], radius, amp);
                else
                    StampGaussianAdd(map, spine[i], radius, amp);
            }
        }

        map = Blur3x3(map);
        if (!normalizeWithMaxStamp)
            map = Blur3x3(map);

        Normalize01(map);
        return map;
    }

    private static void StampGaussianAdd(float[,] map, Vec2 center, float radius, float amplitude)
    {
        int width = map.GetLength(0);
        int height = map.GetLength(1);

        float sigma = radius;
        float twoSigmaSq = 2f * sigma * sigma;
        float reach = radius * 3f;

        int minX = Math.Max(0, (int)MathF.Floor(center.X - reach));
        int maxX = Math.Min(width - 1, (int)MathF.Ceiling(center.X + reach));
        int minY = Math.Max(0, (int)MathF.Floor(center.Y - reach));
        int maxY = Math.Min(height - 1, (int)MathF.Ceiling(center.Y + reach));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x - center.X;
                float dy = y - center.Y;
                float d2 = dx * dx + dy * dy;

                float g = MathF.Exp(-d2 / twoSigmaSq) * amplitude;
                map[x, y] += g;
            }
        }
    }

    private static bool TryGetNearestCorridorInfo(Corridor corridor, int x, int y, out int bestIndex, out float bestDistSq)
    {
        bestIndex = -1;
        bestDistSq = float.MaxValue;

        for (int i = 0; i < corridor.Spine.Count; i++)
        {
            float dx = x - corridor.Spine[i].X;
            float dy = y - corridor.Spine[i].Y;
            float d2 = dx * dx + dy * dy;

            if (d2 < bestDistSq)
            {
                bestDistSq = d2;
                bestIndex = i;
            }
        }

        return bestIndex >= 0;
    }

    private static bool IsInsideCorridor(Corridor corridor, int x, int y)
    {
        if (!TryGetNearestCorridorInfo(corridor, x, y, out int idx, out float d2))
            return false;

        float r = corridor.Radii[idx];
        return d2 <= r * r;
    }

    // ---------------------------------------------------------------------
    // DLA
    // ---------------------------------------------------------------------

    private static DlaGraph RunSplineBoundDla(
        Corridor corridor,
        int width,
        int height,
        Random rng,
        float targetFillRatio,
        int maxWalkSteps,
        int spawnAttemptsPerParticle)
    {

        bool[,] occupied = new bool[width, height];
        int[,] nodeAt = new int[width, height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                nodeAt[x, y] = -1;

        List<DlaNode> nodes = new List<DlaNode>();

        // Seed the aggregate from the spline itself.
        SeedSpineIntoGraph(corridor, occupied, nodeAt, nodes, width, height);

        int corridorArea = 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (IsInsideCorridor(corridor, x, y))
                    corridorArea++;

        int targetNodeCount = nodes.Count + Math.Max(200, (int)(corridorArea * targetFillRatio));
        int failedParticles = 0;
        int maxFailedParticles = targetNodeCount * 6;

        while (nodes.Count < targetNodeCount && failedParticles < maxFailedParticles)
        {
            if (!TrySpawnWalker(corridor, occupied, rng, out int x, out int y, spawnAttemptsPerParticle))
            {
                failedParticles++;
                continue;
            }

            bool stuck = false;

            for (int step = 0; step < maxWalkSteps; step++)
            {
                int parent = FindAdjacentOccupiedNode(x, y, occupied, nodeAt, width, height);
                if (parent >= 0)
                {
                    occupied[x, y] = true;
                    nodeAt[x, y] = nodes.Count;
                    nodes.Add(new DlaNode(x, y, parent));
                    stuck = true;
                    break;
                }

                StepWalker(corridor, rng, ref x, ref y, width, height);
            }

            if (!stuck)
                failedParticles++;
        }

        return new DlaGraph
        {
            Width = width,
            Height = height,
            Nodes = nodes
        };
    }

    private static int EstimateWidth(Corridor corridor)
    {
        float maxX = 0f;
        for (int i = 0; i < corridor.Spine.Count; i++)
            if (corridor.Spine[i].X > maxX)
                maxX = corridor.Spine[i].X;

        return (int)MathF.Ceiling(maxX) + 1;
    }

    private static int EstimateHeight(Corridor corridor)
    {
        float maxY = 0f;
        for (int i = 0; i < corridor.Spine.Count; i++)
            if (corridor.Spine[i].Y > maxY)
                maxY = corridor.Spine[i].Y;

        return (int)MathF.Ceiling(maxY) + 1;
    }

    private static void SeedSpineIntoGraph(
        Corridor corridor,
        bool[,] occupied,
        int[,] nodeAt,
        List<DlaNode> nodes,
        int width,
        int height)
    {
        int lastNode = -1;

        for (int i = 0; i < corridor.Spine.Count - 1; i++)
        {
            Vec2 a = corridor.Spine[i];
            Vec2 b = corridor.Spine[i + 1];

            RasterizeLine(a, b, width, height, (x, y) =>
            {
                if (occupied[x, y])
                {
                    lastNode = nodeAt[x, y];
                    return;
                }

                int parent = lastNode;
                occupied[x, y] = true;
                nodeAt[x, y] = nodes.Count;
                nodes.Add(new DlaNode(x, y, parent));
                lastNode = nodes.Count - 1;
            });
        }
    }

    private static bool TrySpawnWalker(
        Corridor corridor,
        bool[,] occupied,
        Random rng,
        out int x,
        out int y,
        int maxAttempts)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            int index = rng.Next(1, corridor.Spine.Count - 1);

            Vec2 center = corridor.Spine[index];
            float radius = corridor.Radii[index];

            Vec2 tangent = GetTangent(corridor.Spine, index);
            Vec2 normal = Perp(tangent);
            if (rng.NextDouble() < 0.5)
                normal = normal * -1f;

            // Spawn in the outer half of the corridor so walkers come inward.
            float outward = RandomRange(rng, radius * 0.55f, radius * 0.98f);
            float along = RandomRange(rng, -radius * 0.20f, radius * 0.20f);

            Vec2 p = center + normal * outward + tangent * along;

            x = (int)MathF.Round(p.X);
            y = (int)MathF.Round(p.Y);

            if (x < 0 || y < 0 || x >= occupied.GetLength(0) || y >= occupied.GetLength(1))
                continue;

            if (!IsInsideCorridor(corridor, x, y))
                continue;

            if (occupied[x, y])
                continue;

            return true;
        }

        x = 0;
        y = 0;
        return false;
    }

    private static void StepWalker(
        Corridor corridor,
        Random rng,
        ref int x,
        ref int y,
        int width,
        int height)
    {
        TryGetNearestCorridorInfo(corridor, x, y, out int idx, out _);

        Vec2 spinePoint = corridor.Spine[idx];
        Vec2 tangent = GetTangent(corridor.Spine, idx);
        Vec2 inward = Normalize(new Vec2(spinePoint.X - x, spinePoint.Y - y));

        // Mostly random, but with a gentle inward bias so branches grow off the trunk.
        Vec2 move =
            RandomUnit8(rng) * 0.70f +
            inward * 0.35f +
            tangent * RandomRange(rng, -0.18f, 0.18f);

        int nx = x + Math.Sign(move.X);
        int ny = y + Math.Sign(move.Y);

        if (nx == x && ny == y)
            return;

        if (nx < 0 || ny < 0 || nx >= width || ny >= height)
            return;

        if (!IsInsideCorridor(corridor, nx, ny))
            return;

        x = nx;
        y = ny;
    }

    private static int FindAdjacentOccupiedNode(
        int x,
        int y,
        bool[,] occupied,
        int[,] nodeAt,
        int width,
        int height)
    {
        for (int oy = -1; oy <= 1; oy++)
        {
            for (int ox = -1; ox <= 1; ox++)
            {
                if (ox == 0 && oy == 0)
                    continue;

                int nx = x + ox;
                int ny = y + oy;

                if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                    continue;

                if (occupied[nx, ny])
                    return nodeAt[nx, ny];
            }
        }

        return -1;
    }

    // ---------------------------------------------------------------------
    // Height maps
    // ---------------------------------------------------------------------

    private static float[,] BuildSplineBodyMap(int width, int height, List<Vec2> spine, float baseRadius)
    {
        float[,] map = new float[width, height];
        int count = spine.Count;

        for (int i = 0; i < count; i++)
        {
            float t = count <= 1 ? 0f : i / (float)(count - 1);

            float taper = 0.45f + 0.55f * MathF.Pow(MathF.Sin(t * MathF.PI), 0.75f);
            float radius = baseRadius * taper;

            StampGaussianMax(map, spine[i], radius, 1.0f);
        }

        map = Blur3x3(map);
        map = Blur3x3(map);
        Normalize01(map);
        return map;
    }

    private static float[,] BuildWeightedRidgeMap(DlaGraph graph)
    {
        int width = graph.Width;
        int height = graph.Height;

        float[,] ridge = new float[width, height];
        List<DlaNode> nodes = graph.Nodes;

        if (nodes.Count == 0)
            return ridge;

        float[] weight = new float[nodes.Count];
        for (int i = 0; i < weight.Length; i++)
            weight[i] = 1f;

        // Push depth back toward the trunk.
        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            int parent = nodes[i].ParentIndex;
            if (parent >= 0)
                weight[parent] = Math.Max(weight[parent], weight[i] + 1f);
        }

        float maxWeight = 1f;
        for (int i = 0; i < weight.Length; i++)
            if (weight[i] > maxWeight)
                maxWeight = weight[i];

        for (int i = 0; i < nodes.Count; i++)
        {
            DlaNode n = nodes[i];
            if (n.X < 0 || n.Y < 0 || n.X >= width || n.Y >= height)
                continue;

            float w = weight[i] / maxWeight;

            // Keep side ridges relatively close to the main spine in elevation.
            float h = 0.82f + 0.18f * MathF.Pow(w, 0.35f);

            if (n.ParentIndex >= 0)
            {
                DlaNode p = nodes[n.ParentIndex];
                if (p.X < 0 || p.Y < 0 || p.X >= width || p.Y >= height)
                    continue;

                float wp = weight[n.ParentIndex] / maxWeight;
                float hp = 0.82f + 0.18f * MathF.Pow(wp, 0.35f);

                float edgeH = MathF.Min(h, hp);

                // Draw actual connected ridge segments with a little thickness.
                DrawThickValueLine(ridge, n.X, n.Y, p.X, p.Y, 1.15f, edgeH);
            }
            else
            {
                StampDiskMax(ridge, n.X, n.Y, 1.15f, h);
            }
        }

        return ridge;
    }

    // ---------------------------------------------------------------------
    // Upscale + blur
    // ---------------------------------------------------------------------

    private static float[,] UpscaleWithSmallBlur(float[,] source, int targetWidth, int targetHeight)
    {
        float[,] current = source;

        while (current.GetLength(0) < targetWidth || current.GetLength(1) < targetHeight)
        {
            int nextW = Math.Min(targetWidth, current.GetLength(0) * 2);
            int nextH = Math.Min(targetHeight, current.GetLength(1) * 2);

            current = ResizeBilinear(current, nextW, nextH);
            current = Blur3x3(current);
        }

        if (current.GetLength(0) != targetWidth || current.GetLength(1) != targetHeight)
            current = ResizeBilinear(current, targetWidth, targetHeight);

        return current;
    }

    private static float[,] ResizeBilinear(float[,] src, int dstW, int dstH)
    {
        int srcW = src.GetLength(0);
        int srcH = src.GetLength(1);

        float[,] dst = new float[dstW, dstH];

        if (dstW <= 1 || dstH <= 1)
            return dst;

        for (int y = 0; y < dstH; y++)
        {
            float v = y / (float)(dstH - 1) * (srcH - 1);
            int y0 = (int)MathF.Floor(v);
            int y1 = Math.Min(y0 + 1, srcH - 1);
            float ty = v - y0;

            for (int x = 0; x < dstW; x++)
            {
                float u = x / (float)(dstW - 1) * (srcW - 1);
                int x0 = (int)MathF.Floor(u);
                int x1 = Math.Min(x0 + 1, srcW - 1);
                float tx = u - x0;

                float a = Lerp(src[x0, y0], src[x1, y0], tx);
                float b = Lerp(src[x0, y1], src[x1, y1], tx);
                dst[x, y] = Lerp(a, b, ty);
            }
        }

        return dst;
    }

    private static float[,] Blur3x3(float[,] src)
    {
        int width = src.GetLength(0);
        int height = src.GetLength(1);
        float[,] dst = new float[width, height];

        int[,] kernel =
        {
            { 1, 2, 1 },
            { 2, 4, 2 },
            { 1, 2, 1 }
        };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float sum = 0f;
                int weightSum = 0;

                for (int ky = -1; ky <= 1; ky++)
                {
                    for (int kx = -1; kx <= 1; kx++)
                    {
                        int sx = Clamp(x + kx, 0, width - 1);
                        int sy = Clamp(y + ky, 0, height - 1);

                        int w = kernel[ky + 1, kx + 1];
                        sum += src[sx, sy] * w;
                        weightSum += w;
                    }
                }

                dst[x, y] = sum / weightSum;
            }
        }

        return dst;
    }

    // ---------------------------------------------------------------------
    // Drawing
    // ---------------------------------------------------------------------

    private static void StampGaussianMax(float[,] map, Vec2 center, float radius, float amplitude)
    {
        int width = map.GetLength(0);
        int height = map.GetLength(1);

        float sigma = radius;
        float twoSigmaSq = 2f * sigma * sigma;
        float reach = radius * 3f;

        int minX = Math.Max(0, (int)MathF.Floor(center.X - reach));
        int maxX = Math.Min(width - 1, (int)MathF.Ceiling(center.X + reach));
        int minY = Math.Max(0, (int)MathF.Floor(center.Y - reach));
        int maxY = Math.Min(height - 1, (int)MathF.Ceiling(center.Y + reach));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x - center.X;
                float dy = y - center.Y;
                float d2 = dx * dx + dy * dy;

                float g = MathF.Exp(-d2 / twoSigmaSq) * amplitude;
                if (g > map[x, y])
                    map[x, y] = g;
            }
        }
    }

    private static void DrawValueLine(float[,] map, int x0, int y0, int x1, int y1, float value)
    {
        RasterizeLine(new Vec2(x0, y0), new Vec2(x1, y1), map.GetLength(0), map.GetLength(1), (x, y) =>
        {
            if (value > map[x, y])
                map[x, y] = value;
        });
    }

    private static void RasterizeLine(Vec2 a, Vec2 b, int width, int height, Action<int, int> plot)
    {
        int x0 = (int)MathF.Round(a.X);
        int y0 = (int)MathF.Round(a.Y);
        int x1 = (int)MathF.Round(b.X);
        int y1 = (int)MathF.Round(b.Y);

        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            if (x0 >= 0 && y0 >= 0 && x0 < width && y0 < height)
                plot(x0, y0);

            if (x0 == x1 && y0 == y1)
                break;

            int e2 = err * 2;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    // ---------------------------------------------------------------------
    // Spline helpers
    // ---------------------------------------------------------------------

    private static List<Vec2> SampleCatmullRomSpline(List<Vec2> controlPoints, int samplesPerSegment)
    {
        if (controlPoints == null || controlPoints.Count < 2)
            throw new ArgumentException("Need at least 2 control points.");

        List<Vec2> samples = new List<Vec2>();

        for (int i = 0; i < controlPoints.Count - 1; i++)
        {
            Vec2 p0 = (i == 0) ? controlPoints[i] : controlPoints[i - 1];
            Vec2 p1 = controlPoints[i];
            Vec2 p2 = controlPoints[i + 1];
            Vec2 p3 = (i + 2 < controlPoints.Count) ? controlPoints[i + 2] : controlPoints[i + 1];

            for (int j = 0; j < samplesPerSegment; j++)
            {
                float t = j / (float)samplesPerSegment;
                samples.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }

        samples.Add(controlPoints[controlPoints.Count - 1]);
        return samples;
    }

    private static Vec2 CatmullRom(Vec2 p0, Vec2 p1, Vec2 p2, Vec2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return new Vec2(
            0.5f * ((2f * p1.X) +
                    (-p0.X + p2.X) * t +
                    (2f * p0.X - 5f * p1.X + 4f * p2.X - p3.X) * t2 +
                    (-p0.X + 3f * p1.X - 3f * p2.X + p3.X) * t3),

            0.5f * ((2f * p1.Y) +
                    (-p0.Y + p2.Y) * t +
                    (2f * p0.Y - 5f * p1.Y + 4f * p2.Y - p3.Y) * t2 +
                    (-p0.Y + 3f * p1.Y - 3f * p2.Y + p3.Y) * t3)
        );
    }

    // ---------------------------------------------------------------------
    // Utility
    // ---------------------------------------------------------------------

    private static List<Vec2> ScalePoints(List<Vec2> points, int srcW, int srcH, int dstW, int dstH)
    {
        List<Vec2> result = new List<Vec2>(points.Count);

        float sx = srcW > 1 ? (dstW - 1) / (float)(srcW - 1) : 0f;
        float sy = srcH > 1 ? (dstH - 1) / (float)(srcH - 1) : 0f;

        for (int i = 0; i < points.Count; i++)
        {
            float x = points[i].X * sx;
            float y = points[i].Y * sy;

            x = Math.Clamp(x, 0f, dstW - 1);
            y = Math.Clamp(y, 0f, dstH - 1);

            result.Add(new Vec2(x, y));
        }

        return result;
    }

    private static void MergeMax(float[,] target, float[,] source)
    {
        int width = target.GetLength(0);
        int height = target.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (source[x, y] > target[x, y])
                    target[x, y] = source[x, y];
            }
        }
    }

    private static void Normalize01(float[,] map)
    {
        int width = map.GetLength(0);
        int height = map.GetLength(1);

        float min = float.MaxValue;
        float max = float.MinValue;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float v = map[x, y];
                if (v < min) min = v;
                if (v > max) max = v;
            }
        }

        float range = max - min;
        if (range <= 1e-6f)
        {
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    map[x, y] = 0f;
            return;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                map[x, y] = (map[x, y] - min) / range;
            }
        }
    }

    private static Vec2 GetTangent(List<Vec2> points, int index)
    {
        int prev = Math.Max(0, index - 1);
        int next = Math.Min(points.Count - 1, index + 1);
        return Normalize(points[next] - points[prev]);
    }

    private static Vec2 RandomUnit8(Random rng)
    {
        int dx = rng.Next(-1, 2);
        int dy = rng.Next(-1, 2);

        if (dx == 0 && dy == 0)
            dx = 1;

        return Normalize(new Vec2(dx, dy));
    }

    private static Vec2 Perp(Vec2 v) => new Vec2(-v.Y, v.X);

    private static float Distance(Vec2 a, Vec2 b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static Vec2 Normalize(Vec2 v)
    {
        float lenSq = v.X * v.X + v.Y * v.Y;
        if (lenSq < 1e-8f)
            return new Vec2(0f, 0f);

        float inv = 1f / MathF.Sqrt(lenSq);
        return new Vec2(v.X * inv, v.Y * inv);
    }

    private static Vec2 Lerp(Vec2 a, Vec2 b, float t)
        => new Vec2(Lerp(a.X, b.X, t), Lerp(a.Y, b.Y, t));

    private static float Lerp(float a, float b, float t)
        => a + (b - a) * t;

    private static float RandomRange(Random rng, float min, float max)
        => min + (float)rng.NextDouble() * (max - min);

    private static float Saturate(float v)
        => v < 0f ? 0f : (v > 1f ? 1f : v);

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Saturate((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    private static int Clamp(int v, int min, int max)
        => v < min ? min : (v > max ? max : v);

    private struct Vec2
    {
        public float X;
        public float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);
    }

    private class Corridor
    {
        public List<Vec2> Spine;
        public float[] Radii;
    }

    private struct DlaNode
    {
        public int X;
        public int Y;
        public int ParentIndex;

        public DlaNode(int x, int y, int parentIndex)
        {
            X = x;
            Y = y;
            ParentIndex = parentIndex;
        }
    }

    private class DlaGraph
    {
        public int Width;
        public int Height;
        public List<DlaNode> Nodes;
    }
}
