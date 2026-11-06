namespace Moonwell.Testing
{
    /// <summary>Cases that check floating text: made at a place or on a unit, played once, its fade, its end, and who sees it.</summary>
    public static class FloatingTextCases
    {
        /// <summary>A game component that holds texts, ended by the case.</summary>
        private sealed class Holder : GameComponent
        {
        }

        private static Unit NewFootman(FakeWorld world, out unit handle)
        {
            handle = world.NewUnit(ObjectTypes.FourCC("hfoo"));
            world.PlaceUnit(handle, 100f, 50f);
            return Unit.Of(handle)!;
        }

        /// <summary>Create asks the game once for a text at the place, white, at World Editor's size 10, standing still and lasting.</summary>
        public static void Create(FakeWorld world)
        {
            var text = FloatingText.Create("Shop", 100f, 200f, Game.Instance);
            Expect.Equal(1, world.Texts.Count, "texts made");
            var made = world.Texts[0];
            Expect.Equal("Shop", made.Words, "the words");
            Expect.Near(0.023f, made.LetterHeight, "size 10 is the game's 0.023");
            Expect.Near(100f, made.X, "the x");
            Expect.Near(200f, made.Y, "the y");
            Expect.Near(0f, made.Height, "on the ground");
            Expect.Equal(255, made.Color![3], "opaque");
            Expect.Near(0f, made.YVelocity, "standing still");
            Expect.Equal("Shop", text.Text, "the words read back");
            Expect.Near(10f, text.Size, "size 10 unless set");
            Expect.Near(0f, text.Duration, "lasting unless set");
            world.Advance(10f);
            Expect.Equal(false, text.HasEnded, "still there");
        }

        /// <summary>Sets reach the game and read back: words, size, colour, height, place, speed and angle.</summary>
        public static void Sets(FakeWorld world)
        {
            var text = FloatingText.Create("a", 0f, 0f, Game.Instance);
            var made = world.Texts[0];
            text.Text = "b";
            text.Size = 20f;
            text.Color = new Color(255, 0, 0, 128);
            text.Height = 40f;
            text.PlaceAt(10f, 20f);
            text.Speed = 128f;
            text.Angle = 0f;
            Expect.Equal("b", made.Words, "the words");
            Expect.Near(0.046f, made.LetterHeight, "size 20");
            Expect.Equal(255, made.Color![0], "red");
            Expect.Equal(128, made.Color![3], "its alpha");
            Expect.Near(40f, made.Height, "the height");
            Expect.Near(10f, made.X, "the x");
            Expect.Near(0.071f, made.XVelocity, "128 a second east is Blizzard.j's 0.071");
            Expect.Near(0f, made.YVelocity, "none north");
            Expect.Near(128f, text.Speed, "the speed reads back");
            Expect.Equal(128, text.Color.A, "the colour reads back");
        }

        /// <summary>Pop rises 64 a second, stays opaque for 1 s, fades over the next, and ends at 2 s, once, in the game too.</summary>
        public static void Pop(FakeWorld world)
        {
            var before = Game.Instance.ChildCount;
            FloatingText.Pop("+25", 0f, 0f, Color.TooltipGold);
            var made = world.Texts[0];
            Expect.Near(64f * 0.071f / 128f, made.YVelocity, "rises 64 a second");
            Expect.Equal(Color.TooltipGold.R, made.Color![0], "its colour");
            world.Advance(1f);
            Expect.Equal(255, made.Color![3], "opaque for its first second");
            world.Advance(0.5f);
            Expect.True(made.Color![3] > 100 && made.Color![3] < 155, "half faded at 1.5 s: " + made.Color![3]);
            world.Advance(0.6f);
            Expect.Equal(true, made.Ended, "ended at 2 s");
            Expect.Equal(before, Game.Instance.ChildCount, "the text and its timer are gone");
        }

        /// <summary>A text's Duration counts from when it was made, its FadeOut is the end of it, and with no Duration nothing fades.</summary>
        public static void DurationAndFade(FakeWorld world)
        {
            var text = FloatingText.Create("a", 0f, 0f, Game.Instance);
            text.FadeOut = 1f;
            world.Advance(5f);
            Expect.Equal(255, world.Texts[0].Color![3], "no Duration, no fade");
            text.Duration = 8f;
            world.Advance(2.5f);
            Expect.True(world.Texts[0].Color![3] < 255, "fading in its last second");
            Expect.Equal(false, text.HasEnded, "not yet");
            world.Advance(0.6f);
            Expect.Equal(true, text.HasEnded, "ended 8 s after it was made");
        }

        /// <summary>A text ends with its scope, once in the game, and asks nothing after.</summary>
        public static void ScopeEnd(FakeWorld world)
        {
            var holder = Game.Instance.Add(new Holder());
            var text = FloatingText.Create("a", 0f, 0f, holder);
            text.Duration = 5f;
            holder.End();
            Expect.Equal(true, text.HasEnded, "ended with its scope");
            Expect.Equal(true, world.Texts[0].Ended, "ended in the game");
            text.Text = "b";
            world.Advance(6f);
            Expect.Equal("a", text.Text, "a set after the end does nothing");
        }

        /// <summary>A text made in a scope that has ended ends at once, and the game is not asked to make it.</summary>
        public static void EndedScope(FakeWorld world)
        {
            var holder = Game.Instance.Add(new Holder());
            holder.End();
            var text = FloatingText.Create("a", 0f, 0f, holder);
            Expect.Equal(true, text.HasEnded, "ended at once");
            Expect.Equal(0, world.Texts.Count, "texts made");
        }

        /// <summary>A unit's text is put over it and follows it; a speed does not move it off.</summary>
        public static void OnUnitFollows(FakeWorld world)
        {
            var footman = NewFootman(world, out var handle);
            var text = footman.AddText("Boss", Game.Instance);
            var made = world.Texts[0];
            Expect.Same(handle, made.Over, "over the unit");
            Expect.Near(100f, made.X, "where the unit is");
            text.Speed = 64f;
            Expect.Near(0f, made.YVelocity, "a text that follows does not drift");
            world.PlaceUnit(handle, 300f, 50f);
            world.Advance(0.1f);
            Expect.Near(300f, made.X, "followed the unit");
            Expect.Same(footman, text.Unit, "its unit");
            footman.Remove();
        }

        /// <summary>A unit's text shows only while this machine's player sees the unit; a Pop is checked when it is made.</summary>
        public static void HiddenFromUnseen(FakeWorld world)
        {
            var footman = NewFootman(world, out var handle);
            world.Unseen.Add(handle);
            footman.AddText("Boss", Game.Instance);
            footman.PopText("-25", Color.Red);
            Expect.Equal(false, world.Texts[0].Shown, "the label is hidden");
            Expect.Equal(false, world.Texts[1].Shown, "the pop is hidden");
            world.Unseen.Clear();
            world.Advance(0.1f);
            Expect.Equal(true, world.Texts[0].Shown, "the label shows once the unit is seen");
            Expect.Equal(false, world.Texts[1].Shown, "the pop was checked when made");
            Expect.Near(64f * 0.071f / 128f, world.Texts[1].YVelocity, "the pop rises");
            footman.Remove();
        }

        /// <summary>A unit's text ends with the unit, once, and nothing is made on a unit that is gone.</summary>
        public static void EndsWithUnit(FakeWorld world)
        {
            var footman = NewFootman(world, out var handle);
            var text = footman.AddText("Boss", Game.Instance);
            footman.Remove();
            Expect.Equal(true, text.HasEnded, "ended with the unit");
            Expect.Equal(true, world.Texts[0].Ended, "ended in the game");
            var late = footman.AddText("Boss", Game.Instance);
            footman.PopText("-1");
            Expect.Equal(true, late.HasEnded, "ended at once on a gone unit");
            Expect.Equal(1, world.Texts.Count, "nothing made on a gone unit");
        }

        /// <summary>A text shown to another player is not made on this machine, and ends at its Duration on every machine.</summary>
        public static void ShownTo(FakeWorld world)
        {
            world.LocalSlot = 0;
            var theirs = FloatingText.Create("a", 0f, 0f, Game.Instance, shownTo: Player.Two);
            theirs.Duration = 1f;
            theirs.Text = "b";
            FloatingText.Pop("+1", 0f, 0f, shownTo: Player.Two);
            Expect.Equal(0, world.Texts.Count, "nothing made for another player");
            Expect.Equal("b", theirs.Text, "reads what was set");
            Expect.Equal(true, theirs.IsShownTo(Player.Two), "shown to its player");
            Expect.Equal(false, theirs.IsShownTo(Player.One), "not to another");
            FloatingText.Create("c", 0f, 0f, Game.Instance, shownTo: Player.One);
            Expect.Equal(1, world.Texts.Count, "made for this machine's player");
            world.Advance(1.1f);
            Expect.Equal(true, theirs.HasEnded, "ended at its Duration");
        }
    }
}
