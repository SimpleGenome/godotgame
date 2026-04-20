Using Monotone Hermit Curve to remap 0-1 values into real units

This example is for mapping the height map.

Initialising the height curve

```cs
var heightCurve = new MonotoneHermiteCurve(
    minValue: -5000f,
    maxValue: 8000f,
    averageValue: 200f,
    runawayThresholdLower: 0.30f,
    runawayThresholdUpper: 0.72f,
    centerSteepness: 1.6f
);
```

Evaluating a single value

```cs
float height = heightCurve.Evaluate(0.63f);
```

Whole float[,] map to a new output map:

```cs
float[,] rawHeightMap = new float[1024, 1024];
float[,] remappedHeightMap = heightCurve.Evaluate(rawHeightMap);
```

Whole float[,] map into an existing output map:

```cs
float[,] rawHeightMap = new float[1024, 1024];
float[,] remappedHeightMap = new float[1024, 1024];

heightCurve.EvaluateInto(rawHeightMap, remappedHeightMap);
```

In-place overwrite:

```cs
heightCurve.EvaluateInPlace(rawHeightMap);
```