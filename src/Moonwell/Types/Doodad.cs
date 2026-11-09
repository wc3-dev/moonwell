namespace Moonwell
{
    /// <summary>A doodad type: data only, since the game gives no handle to a doodad, so it is not an actor. Code can only play an animation on the doodads of a type in a place.</summary>
    public class Doodad
    {
        /// <summary>Plays an animation ("death", "birth", or any name the model has) on the doodads of a type within a radius of a place, or on the nearest one only (SetDoodadAnimation). A name the model lacks plays its stand.</summary>
        public static void PlayAnimation(int typeId, float x, float y, float radius, string name, bool nearestOnly = false)
        {
            Runtime.World.AnimateDoodads(typeId, x, y, radius, nearestOnly, name);
        }

        /// <summary>Plays an animation on the doodads of a registered class within a radius of a place, or on the nearest one only.</summary>
        public static void PlayAnimation<T>(float x, float y, float radius, string name, bool nearestOnly = false)
            where T : Doodad
        {
            PlayAnimation(ObjectTypes.IdOf<T>(), x, y, radius, name, nearestOnly);
        }

        /// <summary>Plays an animation on the doodads of a type inside a rect (SetDoodadAnimationRect).</summary>
        public static void PlayAnimationIn(int typeId, float left, float bottom, float right, float top, string name)
        {
            Runtime.World.AnimateDoodadsIn(typeId, left, bottom, right, top, name);
        }

        /// <summary>Plays an animation on the doodads of a registered class inside a rect.</summary>
        public static void PlayAnimationIn<T>(float left, float bottom, float right, float top, string name)
            where T : Doodad
        {
            PlayAnimationIn(ObjectTypes.IdOf<T>(), left, bottom, right, top, name);
        }
    }
}
