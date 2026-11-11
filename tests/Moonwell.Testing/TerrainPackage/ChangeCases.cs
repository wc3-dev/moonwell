using System.Text;

namespace Moonwell.Testing
{
    /// <summary>Cases for the terrain changes a map makes and its scopes take back, on a flat grid of 20 by 20 corners from (0, 0).</summary>
    public static class ChangeCases
    {
        /// <summary>A game component that holds what a case makes, ended by the case.</summary>
        private sealed class Holder : GameComponent
        {
        }

        private static FakeTerrainWorld Flat()
        {
            var fake = new FakeTerrainWorld();
            var text = new StringBuilder();
            for (var k = 0; k < 20 * 20; k++)
            {
                text.Append(GridText.Corner(0f, 2));
            }

            Terrain.Install(fake, 20, 20, 0f, 0f, text.ToString());
            return fake;
        }

        /// <summary>A raise moves Height as the game's crater does (cos of the distance over the radius, at corners), asks the game for the crater, and its end takes both back: the crater by stopping it.</summary>
        public static void RaiseHeight(FakeWorld world)
        {
            var fake = Flat();
            var holder = Game.Instance.Add(new Holder());
            Terrain.Raise(1280f, 1280f, 256f, 100f, holder);
            Expect.Near(100f, Terrain.Height(1280f, 1280f), "the centre");
            Expect.Near(70.711f, Terrain.Height(1408f, 1280f), "a corner at 128");
            Expect.Near(85.355f, Terrain.Height(1344f, 1280f), "half way, blended");
            Expect.Near(0f, Terrain.Height(1536f, 1280f), "the radius");
            Expect.Equal(1, fake.Craters.Count, "one crater asked");
            Expect.Near(-100f, fake.Craters[0][3], "a raise is a crater of negative depth");
            holder.End();
            Expect.Near(0f, Terrain.Height(1280f, 1280f), "the centre after");
            Expect.Near(0f, Terrain.Height(1408f, 1280f), "a corner after");
            Expect.Equal(1, fake.Craters.Count, "the end asks no crater");
            Expect.Equal(1, fake.Stopped.Count, "it stops its own");
            Expect.Equal(0, fake.Stopped[0], "the one it made");
        }

        /// <summary>Raises add up where they meet, and ending one leaves the other; Lower goes down.</summary>
        public static void RaisesAdd(FakeWorld world)
        {
            Flat();
            var first = Terrain.Raise(1280f, 1280f, 256f, 100f, Game.Instance);
            var second = Terrain.Lower(1280f, 1280f, 512f, 30f, Game.Instance);
            Expect.Near(70f, Terrain.Height(1280f, 1280f), "100 up and 30 down");
            first.End();
            Expect.Near(-30f, Terrain.Height(1280f, 1280f), "the lower is left");
            second.End();
            Expect.Near(0f, Terrain.Height(1280f, 1280f), "flat again");
        }

        /// <summary>A ray over flat ground hits a hill raised in its way, though its block was read before the raise.</summary>
        public static void RaiseRaycast(FakeWorld world)
        {
            Flat();
            Expect.True(!Terrain.Raycast(new Vector3(256f, 1280f, 50f), new Vector3(2304f, 1280f, 50f), out _), "no hit on flat ground");
            Terrain.Raise(1280f, 1280f, 384f, 200f, Game.Instance);
            Expect.True(Terrain.Raycast(new Vector3(256f, 1280f, 50f), new Vector3(2304f, 1280f, 50f), out var hit), "the hill is hit");
            Expect.True(hit.Point.X < 1280f && hit.Point.X > 896f, "on its near side: " + hit.Point.X);
        }

        /// <summary>
        /// A save that holds lasting craters loads wrong, and a load shows the map's own tiles (game facts, 11-11). So a save
        /// stops the craters of the raises held, the next moment makes them again, and a load makes them again in a game
        /// loaded from the save; Height reads the raise all along; a load paints the tiles held again and leaves pathing; an
        /// ended raise and one made since the save are not made again.
        /// </summary>
        public static void SaveAndLoad(FakeWorld world)
        {
            var fake = Flat();
            var holder = Game.Instance.Add(new Holder());
            Terrain.Block(1280f, 1280f, 64f, Pathing.Walk, holder);
            Expect.True(!world.Listening.Contains(Moonwell.Hosting.GameEvent.Loaded) && !world.Listening.Contains(Moonwell.Hosting.GameEvent.Saving), "pathing asks for no save or load to be heard");
            Terrain.Raise(1280f, 1280f, 256f, 100f, holder);
            Expect.True(world.Listening.Contains(Moonwell.Hosting.GameEvent.Saving), "a raise asks for a save to be heard");
            Expect.True(world.Listening.Contains(Moonwell.Hosting.GameEvent.Loaded), "and a loaded game");
            Terrain.Raise(640f, 640f, 256f, 50f, Game.Instance).End();
            Terrain.SetTile(1280f, 1280f, Tile.LordaeronSummerGrass, 64f, holder);
            Expect.Equal(2, fake.Craters.Count, "two craters asked");
            Expect.Equal(1, fake.Stopped.Count, "the ended raise's is stopped");

            // The game is about to save: the save holds no crater.
            Inbound.GameSaving();
            Expect.Equal(2, fake.Stopped.Count, "the raise held is stopped for the save");
            Expect.Equal(0, fake.Stopped[1], "its own crater");
            Expect.Near(100f, Terrain.Height(1280f, 1280f), "Height reads the raise all along");
            var late = Terrain.Raise(1920f, 1920f, 256f, 30f, Game.Instance);
            Expect.Equal(3, fake.Craters.Count, "a raise made in that moment asks its crater");

            // The game that goes on: the next moment makes the stopped one again.
            world.Advance(0.01f);
            Expect.Equal(4, fake.Craters.Count, "the raise held is asked again, and neither the ended one nor the one made since");
            Expect.Near(1280f, fake.Craters[3][0], "at its place");
            Expect.Near(-100f, fake.Craters[3][3], "with its own depth");
            late.End();
            Expect.Equal(3, fake.Stopped.Count, "the late raise ends as any other");

            // A game loaded from a save: as when the save was written, with the craters stopped and the map's own tiles.
            Inbound.GameSaving();
            Expect.Equal(4, fake.Stopped.Count, "stopped for the second save");
            fake.Tiles.Clear();
            fake.Variations.Clear();
            var paints = fake.Paints;
            var sets = fake.PathSets;
            Inbound.GameLoaded();
            Expect.Equal(5, fake.Craters.Count, "the load asks the raise again");
            Expect.Near(-100f, fake.Craters[4][3], "with its own depth");
            Expect.Near(100f, Terrain.Height(1280f, 1280f), "Height is as before the load");
            Expect.Near(70.711f, Terrain.Height(1408f, 1280f), "at every corner");
            Expect.Near(0f, Terrain.Height(640f, 640f), "the ended raise stays ended");
            Expect.Equal(Tile.LordaeronSummerGrass, Terrain.Tile(1280f, 1280f), "the tile is painted again");
            Expect.Equal(paints + 1, fake.Paints, "one corner painted");
            Expect.Equal(sets, fake.PathSets, "pathing lasts through a load: nothing is set again");
            Expect.True(!Terrain.IsWalkable(1296f, 1296f), "and is still taken");
            world.Advance(0.01f);
            Expect.Equal(5, fake.Craters.Count, "the moment after the load asks nothing more");

            holder.End();
            Expect.Equal(5, fake.Stopped.Count, "the end stops the crater the load made");
            Expect.Equal(4, fake.Stopped[4], "that one");
            Expect.Near(0f, Terrain.Height(1280f, 1280f), "and takes the raise back");
            Expect.Equal(Tile.LordaeronSummerDirt, Terrain.Tile(1280f, 1280f), "and the tile");
            Expect.True(Terrain.IsWalkable(1296f, 1296f), "and the pathing");
        }

        /// <summary>A radius of 0 is refused, and a raise in an ended scope is never made.</summary>
        public static void RaiseRefused(FakeWorld world)
        {
            var fake = Flat();
            var none = Terrain.Raise(0f, 0f, 0f, 100f, Game.Instance);
            Expect.True(none.HasEnded, "a radius of 0 gives an ended change");
            var holder = Game.Instance.Add(new Holder());
            holder.End();
            var late = Terrain.Raise(1280f, 1280f, 256f, 100f, holder);
            Expect.True(late.HasEnded, "ended at once");
            Expect.Equal(0, fake.Craters.Count, "no crater asked");
            Expect.Near(0f, Terrain.Height(1280f, 1280f), "the ground is flat");
        }

        /// <summary>Ripple, Wave and Shake ask the game and leave Height alone.</summary>
        public static void LookOnly(FakeWorld world)
        {
            var fake = Flat();
            Terrain.Ripple(1280f, 1280f, 400f, 30f, 2f);
            Terrain.Wave(0f, 0f, 1000f, 0f, 128f, 40f, 1f);
            Terrain.Shake(1280f, 1280f, 300f, 10f, 3f);
            Expect.Equal(3, fake.Played.Count, "three played");
            Expect.Equal("shake", fake.Played[2], "the last a shake");
            Expect.Near(0f, Terrain.Height(1280f, 1280f), "Height is flat");
        }

        /// <summary>Block takes walking away at once in the cells whose centres are in its circle, and its end gives it back.</summary>
        public static void Block(FakeWorld world)
        {
            var fake = Flat();
            Expect.True(Terrain.IsWalkable(1296f, 1296f), "walkable before");
            var holder = Game.Instance.Add(new Holder());
            Terrain.Block(1280f, 1280f, 64f, Pathing.Walk | Pathing.Build, holder);
            Expect.True(!Terrain.IsWalkable(1296f, 1296f), "not walkable at once");
            Expect.True(!Terrain.IsBuildable(1296f, 1296f), "not buildable");
            Expect.True(Terrain.IsFlyable(1296f, 1296f), "still flyable");
            Expect.True(Terrain.IsWalkable(1424f, 1296f), "a cell outside the circle");
            Expect.True(!fake.IsWalkable(1296f, 1296f), "the game was told");
            holder.End();
            Expect.True(Terrain.IsWalkable(1296f, 1296f), "walkable after");
            Expect.True(fake.IsWalkable(1296f, 1296f) && fake.IsBuildable(1296f, 1296f), "the game was told again");
        }

        /// <summary>The newest pathing change held wins, and ending one shows the one under it, or the ground before.</summary>
        public static void PathingNewestWins(FakeWorld world)
        {
            Flat();
            var wall = Terrain.Block(Bounds.FromCorners(new Vector2(1024f, 1024f), new Vector2(1536f, 1536f)), Pathing.Walk, Game.Instance);
            var gap = Terrain.Allow(1280f, 1280f, 64f, Pathing.Walk, Game.Instance);
            Expect.True(Terrain.IsWalkable(1296f, 1296f), "the gap is walkable");
            Expect.True(!Terrain.IsWalkable(1104f, 1104f), "the wall is not");
            gap.End();
            Expect.True(!Terrain.IsWalkable(1296f, 1296f), "the wall shows under the gap");
            var later = Terrain.Allow(1280f, 1280f, 64f, Pathing.Walk, Game.Instance);
            wall.End();
            Expect.True(Terrain.IsWalkable(1104f, 1104f), "the ground before the wall");
            later.End();
            Expect.True(Terrain.IsWalkable(1296f, 1296f), "the ground before both");
        }

        /// <summary>A cell the map was made unwalkable in, allowed and then ended, is unwalkable again.</summary>
        public static void PathingBefore(FakeWorld world)
        {
            var fake = new FakeTerrainWorld();
            fake.Block(1296f, 1296f, FakeTerrainWorld.NoWalk);
            var text = new StringBuilder();
            for (var k = 0; k < 20 * 20; k++)
            {
                text.Append(GridText.Corner(0f, 2));
            }

            Terrain.Install(fake, 20, 20, 0f, 0f, text.ToString());
            var open = Terrain.Allow(1296f, 1296f, 8f, Pathing.Walk, Game.Instance);
            Expect.True(Terrain.IsWalkable(1296f, 1296f), "allowed");
            open.End();
            Expect.True(!Terrain.IsWalkable(1296f, 1296f), "as the map made it");
        }

        /// <summary>SetTile paints the corners in its circle, the newest wins, and ending each shows what is under it, with the first tile's variation.</summary>
        public static void Tiles(FakeWorld world)
        {
            var fake = Flat();
            var holder = Game.Instance.Add(new Holder());
            Terrain.SetTile(1280f, 1280f, Tile.LordaeronSummerGrass, 150f, holder);
            Expect.Equal(Tile.LordaeronSummerGrass, Terrain.Tile(1280f, 1280f), "the centre");
            Expect.Equal(Tile.LordaeronSummerGrass, Terrain.Tile(1408f, 1280f), "a corner at 128");
            Expect.Equal(Tile.LordaeronSummerDirt, Terrain.Tile(1536f, 1280f), "a corner at 256");
            Expect.Equal(5, fake.Paints, "five corners painted");
            var snow = Terrain.SetTile(Bounds.FromCorners(new Vector2(1270f, 1270f), new Vector2(1290f, 1290f)), Tile.LordaeronSummerRock, Game.Instance);
            Expect.Equal(Tile.LordaeronSummerRock, Terrain.Tile(1280f, 1280f), "the newest wins");
            holder.End();
            Expect.Equal(Tile.LordaeronSummerRock, Terrain.Tile(1280f, 1280f), "the rock is held still");
            Expect.Equal(Tile.LordaeronSummerDirt, Terrain.Tile(1408f, 1280f), "the ground before");
            Expect.Equal(3, fake.VariationAt(1408f, 1280f), "with its variation");
            snow.End();
            Expect.Equal(Tile.LordaeronSummerDirt, Terrain.Tile(1280f, 1280f), "the ground before both");
        }

        /// <summary>A development build reports a tile the map's list lacks, and every build paints it all the same.</summary>
        public static void TileOutsideList(FakeWorld world)
        {
            Flat();
            Terrain.InstallTiles("LdrtLgrs");
            var before = world.Printed.Count;
            Terrain.SetTile(1280f, 1280f, Tile.LordaeronSummerGrass, 64f, Game.Instance);
            Expect.Equal(before, world.Printed.Count, "a tile in the list is not reported");
            Terrain.SetTile(1280f, 1280f, Tile.NorthrendSnow, 64f, Game.Instance);
#if DEBUG
            Expect.Equal(before + 1, world.Printed.Count, "one report");
            Expect.True(world.Printed[before].Contains("Nsnw"), "it names the tile: " + world.Printed[before]);
#else
            Expect.Equal(before, world.Printed.Count, "a release build reports nothing");
#endif
            Expect.Equal(Tile.NorthrendSnow, Terrain.Tile(1280f, 1280f), "painted all the same");
        }

        /// <summary>Ending a blight takes its blight off and makes the blight and cleanse changes still held again, oldest first.</summary>
        public static void Blight(FakeWorld world)
        {
            var fake = Flat();
            var wide = Terrain.Blight(1280f, 1280f, 400f, Game.Instance);
            Terrain.Cleanse(1280f, 1280f, 100f, Game.Instance);
            var rect = Terrain.Blight(Bounds.FromCorners(new Vector2(0f, 0f), new Vector2(256f, 128f)), Game.Instance);
            Expect.Equal(3, fake.Blights.Count, "three asked");
            Expect.Equal("off 1280 1280 100", fake.Blights[1], "the cleanse");
            Expect.Equal("on 0 0 256 128", fake.Blights[2], "the rect");
            rect.End();
            Expect.Equal("off 0 0 256 128", fake.Blights[3], "the rect's blight taken off");
            Expect.Equal("on 1280 1280 400", fake.Blights[4], "the wide blight again");
            Expect.Equal("off 1280 1280 100", fake.Blights[5], "then the cleanse");
            Expect.Equal(6, fake.Blights.Count, "nothing more");
            wide.End();
            Expect.Equal("off 1280 1280 400", fake.Blights[6], "the wide blight taken off");
            Expect.Equal("off 1280 1280 100", fake.Blights[7], "the cleanse again");
        }

        /// <summary>A cleanse whose centre stays blighted (a building's blight) is reported.</summary>
        public static void CleanseReported(FakeWorld world)
        {
            var fake = Flat();
            fake.Blighted = (x, y) => true;
            var before = world.Printed.Count;
            Terrain.Cleanse(1280f, 1280f, 300f, Game.Instance);
#if DEBUG
            Expect.Equal(before + 1, world.Printed.Count, "one report");
            Expect.True(world.Printed[before].Contains("building"), "it says why: " + world.Printed[before]);
#else
            Expect.Equal(before, world.Printed.Count, "a release build reports nothing");
#endif
        }

        /// <summary>The newest water tint held shows, and white when none is.</summary>
        public static void WaterTint(FakeWorld world)
        {
            var fake = Flat();
            var red = Terrain.WaterTint(new Color(255, 0, 0), Game.Instance);
            var blue = Terrain.WaterTint(new Color(0, 0, 255), Game.Instance);
            Expect.Equal("0 0 255 255", fake.Tints[1], "blue");
            red.End();
            Expect.Equal("0 0 255 255", fake.Tints[2], "blue still");
            blue.End();
            Expect.Equal("255 255 255 255", fake.Tints[3], "white");
        }

        /// <summary>Spread makes a change a quarter second's growth wider each step, ends the one before, and stops at the radius.</summary>
        public static void Spread(FakeWorld world)
        {
            var fake = Flat();
            var holder = Game.Instance.Add(new Holder());
            Terrain.Spread(100f, 200f, (radius, scope) => Terrain.Blight(1280f, 1280f, radius, scope), holder);
            Expect.Equal("on 1280 1280 50", fake.Blights[0], "a quarter second's growth at once");
            world.Advance(0.25f);
            Expect.Equal("on 1280 1280 100", fake.Blights[1], "the full radius");
            Expect.Equal("off 1280 1280 50", fake.Blights[2], "the one before ends");
            Expect.Equal("on 1280 1280 100", fake.Blights[3], "the one held is made again");
            var count = fake.Blights.Count;
            world.Advance(1f);
            Expect.Equal(count, fake.Blights.Count, "it stops at the radius");
            holder.End();
            Expect.Equal("off 1280 1280 100", fake.Blights[count], "the scope ends the last");
        }

        /// <summary>Trail lays a piece where its unit is, another once it moved 64, and each ends after its seconds.</summary>
        public static void Trail(FakeWorld world)
        {
            var fake = Flat();
            var handle = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            world.PlaceUnit(handle, 1280f, 1280f);
            var footman = Unit.Of(handle)!;
            var holder = Game.Instance.Add(new Holder());
            Terrain.Trail(footman, 1f, (x, y, scope) => Terrain.SetTile(x, y, Tile.LordaeronSummerGrass, 10f, scope), holder);
            Expect.Equal(Tile.LordaeronSummerGrass, Terrain.Tile(1280f, 1280f), "a piece at once");
            world.PlaceUnit(handle, 1300f, 1280f);
            world.Advance(0.25f);
            Expect.Equal(1, fake.Paints, "no piece for 20");
            world.PlaceUnit(handle, 1408f, 1280f);
            world.Advance(0.25f);
            Expect.Equal(Tile.LordaeronSummerGrass, Terrain.Tile(1408f, 1280f), "a piece for 128");
            world.Advance(0.6f);
            Expect.Equal(Tile.LordaeronSummerDirt, Terrain.Tile(1280f, 1280f), "the first ended after a second");
            Expect.Equal(Tile.LordaeronSummerGrass, Terrain.Tile(1408f, 1280f), "the second not yet");
            holder.End();
            Expect.Equal(Tile.LordaeronSummerDirt, Terrain.Tile(1408f, 1280f), "the scope ends the rest");
        }
    }
}
