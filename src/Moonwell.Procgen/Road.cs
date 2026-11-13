using System;
using System.Collections.Generic;

namespace Moonwell.Procgen
{
    /// <summary>
    /// A road of a <c>TerrainGenerator</c> from one place to another: a line of points from <c>From</c> to <c>To</c>, the
    /// straight lines between them down its middle. Where a plan holds it, the ground along it is levelled to an easy slope,
    /// painted with the generator's <c>RoadTile</c>, and kept clear of trees and blocked corners.
    /// </summary>
    public sealed class Road
    {
        private readonly List<Vector2> points;

        internal Road(Place from, Place to, float width, List<Vector2> points)
        {
            From = from;
            To = to;
            Width = width;
            this.points = points;
            var length = 0f;
            for (var k = 1; k < points.Count; k++)
            {
                length += points[k - 1].DistanceTo(points[k]);
            }

            Length = length;
        }

        /// <summary>The place it starts at.</summary>
        [LocalSafe]
        public Place From { get; }

        /// <summary>The place it ends at.</summary>
        [LocalSafe]
        public Place To { get; }

        /// <summary>How wide it is, in the game's units.</summary>
        [LocalSafe]
        public float Width { get; }

        /// <summary>Its middle from From's centre to To's, a point about every 256.</summary>
        [LocalSafe]
        public IReadOnlyList<Vector2> Points => points;

        /// <summary>Its length along the points.</summary>
        [LocalSafe]
        public float Length { get; }

        /// <summary>The road as a route ending at one of its places: From to To, or To to From. Another place throws ArgumentException.</summary>
        [LocalSafe]
        public Route Toward(Place place)
        {
            if (place == To)
            {
                return new Route(new List<Vector2>(points), true);
            }

            if (place != From)
            {
                throw new ArgumentException("Road.Toward takes the place the road starts or ends at");
            }

            var back = new List<Vector2>(points);
            back.Reverse();
            return new Route(back, true);
        }

        /// <summary>
        /// The road's outline from one length along it to another (from From), as wide as the road: for an area along a
        /// stretch of it (<c>AreaShape.Polygon(road.Outline(0f, road.Length / 3f))</c>). With no lengths, the whole road. A
        /// stretch of no length throws ArgumentException.
        /// </summary>
        [LocalSafe]
        public Polygon Outline(float from = 0f, float to = float.MaxValue)
        {
            from = Math.Max(from, 0f);
            to = Math.Min(to, Length);
            if (!(to > from))
            {
                throw new ArgumentException("Road.Outline takes a stretch along the road, from " + from + " to " + to);
            }

            var middle = new List<Vector2>();
            var along = 0f;
            for (var k = 1; k < points.Count; k++)
            {
                var a = points[k - 1];
                var b = points[k];
                var length = a.DistanceTo(b);
                if (middle.Count == 0 && along + length > from)
                {
                    middle.Add(Vector2.Lerp(a, b, length > 0f ? (from - along) / length : 0f));
                }

                if (along + length >= to)
                {
                    middle.Add(Vector2.Lerp(a, b, length > 0f ? (to - along) / length : 1f));
                    break;
                }

                along += length;
                if (middle.Count > 0)
                {
                    middle.Add(b);
                }
            }

            var half = Width / 2f;
            var outline = new Vector2[middle.Count * 2];
            for (var k = 0; k < middle.Count; k++)
            {
                var ahead = middle[Math.Min(k + 1, middle.Count - 1)] - middle[Math.Max(k - 1, 0)];
                var side = new Vector2(-ahead.Y, ahead.X).Normalized() * half;
                outline[k] = middle[k] + side;
                outline[outline.Length - 1 - k] = middle[k] - side;
            }

            return new Polygon(outline);
        }
    }
}
