using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>Cases for 0.10's camera work (decisions 233 to 235): the new changes on a player's camera and a cinematic's, setups made in code, and the follow cameras.</summary>
    public static class CameraWorkCases
    {
        private static FakeInputWorld Fresh(FakeWorld world)
        {
            world.SetSlot(0, SlotState.Playing, Controller.User);
            world.SetSlot(1, SlotState.Playing, Controller.User);
            var fake = new FakeInputWorld();
            InputSystem.Install(fake, false, new string[0], new float[0]);
            return fake;
        }

        private static Unit Footman(FakeWorld world)
        {
            return Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo")))!;
        }

        /// <summary>The calls after a mark, as "kind slot", with the field code for "field" and "input" calls.</summary>
        private static List<string> Since(FakeInputWorld fake, int mark)
        {
            var list = new List<string>();
            for (var i = mark; i < fake.CameraCalls.Count; i++)
            {
                var call = fake.CameraCalls[i];
                var text = call.Kind + " " + call.Slot;
                if (call.Kind == "field" || call.Kind == "input")
                {
                    text += " " + call.Field;
                }

                if (call.Kind == "input")
                {
                    text += call.On ? " on" : " off";
                }

                list.Add(text);
            }

            return list;
        }

        private static void ExpectCalls(FakeInputWorld fake, int mark, string[] expected, string what)
        {
            var got = Since(fake, mark);
            Expect.Equal(string.Join(", ", expected), string.Join(", ", got), what);
        }

        /// <summary>The follow offset, the map's bounds, the locks, the hotkey lock and the model camera reach the world with the camera's player.</summary>
        public static void Changes(FakeWorld world)
        {
            var fake = Fresh(world);
            var one = Player.One.Camera();
            var footman = Footman(world);
            one.Follow(footman, new Vector2(200f, -50f));
            Expect.Near(200f, fake.CameraCalls[0].A, "the offset's x");
            Expect.Near(-50f, fake.CameraCalls[0].B, "the offset's y");
            one.Follow(footman);
            Expect.Near(0f, fake.CameraCalls[1].A, "no offset by default");
            Player.Two.Camera().ResetBounds();
            one.LockZoom();
            one.UnlockZoom();
            one.LockRotation();
            one.UnlockRotation();
            one.AllowHotkeyLock(false);
            one.PlayModel("Objects\\CinematicCameras\\CameraZoomInMid.mdx");
            ExpectCalls(fake, 2, new[] { "map-bounds 1", "input 0 0 off", "input 0 0 on", "input 0 5 off", "input 0 5 on", "hotkey 0", "model 0" }, "the changes");
            Expect.True(!fake.CameraCalls[7].On, "the hotkey lock stopped");
            Expect.Equal("Objects\\CinematicCameras\\CameraZoomInMid.mdx", fake.CameraCalls[8].Path, "the model's path");
        }

        /// <summary>A shake ends after its seconds; a new shake replaces the old one and its timer; 0 seconds lasts until StopShake.</summary>
        public static void Shake(FakeWorld world)
        {
            var fake = Fresh(world);
            var one = Player.One.Camera();
            one.Shake(3f, 2f);
            Expect.Equal("shake", fake.CameraCalls[0].Kind, "the shake");
            Expect.Near(3f, fake.CameraCalls[0].A, "its strength");
            world.Advance(1f);
            one.Shake(5f, 2f);
            world.Advance(1.5f);
            ExpectCalls(fake, 0, new[] { "shake 0", "shake 0" }, "the first shake's timer ended with it");
            world.Advance(1f);
            ExpectCalls(fake, 0, new[] { "shake 0", "shake 0", "unshake 0" }, "the second shake ends after its seconds");
            one.Shake(1f);
            world.Advance(30f);
            ExpectCalls(fake, 3, new[] { "shake 0" }, "0 seconds lasts");
            one.StopShake();
            ExpectCalls(fake, 3, new[] { "shake 0", "unshake 0" }, "until StopShake");
        }

        private static IEnumerable<Wait> LongScene(Cinematic cine)
        {
            yield return Wait.Seconds(10f);
        }

        /// <summary>A cinematic's shake, model camera and follow offset reach every watcher.</summary>
        public static void CinematicCamera(FakeWorld world)
        {
            var fake = Fresh(world);
            var cine = Cinematic.Play(LongScene, Game.Instance);
            var footman = Footman(world);
            cine.Camera().Shake(2f, 1f);
            cine.Camera().PlayModel("Objects\\CinematicCameras\\CameraZoomOut.mdx");
            cine.Camera().Follow(footman, new Vector2(10f, 20f));
            world.Advance(1.5f);
            cine.Camera().StopShake();
            ExpectCalls(fake, 0, new[] { "shake 0", "shake 1", "model 0", "model 1", "follow 0", "follow 1", "unshake 0", "unshake 1", "unshake 0", "unshake 1" }, "each watcher's");
        }

        /// <summary>A setup created in code holds the fields given, in the build's order, and no type; one created before Install is made when it runs.</summary>
        public static void Created(FakeWorld world)
        {
            var fake = Fresh(world);
            var setup = CameraSetup.Create(100f, 200f, distance: 900f, angle: 320f, rotation: 45f);
            Expect.Equal(1, fake.Made.Count, "made at once after Install");
            var made = fake.Made[0];
            Expect.Near(100f, made.X, "x");
            Expect.Near(200f, made.Y, "y");
            Expect.Equal(3, made.Codes.Count, "three fields");
            Expect.Equal(5, made.Codes[0], "rotation first, as the build writes them");
            Expect.Equal(2, made.Codes[1], "then the angle");
            Expect.Equal(0, made.Codes[2], "then the distance");
            Expect.Near(45f, made.Values[0], "the rotation");
            Expect.Near(320f, made.Values[1], "the angle");
            Expect.Near(900f, made.Values[2], "the distance");
            Expect.Equal(-1, made.Type, "no type");
            Expect.Equal("", setup.Name, "no name");
            Player.Two.Camera().Apply(setup, 2f);
            Expect.Same(made.Setup, fake.CameraCalls[0].Setup, "Apply uses it");

            InputSystem.Uninstall();
            var early = CameraSetup.Create(1f, 2f);
            var later = Fresh(world);
            Expect.Equal(1, later.Made.Count, "made when Install runs");
            Expect.Equal(0, later.Made[0].Codes.Count, "with no fields");
            Player.One.Camera().Apply(early);
            Expect.Same(later.Made[0].Setup, later.CameraCalls[0].Setup, "and applied");
        }

        /// <summary>
        /// A third-person camera takes the input, locks on the unit and puts the camera in place at once, then moves it each
        /// tick over the tick, turning the short way; its scope ending gives the camera and the input back as the camera's
        /// own locks say.
        /// </summary>
        public static void ThirdPerson(FakeWorld world)
        {
            var fake = Fresh(world);
            var footman = Footman(world);
            footman.FaceNow(350f);
            Player.One.Camera().LockZoom();
            var scope = new TestScope("view", new List<string>());
            var mark = fake.CameraCalls.Count;
            var view = ThirdPersonCamera.Start(Player.One, footman, scope, distance: 500f, height: 100f, angle: 340f);
            ExpectCalls(fake, mark, new[] { "input 0 0 off", "input 0 5 off", "input 0 2 off", "follow 0", "field 0 5", "field 0 2", "field 0 0", "field 0 6" }, "the start");
            Expect.Near(350f, fake.CameraCalls[mark + 4].A, "the rotation is the facing");
            Expect.Near(0f, fake.CameraCalls[mark + 4].Seconds, "at once");
            Expect.Near(500f, fake.CameraCalls[mark + 6].A, "the distance");
            Expect.Near(100f, fake.CameraCalls[mark + 7].A, "the height");
            footman.FaceNow(10f);
            mark = fake.CameraCalls.Count;
            world.Advance(0.03f);
            ExpectCalls(fake, mark, new[] { "field 0 5", "field 0 2", "field 0 0", "field 0 6" }, "a tick");
            Expect.Near(370f, fake.CameraCalls[mark].A, "350 to 10 turns 20 degrees forward");
            Expect.Near(0.03f, fake.CameraCalls[mark].Seconds, "over the tick");
            view.Distance = 800f;
            mark = fake.CameraCalls.Count;
            world.Advance(0.03f);
            Expect.Near(800f, fake.CameraCalls[mark + 2].A, "a new distance");
            mark = fake.CameraCalls.Count;
            scope.End();
            ExpectCalls(fake, mark, new[] { "stop 0", "reset 0", "input 0 0 off", "input 0 5 on", "input 0 2 on" }, "the end gives back what the locks say");
            mark = fake.CameraCalls.Count;
            world.Advance(1f);
            ExpectCalls(fake, mark, new string[0], "nothing after the end");
        }

        /// <summary>A second follow camera for a player ends the first; a top-down camera keeps its rotation; one ends with its unit; one started in an ended scope changes nothing.</summary>
        public static void TopDown(FakeWorld world)
        {
            var fake = Fresh(world);
            var footman = Footman(world);
            var first = ThirdPersonCamera.Start(Player.Two, footman, Game.Instance);
            var mark = fake.CameraCalls.Count;
            var view = TopDownCamera.Start(Player.Two, footman, Game.Instance, rotation: 60f);
            Expect.True(first.HasEnded, "the first ended");
            ExpectCalls(fake, mark, new[] { "stop 1", "reset 1", "input 1 0 on", "input 1 5 on", "input 1 2 on", "input 1 0 off", "input 1 5 off", "input 1 2 off", "follow 1", "field 1 5", "field 1 2", "field 1 0", "field 1 6" }, "the first's end, then the second's start");
            Expect.Near(60f, fake.CameraCalls[mark + 9].A, "the rotation");
            Expect.Near(1650f, fake.CameraCalls[mark + 11].A, "World Editor's distance");
            footman.FaceNow(180f);
            mark = fake.CameraCalls.Count;
            world.Advance(0.03f);
            Expect.Near(60f, fake.CameraCalls[mark].A, "the facing does not turn it");
            footman.Remove();
            world.Advance(0.03f);
            Expect.True(view.HasEnded, "it ends with its unit");
            var ended = new TestScope("gone", new List<string>());
            ended.End();
            mark = fake.CameraCalls.Count;
            var none = ThirdPersonCamera.Start(Player.One, Footman(world), ended);
            Expect.True(none.HasEnded, "in an ended scope it ends at once");
            ExpectCalls(fake, mark, new string[0], "and changes nothing");
        }

        /// <summary>A follow camera stops following while its player watches a cinematic and starts again after; another player's cinematic leaves it be; ended during a cinematic, it leaves the camera to the cinematic.</summary>
        public static void Paused(FakeWorld world)
        {
            var fake = Fresh(world);
            var footman = Footman(world);
            ThirdPersonCamera.Start(Player.One, footman, Game.Instance);
            Cinematic.Play(LongScene, Game.Instance, shownTo: Player.Two);
            var mark = fake.CameraCalls.Count;
            world.Advance(0.03f);
            ExpectCalls(fake, mark, new[] { "field 0 5", "field 0 2", "field 0 0", "field 0 6" }, "another player's cinematic");
            var cine = Cinematic.Play(LongScene, Game.Instance, shownTo: Player.One);
            mark = fake.CameraCalls.Count;
            world.Advance(0.03f);
            world.Advance(0.03f);
            ExpectCalls(fake, mark, new[] { "stop 0" }, "paused while watching");
            cine.End();
            mark = fake.CameraCalls.Count;
            world.Advance(0.03f);
            ExpectCalls(fake, mark, new[] { "input 0 0 off", "input 0 5 off", "input 0 2 off", "follow 0", "field 0 5", "field 0 2", "field 0 0", "field 0 6" }, "taken again after");
            var scope = new TestScope("view", new List<string>());
            var view = ThirdPersonCamera.Start(Player.One, footman, scope);
            Cinematic.Play(LongScene, Game.Instance, shownTo: Player.One);
            world.Advance(0.03f);
            mark = fake.CameraCalls.Count;
            scope.End();
            ExpectCalls(fake, mark, new[] { "input 0 0 on", "input 0 5 on", "input 0 2 on" }, "no reset under a cinematic");
            Expect.True(view.HasEnded, "ended");
        }
    }
}
