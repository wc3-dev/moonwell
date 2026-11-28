using System;
using System.Collections.Generic;
using System.Text;
using Moonwell.Engine;

namespace Moonwell.Testing
{
    /// <summary>
    /// Cases for where a building fits (J60), each the game's own answer in probe <c>probe-placement</c> (game facts 11-28),
    /// on a flat grid of 4,096 square from (-2048, -2048) whose playable area is x and y -1536 to 1536.
    /// </summary>
    public static class BuildCases
    {
        private static readonly int Farm = ObjectTypes.FourCC("hhou");
        private static readonly int Hall = ObjectTypes.FourCC("htow");
        private static readonly int Burrow = ObjectTypes.FourCC("otrb");
        private static readonly int Ziggurat = ObjectTypes.FourCC("uzig");
        private static readonly int Mine = ObjectTypes.FourCC("ngol");
        private static readonly int Necropolis = ObjectTypes.FourCC("unpl");
        private static readonly int Corners = ObjectTypes.FourCC("Xcrs");
        private static readonly int Footman = ObjectTypes.FourCC("hfoo");

        // The standard buildings' rows as the build writes them, and one of a texture that takes two corner cells of four.
        private const string Table = "hhou4x4,2,0,-;htow16x16,2,0,g;otrb6x6,2,0,-;uzig6x6,2,1,-;ngol16x16,2,0,m;unpl16x16,2,0,-;Xcrs2x2:9,2,0,-";

        private static FakePathingWorld Setup(FakeTerrainWorld terrain)
        {
            terrain.AreaMinX = -1536f;
            terrain.AreaMinY = -1536f;
            terrain.AreaMaxX = 1536f;
            terrain.AreaMaxY = 1536f;
            const int corners = 33;
            var text = new StringBuilder();
            for (var k = 0; k < corners * corners; k++)
            {
                text.Append(GridText.Corner(0f, 2));
            }

            Terrain.Install(terrain, corners, corners, -2048f, -2048f, text.ToString());
            var fake = new FakePathingWorld(terrain);
            Paths.Install(fake);
            Paths.Buildings(Table);
            return fake;
        }

        /// <summary>A building's place goes onto the destructables' grid: the 64 grid toward 0, 32 further when half its cells is odd.</summary>
        public static void Place(FakeWorld world)
        {
            Setup(new FakeTerrainWorld());
            Expect.True(Paths.BuildPlace(Farm, 2767f, 2383f) == new Vector2(2752f, 2368f), "a farm asked at (2767, 2383)");
            Expect.True(Paths.BuildPlace(Farm, -465f, 2479f) == new Vector2(-448f, 2432f), "a farm asked at (-465, 2479)");
            Expect.True(Paths.BuildPlace(Burrow, 2767f, 1615f) == new Vector2(2784f, 1632f), "a burrow asked at (2767, 1615)");
            Expect.True(Paths.BuildPlace(Burrow, -465f, 1711f) == new Vector2(-480f, 1696f), "a burrow asked at (-465, 1711)");
            Expect.True(Paths.BuildPlace(Burrow, 0f, -17f) == new Vector2(32f, -32f), "0 counts as above");
            Expect.Throws<ArgumentException>(() => Paths.BuildPlace(Footman, 0f, 0f), "a type the table does not hold");
        }

        /// <summary>Two buildings' cells may touch and not overlap, the unbuildable ring of a hall too; another player's count the same.</summary>
        public static void Buildings(FakeWorld world)
        {
            var fake = Setup(new FakeTerrainWorld());
            Expect.True(Paths.CanBuild(Farm, 0f, 0f), "open ground");
            fake.Put(Farm, 0f, 0f, 72f, 1, building: true);
            Expect.True(!Paths.CanBuild(Farm, 112f, 0f) && !Paths.CanBuild(Farm, -112f, 0f) && !Paths.CanBuild(Farm, 0f, 112f), "a farm within 112 of a farm");
            Expect.True(Paths.CanBuild(Farm, 128f, 0f) && Paths.CanBuild(Farm, -128f, 0f) && Paths.CanBuild(Farm, 128f, 128f), "a farm at 128");
            Expect.True(!Paths.CanBuild(Burrow, -112f, 0f) && Paths.CanBuild(Burrow, -128f, 0f), "a burrow, which moves to -160 from -128");
            fake.Clear();
            fake.Put(Hall, 0f, 0f, 176f, 0, building: true);
            Expect.True(!Paths.CanBuild(Hall, 496f, 0f) && Paths.CanBuild(Hall, 512f, 0f) && Paths.CanBuild(Hall, -512f, 0f), "halls 512 apart: the rings");
            Expect.True(!Paths.CanBuild(Farm, 304f, 0f) && Paths.CanBuild(Farm, 320f, 0f), "a farm at 320 from a hall, on its ring's edge");
        }

        /// <summary>A cell set unbuildable refuses every building with a cell of it, ring cells too; cells not walkable refuse none.</summary>
        public static void Ground(FakeWorld world)
        {
            var terrain = new FakeTerrainWorld();
            Setup(terrain);
            terrain.Block(16f, 16f, FakeTerrainWorld.NoBuild);
            Terrain.Refresh(Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(32f, 32f)));
            Expect.True(!Paths.CanBuild(Farm, -56f, 0f) && Paths.CanBuild(Farm, -64f, 0f), "a farm's left side");
            Expect.True(!Paths.CanBuild(Farm, 120f, 0f) && Paths.CanBuild(Farm, 128f, 0f), "a farm's right side");
            Expect.True(!Paths.CanBuild(Hall, -248f, 16f) && Paths.CanBuild(Hall, -256f, 16f), "a hall's ring, left");
            Expect.True(!Paths.CanBuild(Hall, 312f, 16f) && Paths.CanBuild(Hall, 320f, 16f), "a hall's ring, right");
            terrain.Block(16f, 16f, FakeTerrainWorld.NoWalk | FakeTerrainWorld.NoFly);
            Terrain.Refresh(Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(32f, 32f)));
            Expect.True(Paths.CanBuild(Farm, 0f, 0f) && Paths.CanBuild(Hall, 0f, 0f), "a cell not walkable nor flyable refuses none");
        }

        /// <summary>A tree's cells, which the item trick sees, refuse a building on them.</summary>
        public static void Trees(FakeWorld world)
        {
            var terrain = new FakeTerrainWorld();
            Setup(terrain);
            terrain.Occupied = (x, y) => x > -64f && x < 64f && y > -64f && y < 64f;
            Expect.True(!Paths.CanBuild(Farm, 112f, 0f) && Paths.CanBuild(Farm, 128f, 0f), "a farm by a tree");
            Expect.True(!Paths.CanBuild(Hall, 304f, 0f) && Paths.CanBuild(Hall, 320f, 0f), "a hall's ring by a tree");
        }

        /// <summary>Units count only with a builder's player: everyone's ground units but that player's own unpaused ones.</summary>
        public static void Units(FakeWorld world)
        {
            var fake = Setup(new FakeTerrainWorld());
            fake.Put(Footman, 0f, 0f, 31f, 1);
            Expect.True(Paths.CanBuild(Farm, 0f, 0f), "no player: units don't count");
            Expect.True(!Paths.CanBuild(Farm, 0f, 0f, Player.Get(0)) && !Paths.CanBuild(Farm, 120f, 0f, Player.Get(0)), "another player's unit");
            Expect.True(Paths.CanBuild(Farm, 128f, 0f, Player.Get(0)), "clear of its square");
            Expect.True(Paths.CanBuild(Farm, 0f, 0f, Player.Get(1)), "the builder's player's own, free to move");
            fake.Clear();
            fake.Put(Footman, 0f, 0f, 31f, 0, paused: true);
            Expect.True(!Paths.CanBuild(Farm, 0f, 0f, Player.Get(0)), "its own paused unit");
            fake.Clear();
            fake.Put(Footman, 0f, 0f, 8f, 1, flies: true);
            Expect.True(Paths.CanBuild(Farm, 0f, 0f, Player.Get(0)), "a flier never");
        }

        /// <summary>A building that needs blight needs every cell blighted; blight refuses none of the others.</summary>
        public static void Blight(FakeWorld world)
        {
            var terrain = new FakeTerrainWorld();
            Setup(terrain);
            terrain.Blighted = (x, y) => x > -192f && x < 192f && y > -192f && y < 192f;
            Expect.True(Paths.CanBuild(Ziggurat, 0f, 0f), "a ziggurat at (32, 32): cells -64 to 128");
            Expect.True(Paths.CanBuild(Ziggurat, 64f, 64f), "at (96, 96): cells to 192");
            Expect.True(!Paths.CanBuild(Ziggurat, 128f, 0f), "at 160: a cell past the blight");
            Expect.True(!Paths.CanBuild(Ziggurat, 600f, 600f), "unblighted ground");
            Expect.True(Paths.CanBuild(Farm, 0f, 0f) && Paths.CanBuild(Necropolis, 0f, 0f), "blight refuses neither a farm nor a necropolis");
        }

        /// <summary>Every cell inside the playable area.</summary>
        public static void Edge(FakeWorld world)
        {
            Setup(new FakeTerrainWorld());
            Expect.True(Paths.CanBuild(Farm, 1472f, 0f) && !Paths.CanBuild(Farm, 1536f, 0f), "a farm at the right edge");
            Expect.True(Paths.CanBuild(Farm, -1472f, 0f) && !Paths.CanBuild(Farm, -1536f, 0f), "at the left edge");
            Expect.True(Paths.CanBuild(Hall, 1280f, 0f) && !Paths.CanBuild(Hall, 1344f, 0f), "a hall at the right edge");
        }

        /// <summary>A building that takes gold in is kept 768 from every gold mine, centre to centre; the others only by cells.</summary>
        public static void Mines(FakeWorld world)
        {
            var fake = Setup(new FakeTerrainWorld());
            fake.Put(Mine, 0f, 0f, 50f, 27, building: true);
            Expect.True(!Paths.CanBuild(Hall, 704f, 0f) && Paths.CanBuild(Hall, 768f, 0f), "along x");
            Expect.True(!Paths.CanBuild(Hall, 640f, 384f) && Paths.CanBuild(Hall, 704f, 384f), "746 refused, 802 taken");
            Expect.True(!Paths.CanBuild(Hall, 384f, 640f) && Paths.CanBuild(Hall, 448f, 640f), "and across");
            Expect.True(!Paths.CanBuild(Necropolis, 496f, 0f) && Paths.CanBuild(Necropolis, 512f, 0f), "a necropolis, by its cells");
            Expect.True(!Paths.CanBuild(Farm, 304f, 0f) && Paths.CanBuild(Farm, 320f, 0f), "a farm, by the mine's cells");
        }

        /// <summary>A texture that takes some cells of its rectangle is checked at those alone.</summary>
        public static void Shapes(FakeWorld world)
        {
            var terrain = new FakeTerrainWorld();
            Setup(terrain);

            // Asked at (0, 0) it stands at (32, 32): cells 0 and 1 each way, taking (0, 0) and (1, 1).
            terrain.Block(48f, 16f, FakeTerrainWorld.NoBuild);
            Terrain.Refresh(Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(64f, 64f)));
            Expect.True(Paths.CanBuild(Corners, 0f, 0f), "a cell it does not take");
            terrain.Block(16f, 16f, FakeTerrainWorld.NoBuild);
            Terrain.Refresh(Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(64f, 64f)));
            Expect.True(!Paths.CanBuild(Corners, 0f, 0f), "a cell it takes");
        }

        /// <summary>Places for several buildings: nearest first on the building's grid, none overlapping, none where it can't go.</summary>
        public static void Spots(FakeWorld world)
        {
            var fake = Setup(new FakeTerrainWorld());
            var spots = Paths.BuildSpots(Farm, 10f, 10f, 5);
            Expect.Equal(5, spots.Count, "five");
            Expect.True(spots[0] == new Vector2(0f, 0f), "the point's own place first, " + spots[0]);
            Expect.True(spots[1] == new Vector2(128f, 0f) && spots[2] == new Vector2(0f, 128f), "then the nearest that don't overlap it, lowest first of two as near: " + spots[1] + " " + spots[2]);
            Expect.True(spots[3] == new Vector2(0f, -128f) && spots[4] == new Vector2(-128f, 0f), "then " + spots[3] + " " + spots[4]);
            var asked = fake.UnitsAsked;
            Paths.BuildSpots(Hall, 0f, 0f, 3);
            Expect.Equal(asked + 1, fake.UnitsAsked, "one look at the units for all the places");
            fake.Put(Farm, 0f, 0f, 72f, 0, building: true);
            Expect.True(Paths.BuildSpots(Farm, 0f, 0f, 1)[0] != new Vector2(0f, 0f), "not on a building");
            Expect.Equal(0, Paths.BuildSpots(Farm, 0f, 0f, 0).Count, "none asked");
            Expect.Equal(0, Paths.BuildSpots(Hall, 5000f, 5000f, 2).Count, "none off the map");
        }
    }
}
