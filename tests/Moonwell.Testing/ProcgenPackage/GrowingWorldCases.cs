using System;
using System.Collections.Generic;
using System.Text;
using Moonwell.Procgen;

namespace Moonwell.Testing
{
    /// <summary>Cases for growing worlds (decisions 293 to 296): chunks made near heroes in any order equal the whole plan, taken back exactly and made again the same, units in the way put back, camps made once.</summary>
    public static class GrowingWorldCases
    {
        /// <summary>The world's corners across and up, from (0, 0).</summary>
        private const int Side = 40;

        private const int Tree = 0x4C546C74;
        private const int HeroType = 0x48303031;

        private static readonly int Kobold = ObjectTypes.FourCC("nkob");

        /// <summary>A game component that holds what a case makes, ended by the case.</summary>
        private sealed class Holder : GameComponent
        {
        }

        private static Bounds World()
        {
            return Bounds.FromCorners(new Vector2(0f, 0f), new Vector2((Side - 1) * 128f, (Side - 1) * 128f));
        }

        /// <summary>Hills, tiles, blocked slopes, a forest, a mix of two biomes above and a third to the right, a home, camps and roads; the world's own edge fades.</summary>
        private static TerrainGenerator Land(int seed, out PlaceList camps)
        {
            var land = new TerrainGenerator(seed) { Lowest = 0f, Highest = 500f, FeatureSize = 1024f };
            land.Paint(Tile.LordaeronSummerGrass);
            land.Paint(Tile.LordaeronSummerDirt, fromHeight: 0.4f, cover: 0.6f);
            land.Block(Pathing.Walk | Pathing.Build, fromSlope: 25f);
            land.Forest(Tree, cover: 0.5f, density: 0.6f, toSlope: 25f);
            land.Mix(Bounds.FromCorners(new Vector2(0f, 2560f), new Vector2(5000f, 5000f)), 1536f, new RockyHills(), new Grassland());
            land.Use(new SnowyForest(), Bounds.FromCorners(new Vector2(2560f, 0f), new Vector2(5000f, 2560f)));
            var home = land.Level(1024f, 1024f, 384f);
            camps = land.Places("Camp", World(), 4, radius: 256f, spacing: 1200f, from: home, difficulties: 2);
            camps.Add(1, Kobold, Kobold);
            camps.Add(2, Kobold);
            land.RoadTile = Tile.LordaeronSummerDirt;
            land.Join();
            return land;
        }

        /// <summary>A flat fake terrain a little larger than the world.</summary>
        private static void Flat()
        {
            var fake = new FakeTerrainWorld();
            var text = new StringBuilder();
            for (var k = 0; k < (Side + 2) * (Side + 2); k++)
            {
                text.Append(GridText.Corner(0f, 2));
            }

            Terrain.Install(fake, Side + 2, Side + 2, 0f, 0f, text.ToString());
        }

        /// <summary>A hero in the world at a place, flying so it is never put back.</summary>
        private static unit Hero(FakeWorld world, float x, float y)
        {
            var hero = world.NewUnit(HeroType, 0, hero: true);
            world.MakeFlying(hero);
            world.PlacedUnits.Add(hero);
            world.PlaceUnit(hero, x, y);
            return hero;
        }

        /// <summary>The ground at each corner of the world: its height, its tile, and whether units can walk and build by it.</summary>
        private static float[] Ground()
        {
            var ground = new float[Side * Side * 4];
            for (var k = 0; k < Side * Side; k++)
            {
                var x = k % Side * 128f;
                var y = k / Side * 128f;
                ground[k * 4] = Terrain.Height(x, y);
                ground[k * 4 + 1] = Terrain.Tile(x, y);
                ground[k * 4 + 2] = Terrain.IsWalkable(x + 16f, y + 16f) ? 1f : 0f;
                ground[k * 4 + 3] = Terrain.IsBuildable(x - 48f, y - 48f) ? 1f : 0f;
            }

            return ground;
        }

        /// <summary>The trees made from a spawn on: for each corner, its type and variation, and its height.</summary>
        private static float[] Trees(FakeWorld world, int from)
        {
            var trees = new float[Side * Side * 2];
            for (var k = from; k < world.DestructableSpawns.Count; k++)
            {
                var spawn = world.DestructableSpawns[k];
                var at = (int)(spawn.Y / 128f + 0.5f) * Side + (int)(spawn.X / 128f + 0.5f);
                trees[at * 2] = spawn.TypeId * 16 + spawn.Variation + 1;
                trees[at * 2 + 1] = spawn.Z;
            }

            return trees;
        }

        private static int Differ(float[] a, float[] b)
        {
            var differ = 0;
            for (var k = 0; k < a.Length; k++)
            {
                differ += a[k] == b[k] ? 0 : 1;
            }

            return differ;
        }

        /// <summary>Whether the world's ground is as it was: flat, the fake's tile, walkable and buildable.</summary>
        private static int Changed()
        {
            var flat = Ground();
            var changed = 0;
            for (var k = 0; k < Side * Side; k++)
            {
                changed += flat[k * 4] != 0f || flat[k * 4 + 2] != 1f || flat[k * 4 + 3] != 1f ? 1 : 0;
            }

            return changed;
        }

        /// <summary>A world grown in a holder, every chunk near a hero at a place, made to the end; gives the chunks in the order made.</summary>
        private static List<int> GrowAll(FakeWorld world, TerrainGenerator land, Holder holder, float x, float y)
        {
            var grown = land.Grow(World(), holder, chunkSize: 7);
            grown.Near = 100000f;
            grown.Far = 200000f;
            var order = new List<int>();
            grown.Made += chunk => order.Add(chunk.Row * 6 + chunk.Column);
            Hero(world, x, y);
            for (var k = 0; k < 20000 && order.Count < 36; k++)
            {
                world.Advance(0.02f);
            }

            return order;
        }

        /// <summary>Each chunk's plan is the whole world's at its corners, the faded edge and the short last chunks too, and every place is held by one chunk.</summary>
        public static void Pieces(FakeWorld world)
        {
            var land = Land(31, out _);
            var whole = land.Plan(World());
            var mismatches = 0;
            var held = 0;
            var sizes = 0;
            for (var index = 0; index < 36; index++)
            {
                var at = index * 5 % 36;
                var column = at % 6;
                var row = at / 6;
                var chunk = new Chunk(column, row, new Vector2(0f, 0f), 7, Side, Side);
                var plan = land.Plan(chunk.Plot, World());
                sizes += plan.Columns == Math.Min(7, Side - column * 7) && plan.Rows == Math.Min(7, Side - row * 7) ? 0 : 1;
                held += plan.Places.Count;
                for (var k = 0; k < plan.Columns * plan.Rows; k++)
                {
                    var w = (row * 7 + k / plan.Columns) * Side + column * 7 + k % plan.Columns;
                    mismatches += plan.Heights.Cells[k] == whole.Heights.Cells[w] && plan.Tiles[k] == whole.Tiles[w] && plan.Blocked[k] == whole.Blocked[w]
                        && plan.Trees[k] == whole.Trees[w] && plan.TreeVariations[k] == whole.TreeVariations[w] ? 0 : 1;
                }

                Expect.True(chunk.Area.Size.X == plan.Columns * 128f && chunk.Area.Min.X == column * 896f - 64f, "a chunk's area is its corners and 64 round each: " + chunk.Area);
            }

            Expect.Equal(0, sizes, "chunks of 7 corners, the last ones 5");
            Expect.Equal(0, mismatches, "every chunk's corners are the whole plan's");
            Expect.Equal(whole.Places.Count, held, "each place held by one chunk");
            var biomed = 0;
            foreach (var tile in whole.Tiles)
            {
                biomed += tile != 0 && tile != Tile.LordaeronSummerGrass && tile != Tile.LordaeronSummerDirt ? 1 : 0;
            }

            Expect.True(biomed > 100, "the biomes' own tiles in the world: " + biomed);
            Expect.True(whole.Places.Count >= 3, "a home and camps: " + whole.Places.Count);
            Expect.Near(0f, whole.Heights[0, 20], "the world's edge fades");
            Expect.Throws<ArgumentException>(() => land.Grow(World(), Game.Instance, chunkSize: 0), "a chunk size of 0");
            Expect.Throws<ArgumentException>(() => new TerrainGenerator(1) { FeatureSize = 0f }.Grow(World(), Game.Instance), "a feature size of 0");
        }

        /// <summary>A world grown from opposite corners is made in other orders, and both are the whole plan applied, ground and trees; ending the scope takes all of it back.</summary>
        public static void AnyOrder(FakeWorld world)
        {
            Flat();
            var land = Land(31, out _);
            var holder = Game.Instance.Add(new Holder());
            land.Plan(World()).Apply(holder);
            var ground = Ground();
            var trees = Trees(world, 0);
            holder.End();
            Expect.Equal(0, Changed(), "applied and taken back");

            var orders = new List<int>[2];
            for (var run = 0; run < 2; run++)
            {
                var from = world.DestructableSpawns.Count;
                var grown = Game.Instance.Add(new Holder());
                orders[run] = GrowAll(world, land, grown, run == 0 ? 0f : 4992f, run == 0 ? 0f : 4992f);
                Expect.Equal(36, orders[run].Count, "every chunk made");
                Expect.Equal(0, Differ(ground, Ground()), "chunk by chunk, the whole plan's ground");
                Expect.Equal(0, Differ(trees, Trees(world, from)), "and its trees, at the same heights");
                grown.End();
                Expect.Equal(0, Changed(), "ending the scope takes the world back");
                world.PlacedUnits.Clear();
            }

            Expect.True(orders[0][0] == 0 && orders[1][0] == 35, "nearest first: " + orders[0][0] + " and " + orders[1][0]);
        }

        /// <summary>A chunk far from every hero is taken back, cut and dead trees too, TakingBack first; made again, it is the same; a dead hero holds none; two heroes hold two areas.</summary>
        public static void TakeBack(FakeWorld world)
        {
            Flat();
            var land = Land(31, out _);
            var holder = Game.Instance.Add(new Holder());
            var grown = land.Grow(World(), holder, chunkSize: 8);
            grown.Near = 0f;
            grown.Far = 1024f;
            var made = new List<string>();
            var taken = new List<string>();
            var standing = 0;
            grown.Made += chunk => made.Add(chunk.Column + "," + chunk.Row);
            grown.TakingBack += chunk =>
            {
                taken.Add(chunk.Column + "," + chunk.Row);
                standing += chunk.IsMade && chunk.Scope != null && !chunk.Scope.HasEnded ? 1 : 0;
            };

            var hero = Hero(world, 512f, 512f);
            world.Advance(1f);
            Expect.Equal("0,0", string.Join(";", made), "the chunk the hero stands in, alone");
            var first = Ground();
            var trees = Trees(world, 0);
            var spawns = world.DestructableSpawns.Count;
            Expect.True(spawns > 5, "with trees: " + spawns);
            world.SlayDestructable(world.DestructableSpawns[0].Made!);
            world.PutDestructableLife(world.DestructableSpawns[1].Made!, 5f);

            world.PlaceUnit(hero, -500f, 512f);
            world.Advance(1f);
            Expect.Equal(0, taken.Count, "a hero within Far keeps it");
            world.PlaceUnit(hero, 4500f, 4500f);
            world.Advance(1f);
            Expect.Equal("0,0", string.Join(";", taken), "taken back once every hero is far");
            Expect.Equal(1, standing, "TakingBack runs while it stands");
            var removed = 0;
            for (var k = 0; k < spawns; k++)
            {
                removed += world.RemovedDestructables.Contains(world.DestructableSpawns[k].Made!) ? 1 : 0;
            }

            Expect.Equal(spawns, removed, "every tree removed, the dead and cut ones too");
            var left = Ground();
            var back = 0;
            for (var k = 0; k < 64; k++)
            {
                var at = (k / 8 * Side + k % 8) * 4;
                back += left[at] == 0f && left[at + 2] == 1f && left[at + 3] == 1f ? 0 : 1;
            }

            Expect.Equal(0, back, "its ground as it was");
            Expect.Equal("0,0;4,4", string.Join(";", made), "the far chunk made");

            var remade = world.DestructableSpawns.Count;
            world.PlaceUnit(hero, 512f, 512f);
            world.Advance(1f);
            Expect.Equal("4,4", taken[taken.Count - 1], "the far one taken back");
            var again = Ground();
            var same = 0;
            for (var k = 0; k < 64; k++)
            {
                var at = (k / 8 * Side + k % 8) * 4;
                for (var part = 0; part < 4; part++)
                {
                    same += again[at + part] == first[at + part] ? 0 : 1;
                }
            }

            Expect.Equal(0, same, "made again, the same ground");
            Expect.Equal(0, Differ(trees, Trees(world, remade)), "and the same trees");

            var other = Hero(world, 4500f, 512f);
            world.Advance(1f);
            Expect.Equal("4,0", made[made.Count - 1], "a second hero, a second area");
            world.SlayUnit(hero);
            world.Advance(1f);
            Expect.Equal("0,0", taken[taken.Count - 1], "a dead hero holds none");
            world.SlayUnit(other);
            world.Advance(1f);
            Expect.Equal(0, Changed(), "with no hero living, the world is as it was");
            holder.End();
        }

        /// <summary>Once a chunk is made, a ground unit by a new tree or blocked corner is put back through the game's pathing before Made; one in the open, a flyer and a dead one are not.</summary>
        public static void Units(FakeWorld world)
        {
            Flat();
            var land = Land(31, out _);
            var plan = land.Plan(World());
            var trapped = -1;
            var open = -1;
            var chunk = 0;
            for (; chunk < 25 && (trapped < 0 || open < 0); chunk++)
            {
                trapped = -1;
                open = -1;
                for (var k = 0; k < 36; k++)
                {
                    var at = (chunk / 5 * 8 + 1 + k / 6) * Side + chunk % 5 * 8 + 1 + k % 6;
                    trapped = trapped < 0 && plan.Trees[at] != 0 ? at : trapped;
                    var clear = true;
                    for (var n = 0; n < 9; n++)
                    {
                        var near = at + (n / 3 - 1) * Side + n % 3 - 1;
                        clear = clear && plan.Trees[near] == 0 && (plan.Blocked[near] & Pathing.Walk) == 0;
                    }

                    open = open < 0 && clear ? at : open;
                }
            }

            chunk--;
            Expect.True(trapped >= 0 && open >= 0, "a chunk with a tree and open ground");
            var footman = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            world.PlaceUnit(footman, trapped % Side * 128f + 40f, trapped / Side * 128f);
            var walker = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            world.PlaceUnit(walker, open % Side * 128f, open / Side * 128f);
            var flyer = world.NewUnit(ObjectTypes.FourCC("hgry"));
            world.MakeFlying(flyer);
            world.PlaceUnit(flyer, trapped % Side * 128f, trapped / Side * 128f);
            var corpse = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            world.PlaceUnit(corpse, trapped % Side * 128f, trapped / Side * 128f);
            world.SlayUnit(corpse);

            var holder = Game.Instance.Add(new Holder());
            var grown = land.Grow(World(), holder, chunkSize: 8);
            grown.Near = 0f;
            var before = world.UnitCalls.Count;
            var movesAtMade = -1;
            grown.Made += made => movesAtMade = Moves(world, before);
            Hero(world, chunk % 5 * 1024f + 448f, chunk / 5 * 1024f + 448f);
            world.Advance(1f);
            Expect.Equal(1, Moves(world, before), "one unit put back");
            Expect.Equal(1, movesAtMade, "before Made");
            Expect.Near(trapped % Side * 128f + 40f, world.UnitX(footman), "the one by the tree, at its own place for the game to move");
            holder.End();
        }

        /// <summary>A camp is made the first time its chunk is, held by the world: it stays while the chunk is gone, and is not made again with it.</summary>
        public static void Camps(FakeWorld world)
        {
            Flat();
            var land = Land(31, out var camps);
            var camp = camps[0];
            var holder = Game.Instance.Add(new Holder());
            var grown = land.Grow(World(), holder, chunkSize: 8);
            grown.Near = 0f;
            grown.Far = 1024f;
            var hero = Hero(world, camp.X, camp.Y);
            world.Advance(1f);
            var units = new List<Unit>(camp.Units);
            Expect.True(units.Count > 0, "the camp made with its chunk");
            world.SlayUnit(units[0].Handle);
            var far = camp.X < 2500f ? 4500f : 300f;
            world.PlaceUnit(hero, far, far);
            world.Advance(2f);
            var kept = 0;
            foreach (var unit in units)
            {
                kept += unit.IsGone ? 0 : 1;
            }

            Expect.Equal(units.Count, kept, "kept while its chunk is gone");
            world.PlaceUnit(hero, camp.X, camp.Y);
            world.Advance(2f);
            var same = camp.Units.Count == units.Count ? 0 : 1;
            for (var k = 0; k < units.Count && same == 0; k++)
            {
                same += ReferenceEquals(units[k], camp.Units[k]) ? 0 : 1;
            }

            Expect.Equal(0, same, "not made again with it");
            Expect.True(!units[0].IsAlive, "a creep killed stays dead");
            holder.End();
            var gone = 0;
            foreach (var unit in units)
            {
                gone += unit.IsGone ? 1 : 0;
            }

            Expect.Equal(units.Count, gone, "ending the world removes them");
        }

        /// <summary>Ending a world, or the scope it was grown in, takes back every chunk: none reads IsMade after (J48).</summary>
        public static void Ended(FakeWorld world)
        {
            Flat();
            var land = Land(31, out _);
            for (var run = 0; run < 2; run++)
            {
                var holder = Game.Instance.Add(new Holder());
                var grown = land.Grow(World(), holder, chunkSize: 8);
                grown.Near = 0f;
                var chunks = new List<Chunk>();
                grown.Made += chunks.Add;
                Hero(world, 960f, 960f);
                world.Advance(4f);
                var made = 0;
                foreach (var chunk in chunks)
                {
                    made += chunk.IsMade ? 1 : 0;
                }

                Expect.Equal(4, made, "the four chunks the hero stands between, made");
                if (run == 0)
                {
                    grown.End();
                }

                holder.End();
                made = 0;
                foreach (var chunk in chunks)
                {
                    made += chunk.IsMade ? 1 : 0;
                }

                Expect.Equal(0, made, run == 0 ? "none made once the world ended" : "none made once the scope it was grown in ended");
                world.PlacedUnits.Clear();
            }
        }

#if DEBUG
        /// <summary>
        /// A forest of over 500 trees, made by Apply, Make or Grow, is one child of the scope given and is never reported as a
        /// leak; 500 timers of the map's own in that scope still are (decision 326).
        /// </summary>
        public static void NoLeakReported(FakeWorld world)
        {
            Flat();
            var land = new TerrainGenerator(5) { Lowest = 0f, Highest = 500f, FeatureSize = 1024f };
            land.Paint(Tile.LordaeronSummerDirt, fromHeight: 0.4f);
            land.Forest(Tree, cover: 1f, density: 1f);
            for (var way = 0; way < 3; way++)
            {
                var from = world.DestructableSpawns.Count;
                var holder = Game.Instance.Add(new Holder());
                if (way == 0)
                {
                    land.Plan(World()).Apply(holder);
                }
                else if (way == 1)
                {
                    var making = land.Make(World(), holder);
                    for (var k = 0; k < 2000 && !making.IsDone; k++)
                    {
                        world.Advance(0.02f);
                    }
                }
                else
                {
                    var grown = land.Grow(World(), holder, chunkSize: Side);
                    grown.Near = 0f;
                    var made = false;
                    grown.Made += chunk => made = true;
                    Hero(world, 512f, 512f);
                    for (var k = 0; k < 2000 && !made; k++)
                    {
                        world.Advance(0.02f);
                    }
                }

                var name = way == 0 ? "Apply" : way == 1 ? "Make" : "Grow";
                Expect.True(world.DestructableSpawns.Count - from > 1000, name + " makes a forest: " + (world.DestructableSpawns.Count - from));
                Expect.Equal(1, holder.ChildCount, name + "'s scope holds one thing");
                Expect.Equal(0, LeakReports(world), name + " reports no leak: " + string.Join(" | ", world.Logged));
                holder.End();
                world.PlacedUnits.Clear();
            }

            var own = Game.Instance.Add(new Holder());
            land.Plan(World()).Apply(own);
            for (var k = 0; k < 499; k++)
            {
                own.After(1000f, () => { });
            }

            Expect.Equal(1, LeakReports(world), "the map's own 499 timers beside a forest are reported");
            own.End();
        }

        private static int LeakReports(FakeWorld world)
        {
            var count = 0;
            foreach (var line in world.Logged)
            {
                count += line.IndexOf(".Leak", StringComparison.Ordinal) >= 0 ? 1 : 0;
            }

            return count;
        }
#endif

        private static int Moves(FakeWorld world, int from)
        {
            var moves = 0;
            for (var k = from; k < world.UnitCalls.Count; k++)
            {
                moves += world.UnitCalls[k] == "move" ? 1 : 0;
            }

            return moves;
        }
    }
}
