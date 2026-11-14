using System;
using System.Text;
using Moonwell.Procgen;

namespace Moonwell.Testing
{
    /// <summary>Cases for procgen places and roads (decisions 287 to 290): places picked and levelled, what is made at them, roads on the ground, and routes along them.</summary>
    public static class PlacesAndRoadsCases
    {
        /// <summary>A game component that holds what a case makes, ended by the case.</summary>
        private sealed class Holder : GameComponent
        {
        }

        private static readonly int Kobold = ObjectTypes.FourCC("nkob");
        private static readonly int Ogre = ObjectTypes.FourCC("nogr");
        private static readonly int Merchant = ObjectTypes.FourCC("ngme");
        private static readonly int Tree = ObjectTypes.FourCC("LTlt");

        /// <summary>A world of 81 by 81 corners from (0, 0).</summary>
        private static Bounds World()
        {
            return Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(10240f, 10240f));
        }

        /// <summary>An area of 21 by 21 corners from (0, 0), inside the flat fake terrain.</summary>
        private static Bounds Small()
        {
            return Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(2560f, 2560f));
        }

        /// <summary>Steep hills with no fade, from 0 up, so a place's level is the lowest ground.</summary>
        private static TerrainGenerator Land(int seed)
        {
            return new TerrainGenerator(seed) { Lowest = 0f, Highest = 600f, FeatureSize = 1024f, EdgeFade = 0f };
        }

        private static void Flat()
        {
            var fake = new FakeTerrainWorld();
            var text = new StringBuilder();
            for (var k = 0; k < 24 * 24; k++)
            {
                text.Append(GridText.Corner(0f, 2));
            }

            Terrain.Install(fake, 24, 24, 0f, 0f, text.ToString());
        }

        /// <summary>Places picks spots in the area from the seed, apart and inside it, difficulty by distance in even shares; the same calls pick the same; fewer when none fit; an exit sits on its side.</summary>
        public static void Picked(FakeWorld world)
        {
            var land = Land(3);
            var home = land.Level(1000f, 1000f, 512f);
            var camps = land.Places("Camp", World(), 12, radius: 320f, spacing: 1500f, from: home, difficulties: 3);
            Expect.Equal(12, camps.Count, "twelve camps fit");
            Expect.Equal(3, camps.Difficulties, "spread over three difficulties");
            var inside = 0;
            var apart = 0;
            var ordered = 0;
            var counts = new int[4];
            for (var k = 0; k < camps.Count; k++)
            {
                var camp = camps[k];
                inside += camp.X >= 576f && camp.X <= 10240f - 576f && camp.Y >= 576f && camp.Y <= 10240f - 576f ? 1 : 0;
                counts[camp.Difficulty]++;
                apart += Distance(camp, home) >= 1500f ? 1 : 0;
                for (var j = 0; j < camps.Count; j++)
                {
                    apart += j != k && Distance(camp, camps[j]) < 1500f ? -100 : 0;
                    ordered += Distance(camps[j], home) < Distance(camp, home) && camps[j].Difficulty > camp.Difficulty ? 1 : 0;
                }
            }

            Expect.Equal(12, inside, "each whole place inside the area");
            Expect.Equal(12, apart, "each 1500 from every other place");
            Expect.Equal(0, ordered, "a nearer camp is never harder");
            Expect.True(counts[1] == 4 && counts[2] == 4 && counts[3] == 4, "even shares: " + counts[1] + ", " + counts[2] + ", " + counts[3]);
            Expect.Equal("Camp", camps[0].Name, "named");
            Expect.Equal(1, home.Difficulty, "a place from Level is difficulty 1");

            var again = Land(3);
            var otherHome = again.Level(1000f, 1000f, 512f);
            var same = again.Places("Camp", World(), 12, radius: 320f, spacing: 1500f, from: otherHome, difficulties: 3);
            var other = Land(4).Places("Camp", World(), 12, radius: 320f, spacing: 1500f);
            var differ = 0;
            var moved = 0;
            for (var k = 0; k < 12; k++)
            {
                differ += same[k].X != camps[k].X || same[k].Y != camps[k].Y || same[k].Difficulty != camps[k].Difficulty ? 1 : 0;
                moved += other[k].X != camps[k].X ? 1 : 0;
            }

            Expect.Equal(0, differ, "the same seed and calls pick the same spots");
            Expect.True(moved > 0, "another seed picks others");
            Expect.Equal(0, Land(3).Places("Big", Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(1000f, 1000f)), 5, radius: 400f).Count, "none fit an area too small");

            var gate = land.Exit("North gate", World(), Side.Top);
            Expect.Near(10240f, gate.Y, "an exit at the top is on the top side");
            Expect.True(gate.X >= 640f && gate.X <= 10240f - 640f, "kept a radius and blend from the corners: " + gate.X);
            Expect.Near(0f, land.Exit("West", World(), Side.Left).X, "one on the left side");
        }

        /// <summary>A place is level at the ground as it was, never blocked and treeless; a plan holds the places nearest its corners, one plan of touching plans each.</summary>
        public static void Levelled(FakeWorld world)
        {
            var land = Land(8);
            land.Block(Pathing.Walk);
            land.Forest(Tree, cover: 1f);
            var home = land.Level(1280f, 1280f, 300f);
            var spots = land.Places("Shrine", Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(5120f, 2560f)), 4, radius: 256f, spacing: 700f);
            var left = land.Plan(Small());
            var right = land.Plan(Bounds.FromCorners(new Vector2(2688f, 0f), new Vector2(5248f, 2560f)));
            Expect.Near(0f, left.Heights[10, 10], "level at the ground as it was");
            Expect.Near(0f, left.Heights[12, 10], "out to the radius");
            Expect.True(left.Blocked[10 * 21 + 10] == Pathing.None && left.Trees[10 * 21 + 10] == 0, "never blocked, no tree");
            Expect.True(left.Blocked[0] == Pathing.Walk && left.Trees[0] != 0, "the ground away from it is");
            var held = 0;
            foreach (var spot in spots)
            {
                var inLeft = Holds(left, spot) ? 1 : 0;
                var inRight = Holds(right, spot) ? 1 : 0;
                held += inLeft + inRight;
                var plan = inLeft == 1 ? left : right;
                var column = (int)((spot.X - plan.X) / 128f + 0.5f);
                var row = (int)((spot.Y - plan.Y) / 128f + 0.5f);
                Expect.Near(0f, plan.Heights[column, row], "a picked place is level too");
            }

            Expect.Equal(spots.Count, held, "each picked place held by one plan of the two");
            Expect.True(Holds(left, home) && !Holds(right, home), "the level place by the plan holding its centre");
        }

        /// <summary>A place's group is made in the plan's scope: the first type at its centre, the rest round it, owned by Neutral Hostile or the owner given, and in steps the same; ending the scope removes them.</summary>
        public static void Made(FakeWorld world)
        {
            Flat();
            var land = Land(5);
            var home = land.Level(300f, 300f, 200f, blend: 128f);
            var camps = land.Places("Camp", Small(), 4, radius: 192f, spacing: 600f, blend: 128f, from: home, difficulties: 2);
            camps.Add(1, Kobold, Kobold, Kobold);
            camps.Add(2, Ogre, Ogre);
            var shops = land.Places("Shop", Small(), 1, radius: 256f, blend: 128f, owner: Player.NeutralPassive);
            shops.Add(1, Merchant, Kobold);
            land.Road(home, shops[0]);
            Expect.Equal(4, camps.Count, "four camps fit");

            var holder = Game.Instance.Add(new Holder());
            land.Plan(Small()).Apply(holder);
            var wrong = 0;
            foreach (var camp in camps)
            {
                var units = camp.Units;
                wrong += units.Count != (camp.Difficulty == 1 ? 3 : 2) ? 1 : 0;
                wrong += Math.Abs(units[0].X - camp.X) > 0.01f || Math.Abs(units[0].Y - camp.Y) > 0.01f ? 1 : 0;
                for (var k = 0; k < units.Count; k++)
                {
                    wrong += units[k].TypeId != (camp.Difficulty == 1 ? Kobold : Ogre) || units[k].Owner != Player.NeutralAggressive ? 1 : 0;
                    var dx = units[k].X - camp.X;
                    var dy = units[k].Y - camp.Y;
                    wrong += k > 0 && Math.Abs(Maths.Sqrt(dx * dx + dy * dy) - 96f) > 0.5f ? 1 : 0;
                }
            }

            Expect.Equal(0, wrong, "each camp's group by its difficulty, the first in the middle, the rest round it, Neutral Hostile");
            var shop = shops[0].Units;
            Expect.True(shop.Count == 2 && shop[0].TypeId == Merchant && shop[0].Owner == Player.NeutralPassive, "the shop's merchant in its middle, owned as given");
            Expect.Equal(0, home.Units.Count, "nothing made at a place from Level");
            holder.End();
            var gone = 0;
            foreach (var camp in camps)
            {
                foreach (var unit in camp.Units)
                {
                    gone += unit.IsGone ? 1 : 0;
                }
            }

            Expect.True(gone == 10 && shop[0].IsGone, "ending the scope removes them: " + gone);

            var stepped = Game.Instance.Add(new Holder());
            var making = land.Make(Small(), stepped, msPerStep: 0.2f);
            var steps = making.Step;
            for (var k = 0; k < 20000 && !making.IsDone; k++)
            {
                world.Advance(0.02f);
                steps += making.Step != steps.Substring(steps.LastIndexOf(',') + 1) ? "," + making.Step : "";
            }

            Expect.Equal("Roads,Heights,Tiles,Places", steps, "roads first and places last, each a step of its own");
            var moved = 0;
            foreach (var camp in camps)
            {
                moved += camp.Units.Count != (camp.Difficulty == 1 ? 3 : 2) || camp.Units[0].IsGone || Math.Abs(camp.Units[0].X - camp.X) > 0.01f ? 1 : 0;
            }

            Expect.Equal(0, moved, "made in steps, the same groups");
            stepped.End();
        }

        /// <summary>A road runs from one place's centre to the other's through the points given, a point about every 256; it is a route either way, and its outline covers a stretch of it.</summary>
        public static void Line(FakeWorld world)
        {
            var land = Land(5);
            var a = land.Level(1000f, 1000f, 400f);
            var b = land.Level(9000f, 8000f, 400f);
            var bend = new Vector2(2000f, 8000f);
            var road = land.Road(a, b, width: 320f, through: bend);
            var points = road.Points;
            Expect.True(points[0] == new Vector2(1000f, 1000f) && points[points.Count - 1] == new Vector2(9000f, 8000f), "from a's centre to b's");
            var through = false;
            var length = 0f;
            var spaced = 0;
            for (var k = 1; k < points.Count; k++)
            {
                through = through || points[k] == bend;
                var step = points[k - 1].DistanceTo(points[k]);
                length += step;
                spaced += step > 100f && step < 700f ? 1 : 0;
            }

            Expect.True(through, "through the point given");
            Expect.Equal(points.Count - 1, spaced, "a point about every 256");
            Expect.True(Math.Abs(length - road.Length) < 0.1f, "its length along the points");
            Expect.True(road.From == a && road.To == b && road.Width == 320f, "its places and width");
            var toB = road.Toward(b);
            var toA = road.Toward(a);
            Expect.True(toB.Reaches && toB.Points[0] == points[0] && toA.Points[0] == points[points.Count - 1] && toA.Points[toA.Points.Count - 1] == points[0], "a route either way");
            Expect.True(Math.Abs(road.Length - toA.Length) < 0.1f, "of the road's length");
            Expect.Throws<ArgumentException>(() => road.Toward(land.Level(0f, 0f, 100f)), "toward a place it does not join");

            var stretch = road.Outline(0f, road.Length / 3f);
            Expect.True(stretch.Contains(points[2]) && !stretch.Contains(points[points.Count - 3]), "the first third, not the end");
            var ahead = (points[3] - points[1]).Normalized();
            var side = new Vector2(-ahead.Y, ahead.X);
            Expect.True(stretch.Contains(points[2] + side * 100f) && stretch.Contains(points[2] - side * 100f), "as wide as the road");
            Expect.True(!stretch.Contains(points[2] + side * 300f) && !stretch.Contains(points[2] - side * 300f), "and no wider");
            var whole = road.Outline();
            var covered = 0;
            for (var k = 1; k < points.Count; k++)
            {
                covered += whole.Contains(Vector2.Lerp(points[k - 1], points[k], 0.5f)) ? 1 : 0;
            }

            Expect.Equal(points.Count - 1, covered, "the whole road with no lengths");
            Expect.Throws<ArgumentException>(() => road.Outline(500f, 500f), "a stretch of no length");

            var again = Land(5);
            var twin = again.Road(again.Level(1000f, 1000f, 400f), again.Level(9000f, 8000f, 400f), width: 320f, through: bend);
            var differ = 0;
            for (var k = 0; k < points.Count; k++)
            {
                differ += twin.Points[k] != points[k] ? 1 : 0;
            }

            Expect.True(twin.Points.Count == points.Count && differ == 0, "the same seed and calls give the same road");
            Expect.Throws<ArgumentException>(() => land.Road(a, a), "one place for both ends");
            Expect.Throws<ArgumentException>(() => land.Road(a, b, width: 0f), "a width of 0");
        }

        /// <summary>Along a road the ground is painted with RoadTile, never blocked, treeless, and levelled to an easy slope up to the places it joins, where the hills beside it are steep.</summary>
        public static void Ground(FakeWorld world)
        {
            var land = Land(9);
            land.Paint(Tile.LordaeronSummerGrass);
            land.Block(Pathing.Walk);
            land.Forest(Tree, cover: 1f);
            land.RoadTile = Tile.LordaeronSummerDirt;
            var a = land.Level(640f, 640f, 256f);
            var b = land.Level(4480f, 4480f, 256f);
            var road = land.Road(a, b);
            var area = Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(5120f, 5120f));
            var plan = land.Plan(area);
            var bare = Land(9).Plan(area);
            var painted = 0;
            var wrong = 0;
            var steepest = 0f;
            var hillsSteepest = 0f;
            for (var row = 1; row < plan.Rows; row++)
            {
                for (var column = 1; column < plan.Columns; column++)
                {
                    var gap = Gap(road, column * 128f, row * 128f);
                    var at = row * plan.Columns + column;
                    painted += gap <= 0f && plan.Tiles[at] == Tile.LordaeronSummerDirt ? 1 : 0;
                    wrong += gap <= 0f && plan.Tiles[at] != Tile.LordaeronSummerDirt ? 1 : 0;
                    wrong += gap < 64f && plan.Blocked[at] != Pathing.None ? 1 : 0;
                    wrong += gap < 128f && plan.Trees[at] != 0 ? 1 : 0;
                    if (gap <= 0f && Gap(road, (column - 1) * 128f, row * 128f) <= 0f)
                    {
                        steepest = Math.Max(steepest, Math.Abs(plan.Heights[column, row] - plan.Heights[column - 1, row]));
                        hillsSteepest = Math.Max(hillsSteepest, Math.Abs(bare.Heights[column, row] - bare.Heights[column - 1, row]));
                    }
                }
            }

            Expect.True(painted > 40, "the road painted: " + painted);
            Expect.Equal(0, wrong, "painted, unblocked and treeless along it");
            Expect.True(plan.Blocked[40] == Pathing.Walk, "blocked away from it");
            Expect.True(steepest <= 0.14f * 128f * 1.5f, "an easy slope along it: " + steepest);
            Expect.True(hillsSteepest > steepest * 2f, "where the hills were steep: " + hillsSteepest);
            Expect.Near(0f, plan.Heights[5, 5], "level at the places it joins");
        }

        /// <summary>Two plans of one seed that touch agree on a road crossing their shared edge; the two halves are the whole planned in one go.</summary>
        public static void Seams(FakeWorld world)
        {
            var land = Land(21);
            land.Paint(Tile.LordaeronSummerGrass);
            land.Block(Pathing.Walk, fromSlope: 30f);
            land.Scatter(Tree, chance: 0.2f);
            land.RoadTile = Tile.LordaeronSummerDirt;
            land.Road(land.Level(-1800f, 600f, 256f), land.Level(1800f, 2000f, 256f));
            var whole = land.Plan(Bounds.FromCorners(new Vector2(-2560f, 0f), new Vector2(2560f, 2560f)));
            var left = land.Plan(Bounds.FromCorners(new Vector2(-2560f, 0f), new Vector2(0f, 2560f)));
            var right = land.Plan(Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(2560f, 2560f)));
            var differ = 0;
            var onRoad = 0;
            for (var row = 0; row < 21; row++)
            {
                onRoad += right.Tiles[row * 21] == Tile.LordaeronSummerDirt ? 1 : 0;
                for (var column = 0; column < 21; column++)
                {
                    differ += Same(whole, column, row, left, column, row) ? 0 : 1;
                    differ += Same(whole, column + 20, row, right, column, row) ? 0 : 1;
                }
            }

            Expect.True(onRoad > 0, "the road crosses the shared edge");
            Expect.Equal(0, differ, "both halves match the whole, the shared edge too");
        }

        /// <summary>Join adds roads until every place reaches every other; Route goes the shortest way over the roads, along each road's points; places no road joins give the start alone.</summary>
        public static void Network(FakeWorld world)
        {
            var land = Land(7);
            var home = land.Level(1000f, 1000f, 512f);
            var camps = land.Places("Camp", World(), 8, radius: 256f, spacing: 1500f);
            var gate = land.Exit("Gate", World(), Side.Right);
            var far = land.Level(9000f, 9000f, 400f);
            var top = land.Road(home, far, through: new Vector2(1000f, 9000f));
            var middle = land.Road(home, far);
            var bottom = land.Road(home, far, through: new Vector2(9000f, 1000f));
            var shortest = Math.Min(top.Length, Math.Min(middle.Length, bottom.Length));
            Expect.True(Math.Abs(shortest - land.Route(home, far).Length) < 0.1f, "the shortest of three lanes");
            Expect.True(!land.Route(home, gate).Reaches, "the gate not yet joined");
            var added = land.Join();
            Expect.Equal(9, added.Count, "nine roads join eleven places, two already joined");
            Expect.Equal(0, land.Join().Count, "none once every place is joined");
            var all = new System.Collections.Generic.List<Place> { home, far, gate };
            all.AddRange(camps);
            var cut = 0;
            foreach (var p in all)
            {
                foreach (var q in all)
                {
                    var way = land.Route(p, q);
                    cut += !way.Reaches || way.Points[0] != new Vector2(p.X, p.Y) || way.Points[way.Points.Count - 1] != new Vector2(q.X, q.Y) ? 1 : 0;
                }
            }

            Expect.Equal(0, cut, "every place reaches every other, from its centre to the other's");

            var chain = Land(1);
            var a = chain.Level(0f, 0f, 200f);
            var b = chain.Level(3000f, 0f, 200f);
            var c = chain.Level(3000f, 3000f, 200f);
            var lone = chain.Level(9000f, 9000f, 200f);
            var ab = chain.Road(a, b);
            var cb = chain.Road(c, b);
            var route = chain.Route(a, c);
            Expect.True(route.Reaches, "a to c over two roads");
            Expect.Equal(ab.Points.Count + cb.Points.Count - 1, route.Points.Count, "every point of both, the place between once");
            Expect.True(Math.Abs(ab.Length + cb.Length - route.Length) < 0.1f, "their lengths added");
            Expect.True(route.Points[0] == new Vector2(0f, 0f) && route.Points[route.Points.Count - 1] == new Vector2(3000f, 3000f) && route.Points[ab.Points.Count - 1] == new Vector2(3000f, 0f), "from a by b to c, the second road backwards");
            var alone = chain.Route(a, lone);
            Expect.True(!alone.Reaches && alone.Points.Count == 1 && alone.Length == 0f, "a place no road joins: the start alone");
            Expect.True(chain.Route(a, a).Reaches && chain.Route(a, a).Points.Count == 1, "a place to itself");
        }

        /// <summary>A place from Level(Bounds) is a rectangle (decisions 358 to 360): level, unblocked and treeless out to its corners, blended over an even band with round corners, and kept clear by Places.</summary>
        public static void Rectangle(FakeWorld world)
        {
            var land = Land(8);
            land.Block(Pathing.Walk);
            land.Forest(Tree, cover: 1f);
            var clearing = land.Level(Bounds.FromCorners(new Vector2(640f, 640f), new Vector2(1920f, 1408f)));
            Expect.True(clearing.IsRectangle && clearing.Width == 1280f && clearing.Height == 768f && clearing.Radius == 384f, "its size, the radius half the shorter side");
            Expect.True(clearing.X == 1280f && clearing.Y == 1024f && clearing.Name == "", "centred on the area");
            var plan = land.Plan(Small());
            var wrong = 0;
            for (var row = 5; row <= 11; row++)
            {
                for (var column = 5; column <= 15; column++)
                {
                    var at = row * 21 + column;
                    wrong += Math.Abs(plan.Heights[column, row]) > 0.001f || plan.Blocked[at] != Pathing.None || plan.Trees[at] != 0 ? 1 : 0;
                }
            }

            Expect.Equal(0, wrong, "level, never blocked and treeless out to its corners");
            Expect.True(plan.Blocked[11 * 21 + 18] == Pathing.Walk && plan.Blocked[14 * 21 + 18] == Pathing.Walk, "blocked past the band");

            var bare = Land(8).Plan(Small());
            Expect.True(Math.Abs(plan.Heights[16, 8] - 0.5f * bare.Heights[16, 8]) < 0.5f, "half a blend past an edge, half the hills: " + plan.Heights[16, 8] + " of " + bare.Heights[16, 8]);
            var t = Maths.Sqrt(2f) / 2f;
            Expect.True(Math.Abs(plan.Heights[16, 12] - t * t * (3f - 2f * t) * bare.Heights[16, 12]) < 0.5f, "a corner blended by its distance, so the band is round there");

            var spots = land.Places("Camp", Small(), 6, radius: 128f, blend: 128f);
            var over = 0;
            foreach (var spot in spots)
            {
                var dx = Math.Max(Math.Abs(spot.X - 1280f) - 640f, 0f);
                var dy = Math.Max(Math.Abs(spot.Y - 1024f) - 384f, 0f);
                over += Maths.Sqrt(dx * dx + dy * dy) < 384f ? 1 : 0;
            }

            Expect.Equal(0, over, "Places keeps its spots off the rectangle and its band");
            Expect.Throws<ArgumentException>(() => land.Level(Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(512f, 0f))), "a side of 0");
        }

        /// <summary>Places and groups refuse what makes no place.</summary>
        public static void Refused(FakeWorld world)
        {
            var land = Land(1);
            Expect.Throws<ArgumentException>(() => land.Places("Camp", World(), 3, radius: 0f), "a radius of 0");
            Expect.Throws<ArgumentException>(() => land.Places("Camp", World(), -1), "a count below 0");
            Expect.Throws<ArgumentException>(() => land.Places("Camp", World(), 3, difficulties: 0), "no difficulty");
            Expect.Throws<ArgumentException>(() => land.Exit("Gate", World(), Side.Top, radius: 0f), "an exit of radius 0");
            var camps = land.Places("Camp", World(), 3, difficulties: 2);
            Expect.Throws<ArgumentException>(() => camps.Add(0, Kobold), "a difficulty of 0");
            Expect.Throws<ArgumentException>(() => camps.Add(3, Kobold), "a difficulty past the list's");
            Expect.Throws<ArgumentException>(() => camps.Add(1), "no types");
            Expect.Throws<ArgumentException>(() => camps.Add(1, Kobold, 0), "a type of 0");
        }

        private static float Distance(Place a, Place b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return Maths.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>How far a point is past the road's edge, below 0 on it.</summary>
        internal static float Gap(Road road, float x, float y)
        {
            var point = new Vector2(x, y);
            var nearest = float.MaxValue;
            for (var k = 1; k < road.Points.Count; k++)
            {
                var a = road.Points[k - 1];
                var along = road.Points[k] - a;
                var t = Maths.Clamp01((point - a).Dot(along) / along.LengthSquared);
                nearest = Math.Min(nearest, point.DistanceTo(a + along * t));
            }

            return nearest - road.Width / 2f;
        }

        private static bool Holds(TerrainPlan plan, Place place)
        {
            foreach (var held in plan.Places)
            {
                if (held == place)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Same(TerrainPlan a, int columnA, int rowA, TerrainPlan b, int columnB, int rowB)
        {
            var at = rowA * a.Columns + columnA;
            var bt = rowB * b.Columns + columnB;
            return Math.Abs(a.Heights[columnA, rowA] - b.Heights[columnB, rowB]) < 0.01f && a.Tiles[at] == b.Tiles[bt] && a.Blocked[at] == b.Blocked[bt] && a.Trees[at] == b.Trees[bt];
        }
    }
}
