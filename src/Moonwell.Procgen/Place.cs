using System;
using System.Collections;
using System.Collections.Generic;

namespace Moonwell.Procgen
{
    /// <summary>A side of an area, for an exit.</summary>
    public enum Side
    {
        Left,
        Right,
        Bottom,
        Top,
    }

    /// <summary>
    /// A level place of a <c>TerrainGenerator</c>: a circle, or a rectangle from <c>Level(Bounds)</c>, of ground level at the ground as it was, never raised, so the
    /// buildings and items made there stand on it (the game draws them sunk on raised ground). It is never blocked, has no
    /// trees, and roads can join it. <c>Level</c>, <c>Places</c> and <c>Exit</c> give one.
    /// </summary>
    public sealed class Place
    {
        internal readonly float Blend;

        /// <summary>The places it was picked with, which say what is made there; null for one from Level or Exit.</summary>
        internal readonly PlaceList? List;

        private List<Unit> units = new List<Unit>();

        internal Place(string name, float x, float y, float radius, float blend, PlaceList? list)
        {
            Name = name;
            X = x;
            Y = y;
            Radius = radius;
            Width = 2f * radius;
            Height = 2f * radius;
            Blend = blend;
            List = list;
        }

        internal Place(Bounds area, float blend)
        {
            Name = "";
            X = area.Center.X;
            Y = area.Center.Y;
            Width = area.Size.X;
            Height = area.Size.Y;
            Radius = Math.Min(Width, Height) / 2f;
            IsRectangle = true;
            Blend = blend;
        }

        /// <summary>The name it was given; empty for a place from Level.</summary>
        [LocalSafe]
        public string Name { get; }

        /// <summary>The x of its centre.</summary>
        [LocalSafe]
        public float X { get; }

        /// <summary>The y of its centre.</summary>
        [LocalSafe]
        public float Y { get; }

        /// <summary>How far out from its centre it is level every way: half the shorter side of a rectangle.</summary>
        [LocalSafe]
        public float Radius { get; }

        /// <summary>How wide it is level: twice the radius of a circle.</summary>
        [LocalSafe]
        public float Width { get; }

        /// <summary>How high it is level, along y: twice the radius of a circle.</summary>
        [LocalSafe]
        public float Height { get; }

        /// <summary>Whether it is a rectangle, from <c>Level(Bounds)</c>, rather than a circle.</summary>
        [LocalSafe]
        public bool IsRectangle { get; }

        /// <summary>How hard it is, from 1 for the places nearest the place they were spread from to the most difficulties asked for the farthest; 1 when none was asked.</summary>
        [LocalSafe]
        public int Difficulty { get; internal set; } = 1;

        /// <summary>The units and buildings made there the last time a plan holding it was made, in the order of their group; empty before then, or when nothing is made there. A unit killed or removed stays in it.</summary>
        [LocalSafe]
        public IReadOnlyList<Unit> Units => units;

        /// <summary>The world corner nearest its centre, counted from (0, 0): the one plan holding that corner holds the place.</summary>
        internal int Column => Noise.Floor(X / 128f + 0.5f);

        internal int Row => Noise.Floor(Y / 128f + 0.5f);

        /// <summary>How far a point is outside it: 0 on it, so the blend is an even band all round.</summary>
        internal float Beyond(float x, float y)
        {
            var dx = Math.Abs(x - X);
            var dy = Math.Abs(y - Y);
            if (!IsRectangle)
            {
                return Math.Max(Maths.Sqrt(dx * dx + dy * dy) - Radius, 0f);
            }

            dx = Math.Max(dx - Width / 2f, 0f);
            dy = Math.Max(dy - Height / 2f, 0f);
            return Maths.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Makes its group in the scope: the first type at its centre, the rest round it facing the centre, owned by the
        /// owner its places were given (Neutral Hostile unless set). Nothing for a place with no group of its difficulty.
        /// </summary>
        internal void Make(Scope scope)
        {
            units = new List<Unit>();
            var group = List?.GroupOf(this);
            if (group == null)
            {
                return;
            }

            var owner = List!.Owner ?? Player.NeutralAggressive;
            var ring = Math.Min(Radius * 0.5f, 256f);
            var start = Noise.Hash(List.Seed + 3, Column, Row) * 360f;
            for (var k = 0; k < group.Length; k++)
            {
                var x = X;
                var y = Y;
                var facing = start;
                if (k > 0)
                {
                    var angle = start + 360f * (k - 1) / (group.Length - 1);
                    x += Angles.Cos(angle) * ring;
                    y += Angles.Sin(angle) * ring;
                    facing = angle + 180f;
                }

                var made = Unit.Create(group[k], owner, x, y, scope, facing);
                if (made != null)
                {
                    units.Add(made);
                }
            }
        }

        /// <summary>The units its group makes: none when it has no group.</summary>
        internal int UnitsToMake()
        {
            var group = List?.GroupOf(this);
            return group == null ? 0 : group.Length;
        }
    }

    /// <summary>
    /// The places one <c>TerrainGenerator.Places</c> call picked, in the order picked, and the groups of units and buildings
    /// to make at them by difficulty.
    /// </summary>
    public sealed class PlaceList : IReadOnlyList<Place>
    {
        internal readonly int Seed;
        internal readonly Player? Owner;

        private readonly List<Place> places = new List<Place>();

        /// <summary>The groups of each difficulty, from 1.</summary>
        private readonly List<int[]>[] groups;

        internal PlaceList(int seed, int difficulties, Player? owner)
        {
            Seed = seed;
            Owner = owner;
            groups = new List<int[]>[difficulties];
            for (var k = 0; k < difficulties; k++)
            {
                groups[k] = new List<int[]>();
            }
        }

        /// <summary>The difficulties its places are spread over.</summary>
        [LocalSafe]
        public int Difficulties => groups.Length;

        /// <summary>How many places were picked: fewer than asked when no more fit.</summary>
        [LocalSafe]
        public int Count => places.Count;

        /// <summary>A place, in the order picked.</summary>
        [LocalSafe]
        public Place this[int index] => places[index];

        /// <summary>
        /// Adds a group of units and buildings to make at the places of a difficulty: the first at a place's centre (a
        /// building goes there), the rest round it. Each place takes one group of its difficulty, picked from the seed, when
        /// its plan is made. Types are object ids (<c>ObjectTypes.IdOf&lt;Kobold&gt;()</c> or <c>ObjectTypes.FourCC("nkob")</c>).
        /// A difficulty out of 1 to <c>Difficulties</c>, no types, or a type of 0 throws ArgumentException.
        /// </summary>
        public void Add(int difficulty, params int[] types)
        {
            if (difficulty < 1 || difficulty > groups.Length)
            {
                throw new ArgumentException("PlaceList.Add takes a difficulty from 1 to " + groups.Length + ", not " + difficulty);
            }

            if (types == null || types.Length == 0)
            {
                throw new ArgumentException("PlaceList.Add takes one type or more");
            }

            foreach (var type in types)
            {
                if (type == 0)
                {
                    throw new ArgumentException("PlaceList.Add takes object ids, not 0");
                }
            }

            groups[difficulty - 1].Add((int[])types.Clone());
        }

        public IEnumerator<Place> GetEnumerator()
        {
            return places.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        internal void Take(Place place)
        {
            places.Add(place);
        }

        /// <summary>The group a place makes, picked from the seed by where it is; null when its difficulty has none.</summary>
        internal int[]? GroupOf(Place place)
        {
            var choices = groups[place.Difficulty - 1];
            if (choices.Count == 0)
            {
                return null;
            }

            return choices[Math.Min((int)(Noise.Hash(Seed + 5, place.Column, place.Row) * choices.Count), choices.Count - 1)];
        }
    }
}
