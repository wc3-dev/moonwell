namespace Moonwell.Hosting
{
    /// <summary>The game as the terrain package sees it: the ground's height, the four pathing flags, the item trick that finds where a unit would land, the playable area, and the changes a map makes to the ground. Types are the game's numbers.</summary>
    internal interface ITerrainWorld
    {
        /// <summary>The height of the ground at a point as the game reads it, deformations included. The game can answer differently on each machine, so a map reads it only inside <c>Local.For</c>.</summary>
        [MachineLocal]
        float LocationZ(float x, float y);

        /// <summary>Whether units can walk at a point.</summary>
        bool IsWalkable(float x, float y);

        /// <summary>Whether flying units can fly at a point.</summary>
        bool IsFlyable(float x, float y);

        /// <summary>Whether a building can be built at a point.</summary>
        bool IsBuildable(float x, float y);

        /// <summary>Whether ships can float at a point.</summary>
        bool IsFloatable(float x, float y);

        /// <summary>Puts an item at a point and reads back where the game left it: the nearest place an item can lie.</summary>
        void Place(float x, float y, out float landedX, out float landedY);

        /// <summary>The playable area: the map's bounds less the camera's margins.</summary>
        void PlayableArea(out float minX, out float minY, out float maxX, out float maxY);

        /// <summary>Makes a lasting crater: the ground at a corner a distance d from the centre goes down by depth x cos(90 degrees x d / radius) inside the radius, from the next moment. A negative depth raises it.</summary>
        void Crater(float x, float y, float radius, float depth);

        /// <summary>Plays rings that run out from a point over some seconds; the ground is as before after.</summary>
        void Ripple(float x, float y, float radius, float depth, float seconds);

        /// <summary>Plays a wave that runs from one point to another over some seconds; the ground is as before after.</summary>
        void Wave(float fromX, float fromY, float toX, float toY, float radius, float depth, float seconds);

        /// <summary>Shakes the ground in a circle, each moment by up to so much up or down, for some seconds; the ground is as before after.</summary>
        void Shake(float x, float y, float radius, float by, float seconds);

        /// <summary>The tile at the corner nearest a point, its four-letter id as a number.</summary>
        int TileAt(float x, float y);

        /// <summary>The variation of the tile at the corner nearest a point.</summary>
        int VariationAt(float x, float y);

        /// <summary>Paints the one corner nearest a point with a tile and a variation (-1 for any).</summary>
        void PaintTile(float x, float y, int tile, int variation);

        /// <summary>Turns one kind of pathing on or off in the 32 by 32 cell that holds a point: walkable 1, flyable 2, buildable 4, floatable 8.</summary>
        void SetPathing(float x, float y, int kind, bool on);

        /// <summary>Adds or removes blight in a circle; removing leaves blight a building made.</summary>
        void SetBlight(float x, float y, float radius, bool on);

        /// <summary>Adds or removes blight in a rect, by its edges; removing leaves blight a building made.</summary>
        void SetBlight(float minX, float minY, float maxX, float maxY, bool on);

        /// <summary>Whether a point is blighted.</summary>
        bool IsBlighted(float x, float y);

        /// <summary>Tints the map's water by a colour, 0 to 255 each: white leaves it as it started.</summary>
        void TintWater(int red, int green, int blue, int alpha);
    }
}
