namespace Moonwell.Testing
{
    /// <summary>Cases that check special effects: made at a point or on a unit, their looks, and how they end.</summary>
    public static class EffectCases
    {
        private const string Glow = "Abilities\\Spells\\Human\\HolySpell\\HolyLight.mdl";

        /// <summary>A game component that holds effects, ended by the case.</summary>
        private sealed class Holder : GameComponent
        {
        }

        /// <summary>Create asks the game once for the model at the place, and keeps the handle and the model.</summary>
        public static void Create(FakeWorld world)
        {
            var fx = SpecialEffect.Create(Glow, 100f, 200f, Game.Instance);
            Expect.Equal(1, world.Effects.Count, "effects made");
            var made = world.Effects[0];
            Expect.Equal(Glow, made.Model, "the model given to the game");
            Expect.Near(100f, made.X, "the x given to the game");
            Expect.Near(200f, made.Y, "the y given to the game");
            Expect.True(fx.Handle != null, "the handle is kept");
            Expect.Same(made, world.EffectOf(fx.Handle!), "the handle is the game's");
            Expect.Equal(Glow, fx.Model, "the model reads the path");
            Expect.Equal(true, fx.PlaysDeath, "it plays its death unless set");
        }

        /// <summary>An effect left to play its death ends with DestroyEffect.</summary>
        public static void EndPlaysDeath(FakeWorld world)
        {
            var fx = SpecialEffect.Create(Glow, 0f, 0f, Game.Instance);
            fx.End();
            var made = world.Effects[0];
            Expect.Equal(true, made.Ended, "ended in the game");
            Expect.Equal(true, made.Death, "with its death");
            Expect.Equal(true, fx.HasEnded, "ended");
        }

        /// <summary>An effect whose death is turned off vanishes at once.</summary>
        public static void EndAtOnce(FakeWorld world)
        {
            var fx = SpecialEffect.Create(Glow, 0f, 0f, Game.Instance);
            fx.PlaysDeath = false;
            fx.End();
            var made = world.Effects[0];
            Expect.Equal(true, made.Ended, "ended in the game");
            Expect.Equal(false, made.Death, "without its death");
        }

        /// <summary>An effect ends when the scope it was made in ends, with the death it was set to play.</summary>
        public static void ScopeEnd(FakeWorld world)
        {
            var holder = Game.Instance.Add(new Holder());
            var fx = SpecialEffect.Create(Glow, 0f, 0f, holder);
            fx.PlaysDeath = false;
            holder.End();
            Expect.Equal(true, fx.HasEnded, "ended with its scope");
            Expect.Equal(true, world.Effects[0].Ended, "ended in the game");
            Expect.Equal(false, world.Effects[0].Death, "the setting holds when the scope ends it");
        }

        /// <summary>An effect made in a scope that has ended ends at once, and the game is never asked to make it.</summary>
        public static void EndedScope(FakeWorld world)
        {
            var holder = Game.Instance.Add(new Holder());
            holder.End();
            var fx = SpecialEffect.Create(Glow, 0f, 0f, holder);
            Expect.Equal(true, fx.HasEnded, "ended at once");
            Expect.Equal(0, world.Effects.Count, "effects made");
            Expect.True(fx.Handle == null, "no handle");
        }

        /// <summary>X and Y read what was made and set; a set moves the game's effect, on the ground.</summary>
        public static void Place(FakeWorld world)
        {
            var fx = SpecialEffect.Create(Glow, 100f, 200f, Game.Instance);
            Expect.Near(100f, fx.X, "x as made");
            Expect.Near(200f, fx.Y, "y as made");
            Expect.Near(0f, fx.Height, "height unless set");
            fx.X = 300f;
            var made = world.Effects[0];
            Expect.Near(300f, made.X, "the game's x");
            Expect.Near(200f, made.Y, "the game's y");
            Expect.Near(0f, made.Z, "the game's height, on the ground");
            Expect.Near(300f, fx.X, "x as set");
            fx.Y = -50f;
            Expect.Near(-50f, made.Y, "the game's y after a set");
        }

        /// <summary>Height is above the ground, and stays so as the effect moves over higher ground.</summary>
        public static void HeightFollowsGround(FakeWorld world)
        {
            world.Ground = (x, y) => x > 500f ? 200f : 0f;
            var fx = SpecialEffect.Create(Glow, 0f, 0f, Game.Instance);
            fx.Height = 50f;
            var made = world.Effects[0];
            Expect.Near(50f, made.Z, "50 above flat ground");
            fx.X = 600f;
            Expect.Near(250f, made.Z, "50 above the hill");
            Expect.Near(50f, fx.Height, "the height reads what was set");
        }

        /// <summary>PlaceAt moves in one call, reading the ground once.</summary>
        public static void PlaceAt(FakeWorld world)
        {
            world.Ground = (x, y) => x > 500f ? 200f : 0f;
            var fx = SpecialEffect.Create(Glow, 0f, 0f, Game.Instance);
            var reads = world.GroundReads;
            var places = world.Effects[0].Places;
            fx.PlaceAt(600f, 10f);
            var made = world.Effects[0];
            Expect.Equal(reads + 1, world.GroundReads, "one ground read");
            Expect.Equal(places + 1, made.Places, "one move");
            Expect.Near(600f, made.X, "the game's x");
            Expect.Near(10f, made.Y, "the game's y");
            Expect.Near(200f, made.Z, "on the hill");
            Expect.Near(600f, fx.X, "x");
            Expect.Near(10f, fx.Y, "y");
        }

        /// <summary>After the end, setting asks nothing of the game, and reads give the last values.</summary>
        public static void AfterEnd(FakeWorld world)
        {
            var fx = SpecialEffect.Create(Glow, 100f, 0f, Game.Instance);
            fx.Height = 30f;
            fx.End();
            var made = world.Effects[0];
            var places = made.Places;
            fx.X = 9f;
            fx.Y = 9f;
            fx.Height = 1f;
            fx.PlaceAt(5f, 5f);
            fx.Facing = 90f;
            fx.Pitch = 10f;
            fx.Roll = 10f;
            fx.Scale = 2f;
            fx.AnimationSpeed = 2f;
            fx.PlayAnimation(Animation.Attack);
            fx.QueueAnimation(Animation.Stand);
            fx.PlaysDeath = false;
            fx.End();
            Expect.Equal(places, made.Places, "no move after the end");
            Expect.Near(100f, fx.X, "the last x");
            Expect.Near(30f, fx.Height, "the last height");
            Expect.Near(0f, fx.Facing, "the last facing");
            Expect.Near(1f, fx.Scale, "the last scale");
            Expect.Near(1f, fx.AnimationSpeed, "the last speed");
            Expect.Equal(true, fx.PlaysDeath, "the last setting");
            Expect.Equal(true, made.Death, "it ended with its death");
        }

        /// <summary>Facing reads from 0 to 360, as a unit's does; Pitch and Roll read as they were set.</summary>
        public static void FacingWraps(FakeWorld world)
        {
            var fx = SpecialEffect.Create(Glow, 0f, 0f, Game.Instance);
            fx.Facing = 400f;
            Expect.Near(40f, fx.Facing, "400 reads 40");
            Expect.Near(0.6981317f, world.Effects[0].Yaw, "the game's yaw for 40");
            fx.Facing = -90f;
            Expect.Near(270f, fx.Facing, "-90 reads 270");
            fx.Pitch = -30f;
            Expect.Near(-30f, fx.Pitch, "a pitch reads as set");
        }

        /// <summary>An effect put on a unit in a scope that has ended ends at once, and the game is not asked to make it.</summary>
        public static void OnUnitEndedScope(FakeWorld world)
        {
            var footman = NewFootman(world, out var handle);
            var holder = Game.Instance.Add(new Holder());
            holder.End();
            var fx = footman.AddEffect(Glow, AttachPoint.Overhead, holder);
            Expect.Equal(true, fx.HasEnded, "ended at once");
            Expect.True(fx.Handle == null, "no handle");
            Expect.Equal(0, world.Effects.Count, "effects made");
            footman.Remove();
        }

        /// <summary>Facing, Pitch and Roll are degrees, 0 unless set, and the game is given radians.</summary>
        public static void Facing(FakeWorld world)
        {
            var fx = SpecialEffect.Create(Glow, 0f, 0f, Game.Instance);
            Expect.Near(0f, fx.Facing, "facing unless set");
            fx.Facing = 90f;
            fx.Pitch = 45f;
            fx.Roll = 30f;
            var made = world.Effects[0];
            Expect.Near(1.5707964f, made.Yaw, "the game's yaw");
            Expect.Near(0.7853982f, made.Pitch, "the game's pitch");
            Expect.Near(0.5235988f, made.Roll, "the game's roll");
            Expect.Near(90f, fx.Facing, "facing reads degrees");
            Expect.Near(45f, fx.Pitch, "pitch reads degrees");
            Expect.Near(30f, fx.Roll, "roll reads degrees");
        }

        /// <summary>Scale is 1 unless set, and a set after the end asks nothing of the game.</summary>
        public static void Scale(FakeWorld world)
        {
            var fx = SpecialEffect.Create(Glow, 0f, 0f, Game.Instance);
            Expect.Near(1f, fx.Scale, "scale unless set");
            fx.Scale = 2f;
            Expect.Near(2f, world.Effects[0].Scale, "the game's scale");
            Expect.Near(2f, fx.Scale, "scale as set");
            fx.End();
            fx.Scale = 3f;
            Expect.Near(2f, world.Effects[0].Scale, "no scale after the end");
            Expect.Near(2f, fx.Scale, "the last scale");
        }

        /// <summary>An animation is played by name, and another can be queued after it.</summary>
        public static void Animations(FakeWorld world)
        {
            var fx = SpecialEffect.Create(Glow, 0f, 0f, Game.Instance);
            fx.PlayAnimation(Animation.Attack);
            fx.QueueAnimation(Animation.Stand);
            var given = world.Effects[0].Animations;
            Expect.Equal(2, given.Count, "animations given");
            Expect.Equal("attack", given[0], "played");
            Expect.Equal("queue stand", given[1], "queued");
        }

        /// <summary>AnimationSpeed is 1 unless set.</summary>
        public static void AnimationSpeed(FakeWorld world)
        {
            var fx = SpecialEffect.Create(Glow, 0f, 0f, Game.Instance);
            Expect.Near(1f, fx.AnimationSpeed, "speed unless set");
            fx.AnimationSpeed = 0.25f;
            Expect.Near(0.25f, world.Effects[0].Speed, "the game's speed");
            Expect.Near(0.25f, fx.AnimationSpeed, "speed as set");
        }

        /// <summary>Tint is white unless set, gives the game its red, green, blue and alpha, and reads back what was set; after the end it asks nothing.</summary>
        public static void Tint(FakeWorld world)
        {
            var fx = SpecialEffect.Create(Glow, 0f, 0f, Game.Instance);
            Expect.True(fx.Tint == Color.White, "white unless set");
            fx.Tint = new Color(255, 0, 0, 128);
            var given = world.Effects[0].Tint!;
            Expect.Equal(255, given[0], "the game's red");
            Expect.Equal(0, given[1], "the game's green");
            Expect.Equal(0, given[2], "the game's blue");
            Expect.Equal(128, given[3], "the game's alpha");
            Expect.True(fx.Tint == new Color(255, 0, 0, 128), "reads what was set");
            fx.End();
            fx.Tint = Color.White;
            Expect.True(fx.Tint == new Color(255, 0, 0, 128), "the last tint after the end");
        }

        /// <summary>Makes a footman at a place, with no class registered for its type.</summary>
        private static Unit NewFootman(FakeWorld world, out unit handle)
        {
            handle = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            world.PlaceUnit(handle, 100f, 50f);
            return Unit.Of(handle)!;
        }

        /// <summary>An effect on a unit is put at its point, and its place is the unit's: setting it asks nothing of the game.</summary>
        public static void OnUnit(FakeWorld world)
        {
            var footman = NewFootman(world, out var handle);
            var fx = footman.AddEffect(Glow, AttachPoint.Overhead, Game.Instance);
            var made = world.Effects[0];
            Expect.Same(handle, made.Unit, "on the unit's handle");
            Expect.Equal("overhead", made.Point, "at its point");
            Expect.Same(footman, fx.Unit, "the unit");
            Expect.Equal("overhead", fx.AttachPoint, "the point");
            Expect.Near(100f, fx.X, "the unit's x");
            Expect.Near(50f, fx.Y, "the unit's y");
            Expect.Near(0f, fx.Height, "no height of its own");
            fx.X = 9f;
            fx.Height = 40f;
            fx.PlaceAt(5f, 5f);
            Expect.Equal(0, made.Places, "no move asked of the game");
            world.PlaceUnit(handle, 300f, 60f);
            Expect.Near(300f, fx.X, "follows the unit's x");
            Expect.Near(60f, fx.Y, "follows the unit's y");
            Expect.Near(0f, fx.Height, "still no height");
        }

        /// <summary>An effect at a point has no unit and no point.</summary>
        public static void AtPointHasNoUnit(FakeWorld world)
        {
            var fx = SpecialEffect.Create(Glow, 0f, 0f, Game.Instance);
            Expect.True(fx.Unit == null, "no unit");
            Expect.Equal("", fx.AttachPoint, "no point");
        }

        /// <summary>An effect on a unit ends when the unit ends, though its scope goes on; one ended before is not ended again.</summary>
        public static void EndsWithUnit(FakeWorld world)
        {
            var footman = NewFootman(world, out var handle);
            var fx = footman.AddEffect(Glow, AttachPoint.Origin, Game.Instance);
            var early = footman.AddEffect(Glow, AttachPoint.Chest, Game.Instance);
            early.End();
            footman.Remove();
            Expect.Equal(true, fx.HasEnded, "ended with the unit");
            Expect.Equal(1, world.Effects[0].Ends, "ended once in the game");
            Expect.Equal(1, world.Effects[1].Ends, "the early one ended once");
        }

        /// <summary>A unit the game removed is swept, and its effects end with it, once each.</summary>
        public static void UnitSwept(FakeWorld world)
        {
            var footman = NewFootman(world, out var handle);
            var fx = footman.AddEffect(Glow, AttachPoint.Overhead, Game.Instance);
            world.VanishUnit(handle);
            world.Advance(0.3f);
            Expect.True(footman.IsGone, "the sweep found the unit gone");
            Expect.Equal(true, fx.HasEnded, "ended with the unit");
            Expect.Equal(1, world.Effects[0].Ends, "ended once in the game");
        }

        /// <summary>An effect whose scope is its unit ends once, as the unit's child.</summary>
        public static void UnitIsScope(FakeWorld world)
        {
            var footman = NewFootman(world, out var handle);
            var fx = footman.AddEffect(Glow, AttachPoint.Overhead, footman);
            footman.Remove();
            Expect.Equal(true, fx.HasEnded, "ended with the unit");
            Expect.Equal(1, world.Effects[0].Ends, "ended once in the game");
        }

        /// <summary>An effect put on a unit that is gone ends at once, and the game is not asked to make it.</summary>
        public static void OnGoneUnit(FakeWorld world)
        {
            var footman = NewFootman(world, out var handle);
            world.VanishUnit(handle);
            world.Advance(0.3f);
            var fx = footman.AddEffect(Glow, AttachPoint.Overhead, Game.Instance);
            Expect.Equal(true, fx.HasEnded, "ended at once");
            Expect.True(fx.Handle == null, "no handle");
            Expect.Equal(0, world.Effects.Count, "effects made");
        }

        /// <summary>PlayOnce makes the effect and ends it with its death in the same call.</summary>
        public static void PlayOnce(FakeWorld world)
        {
            SpecialEffect.PlayOnce(Glow, 10f, 20f);
            Expect.Equal(1, world.Effects.Count, "effects made");
            var made = world.Effects[0];
            Expect.Equal(Glow, made.Model, "the model");
            Expect.Near(10f, made.X, "the x");
            Expect.Near(20f, made.Y, "the y");
            Expect.Equal(1, made.Ends, "ended once");
            Expect.Equal(true, made.Death, "with its death");
        }

        /// <summary>PlayEffectOnce on a unit makes the effect at its point and ends it with its death; on a unit that is gone it asks nothing.</summary>
        public static void PlayOnceOnUnit(FakeWorld world)
        {
            var footman = NewFootman(world, out var handle);
            footman.PlayEffectOnce(Glow, AttachPoint.Origin);
            var made = world.Effects[0];
            Expect.Same(handle, made.Unit, "on the unit");
            Expect.Equal("origin", made.Point, "at its point");
            Expect.Equal(1, made.Ends, "ended once");
            Expect.Equal(true, made.Death, "with its death");
            world.VanishUnit(handle);
            world.Advance(0.3f);
            footman.PlayEffectOnce(Glow, AttachPoint.Origin);
            Expect.Equal(1, world.Effects.Count, "nothing made on the gone unit");
        }

        /// <summary>An effect shown to one player gets its model on that player's machine only; every read is the same on every machine.</summary>
        public static void ShownTo(FakeWorld world)
        {
            world.LocalSlot = 0;
            var mine = SpecialEffect.Create(Glow, 0f, 0f, Game.Instance, shownTo: Player.One);
            var theirs = SpecialEffect.Create(Glow, 0f, 0f, Game.Instance, shownTo: Player.Two);
            var everyone = SpecialEffect.Create(Glow, 0f, 0f, Game.Instance);
            Expect.Equal(Glow, world.Effects[0].Model, "the model for this machine's player");
            Expect.Equal("", world.Effects[1].Model, "an empty model for another player's");
            Expect.Equal(Glow, world.Effects[2].Model, "the model for everyone");
            Expect.Equal(Glow, theirs.Model, "the model reads the path");
            Expect.Equal(true, theirs.IsShownTo(Player.Two), "shown to its player");
            Expect.Equal(false, theirs.IsShownTo(Player.One), "not to another");
            Expect.Equal(true, mine.IsShownTo(Player.One), "mine is shown to me");
            Expect.Equal(true, everyone.IsShownTo(Player.One), "shown to everyone");
            Expect.Equal(true, everyone.IsShownTo(Player.Two), "shown to everyone");
        }

        /// <summary>An effect on a unit and one played once take shownTo the same way.</summary>
        public static void ShownToOther(FakeWorld world)
        {
            world.LocalSlot = 0;
            var footman = NewFootman(world, out var handle);
            var fx = footman.AddEffect(Glow, AttachPoint.Overhead, Game.Instance, shownTo: Player.Two);
            SpecialEffect.PlayOnce(Glow, 0f, 0f, shownTo: Player.Two);
            footman.PlayEffectOnce(Glow, AttachPoint.Origin, shownTo: Player.Two);
            Expect.Equal("", world.Effects[0].Model, "on a unit");
            Expect.Equal("", world.Effects[1].Model, "played once");
            Expect.Equal("", world.Effects[2].Model, "played once on a unit");
            Expect.Equal(false, fx.IsShownTo(Player.One), "not shown to another");
        }
    }
}
