# Using Monotone Hermit Curve to remap 0-1 values into real units

This example is for mapping the height map.

### Initialising the height curve

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

### Evaluating a single value

```cs
float height = heightCurve.Evaluate(0.63f);
```

### Whole float[,] map to a new output map:

```cs
float[,] rawHeightMap = new float[1024, 1024];
float[,] remappedHeightMap = heightCurve.Evaluate(rawHeightMap);
```

### Whole float[,] map into an existing output map:

```cs
float[,] rawHeightMap = new float[1024, 1024];
float[,] remappedHeightMap = new float[1024, 1024];

heightCurve.EvaluateInto(rawHeightMap, remappedHeightMap);
```

### In-place overwrite:

```cs
heightCurve.EvaluateInPlace(rawHeightMap);
```


# MonotoneHermiteCurve Parameter Guide

This curve remaps an input value in the range `0..1` into a more useful output range, such as:

- terrain height: `-5000m .. 8000m`
- temperature: `-60C .. 50C`
- humidity: `0 .. 100`

It is designed to give more control over **how values are distributed**, not just what the minimum and maximum are.

---

## Parameters

### `inputValue`
The raw source value, expected to be between `0` and `1`.

### `minValue`
The output when `inputValue = 0`.

### `maxValue`
The output when `inputValue = 1`.

### `averageValue`
The output when `inputValue = 0.5`.

If omitted, it defaults to the midpoint between `minValue` and `maxValue`.

### `runawayThresholdLower`
The lower control point on the x-axis, usually somewhere below `0.5`.

This helps define where the curve starts transitioning into the middle of the range.

### `runawayThresholdUpper`
The upper control point on the x-axis, usually somewhere above `0.5`.

This helps define where the curve transitions out of the middle and toward the upper end.

### `centerSteepness`
Controls how compressed or expanded the middle part of the curve is.

- `1.0` = neutral
- greater than `1.0` = steeper middle
- less than `1.0` = flatter middle

---

# What each value does

## `minValue`

### A) Effect on the curve
Sets the very bottom of the curve at `x = 0`.

It does not change the shape much by itself. It mainly shifts the lower endpoint.

### B) Effect on a height map
Controls the lowest possible terrain.

Examples:
- lower `minValue` = deeper oceans, trenches, valleys
- higher `minValue` = shallower oceans, lifted lowlands

---

## `maxValue`

### A) Effect on the curve
Sets the very top of the curve at `x = 1`.

Like `minValue`, this mostly defines the upper endpoint rather than the curve shape.

### B) Effect on a height map
Controls the highest possible terrain.

Examples:
- higher `maxValue` = taller mountains, higher peaks
- lower `maxValue` = flatter highlands, lower mountains

---

## `averageValue`

### A) Effect on the curve
Sets the exact output value at `x = 0.5`.

This shifts the middle of the curve up or down and changes the balance between low and high values.

- a lower `averageValue` pulls the curve downward around the center
- a higher `averageValue` pushes the curve upward around the center

### B) Effect on a height map
This is one of the most important controls for the overall "feel" of the map.

Examples:
- lower `averageValue` = more of the map tends toward low elevations
- higher `averageValue` = more of the map tends toward uplands and higher plateaus

Practical effect:
- if your noise map is evenly distributed, lowering `averageValue` usually gives more sea, basin, and lowland area
- raising `averageValue` usually gives more elevated land and less deep low terrain

---

## `runawayThresholdLower`

### A) Effect on the curve
Sets the lower interior x-position of the curve.

It helps decide where the lower region transitions into the middle region.

- moving it closer to `0` stretches the lower part of the curve
- moving it closer to `0.5` compresses the lower part of the curve

### B) Effect on a height map
This changes how much of the input range is spent in the low-elevation band.

Examples:
- lower threshold farther left = more gradual buildup from the minimum
- lower threshold closer to center = low terrain changes more quickly into mid terrain

Practical terrain effect:
- farther left can create broader shelves, plains, shallow seabeds, or long foothill ramps
- closer to center can make lowlands give way faster to inland terrain

---

## `runawayThresholdUpper`

### A) Effect on the curve
Sets the upper interior x-position of the curve.

It helps decide where the middle region transitions into the high region.

- moving it closer to `1` stretches the upper part of the curve
- moving it closer to `0.5` compresses the upper part of the curve

### B) Effect on a height map
This changes how much of the input range is spent in the high-elevation band.

Examples:
- upper threshold farther right = more gradual approach into peaks
- upper threshold closer to center = the map reaches high elevations more quickly

Practical terrain effect:
- farther right can create broad uplands, plateaus, and gentle alpine buildup
- closer to center can create sharper mountain emergence and less time spent in upper-mid elevations

---

## `centerSteepness`

### A) Effect on the curve
Controls how strongly the curve rises through the middle.

- `centerSteepness = 1.0` gives the neutral curve
- `centerSteepness > 1.0` pushes the knee values outward in y-space, making the center transition steeper
- `centerSteepness < 1.0` pulls the knee values toward the average, flattening the center

This is the main "shape intensity" control.

### B) Effect on a height map
This changes how abruptly the map moves between low, mid, and high terrain.

Examples:
- higher steepness = stronger separation between terrain bands
- lower steepness = smoother, more gradual transitions

Practical terrain effect:
- high steepness can make coasts, escarpments, foothills, and mountain bands feel more dramatic
- low steepness can make terrain feel softer, more rolling, and more blended

---

# How the parameters interact

## `minValue` + `maxValue`
These define the total output range.

For a height map, they define the full possible altitude span.

Example:
- `-5000 .. 8000` gives a very large world relief range
- `-500 .. 1500` gives a much gentler world

---

## `averageValue` + `centerSteepness`
These strongly affect the overall distribution of terrain.

- `averageValue` changes where the middle sits
- `centerSteepness` changes how quickly the curve moves through that middle

Together, these decide whether the world feels:
- mostly low and flooded
- mostly balanced
- mostly elevated

and whether changes in elevation feel:
- gentle
- moderate
- dramatic

---

## `runawayThresholdLower` + `runawayThresholdUpper`
These define where the lower, middle, and upper regions are divided along the input axis.

The distance between them matters a lot:

- **closer together** = narrower middle band
- **farther apart** = wider middle band

### Height map effect
- narrow middle band = quicker movement from lowlands to highlands
- wide middle band = more space spent in mid-elevations

---

# Example interpretations

## Example 1: Deep oceans, lots of lowland, rare mountains

```csharp
minValue: -6000f
maxValue: 4000f
averageValue: -500f
runawayThresholdLower: 0.30f
runawayThresholdUpper: 0.80f
centerSteepness: 0.9f