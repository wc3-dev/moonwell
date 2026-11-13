using System;
using System.Collections.Generic;

namespace Moonwell.Procgen
{
    /// <summary>
    /// Makes ground from a seed: rolling heights from layered noise, tiles, blocked corners and trees by rules on height
    /// and slope, level places for bases and camps with what is made at them, and roads between places, with routes along
    /// them. Set it up, then <c>Plan(area)</c> works the ground out and the plan's
    /// <c>Apply</c> makes it. The same seed, settings and area give the same ground on every machine; a seed from
    /// <c>Game.Random</c> gives new ground every game. The game makes no cliffs, ramps or water while it runs, so the
    /// ground is slopes. Slopes are read from the heights the plan adds, so the rules fit ground that starts flat. A
    /// generator is a biome: its own hill shape and rules are the ground outside every biome it uses.
    /// </summary>
    public sealed class TerrainGenerator : Biome
    {
        /// <summary>The distance between corners.</summary>
        private const float TileSize = 128f;

        /// <summary>How many corners across a patch of a rule's cover is.</summary>
        private const float PatchSize = 4f;

        /// <summary>How many corners across a forest's patch is.</summary>
        private const float ForestPatch = 8f;

        /// <summary>How many corners across a gap in a forest wall is.</summary>
        private const int GapSize = 4;

        // What the work costs in the game's Lua, in microseconds, measured on 3.0.1.24371 (game facts, 11-13): fixed numbers,
        // so a step of Make does the same corners on every machine.
        private const float NoiseCost = 3.6f;   // a corner's noise, a layer
        private const float LevelCost = 1.5f;   // a corner in a level place's reach
        private const float FadeCost = 0.5f;    // a corner faded
        private const float RuleCost = 0.5f;    // a corner, a rule
        private const float RaiseCost = 6.2f;   // a corner's crater
        private const float PaintCost = 4f;     // a corner painted
        private const float ScanCost = 0.5f;    // a corner looked at for blocking (only blocked ones are walked)
        private const float BlockCost = 130f;   // a blocked corner, a kind
        private const float TreeCost = 45f;     // a tree made at the ground's height (200 in 9 ms)

        // Inferred from NoiseCost, not measured: deciding a corner's tree, a rule.
        private const float ForestCost = 8f;
        private const float WallCost = 14f;
        private const float ScatterCost = 1f;

        // Inferred, not measured: a corner looked at near a stretch of road, a place's level read at a road's point, and a
        // unit or building made at a place (decision 220 measured 0.34 to 0.44 ms to make and remove a unit).
        private const float StampCost = 1f;
        private const float PlaceCost = 0.5f;
        private const float UnitCost = 300f;

        /// <summary>The distance between a road's points.</summary>
        private const float RoadSpacing = 256f;

        /// <summary>How far a road bends off the straight line, as a share of a leg's length, and at most.</summary>
        private const float RoadBend = 0.25f;

        private const float RoadBendMost = 768f;

        /// <summary>How far along a road its bends are apart.</summary>
        private const float RoadWave = 3072f;

        /// <summary>The steepest a road is levelled to, as rise over run (about 8 degrees).</summary>
        private const float RoadSlope = 0.14f;

        /// <summary>How far past a road's edge the ground blends back to the hills.</summary>
        private const float RoadBlend = 256f;

        /// <summary>The road's points on each side of one that its heights are averaged over, before the slope is limited.</summary>
        private const int RoadSmooth = 3;

        /// <summary>How far past a road's edge no corner is blocked (a blocked corner takes the cells 64 round it) and no tree stands.</summary>
        private const float RoadUnblocked = 64f;

        private const float RoadNoTrees = 128f;

        /// <summary>How many spots Places tries for each place asked.</summary>
        private const int PlaceTries = 30;

        /// <summary>The names of Make's steps, as <c>TerrainMaking.Step</c> gives them.</summary>
        private const string RoadsStep = "Roads";

        private const string HeightsStep = "Heights";

        private const string TilesStep = "Tiles";

        private const string BlockedStep = "Blocked";

        private const string TreesStep = "Trees";

        private const string PlacesStep = "Places";

        /// <summary>How far across, in corners, the patches of noise that pick a corner's biome where biomes blend are.</summary>
        private const float PickSize = 3f;

        // Measured in .NET over a 400 by 400 corner plan of two biomes (11-13): the noise is read at MixPatch over the size, so
        // patches come about the size across, and its margin is MixSlope a unit of that, so a border blends over about the blend.
        private const float MixPatch = 1.45f;
        private const float MixSlope = 0.385f;

        /// <summary>The Use and Mix calls, in order: later ones lie over earlier ones.</summary>
        private readonly List<Spot> spots = new List<Spot>();

        /// <summary>Every place, from Level, Places and Exit, in the order made.</summary>
        private readonly List<Place> places = new List<Place>();

        private readonly List<Road> roads = new List<Road>();

        /// <summary>The Places and Exit calls so far, each one's seed apart.</summary>
        private int placeCalls;

        /// <summary>A generator for a seed.</summary>
        public TerrainGenerator(int seed)
        {
            Seed = seed;
        }

        /// <summary>The seed the ground comes from.</summary>
        [LocalSafe]
        public int Seed { get; }

        /// <summary>How far in from the area's edge the heights fade to the ground as it was, so an area inside a hand-made map has no seam; 0 for no fade, for areas that meet each other.</summary>
        public float EdgeFade { get; set; } = 512f;

        /// <summary>The tile painted along roads, one of <c>Tile</c>'s; 0 (as it starts) leaves the tiles the rules paint.</summary>
        public int RoadTile { get; set; }

        /// <summary>
        /// How far biomes blend at their borders, in the game's units: past the edge of a <c>Use</c>'s or <c>Mix</c>'s
        /// area into what lies under it, and across the borders of a <c>Mix</c>'s patches. Heights blend smoothly over it,
        /// and tiles, trees and blocking come from one biome or the other in ragged patches. 0 gives sharp borders.
        /// </summary>
        public float BiomeBlend { get; set; } = 512f;

        /// <summary>
        /// Puts a biome in an area: its hills, tiles, trees and blocking there, blending over <c>BiomeBlend</c> past the
        /// area's edge into what lies under it. Later <c>Use</c> and <c>Mix</c> calls lie over earlier ones, and the
        /// generator's own settings and rules are the ground outside every biome. A plan reads the biome as it is when the
        /// plan starts, so changing it later changes later plans. Biome borders come from the world, so plans of one
        /// generator agree on a border across their edge. A null biome throws ArgumentException.
        /// </summary>
        public void Use(Biome biome, Bounds area)
        {
            Mix(area, 1f, biome);
        }

        /// <summary>
        /// Puts several biomes in an area, in patches about <paramref name="size"/> across (in the game's units) picked
        /// from the seed and blended over <c>BiomeBlend</c> at their borders; read as <c>Use</c>'s otherwise. A biome
        /// listed twice covers about twice the ground. A size of 0 or less, no biomes or a null one throws ArgumentException.
        /// </summary>
        public void Mix(Bounds area, float size, params Biome[] biomes)
        {
            var refused = !(size > 0f) || biomes == null || biomes.Length == 0;
            for (var k = 0; !refused && k < biomes!.Length; k++)
            {
                refused = biomes[k] == null;
            }

            if (refused)
            {
                throw new ArgumentException("TerrainGenerator.Use and Mix take biomes, none null, and a size above 0, not " + size);
            }

            spots.Add(new Spot(area, (Biome[])biomes!.Clone(), size, Seed + 32452843 * (spots.Count + 1)));
        }

        /// <summary>
        /// A level place at a point (the game's coordinates), for a base, a hub or a spawn: the ground within the radius is
        /// level at the ground as it was, never raised, so buildings and items made there stand on it, and blends back to
        /// the hills over <paramref name="blend"/> past it. It is never blocked and has no trees. A radius of 0 or less
        /// throws ArgumentException.
        /// </summary>
        public Place Level(float x, float y, float radius, float blend = 256f)
        {
            CheckRadius(radius);
            var place = new Place("", x, y, radius, blend > 0f ? blend : 0f, null);
            places.Add(place);
            return place;
        }

        /// <summary>
        /// Level places picked in an area from the seed, for camps, shops, shrines or entrances: up to
        /// <paramref name="count"/> spots, each at least <paramref name="spacing"/> from every other place and never
        /// overlapping one, the whole place inside the area. Fewer come back when no more fit. With a place to spread them
        /// <paramref name="from"/>, the nearest share are difficulty 1 and the farthest <paramref name="difficulties"/>, in
        /// even shares; groups added to the list are made at them when their plan is made, owned by
        /// <paramref name="owner"/> (Neutral Hostile unless set). The same seed and calls pick the same spots. A radius of 0
        /// or less, a count below 0 or fewer than 1 difficulty throws ArgumentException.
        /// </summary>
        public PlaceList Places(string name, Bounds area, int count, float radius = 256f, float spacing = 0f, Place? from = null, int difficulties = 1, Player? owner = null, float blend = 256f)
        {
            CheckRadius(radius);
            if (count < 0 || difficulties < 1)
            {
                throw new ArgumentException("TerrainGenerator.Places takes a count of 0 or more and 1 difficulty or more, not " + count + " and " + difficulties);
            }

            blend = blend > 0f ? blend : 0f;
            var list = new PlaceList(PlaceSeed(), difficulties, owner);
            var margin = radius + blend;
            var spanX = area.Size.X - 2f * margin;
            var spanY = area.Size.Y - 2f * margin;
            for (var k = 0; k < count * PlaceTries && list.Count < count && spanX >= 0f && spanY >= 0f; k++)
            {
                var x = area.Min.X + margin + Noise.Hash(list.Seed, k, 0) * spanX;
                var y = area.Min.Y + margin + Noise.Hash(list.Seed, k, 1) * spanY;
                if (IsClear(x, y, radius, blend, spacing))
                {
                    var place = new Place(name, x, y, radius, blend, list);
                    list.Take(place);
                    places.Add(place);
                }
            }

            if (from != null && difficulties > 1)
            {
                var order = new List<int>();
                var distances = new float[list.Count];
                for (var k = 0; k < list.Count; k++)
                {
                    order.Add(k);
                    var dx = list[k].X - from.X;
                    var dy = list[k].Y - from.Y;
                    distances[k] = dx * dx + dy * dy;
                }

                order.Sort((a, b) => distances[a] != distances[b] ? distances[a].CompareTo(distances[b]) : a.CompareTo(b));
                for (var rank = 0; rank < order.Count; rank++)
                {
                    list[order[rank]].Difficulty = 1 + rank * difficulties / order.Count;
                }
            }

            return list;
        }

        /// <summary>
        /// A level place on one side of an area, where it joins the next: its centre on the side, at a spot along it picked
        /// from the seed, kept a radius and blend from the corners. Roads join it as any place; the map puts what leads on
        /// (a gate, a way to the next area) there. A radius of 0 or less throws ArgumentException.
        /// </summary>
        public Place Exit(string name, Bounds area, Side side, float radius = 384f, float blend = 256f)
        {
            CheckRadius(radius);
            blend = blend > 0f ? blend : 0f;
            var upright = side == Side.Left || side == Side.Right;
            var min = upright ? area.Min.Y : area.Min.X;
            var max = upright ? area.Max.Y : area.Max.X;
            var margin = Math.Min(radius + blend, (max - min) / 2f);
            var along = min + margin + Noise.Hash(PlaceSeed(), 0, 0) * (max - min - 2f * margin);
            var x = side == Side.Left ? area.Min.X : side == Side.Right ? area.Max.X : along;
            var y = side == Side.Bottom ? area.Min.Y : side == Side.Top ? area.Max.Y : along;
            var place = new Place(name, x, y, radius, blend, null);
            places.Add(place);
            return place;
        }

        /// <summary>
        /// A road from one place to another, through the points given in order: a line bent from the seed, cut and filled
        /// to an easy slope along it, painted with <c>RoadTile</c>, and clear of trees and blocked corners, wherever a plan of
        /// this generator holds it. Several roads between the same places are several lanes. The same seed and calls give the
        /// same road. One place for both ends, or a width of 0 or less, throws ArgumentException.
        /// </summary>
        public Road Road(Place from, Place to, float width = 256f, params Vector2[] through)
        {
            if (from == null || to == null || from == to || !(width > 0f))
            {
                throw new ArgumentException("TerrainGenerator.Road takes two places and a width above 0");
            }

            var seed = Seed + 1299709 * (roads.Count + 1);
            var stops = new List<Vector2> { new Vector2(from.X, from.Y) };
            if (through != null)
            {
                stops.AddRange(through);
            }

            stops.Add(new Vector2(to.X, to.Y));
            var points = new List<Vector2> { stops[0] };
            for (var leg = 1; leg < stops.Count; leg++)
            {
                var a = stops[leg - 1];
                var b = stops[leg];
                var length = a.DistanceTo(b);
                if (!(length > 0f))
                {
                    continue;
                }

                var steps = Math.Max(1, (int)(length / RoadSpacing + 0.5f));
                var side = new Vector2(a.Y - b.Y, b.X - a.X) / length;
                var bend = Math.Min(length * RoadBend, RoadBendMost);
                for (var k = 1; k <= steps; k++)
                {
                    var t = k / (float)steps;
                    var off = k < steps ? bend * (2f * Noise.At(seed, t * length / RoadWave, leg) - 1f) * Angles.Sin(180f * t) : 0f;
                    points.Add(k < steps ? Vector2.Lerp(a, b, t) + side * off : b);
                }
            }

            var road = new Road(from, to, width, points);
            roads.Add(road);
            return road;
        }

        /// <summary>
        /// Adds roads until every place is reached from every other along roads: the shortest straight gap between places not
        /// yet joined first, each a road 256 wide. Gives the roads it added, none when every place is joined.
        /// </summary>
        public IReadOnlyList<Road> Join()
        {
            var count = places.Count;
            var group = new int[count];
            for (var k = 0; k < count; k++)
            {
                group[k] = k;
            }

            foreach (var road in roads)
            {
                var a = places.IndexOf(road.From);
                var b = places.IndexOf(road.To);
                if (a >= 0 && b >= 0)
                {
                    group[Root(group, a)] = Root(group, b);
                }
            }

            var froms = new List<int>();
            var tos = new List<int>();
            var gaps = new List<float>();
            var order = new List<int>();
            for (var a = 0; a < count; a++)
            {
                for (var b = a + 1; b < count; b++)
                {
                    var dx = places[a].X - places[b].X;
                    var dy = places[a].Y - places[b].Y;
                    order.Add(order.Count);
                    froms.Add(a);
                    tos.Add(b);
                    gaps.Add(dx * dx + dy * dy);
                }
            }

            order.Sort((p, q) => gaps[p] != gaps[q] ? gaps[p].CompareTo(gaps[q]) : p.CompareTo(q));
            var added = new List<Road>();
            foreach (var pair in order)
            {
                var a = Root(group, froms[pair]);
                var b = Root(group, tos[pair]);
                if (a != b)
                {
                    group[a] = b;
                    added.Add(Road(places[froms[pair]], places[tos[pair]]));
                }
            }

            return added;
        }

        /// <summary>
        /// The shortest way from one place to another along the roads so far, as a route through every road's points; the
        /// route is the start alone, with <c>Reaches</c> false, when no roads join them.
        /// </summary>
        [LocalSafe]
        public Route Route(Place from, Place to)
        {
            var start = new List<Vector2> { new Vector2(from.X, from.Y) };
            var a = places.IndexOf(from);
            var b = places.IndexOf(to);
            if (a < 0 || b < 0)
            {
                return new Route(start, false);
            }

            if (a == b)
            {
                return new Route(start, true);
            }

            var count = places.Count;
            var best = new float[count];
            var via = new Road?[count];
            var done = new bool[count];
            for (var k = 0; k < count; k++)
            {
                best[k] = float.MaxValue;
            }

            best[a] = 0f;
            while (true)
            {
                var at = -1;
                for (var k = 0; k < count; k++)
                {
                    if (!done[k] && best[k] < float.MaxValue && (at < 0 || best[k] < best[at]))
                    {
                        at = k;
                    }
                }

                if (at < 0 || at == b)
                {
                    break;
                }

                done[at] = true;
                foreach (var road in roads)
                {
                    var other = road.From == places[at] ? road.To : road.To == places[at] ? road.From : null;
                    var next = other == null ? -1 : places.IndexOf(other);
                    if (next >= 0 && best[at] + road.Length < best[next])
                    {
                        best[next] = best[at] + road.Length;
                        via[next] = road;
                    }
                }
            }

            if (via[b] == null)
            {
                return new Route(start, false);
            }

            var chain = new List<Road>();
            for (var here = to; here != from;)
            {
                var road = via[places.IndexOf(here)]!;
                chain.Add(road);
                here = road.From == here ? road.To : road.From;
            }

            chain.Reverse();
            var points = start;
            var on = from;
            foreach (var road in chain)
            {
                var forward = road.From == on;
                for (var k = 1; k < road.Points.Count; k++)
                {
                    points.Add(road.Points[forward ? k : road.Points.Count - 1 - k]);
                }

                on = forward ? road.To : road.From;
            }

            return new Route(points, true);
        }

        /// <summary>
        /// Works out the ground for an area: a corner every 128 from its lower left. It reads only the settings, so it
        /// changes nothing and gives the same plan each time. The ground comes from the corners' places in the world, so
        /// plans of one generator meet where their areas touch when <c>EdgeFade</c> is 0, and an area's ground depends on
        /// where it is. A <c>FeatureSize</c> of 0 or less, or fewer than 1 layer, throws ArgumentException.
        /// </summary>
        [LocalSafe]
        public TerrainPlan Plan(Bounds area)
        {
            var ground = new Groundwork(this, area, null);
            ground.Roads(0, ground.RoadCount);
            ground.Hills(0, ground.Cells);
            ground.Levels(0, ground.Places);
            ground.Fade(0, ground.Cells);
            ground.Heights(0, ground.Corners);
            ground.Rules(0, ground.Corners);
            return ground.Plan;
        }

        /// <summary>
        /// Makes the ground for an area in steps over frames, for as long as the scope holds it: the same ground as
        /// <c>Plan(area).Apply(scope)</c>, from the settings as they are now. Each step does about
        /// <paramref name="msPerStep"/> of work, a fixed amount counted in corners, so every machine makes the same corners at
        /// the same moment; a step comes about every frame, the first one after this call. The <c>TerrainMaking</c> it gives
        /// tells how far it is, for a bar. Throws as <c>Plan</c> does, and for <paramref name="msPerStep"/> of 0 or less.
        /// </summary>
        [ChangesState]
        public TerrainMaking Make(Bounds area, Scope scope, float msPerStep = 4f)
        {
            if (!(msPerStep > 0f))
            {
                throw new ArgumentException("TerrainGenerator.Make takes milliseconds a step above 0, not " + msPerStep);
            }

            var ground = new Groundwork(this, area, scope);
            var work = new Work();
            if (ground.RoadCount > 0)
            {
                work.Add(RoadsStep, ground.RoadCount, ground.RoadsCost / ground.RoadCount, ground.Roads, ground.RoadCostOf);
            }

            work.Add(HeightsStep, ground.Cells, ground.HillCost, ground.Hills);
            work.Add(HeightsStep, ground.Places, LevelCost * ground.PlaceCorners, ground.Levels);
            if (EdgeFade > 0f)
            {
                work.Add(HeightsStep, ground.Cells, FadeCost, ground.Fade);
            }

            work.Add(HeightsStep, ground.Corners, RaiseCost, ground.Heights);
            work.Add(TilesStep, ground.Corners, ground.RulesCost, ground.Rules);
            if (ground.AnyBlocks)
            {
                // shortcut: a guess of one blocked corner in eight until the rules have run, when the bar's speed changes to fit.
                work.Add(BlockedStep, ground.Corners, ScanCost + BlockCost / 8f, ground.Blocks, ground.BlockCostOf, ground.BlocksCost);
            }

            if (ground.AnyTrees)
            {
                // shortcut: a guess of one tree in eight corners until the rules have run, as for blocking.
                work.Add(TreesStep, ground.Corners, ScanCost + TreeCost / 8f, ground.Trees, ground.TreeCostOf, ground.TreesCost);
            }

            if (ground.PlacesCost > ScanCost * ground.Plan.Places.Count)
            {
                work.Add(PlacesStep, ground.Plan.Places.Count, ground.PlacesCost / ground.Plan.Places.Count, ground.MakePlaces, ground.PlaceCostOf);
            }

            return new TerrainMaking(work, msPerStep * 1000f, scope);
        }

        private static void CheckRadius(float radius)
        {
            if (!(radius > 0f))
            {
                throw new ArgumentException("TerrainGenerator's places take a radius above 0, not " + radius);
            }
        }

        /// <summary>The root of a place's group of joined places.</summary>
        private static int Root(int[] group, int place)
        {
            while (group[place] != place)
            {
                place = group[place];
            }

            return place;
        }

        /// <summary>A seed for the next Places or Exit call, apart from the other rules' seeds.</summary>
        private int PlaceSeed()
        {
            placeCalls++;
            return Seed + 15485863 * placeCalls;
        }

        /// <summary>Whether a place there would keep its spacing from every place so far, and its level ground off theirs.</summary>
        private bool IsClear(float x, float y, float radius, float blend, float spacing)
        {
            foreach (var place in places)
            {
                var need = Math.Max(spacing, radius + place.Radius + Math.Max(blend, place.Blend));
                var dx = place.X - x;
                var dy = place.Y - y;
                if (dx * dx + dy * dy < need * need)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The kinds in a set of pathing.</summary>
        private static int Kinds(Pathing kinds)
        {
            var count = 0;
            for (var bits = (int)kinds; bits != 0; bits &= bits - 1)
            {
                count++;
            }

            return count;
        }

        /// <summary>Levels the corners within a radius of a point (from the first corner of the cells) at a height, blends the ones in the rim past it but not those on a road, which keep the road's easy slope, and marks the level ones.</summary>
        private static void LevelPlace(float[] cells, bool[] level, float[] roadGap, int columns, int rows, float x, float y, float radius, float blend, float target)
        {
            var reach = radius + blend;
            var lastColumn = Math.Min(Noise.Floor((x + reach) / TileSize) + 1, columns - 1);
            var lastRow = Math.Min(Noise.Floor((y + reach) / TileSize) + 1, rows - 1);
            for (var row = Math.Max(Noise.Floor((y - reach) / TileSize), 0); row <= lastRow; row++)
            {
                for (var column = Math.Max(Noise.Floor((x - reach) / TileSize), 0); column <= lastColumn; column++)
                {
                    var dx = column * TileSize - x;
                    var dy = row * TileSize - y;
                    var distance = Maths.Sqrt(dx * dx + dy * dy);
                    var at = row * columns + column;
                    if (distance <= radius)
                    {
                        cells[at] = target;
                        level[at] = true;
                    }
                    else if (distance < reach && roadGap[at] > 0f)
                    {
                        var t = (distance - radius) / blend;
                        cells[at] = target + (cells[at] - target) * (t * t * (3f - 2f * t));
                    }
                }
            }
        }

        /// <summary>
        /// One area's ground being worked out, and made when a scope is given, in passes over ranges of units, so Plan runs
        /// each whole and Make a step at a time. It takes the generator's settings as they are when it starts.
        /// </summary>
        private sealed class Groundwork
        {
            internal readonly TerrainPlan Plan;

            private readonly Scope? scope;
            private readonly int seed;
            private readonly float fade;
            private readonly float blend;

            /// <summary>The biomes as the plan takes them: the generator's own first, then each one that Use and Mix name, once.</summary>
            private readonly Land[] lands;

            /// <summary>The Use and Mix calls, and each one's biomes as lands.</summary>
            private readonly Spot[] spots;

            private readonly int[][] spotLands;

            /// <summary>Each land's share of the point weighed last, and the scores of a Mix's biomes there.</summary>
            private readonly float[] weights;

            private readonly float[] scores;

            /// <summary>The land each cell takes its rules from.</summary>
            private readonly int[] landOf;
            private readonly Place[] places;
            private readonly int roadTile;

            /// <summary>The roads that come near the cells.</summary>
            private readonly Road[] roads;

            /// <summary>What working each road out costs.</summary>
            private readonly float[] roadCosts;
            private readonly int firstColumn;
            private readonly int firstRow;
            private readonly int columns;
            private readonly int rows;

            /// <summary>The cells across: the area's corners and one more on each side, so a slope at the edge is read as the neighbouring plan reads it.</summary>
            private readonly int wide;

            private readonly float[] cells;
            private readonly bool[] level;

            /// <summary>How far each cell is past the edge of the nearest road (below 0 on it), and that road's height there.</summary>
            private readonly float[] roadGap;

            private readonly float[] roadHeight;

            /// <summary>The kinds of pathing the rules have taken so far, over every corner.</summary>
            private int kindsBlocked;

            /// <summary>The trees the rules have stood so far.</summary>
            private int treesStood;

            internal Groundwork(TerrainGenerator generator, Bounds area, Scope? scope)
            {
                this.scope = scope;
                seed = generator.Seed;
                fade = generator.EdgeFade / TileSize;
                blend = generator.BiomeBlend > 0f ? generator.BiomeBlend : 0f;
                var biomes = new List<Biome> { generator };
                spots = generator.spots.ToArray();
                spotLands = new int[spots.Length][];
                var most = 1;
                for (var k = 0; k < spots.Length; k++)
                {
                    var named = spots[k].Biomes;
                    spotLands[k] = new int[named.Length];
                    for (var i = 0; i < named.Length; i++)
                    {
                        var at = biomes.IndexOf(named[i]);
                        if (at < 0)
                        {
                            at = biomes.Count;
                            biomes.Add(named[i]);
                        }

                        spotLands[k][i] = at;
                    }

                    most = Math.Max(most, named.Length);
                }

                lands = new Land[biomes.Count];
                for (var k = 0; k < lands.Length; k++)
                {
                    lands[k] = new Land(biomes[k], seed);
                }

                weights = new float[lands.Length];
                scores = new float[most];
                places = generator.places.ToArray();
                roadTile = generator.RoadTile;
                var size = area.Size;
                columns = (int)(size.X / TileSize + 0.5f) + 1;
                rows = (int)(size.Y / TileSize + 0.5f) + 1;
                Plan = new TerrainPlan(area.Min.X, area.Min.Y, columns, rows);
                firstColumn = Noise.Floor(area.Min.X / TileSize + 0.5f);
                firstRow = Noise.Floor(area.Min.Y / TileSize + 0.5f);
                wide = columns + 2;
                cells = new float[wide * (rows + 2)];
                level = new bool[cells.Length];
                roadGap = new float[cells.Length];
                roadHeight = new float[cells.Length];
                landOf = new int[cells.Length];
                for (var at = 0; at < cells.Length; at++)
                {
                    roadGap[at] = float.MaxValue;
                }

                // A cell reads the lands of the calls that reach the area; a road's points read the ground anywhere along it.
                var reaches = new bool[lands.Length];
                reaches[0] = true;
                var pointCost = 0f;
                for (var k = 0; k < spots.Length; k++)
                {
                    var mixCost = spotLands[k].Length > 1 ? 2f * NoiseCost * spotLands[k].Length : 0f;
                    pointCost += mixCost;
                    if (spots[k].Reaches(area, blend + TileSize))
                    {
                        HillCost += mixCost;
                        foreach (var land in spotLands[k])
                        {
                            reaches[land] = true;
                        }
                    }
                }

                for (var k = 0; k < lands.Length; k++)
                {
                    pointCost += NoiseCost * lands[k].Layers;
                    if (reaches[k])
                    {
                        HillCost += NoiseCost * lands[k].Layers;
                        RulesCost = Math.Max(RulesCost, lands[k].RulesCost);
                        AnyBlocks = AnyBlocks || lands[k].Blocks.Length > 0;
                        AnyTrees = AnyTrees || lands[k].Trees.Length > 0;
                    }
                }

                var near = new List<Road>();
                var costs = new List<float>();
                foreach (var road in generator.roads)
                {
                    var cost = 0f;
                    var points = road.Points;
                    for (var k = 1; k < points.Count; k++)
                    {
                        cost += StampCost * Reach(road, points[k - 1], points[k], out _, out _, out _, out _);
                    }

                    if (cost > 0f)
                    {
                        near.Add(road);
                        costs.Add(cost + points.Count * (pointCost + PlaceCost * places.Length));
                    }
                }

                roads = near.ToArray();
                roadCosts = costs.ToArray();
                foreach (var place in places)
                {
                    if (place.Column >= firstColumn && place.Column < firstColumn + columns && place.Row >= firstRow && place.Row < firstRow + rows)
                    {
                        Plan.Hold(place);
                    }
                }
            }

            /// <summary>The cells: the area's corners and one more on every side.</summary>
            internal int Cells => cells.Length;

            /// <summary>What a cell's hills cost, with every biome that reaches the area.</summary>
            internal float HillCost { get; }

            /// <summary>What a corner's rules cost, by the biome that reaches the area with the most.</summary>
            internal float RulesCost { get; }

            /// <summary>Whether a biome that reaches the area blocks, or stands trees.</summary>
            internal bool AnyBlocks { get; }

            internal bool AnyTrees { get; }

            /// <summary>The area's corners.</summary>
            internal int Corners => columns * rows;

            /// <summary>The level places.</summary>
            internal int Places => places.Length;

            /// <summary>The roads that come near the area.</summary>
            internal int RoadCount => roads.Length;

            /// <summary>What working out every road near the area costs.</summary>
            internal float RoadsCost
            {
                get
                {
                    var sum = 0f;
                    foreach (var cost in roadCosts)
                    {
                        sum += cost;
                    }

                    return sum;
                }
            }

            /// <summary>What making the units and buildings at every place the plan holds costs.</summary>
            internal float PlacesCost
            {
                get
                {
                    var sum = 0f;
                    for (var k = 0; k < Plan.Places.Count; k++)
                    {
                        sum += PlaceCostOf(k);
                    }

                    return sum;
                }
            }

            /// <summary>The most corners a level place reaches.</summary>
            internal float PlaceCorners
            {
                get
                {
                    var most = 0f;
                    for (var k = 0; k < places.Length; k++)
                    {
                        var across = 2f * (places[k].Radius + places[k].Blend) / TileSize + 3f;
                        most = Math.Max(most, across * across);
                    }

                    return most;
                }
            }

            /// <summary>The hills of the cells in a range, from the noise, cut and filled to the roads' heights along them.</summary>
            internal void Hills(int from, int to)
            {
                for (var at = from; at < to; at++)
                {
                    var column = firstColumn - 1 + at % wide;
                    var row = firstRow - 1 + at / wide;
                    var height = Height(column, row);
                    landOf[at] = lands.Length > 1 ? Pick(column, row) : 0;
                    var gap = roadGap[at];
                    if (gap < RoadBlend)
                    {
                        var t = gap > 0f ? gap / RoadBlend : 0f;
                        height = roadHeight[at] + (height - roadHeight[at]) * (t * t * (3f - 2f * t));
                    }

                    cells[at] = height;
                }
            }

            /// <summary>Works out the roads in a range, each's heights along it and the cells near it.</summary>
            internal void Roads(int from, int to)
            {
                for (var k = from; k < to; k++)
                {
                    Stamp(roads[k], Profile(roads[k]));
                }
            }

            /// <summary>What working out a road costs.</summary>
            internal float RoadCostOf(int k)
            {
                return roadCosts[k];
            }

            /// <summary>Makes the units and buildings of the places in a range.</summary>
            internal void MakePlaces(int from, int to)
            {
                Plan.MakePlaces(from, to, scope!);
            }

            /// <summary>What making a place's units and buildings costs.</summary>
            internal float PlaceCostOf(int k)
            {
                return ScanCost + UnitCost * Plan.Places[k].UnitsToMake();
            }

            /// <summary>Levels the places in a range, in order.</summary>
            internal void Levels(int from, int to)
            {
                for (var k = from; k < to; k++)
                {
                    var place = places[k];
                    LevelPlace(cells, level, roadGap, wide, rows + 2, place.X - (firstColumn - 1) * TileSize, place.Y - (firstRow - 1) * TileSize, place.Radius, place.Blend, 0f);
                }
            }

            /// <summary>
            /// Fades the heights of the cells in a range to the ground as it was at the area's edge, over the fade's corners in, as
            /// <c>Field.FadeEdges</c> does; the cells outside the area become the ground as it was. No fade does nothing.
            /// </summary>
            internal void Fade(int from, int to)
            {
                if (!(fade > 0f))
                {
                    return;
                }

                for (var at = from; at < to; at++)
                {
                    var row = at / wide - 1;
                    var column = at % wide - 1;
                    var edge = Math.Min(Math.Min(column, columns - 1 - column), Math.Min(row, rows - 1 - row));
                    if (edge < fade)
                    {
                        var t = edge > 0 ? edge / fade : 0f;
                        cells[at] *= t * t * (3f - 2f * t);
                    }
                }
            }

            /// <summary>Puts the heights of the corners in a range in the plan, and raises them when making.</summary>
            internal void Heights(int from, int to)
            {
                for (var at = from; at < to; at++)
                {
                    Plan.Heights.Cells[at] = cells[Cell(at)];
                }

                if (scope != null)
                {
                    InRows(from, to, RaisePiece);
                }
            }

            /// <summary>Works out the tile and the blocked pathing of the corners in a range by the rules, and paints them when making.</summary>
            internal void Rules(int from, int to)
            {
                for (var at = from; at < to; at++)
                {
                    var cell = Cell(at);
                    var land = lands[landOf[cell]];
                    var paints = land.Paints;
                    var blocks = land.Blocks;
                    var trees = land.Trees;
                    var span = land.Highest - land.Lowest;
                    var alongX = (cells[cell + 1] - cells[cell - 1]) / (2f * TileSize);
                    var alongY = (cells[cell + wide] - cells[cell - wide]) / (2f * TileSize);
                    var steep = alongX * alongX + alongY * alongY;
                    var height = span != 0f ? Maths.Clamp01((cells[cell] - land.Lowest) / span) : 0f;
                    for (var k = 0; k < paints.Length; k++)
                    {
                        var rule = paints[k];
                        if (rule.Fits(height, steep) && (rule.Cover >= 1f || Noise.Layered(rule.Seed, (firstColumn + at % columns) / PatchSize, (firstRow + at / columns) / PatchSize, 2, 0.5f) < rule.Cover))
                        {
                            Plan.Tiles[at] = rule.Value;
                        }
                    }

                    if (roadTile != 0 && roadGap[cell] <= 0f)
                    {
                        Plan.Tiles[at] = roadTile;
                    }

                    if (level[cell] || roadGap[cell] < RoadUnblocked)
                    {
                        continue;
                    }

                    var kinds = 0;
                    for (var k = 0; k < blocks.Length; k++)
                    {
                        if (blocks[k].Fits(height, steep))
                        {
                            kinds |= blocks[k].Value;
                        }
                    }

                    Plan.Blocked[at] = (Pathing)kinds;
                    kindsBlocked += Kinds((Pathing)kinds);
                    if (roadGap[cell] < RoadNoTrees)
                    {
                        continue;
                    }

                    var column = firstColumn + at % columns;
                    var row = firstRow + at / columns;
                    for (var k = trees.Length - 1; k >= 0; k--)
                    {
                        var rule = trees[k];
                        if (rule.Ranges.Fits(height, steep) && (rule.Ranges.Value == 0 || rule.Ranges.Value == Plan.Tiles[at]) && rule.Stands(column, row))
                        {
                            Plan.Trees[at] = rule.Type;
                            Plan.TreeVariations[at] = rule.Variation(column, row);
                            treesStood++;
                            break;
                        }
                    }
                }

                if (scope != null)
                {
                    InRows(from, to, PaintPiece);
                }
            }

            /// <summary>Blocks the corners in a range.</summary>
            internal void Blocks(int from, int to)
            {
                InRows(from, to, BlockPiece);
            }

            /// <summary>Makes the trees of the corners in a range.</summary>
            internal void Trees(int from, int to)
            {
                Plan.MakeTrees(from, to, scope!);
            }

            /// <summary>What making a corner's tree costs.</summary>
            internal float TreeCostOf(int at)
            {
                return ScanCost + (Plan.Trees[at] != 0 ? TreeCost : 0f);
            }

            /// <summary>What making every tree costs, once the rules have run.</summary>
            internal float TreesCost()
            {
                return ScanCost * Corners + TreeCost * treesStood;
            }

            /// <summary>What blocking a corner costs.</summary>
            internal float BlockCostOf(int at)
            {
                return ScanCost + BlockCost * Kinds(Plan.Blocked[at]);
            }

            /// <summary>What blocking every corner costs, once the rules have run.</summary>
            internal float BlocksCost()
            {
                return ScanCost * Corners + BlockCost * kindsBlocked;
            }

            /// <summary>The cell of a corner of the area.</summary>
            private int Cell(int at)
            {
                return (at / columns + 1) * wide + at % columns + 1;
            }

            /// <summary>The ground at a point of the world before roads: the hills, and the places levelled.</summary>
            private float GroundAt(float x, float y)
            {
                var height = Height(x / TileSize, y / TileSize);
                foreach (var place in places)
                {
                    var dx = x - place.X;
                    var dy = y - place.Y;
                    var distance = Maths.Sqrt(dx * dx + dy * dy);
                    if (distance <= place.Radius)
                    {
                        height = 0f;
                    }
                    else if (distance < place.Radius + place.Blend)
                    {
                        var t = (distance - place.Radius) / place.Blend;
                        height *= t * t * (3f - 2f * t);
                    }
                }

                return height;
            }

            /// <summary>
            /// A road's height at each of its points: the ground there, averaged over a few points each side, kept within an easy
            /// slope of every place's level edge, so it comes down to the places it meets and passes, then kept to an easy slope
            /// from point to point, forward and back.
            /// </summary>
            private float[] Profile(Road road)
            {
                var points = road.Points;
                var count = points.Count;
                var ground = new float[count];
                for (var k = 0; k < count; k++)
                {
                    ground[k] = GroundAt(points[k].X, points[k].Y);
                }

                var heights = new float[count];
                for (var k = 0; k < count; k++)
                {
                    var sum = 0f;
                    var first = Math.Max(k - RoadSmooth, 0);
                    var last = Math.Min(k + RoadSmooth, count - 1);
                    for (var j = first; j <= last; j++)
                    {
                        sum += ground[j];
                    }

                    var cap = float.MaxValue;
                    foreach (var place in places)
                    {
                        var dx = points[k].X - place.X;
                        var dy = points[k].Y - place.Y;
                        cap = Math.Min(cap, RoadSlope * Math.Max(Maths.Sqrt(dx * dx + dy * dy) - place.Radius, 0f));
                    }

                    heights[k] = Maths.Clamp(sum / (last - first + 1), -cap, cap);
                }

                for (var k = 1; k < count; k++)
                {
                    var rise = RoadSlope * points[k - 1].DistanceTo(points[k]);
                    heights[k] = Maths.Clamp(heights[k], heights[k - 1] - rise, heights[k - 1] + rise);
                }

                for (var k = count - 2; k >= 0; k--)
                {
                    var rise = RoadSlope * points[k].DistanceTo(points[k + 1]);
                    heights[k] = Maths.Clamp(heights[k], heights[k + 1] - rise, heights[k + 1] + rise);
                }

                return heights;
            }

            /// <summary>Marks the cells near a road with how far past its edge they are and its height there, where it is the nearest road so far.</summary>
            private void Stamp(Road road, float[] heights)
            {
                var half = road.Width / 2f;
                var points = road.Points;
                for (var k = 1; k < points.Count; k++)
                {
                    var a = points[k - 1];
                    var b = points[k];
                    if (Reach(road, a, b, out var fromColumn, out var toColumn, out var fromRow, out var toRow) == 0)
                    {
                        continue;
                    }

                    var alongX = b.X - a.X;
                    var alongY = b.Y - a.Y;
                    var lengthSquared = alongX * alongX + alongY * alongY;
                    for (var row = fromRow; row <= toRow; row++)
                    {
                        for (var column = fromColumn; column <= toColumn; column++)
                        {
                            var x = (firstColumn - 1 + column) * TileSize;
                            var y = (firstRow - 1 + row) * TileSize;
                            var t = lengthSquared > 0f ? Maths.Clamp01(((x - a.X) * alongX + (y - a.Y) * alongY) / lengthSquared) : 0f;
                            var dx = x - (a.X + alongX * t);
                            var dy = y - (a.Y + alongY * t);
                            var gap = Maths.Sqrt(dx * dx + dy * dy) - half;
                            var at = row * wide + column;
                            if (gap < roadGap[at])
                            {
                                roadGap[at] = gap;
                                roadHeight[at] = heights[k - 1] + (heights[k] - heights[k - 1]) * t;
                            }
                        }
                    }
                }
            }

            /// <summary>The cells a stretch of road reaches (its half width and the blend past it), clipped to the cells: their first and last column and row, and how many.</summary>
            private int Reach(Road road, Vector2 a, Vector2 b, out int fromColumn, out int toColumn, out int fromRow, out int toRow)
            {
                var reach = road.Width / 2f + RoadBlend;
                fromColumn = Math.Max(Noise.Floor((Math.Min(a.X, b.X) - reach) / TileSize) - firstColumn + 1, 0);
                toColumn = Math.Min(Noise.Floor((Math.Max(a.X, b.X) + reach) / TileSize) - firstColumn + 2, wide - 1);
                fromRow = Math.Max(Noise.Floor((Math.Min(a.Y, b.Y) - reach) / TileSize) - firstRow + 1, 0);
                toRow = Math.Min(Noise.Floor((Math.Max(a.Y, b.Y) + reach) / TileSize) - firstRow + 2, rows + 1);
                return toColumn >= fromColumn && toRow >= fromRow ? (toColumn - fromColumn + 1) * (toRow - fromRow + 1) : 0;
            }

            /// <summary>
            /// The height of a point of the world, counted in corners from (0, 0): each biome's hills there, by its share of the
            /// point, which it leaves in the weights.
            /// </summary>
            private float Height(float column, float row)
            {
                if (lands.Length == 1)
                {
                    return lands[0].Height(seed, column, row);
                }

                Weigh(column * TileSize, row * TileSize);
                var height = 0f;
                for (var b = 0; b < lands.Length; b++)
                {
                    if (weights[b] > 0f)
                    {
                        height += weights[b] * lands[b].Height(seed, column, row);
                    }
                }

                return height;
            }

            /// <summary>
            /// Each land's share of a point of the world, in the weights: the generator's own under every call, and each Use and
            /// Mix in order over what came before, full inside its area and fading out over the blend past it; a Mix shares its
            /// part between its biomes by their patches' noise, the highest whole and the others fading out over a margin.
            /// </summary>
            private void Weigh(float x, float y)
            {
                weights[0] = 1f;
                for (var b = 1; b < weights.Length; b++)
                {
                    weights[b] = 0f;
                }

                for (var k = 0; k < spots.Length; k++)
                {
                    var spot = spots[k];
                    var share = spot.Share(x, y, blend);
                    if (!(share > 0f))
                    {
                        continue;
                    }

                    for (var b = 0; b < weights.Length; b++)
                    {
                        weights[b] *= 1f - share;
                    }

                    var named = spotLands[k];
                    if (named.Length == 1)
                    {
                        weights[named[0]] += share;
                        continue;
                    }

                    var scale = MixPatch / spot.Size;
                    var top = 0f;
                    for (var i = 0; i < named.Length; i++)
                    {
                        scores[i] = Noise.Layered(spot.Seed + 7919 * i, x * scale, y * scale, 2, 0.5f);
                        top = Math.Max(top, scores[i]);
                    }

                    var margin = MixSlope * blend * scale;
                    var sum = 0f;
                    for (var i = 0; i < named.Length; i++)
                    {
                        var t = margin > 0f ? Maths.Clamp01(1f - (top - scores[i]) / margin) : scores[i] >= top ? 1f : 0f;
                        scores[i] = t * t * (3f - 2f * t);
                        sum += scores[i];
                    }

                    for (var i = 0; i < named.Length; i++)
                    {
                        weights[named[i]] += share * scores[i] / sum;
                    }
                }
            }

            /// <summary>
            /// The land a corner of the world takes its rules from, by the weights Height left: the only one, or where they
            /// blend, one picked by noise against their shares, so the border is ragged and each side keeps its own rules.
            /// </summary>
            private int Pick(int column, int row)
            {
                var pick = -1f;
                var sum = 0f;
                var last = 0;
                for (var b = 0; b < weights.Length; b++)
                {
                    if (!(weights[b] > 0f))
                    {
                        continue;
                    }

                    if (pick < 0f)
                    {
                        if (weights[b] >= 1f)
                        {
                            return b;
                        }

                        pick = Noise.At(seed + 49979687, column / PickSize, row / PickSize);
                    }

                    sum += weights[b];
                    last = b;
                    if (pick < sum)
                    {
                        return b;
                    }
                }

                return last;
            }

            /// <summary>Runs a piece for each row's part of a range of corners, with its first corner and the one past its last.</summary>
            private void InRows(int from, int to, Action<int, int> piece)
            {
                while (from < to)
                {
                    var end = Math.Min(to, (from / columns + 1) * columns);
                    piece(from, end);
                    from = end;
                }
            }

            private void RaisePiece(int from, int to)
            {
                var by = new float[to - from];
                var any = false;
                for (var at = from; at < to; at++)
                {
                    by[at - from] = Plan.Heights.Cells[at];
                    any = any || by[at - from] != 0f;
                }

                if (any)
                {
                    Terrain.Raise(X(from), Y(from), by.Length, by, scope!);
                }
            }

            private void PaintPiece(int from, int to)
            {
                var tiles = new int[to - from];
                var any = false;
                for (var at = from; at < to; at++)
                {
                    tiles[at - from] = Plan.Tiles[at];
                    any = any || tiles[at - from] != 0;
                }

                if (any)
                {
                    Terrain.SetTile(X(from), Y(from), tiles.Length, tiles, scope!);
                }
            }

            private void BlockPiece(int from, int to)
            {
                var kinds = new Pathing[to - from];
                var any = false;
                for (var at = from; at < to; at++)
                {
                    kinds[at - from] = Plan.Blocked[at];
                    any = any || kinds[at - from] != Pathing.None;
                }

                if (any)
                {
                    Terrain.Block(X(from), Y(from), kinds.Length, kinds, scope!);
                }
            }

            /// <summary>The x of a corner of the area.</summary>
            private float X(int at)
            {
                return Plan.X + at % columns * TileSize;
            }

            /// <summary>The y of a corner of the area.</summary>
            private float Y(int at)
            {
                return Plan.Y + at / columns * TileSize;
            }
        }

        /// <summary>A biome as a plan takes it: its hill shape and its rules, ready to read.</summary>
        private sealed class Land
        {
            internal readonly float Lowest;
            internal readonly float Highest;
            internal readonly int Layers;
            internal readonly Fit[] Paints;
            internal readonly Fit[] Blocks;
            internal readonly TreeFit[] Trees;

            /// <summary>What a corner's rules cost.</summary>
            internal readonly float RulesCost;

            private readonly float featureCells;
            private readonly float roughness;

            /// <summary>
            /// How far the noise is stretched about its middle: layers added together stay nearer the middle than one does, so
            /// this brings them back to one layer's spread, whatever the layers and roughness.
            /// </summary>
            private readonly float spread;

            internal Land(Biome biome, int seed)
            {
                if (!(biome.FeatureSize > 0f) || biome.Layers < 1)
                {
                    throw new ArgumentException("TerrainGenerator.Plan takes biomes with a FeatureSize above 0 and 1 layer or more, not " + biome.FeatureSize + " and " + biome.Layers);
                }

                Lowest = biome.Lowest;
                Highest = biome.Highest;
                Layers = biome.Layers;
                featureCells = biome.FeatureSize / TileSize;
                roughness = biome.Roughness;
                var sum = 0f;
                var squares = 0f;
                var strength = 1f;
                for (var layer = 0; layer < Layers; layer++)
                {
                    sum += strength;
                    squares += strength * strength;
                    strength *= roughness;
                }

                spread = sum / Maths.Sqrt(squares);
                Paints = new Fit[biome.PaintRules.Count];
                for (var k = 0; k < Paints.Length; k++)
                {
                    var rule = biome.PaintRules[k];
                    Paints[k] = new Fit(rule.Tile, rule, rule.Cover, seed + 7919 * rule.Number);
                }

                Blocks = new Fit[biome.BlockRules.Count];
                for (var k = 0; k < Blocks.Length; k++)
                {
                    Blocks[k] = new Fit((int)biome.BlockRules[k].Kinds, biome.BlockRules[k], 1f, 0);
                }

                Trees = new TreeFit[biome.TreeRules.Count];
                var treeCost = 0f;
                for (var k = 0; k < Trees.Length; k++)
                {
                    var rule = biome.TreeRules[k];
                    var ranges = new Fit(rule.OnTile, rule, 1f, 0);
                    var treeSeed = seed + 104729 * rule.Number;
                    if (rule is ForestRule forest)
                    {
                        Trees[k] = new TreeFit(TreeFit.Forest, rule.Type, rule.Variations, forest.Cover, forest.Density, 0f, treeSeed, ranges);
                        treeCost += ForestCost;
                    }
                    else if (rule is WallRule wall)
                    {
                        Trees[k] = new TreeFit(TreeFit.Walls, rule.Type, rule.Variations, wall.Gaps, wall.Width * wall.Width / 4f, wall.Spacing / TileSize, treeSeed, ranges);
                        treeCost += WallCost;
                    }
                    else
                    {
                        Trees[k] = new TreeFit(TreeFit.Single, rule.Type, rule.Variations, ((ScatterRule)rule).Chance, 0f, 0f, treeSeed, ranges);
                        treeCost += ScatterCost;
                    }
                }

                RulesCost = RuleCost * (2 + Paints.Length + Blocks.Length + Trees.Length) + PaintCost + treeCost;
            }

            /// <summary>The height of a point of the world, counted in corners from (0, 0), from the noise at its place, between Lowest and Highest.</summary>
            internal float Height(int seed, float column, float row)
            {
                var noise = Noise.Layered(seed, column / featureCells, row / featureCells, Layers, roughness);
                return Lowest + (Highest - Lowest) * Maths.Clamp01(0.5f + (noise - 0.5f) * spread);
            }
        }

        /// <summary>A Use or Mix call: its area, its biomes, the size of their patches and the seed of their noise.</summary>
        private sealed class Spot
        {
            internal readonly Biome[] Biomes;
            internal readonly float Size;
            internal readonly int Seed;
            private readonly Bounds area;

            internal Spot(Bounds area, Biome[] biomes, float size, int seed)
            {
                this.area = area;
                Biomes = biomes;
                Size = size;
                Seed = seed;
            }

            /// <summary>Its share of a point: 1 inside its area, fading to 0 over the blend past its edge.</summary>
            internal float Share(float x, float y, float blend)
            {
                var dx = Math.Max(Math.Max(area.Min.X - x, x - area.Max.X), 0f);
                var dy = Math.Max(Math.Max(area.Min.Y - y, y - area.Max.Y), 0f);
                if (dx == 0f && dy == 0f)
                {
                    return 1f;
                }

                var distance = Maths.Sqrt(dx * dx + dy * dy);
                if (!(distance < blend))
                {
                    return 0f;
                }

                var t = 1f - distance / blend;
                return t * t * (3f - 2f * t);
            }

            /// <summary>Whether it reaches an area grown by a margin.</summary>
            internal bool Reaches(Bounds other, float margin)
            {
                return area.Min.X <= other.Max.X + margin && area.Max.X >= other.Min.X - margin && area.Min.Y <= other.Max.Y + margin && area.Max.Y >= other.Min.Y - margin;
            }
        }

        /// <summary>A tree rule as a plan takes it: its kind and type, where it stands trees, and the heights, slopes and tile it fits.</summary>
        private sealed class TreeFit
        {
            internal const int Forest = 0;
            internal const int Walls = 1;
            internal const int Single = 2;

            internal readonly int Kind;
            internal readonly int Type;

            /// <summary>The heights and slopes it fits, with the tile as its value (0 for any).</summary>
            internal readonly Fit Ranges;

            private readonly int variations;

            /// <summary>A forest's cover, a wall's gaps, or a scatter's chance.</summary>
            private readonly float share;

            /// <summary>A forest's density, or a wall's half width in corners, squared.</summary>
            private readonly float spare;

            /// <summary>A wall's spacing in corners.</summary>
            private readonly float spacing;

            private readonly int seed;

            internal TreeFit(int kind, int type, int variations, float share, float spare, float spacing, int seed, Fit ranges)
            {
                Kind = kind;
                Type = type;
                this.variations = variations;
                this.share = share;
                this.spare = spare;
                this.spacing = spacing;
                this.seed = seed;
                Ranges = ranges;
            }

            /// <summary>Whether the rule stands a tree on a corner of the world, counted in corners from (0, 0).</summary>
            internal bool Stands(int column, int row)
            {
                if (Kind == Single)
                {
                    return Noise.Hash(seed, column, row) < share;
                }

                if (Kind == Forest)
                {
                    return Noise.Layered(seed, column / ForestPatch, row / ForestPatch, 2, 0.5f) < share && Noise.Hash(seed + 1, column, row) < spare;
                }

                // A wall is where the noise crosses its middle: the corners nearer that line than half the width, by the noise's slope there.
                var here = Noise.At(seed, column / spacing, row / spacing);
                var alongX = Noise.At(seed, (column + 1) / spacing, row / spacing) - here;
                var alongY = Noise.At(seed, column / spacing, (row + 1) / spacing) - here;
                var off = here - 0.5f;
                return off * off <= spare * (alongX * alongX + alongY * alongY)
                    && Noise.Hash(seed + 1, Noise.Floor(column / (float)GapSize), Noise.Floor(row / (float)GapSize)) >= share;
            }

            /// <summary>The model a tree on a corner of the world is made with.</summary>
            internal int Variation(int column, int row)
            {
                return Math.Min((int)(Noise.Hash(seed + 2, column, row) * variations), variations - 1);
            }
        }

        /// <summary>A paint or block rule as a plan takes it: what it gives, and the heights and slopes it fits.</summary>
        private sealed class Fit
        {
            /// <summary>The tile, or the kinds of pathing.</summary>
            internal readonly int Value;

            /// <summary>The share of the corners a paint rule covers, 1 for all.</summary>
            internal readonly float Cover;

            /// <summary>The seed of a paint rule's patches.</summary>
            internal readonly int Seed;

            private readonly float fromHeight;
            private readonly float toHeight;

            /// <summary>The least steepness, as rise over run, squared.</summary>
            private readonly float fromSteep;

            /// <summary>The most steepness, as fromSteep; below 0 for no limit.</summary>
            private readonly float toSteep;

            internal Fit(int value, TerrainRule rule, float cover, int seed)
            {
                Value = value;
                fromHeight = rule.FromHeight;
                toHeight = rule.ToHeight;
                fromSteep = Steepness(rule.FromSlope);
                toSteep = rule.ToSlope >= 90f ? -1f : Steepness(rule.ToSlope);
                Cover = cover;
                Seed = seed;
            }

            /// <summary>Whether a corner's height (a share of the range) and steepness (rise over run, squared) are in the rule's ranges.</summary>
            internal bool Fits(float height, float steep)
            {
                return height >= fromHeight && height <= toHeight && steep >= fromSteep && (toSteep < 0f || steep <= toSteep);
            }

            /// <summary>A slope in degrees as rise over run, squared, so a corner's slope is compared with no root or angle.</summary>
            private static float Steepness(float degrees)
            {
                if (!(degrees > 0f))
                {
                    return 0f;
                }

                var rise = Angles.Sin(degrees) / Angles.Cos(degrees);
                return rise * rise;
            }
        }
    }
}
