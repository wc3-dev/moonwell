using System;

namespace Moonwell
{
    /// <summary>
    /// The cameras of a cinematic's watchers, from <c>cine.Camera()</c>: each change goes to every person watching, as their
    /// own camera's does, so it is safe anywhere. Reads are each player's own camera's, inside <c>Local.For</c>.
    /// </summary>
    public sealed class CinematicCamera
    {
        private readonly Cinematic cinematic;

        internal CinematicCamera(Cinematic cinematic)
        {
            this.cinematic = cinematic;
        }

        /// <summary>Pans the watchers' cameras to a place over some seconds (0 is at once).</summary>
        [LocalSafe]
        public void PanTo(Vector2 position, float seconds = 0f) => Each(camera => camera.PanTo(position, seconds));

        /// <summary>Pans the watchers' cameras to a place over some seconds (0 is at once).</summary>
        [LocalSafe]
        public void PanTo(float x, float y, float seconds) => Each(camera => camera.PanTo(x, y, seconds));

        /// <summary>Sets the distance to the target over some seconds.</summary>
        [LocalSafe]
        public void SetDistance(float distance, float seconds = 0f) => Each(camera => camera.SetDistance(distance, seconds));

        /// <summary>Sets the angle of attack, in degrees, over some seconds.</summary>
        [LocalSafe]
        public void SetAngleOfAttack(float degrees, float seconds = 0f) => Each(camera => camera.SetAngleOfAttack(degrees, seconds));

        /// <summary>Sets the rotation, in degrees, over some seconds.</summary>
        [LocalSafe]
        public void SetRotation(float degrees, float seconds = 0f) => Each(camera => camera.SetRotation(degrees, seconds));

        /// <summary>Sets the roll, in degrees, over some seconds.</summary>
        [LocalSafe]
        public void SetRoll(float degrees, float seconds = 0f) => Each(camera => camera.SetRoll(degrees, seconds));

        /// <summary>Sets the field of view, in degrees, over some seconds.</summary>
        [LocalSafe]
        public void SetFieldOfView(float degrees, float seconds = 0f) => Each(camera => camera.SetFieldOfView(degrees, seconds));

        /// <summary>Sets the far clipping distance over some seconds.</summary>
        [LocalSafe]
        public void SetFarZ(float distance, float seconds = 0f) => Each(camera => camera.SetFarZ(distance, seconds));

        /// <summary>Sets the height offset above the ground over some seconds.</summary>
        [LocalSafe]
        public void SetZOffset(float offset, float seconds = 0f) => Each(camera => camera.SetZOffset(offset, seconds));

        /// <summary>Locks the watchers' cameras on a unit, with an offset in the world's directions, until StopFollowing or a pan.</summary>
        [LocalSafe]
        public void Follow(Unit unit, Vector2 offset = default) => Each(camera => camera.Follow(unit, offset));

        /// <summary>Ends a lock on a unit.</summary>
        [LocalSafe]
        public void StopFollowing() => Each(camera => camera.StopFollowing());

        /// <summary>Applies a camera World Editor placed (see CameraSetup.Named) over some seconds.</summary>
        [LocalSafe]
        public void Apply(CameraSetup setup, float seconds = 0f) => Each(camera => camera.Apply(setup, seconds));

        /// <summary>Puts the watchers' cameras back to the game camera over some seconds: in the cinematic's Ended, after a skip too.</summary>
        [LocalSafe]
        public void Reset(float seconds = 0f) => Each(camera => camera.Reset(seconds));

        /// <summary>Limits where the watchers' cameras can go to these bounds.</summary>
        [LocalSafe]
        public void SetBounds(Bounds bounds) => Each(camera => camera.SetBounds(bounds));

        /// <summary>Shakes the watchers' cameras for some seconds, or until StopShake when seconds is 0 (see Camera.Shake).</summary>
        public void Shake(float strength, float seconds = 0f) => Each(camera => camera.Shake(strength, seconds));

        /// <summary>Ends the watchers' camera shake.</summary>
        public void StopShake() => Each(camera => camera.StopShake());

        /// <summary>Plays a camera path from a model on the watchers' cameras, until Reset (see Camera.PlayModel).</summary>
        [LocalSafe]
        public void PlayModel(string path) => Each(camera => camera.PlayModel(path));

        private void Each(Action<Camera> change)
        {
            foreach (var person in Player.Users)
            {
                if (cinematic.IsShownTo(person))
                {
                    change(person.Camera());
                }
            }
        }
    }
}
