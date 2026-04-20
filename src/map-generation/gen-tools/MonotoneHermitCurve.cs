using System;

public sealed class MonotoneHermiteCurve
{
    private readonly float _x0 = 0f;
    private readonly float _x1;
    private readonly float _x2 = 0.5f;
    private readonly float _x3;
    private readonly float _x4 = 1f;

    private readonly float _y0;
    private readonly float _y1;
    private readonly float _y2;
    private readonly float _y3;
    private readonly float _y4;

    private readonly float _m0;
    private readonly float _m1;
    private readonly float _m2;
    private readonly float _m3;
    private readonly float _m4;

    private readonly float _h01;
    private readonly float _h12;
    private readonly float _h23;
    private readonly float _h34;

    private readonly float _invH01;
    private readonly float _invH12;
    private readonly float _invH23;
    private readonly float _invH34;

    public float MinValue => _y0;
    public float MaxValue => _y4;
    public float AverageValue => _y2;
    public float LowerThreshold => _x1;
    public float UpperThreshold => _x3;

    public MonotoneHermiteCurve(
        float minValue,
        float maxValue,
        float? averageValue = null,
        float runawayThresholdLower = 0.25f,
        float runawayThresholdUpper = 0.75f,
        float centerSteepness = 1.0f)
    {
        if (maxValue < minValue)
            throw new ArgumentException("maxValue must be >= minValue.");

        float yMid = averageValue ?? ((minValue + maxValue) * 0.5f);
        yMid = Clamp(yMid, minValue, maxValue);

        float x1 = Clamp(runawayThresholdLower, 0.0001f, 0.4999f);
        float x3 = Clamp(runawayThresholdUpper, 0.5001f, 0.9999f);

        if (x1 >= x3)
            throw new ArgumentException("runawayThresholdLower must be < runawayThresholdUpper.");

        _x1 = x1;
        _x3 = x3;

        _y0 = minValue;
        _y2 = yMid;
        _y4 = maxValue;

        float y1Neutral = Lerp(_y0, _y2, _x1 / _x2);
        float y3Neutral = Lerp(_y2, _y4, (_x3 - _x2) / (_x4 - _x2));

        _y1 = Clamp(
            ShapeKnee(
                neutralValue: y1Neutral,
                flatTarget: _y2,
                steepTarget: _y0,
                centerSteepness: centerSteepness),
            _y0, _y2);

        _y3 = Clamp(
            ShapeKnee(
                neutralValue: y3Neutral,
                flatTarget: _y2,
                steepTarget: _y4,
                centerSteepness: centerSteepness),
            _y2, _y4);

        _h01 = _x1 - _x0;
        _h12 = _x2 - _x1;
        _h23 = _x3 - _x2;
        _h34 = _x4 - _x3;

        _invH01 = 1f / _h01;
        _invH12 = 1f / _h12;
        _invH23 = 1f / _h23;
        _invH34 = 1f / _h34;

        float d01 = (_y1 - _y0) * _invH01;
        float d12 = (_y2 - _y1) * _invH12;
        float d23 = (_y3 - _y2) * _invH23;
        float d34 = (_y4 - _y3) * _invH34;

        _m0 = ComputeEndpointTangent(_h01, _h12, d01, d12);
        _m1 = ComputeInteriorTangent(_h01, _h12, d01, d12);
        _m2 = ComputeInteriorTangent(_h12, _h23, d12, d23);
        _m3 = ComputeInteriorTangent(_h23, _h34, d23, d34);
        _m4 = ComputeEndpointTangent(_h34, _h23, d34, d23);
    }

    /// <summary>
    /// Evaluate one input value in [0,1].
    /// Values outside [0,1] are clamped.
    /// </summary>
    public float Evaluate(float x)
    {
        if (x <= 0f) return _y0;
        if (x >= 1f) return _y4;

        if (x < _x1)
            return EvaluateSegment(x, _x0, _y0, _m0, _x1, _y1, _m1, _h01, _invH01);

        if (x < _x2)
            return EvaluateSegment(x, _x1, _y1, _m1, _x2, _y2, _m2, _h12, _invH12);

        if (x < _x3)
            return EvaluateSegment(x, _x2, _y2, _m2, _x3, _y3, _m3, _h23, _invH23);

        return EvaluateSegment(x, _x3, _y3, _m3, _x4, _y4, _m4, _h34, _invH34);
    }

    /// <summary>
    /// Remap a full 2D map into a newly allocated output array.
    /// </summary>
    public float[,] Evaluate(float[,] input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        int width = input.GetLength(0);
        int height = input.GetLength(1);

        float[,] output = new float[width, height];
        EvaluateInto(input, output);
        return output;
    }

    /// <summary>
    /// Remap a full 2D map into an existing output array.
    /// input and output must have the same dimensions.
    /// </summary>
    public void EvaluateInto(float[,] input, float[,] output)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        if (output == null)
            throw new ArgumentNullException(nameof(output));

        int width = input.GetLength(0);
        int height = input.GetLength(1);

        if (output.GetLength(0) != width || output.GetLength(1) != height)
            throw new ArgumentException("Input and output maps must have the same dimensions.");

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                output[x, y] = Evaluate(input[x, y]);
            }
        }
    }

    /// <summary>
    /// Remap a full 2D map in place, replacing the original values.
    /// </summary>
    public void EvaluateInPlace(float[,] map)
    {
        if (map == null)
            throw new ArgumentNullException(nameof(map));

        int width = map.GetLength(0);
        int height = map.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                map[x, y] = Evaluate(map[x, y]);
            }
        }
    }

    private static float EvaluateSegment(
        float x,
        float xA, float yA, float mA,
        float xB, float yB, float mB,
        float h, float invH)
    {
        float t = (x - xA) * invH;
        float t2 = t * t;
        float t3 = t2 * t;

        float h00 = 2f * t3 - 3f * t2 + 1f;
        float h10 = t3 - 2f * t2 + t;
        float h01 = -2f * t3 + 3f * t2;
        float h11 = t3 - t2;

        return h00 * yA
             + h10 * h * mA
             + h01 * yB
             + h11 * h * mB;
    }

    private static float ComputeInteriorTangent(float hLeft, float hRight, float dLeft, float dRight)
    {
        if (dLeft == 0f || dRight == 0f || !SameSign(dLeft, dRight))
            return 0f;

        float w1 = 2f * hRight + hLeft;
        float w2 = hRight + 2f * hLeft;
        return (w1 + w2) / ((w1 / dLeft) + (w2 / dRight));
    }

    private static float ComputeEndpointTangent(float h0, float h1, float d0, float d1)
    {
        float m = ((2f * h0 + h1) * d0 - h0 * d1) / (h0 + h1);

        if (!SameSign(m, d0))
            return 0f;

        if (!SameSign(d0, d1) && MathF.Abs(m) > MathF.Abs(3f * d0))
            return 3f * d0;

        return m;
    }

    private static float ShapeKnee(
        float neutralValue,
        float flatTarget,
        float steepTarget,
        float centerSteepness)
    {
        centerSteepness = MathF.Max(0f, centerSteepness);

        if (centerSteepness >= 1f)
        {
            float t = 1f - (1f / centerSteepness);
            return Lerp(neutralValue, steepTarget, t);
        }
        else
        {
            float t = 1f - centerSteepness;
            return Lerp(neutralValue, flatTarget, t);
        }
    }

    private static bool SameSign(float a, float b)
    {
        return (a > 0f && b > 0f) || (a < 0f && b < 0f);
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + ((b - a) * t);
    }

    private static float Clamp(float v, float min, float max)
    {
        return MathF.Max(min, MathF.Min(max, v));
    }
}