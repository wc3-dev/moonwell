namespace Moonwell
{
    /// <summary>
    /// A player's camera. Get one with <c>player.Camera()</c>. Where a camera looks and how is that machine's alone, so the
    /// reads (the target, the eye, the distance and the angles) are read inside <c>Local.For</c>, and the game gives them a
    /// moment late: after a pan or a move, the target reads the old place until the next frame. A read is this machine's
    /// camera whichever player's camera it is read from: inside <c>Local.For(Player.One, ...)</c>,
    /// <c>Player.Two.Camera().Distance</c> reads Player One's. The changes (PanTo, the
    /// Set methods, Follow, StopFollowing, Apply, Reset, SetBounds, ResetBounds, the locks, AllowHotkeyLock, PlayModel) move
    /// only that player's camera and run only on that player's machine, so they are safe anywhere. Shake and StopShake keep
    /// a timer, which is game state, so they stay outside Local.For. Angles are in degrees. No event comes when a camera
    /// moves. For a camera that follows a unit by itself, see ThirdPersonCamera and TopDownCamera.
    /// </summary>
    public sealed class Camera
    {
        /// <summary>Makes the camera of a slot.</summary>
        internal Camera(int slot)
        {
            Slot = slot;
        }

        /// <summary>The slot of the camera's player.</summary>
        internal int Slot { get; }

        /// <summary>The player's follow camera running now; null when none.</summary>
        internal FollowCamera? following;

        private bool zoomLocked;
        private bool rotationLocked;
        private Timer? shaking;

        /// <summary>The player of this camera.</summary>
        [LocalSafe]
        public Player Player => Player.Get(Slot);

        /// <summary>The x the camera looks at, on this machine. After a pan or a move it reads the old place until the next frame. This machine's alone, so it is read inside Local.For.</summary>
        public float TargetX
        {
            [MachineLocal]
            get { return InputSystem.World.CameraTargetX(); }
        }

        /// <summary>The y the camera looks at, on this machine. After a pan or a move it reads the old place until the next frame. This machine's alone, so it is read inside Local.For.</summary>
        public float TargetY
        {
            [MachineLocal]
            get { return InputSystem.World.CameraTargetY(); }
        }

        /// <summary>The z the camera looks at, on this machine. After a pan or a move it reads the old place until the next frame. This machine's alone, so it is read inside Local.For.</summary>
        public float TargetZ
        {
            [MachineLocal]
            get { return InputSystem.World.CameraTargetZ(); }
        }

        /// <summary>The place the camera looks at, on this machine, as a new Vector3 each time (three native reads). After a pan or a move it reads the old place until the next frame. This machine's alone, so it is read inside Local.For.</summary>
        public Vector3 Target
        {
            [MachineLocal]
            get { return new Vector3(TargetX, TargetY, TargetZ); }
        }

        /// <summary>The x of the camera's eye, on this machine. This machine's alone, so it is read inside Local.For.</summary>
        public float EyeX
        {
            [MachineLocal]
            get { return InputSystem.World.CameraEyeX(); }
        }

        /// <summary>The y of the camera's eye, on this machine. This machine's alone, so it is read inside Local.For.</summary>
        public float EyeY
        {
            [MachineLocal]
            get { return InputSystem.World.CameraEyeY(); }
        }

        /// <summary>The z of the camera's eye, on this machine. This machine's alone, so it is read inside Local.For.</summary>
        public float EyeZ
        {
            [MachineLocal]
            get { return InputSystem.World.CameraEyeZ(); }
        }

        /// <summary>The place of the camera's eye, on this machine, as a new Vector3 each time (three native reads). This machine's alone, so it is read inside Local.For.</summary>
        public Vector3 Eye
        {
            [MachineLocal]
            get { return new Vector3(EyeX, EyeY, EyeZ); }
        }

        /// <summary>The distance from the camera to its target, on this machine. This machine's alone, so it is read inside Local.For.</summary>
        public float Distance
        {
            [MachineLocal]
            get { return InputSystem.World.CameraField(CameraFields.Distance); }
        }

        /// <summary>The camera's angle of attack in degrees, on this machine (the game gives radians; this converts). This machine's alone, so it is read inside Local.For.</summary>
        public float AngleOfAttack
        {
            [MachineLocal]
            get { return Angles.ToDegrees(InputSystem.World.CameraField(CameraFields.AngleOfAttack)); }
        }

        /// <summary>The camera's rotation in degrees, on this machine (the game gives radians; this converts). This machine's alone, so it is read inside Local.For.</summary>
        public float Rotation
        {
            [MachineLocal]
            get { return Angles.ToDegrees(InputSystem.World.CameraField(CameraFields.Rotation)); }
        }

        /// <summary>The camera's roll in degrees, on this machine (the game gives radians; this converts). This machine's alone, so it is read inside Local.For.</summary>
        public float Roll
        {
            [MachineLocal]
            get { return Angles.ToDegrees(InputSystem.World.CameraField(CameraFields.Roll)); }
        }

        /// <summary>The camera's field of view in degrees, on this machine (the game gives radians; this converts). This machine's alone, so it is read inside Local.For.</summary>
        public float FieldOfView
        {
            [MachineLocal]
            get { return Angles.ToDegrees(InputSystem.World.CameraField(CameraFields.FieldOfView)); }
        }

        /// <summary>The far clipping distance, on this machine. This machine's alone, so it is read inside Local.For.</summary>
        public float FarZ
        {
            [MachineLocal]
            get { return InputSystem.World.CameraField(CameraFields.FarZ); }
        }

        /// <summary>The camera's height offset above the ground, on this machine. This machine's alone, so it is read inside Local.For.</summary>
        public float ZOffset
        {
            [MachineLocal]
            get { return InputSystem.World.CameraField(CameraFields.ZOffset); }
        }

        /// <summary>Pans the player's camera to a place over some seconds (0 is at once). It moves only that player's camera, so it is safe anywhere. A read of the target right after still gives the old place.</summary>
        [LocalSafe]
        public void PanTo(Vector2 position, float seconds = 0f)
        {
            InputSystem.World.PanCamera(Player.Handle, position.X, position.Y, seconds);
        }

        /// <summary>Pans the player's camera to a place over some seconds (0 is at once). It moves only that player's camera, so it is safe anywhere. A read of the target right after still gives the old place.</summary>
        [LocalSafe]
        public void PanTo(float x, float y, float seconds)
        {
            InputSystem.World.PanCamera(Player.Handle, x, y, seconds);
        }

        /// <summary>Sets the distance to the target over some seconds. It changes only that player's camera, so it is safe anywhere.</summary>
        [LocalSafe]
        public void SetDistance(float distance, float seconds = 0f)
        {
            InputSystem.World.SetField(Player.Handle, CameraFields.Distance, distance, seconds);
        }

        /// <summary>Sets the angle of attack, in degrees, over some seconds. It changes only that player's camera, so it is safe anywhere.</summary>
        [LocalSafe]
        public void SetAngleOfAttack(float degrees, float seconds = 0f)
        {
            InputSystem.World.SetField(Player.Handle, CameraFields.AngleOfAttack, degrees, seconds);
        }

        /// <summary>Sets the rotation, in degrees, over some seconds. It changes only that player's camera, so it is safe anywhere.</summary>
        [LocalSafe]
        public void SetRotation(float degrees, float seconds = 0f)
        {
            InputSystem.World.SetField(Player.Handle, CameraFields.Rotation, degrees, seconds);
        }

        /// <summary>Sets the roll, in degrees, over some seconds. It changes only that player's camera, so it is safe anywhere.</summary>
        [LocalSafe]
        public void SetRoll(float degrees, float seconds = 0f)
        {
            InputSystem.World.SetField(Player.Handle, CameraFields.Roll, degrees, seconds);
        }

        /// <summary>Sets the field of view, in degrees, over some seconds. It changes only that player's camera, so it is safe anywhere.</summary>
        [LocalSafe]
        public void SetFieldOfView(float degrees, float seconds = 0f)
        {
            InputSystem.World.SetField(Player.Handle, CameraFields.FieldOfView, degrees, seconds);
        }

        /// <summary>Sets the far clipping distance over some seconds. It changes only that player's camera, so it is safe anywhere.</summary>
        [LocalSafe]
        public void SetFarZ(float distance, float seconds = 0f)
        {
            InputSystem.World.SetField(Player.Handle, CameraFields.FarZ, distance, seconds);
        }

        /// <summary>Sets the height offset over some seconds. It changes only that player's camera, so it is safe anywhere.</summary>
        [LocalSafe]
        public void SetZOffset(float offset, float seconds = 0f)
        {
            InputSystem.World.SetField(Player.Handle, CameraFields.ZOffset, offset, seconds);
        }

        /// <summary>Locks the player's camera on a unit, so it moves with it, until StopFollowing or a pan. The offset is in the world's directions (x east, y north), whichever way the unit faces. It changes only that player's camera, so it is safe anywhere.</summary>
        [LocalSafe]
        public void Follow(Unit unit, Vector2 offset = default)
        {
            InputSystem.World.FollowUnit(Player.Handle, unit.Handle, offset.X, offset.Y);
        }

        /// <summary>Ends a lock on a unit by setting the camera's position to where it looks now. It changes only that player's camera, so it is safe anywhere.</summary>
        [LocalSafe]
        public void StopFollowing()
        {
            InputSystem.World.StopFollowing(Player.Handle);
        }

        /// <summary>Applies a camera World Editor placed (see CameraSetup.Named) over some seconds. It changes only that player's camera, so it is safe anywhere.</summary>
        [LocalSafe]
        public void Apply(CameraSetup setup, float seconds = 0f)
        {
            InputSystem.World.ApplySetup(Player.Handle, setup.Handle, seconds);
        }

        /// <summary>Puts the player's camera back to the game camera over some seconds. It changes only that player's camera, so it is safe anywhere.</summary>
        [LocalSafe]
        public void Reset(float seconds = 0f)
        {
            InputSystem.World.ResetCamera(Player.Handle, seconds);
        }

        /// <summary>Limits where the player's camera can go to these bounds. It changes only that player's camera, so it is safe anywhere.</summary>
        [LocalSafe]
        public void SetBounds(Bounds bounds)
        {
            InputSystem.World.SetBounds(Player.Handle, bounds.Min.X, bounds.Min.Y, bounds.Max.X, bounds.Max.Y);
        }

        /// <summary>Puts the player's camera bounds back to the map's. It changes only that player's camera, so it is safe anywhere.</summary>
        [LocalSafe]
        public void ResetBounds()
        {
            InputSystem.World.ResetBounds(Player.Handle);
        }

        /// <summary>Stops the player's mouse wheel from zooming, until UnlockZoom. It changes only that player's camera, so it is safe anywhere.</summary>
        [LocalSafe]
        public void LockZoom()
        {
            zoomLocked = true;
            InputSystem.World.SetInputControlled(Player.Handle, CameraFields.Distance, false);
        }

        /// <summary>Gives the zoom back to the player's mouse wheel. It changes only that player's camera, so it is safe anywhere.</summary>
        [LocalSafe]
        public void UnlockZoom()
        {
            zoomLocked = false;
            InputSystem.World.SetInputControlled(Player.Handle, CameraFields.Distance, true);
        }

        /// <summary>Stops the player's own input from turning the camera, until UnlockRotation. It changes only that player's camera, so it is safe anywhere.</summary>
        [LocalSafe]
        public void LockRotation()
        {
            rotationLocked = true;
            InputSystem.World.SetInputControlled(Player.Handle, CameraFields.Rotation, false);
        }

        /// <summary>Gives the rotation back to the player's own input. It changes only that player's camera, so it is safe anywhere.</summary>
        [LocalSafe]
        public void UnlockRotation()
        {
            rotationLocked = false;
            InputSystem.World.SetInputControlled(Player.Handle, CameraFields.Rotation, true);
        }

        /// <summary>Lets Ctrl+C lock the player's camera on the selected unit (the game's default), or not. It changes only that player's camera, so it is safe anywhere.</summary>
        [LocalSafe]
        public void AllowHotkeyLock(bool allow)
        {
            InputSystem.World.AllowHotkeyLock(Player.Handle, allow);
        }

        /// <summary>
        /// Shakes the player's camera with the game's earthquake noise (Blizzard.j's CameraSetEQNoiseForPlayer: 1 is gentle,
        /// 5 strong), for some seconds, or until StopShake when seconds is 0. A new shake replaces the one before. It keeps a
        /// timer, which is game state, so it is called outside Local.For.
        /// </summary>
        public void Shake(float strength, float seconds = 0f)
        {
            shaking?.End();
            shaking = null;
            InputSystem.World.Shake(Player.Handle, strength);
            if (seconds > 0f)
            {
                shaking = Game.Instance.OwnAfter(seconds, () =>
                {
                    shaking = null;
                    InputSystem.World.StopShake(Player.Handle);
                });
            }
        }

        /// <summary>Ends the player's camera shake. It ends the shake's timer, which is game state, so it is called outside Local.For.</summary>
        public void StopShake()
        {
            shaking?.End();
            shaking = null;
            InputSystem.World.StopShake(Player.Handle);
        }

        /// <summary>Plays a camera path from a model (one of the game's is <c>Objects\CinematicCameras\CameraZoomInMid.mdx</c>) at where the camera looks, until Reset. It changes only that player's camera, so it is safe anywhere.</summary>
        [LocalSafe]
        public void PlayModel(string path)
        {
            InputSystem.World.PlayModel(Player.Handle, path);
        }

        /// <summary>Gives the player's input what a follow camera took, as the camera's own locks say.</summary>
        internal void GiveInputBack()
        {
            InputSystem.World.SetInputControlled(Player.Handle, CameraFields.Distance, !zoomLocked);
            InputSystem.World.SetInputControlled(Player.Handle, CameraFields.Rotation, !rotationLocked);
            InputSystem.World.SetInputControlled(Player.Handle, CameraFields.AngleOfAttack, true);
        }
    }
}
