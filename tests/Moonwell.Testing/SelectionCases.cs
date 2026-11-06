namespace Moonwell.Testing
{
    /// <summary>Cases for a unit's animation and for players' selections (decisions 200 and 203), on the fake game.</summary>
    public static class SelectionCases
    {
        /// <summary>The type id the selection cases register SelectedUnit for.</summary>
        private static readonly int SelectedType = ObjectTypes.FourCC("h0se");

        /// <summary>Locust, which the game drops from a selection.</summary>
        private static readonly int Locust = ObjectTypes.FourCC("Aloc");

        /// <summary>Fails unless the log is exactly these lines.</summary>
        private static void ExpectLog(string[] expected, string what)
        {
            Expect.Equal(string.Join(", ", expected), string.Join(", ", TestLog.Lines.ToArray()), what);
        }

        /// <summary>Fails unless a player's Selection is exactly these units, in order.</summary>
        private static void ExpectSelection(Player player, Unit[] expected, string what)
        {
            var selection = player.Selection;
            Expect.Equal(expected.Length, selection.Count, what + ": the count");
            for (var i = 0; i < expected.Length && i < selection.Count; i++)
            {
                Expect.Same(expected[i], selection[i], what + ": unit " + i);
            }
        }

        /// <summary>A plain footman of a slot, taken in.</summary>
        private static Unit Footman(FakeWorld world, int slot)
        {
            return Unit.Of(world.NewUnit(ObjectTypes.FourCC("hfoo"), slot: slot))!;
        }

        /// <summary>The calls reach the game's: by name, by index, queued, the speed, and a tag added and removed.</summary>
        public static void AnimationCalls(FakeWorld world)
        {
            var unit = Footman(world, 0);
            unit.PlayAnimation(Animation.Attack);
            unit.PlayAnimation(3);
            unit.QueueAnimation(Animation.Stand);
            unit.AnimationSpeed = 0.5f;
            unit.AddAnimationTag(AnimationTag.Defend);
            unit.RemoveAnimationTag(AnimationTag.Defend);
            Expect.Equal("play attack, index 3, queue stand, speed 0.5, tag defend, untag defend", string.Join(", ", world.UnitAnimations[unit.Handle].ToArray()), "what the game was asked");
            Expect.Near(0.5f, unit.AnimationSpeed, "the speed reads what was set");
        }

        /// <summary>The speed reads 1 until set; once the unit is gone nothing reaches the game and the speed keeps its last value.</summary>
        public static void AnimationGone(FakeWorld world)
        {
            var unit = Footman(world, 0);
            Expect.Near(1f, unit.AnimationSpeed, "1 until set");
            unit.AnimationSpeed = 2f;
            unit.Remove();
            unit.AnimationSpeed = 0f;
            unit.PlayAnimation(Animation.Attack);
            unit.PlayAnimation(1);
            unit.QueueAnimation(Animation.Stand);
            unit.AddAnimationTag(AnimationTag.Alternate);
            unit.RemoveAnimationTag(AnimationTag.Alternate);
            Expect.Equal("speed 2", string.Join(", ", world.UnitAnimations[unit.Handle].ToArray()), "nothing after the removal");
            Expect.Near(2f, unit.AnimationSpeed, "the last speed set");
        }

        /// <summary>A selection runs the unit's class, its components, the game's components, then the selecting player's, whoever owns the unit; Selection already holds the unit in OnSelected, and no longer in OnDeselected.</summary>
        public static void ReactionsOrder(FakeWorld world)
        {
            ObjectTypes.Unit(SelectedType, () => new SelectedUnit());
            Game.Instance.Add(new SelectionGame());
            Player.Get(0).Add(new SelectionPlayer());
            Player.Get(1).Add(new SelectionPlayer());
            var unit = Unit.Of(world.NewUnit(SelectedType, slot: 1))!;
            unit.Add(new SelectedComponent());
            TestLog.Lines.Clear();
            world.PlayerSelects(unit.Handle, 0);
            world.PlayerSelects(unit.Handle, 0, false);
            ExpectLog(new[]
            {
                "unit selected 0 holding 1", "component selected 0", "game selected 0", "player 0 selected 0",
                "unit deselected 0 holding 0", "component deselected 0", "game deselected 0", "player 0 deselected 0",
            }, "the reactions in order; the owner's player components hear nothing");
        }

        /// <summary>Selection follows the events in order: a unit selected again keeps its place, a deselected one goes, and each player has their own.</summary>
        public static void FollowsEvents(FakeWorld world)
        {
            var a = Footman(world, 0);
            var b = Footman(world, 0);
            var c = Footman(world, 1);
            Moonwell.Runtime.ListenForSelection();
            world.PlayerSelects(a.Handle, 0);
            world.PlayerSelects(b.Handle, 0);
            world.PlayerSelects(c.Handle, 0);
            world.PlayerSelects(a.Handle, 0);
            world.PlayerSelects(c.Handle, 1);
            ExpectSelection(Player.Get(0), new[] { a, b, c }, "player 1's, an enemy's unit included, selected again in place");
            ExpectSelection(Player.Get(1), new[] { c }, "player 2's own");
            world.PlayerSelects(b.Handle, 0, false);
            ExpectSelection(Player.Get(0), new[] { a, c }, "b deselected");
            ExpectSelection(Player.Get(2), new Unit[0], "none for a player who selected nothing");
        }

        /// <summary>The game drops a selected unit with no event when it dies, is removed, hidden, given Locust or given to a player who shares no control (seen 11-06); one given to a player who shares control stays; none runs OnDeselected.</summary>
        public static void SilentDrops(FakeWorld world)
        {
            ObjectTypes.Unit(SelectedType, () => new SelectedUnit());
            var dying = Unit.Of(world.NewUnit(SelectedType, slot: 0))!;
            var removed = Footman(world, 0);
            var hidden = Footman(world, 0);
            var locust = Footman(world, 0);
            var given = Footman(world, 0);
            var shared = Footman(world, 0);
            var kept = Footman(world, 0);
            var units = new[] { dying, removed, hidden, locust, given, shared, kept };
            for (var i = 0; i < units.Length; i++)
            {
                world.PlayerSelects(units[i].Handle, 0);
            }

            TestLog.Lines.Clear();
            dying.Kill();
            removed.Remove();
            world.HiddenUnits.Add(hidden.Handle);
            world.AddAbility(locust.Handle, Locust);
            given.Owner = Player.Get(2);
            Player.Get(3).SetAlliance(Player.Get(0), Alliance.SharedControl);
            shared.Owner = Player.Get(3);
            ExpectSelection(Player.Get(0), new[] { shared, kept }, "the dropped ones left out");
            ExpectLog(new string[0], "no OnDeselected");
        }

        /// <summary>A read inside Local.For leaves out a dropped unit and changes nothing, so every machine keeps the same list; the next read outside lets it go.</summary>
        public static void ReadInLocal(FakeWorld world)
        {
            Moonwell.Runtime.ListenForSelection();
            var a = Footman(world, 0);
            var b = Footman(world, 0);
            world.PlayerSelects(a.Handle, 0);
            world.PlayerSelects(b.Handle, 0);
            a.Kill();
            var inside = -1;
            Local.For(Player.Get(0), () => inside = Player.Get(0).Selection.Count);
            Expect.Equal(1, inside, "the dead one left out inside");
            ExpectSelection(Player.Get(0), new[] { b }, "outside");
        }

        /// <summary>Select clears and selects, AddToSelection adds, Deselect and ClearSelection take away, each on the player's own machine only; a unit that is gone is not asked for.</summary>
        public static void SelectCalls(FakeWorld world)
        {
            world.LocalSlot = 0;
            var unit = Footman(world, 0);
            var me = Player.Get(0);
            me.Select(unit);
            me.AddToSelection(unit);
            me.Deselect(unit);
            me.ClearSelection();
            Player.Get(1).Select(unit);
            Player.Get(1).ClearSelection();
            Expect.Equal("0 clear, 0 select, 0 select, 0 deselect, 0 clear", string.Join(", ", world.SelectionCalls.ToArray()), "the local player's only");
            Expect.Equal(0, me.Selection.Count, "nothing selected until the game's event");
            unit.Remove();
            world.SelectionCalls.Clear();
            me.Select(unit);
            me.AddToSelection(unit);
            me.Deselect(unit);
            Expect.Equal(0, world.SelectionCalls.Count, "nothing for a unit that is gone");
        }

        /// <summary>With no selection reaction written, the world is not asked to listen; a class with one makes it listen.</summary>
        public static void OnlyWhenWanted(FakeWorld world)
        {
            Footman(world, 0);
            Expect.True(!world.Listening.Contains(Moonwell.Hosting.GameEvent.Selection), "not listening");
            ObjectTypes.Unit(SelectedType, () => new SelectedUnit());
            Unit.Of(world.NewUnit(SelectedType, slot: 0));
            Expect.True(world.Listening.Contains(Moonwell.Hosting.GameEvent.Selection), "listening");
        }
    }
}
