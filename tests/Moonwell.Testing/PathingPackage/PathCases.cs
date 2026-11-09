using System;
using System.Collections.Generic;
using System.Text;
using Moonwell.Engine;

namespace Moonwell.Testing
{
    /// <summary>
    /// Cases for the pathing package (decisions 243 to 246) on a flat grid of 36 by 36 cells from (-576, -576), whose
    /// playable area (the terrain fake's) is x -512 to 512 and y -256 to 256: cell columns -16 to 15 and rows -8 to 7.
    /// </summary>
    public static class PathCases
    {
        private static readonly int Footman = ObjectTypes.FourCC("hfoo");

        private static FakePathingWorld Setup(FakeTerrainWorld terrain)
        {
            var text = new StringBuilder();
            for (var k = 0; k < 10 * 10; k++)
            {
                text.Append(GridText.Corner(0f, 2));
            }

            Terrain.Install(terrain, 10, 10, -576f, -576f, text.ToString());
            var fake = new FakePathingWorld(terrain);
            Pathing.Install(fake);
            return fake;
        }

        // Blocks walking in the cells of columns from..to and rows from..to.
        private static void Wall(FakeTerrainWorld terrain, int colFrom, int colTo, int rowFrom, int rowTo, int flags)
        {
            for (var col = colFrom; col <= colTo; col++)
            {
                for (var row = rowFrom; row <= rowTo; row++)
                {
                    terrain.Block(col * 32f + 16f, row * 32f + 16f, flags);
                }
            }
        }

        // A wall two cells thick at x 0 to 64 across the whole playable height, with a gap at rows 0 and 1 (y 0 to 64).
        private static void Gap64(FakeTerrainWorld terrain)
        {
            Wall(terrain, 0, 1, -8, -1, FakeTerrainWorld.NoWalk);
            Wall(terrain, 0, 1, 2, 7, FakeTerrainWorld.NoWalk);
        }

        /// <summary>A collision size's square, as probe 11-09 measured it: below 16 one cell, to 31 two, to 47 three, then four.</summary>
        public static void Squares(FakeWorld world)
        {
            Expect.Equal(1, Cells.Across(0f), "0");
            Expect.Equal(1, Cells.Across(15f), "15");
            Expect.Equal(2, Cells.Across(16f), "16");
            Expect.Equal(2, Cells.Across(31f), "31");
            Expect.Equal(3, Cells.Across(32f), "32");
            Expect.Equal(3, Cells.Across(47f), "47");
            Expect.Equal(4, Cells.Across(48f), "48");
            Expect.Equal(4, Cells.Across(144f), "144");
        }

        /// <summary>Fit and nearest spot ask the helper of the size's square and the movement; a flier asks the terrain.</summary>
        public static void FitAsksTheGame(FakeWorld world)
        {
            var terrain = new FakeTerrainWorld();
            var fake = Setup(terrain);
            Gap64(terrain);
            Expect.True(Pathing.CanStand(31f, -200f, 0f), "open ground");
            Expect.Equal(2, fake.LastCells, "a 31 asks the two-cell helper");
            Expect.True(!Pathing.CanStand(40f, 32f, 100f), "in the wall");
            Expect.Equal(3, fake.LastCells, "a 40 asks the three-cell helper");
            Expect.True(Pathing.CanStand(40f, 32f, 100f, Movement.Amphibious), "an amphibious one floats in it, as the wall's cells float");
            Expect.True(fake.LastMovement == Movement.Amphibious, "with its movement");
            var spot = Pathing.NearestSpot(8f, 40f, 100f);
            Expect.True(spot == new Vector2(80f, 112f), "pushed to the nearest free cell's middle, " + spot);
            var tries = fake.Tries;
            Expect.True(Pathing.CanStand(31f, 32f, 100f, Movement.Fly), "a flier stands over a wall");
            Expect.Equal(tries, fake.Tries, "a flier asks no helper");
        }

        /// <summary>On open ground the path is the straight line, its length the distance.</summary>
        public static void Open(FakeWorld world)
        {
            Setup(new FakeTerrainWorld());
            var path = Pathing.FindPath(31f, -300f, -100f, 300f, 100f);
            Expect.True(path.Reaches, "reaches");
            Expect.Equal(2, path.Points.Count, "start and end");
            Expect.True(path.Points[1] == new Vector2(300f, 100f), "ends at the target");
            Expect.Near(new Vector2(-300f, -100f).DistanceTo(new Vector2(300f, 100f)), path.Length, "length");
        }

        /// <summary>A gap 64 wide lets a 31 straight through and stops a 32, whose path ends at the reachable cell nearest the target.</summary>
        public static void GapBySize(FakeWorld world)
        {
            var terrain = new FakeTerrainWorld();
            Setup(terrain);
            Gap64(terrain);
            var small = Pathing.FindPath(31f, -200f, 40f, 200f, 40f);
            Expect.True(small.Reaches, "a 31 goes through");
            Expect.Near(400f, small.Length, "straight through");
            var large = Pathing.FindPath(32f, -200f, 40f, 200f, 40f);
            Expect.True(!large.Reaches, "a 32 does not");
            var end = large.Points[large.Points.Count - 1];
            Expect.True(end == new Vector2(-48f, 48f), "it ends against the wall, nearest the target: " + end);
        }

        /// <summary>A wall with ground round its ends is walked round; every leg is clear and the path is longer than the line.</summary>
        public static void AroundAWall(FakeWorld world)
        {
            var terrain = new FakeTerrainWorld();
            Setup(terrain);
            Wall(terrain, 0, 1, -4, 3, FakeTerrainWorld.NoWalk);
            var path = Pathing.FindPath(31f, -200f, 0f, 200f, 0f);
            Expect.True(path.Reaches, "reaches");
            Expect.True(path.Points.Count > 2, "it turns");
            Expect.True(path.Length > 400f && path.Length < 600f, "longer than the line, not far: " + path.Length);
            foreach (var point in path.Points)
            {
                Expect.True(point.X < 0f || point.X >= 64f || point.Y >= 128f || point.Y < -128f, "no point in the wall: " + point);
            }
        }

        /// <summary>A diagonal row of cells touching at corners, across the whole area, cannot be crossed, even by one cell.</summary>
        public static void NoCornerCutting(FakeWorld world)
        {
            var terrain = new FakeTerrainWorld();
            Setup(terrain);
            for (var k = -8; k <= 7; k++)
            {
                Wall(terrain, k, k, k, k, FakeTerrainWorld.NoWalk);
            }

            Expect.True(!Pathing.FindPath(8f, -64f, 64f, 64f, -64f).Reaches, "a size 8 cannot pass between the corners");
            Expect.True(Pathing.FindPath(8f, -64f, 64f, -200f, 200f).Reaches, "on its own side it goes anywhere");
        }

        /// <summary>A target no square can stand on does not reach; one outside the playable area neither.</summary>
        public static void TargetBlocked(FakeWorld world)
        {
            var terrain = new FakeTerrainWorld();
            Setup(terrain);
            Wall(terrain, 0, 1, -2, 1, FakeTerrainWorld.NoWalk);
            Expect.True(!Pathing.FindPath(31f, -200f, 0f, 32f, 0f).Reaches, "inside the wall");
            Expect.True(!Pathing.FindPath(31f, -200f, 0f, 2000f, 0f).Reaches, "outside the area");
        }

        /// <summary>Destructables and buildings come from the item trick, asked once a cell; Refresh asks again.</summary>
        public static void ItemsAndRefresh(FakeWorld world)
        {
            var terrain = new FakeTerrainWorld();
            Setup(terrain);
            var blocked = true;
            terrain.Occupied = (x, y) => blocked && x >= 0f && x < 64f;
            Expect.True(!Pathing.FindPath(31f, -200f, 0f, 200f, 0f).Reaches, "the trees wall it off");
            var asked = terrain.PlaceCalls;
            Expect.True(asked > 0, "the item was asked");
            Expect.True(!Pathing.FindPath(31f, -200f, 0f, 200f, 0f).Reaches, "the same again");
            Expect.Equal(asked, terrain.PlaceCalls, "no cell asked twice");
            blocked = false;
            Expect.True(!Pathing.FindPath(31f, -200f, 0f, 200f, 0f).Reaches, "kept until a refresh");
            Pathing.Refresh(Bounds.FromCorners(new Vector2(0f, -600f), new Vector2(63f, 600f)));
            Expect.True(Pathing.FindPath(31f, -200f, 0f, 200f, 0f).Reaches, "open after the refresh");
        }

        /// <summary>Across a strip of water the whole area high: ground stops, amphibious crosses, float keeps to the water, fly goes straight.</summary>
        public static void Movements(FakeWorld world)
        {
            var terrain = new FakeTerrainWorld();
            Setup(terrain);
            Wall(terrain, -16, -1, -8, 7, FakeTerrainWorld.NoFloat);
            Wall(terrain, 4, 15, -8, 7, FakeTerrainWorld.NoFloat);
            Wall(terrain, 0, 3, -8, 7, FakeTerrainWorld.NoWalk);
            Expect.True(!Pathing.FindPath(31f, -200f, 0f, 200f, 0f).Reaches, "ground stops at the water");
            Expect.True(Pathing.FindPath(31f, -200f, 0f, 200f, 0f, Movement.Amphibious).Reaches, "amphibious crosses");
            Expect.True(Pathing.FindPath(31f, 64f, -200f, 64f, 200f, Movement.Float).Reaches, "a ship along the strip");
            Expect.True(!Pathing.FindPath(31f, 64f, 0f, -200f, 0f, Movement.Float).Reaches, "a ship not onto land");
            var fly = Pathing.FindPath(31f, -200f, 0f, 200f, 0f, Movement.Fly);
            Expect.True(fly.Reaches && fly.Points.Count == 2, "a flier straight over");
        }

        /// <summary>Spots for a group: the point first, then cells around it, no two squares overlapping, none on a unit.</summary>
        public static void Spots(FakeWorld world)
        {
            var terrain = new FakeTerrainWorld();
            var fake = Setup(terrain);
            var spots = Pathing.Spots(31f, 0f, 0f, 5);
            Expect.Equal(5, spots.Count, "five");
            Expect.True(spots[0] == new Vector2(0f, 0f), "the point first");
            for (var a = 0; a < spots.Count; a++)
            {
                for (var b = a + 1; b < spots.Count; b++)
                {
                    var apart = Math.Abs(Cells.Of(spots[a].X) - Cells.Of(spots[b].X)) >= 2 || Math.Abs(Cells.Of(spots[a].Y) - Cells.Of(spots[b].Y)) >= 2;
                    Expect.True(apart, "squares apart: " + spots[a] + " " + spots[b]);
                }
            }

            fake.Stand(31f, 0f, 0f);
            Expect.True(Pathing.Spots(31f, 0f, 0f, 1)[0] != new Vector2(0f, 0f), "not on a standing unit");
        }

        /// <summary>A unit's own calls go by its collision size and place.</summary>
        public static void UnitCalls(FakeWorld world)
        {
            var terrain = new FakeTerrainWorld();
            var fake = Setup(terrain);
            Gap64(terrain);
            var unit = Unit.Create(Footman, Player.Get(0), -200f, 40f, Game.Instance);
            Expect.True(unit.CanReach(200f, 40f), "a 31 reaches through the gap");
            fake.Collisions[unit.Handle] = 32f;
            Expect.True(!unit.CanReach(200f, 40f), "a 32 does not");
            Expect.True(unit.FindPath(200f, 40f).Points[0] == new Vector2(-200f, 40f), "from where it stands");
            Expect.True(!unit.CanStand(32f, 100f), "not in the wall");
            Expect.True(unit.NearestSpot(-200f, 40f) == new Vector2(-200f, 40f), "where it stands is free: units are not in the helper's fake");
        }
    }
}
