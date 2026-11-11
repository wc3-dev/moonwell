using Moonwell.Hosting;

namespace Moonwell.Engine
{
    /// <summary>
    /// The game as the terrain package sees it, through the game's natives; the only class of the package that calls
    /// them. The location behind <c>LocationZ</c> is made when the world is made, which is in <c>Terrain.Install</c>, in
    /// synced code: <c>LocationZ</c> is machine-local, a map calls it only inside <c>Local.For</c>, and making a handle
    /// there would make it on one machine only.
    /// </summary>
    internal sealed class NativeTerrainWorld : ITerrainWorld
    {
        /// <summary>The location LocationZ moves about: one handle, made at install, for the map's life.</summary>
        private readonly location spot;

        /// <summary>The item Place puts down, made on first use.</summary>
        private item? marker;

        /// <summary>Makes the world, and the location LocationZ moves about.</summary>
        internal NativeTerrainWorld()
        {
            spot = Natives.Location(0f, 0f);
        }

        /// <summary>The ground's height at a point, as the game gives it on this machine.</summary>
        public float LocationZ(float x, float y)
        {
            Natives.MoveLocation(spot, x, y);
            return Natives.GetLocationZ(spot);
        }

        /// <summary>Whether units can walk at a point: the game says whether the pathing type is off there, so the answer is turned round.</summary>
        public bool IsWalkable(float x, float y) => !Natives.IsTerrainPathable(x, y, Natives.PATHING_TYPE_WALKABILITY);

        /// <summary>Whether flying units can fly at a point.</summary>
        public bool IsFlyable(float x, float y) => !Natives.IsTerrainPathable(x, y, Natives.PATHING_TYPE_FLYABILITY);

        /// <summary>Whether a building can be built at a point.</summary>
        public bool IsBuildable(float x, float y) => !Natives.IsTerrainPathable(x, y, Natives.PATHING_TYPE_BUILDABILITY);

        /// <summary>Whether ships can float at a point.</summary>
        public bool IsFloatable(float x, float y) => !Natives.IsTerrainPathable(x, y, Natives.PATHING_TYPE_FLOATABILITY);

        /// <summary>Puts one item down at the point, shown for a moment, and reads where the game left it.</summary>
        public void Place(float x, float y, out float landedX, out float landedY)
        {
            if (marker == null)
            {
                marker = Natives.CreateItem(ObjectTypes.FourCC("wolg"), x, y);
            }

            Natives.SetItemVisible(marker, true);
            Natives.SetItemPosition(marker, x, y);
            landedX = Natives.GetItemX(marker);
            landedY = Natives.GetItemY(marker);
            Natives.SetItemVisible(marker, false);
        }

        /// <summary>The playable area, as the game's own bj_mapInitialPlayableArea is made: the camera's bounds, less the camera's margins.</summary>
        public void PlayableArea(out float minX, out float minY, out float maxX, out float maxY)
        {
            minX = Natives.GetCameraBoundMinX() - Natives.GetCameraMargin(Natives.CAMERA_MARGIN_LEFT);
            minY = Natives.GetCameraBoundMinY() - Natives.GetCameraMargin(Natives.CAMERA_MARGIN_BOTTOM);
            maxX = Natives.GetCameraBoundMaxX() + Natives.GetCameraMargin(Natives.CAMERA_MARGIN_RIGHT);
            maxY = Natives.GetCameraBoundMaxY() + Natives.GetCameraMargin(Natives.CAMERA_MARGIN_TOP);
        }

        /// <summary>A lasting crater of the game's, made at once (duration 0).</summary>
        public object Crater(float x, float y, float radius, float depth)
        {
            return Natives.TerrainDeformCrater(x, y, radius, depth, 0, true);
        }

        /// <summary>Stops a crater at once: a settled one of 300 read 0 a second after (game facts, 11-11).</summary>
        public void StopCrater(object crater)
        {
            Natives.TerrainDeformStop((terraindeformation)crater, 0);
        }

        /// <summary>One ripple, as Blizzard.j's TerrainDeformationRippleBJ makes it from the centre out, with waves 128 wide passing twice a second.</summary>
        public void Ripple(float x, float y, float radius, float depth, float seconds)
        {
            Natives.TerrainDeformRipple(x, y, radius, depth, Slots.Whole(seconds * 1000f), 1, 2f * radius / 128f, 2f * seconds / 0.5f, 0f, false);
        }

        /// <summary>A wave from one point to another, as Blizzard.j's TerrainDeformationWaveBJ, with no trail.</summary>
        public void Wave(float fromX, float fromY, float toX, float toY, float radius, float depth, float seconds)
        {
            var dx = toX - fromX;
            var dy = toY - fromY;
            var distance = Maths.Sqrt(dx * dx + dy * dy);
            Natives.TerrainDeformWave(fromX, fromY, dx, dy, distance, distance / seconds, radius, depth, 0, 1);
        }

        /// <summary>The game's random deformation, changing every 50 ms.</summary>
        public void Shake(float x, float y, float radius, float by, float seconds)
        {
            Natives.TerrainDeformRandom(x, y, radius, -by, by, Slots.Whole(seconds * 1000f), 50);
        }

        /// <summary>The tile at the nearest corner.</summary>
        public int TileAt(float x, float y) => Natives.GetTerrainType(x, y);

        /// <summary>The tile's variation at the nearest corner.</summary>
        public int VariationAt(float x, float y) => Natives.GetTerrainVariance(x, y);

        /// <summary>Paints the nearest corner alone: area 1, as area 0 paints nothing (probe, 11-09).</summary>
        public void PaintTile(float x, float y, int tile, int variation)
        {
            Natives.SetTerrainType(x, y, tile, variation, 1, 0);
        }

        /// <summary>Sets one kind of pathing in a cell.</summary>
        public void SetPathing(float x, float y, int kind, bool on)
        {
            var type = kind == 1 ? Natives.PATHING_TYPE_WALKABILITY : kind == 2 ? Natives.PATHING_TYPE_FLYABILITY : kind == 4 ? Natives.PATHING_TYPE_BUILDABILITY : Natives.PATHING_TYPE_FLOATABILITY;
            Natives.SetTerrainPathable(x, y, type, on);
        }

        /// <summary>Blight in a circle; the player only says whose blight it is to the game, and changes nothing seen (probe, 11-09).</summary>
        public void Blight(float x, float y, float radius, bool on)
        {
            Natives.SetBlight(Natives.Player(0), x, y, radius, on);
        }

        /// <summary>Blight in a rect, through a rect made for the call.</summary>
        public void Blight(float minX, float minY, float maxX, float maxY, bool on)
        {
            var area = Natives.Rect(minX, minY, maxX, maxY);
            Natives.SetBlightRect(Natives.Player(0), area, on);
            Natives.RemoveRect(area);
        }

        /// <summary>Whether a point is blighted.</summary>
        public bool IsBlighted(float x, float y) => Natives.IsPointBlighted(x, y);

        /// <summary>The game's water tint.</summary>
        public void TintWater(int red, int green, int blue, int alpha)
        {
            Natives.SetWaterBaseColor(red, green, blue, alpha);
        }
    }
}
