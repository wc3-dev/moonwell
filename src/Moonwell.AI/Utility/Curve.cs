using System;

namespace Moonwell.AI
{
    /// <summary>
    /// Turns an input of 0..1 into a score of 0..1, for an <see cref="Option"/>'s input: <c>Curve.Rising</c>,
    /// <c>Curve.Falling</c>, <c>Curve.Line</c>, <c>Curve.Power</c>, <c>Curve.S</c>, <c>Curve.Step</c> or <c>Curve.Points</c>.
    /// The input is clamped to 0..1 first, and the score is clamped to 0..1.
    /// </summary>
    [LocalSafe]
    public sealed class Curve
    {
        /// <summary>The shape of a straight line from one score to another.</summary>
        private const int LineShape = 0;

        /// <summary>The shape of the input to a power.</summary>
        private const int PowerShape = 1;

        /// <summary>The shape of the logistic S.</summary>
        private const int SShape = 2;

        /// <summary>The shape of a step from 0 to 1.</summary>
        private const int StepShape = 3;

        /// <summary>The shape of straight lines between points.</summary>
        private const int PointsShape = 4;

        /// <summary>Which shape the curve has.</summary>
        private readonly int shape;

        /// <summary>The line's start, the power's exponent, the S's middle or the step's input.</summary>
        private readonly float first;

        /// <summary>The line's end or the S's steepness.</summary>
        private readonly float second;

        /// <summary>The points as x, y pairs, for a curve of points; null for any other.</summary>
        private readonly float[]? points;

        /// <summary>Makes a curve of a shape with its numbers.</summary>
        private Curve(int shape, float first, float second, float[]? points)
        {
            this.shape = shape;
            this.first = first;
            this.second = second;
            this.points = points;
        }

        /// <summary>The score is the input: 0 gives 0, 1 gives 1.</summary>
        public static Curve Rising => Line(0f, 1f);

        /// <summary>The score is 1 less the input: 0 gives 1, 1 gives 0.</summary>
        public static Curve Falling => Line(1f, 0f);

        /// <summary>A straight line from the score <c>from</c> at 0 to the score <c>to</c> at 1.</summary>
        public static Curve Line(float from, float to)
        {
            return new Curve(LineShape, from, to, null);
        }

        /// <summary>The input to a power: above 1 it rises slowly, then fast; below 1 fast, then slowly. An exponent of 0 or less throws an ArgumentException.</summary>
        public static Curve Power(float exponent)
        {
            if (!(exponent > 0f))
            {
                throw new ArgumentException("The exponent of Curve.Power must be more than 0, not " + exponent + ".");
            }

            return new Curve(PowerShape, exponent, 0f, null);
        }

        /// <summary>The logistic S, 1 / (1 + e^(-steepness * (input - middle))): 0.5 at the middle, rising faster the steeper it is, and falling for a negative steepness.</summary>
        public static Curve S(float middle, float steepness)
        {
            return new Curve(SShape, middle, steepness, null);
        }

        /// <summary>0 below the input <c>at</c>, and 1 from it on.</summary>
        public static Curve Step(float at)
        {
            return new Curve(StepShape, at, 0f, null);
        }

        /// <summary>
        /// Straight lines between points given as x, y pairs, the x never smaller than the one before:
        /// <c>Curve.Points(0f, 0f, 0.5f, 0.2f, 1f, 1f)</c>. Below the first x it gives the first y, past the last x the last
        /// y. Two points with the same x make a jump, and at exactly that x it gives the later point's y. No points, an odd
        /// count or an x smaller than the one before throws an ArgumentException. The numbers are copied.
        /// </summary>
        public static Curve Points(params float[] xy)
        {
            if (xy == null || xy.Length == 0 || xy.Length % 2 != 0)
            {
                throw new ArgumentException("Curve.Points takes x, y pairs, at least one, not " + (xy == null ? 0 : xy.Length) + " numbers.");
            }

            for (var i = 2; i < xy.Length; i += 2)
            {
                if (xy[i] < xy[i - 2])
                {
                    throw new ArgumentException("The x of Curve.Points never goes down, but " + xy[i] + " comes after " + xy[i - 2] + ".");
                }
            }

            return new Curve(PointsShape, 0f, 0f, (float[])xy.Clone());
        }

        /// <summary>The score of an input: the input clamped to 0..1, put through the curve, and the score clamped to 0..1.</summary>
        public float Of(float x)
        {
            var at = Math.Clamp(x, 0f, 1f);
            float y;
            switch (shape)
            {
                case LineShape:
                    y = first + (second - first) * at;
                    break;
                case PowerShape:
                    y = (float)Math.Pow(at, first);
                    break;
                case SShape:
                    // Clamped so that a steep S never makes e^power infinite in the game's floats.
                    y = (float)(1.0 / (1.0 + Math.Exp(Math.Clamp(-second * (at - first), -80f, 80f))));
                    break;
                case StepShape:
                    y = at >= first ? 1f : 0f;
                    break;
                default:
                    y = OnPoints(at);
                    break;
            }

            return Math.Clamp(y, 0f, 1f);
        }

        /// <summary>The y of the points at an x: the first y below the first x, otherwise from the last point at or before x, on the line to the next one.</summary>
        private float OnPoints(float x)
        {
            var xy = points!;
            if (x < xy[0])
            {
                return xy[1];
            }

            var at = xy.Length - 2;
            while (xy[at] > x)
            {
                at -= 2;
            }

            if (at == xy.Length - 2)
            {
                return xy[at + 1];
            }

            return xy[at + 1] + (xy[at + 3] - xy[at + 1]) * (x - xy[at]) / (xy[at + 2] - xy[at]);
        }
    }
}
