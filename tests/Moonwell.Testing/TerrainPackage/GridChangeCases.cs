using System.Text;

namespace Moonwell.Testing
{
    /// <summary>Cases for the terrain changes over a grid of corners (decision 268), on a flat grid of 20 by 20 corners from (0, 0).</summary>
    public static class GridChangeCases
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

        /// <summary>A grid raise moves each corner by its own amount, from the corner nearest the point given, with a crater of one tile's radius a corner; 0 leaves a corner, and the end takes it all back.</summary>
        public static void Raise(FakeWorld world)
        {
            var fake = Flat();
            var holder = Game.Instance.Add(new Holder());
            Terrain.Raise(1290f, 1270f, 3, new[] { 100f, 0f, -50f, 20f, 40f, 60f }, holder);
            Expect.Near(100f, Terrain.Height(1280f, 1280f), "the first corner");
            Expect.Near(0f, Terrain.Height(1408f, 1280f), "a corner of 0 is left");
            Expect.Near(-50f, Terrain.Height(1536f, 1280f), "a negative amount lowers");
            Expect.Near(60f, Terrain.Height(1536f, 1408f), "the last corner, a row up");
            Expect.Near(50f, Terrain.Height(1344f, 1280f), "blended between corners");
            Expect.Near(0f, Terrain.Height(1152f, 1280f), "the corner beside the grid");
            Expect.Equal(5, fake.Craters.Count, "a crater a corner moved");
            Expect.Near(1280f, fake.Craters[0][0], "at the corner");
            Expect.Near(128f, fake.Craters[0][2], "of one tile's radius");
            Expect.Near(-100f, fake.Craters[0][3], "a raise is a crater of negative depth");
            var hill = Terrain.Raise(1280f, 1280f, 256f, 10f, Game.Instance);
            Expect.Near(110f, Terrain.Height(1280f, 1280f), "a single raise adds to it");
            holder.End();
            Expect.Near(10f, Terrain.Height(1280f, 1280f), "the single raise is left");
            Expect.Near(0f, Terrain.Height(1536f, 1408f), "the last corner after");
            Expect.Equal(6, fake.Craters.Count, "the end asks no crater");
            Expect.Equal(5, fake.Stopped.Count, "it stops its five");
            hill.End();
        }

        /// <summary>A grid of tiles paints a tile a corner and leaves a corner of 0; a change on top wins and its end shows the grid's tile; the grid's end shows the ground before.</summary>
        public static void Tiles(FakeWorld world)
        {
            var fake = Flat();
            var holder = Game.Instance.Add(new Holder());
            Terrain.SetTile(1280f, 1280f, 2, new[] { Tile.LordaeronSummerGrass, 0, Tile.LordaeronSummerRock, Tile.LordaeronSummerGrass }, holder);
            Expect.Equal(Tile.LordaeronSummerGrass, Terrain.Tile(1280f, 1280f), "the first corner");
            Expect.Equal(Tile.LordaeronSummerDirt, Terrain.Tile(1408f, 1280f), "a corner of 0 is left");
            Expect.Equal(Tile.LordaeronSummerRock, Terrain.Tile(1280f, 1408f), "the row above");
            Expect.Equal(3, fake.Paints, "three corners painted");
            var patch = Terrain.SetTile(1280f, 1280f, Tile.LordaeronSummerDarkGrass, 64f, Game.Instance);
            Expect.Equal(Tile.LordaeronSummerDarkGrass, Terrain.Tile(1280f, 1280f), "the newest wins");
            patch.End();
            Expect.Equal(Tile.LordaeronSummerGrass, Terrain.Tile(1280f, 1280f), "the grid's tile shows under it");
            var over = Terrain.SetTile(1408f, 1280f, Tile.LordaeronSummerDarkGrass, 64f, Game.Instance);
            holder.End();
            Expect.Equal(Tile.LordaeronSummerDirt, Terrain.Tile(1280f, 1280f), "the ground before");
            Expect.Equal(3, fake.VariationAt(1280f, 1408f), "with its variation");
            Expect.Equal(Tile.LordaeronSummerDarkGrass, Terrain.Tile(1408f, 1280f), "a change over a corner the grid left is still held");
            over.End();
            Expect.Equal(Tile.LordaeronSummerDirt, Terrain.Tile(1408f, 1280f), "and ends by itself");
        }

        /// <summary>A grid of pathing takes each corner's kinds in the 16 cells around it, leaves a corner of None, and its end gives them back.</summary>
        public static void Block(FakeWorld world)
        {
            var fake = Flat();
            var holder = Game.Instance.Add(new Holder());
            Terrain.Block(1280f, 1280f, 2, new[] { Pathing.Walk | Pathing.Build, Pathing.None }, holder);
            Expect.True(!Terrain.IsWalkable(1232f, 1232f), "the lower-left cell of the corner's square");
            Expect.True(!Terrain.IsWalkable(1328f, 1328f), "its upper-right cell");
            Expect.True(!Terrain.IsBuildable(1296f, 1296f), "building is taken too");
            Expect.True(Terrain.IsFlyable(1296f, 1296f), "flying is left");
            Expect.True(Terrain.IsWalkable(1360f, 1296f), "the square of the corner of None is left");
            Expect.True(Terrain.IsWalkable(1200f, 1296f), "a cell beside the grid");
            Expect.Equal(32, fake.PathSets, "16 cells, two kinds each");
            Expect.True(!fake.IsWalkable(1296f, 1296f), "the game was told");
            holder.End();
            Expect.True(Terrain.IsWalkable(1232f, 1232f) && Terrain.IsBuildable(1296f, 1296f), "given back");
            Expect.Equal(64, fake.PathSets, "and the game told again");
        }

        /// <summary>A save stops a grid raise's craters, one a corner, and the next moment or a load makes them again; a load paints a grid of tiles again; Height stays as it was (game facts, 11-11).</summary>
        public static void SaveAndLoad(FakeWorld world)
        {
            var fake = Flat();
            var holder = Game.Instance.Add(new Holder());
            Terrain.Raise(1280f, 1280f, 2, new[] { 100f, 0f, -50f, 20f }, holder);
            Terrain.SetTile(1280f, 1280f, 2, new[] { Tile.LordaeronSummerGrass, 0, 0, Tile.LordaeronSummerRock }, holder);
            Expect.Equal(3, fake.Craters.Count, "a crater a corner moved");

            Inbound.GameSaving();
            Expect.Equal(3, fake.Stopped.Count, "each is stopped for the save");
            Expect.Near(100f, Terrain.Height(1280f, 1280f), "Height reads the raise all along");
            world.Advance(0.01f);
            Expect.Equal(6, fake.Craters.Count, "and asked again the next moment");
            Expect.Near(-20f, fake.Craters[5][3], "the last with its own depth");

            // A game loaded from a save: as when the save was written, with the craters stopped and the map's own tiles.
            Inbound.GameSaving();
            Expect.Equal(6, fake.Stopped.Count, "stopped for a second save");
            fake.Tiles.Clear();
            fake.Variations.Clear();
            var paints = fake.Paints;
            Inbound.GameLoaded();
            Expect.Equal(9, fake.Craters.Count, "a load asks a crater a corner again");
            Expect.Near(100f, Terrain.Height(1280f, 1280f), "the first corner is as before the load");
            Expect.Near(-50f, Terrain.Height(1280f, 1408f), "the row above");
            Expect.Equal(Tile.LordaeronSummerGrass, Terrain.Tile(1280f, 1280f), "the first tile is painted again");
            Expect.Equal(Tile.LordaeronSummerRock, Terrain.Tile(1408f, 1408f), "and the last");
            Expect.Equal(paints + 2, fake.Paints, "two corners painted");
            world.Advance(0.01f);
            Expect.Equal(9, fake.Craters.Count, "the moment after the load asks nothing more");

            holder.End();
            Expect.Equal(9, fake.Stopped.Count, "the end stops the craters the load made");
            Expect.Near(0f, Terrain.Height(1280f, 1280f), "and takes the raise back");
            Expect.Equal(Tile.LordaeronSummerDirt, Terrain.Tile(1280f, 1280f), "and the tile");
        }

        /// <summary>A grid that is not rows of its columns is refused: the change comes back ended and the game is asked nothing; so is one made in an ended scope.</summary>
        public static void Refused(FakeWorld world)
        {
            var fake = Flat();
            Expect.True(Terrain.Raise(1280f, 1280f, 0, new[] { 1f, 2f }, Game.Instance).HasEnded, "no columns");
            Expect.True(Terrain.Raise(1280f, 1280f, 2, new[] { 1f, 2f, 3f }, Game.Instance).HasEnded, "not rows of 2");
            Expect.True(Terrain.SetTile(1280f, 1280f, 3, new int[0], Game.Instance).HasEnded, "an empty grid");
            Expect.True(Terrain.Block(1280f, 1280f, 4, new[] { Pathing.Walk }, Game.Instance).HasEnded, "less than a row");
            var holder = Game.Instance.Add(new Holder());
            holder.End();
            Expect.True(Terrain.Raise(1280f, 1280f, 1, new[] { 100f }, holder).HasEnded, "an ended scope");
            Expect.Equal(0, fake.Craters.Count + fake.Paints + fake.PathSets, "the game was asked nothing");
            Expect.Near(0f, Terrain.Height(1280f, 1280f), "the ground is flat");
        }

        /// <summary>Corners outside the map are left out, and the array is copied: changing it after the call changes nothing.</summary>
        public static void OutsideAndCopy(FakeWorld world)
        {
            var fake = Flat();
            var by = new[] { 10f, 20f, 30f };
            var raise = Terrain.Raise(-130f, 0f, 3, by, Game.Instance);
            Expect.Equal(2, fake.Craters.Count, "the corner outside the map is left out");
            Expect.Near(20f, Terrain.Height(0f, 0f), "the map's first corner");
            Expect.Near(30f, Terrain.Height(128f, 0f), "the next");
            by[1] = 500f;
            raise.End();
            Expect.Near(0f, Terrain.Height(0f, 0f), "the end takes off what was made, not what the array holds now");
            var grass = Tile.LordaeronSummerGrass;
            var tiles = Terrain.SetTile(2432f, 2432f, 2, new[] { grass, grass, grass, grass }, Game.Instance);
            Expect.Equal(1, fake.Paints, "one corner of four is in the map");
            Expect.Equal(grass, Terrain.Tile(2432f, 2432f), "the map's last corner");
            tiles.End();
        }
    }
}
