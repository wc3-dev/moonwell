using System;
using System.Collections.Generic;

namespace Moonwell.Testing
{
    /// <summary>Lists the cases, runs them, and prints the results.</summary>
    public static class Cases
    {
        /// <summary>Every case, grouped by file in task order.</summary>
        public static List<Case> All()
        {
            var list = new List<Case>();
            AddFirst(list);
            AddAuditTimers(list);
            AddMiddle(list);
            AddHeroes(list);
            AddEffects(list);
            AddUnitAbilities(list);
            AddLast(list);
            AddCompiler(list);
            AddMaths(list);
            AddInteractions(list);
            AddAuditInteractions(list);
            AddPhysics(list);
            AddTerrain(list);
            AddAuditMaths(list);
            AddAreas(list);
            AddAuditAreas(list);
            AddInput(list);
            AddFrames(list);
            AddKeys(list);
            AddOrders(list);
            AddLifecycle(list);
            AddScope(list);
            AddAuditDocs(list);
            AddLog(list);
            AddDay(list);
            AddText(list);
            AddHolds(list);
            AddSpellBuffs(list);
            AddDummies(list);
            AddLoad(list);
            AddShare(list);
            AddPlayerFiles(list);
            AddJson(list);
            AddDebug(list);
            return list;
        }

        /// <summary>Adds the cases for dummy casters.</summary>
        private static void AddDummies(List<Case> list)
        {
            list.Add(new Case { Name = "fake.dummy-effect-in-order", Run = FakeGameCases.DummyEffectInOrder });
            list.Add(new Case { Name = "fake.dummy-wrong-kind", Run = FakeGameCases.DummyWrongKind });
            list.Add(new Case { Name = "fake.dummy-hidden-target", Run = FakeGameCases.DummyHiddenTarget });
            list.Add(new Case { Name = "fake.dummy-channel-stop", Run = FakeGameCases.DummyChannelStop });
            list.Add(new Case { Name = "dummy.registry", Run = DummyCases.Registry });
            list.Add(new Case { Name = "dummy.cast-on", Run = DummyCases.CastOn });
            list.Add(new Case { Name = "dummy.cast-at", Run = DummyCases.CastAt });
            list.Add(new Case { Name = "dummy.cast-around", Run = DummyCases.CastAround });
            list.Add(new Case { Name = "dummy.level", Run = DummyCases.Level });
            list.Add(new Case { Name = "dummy.player", Run = DummyCases.ForPlayer });
            list.Add(new Case { Name = "dummy.refused", Run = DummyCases.Refused });
            list.Add(new Case { Name = "dummy.gone-or-dead-caster", Run = DummyCases.GoneOrDeadCaster });
            list.Add(new Case { Name = "dummy.ended-scope", Run = DummyCases.EndedScope });
            list.Add(new Case { Name = "dummy.fog", Run = DummyCases.Fog });
            list.Add(new Case { Name = "dummy.held-order", Run = DummyCases.HeldOrder });
            list.Add(new Case { Name = "dummy.nested", Run = DummyCases.Nested });
            list.Add(new Case { Name = "dummy.fifty-at-once", Run = DummyCases.FiftyAtOnce });
            list.Add(new Case { Name = "dummy.hidden", Run = DummyCases.Hidden });
            list.Add(new Case { Name = "dummy.no-cast-reactions", Run = DummyCases.NoCastReactions });
            list.Add(new Case { Name = "dummy.hit-credit", Run = DummyCases.HitCredit });
            list.Add(new Case { Name = "dummy.kill-credit", Run = DummyCases.KillCredit });
            list.Add(new Case { Name = "dummy.player-credit", Run = DummyCases.PlayerCredit });
            list.Add(new Case { Name = "dummy.spell-buff", Run = DummyCases.SpellBuff });
            list.Add(new Case { Name = "dummy.caster-gone-between-hits", Run = DummyCases.CasterGoneBetweenHits });
            list.Add(new Case { Name = "dummy.package-hit-credit", Run = DummyCases.PackageHitCredit });
            list.Add(new Case { Name = "dummy.reuse", Run = DummyCases.Reuse });
            list.Add(new Case { Name = "dummy.busy-duration", Run = DummyCases.BusyDuration });
            list.Add(new Case { Name = "dummy.channel-stop", Run = DummyCases.ChannelStop });
            list.Add(new Case { Name = "dummy.scope-ends-in-effect", Run = DummyCases.ScopeEndsInEffect });
            list.Add(new Case { Name = "dummy.owner-change", Run = DummyCases.OwnerChange });
            list.Add(new Case { Name = "dummy.never-removed", Run = DummyCases.NeverRemoved });
            list.Add(new Case { Name = "dummy.package-unknown-target", Run = DummyCases.PackageUnknownTarget });
        }

        private static void AddLoad(List<Case> list)
        {
            list.Add(new Case { Name = "load.undone-links-break-strings", Run = LoadCases.UndoneLinksBreakStrings });
            list.Add(new Case { Name = "load.relink-mends-strings", Run = LoadCases.RelinkMendsStrings });
            list.Add(new Case { Name = "load.relink-twice", Run = LoadCases.RelinkTwice });
        }

        private static void AddPlayerFiles(List<Case> list)
        {
            list.Add(new Case { Name = "code.encode", Run = CodeLetterCases.Encode });
            list.Add(new Case { Name = "code.decode-tolerant", Run = CodeLetterCases.DecodeTolerant });
            list.Add(new Case { Name = "code.decode-bad", Run = CodeLetterCases.DecodeBad });
            list.Add(new Case { Name = "code.round-trip", Run = CodeLetterCases.RoundTrip });
            list.Add(new Case { Name = "bytes.int-sizes", Run = ByteCases.IntSizes });
            list.Add(new Case { Name = "bytes.round-trip", Run = ByteCases.RoundTrip });
            list.Add(new Case { Name = "bytes.layout", Run = ByteCases.Layout });
            list.Add(new Case { Name = "bytes.code", Run = ByteCases.Code });
            list.Add(new Case { Name = "bytes.past-the-end", Run = ByteCases.PastTheEnd });
            list.Add(new Case { Name = "file.escape-round-trip", Run = FileCases.EscapeRoundTrip });
            list.Add(new Case { Name = "file.escaped-is-safe", Run = FileCases.EscapedIsSafe });
            list.Add(new Case { Name = "file.pieces", Run = FileCases.Pieces });
            list.Add(new Case { Name = "file.empty-pieces", Run = FileCases.EmptyPieces });
            list.Add(new Case { Name = "file.no-letter-start", Run = FileCases.NoLetterStart });
            list.Add(new Case { Name = "file.longest-line-fits", Run = FileCases.LongestLineFits });
            list.Add(new Case { Name = "file.write-then-read", Run = FileCases.WriteThenRead });
            list.Add(new Case { Name = "file.installed-folder", Run = FileCases.InstalledFolder });
            list.Add(new Case { Name = "file.other-players-file", Run = FileCases.OtherPlayersFile });
            list.Add(new Case { Name = "file.missing-file", Run = FileCases.MissingFile });
            list.Add(new Case { Name = "file.bad-path", Run = FileCases.BadPath });
            list.Add(new Case { Name = "file.too-long", Run = FileCases.TooLong });
            list.Add(new Case { Name = "file.write-waits-for-echo", Run = FileCases.WriteWaitsForEcho });
            list.Add(new Case { Name = "file.replay-writes-nothing", Run = FileCases.ReplayWritesNothing });
            list.Add(new Case { Name = "file.read-file-here", Run = FileCases.ReadFileHere });
            list.Add(new Case { Name = "file.read-file-from-another-machine", Run = FileCases.ReadFileFromAnotherMachine });
            list.Add(new Case { Name = "file.read-file-missing", Run = FileCases.ReadFileMissing });
            list.Add(new Case { Name = "share.each-type", Run = ShareCases.ShareEachType });
            list.Add(new Case { Name = "share.computer-default", Run = ShareCases.ShareComputer });
            list.Add(new Case { Name = "log.waits-for-the-replay-guard", Run = LogCases.WaitsForTheReplayGuard });
            list.Add(new Case { Name = "log.not-written-in-a-replay", Run = LogCases.NotWrittenInAReplay });
        }

        private static void AddJson(List<Case> list)
        {
            list.Add(new Case { Name = "json.write", Run = JsonCases.Write });
            list.Add(new Case { Name = "json.parse-back", Run = JsonCases.ParseBack });
            list.Add(new Case { Name = "json.numbers", Run = JsonCases.Numbers });
            list.Add(new Case { Name = "json.escapes", Run = JsonCases.Escapes });
            list.Add(new Case { Name = "json.order", Run = JsonCases.Order });
            list.Add(new Case { Name = "json.bad", Run = JsonCases.Bad });
            list.Add(new Case { Name = "json.depth", Run = JsonCases.Depth });
            list.Add(new Case { Name = "json.parents", Run = JsonCases.Parents });
            list.Add(new Case { Name = "json.kinds", Run = JsonCases.Kinds });
            list.Add(new Case { Name = "json.through-a-file", Run = JsonCases.ThroughAFile });
            list.Add(new Case { Name = "chat.heard", Run = ChatCases.Heard });
            list.Add(new Case { Name = "chat.only-its-word", Run = ChatCases.OnlyItsWord });
            list.Add(new Case { Name = "chat.ends", Run = ChatCases.Ends });
            list.Add(new Case { Name = "chat.two-of-one-name", Run = ChatCases.TwoOfOneName });
            list.Add(new Case { Name = "chat.listens-once", Run = ChatCases.ListensOnce });
            list.Add(new Case { Name = "chat.ended-let-go", Run = ChatCases.EndedLetGo });
            list.Add(new Case { Name = "chat.made-and-ended-by-a-handler", Run = ChatCases.MadeAndEndedByAHandler });
            list.Add(new Case { Name = "chat.bad-name-refused", Run = ChatCases.BadNameRefused });
            list.Add(new Case { Name = "save.round-trip", Run = SaveCases.RoundTrip });
            list.Add(new Case { Name = "save.typing-tolerated", Run = SaveCases.TypingTolerated });
            list.Add(new Case { Name = "save.bad-codes", Run = SaveCases.BadCodes });
            list.Add(new Case { Name = "save.other-name", Run = SaveCases.OtherName });
            list.Add(new Case { Name = "save.other-class", Run = SaveCases.OtherClass });
            list.Add(new Case { Name = "save.other-key", Run = SaveCases.OtherKey });
            list.Add(new Case { Name = "save.type-changed", Run = SaveCases.TypeChanged });
            list.Add(new Case { Name = "save.no-plain-numbers", Run = SaveCases.NoPlainNumbers });
            list.Add(new Case { Name = "save.ids-take-four", Run = SaveCases.IdsTakeFour });
            list.Add(new Case { Name = "save.dashes", Run = SaveCases.Dashes });
            list.Add(new Case { Name = "save.load-from-file", Run = SaveCases.LoadFromFile });
            list.Add(new Case { Name = "save.load-missing", Run = SaveCases.LoadMissing });
            list.Add(new Case { Name = "save.load-edited", Run = SaveCases.LoadEdited });
            list.Add(new Case { Name = "save.slots", Run = SaveCases.Slots });
            list.Add(new Case { Name = "save.bad-slot", Run = SaveCases.BadSlot });
            list.Add(new Case { Name = "save.load-scope-ended", Run = SaveCases.LoadScopeEnded });
        }

        private static void AddShare(List<Case> list)
        {
            list.Add(new Case { Name = "share.short-answer", Run = ShareCases.ShortAnswer });
            list.Add(new Case { Name = "share.long-answer", Run = ShareCases.LongAnswer });
            list.Add(new Case { Name = "share.zero-byte", Run = ShareCases.ZeroByte });
            list.Add(new Case { Name = "share.null-and-empty", Run = ShareCases.NullAndEmpty });
            list.Add(new Case { Name = "share.other-machine", Run = ShareCases.OtherMachine });
            list.Add(new Case { Name = "share.two-in-flight", Run = ShareCases.TwoInFlight });
            list.Add(new Case { Name = "share.wrong-sender", Run = ShareCases.WrongSender });
            list.Add(new Case { Name = "share.scope-ended", Run = ShareCases.ScopeEnded });
            list.Add(new Case { Name = "share.leave-halfway", Run = ShareCases.LeaveHalfway });
            list.Add(new Case { Name = "share.computer", Run = ShareCases.Computer });
            list.Add(new Case { Name = "share.read-is-local", Run = ShareCases.ReadIsLocal });
            list.Add(new Case { Name = "realtime.epoch", Run = RealTimeCases.Epoch });
            list.Add(new Case { Name = "realtime.known-day", Run = RealTimeCases.KnownDay });
            list.Add(new Case { Name = "realtime.leap-day", Run = RealTimeCases.LeapDay });
            list.Add(new Case { Name = "realtime.last", Run = RealTimeCases.Last });
            list.Add(new Case { Name = "realtime.before-epoch", Run = RealTimeCases.BeforeEpoch });
            list.Add(new Case { Name = "realtime.add-across", Run = RealTimeCases.AddAcross });
            list.Add(new Case { Name = "realtime.compare", Run = RealTimeCases.Compare });
            list.Add(new Case { Name = "realtime.now-moves", Run = RealTimeCases.NowMoves });
            list.Add(new Case { Name = "realtime.local-now", Run = RealTimeCases.LocalNow });
            list.Add(new Case { Name = "realtime.start-waits", Run = RealTimeCases.StartWaits });
            list.Add(new Case { Name = "realtime.start-without-date", Run = RealTimeCases.StartWithoutDate });
            list.Add(new Case { Name = "realtime.first-person-leaves", Run = RealTimeCases.FirstPersonLeaves });
            list.Add(new Case { Name = "realtime.after-load", Run = RealTimeCases.AfterLoad });
        }

        /// <summary>Adds the cases for the buffs the game puts on: the fake world's game buffs, then the watch.</summary>
        private static void AddSpellBuffs(List<Case> list)
        {
            list.Add(new Case { Name = "fake.game-buff-reads", Run = SpellBuffCases.FakeGameBuffReads });
            list.Add(new Case { Name = "fake.spell-hit-order", Run = SpellBuffCases.FakeSpellHitOrder });
            list.Add(new Case { Name = "fake.buff-off-at-death", Run = SpellBuffCases.FakeBuffOffAtDeath });
            list.Add(new Case { Name = "fake.aura", Run = SpellBuffCases.FakeAura });
            list.Add(new Case { Name = "spellbuff.after-hit", Run = SpellBuffCases.AfterHit });
            list.Add(new Case { Name = "spellbuff.hit-sees-it", Run = SpellBuffCases.HitSeesIt });
            list.Add(new Case { Name = "spellbuff.no-hit-cast", Run = SpellBuffCases.NoHitCast });
            list.Add(new Case { Name = "spellbuff.area", Run = SpellBuffCases.Area });
            list.Add(new Case { Name = "spellbuff.aura", Run = SpellBuffCases.Aura });
            list.Add(new Case { Name = "spellbuff.given-again", Run = SpellBuffCases.GivenAgain });
            list.Add(new Case { Name = "spellbuff.ends-with-game", Run = SpellBuffCases.EndsWithGame });
            list.Add(new Case { Name = "spellbuff.sweep", Run = SpellBuffCases.Sweep });
            list.Add(new Case { Name = "spellbuff.removed-from-code", Run = SpellBuffCases.RemovedFromCode });
            list.Add(new Case { Name = "spellbuff.death", Run = SpellBuffCases.Death });
            list.Add(new Case { Name = "spellbuff.unit-removed", Run = SpellBuffCases.UnitRemoved });
            list.Add(new Case { Name = "spellbuff.apply-refused", Run = SpellBuffCases.ApplyRefused });
            list.Add(new Case { Name = "spellbuff.plain-attack-gives-nothing", Run = SpellBuffCases.PlainAttackGivesNothing });
            list.Add(new Case { Name = "spellbuff.onapply-damage-does-not-loop", Run = SpellBuffCases.OnApplyDamageDoesNotLoop });
            list.Add(new Case { Name = "spellbuff.dead-holder-gives-nothing", Run = SpellBuffCases.DeadHolderGivesNothing });
            list.Add(new Case { Name = "spellbuff.self-buff", Run = SpellBuffCases.SelfBuff });
            list.Add(new Case { Name = "spellbuff.stray-target-area", Run = SpellBuffCases.StrayTargetArea });
            list.Add(new Case { Name = "spellbuff.step-survives-ends", Run = SpellBuffCases.StepSurvivesEnds });
            list.Add(new Case { Name = "spellbuff.equal-auras-keep-source", Run = SpellBuffCases.EqualAurasKeepSource });
            list.Add(new Case { Name = "spellbuff.hit-and-cast-once", Run = SpellBuffCases.HitAndCastOnce });
            list.Add(new Case { Name = "spellbuff.sweep-finds-holders-first", Run = SpellBuffCases.SweepFindsHoldersFirst });
        }

        /// <summary>Adds the cases for holders: a component or buff held by a scope ends with its last holder.</summary>
        private static void AddHolds(List<Case> list)
        {
            list.Add(new Case { Name = "hold.ends-with-holder", Run = HoldCases.EndsWithHolder });
            list.Add(new Case { Name = "hold.two-components", Run = HoldCases.TwoComponents });
            list.Add(new Case { Name = "hold.ended-holder", Run = HoldCases.EndedHolder });
            list.Add(new Case { Name = "hold.removed-releases", Run = HoldCases.RemovedReleases });
            list.Add(new Case { Name = "hold.end-inside-reaction", Run = HoldCases.EndInsideReaction });
            list.Add(new Case { Name = "buff.held-shared", Run = HoldCases.HeldShared });
            list.Add(new Case { Name = "buff.held-duration-first", Run = HoldCases.HeldDurationFirst });
            list.Add(new Case { Name = "buff.held-then-unheld", Run = HoldCases.HeldThenUnheld });
            list.Add(new Case { Name = "buff.held-separate", Run = HoldCases.HeldSeparate });
            list.Add(new Case { Name = "buff.held-removed", Run = HoldCases.HeldRemoved });
            list.Add(new Case { Name = "buff.strongest-up", Run = HoldCases.StrongestUp });
            list.Add(new Case { Name = "buff.strongest-down", Run = HoldCases.StrongestDown });
            list.Add(new Case { Name = "buff.strongest-tie", Run = HoldCases.StrongestTie });
            list.Add(new Case { Name = "buff.strongest-last", Run = HoldCases.StrongestLast });
            list.Add(new Case { Name = "buff.strongest-timed", Run = HoldCases.StrongestTimed });
            list.Add(new Case { Name = "buff.strongest-handover", Run = HoldCases.StrongestHandover });
            list.Add(new Case { Name = "buff.held-refresh-holds-first", Run = HoldCases.RefreshHoldsFirst });
            list.Add(new Case { Name = "buff.held-refresh-ends-itself", Run = HoldCases.RefreshEndsItself });
            list.Add(new Case { Name = "buff.held-same-holder-once", Run = HoldCases.SameHolderOnce });
            list.Add(new Case { Name = "buff.strongest-same-holder-updates", Run = HoldCases.StrongestSameHolderUpdates });
            list.Add(new Case { Name = "buff.unheld-then-held-ends", Run = HoldCases.UnheldThenHeldEnds });
            list.Add(new Case { Name = "buff.unheld-lasting-stays", Run = HoldCases.UnheldLastingStays });
            list.Add(new Case { Name = "buff.held-holder-ends-in-onapply", Run = HoldCases.HolderEndsInOnApply });
            list.Add(new Case { Name = "buff.strongest-holder-ends-in-onapply", Run = HoldCases.StrongestHolderEndsInOnApply });
            list.Add(new Case { Name = "item.carry-gift", Run = CarryCases.Gift });
            list.Add(new Case { Name = "item.carry-two-boots", Run = CarryCases.TwoBoots });
            list.Add(new Case { Name = "item.carry-bag-then-equip", Run = CarryCases.BagThenEquip });
            list.Add(new Case { Name = "item.carry-death", Run = CarryCases.Death });
            list.Add(new Case { Name = "item.carry-item-gone", Run = CarryCases.ItemGone });
            list.Add(new Case { Name = "item.carry-carrier-gone", Run = CarryCases.CarrierGone });
            list.Add(new Case { Name = "item.carry-bag-is-quiet", Run = CarryCases.BagIsQuiet });
            list.Add(new Case { Name = "item.carry-slot-move", Run = CarryCases.SlotMove });
            list.Add(new Case { Name = "item.carry-game-drop", Run = CarryCases.GameDrop });
            list.Add(new Case { Name = "item.carry-given-on-in-drop", Run = CarryCases.GivenOnInDrop });
        }

        /// <summary>Adds the cases for the log: a map's own lines, and the log file's lines and timing.</summary>
        private static void AddLog(List<Case> list)
        {
            list.Add(new Case { Name = "log.map-line", Run = LogCases.MapLine });
            list.Add(new Case { Name = "log.file-keeps-last-lines", Run = LogCases.KeepsLastLines });
            list.Add(new Case { Name = "log.file-splits-long-lines", Run = LogCases.SplitsLongLines });
            list.Add(new Case { Name = "log.file-splits-between-letters", Run = LogCases.SplitsBetweenLetters });
            list.Add(new Case { Name = "log.file-writes-at-most-once-a-second", Run = LogCases.WritesAtMostOnceASecond });
        }

        /// <summary>Adds the cases for the game clock and the time of day.</summary>
        private static void AddDay(List<Case> list)
        {
            list.Add(new Case { Name = "day.game-time", Run = DayCases.GameTime });
            list.Add(new Case { Name = "day.time-of-day", Run = DayCases.TimeOfDay });
            list.Add(new Case { Name = "day.time-of-day-wraps", Run = DayCases.TimeOfDayWraps });
            list.Add(new Case { Name = "day.is-day", Run = DayCases.IsDay });
            list.Add(new Case { Name = "day.maps-own-day", Run = DayCases.MapsOwnDay });
            list.Add(new Case { Name = "day.over-midnight", Run = DayCases.DayOverMidnight });
            list.Add(new Case { Name = "day.speed", Run = DayCases.DaySpeed });
            list.Add(new Case { Name = "day.stopped", Run = DayCases.DayStopped });
            list.Add(new Case { Name = "day.dawn-and-dusk", Run = DayCases.DawnAndDusk });
            list.Add(new Case { Name = "day.jumps", Run = DayCases.DayJumps });
            list.Add(new Case { Name = "day.backward", Run = DayCases.DayBackward });
            list.Add(new Case { Name = "day.maps-own-dawn", Run = DayCases.MapsOwnDawn });
        }

        /// <summary>Adds the cases for colours, coloured text and time as text.</summary>
        private static void AddText(List<Case> list)
        {
            list.Add(new Case { Name = "color.parts", Run = ColorCases.Parts });
            list.Add(new Case { Name = "color.parts-kept-within", Run = ColorCases.PartsKeptWithin });
            list.Add(new Case { Name = "color.equality", Run = ColorCases.Equality });
            list.Add(new Case { Name = "color.html", Run = ColorCases.Html });
            list.Add(new Case { Name = "color.lerp", Run = ColorCases.Lerp });
            list.Add(new Case { Name = "color.from-hsv", Run = ColorCases.FromHsv });
            list.Add(new Case { Name = "color.shades", Run = ColorCases.Shades });
            list.Add(new Case { Name = "color.named", Run = ColorCases.Named });
            list.Add(new Case { Name = "color.player-colors", Run = ColorCases.PlayerColors });
            list.Add(new Case { Name = "color.colored-name", Run = ColorCases.ColoredName });
            list.Add(new Case { Name = "color.unit-tint", Run = ColorCases.UnitTint });
            list.Add(new Case { Name = "text.colored", Run = TextEffectCases.Colored });
            list.Add(new Case { Name = "text.rainbow", Run = TextEffectCases.Rainbow });
            list.Add(new Case { Name = "text.rainbow-shift", Run = TextEffectCases.RainbowShift });
            list.Add(new Case { Name = "text.gradient", Run = TextEffectCases.Gradient });
            list.Add(new Case { Name = "text.spaces-and-shared-codes", Run = TextEffectCases.SpacesAndSharedCodes });
            list.Add(new Case { Name = "text.breaks-and-codes", Run = TextEffectCases.BreaksAndCodes });
            list.Add(new Case { Name = "text.whole-letters", Run = TextEffectCases.WholeLetters });
            list.Add(new Case { Name = "text.long-text-shares-codes", Run = TextEffectCases.LongTextSharesCodes });
            list.Add(new Case { Name = "text.long-text-letter-by-letter", Run = TextEffectCases.LongTextLetterByLetter });
            list.Add(new Case { Name = "text.sweep", Run = TextEffectCases.Sweep });
            list.Add(new Case { Name = "text.sweep-enters", Run = TextEffectCases.SweepEnters });
            list.Add(new Case { Name = "text.sweep-shapes", Run = TextEffectCases.SweepShapes });
            list.Add(new Case { Name = "text.sweep-repeating", Run = TextEffectCases.SweepRepeating });
            list.Add(new Case { Name = "text.sweep-long-text", Run = TextEffectCases.SweepLongText });
            list.Add(new Case { Name = "text.alternate", Run = TextEffectCases.Alternate });
            list.Add(new Case { Name = "text.confetti", Run = TextEffectCases.Confetti });
            list.Add(new Case { Name = "text.highlight", Run = TextEffectCases.Highlight });
            list.Add(new Case { Name = "text.clock", Run = TextEffectCases.Clock });
            list.Add(new Case { Name = "text.time-of-day", Run = TextEffectCases.TimeOfDay });
        }

        /// <summary>Adds the cases for explicit scopes: what code makes belongs to the scope named where it is made.</summary>
        private static void AddScope(List<Case> list)
        {
            list.Add(new Case { Name = "scope.after-belongs-to-its-scope", Run = ScopeCases.AfterBelongsToItsScope });
            list.Add(new Case { Name = "scope.every-belongs-to-its-scope", Run = ScopeCases.EveryBelongsToItsScope });
            list.Add(new Case { Name = "scope.death-after-is-the-units", Run = ScopeCases.DeathAfterIsTheUnits });
            list.Add(new Case { Name = "scope.impact-after-is-the-missiles", Run = ContactCases.ImpactAfterIsTheMissiles });
            list.Add(new Case { Name = "scope.struck-after-is-the-units", Run = ContactCases.StruckAfterIsTheUnits });
            list.Add(new Case { Name = "scope.pair-after-is-the-interactions", Run = PairCases.PairAfterIsTheInteractions });
            list.Add(new Case { Name = "scope.part-after-runs", Run = PairCases.PartAfterRuns });
            list.Add(new Case { Name = "scope.helper-uses-its-scope", Run = ScopeCases.HelperUsesItsScope });
            list.Add(new Case { Name = "scope.create-adopts", Run = ContactCases.CreateAdopts });
            list.Add(new Case { Name = "scope.missile-has-no-default", Run = ContactCases.MissileHasNoDefault });
            list.Add(new Case { Name = "scope.interact-ends-with-scope-or-holder", Run = PairCases.InteractEndsWithScopeOrHolder });
            list.Add(new Case { Name = "scope.made-in-ended-scope-ends", Run = ContactCases.MadeInEndedScopeEnds });
            list.Add(new Case { Name = "scope.timer-ended-as-made-is-reported", Run = ScopeCases.TimerEndedAsMadeIsReported });
            list.Add(new Case { Name = "scope.timer-in-ended-scope-is-reported", Run = ScopeCases.TimerInEndedScopeIsReported });
            list.Add(new Case { Name = "scope.timer-made-while-ending-is-reported", Run = ScopeCases.TimerMadeWhileEndingIsReported });
            list.Add(new Case { Name = "scope.impact-timer-is-reported", Run = ContactCases.ImpactTimerIsReported });
            list.Add(new Case { Name = "scope.timer-ended-by-itself", Run = ScopeCases.TimerEndedByItself });
            list.Add(new Case { Name = "scope.timer-ended-later", Run = ScopeCases.TimerEndedLater });
            list.Add(new Case { Name = "scope.timer-that-ran", Run = ScopeCases.TimerThatRan });
            list.Add(new Case { Name = "scope.refused-every-once", Run = ScopeCases.RefusedEveryOnce });
            list.Add(new Case { Name = "scope.buff-ended-as-applied-is-quiet", Run = BuffCases.EndedAsAppliedIsQuiet });
            list.Add(new Case { Name = "scope.area-ended-as-added-is-quiet", Run = AreaCases.EndedAsAddedIsQuiet });
            list.Add(new Case { Name = "scope.expire-timer-is-reported", Run = ScopeCases.ExpireTimerIsReported });
            list.Add(new Case { Name = "scope.pair-after-in-part-is-reported", Run = PairCases.PairAfterInPartIsReported });
            list.Add(new Case { Name = "scope.interact-ends-with-holder-then-scope", Run = PairCases.InteractEndsWithHolderThenScope });
        }

        /// <summary>Adds the cases for the full audit's doc fixes: OrderedMap and OrderedSet in a foreach, and the guide's forms to write instead.</summary>
        private static void AddAuditDocs(List<Case> list)
        {
            list.Add(new Case { Name = "collections.set-foreach-keeps-order", Run = AuditDocsCases.SetForeachKeepsOrder });
            list.Add(new Case { Name = "collections.set-foreach-change-throws", Run = AuditDocsCases.SetForeachChangeThrows });
            list.Add(new Case { Name = "collections.map-foreach-keeps-order", Run = AuditDocsCases.MapForeachKeepsOrder });
            list.Add(new Case { Name = "collections.map-foreach-change-throws", Run = AuditDocsCases.MapForeachChangeThrows });
            list.Add(new Case { Name = "collections.foreach-left-early", Run = AuditDocsCases.ForeachLeftEarly });
            list.Add(new Case { Name = "collections.linq", Run = AuditDocsCases.Linq });
            list.Add(new Case { Name = "forms.using-statement-in-loop", Run = AuditDocsCases.UsingStatementInLoop });
            list.Add(new Case { Name = "forms.property-pattern-on-null", Run = AuditDocsCases.PropertyPatternOnNull });
            list.Add(new Case { Name = "forms.local-function-before-switch", Run = AuditDocsCases.LocalFunctionBeforeSwitch });
            list.Add(new Case { Name = "forms.readonly-struct-in-list", Run = AuditDocsCases.ReadonlyStructInList });
            list.Add(new Case { Name = "forms.number-formats", Run = AuditDocsCases.NumberFormats });
            list.Add(new Case { Name = "forms.long-const-string-field", Run = AuditDocsCases.LongConstStringField });
        }

        /// <summary>Adds the cases for the full audit's fixes to core's lifecycle.</summary>
        private static void AddLifecycle(List<Case> list)
        {
            list.Add(new Case { Name = "lifecycle.units-the-game-makes-are-met", Run = LifecycleCases.UnitsTheGameMakesAreMet });
            list.Add(new Case { Name = "reentry.drop-inside-pick-up", Run = ReentryCases.DropInsidePickUp });
            list.Add(new Case { Name = "reentry.removed-inside-pick-up", Run = ReentryCases.RemovedInsidePickUp });
            list.Add(new Case { Name = "reentry.drop-while-removed", Run = ReentryCases.DropWhileRemoved });
            list.Add(new Case { Name = "reentry.removed-inside-drop", Run = ReentryCases.RemovedInsideDrop });
            list.Add(new Case { Name = "reentry.killed-while-removed", Run = ReentryCases.KilledWhileRemoved });
            list.Add(new Case { Name = "reentry.hurt-while-removed", Run = ReentryCases.HurtWhileRemoved });
            list.Add(new Case { Name = "reentry.removed-before-player-hears", Run = ReentryCases.RemovedBeforePlayerHears });
            list.Add(new Case { Name = "reentry.throwing-unit-constructor", Run = ReentryCases.ThrowingUnitConstructor });
            list.Add(new Case { Name = "reentry.throwing-item-constructor", Run = ReentryCases.ThrowingItemConstructor });
            list.Add(new Case { Name = "reentry.throwing-class-in-register-step", Run = ReentryCases.ThrowingClassInRegisterStep });
            list.Add(new Case { Name = "reentry.item-setters-after-game-removal", Run = ReentryCases.ItemSettersAfterGameRemoval });
            list.Add(new Case { Name = "reentry.unit-setters-after-game-removal", Run = ReentryCases.UnitSettersAfterGameRemoval });
            list.Add(new Case { Name = "reentry.killed-then-removed-reads-no-life", Run = ReentryCases.KilledThenRemovedReadsNoLife });
            list.Add(new Case { Name = "reentry.tint-clamps", Run = ReentryCases.TintClamps });
            list.Add(new Case { Name = "reentry.of-game-removed-unit", Run = ReentryCases.OfGameRemovedUnit });
            list.Add(new Case { Name = "reentry.progress-of-removed-hero", Run = ReentryCases.ProgressOfRemovedHero });
            list.Add(new Case { Name = "lifecycle.local-throw-keeps-sync", Run = LifecycleCases.LocalThrowKeepsSync });
            list.Add(new Case { Name = "lifecycle.death-respawn-outlives-corpse", Run = LifecycleCases.DeathRespawnOutlivesCorpse });
            list.Add(new Case { Name = "lifecycle.refresh-takes-new-duration", Run = LifecycleCases.RefreshTakesNewDuration });
            list.Add(new Case { Name = "lifecycle.pair-buff-belongs-to-unit", Run = LifecycleCases.PairBuffBelongsToUnit });
            list.Add(new Case { Name = "lifecycle.illusion-is-plain", Run = LifecycleCases.IllusionIsPlain });
            list.Add(new Case { Name = "lifecycle.shared-component-refused", Run = LifecycleCases.SharedComponentRefused });
            list.Add(new Case { Name = "lifecycle.buff-reapplied-to-itself-refused", Run = LifecycleCases.BuffReappliedToItselfRefused });
            list.Add(new Case { Name = "lifecycle.remove-others-component-refused", Run = LifecycleCases.RemoveOthersComponentRefused });
            list.Add(new Case { Name = "lifecycle.adopt-again-refused", Run = LifecycleCases.AdoptAgainRefused });
            list.Add(new Case { Name = "lifecycle.removed-hero-drop-makes-no-second", Run = LifecycleCases.RemovedHeroDropMakesNoSecond });
            list.Add(new Case { Name = "lifecycle.created-and-removed-enter-makes-no-second", Run = LifecycleCases.CreatedAndRemovedEnterMakesNoSecond });
            list.Add(new Case { Name = "lifecycle.hero-lowered", Run = LifecycleCases.HeroLowered });
            list.Add(new Case { Name = "lifecycle.helper-named-like-reaction", Run = LifecycleCases.HelperNamedLikeReaction });
            list.Add(new Case { Name = "lifecycle.hiding-method-is-no-reaction", Run = LifecycleCases.HidingMethodIsNoReaction });
            list.Add(new Case { Name = "lifecycle.unit-created-only-when-it-comes-in", Run = LifecycleCases.UnitCreatedOnlyWhenItComesIn });
            list.Add(new Case { Name = "lifecycle.ended-owner-runs-neither-created-nor-leave", Run = LifecycleCases.EndedOwnerRunsNeitherCreatedNorLeave });
            list.Add(new Case { Name = "lifecycle.buff-carrier-survives-morph", Run = LifecycleCases.BuffCarrierSurvivesMorph });
            list.Add(new Case { Name = "lifecycle.buff-applied-in-death-stays", Run = LifecycleCases.BuffAppliedInDeathStays });
            list.Add(new Case { Name = "lifecycle.create-of-unknown-type-names-it", Run = LifecycleCases.CreateOfUnknownTypeNamesIt });
        }

        /// <summary>Adds the debug mode's cases; they exist only in a development build, as the debug mode does.</summary>
        private static void AddDebug(List<Case> list)
        {
            // Every case here must be named debug.*: the release Lua test leaves those out of its count by that prefix.
#if DEBUG
            list.Add(new Case { Name = "debug.clock.halt", Run = ClockCases.Halt });
            list.Add(new Case { Name = "debug.clock.step-advances", Run = ClockCases.StepAdvances });
            list.Add(new Case { Name = "debug.clock.speed", Run = ClockCases.Speed });
            list.Add(new Case { Name = "debug.clock.no-jump", Run = ClockCases.NoJump });
            list.Add(new Case { Name = "debug.clock.timer-while-halted", Run = ClockCases.TimerWhileHalted });
            list.Add(new Case { Name = "debug.clock.halt-inside-timer", Run = ClockCases.HaltInsideTimer });
            list.Add(new Case { Name = "debug.clock.catchup", Run = ClockCases.Catchup });
            list.Add(new Case { Name = "debug.clock.halted-timer-ended-later-is-quiet", Run = ClockCases.HaltedTimerEndedLaterIsQuiet });
            list.Add(new Case { Name = "debug.clock.stepped-timer-ended-later-is-quiet", Run = ClockCases.SteppedTimerEndedLaterIsQuiet });
            list.Add(new Case { Name = "debug.clock.halted-timer-ended-as-made-is-reported", Run = ClockCases.HaltedTimerEndedAsMadeIsReported });
            list.Add(new Case { Name = "debug.session.off-at-start", Run = SessionCases.OffAtStart });
            list.Add(new Case { Name = "debug.actors.actor-before-added", Run = ActorCases.ActorBeforeAdded });
            list.Add(new Case { Name = "debug.session.toggle", Run = SessionCases.Toggle });
            list.Add(new Case { Name = "debug.session.halt-holds", Run = SessionCases.HaltHolds });
            list.Add(new Case { Name = "debug.abilities.level-out-of-range-reported", Run = UnitAbilityCases.LevelOutOfRangeReported });
            list.Add(new Case { Name = "debug.abilities.level-of-missing-reported", Run = UnitAbilityCases.LevelOfMissingReported });
            list.Add(new Case { Name = "debug.hold.ended-holder-reported", Run = HoldCases.EndedHolderReported });
            list.Add(new Case { Name = "debug.dummy.level-reported", Run = DummyCases.LevelReported });
            list.Add(new Case { Name = "debug.dummy.ended-scope-reported", Run = DummyCases.EndedScopeReported });
            list.Add(new Case { Name = "debug.dummy.research-reported", Run = DummyCases.ResearchReported });
            list.Add(new Case { Name = "debug.dummy.self-only-reported", Run = DummyCases.SelfOnlyReported });
            list.Add(new Case { Name = "debug.dummy.nested-reported", Run = DummyCases.NestedReported });
            list.Add(new Case { Name = "debug.dummy.no-order-reported", Run = DummyCases.NoOrderReported });
            list.Add(new Case { Name = "debug.item.carry-not-carried-reported", Run = CarryCases.NotCarriedReported });
            list.Add(new Case { Name = "debug.realtime.now-early-reported", Run = RealTimeCases.NowEarlyReported });
            list.Add(new Case { Name = "debug.chat.debug-name-reported", Run = ChatCases.DebugNameReported });
            list.Add(new Case { Name = "debug.save.too-long-reported", Run = SaveCases.TooLongReported });
            list.Add(new Case { Name = "debug.session.halt-free", Run = SessionCases.HaltFree });
            list.Add(new Case { Name = "debug.session.map-paused-stays", Run = SessionCases.MapPausedStays });
            list.Add(new Case { Name = "debug.session.new-unit-held", Run = SessionCases.NewUnitHeld });
            list.Add(new Case { Name = "debug.session.step-free", Run = SessionCases.StepFree });
            list.Add(new Case { Name = "debug.session.step-held", Run = SessionCases.StepHeld });
            list.Add(new Case { Name = "debug.session.step-dropped", Run = SessionCases.StepDropped });
            list.Add(new Case { Name = "debug.session.resume-mid-step", Run = SessionCases.ResumeMidStep });
            list.Add(new Case { Name = "debug.session.speeds", Run = SessionCases.Speeds });
            list.Add(new Case { Name = "debug.session.step-count", Run = SessionCases.StepCount });
            list.Add(new Case { Name = "debug.session.off-restores", Run = SessionCases.OffRestores });
            list.Add(new Case { Name = "debug.session.select", Run = SessionCases.Select });
            list.Add(new Case { Name = "debug.session.select-next", Run = SessionCases.SelectNext });
            list.Add(new Case { Name = "debug.session.select-nothing", Run = SessionCases.SelectNothing });
            list.Add(new Case { Name = "debug.session.facade", Run = SessionCases.Facade });
            list.Add(new Case { Name = "debug.session.halt-from-code-turns-on", Run = SessionCases.HaltFromCodeTurnsOn });
            list.Add(new Case { Name = "debug.session.held-h-toggles-once", Run = SessionCases.HeldHTogglesOnce });
            list.Add(new Case { Name = "debug.session.held-unit-removed", Run = SessionCases.HeldUnitRemoved });
            list.Add(new Case { Name = "debug.session.breakpoint-escapes", Run = SessionCases.BreakpointEscapes });
            list.Add(new Case { Name = "debug.session.right-ctrl-selects", Run = SessionCases.RightCtrlSelects });
            list.Add(new Case { Name = "debug.session.step-bare", Run = SessionCases.StepBare });
            list.Add(new Case { Name = "debug.session.speed-bare", Run = SessionCases.SpeedBare });
            list.Add(new Case { Name = "debug.session.ended-selected-reads-null", Run = SessionCases.EndedSelectedReadsNull });
            list.Add(new Case { Name = "debug.panel.status", Run = PanelCases.Status });
            list.Add(new Case { Name = "debug.panel.unit", Run = PanelCases.SelectedUnit });
            list.Add(new Case { Name = "debug.panel.numbers", Run = PanelCases.Numbers });
            list.Add(new Case { Name = "debug.panel.cap", Run = PanelCases.Cap });
            list.Add(new Case { Name = "debug.panel.throwing-shown", Run = PanelCases.ThrowingShown });
            list.Add(new Case { Name = "debug.panel.refresh", Run = PanelCases.Refresh });
            list.Add(new Case { Name = "debug.panel.ended", Run = PanelCases.Ended });
            list.Add(new Case { Name = "debug.panel.off", Run = PanelCases.Off });
            list.Add(new Case { Name = "debug.panel.reuse", Run = PanelCases.Reuse });
            list.Add(new Case { Name = "debug.panel.stats", Run = PanelCases.Stats });
            list.Add(new Case { Name = "debug.panel.free-halt-follows", Run = PanelCases.FreeHaltFollows });
            list.Add(new Case { Name = "debug.interactions.candidates-no-tracking", Run = DebugViewCases.CandidatesNoTracking });
            list.Add(new Case { Name = "debug.interactions.lines", Run = DebugViewCases.Lines });
            list.Add(new Case { Name = "debug.interactions.paired-by", Run = DebugViewCases.PairedBy });
            list.Add(new Case { Name = "debug.interactions.draw", Run = DebugViewCases.Draw });
            list.Add(new Case { Name = "debug.interactions.pair-limit", Run = DebugViewCases.PairLimit });
            list.Add(new Case { Name = "debug.interactions.gizmo-ends", Run = DebugViewCases.GizmoEnds });
            list.Add(new Case { Name = "debug.interactions.stats", Run = DebugViewCases.Stats });
            list.Add(new Case { Name = "debug.interactions.step-length", Run = DebugViewCases.StepLength });
            list.Add(new Case { Name = "debug.areas.lines", Run = AreasViewCases.Lines });
            list.Add(new Case { Name = "debug.areas.outlines", Run = AreasViewCases.Outlines });
            list.Add(new Case { Name = "debug.areas.not-in-any", Run = AreasViewCases.NotInAny });
            list.Add(new Case { Name = "debug.areas.region", Run = AreasViewCases.Region });
            list.Add(new Case { Name = "debug.areas.no-tracker", Run = AreasViewCases.NoTracker });
            list.Add(new Case { Name = "debug.areas.registered", Run = AreasViewCases.Registered });
            list.Add(new Case { Name = "debug.physics.lines", Run = PhysicsViewCases.Lines });
            list.Add(new Case { Name = "debug.physics.stats", Run = PhysicsViewCases.Stats });
#endif
        }

        /// <summary>Adds the interactions package's cases.</summary>
        private static void AddInteractions(List<Case> list)
        {
            list.Add(new Case { Name = "interactions.grid.cells", Run = InteractionsGridCases.Cells });
            list.Add(new Case { Name = "interactions.grid.move", Run = InteractionsGridCases.Move });
            list.Add(new Case { Name = "interactions.grid.collect", Run = InteractionsGridCases.Collect });
            list.Add(new Case { Name = "interactions.grid.edge", Run = InteractionsGridCases.Edge });
            list.Add(new Case { Name = "interactions.grid.lazy", Run = InteractionsGridCases.Lazy });
            list.Add(new Case { Name = "interactions.grid.remove", Run = InteractionsGridCases.Remove });
            list.Add(new Case { Name = "interactions.grid.settings", Run = InteractionsGridCases.Settings });
            list.Add(new Case { Name = "interactions.grid.settings-locked", Run = InteractionsGridCases.SettingsLocked });
            list.Add(new Case { Name = "interactions.grid.reset", Run = InteractionsGridCases.Reset });
            list.Add(new Case { Name = "interactions.grid.settings-checked", Run = InteractionsGridCases.SettingsChecked });
            list.Add(new Case { Name = "interactions.gizmo.create", Run = GizmoCases.Create });
            list.Add(new Case { Name = "interactions.gizmo.tick", Run = GizmoCases.Tick });
            list.Add(new Case { Name = "interactions.gizmo.tick-rounding", Run = GizmoCases.TickRounding });
            list.Add(new Case { Name = "interactions.gizmo.no-tick", Run = GizmoCases.NoTick });
            list.Add(new Case { Name = "interactions.gizmo.move", Run = GizmoCases.Move });
            list.Add(new Case { Name = "interactions.gizmo.ends-with-owner", Run = GizmoCases.EndsWithOwner });
            list.Add(new Case { Name = "interactions.gizmo.leave-in-grid", Run = GizmoCases.LeaveInGrid });
            list.Add(new Case { Name = "interactions.gizmo.cycle-stops", Run = GizmoCases.CycleStops });
            list.Add(new Case { Name = "interactions.gizmo.end-in-tick", Run = GizmoCases.EndInTick });
            list.Add(new Case { Name = "interactions.gizmo.made-in-tick", Run = GizmoCases.MadeInTick });
            list.Add(new Case { Name = "interactions.gizmo.long-tick", Run = GizmoCases.LongTick });
            list.Add(new Case { Name = "interactions.gizmo.cycle-lists", Run = GizmoCases.CycleLists });
            list.Add(new Case { Name = "interactions.gizmo.throwing-tick", Run = GizmoCases.ThrowingTick });
            list.Add(new Case { Name = "interactions.gizmo.throwing-reactions", Run = GizmoCases.ThrowingReactions });
            list.Add(new Case { Name = "interactions.gizmo.shadowed-fields", Run = GizmoCases.ShadowedFields });
            list.Add(new Case { Name = "interactions.gizmo.components", Run = GizmoCases.Components });
            list.Add(new Case { Name = "interactions.gizmo.add-later", Run = GizmoCases.AddLater });
            list.Add(new Case { Name = "interactions.cycle.before-gizmos", Run = GizmoCases.BeforeGizmos });
            list.Add(new Case { Name = "interactions.cycle.awake", Run = GizmoCases.Awake });
            list.Add(new Case { Name = "interactions.tracking.only-when-asked", Run = TrackingCases.OnlyWhenAsked });
            list.Add(new Case { Name = "interactions.tracking.units-filled", Run = TrackingCases.UnitsFilled });
            list.Add(new Case { Name = "interactions.tracking.kinds-from-types", Run = TrackingCases.KindsFromTypes });
            list.Add(new Case { Name = "interactions.tracking.no-place-class", Run = TrackingCases.NoPlaceClass });
            list.Add(new Case { Name = "interactions.tracking.cell-check-spread", Run = TrackingCases.CellCheckSpread });
            list.Add(new Case { Name = "interactions.tracking.cell-check-counts", Run = TrackingCases.CellCheckCounts });
            list.Add(new Case { Name = "interactions.tracking.cell-check-after-ending", Run = TrackingCases.CellCheckAfterEnding });
            list.Add(new Case { Name = "interactions.tracking.no-place", Run = TrackingCases.NoPlace });
            list.Add(new Case { Name = "interactions.tracking.revived", Run = TrackingCases.Revived });
            list.Add(new Case { Name = "interactions.tracking.items-filled", Run = TrackingCases.ItemsFilled });
            list.Add(new Case { Name = "interactions.tracking.destructables-filled", Run = TrackingCases.DestructablesFilled });
            list.Add(new Case { Name = "interactions.tracking.ending", Run = TrackingCases.Ending });
            list.Add(new Case { Name = "interactions.tracking.ending-unseen", Run = TrackingCases.EndingUnseen });
            list.Add(new Case { Name = "interactions.tracking.new-ground-item", Run = TrackingCases.NewGroundItem });
            list.Add(new Case { Name = "interactions.tracking.read-once", Run = TrackingCases.ReadOnce });
            list.Add(new Case { Name = "interactions.tracking.moved", Run = TrackingCases.Moved });
            list.Add(new Case { Name = "interactions.tracking.read-outside-step", Run = TrackingCases.ReadOutsideStep });
            list.Add(new Case { Name = "interactions.tracking.gizmo-place", Run = TrackingCases.GizmoPlace });
            list.Add(new Case { Name = "interactions.tracking.first-use-items", Run = TrackingCases.FirstUseItems });
            list.Add(new Case { Name = "interactions.tracking.first-use-units", Run = TrackingCases.FirstUseUnits });
            list.Add(new Case { Name = "interactions.tracking.install-again", Run = TrackingCases.InstallAgain });
            list.Add(new Case { Name = "interactions.tracking.unit-read", Run = TrackingCases.UnitRead });
            list.Add(new Case { Name = "interactions.tracking.cell-check-fails", Run = TrackingCases.CellCheckFails });
            list.Add(new Case { Name = "interactions.find.in-range", Run = FindCases.InRange });
            list.Add(new Case { Name = "interactions.find.kinds", Run = FindCases.Kinds });
            list.Add(new Case { Name = "interactions.find.no-place-class", Run = FindCases.NoPlaceClass });
            list.Add(new Case { Name = "interactions.find.filter", Run = FindCases.Filter });
            list.Add(new Case { Name = "interactions.find.fills", Run = FindCases.Fills });
            list.Add(new Case { Name = "interactions.find.in-rect", Run = FindCases.InRect });
            list.Add(new Case { Name = "interactions.find.in-polygon", Run = FindCases.InPolygon });
            list.Add(new Case { Name = "interactions.find.along-line", Run = FindCases.AlongLine });
            list.Add(new Case { Name = "interactions.find.along-slanted-line", Run = FindCases.AlongSlantedLine });
            list.Add(new Case { Name = "interactions.find.closest", Run = FindCases.Closest });
            list.Add(new Case { Name = "interactions.find.closest-n", Run = FindCases.ClosestN });
            list.Add(new Case { Name = "interactions.find.stale", Run = FindCases.Stale });
            list.Add(new Case { Name = "interactions.find.no-place", Run = FindCases.NoPlace });
            list.Add(new Case { Name = "interactions.find.order", Run = FindCases.Order });
            list.Add(new Case { Name = "interactions.find.inside-reaction", Run = FindCases.InsideReaction });
            list.Add(new Case { Name = "interactions.find.nested", Run = FindCases.Nested });
            list.Add(new Case { Name = "interactions.find.throwing", Run = FindCases.Throwing });
            list.Add(new Case { Name = "interactions.find.throwing-closest", Run = FindCases.ThrowingClosest });
            list.Add(new Case { Name = "interactions.find.edge", Run = FindCases.Edge });
            list.Add(new Case { Name = "interactions.find.local-tracked", Run = FindCases.LocalTracked });
            list.Add(new Case { Name = "interactions.find.local-cached", Run = FindCases.LocalCached });
            list.Add(new Case { Name = "interactions.find.local-untracked", Run = FindCases.LocalUntracked });
            list.Add(new Case { Name = "interactions.find.local-mixed", Run = FindCases.LocalMixed });
            list.Add(new Case { Name = "interactions.find.in-share-read", Run = FindCases.InShareRead });
            list.Add(new Case { Name = "interactions.interaction.meet-tick-part", Run = InteractionCases.MeetTickPart });
            list.Add(new Case { Name = "interactions.interaction.exact-range", Run = InteractionCases.ExactRange });
            list.Add(new Case { Name = "interactions.interaction.accepts", Run = InteractionCases.Accepts });
            list.Add(new Case { Name = "interactions.interaction.not-holder", Run = InteractionCases.NotHolder });
            list.Add(new Case { Name = "interactions.interaction.two-ways", Run = InteractionCases.TwoWays });
            list.Add(new Case { Name = "interactions.interaction.owned-by-buff", Run = InteractionCases.OwnedByBuff });
            list.Add(new Case { Name = "interactions.interaction.holder-ends", Run = InteractionCases.HolderEnds });
            list.Add(new Case { Name = "interactions.interaction.other-ends", Run = InteractionCases.OtherEnds });
            list.Add(new Case { Name = "interactions.interaction.holder-no-place", Run = InteractionCases.HolderNoPlace });
            list.Add(new Case { Name = "interactions.interaction.ends-in-meet", Run = InteractionCases.EndsInMeet });
            list.Add(new Case { Name = "interactions.interaction.ends-in-tick", Run = InteractionCases.EndsInTick });
            list.Add(new Case { Name = "interactions.interaction.other-ends-in-tick", Run = InteractionCases.OtherEndsInTick });
            list.Add(new Case { Name = "interactions.interaction.interval", Run = InteractionCases.Interval });
            list.Add(new Case { Name = "interactions.interaction.paused-interaction", Run = InteractionCases.PausedInteraction });
            list.Add(new Case { Name = "interactions.interaction.error-reported", Run = InteractionCases.ErrorReported });
            list.Add(new Case { Name = "interactions.interaction.made-in-reaction", Run = InteractionCases.MadeInReaction });
            list.Add(new Case { Name = "interactions.interaction.pairs-view", Run = InteractionCases.PairsView });
            list.Add(new Case { Name = "interactions.interaction.interact-checks", Run = InteractionCases.InteractChecks });
            list.Add(new Case { Name = "interactions.interaction.interact-in-created", Run = InteractionCases.InteractInCreated });
            list.Add(new Case { Name = "interactions.interaction.item-holder", Run = InteractionCases.ItemHolder });
            list.Add(new Case { Name = "interactions.interaction.destructable-holder", Run = InteractionCases.DestructableHolder });
            list.Add(new Case { Name = "interactions.interaction.pause-in-reaction", Run = InteractionCases.PauseInReaction });
            list.Add(new Case { Name = "interactions.interaction.shadowed-fields", Run = InteractionCases.ShadowedFields });
            list.Add(new Case { Name = "interactions.interaction.inner-loops", Run = InteractionCases.InnerLoops });
            list.Add(new Case { Name = "interactions.interaction.guarded-reactions", Run = InteractionCases.GuardedReactions });
            list.Add(new Case { Name = "interactions.interaction.check-fails", Run = InteractionCases.CheckFails });
            list.Add(new Case { Name = "interactions.interaction.other-kinds", Run = InteractionCases.OtherKinds });
            list.Add(new Case { Name = "interactions.interaction.spread-first-checks", Run = InteractionCases.SpreadFirstChecks });
            list.Add(new Case { Name = "interactions.interaction.corner-cells", Run = InteractionCases.CornerCells });
            list.Add(new Case { Name = "interactions.guard.turn", Run = GuardCases.Turn });
            list.Add(new Case { Name = "interactions.pair.wait", Run = PairCases.Wait });
            list.Add(new Case { Name = "interactions.pair.try-cooldown", Run = PairCases.TryCooldown });
            list.Add(new Case { Name = "interactions.pair.pause-resume", Run = PairCases.PauseResume });
            list.Add(new Case { Name = "interactions.pair.stop", Run = PairCases.Stop });
            list.Add(new Case { Name = "interactions.pair.data-and-since", Run = PairCases.DataAndSince });
            list.Add(new Case { Name = "interactions.pair.pair-owns", Run = PairCases.PairOwns });
            list.Add(new Case { Name = "interactions.tree", Run = TreeCases.Tree });
        }

        /// <summary>Adds the physics package's cases.</summary>
        private static void AddPhysics(List<Case> list)
        {
            list.Add(new Case { Name = "physics.gravity-setting", Run = BallisticsCases.GravitySetting });
            list.Add(new Case { Name = "physics.install", Run = BallisticsCases.Install });
            list.Add(new Case { Name = "ballistics.velocity-hits", Run = BallisticsCases.VelocityHits });
            list.Add(new Case { Name = "ballistics.velocity-slanted", Run = BallisticsCases.VelocitySlanted });
            list.Add(new Case { Name = "ballistics.out-of-reach", Run = BallisticsCases.OutOfReach });
            list.Add(new Case { Name = "ballistics.speed-for-angle", Run = BallisticsCases.SpeedForAngle });
            list.Add(new Case { Name = "ballistics.above", Run = BallisticsCases.Above });
            list.Add(new Case { Name = "ballistics.speed-then-velocity", Run = BallisticsCases.SpeedThenVelocity });
            list.Add(new Case { Name = "ballistics.gravity-zero", Run = BallisticsCases.GravityZero });
            list.Add(new Case { Name = "ballistics.vertical", Run = BallisticsCases.Vertical });
            list.Add(new Case { Name = "flight.straight", Run = FlightCases.Straight });
            list.Add(new Case { Name = "flight.gravity", Run = FlightCases.Gravity });
            list.Add(new Case { Name = "flight.acceleration", Run = FlightCases.Acceleration });
            list.Add(new Case { Name = "flight.arc", Run = FlightCases.Arc });
            list.Add(new Case { Name = "flight.aim-unit", Run = FlightCases.AimUnit });
            list.Add(new Case { Name = "flight.aim-point-level", Run = FlightCases.AimPointLevel });
            list.Add(new Case { Name = "flight.follows-ground", Run = FlightCases.FollowsGround });
            list.Add(new Case { Name = "flight.duration", Run = FlightCases.Duration });
            list.Add(new Case { Name = "flight.max-distance", Run = FlightCases.MaxDistance });
            list.Add(new Case { Name = "flight.bounds", Run = FlightCases.Bounds });
            list.Add(new Case { Name = "flight.owner", Run = FlightCases.Ownership });
            list.Add(new Case { Name = "flight.facing", Run = FlightCases.Facing });
            list.Add(new Case { Name = "flight.fire-at-self", Run = FlightCases.FireAtSelf });
            list.Add(new Case { Name = "flight.effect", Run = FlightCases.Effects });
            list.Add(new Case { Name = "flight.made-at-launch", Run = FlightCases.MadeAtLaunch });
            list.Add(new Case { Name = "flight.gizmo-create", Run = FlightCases.GizmoCreate });
            list.Add(new Case { Name = "flight.end-mid-pass", Run = FlightCases.EndMidPass });
            list.Add(new Case { Name = "ground.stop", Run = GroundCases.Stop });
            list.Add(new Case { Name = "ground.bounce", Run = GroundCases.Bounce });
            list.Add(new Case { Name = "ground.slide-rest", Run = GroundCases.SlideRest });
            list.Add(new Case { Name = "ground.crest", Run = GroundCases.Crest });
            list.Add(new Case { Name = "ground.cliff", Run = GroundCases.Cliff });
            list.Add(new Case { Name = "ground.none", Run = GroundCases.None });
            list.Add(new Case { Name = "ground.reads", Run = GroundCases.Reads });
            list.Add(new Case { Name = "ground.water", Run = GroundCases.Water });
            list.Add(new Case { Name = "ground.bounce-settles", Run = GroundCases.BounceSettles });
            list.Add(new Case { Name = "ground.impact-velocity", Run = GroundCases.ImpactVelocity });
            list.Add(new Case { Name = "ground.wake-moved", Run = GroundCases.WakeMoved });
            list.Add(new Case { Name = "ground.fast-ridge", Run = GroundCases.FastRidge });
            list.Add(new Case { Name = "ground.cliff-roll", Run = GroundCases.CliffRoll });
            list.Add(new Case { Name = "ground.cliff-bounce", Run = GroundCases.CliffBounce });
            list.Add(new Case { Name = "ground.put-off", Run = GroundCases.PutOff });
            list.Add(new Case { Name = "ground.bounce-under-gravity", Run = GroundCases.BounceUnderGravity });
            list.Add(new Case { Name = "ground.bounce-friction", Run = GroundCases.BounceFriction });
            list.Add(new Case { Name = "ground.bounce-own-gravity", Run = GroundCases.BounceOwnGravity });
            list.Add(new Case { Name = "ground.bounce-no-gravity", Run = GroundCases.BounceNoGravity });
            list.Add(new Case { Name = "ground.cast-out-of-the-map", Run = GroundCases.CastOutOfTheMap });
            list.Add(new Case { Name = "homing.turn-rate", Run = ComponentCases.TurnRate });
            list.Add(new Case { Name = "homing.circle", Run = ComponentCases.Circle });
            list.Add(new Case { Name = "homing.lost", Run = ComponentCases.Lost });
            list.Add(new Case { Name = "homing.fire-sets-target", Run = ComponentCases.FireSetsTarget });
            list.Add(new Case { Name = "homing.arc", Run = ComponentCases.Arc });
            list.Add(new Case { Name = "homing.at-target", Run = ComponentCases.AtTarget });
            list.Add(new Case { Name = "shadow.follows", Run = ComponentCases.ShadowFollows });
            list.Add(new Case { Name = "shadow.zero-size", Run = ComponentCases.ShadowZeroSize });
            list.Add(new Case { Name = "sound.follows", Run = ComponentCases.SoundFollows });
            list.Add(new Case { Name = "push.knockback", Run = PushCases.Knockback });
            list.Add(new Case { Name = "push.blocked", Run = PushCases.Blocked });
            list.Add(new Case { Name = "push.fast-into-line", Run = PushCases.FastIntoLine });
            list.Add(new Case { Name = "push.slide-along", Run = PushCases.SlideAlong });
            list.Add(new Case { Name = "push.tree", Run = PushCases.Tree });
            list.Add(new Case { Name = "push.lift-land", Run = PushCases.LiftLand });
            list.Add(new Case { Name = "push.flyer", Run = PushCases.Flyer });
            list.Add(new Case { Name = "push.overlap", Run = PushCases.Overlap });
            list.Add(new Case { Name = "push.ends-on-death", Run = PushCases.EndsOnDeath });
            list.Add(new Case { Name = "push.structure", Run = PushCases.Structure });
            list.Add(new Case { Name = "push.body", Run = PushCases.BodyDefaults });
            list.Add(new Case { Name = "push.order-kept", Run = PushCases.OrderKept });
            list.Add(new Case { Name = "push.cell", Run = PushCases.Cell });
            list.Add(new Case { Name = "push.zero", Run = PushCases.Zero });
            list.Add(new Case { Name = "push.reaction-order", Run = PushCases.ReactionOrder });
            list.Add(new Case { Name = "push.slide-along-x", Run = PushCases.SlideAlongX });
            list.Add(new Case { Name = "push.reentrant", Run = PushCases.Reentrant });
            list.Add(new Case { Name = "push.hover", Run = PushCases.Hover });
            list.Add(new Case { Name = "push.grounded-names", Run = PushCases.GroundedNames });
            list.Add(new Case { Name = "push.thrown-step", Run = PushCases.ThrownStep });
            list.Add(new Case { Name = "contact.stop", Run = ContactCases.Stop });
            list.Add(new Case { Name = "contact.can-hit", Run = ContactCases.CanHit });
            list.Add(new Case { Name = "contact.order", Run = ContactCases.Order });
            list.Add(new Case { Name = "contact.bounce", Run = ContactCases.Bounce });
            list.Add(new Case { Name = "contact.pass", Run = ContactCases.Pass });
            list.Add(new Case { Name = "contact.fast", Run = ContactCases.Fast });
            list.Add(new Case { Name = "contact.inside", Run = ContactCases.Inside });
            list.Add(new Case { Name = "contact.top", Run = ContactCases.Top });
            list.Add(new Case { Name = "contact.resting", Run = ContactCases.Resting });
            list.Add(new Case { Name = "contact.sized", Run = ContactCases.Sized });
            list.Add(new Case { Name = "contact.mass", Run = ContactCases.Mass });
            list.Add(new Case { Name = "contact.destructable", Run = ContactCases.DestructableHit });
            list.Add(new Case { Name = "contact.item", Run = ContactCases.ItemHit });
            list.Add(new Case { Name = "contact.no-source", Run = ContactCases.NoSource });
            list.Add(new Case { Name = "contact.end-in-reaction", Run = ContactCases.EndInReaction });
            list.Add(new Case { Name = "contact.owner", Run = ContactCases.Ownership });
            list.Add(new Case { Name = "contact.none-costs-nothing", Run = ContactCases.NoneCostsNothing });
            list.Add(new Case { Name = "contact.dead-source", Run = ContactCases.DeadSource });
            list.Add(new Case { Name = "contact.settles-on-top", Run = ContactCases.SettlesOnTop });
            list.Add(new Case { Name = "contact.teleport", Run = ContactCases.Teleport });
            list.Add(new Case { Name = "missiles.meet-once", Run = MissileContactCases.MeetOnce });
            list.Add(new Case { Name = "missiles.own-outcomes", Run = MissileContactCases.OwnOutcomes });
            list.Add(new Case { Name = "missiles.mass", Run = MissileContactCases.Mass });
            list.Add(new Case { Name = "missiles.rest-then-pass", Run = MissileContactCases.RestThenPass });
            list.Add(new Case { Name = "missiles.sped-up", Run = MissileContactCases.SpedUp });
            list.Add(new Case { Name = "missiles.woken-frame", Run = MissileContactCases.WokenFrame });
            list.Add(new Case { Name = "missiles.none-side", Run = MissileContactCases.NoneSide });
            list.Add(new Case { Name = "missiles.rolling-meet", Run = MissileContactCases.RollingMeet });
            list.Add(new Case { Name = "missiles.bounce-none", Run = MissileContactCases.BounceNone });
            list.Add(new Case { Name = "wall.bounce", Run = WallCases.Bounce });
            list.Add(new Case { Name = "wall.slide", Run = WallCases.Slide });
            list.Add(new Case { Name = "wall.height", Run = WallCases.Height });
            list.Add(new Case { Name = "wall.fast", Run = WallCases.Fast });
            list.Add(new Case { Name = "wall.push", Run = WallCases.Push });
            list.Add(new Case { Name = "wall.checks", Run = WallCases.Checks });
            list.Add(new Case { Name = "wall.slide-under-gravity", Run = WallCases.SlideUnderGravity });
            list.Add(new Case { Name = "wall.slide-into-corner", Run = WallCases.SlideIntoCorner });
            list.Add(new Case { Name = "wall.wide-corners", Run = WallCases.WideCorners });
            list.Add(new Case { Name = "wall.goes", Run = WallCases.WallGoes });
            list.Add(new Case { Name = "wall.set", Run = WallCases.WallSet });
            list.Add(new Case { Name = "wall.slide-radius-zero", Run = WallCases.SlideRadiusZero });
            list.Add(new Case { Name = "wall.slide-radius-zero-far", Run = WallCases.SlideRadiusZeroFar });
            list.Add(new Case { Name = "wall.jumped-along", Run = WallCases.JumpedAlong });
            list.Add(new Case { Name = "wall.push-corner", Run = WallCases.PushCorner });
            list.Add(new Case { Name = "wall.push-on-wall", Run = WallCases.PushOnWall });
            list.Add(new Case { Name = "wall.push-glancing", Run = WallCases.PushGlancing });
            list.Add(new Case { Name = "deck.height", Run = DeckCases.Height });
            list.Add(new Case { Name = "deck.turned-scaled", Run = DeckCases.TurnedScaled });
            list.Add(new Case { Name = "deck.over-water", Run = DeckCases.OverWater });
            list.Add(new Case { Name = "deck.water-edge", Run = DeckCases.WaterEdge });
            list.Add(new Case { Name = "deck.under", Run = DeckCases.Under });
            list.Add(new Case { Name = "deck.unit-on", Run = DeckCases.UnitOn });
            list.Add(new Case { Name = "deck.push-rail", Run = DeckCases.PushRail });
            list.Add(new Case { Name = "deck.push-rail-body", Run = DeckCases.PushRailBody });
            list.Add(new Case { Name = "deck.push-ramp", Run = DeckCases.PushRamp });
            list.Add(new Case { Name = "deck.dies", Run = DeckCases.Dies });
            list.Add(new Case { Name = "deck.restored", Run = DeckCases.Restored });
            list.Add(new Case { Name = "deck.not-a-cylinder", Run = DeckCases.NotACylinder });
            list.Add(new Case { Name = "deck.slope-normal", Run = DeckCases.SlopeNormal });
        }

        /// <summary>Adds the cases for the full audit's fixes to the interactions package.</summary>
        private static void AddAuditInteractions(List<Case> list)
        {
            list.Add(new Case { Name = "interactions.audit.held-end-newest-first", Run = AuditInteractionCases.HeldEndNewestFirst });
            list.Add(new Case { Name = "interactions.audit.teleport-together-stays-paired", Run = AuditInteractionCases.TeleportTogetherStaysPaired });
            list.Add(new Case { Name = "interactions.audit.teleport-together-keeps-stop", Run = AuditInteractionCases.TeleportTogetherKeepsStop });
            list.Add(new Case { Name = "interactions.audit.teleport-apart-parts", Run = AuditInteractionCases.TeleportApartParts });
            list.Add(new Case { Name = "interactions.audit.guide-fire-aura-spares-changed-owner", Run = AuditInteractionCases.GuideFireAuraSparesChangedOwner });
            list.Add(new Case { Name = "interactions.audit.recheck-off-keeps-pair", Run = AuditInteractionCases.RecheckOffKeepsPair });
            list.Add(new Case { Name = "interactions.audit.recheck-refusal-parts-and-meets-again", Run = AuditInteractionCases.RecheckRefusalPartsAndMeetsAgain });
            list.Add(new Case { Name = "interactions.audit.recheck-throwing-accepts-refuses", Run = AuditInteractionCases.RecheckThrowingAcceptsRefuses });
            list.Add(new Case { Name = "interactions.audit.recheck-parts-once-per-meet", Run = AuditInteractionCases.RecheckPartsOncePerMeet });
            list.Add(new Case { Name = "interactions.audit.gizmo-leave-has-no-place", Run = AuditInteractionCases.GizmoLeaveHasNoPlace });
            list.Add(new Case { Name = "interactions.audit.cell-check-removal-keeps-round", Run = AuditInteractionCases.CellCheckRemovalKeepsRound });
            list.Add(new Case { Name = "interactions.audit.destructable-holder-tracked-alone", Run = AuditInteractionCases.DestructableHolderTrackedAlone });
            list.Add(new Case { Name = "interactions.audit.item-holder-tracked-alone", Run = AuditInteractionCases.ItemHolderTrackedAlone });
            list.Add(new Case { Name = "interactions.audit.unit-holder-of-gizmos-tracked-alone", Run = AuditInteractionCases.UnitHolderOfGizmosTrackedAlone });
            list.Add(new Case { Name = "interactions.audit.carried-item-has-no-place", Run = AuditInteractionCases.CarriedItemHasNoPlace });
        }

        /// <summary>Adds the areas package's cases.</summary>
        private static void AddAreas(List<Case> list)
        {
            list.Add(new Case { Name = "areas.shapes.named", Run = AreaShapeCases.Named });
            list.Add(new Case { Name = "areas.shapes.unknown-name", Run = AreaShapeCases.UnknownName });
            list.Add(new Case { Name = "areas.shapes.circle", Run = AreaShapeCases.Circle });
            list.Add(new Case { Name = "areas.shapes.polygon", Run = AreaShapeCases.Polygon });
            list.Add(new Case { Name = "areas.shapes.of", Run = AreaShapeCases.Of });
            list.Add(new Case { Name = "areas.shapes.not-installed", Run = AreaShapeCases.NotInstalled });
            list.Add(new Case { Name = "areas.shapes.wrong-length", Run = AreaShapeCases.WrongLength });
            list.Add(new Case { Name = "areas.area.enter-stay-leave", Run = AreaCases.EnterStayLeave });
            list.Add(new Case { Name = "areas.area.accepts", Run = AreaCases.Accepts });
            list.Add(new Case { Name = "areas.area.already-inside", Run = AreaCases.AlreadyInside });
            list.Add(new Case { Name = "areas.area.already-inside-edge", Run = AreaCases.AlreadyInsideEdge });
            list.Add(new Case { Name = "areas.area.sweep-and-event", Run = AreaCases.SweepAndEvent });
            list.Add(new Case { Name = "areas.area.death", Run = AreaCases.Death });
            list.Add(new Case { Name = "areas.area.removed", Run = AreaCases.Removed });
            list.Add(new Case { Name = "areas.area.area-ends", Run = AreaCases.AreaEnds });
            list.Add(new Case { Name = "areas.area.end-inside-enter", Run = AreaCases.EndInsideEnter });
            list.Add(new Case { Name = "areas.area.remove-inside-left", Run = AreaCases.RemoveInsideLeft });
            list.Add(new Case { Name = "areas.area.throwing", Run = AreaCases.Throwing });
            list.Add(new Case { Name = "areas.area.cells", Run = AreaCases.Cells });
            list.Add(new Case { Name = "areas.area.no-shape", Run = AreaCases.NoShape });
            list.Add(new Case { Name = "areas.area.region-before-install", Run = AreaCases.RegionBeforeInstall });
            list.Add(new Case { Name = "areas.area.fixed-once-added", Run = AreaCases.FixedOnceAdded });
            list.Add(new Case { Name = "areas.area.reads", Run = AreaCases.Reads });
            list.Add(new Case { Name = "areas.area.shared-timer", Run = AreaCases.SharedTimer });
            list.Add(new Case { Name = "areas.area.counts-from-first-unit", Run = AreaCases.CountsFromFirstUnit });
            list.Add(new Case { Name = "areas.area.hidden", Run = AreaCases.Hidden });
            list.Add(new Case { Name = "areas.area.hidden-then-gone", Run = AreaCases.HiddenThenGone });
            list.Add(new Case { Name = "areas.area.hidden-at-start", Run = AreaCases.HiddenAtStart });
            list.Add(new Case { Name = "areas.area.resurrected-reenters", Run = AreaCases.ResurrectedReenters });
            list.Add(new Case { Name = "areas.area.revive-not-twice", Run = AreaCases.ReviveNotTwice });
            list.Add(new Case { Name = "areas.area.corpse-removed", Run = AreaCases.CorpseRemoved });
            list.Add(new Case { Name = "areas.area.step-ends-both-areas", Run = AreaCases.StepEndsBothAreas });
            list.Add(new Case { Name = "areas.area.step-hands-over-and-restarts", Run = AreaCases.StepHandsOverAndRestarts });
            list.Add(new Case { Name = "areas.area.step-hands-over-and-keeps-running", Run = AreaCases.StepHandsOverAndKeepsRunning });
            list.Add(new Case { Name = "areas.area.step-leaves-and-reenters", Run = AreaCases.StepLeavesAndReenters });
        }

        /// <summary>Adds the cases for the full audit's fixes to areas and input.</summary>
        private static void AddAuditAreas(List<Case> list)
        {
            list.Add(new Case { Name = "audit.areas.fake-covers-as-the-game", Run = AuditAreaCases.FakeCoversAsTheGame });
            list.Add(new Case { Name = "audit.areas.fake-walks", Run = AuditAreaCases.FakeWalks });
            list.Add(new Case { Name = "audit.areas.small-circle", Run = AuditAreaCases.SmallCircle });
            list.Add(new Case { Name = "audit.areas.thin-polygon", Run = AuditAreaCases.ThinPolygon });
            list.Add(new Case { Name = "audit.areas.centre-cell-always", Run = AuditAreaCases.CentreCellAlways });
            list.Add(new Case { Name = "audit.areas.shared-edge", Run = AuditAreaCases.SharedEdge });
            list.Add(new Case { Name = "audit.areas.rect-rounded-out", Run = AuditAreaCases.RectRoundedOut });
            list.Add(new Case { Name = "audit.areas.shadowed-fields", Run = AuditAreaCases.ShadowedFields });
            list.Add(new Case { Name = "audit.areas.recheck-leaves-and-enters", Run = AuditAreaCases.RecheckLeavesAndEnters });
            list.Add(new Case { Name = "audit.areas.recheck-refused-at-entry", Run = AuditAreaCases.RecheckRefusedAtEntry });
            list.Add(new Case { Name = "audit.areas.recheck-off", Run = AuditAreaCases.RecheckOff });
            list.Add(new Case { Name = "audit.areas.recheck-forgets-leavers", Run = AuditAreaCases.RecheckForgetsLeavers });
            list.Add(new Case { Name = "audit.areas.recheck-ends-area", Run = AuditAreaCases.RecheckEndsArea });
            list.Add(new Case { Name = "audit.areas.corpse-checks-without-steps", Run = AuditAreaCases.CorpseChecksWithoutSteps });
            list.Add(new Case { Name = "audit.areas.no-tick-no-steps", Run = AuditAreaCases.NoTickNoSteps });
            list.Add(new Case { Name = "audit.areas.many-corpses", Run = AuditAreaCases.ManyCorpses });
            list.Add(new Case { Name = "audit.areas.left-while-ending", Run = AuditAreaCases.LeftWhileEnding });
            list.Add(new Case { Name = "audit.input.no-moves", Run = AuditInputCases.NoMoves });
            list.Add(new Case { Name = "audit.input.moves", Run = AuditInputCases.Moves });
            list.Add(new Case { Name = "audit.input.watchers-kept", Run = AuditInputCases.WatchersKept });
            list.Add(new Case { Name = "audit.input.watchers-changed-in-dispatch", Run = AuditInputCases.WatchersChangedInDispatch });
            list.Add(new Case { Name = "audit.input.camera-named-before-start", Run = AuditInputCases.CameraNamedBeforeStart });
        }

        /// <summary>Adds the order cases.</summary>
        private static void AddOrders(List<Case> list)
        {
            list.Add(new Case { Name = "orders.constants", Run = OrderCases.Constants });
            list.Add(new Case { Name = "orders.reactions-order", Run = OrderCases.ReactionsOrder });
            list.Add(new Case { Name = "orders.place-and-target", Run = OrderCases.PlaceAndTarget });
            list.Add(new Case { Name = "orders.inside-the-call", Run = OrderCases.InsideTheCall });
            list.Add(new Case { Name = "orders.games-own", Run = OrderCases.GamesOwn });
            list.Add(new Case { Name = "orders.only-when-wanted", Run = OrderCases.OnlyWhenWanted });
            list.Add(new Case { Name = "orders.nested", Run = OrderCases.Nested });
            list.Add(new Case { Name = "orders.last-three", Run = OrderCases.LastThree });
            list.Add(new Case { Name = "orders.repeat-takes-place", Run = OrderCases.RepeatTakesPlace });
            list.Add(new Case { Name = "orders.not-worth-it", Run = OrderCases.NotWorthIt });
            list.Add(new Case { Name = "orders.reissue", Run = OrderCases.Reissue });
            list.Add(new Case { Name = "orders.reissue-gone", Run = OrderCases.ReissueGone });
            list.Add(new Case { Name = "orders.spell-done", Run = OrderCases.SpellDone });
            list.Add(new Case { Name = "orders.potion-on-the-way", Run = OrderCases.PotionOnTheWay });
            list.Add(new Case { Name = "orders.after-stun", Run = OrderCases.AfterStun });
            list.Add(new Case { Name = "orders.after-idle-stun", Run = OrderCases.AfterIdleStun });
            list.Add(new Case { Name = "orders.after-stun-other", Run = OrderCases.AfterStunOther });
            list.Add(new Case { Name = "orders.current", Run = OrderCases.Current });
            list.Add(new Case { Name = "orders.cancel-gives-back", Run = OrderCases.CancelGivesBack });
            list.Add(new Case { Name = "orders.cancel-with-none", Run = OrderCases.CancelWithNone });
            list.Add(new Case { Name = "orders.cancel-and-replace", Run = OrderCases.CancelAndReplace });
            list.Add(new Case { Name = "orders.other-reactions-run", Run = OrderCases.OtherReactionsRun });
            list.Add(new Case { Name = "orders.removed-in-reaction", Run = OrderCases.RemovedInReaction });
            list.Add(new Case { Name = "orders.queued-cancel", Run = OrderCases.QueuedCancel });
            list.Add(new Case { Name = "orders.stop-then-refuse", Run = OrderCases.StopThenRefuse });
            list.Add(new Case { Name = "orders.build-then-refuse", Run = OrderCases.BuildThenRefuse });
            list.Add(new Case { Name = "orders.instant-then-refuse", Run = OrderCases.InstantThenRefuse });
            list.Add(new Case { Name = "orders.stop-in-reaction", Run = OrderCases.StopInReaction });
            list.Add(new Case { Name = "orders.cancel-and-stop", Run = OrderCases.CancelAndStop });
            list.Add(new Case { Name = "orders.target-given-back", Run = OrderCases.TargetGivenBack });
            list.Add(new Case { Name = "orders.patrol-under-way", Run = OrderCases.PatrolUnderWay });
        }

        /// <summary>Adds the input package's keyboard cases.</summary>
        private static void AddKeys(List<Case> list)
        {
            list.Add(new Case { Name = "input.keys.codes", Run = KeyCases.Codes });
            list.Add(new Case { Name = "input.keys.down-and-up", Run = KeyCases.DownAndUp });
            list.Add(new Case { Name = "input.keys.modifiers", Run = KeyCases.Modifiers });
            list.Add(new Case { Name = "input.keys.held-now", Run = KeyCases.HeldNow });
            list.Add(new Case { Name = "input.keys.unknown-code", Run = KeyCases.UnknownCode });
            list.Add(new Case { Name = "input.keys.before-install", Run = KeyCases.BeforeInstall });
            list.Add(new Case { Name = "input.keys.people-only", Run = KeyCases.PeopleOnly });
            list.Add(new Case { Name = "input.keys.order", Run = KeyCases.Order });
            list.Add(new Case { Name = "input.keys.once-per-press", Run = KeyCases.OncePerPress });
            list.Add(new Case { Name = "input.keys.long-gap", Run = KeyCases.LongGap });
            list.Add(new Case { Name = "input.keys.modifier-repeat", Run = KeyCases.ModifierRepeat });
            list.Add(new Case { Name = "input.keys.changed-in-reaction", Run = KeyCases.ChangedInReaction });
            list.Add(new Case { Name = "input.keys.added-to-player-in-reaction", Run = KeyCases.AddedToPlayerInReaction });
            list.Add(new Case { Name = "input.keys.throwing", Run = KeyCases.Throwing });
            list.Add(new Case { Name = "input.keys.press-only", Run = KeyCases.PressOnly });
            list.Add(new Case { Name = "input.keys.let-go-unseen", Run = KeyCases.LetGoUnseen });
            list.Add(new Case { Name = "input.keys.still-held", Run = KeyCases.StillHeld });
            list.Add(new Case { Name = "input.keys.down-again", Run = KeyCases.DownAgain });
            list.Add(new Case { Name = "input.keys.repeat-after-send", Run = KeyCases.RepeatAfterSend });
            list.Add(new Case { Name = "input.keys.press-after-release", Run = KeyCases.PressAfterRelease });
            list.Add(new Case { Name = "input.keys.already-up", Run = KeyCases.AlreadyUp });
            list.Add(new Case { Name = "input.keys.modifier-let-go", Run = KeyCases.ModifierLetGo });
            list.Add(new Case { Name = "input.keys.left-player", Run = KeyCases.LeftPlayer });
            list.Add(new Case { Name = "input.keys.only-sender", Run = KeyCases.OnlySender });
            list.Add(new Case { Name = "input.keys.leaver-holds-nothing", Run = KeyCases.LeaverHoldsNothing });
        }

        /// <summary>Adds the input package's cases.</summary>
        private static void AddInput(List<Case> list)
        {
            list.Add(new Case { Name = "input.mouse.moved", Run = MouseCases.Moved });
            list.Add(new Case { Name = "input.mouse.not-a-place", Run = MouseCases.NotAPlace });
            list.Add(new Case { Name = "input.mouse.corner-of-the-world", Run = MouseCases.CornerOfTheWorld });
            list.Add(new Case { Name = "input.mouse.first-not-a-place", Run = MouseCases.FirstNotAPlace });
            list.Add(new Case { Name = "input.mouse.buttons", Run = MouseCases.Buttons });
            list.Add(new Case { Name = "input.mouse.unknown-button", Run = MouseCases.UnknownButton });
            list.Add(new Case { Name = "input.mouse.order", Run = MouseCases.Order });
            list.Add(new Case { Name = "input.mouse.added-to-player-in-reaction", Run = MouseCases.AddedToPlayerInReaction });
            list.Add(new Case { Name = "input.mouse.only-written", Run = MouseCases.OnlyWritten });
            list.Add(new Case { Name = "input.mouse.throwing", Run = MouseCases.Throwing });
            list.Add(new Case { Name = "input.mouse.computer-and-left", Run = MouseCases.ComputerAndLeft });
            list.Add(new Case { Name = "input.mouse.no-mouse", Run = MouseCases.NoMouse });
            list.Add(new Case { Name = "input.mouse.per-slot", Run = MouseCases.PerSlot });
            list.Add(new Case { Name = "input.mouse.local", Run = MouseCases.Local });
            list.Add(new Case { Name = "input.mouse.local-unknown-focus", Run = MouseCases.LocalUnknownFocus });
            list.Add(new Case { Name = "input.mouse.setters", Run = MouseCases.Setters });
            list.Add(new Case { Name = "input.camera.reads", Run = CameraCases.Reads });
            list.Add(new Case { Name = "input.camera.changes", Run = CameraCases.Changes });
            list.Add(new Case { Name = "input.camera.named", Run = CameraCases.Named });
            list.Add(new Case { Name = "input.camera.unknown-camera", Run = CameraCases.UnknownCamera });
            list.Add(new Case { Name = "input.camera.wrong-data", Run = CameraCases.WrongData });
            list.Add(new Case { Name = "input.camera.not-installed", Run = CameraCases.NotInstalled });
        }

        /// <summary>Adds the terrain package's cases.</summary>
        private static void AddTerrain(List<Case> list)
        {
            list.Add(new Case { Name = "terrain.grid.pinned", Run = GridCases.Pinned });
            list.Add(new Case { Name = "terrain.grid.clamped", Run = GridCases.Clamped });
            list.Add(new Case { Name = "terrain.grid.wrong-length", Run = GridCases.WrongLength });
            list.Add(new Case { Name = "terrain.grid.not-installed", Run = GridCases.NotInstalled });
            list.Add(new Case { Name = "terrain.grid.playable", Run = GridCases.Playable });
            list.Add(new Case { Name = "terrain.grid.blocks", Run = GridCases.Blocks });
            list.Add(new Case { Name = "terrain.grid.many-blocks", Run = GridCases.ManyBlocks });
            list.Add(new Case { Name = "terrain.grid.alphabet", Run = GridCases.Alphabet });
            list.Add(new Case { Name = "terrain.grid.span", Run = GridCases.Span });
            list.Add(new Case { Name = "terrain.height.corners", Run = HeightCases.Corners });
            list.Add(new Case { Name = "terrain.height.blend", Run = HeightCases.Blend });
            list.Add(new Case { Name = "terrain.height.clamped", Run = HeightCases.Clamped });
            list.Add(new Case { Name = "terrain.height.step", Run = HeightCases.Step });
            list.Add(new Case { Name = "terrain.height.cliff-blend", Run = HeightCases.CliffBlend });
            list.Add(new Case { Name = "terrain.height.ramp", Run = HeightCases.Ramp });
            list.Add(new Case { Name = "terrain.height.normal-and-slope", Run = HeightCases.NormalAndSlope });
            list.Add(new Case { Name = "terrain.height.not-a-number", Run = HeightCases.NotANumber });
            list.Add(new Case { Name = "terrain.height.vector-point", Run = HeightCases.VectorPoint });
            list.Add(new Case { Name = "terrain.height.not-installed", Run = HeightCases.NotInstalled });
            list.Add(new Case { Name = "terrain.pathing.flags", Run = PathingCases.Flags });
            list.Add(new Case { Name = "terrain.pathing.filled-once", Run = PathingCases.FilledOnce });
            list.Add(new Case { Name = "terrain.pathing.partial-block", Run = PathingCases.PartialBlock });
            list.Add(new Case { Name = "terrain.pathing.refresh", Run = PathingCases.Refresh });
            list.Add(new Case { Name = "terrain.pathing.refresh-span", Run = PathingCases.RefreshSpan });
            list.Add(new Case { Name = "terrain.pathing.outside", Run = PathingCases.Outside });
            list.Add(new Case { Name = "terrain.pathing.not-a-number", Run = PathingCases.NotANumber });
            list.Add(new Case { Name = "terrain.pathing.can-stand", Run = PathingCases.CanStand });
            list.Add(new Case { Name = "terrain.pathing.can-stand-water", Run = PathingCases.CanStandWater });
            list.Add(new Case { Name = "terrain.pathing.local-height", Run = PathingCases.LocalHeight });
            list.Add(new Case { Name = "terrain.pathing.vector-point", Run = PathingCases.VectorPoint });
            list.Add(new Case { Name = "terrain.pathing.not-installed", Run = PathingCases.NotInstalled });
            list.Add(new Case { Name = "terrain.pathing.reinstall", Run = PathingCases.Reinstall });
            list.Add(new Case { Name = "terrain.raycast.hill-hit", Run = GroundRayCases.HillHit });
            list.Add(new Case { Name = "terrain.raycast.miss", Run = GroundRayCases.Miss });
            list.Add(new Case { Name = "terrain.raycast.cliff", Run = GroundRayCases.Cliff });
            list.Add(new Case { Name = "terrain.raycast.starts-under", Run = GroundRayCases.StartsUnder });
            list.Add(new Case { Name = "terrain.raycast.zero-length", Run = GroundRayCases.ZeroLength });
            list.Add(new Case { Name = "terrain.raycast.vertical", Run = GroundRayCases.Vertical });
            list.Add(new Case { Name = "terrain.raycast.leaves-map", Run = GroundRayCases.LeavesMap });
            list.Add(new Case { Name = "terrain.raycast.long-ray", Run = GroundRayCases.LongRay });
            list.Add(new Case { Name = "terrain.raycast.not-finite", Run = GroundRayCases.NotFinite });
            list.Add(new Case { Name = "terrain.raycast.hit-values", Run = GroundRayCases.HitValues });
            list.Add(new Case { Name = "terrain.raycast.not-installed", Run = GroundRayCases.NotInstalled });
            list.Add(new Case { Name = "terrain.raycast.miss-makes-nothing", Run = MissCases.GroundMissMakesNothing });
        }

        /// <summary>Adds the cases for the full audit's fixes to the maths and terrain packages.</summary>
        private static void AddAuditMaths(List<Case> list)
        {
            list.Add(new Case { Name = "maths.bounce.circle-exact", Run = BounceCases.CircleExact });
            list.Add(new Case { Name = "maths.bounce.bounds-exact", Run = BounceCases.BoundsExact });
            list.Add(new Case { Name = "maths.bounce.polygon-exact", Run = BounceCases.PolygonExact });
            list.Add(new Case { Name = "maths.bounce.wall-exact", Run = BounceCases.WallExact });
            list.Add(new Case { Name = "maths.bounce.circle-sweep", Run = BounceCases.CircleSweep });
            list.Add(new Case { Name = "maths.bounce.bounds-sweep", Run = BounceCases.BoundsSweep });
            list.Add(new Case { Name = "maths.bounce.polygon-sweep", Run = BounceCases.PolygonSweep });
            list.Add(new Case { Name = "maths.bounce.wall-sweep", Run = BounceCases.WallSweep });
            list.Add(new Case { Name = "maths.bounce.room", Run = BounceCases.Room });
            list.Add(new Case { Name = "maths.bounce.notch", Run = BounceCases.Notch });
            list.Add(new Case { Name = "maths.bounce.slide-bounds", Run = BounceCases.SlideBounds });
            list.Add(new Case { Name = "maths.bounce.slide-polygon", Run = BounceCases.SlidePolygon });
            list.Add(new Case { Name = "maths.bounce.into-from-surface", Run = BounceCases.IntoFromSurface });
            list.Add(new Case { Name = "maths.bounce.slide-under-gravity", Run = BounceCases.SlideUnderGravity });
            list.Add(new Case { Name = "maths.raycast.corners-found", Run = CornerRayCases.Found });
            list.Add(new Case { Name = "maths.raycast.through-corners", Run = CornerRayCases.ThroughCorners });
            list.Add(new Case { Name = "maths.shapes.polygon-far", Run = FarPolygonCases.AreaAndCenter });
            list.Add(new Case { Name = "terrain.raycast.past-the-edge", Run = GroundClipCases.PastTheEdge });
            list.Add(new Case { Name = "terrain.raycast.long-ray-clipped", Run = GroundClipCases.LongRayClipped });
            list.Add(new Case { Name = "terrain.pathing.every-cell", Run = PackedPathingCases.EveryCell });
            list.Add(new Case { Name = "terrain.pathing.full-grid-memory", Run = PackedPathingCases.FullGridMemory });
            list.Add(new Case { Name = "maths.actors.face-own-place", Run = FaceOwnPlaceCases.KeepsFacing });
            list.Add(new Case { Name = "terrain.bounce.flat", Run = GroundBounceCases.BounceFlat });
            list.Add(new Case { Name = "terrain.bounce.slope", Run = GroundBounceCases.BounceSlope });
            list.Add(new Case { Name = "terrain.bounce.roll-flat", Run = GroundBounceCases.RollFlat });
            list.Add(new Case { Name = "terrain.bounce.roll-slope", Run = GroundBounceCases.RollSlope });
            list.Add(new Case { Name = "terrain.bounce.into-from-ground", Run = GroundBounceCases.IntoFromGround });
            list.Add(new Case { Name = "terrain.bounce.roll-under-gravity", Run = GroundBounceCases.RollUnderGravity });
        }

        /// <summary>Adds the maths package's cases.</summary>
        private static void AddMaths(List<Case> list)
        {
            list.Add(new Case { Name = "maths.scalars.lerp", Run = ScalarCases.Lerp });
            list.Add(new Case { Name = "maths.scalars.inverse-lerp", Run = ScalarCases.InverseLerp });
            list.Add(new Case { Name = "maths.scalars.remap", Run = ScalarCases.Remap });
            list.Add(new Case { Name = "maths.scalars.clamp-and-wrap", Run = ScalarCases.ClampAndWrap });
            list.Add(new Case { Name = "maths.scalars.move-toward", Run = ScalarCases.MoveToward });
            list.Add(new Case { Name = "maths.scalars.sign-and-near", Run = ScalarCases.SignAndNear });
            list.Add(new Case { Name = "maths.scalars.near-grows", Run = ScalarCases.NearGrows });
            list.Add(new Case { Name = "maths.scalars.sqrt", Run = ScalarCases.Sqrt });
            list.Add(new Case { Name = "maths.scalars.distance-and-angle", Run = ScalarCases.DistanceAndAngle });
            list.Add(new Case { Name = "maths.scalars.polar", Run = ScalarCases.Polar });
            list.Add(new Case { Name = "maths.scalars.angles-normalize", Run = ScalarCases.AnglesNormalize });
            list.Add(new Case { Name = "maths.scalars.angles-difference", Run = ScalarCases.AnglesDifference });
            list.Add(new Case { Name = "maths.scalars.angles-lerp-and-move", Run = ScalarCases.AnglesLerpAndMove });
            list.Add(new Case { Name = "maths.scalars.angles-trig", Run = ScalarCases.AnglesTrig });
            list.Add(new Case { Name = "maths.vector2.basics", Run = Vector2Cases.Basics });
            list.Add(new Case { Name = "maths.vector2.angles", Run = Vector2Cases.Angles });
            list.Add(new Case { Name = "maths.vector2.direction-to", Run = Vector2Cases.DirectionTo });
            list.Add(new Case { Name = "maths.vector2.offset-and-rotate", Run = Vector2Cases.OffsetAndRotate });
            list.Add(new Case { Name = "maths.vector2.products", Run = Vector2Cases.Products });
            list.Add(new Case { Name = "maths.vector2.bounce-reflect-slide", Run = Vector2Cases.BounceReflectSlide });
            list.Add(new Case { Name = "maths.vector2.bounce-keeps-speed", Run = Vector2Cases.BounceKeepsSpeed });
            list.Add(new Case { Name = "maths.vector2.limits", Run = Vector2Cases.Limits });
            list.Add(new Case { Name = "maths.vector2.lerp", Run = Vector2Cases.Lerp });
            list.Add(new Case { Name = "maths.vector2.operators", Run = Vector2Cases.Operators });
            list.Add(new Case { Name = "maths.vector2.equality", Run = Vector2Cases.Equality });
            list.Add(new Case { Name = "maths.vector2.text", Run = Vector2Cases.Text });
            list.Add(new Case { Name = "maths.vector3.basics", Run = Vector3Cases.Basics });
            list.Add(new Case { Name = "maths.vector3.direction-to", Run = Vector3Cases.DirectionTo });
            list.Add(new Case { Name = "maths.vector3.yaw-pitch", Run = Vector3Cases.YawPitch });
            list.Add(new Case { Name = "maths.vector3.cross", Run = Vector3Cases.Cross });
            list.Add(new Case { Name = "maths.vector3.bounce", Run = Vector3Cases.Bounce });
            list.Add(new Case { Name = "maths.vector3.from-vector2", Run = Vector3Cases.FromVector2 });
            list.Add(new Case { Name = "maths.vector3.equality-and-text", Run = Vector3Cases.EqualityAndText });
            list.Add(new Case { Name = "maths.ease.ends", Run = Vector3Cases.EaseEnds });
            list.Add(new Case { Name = "maths.ease.middles", Run = Vector3Cases.EaseMiddles });
            list.Add(new Case { Name = "maths.ease.clamped-and-overshoot", Run = Vector3Cases.EaseClampedAndOvershoot });
            list.Add(new Case { Name = "maths.shapes.bounds-corners", Run = ShapeCases.BoundsCorners });
            list.Add(new Case { Name = "maths.shapes.bounds-contains", Run = ShapeCases.BoundsContains });
            list.Add(new Case { Name = "maths.shapes.bounds-intersects", Run = ShapeCases.BoundsIntersects });
            list.Add(new Case { Name = "maths.shapes.bounds-clamp-grow", Run = ShapeCases.BoundsClampGrow });
            list.Add(new Case { Name = "maths.shapes.polygon-contains", Run = ShapeCases.PolygonContains });
            list.Add(new Case { Name = "maths.shapes.polygon-area-center", Run = ShapeCases.PolygonAreaCenter });
            list.Add(new Case { Name = "maths.shapes.polygon-copies", Run = ShapeCases.PolygonCopies });
            list.Add(new Case { Name = "maths.shapes.polygon-too-few", Run = ShapeCases.PolygonTooFew });
            list.Add(new Case { Name = "maths.raycast.circle-hit", Run = RaycastCases.CircleHit });
            list.Add(new Case { Name = "maths.raycast.circle-miss", Run = RaycastCases.CircleMiss });
            list.Add(new Case { Name = "maths.raycast.circle-short", Run = RaycastCases.CircleShort });
            list.Add(new Case { Name = "maths.raycast.circle-inside", Run = RaycastCases.CircleInside });
            list.Add(new Case { Name = "maths.raycast.circle-zero-length", Run = RaycastCases.CircleZeroLength });
            list.Add(new Case { Name = "maths.raycast.segment-hit", Run = RaycastCases.SegmentHit });
            list.Add(new Case { Name = "maths.raycast.segment-parallel", Run = RaycastCases.SegmentParallel });
            list.Add(new Case { Name = "maths.raycast.bounds-hit", Run = RaycastCases.BoundsHit });
            list.Add(new Case { Name = "maths.raycast.polygon-hit", Run = RaycastCases.PolygonHit });
            list.Add(new Case { Name = "maths.raycast.polygon-first-edge", Run = RaycastCases.PolygonFirstEdge });
            list.Add(new Case { Name = "maths.raycast.ray-at", Run = RaycastCases.RayAt });
            list.Add(new Case { Name = "maths.actors.position", Run = ActorMathsCases.Position });
            list.Add(new Case { Name = "maths.actors.place-and-move", Run = ActorMathsCases.PlaceAndMove });
            list.Add(new Case { Name = "maths.actors.distance-and-angle", Run = ActorMathsCases.DistanceAndAngle });
            list.Add(new Case { Name = "maths.actors.face", Run = ActorMathsCases.Face });
            list.Add(new Case { Name = "maths.actors.effect-position", Run = ActorMathsCases.EffectPosition });
            list.Add(new Case { Name = "maths.actors.destructable", Run = ActorMathsCases.DestructableDistances });
            list.Add(new Case { Name = "maths.polygon.contains-floats", Run = ShapeCases.PolygonContainsFloats });
            list.Add(new Case { Name = "maths.lua.shared-not-copied", Run = LuaShapeCases.SharedNotCopied });
            list.Add(new Case { Name = "maths.lua.out-pair", Run = LuaShapeCases.OutPair });
            list.Add(new Case { Name = "maths.lua.number-types", Run = LuaShapeCases.NumberTypes });
            list.Add(new Case { Name = "maths.lua.miss-makes-nothing", Run = MissCases.MissMakesNothing });
            list.Add(new Case { Name = "maths.raycast.ray-values", Run = MissCases.RayValues });
        }

        /// <summary>Adds the first cases: the harness, collections, errors, ownership, timers, hooks, actors and objects (Lua allows 200 locals in a function, and each case made here is one).</summary>
        private static void AddFirst(List<Case> list)
        {
            list.Add(new Case { Name = "harness.handles-are-keys", Run = HarnessCases.HandlesAreKeys });
            list.Add(new Case { Name = "harness.expect-fails", Run = HarnessCases.ExpectFails });
            list.Add(new Case { Name = "fake.removed-destructable-dies", Run = FakeGameCases.RemovedDestructableDies });
            list.Add(new Case { Name = "fake.damage-lowers-life", Run = FakeGameCases.DamageLowersLife });
            list.Add(new Case { Name = "fake.damage-kills", Run = FakeGameCases.DamageKills });
            list.Add(new Case { Name = "fake.damage-to-the-dead", Run = FakeGameCases.DamageToTheDead });
            list.Add(new Case { Name = "fake.removed-unit-reads-nothing", Run = FakeGameCases.RemovedUnitReadsNothing });
            list.Add(new Case { Name = "fake.revive-gives-full-life", Run = FakeGameCases.ReviveGivesFullLife });
            list.Add(new Case { Name = "fake.given-item-leaves-its-holder", Run = FakeGameCases.GivenItemLeavesItsHolder });
            list.Add(new Case { Name = "collections.set-keeps-order", Run = CollectionCases.SetKeepsOrder });
            list.Add(new Case { Name = "collections.set-remove-keeps-order", Run = CollectionCases.SetRemoveKeepsOrder });
            list.Add(new Case { Name = "collections.set-snapshot", Run = CollectionCases.SetSnapshot });
            list.Add(new Case { Name = "collections.map-keeps-order", Run = CollectionCases.MapKeepsOrder });
            list.Add(new Case { Name = "collections.map-remove", Run = CollectionCases.MapRemove });
            list.Add(new Case { Name = "errors.log-line", Run = ErrorCases.LogLine });
            list.Add(new Case { Name = "errors.screen-line", Run = ErrorCases.ScreenLine });
            list.Add(new Case { Name = "errors.log-line-where", Run = ErrorCases.LogLineWhere });
            list.Add(new Case { Name = "errors.screen-line-where", Run = ErrorCases.ScreenLineWhere });
            list.Add(new Case { Name = "errors.shows-at", Run = ErrorCases.ShowsAt });
            list.Add(new Case { Name = "errors.report-throttles", Run = ErrorCases.ReportThrottles });
            list.Add(new Case { Name = "errors.report-many-keys", Run = ErrorCases.ReportManyKeys });
            list.Add(new Case { Name = "names.of", Run = ErrorCases.NamesOf });
            list.Add(new Case { Name = "names.class-of-type", Run = ErrorCases.ClassOfType });
            list.Add(new Case { Name = "world.advance", Run = ErrorCases.WorldAdvance });
            list.Add(new Case { Name = "ownership.ends-newest-first", Run = OwnershipCases.EndsNewestFirst });
            list.Add(new Case { Name = "ownership.ends-grandchildren", Run = OwnershipCases.EndsGrandchildren });
            list.Add(new Case { Name = "ownership.end-once", Run = OwnershipCases.EndOnce });
            list.Add(new Case { Name = "ownership.end-inside-end", Run = OwnershipCases.EndInsideEnd });
            list.Add(new Case { Name = "ownership.adopt-into-ended", Run = OwnershipCases.AdoptIntoEnded });
            list.Add(new Case { Name = "ownership.child-ends-alone", Run = OwnershipCases.ChildEndsAlone });
            list.Add(new Case { Name = "ownership.adopt-while-ending", Run = OwnershipCases.AdoptWhileEnding });
            list.Add(new Case { Name = "ownership.error-does-not-stop", Run = OwnershipCases.ErrorDoesNotStop });
            list.Add(new Case { Name = "ownership.adopt-in-finish", Run = OwnershipCases.AdoptInFinish });
            list.Add(new Case { Name = "timers.after", Run = TimerCases.After });
            list.Add(new Case { Name = "timers.every", Run = TimerCases.Every });
            list.Add(new Case { Name = "timers.no-drift", Run = TimerCases.NoDrift });
            list.Add(new Case { Name = "timers.no-drift-woken-late", Run = TimerCases.NoDriftWokenLate });
            list.Add(new Case { Name = "timers.same-time-in-creation-order", Run = TimerCases.SameTimeInCreationOrder });
            list.Add(new Case { Name = "timers.many-in-due-order", Run = TimerCases.ManyInDueOrder });
            list.Add(new Case { Name = "timers.end-cancels", Run = TimerCases.EndCancels });
            list.Add(new Case { Name = "timers.owned", Run = TimerCases.OwnerAdopts });
            list.Add(new Case { Name = "timers.error-isolated", Run = TimerCases.ErrorIsolated });
            list.Add(new Case { Name = "timers.remaining", Run = TimerCases.Remaining });
            list.Add(new Case { Name = "hooks.overrides", Run = HookCases.Overrides });
            list.Add(new Case { Name = "hooks.cached", Run = HookCases.Cached });
            list.Add(new Case { Name = "hooks.damage-listener", Run = HookCases.DamageListener });
            list.Add(new Case { Name = "hooks.seen-once-per-class", Run = HookCases.SeenOncePerClass });
            list.Add(new Case { Name = "hooks.reset-clears", Run = HookCases.ResetClears });
            list.Add(new Case { Name = "hooks.taken-in-and-ending", Run = HookCases.TakenInAndEnding });
            list.Add(new Case { Name = "hooks.reset", Run = HookCases.WidgetHooksReset });
            list.Add(new Case { Name = "hooks.ending-two", Run = HookCases.EndingTwo });
            list.Add(new Case { Name = "hooks.ending-throwing", Run = HookCases.EndingThrowing });
            list.Add(new Case { Name = "hooks.ending-none", Run = HookCases.EndingNone });
            list.Add(new Case { Name = "hooks.ending-reset", Run = HookCases.EndingReset });
            list.Add(new Case { Name = "events.need-listens-once", Run = HookCases.NeedListensOnce });
            list.Add(new Case { Name = "actors.add-and-get", Run = ActorCases.AddAndGet });
            list.Add(new Case { Name = "actors.remove", Run = ActorCases.RemoveOne });
            list.Add(new Case { Name = "actors.remove-all", Run = ActorCases.RemoveAllOf });
            list.Add(new Case { Name = "actors.remove-of-class", Run = ActorCases.RemoveOf });
            list.Add(new Case { Name = "actors.end-removes-last-first", Run = ActorCases.EndRemovesLastFirst });
            list.Add(new Case { Name = "actors.add-to-ended", Run = ActorCases.AddToEnded });
            list.Add(new Case { Name = "actors.component-owns-timer", Run = ActorCases.ComponentOwnsTimer });
            list.Add(new Case { Name = "actors.error-in-onadded", Run = ActorCases.ErrorInOnAdded });
            list.Add(new Case { Name = "game.start-order", Run = ActorCases.GameStartOrder });
            list.Add(new Case { Name = "game.dispatch-removal", Run = ActorCases.GameDispatchRemoval });
            list.Add(new Case { Name = "players.get", Run = ActorCases.PlayersGet });
            list.Add(new Case { Name = "players.named", Run = ActorCases.PlayersNamed });
            list.Add(new Case { Name = "players.neutral", Run = ActorCases.PlayersNeutral });
            list.Add(new Case { Name = "players.start", Run = ActorCases.PlayersStart });
            list.Add(new Case { Name = "players.existing-out-of-range", Run = ActorCases.ExistingOutOfRange });
            list.Add(new Case { Name = "players.print", Run = PlayerCases.PlayersPrint });
            list.Add(new Case { Name = "players.print-seconds", Run = PlayerCases.PlayersPrintSeconds });
            list.Add(new Case { Name = "game.print", Run = PlayerCases.GamePrint });
        }

        /// <summary>Adds the cases from the full audit's timer findings; the one that needs Moonwell's clock exists only in a development build, and is named debug.* (see AddDebug).</summary>
        private static void AddAuditTimers(List<Case> list)
        {
            list.Add(new Case { Name = "timers.every-late-no-drift", Run = AuditTimerCases.EveryLateNoDrift });
            list.Add(new Case { Name = "timers.every-tiny-late", Run = AuditTimerCases.EveryTinyLate });
            list.Add(new Case { Name = "timers.every-milli-very-late", Run = AuditTimerCases.EveryMilliVeryLate });
            list.Add(new Case { Name = "timers.buff-ticks-late", Run = AuditTimerCases.BuffTicksLate });
            list.Add(new Case { Name = "timers.near-times-order", Run = AuditTimerCases.NearTimesOrder });
            list.Add(new Case { Name = "timers.near-times-many", Run = AuditTimerCases.NearTimesMany });
            list.Add(new Case { Name = "timers.after-zero-same-instant", Run = AuditTimerCases.AfterZeroSameInstant });
            list.Add(new Case { Name = "timers.after-zero-runaway", Run = AuditTimerCases.AfterZeroRunaway });
            list.Add(new Case { Name = "timers.runaway-many-timers", Run = AuditTimerCases.RunawayManyTimers });
            list.Add(new Case { Name = "timers.catch-up-not-runaway", Run = AuditTimerCases.CatchUpNotRunaway });
            list.Add(new Case { Name = "timers.every-zero-refused", Run = AuditTimerCases.EveryZeroRefused });
            list.Add(new Case { Name = "timers.remaining-in-every", Run = AuditTimerCases.RemainingInEvery });
#if DEBUG
            list.Add(new Case { Name = "debug.timers.no-drift-at-speed", Run = AuditTimerCases.NoDriftAtSpeed });
#endif
        }

        /// <summary>Adds the player, local, unit and inventory cases (Lua allows 200 locals in a function, and each case made here is one).</summary>
        private static void AddMiddle(List<Case> list)
        {
            list.Add(new Case { Name = "players.gold", Run = PlayerCases.PlayersGold });
            list.Add(new Case { Name = "players.states", Run = PlayerCases.PlayersStates });
            list.Add(new Case { Name = "players.playing", Run = PlayerCases.PlayersPlaying });
            list.Add(new Case { Name = "players.slot-state", Run = PlayerCases.PlayersSlotState });
            list.Add(new Case { Name = "players.leave-order", Run = PlayerCases.PlayersLeaveOrder });
            list.Add(new Case { Name = "players.leave-listened-when-reacted", Run = PlayerCases.PlayersLeaveListenedWhenReacted });
            list.Add(new Case { Name = "players.name", Run = PlayerCases.PlayersName });
            list.Add(new Case { Name = "players.color", Run = PlayerCases.PlayersColor });
            list.Add(new Case { Name = "players.by-color", Run = PlayerCases.PlayersByColor });
            list.Add(new Case { Name = "players.by-color-in-game-only", Run = PlayerCases.PlayersByColorInGameOnly });
            list.Add(new Case { Name = "players.by-color-shared", Run = PlayerCases.PlayersByColorShared });
            list.Add(new Case { Name = "players.named-colors", Run = PlayerCases.PlayersNamedColors });
            list.Add(new Case { Name = "players.named-color-nobody", Run = PlayerCases.PlayersNamedColorNobody });
            list.Add(new Case { Name = "players.team", Run = PlayerCases.PlayersTeam });
            list.Add(new Case { Name = "players.race", Run = PlayerCases.PlayersRace });
            list.Add(new Case { Name = "players.alliance", Run = PlayerCases.PlayersAlliance });
            list.Add(new Case { Name = "players.allied", Run = PlayerCases.PlayersAllied });
            list.Add(new Case { Name = "players.make-allies", Run = PlayerCases.PlayersMakeAllies });
            list.Add(new Case { Name = "players.make-allies-shared-vision", Run = PlayerCases.PlayersMakeAlliesSharedVision });
            list.Add(new Case { Name = "players.make-enemies", Run = PlayerCases.PlayersMakeEnemies });
            list.Add(new Case { Name = "players.alliance-with-self", Run = PlayerCases.PlayersAllianceWithSelf });
            list.Add(new Case { Name = "players.show-vision", Run = PlayerCases.PlayersShowVision });
            list.Add(new Case { Name = "players.tech", Run = PlayerCases.PlayersTech });
            list.Add(new Case { Name = "players.no-limit", Run = PlayerCases.PlayersNoLimit });
            list.Add(new Case { Name = "players.ability-available", Run = PlayerCases.PlayersAbilityAvailable });
            list.Add(new Case { Name = "local.runs-for-local-player", Run = LocalCases.RunsForLocalPlayer });
            list.Add(new Case { Name = "local.skips-other-players", Run = LocalCases.SkipsOtherPlayers });
            list.Add(new Case { Name = "local.reads-keep-no-last-value", Run = LocalCases.ReadsKeepNoLastValue });
            list.Add(new Case { Name = "local.throw-leaves-reads-kept", Run = LocalCases.ThrowLeavesReadsKept });
            list.Add(new Case { Name = "local.marked-field-holds-its-value", Run = LocalCases.MarkedFieldHoldsItsValue });
            list.Add(new Case { Name = "map.components-in-order", Run = MapCases.ComponentsInOrder });
            list.Add(new Case { Name = "map.components-not-added-early", Run = MapCases.ComponentsNotAddedEarly });
            list.Add(new Case { Name = "map.components-assigned", Run = MapCases.ComponentsAssigned });
            list.Add(new Case { Name = "map.components-add-many", Run = MapCases.ComponentsAddMany });
            list.Add(new Case { Name = "map.begin-takes-in-placed", Run = MapCases.BeginTakesInPlaced });
            list.Add(new Case { Name = "map.player-components-per-player", Run = MapCases.PlayerComponentsPerPlayer });
            list.Add(new Case { Name = "map.player-components-in-order", Run = MapCases.PlayerComponentsInOrder });
            list.Add(new Case { Name = "map.player-components-assigned", Run = MapCases.PlayerComponentsAssigned });
            list.Add(new Case { Name = "map.player-components-not-made-early", Run = MapCases.PlayerComponentsNotMadeEarly });
            list.Add(new Case { Name = "map.player-components-hear-placed-units", Run = MapCases.PlayerComponentsHearPlacedUnits });
            list.Add(new Case { Name = "map.player-component-maker-fails", Run = MapCases.PlayerComponentMakerFails });
            list.Add(new Case { Name = "map.player-components-none-listed", Run = MapCases.PlayerComponentsNoneListed });
            list.Add(new Case { Name = "units.subclass-assigning-drops-parent", Run = MapCases.SubclassAssigningDropsParent });
            list.Add(new Case { Name = "units.subclass-add-many-keeps-parent", Run = MapCases.SubclassAddManyKeepsParent });
            list.Add(new Case { Name = "units.create-default-facing", Run = MapCases.CreateDefaultFacing });
            list.Add(new Case { Name = "map.settings-default-empty", Run = MapCases.SettingsDefaultEmpty });
            list.Add(new Case { Name = "map.balance-default", Run = MapCases.BalanceDefault });
            list.Add(new Case { Name = "types.fourcc", Run = UnitCases.FourCC });
            list.Add(new Case { Name = "types.duplicate", Run = UnitCases.Duplicate });
            list.Add(new Case { Name = "types.idof", Run = UnitCases.IdOf });
            list.Add(new Case { Name = "objects.upgrade-level", Run = ObjectKindCases.UpgradeLevel });
            list.Add(new Case { Name = "objects.id-twice", Run = ObjectKindCases.IdTwice });
            list.Add(new Case { Name = "objects.register-before-components", Run = ObjectKindCases.RegisterBeforeComponents });
            list.Add(new Case { Name = "units.registered-class", Run = UnitCases.RegisteredClass });
            list.Add(new Case { Name = "units.fallback-class", Run = UnitCases.FallbackClass });
            list.Add(new Case { Name = "units.components-before-enter", Run = UnitCases.ComponentsBeforeEnter });
            list.Add(new Case { Name = "units.sweep-detects-removal", Run = UnitCases.SweepDetectsRemoval });
            list.Add(new Case { Name = "units.remove", Run = UnitCases.Remove });
            list.Add(new Case { Name = "units.owned", Run = UnitCases.OwnedByOwner });
            list.Add(new Case { Name = "units.not-owned-by-current", Run = UnitCases.NotOwnedByCurrent });
            list.Add(new Case { Name = "units.create-generic", Run = UnitCases.CreateGeneric });
            list.Add(new Case { Name = "units.create-place", Run = UnitCases.CreatePlace });
            list.Add(new Case { Name = "units.sweep-continues-after-error", Run = UnitCases.SweepContinuesAfterError });
            list.Add(new Case { Name = "units.add-in-onleave", Run = UnitCases.AddInOnLeave });
            list.Add(new Case { Name = "units.enter", Run = UnitCases.Enter });
            list.Add(new Case { Name = "units.player-after-removal", Run = UnitCases.PlayerAfterRemoval });
            list.Add(new Case { Name = "units.player-set", Run = UnitCases.PlayerSet });
            list.Add(new Case { Name = "units.dictionary-key", Run = UnitCases.DictionaryKey });
            list.Add(new Case { Name = "units.life-and-mana", Run = UnitCases.LifeAndMana });
            list.Add(new Case { Name = "units.position", Run = UnitCases.Position });
            list.Add(new Case { Name = "units.move-to", Run = UnitCases.MoveTo });
            list.Add(new Case { Name = "units.facing", Run = UnitCases.Facing });
            list.Add(new Case { Name = "units.level", Run = UnitCases.Level });
            list.Add(new Case { Name = "units.kill", Run = UnitCases.Kill });
            list.Add(new Case { Name = "units.kill-gone", Run = UnitCases.KillGone });
            list.Add(new Case { Name = "units.kill-twice", Run = UnitCases.KillTwice });
            list.Add(new Case { Name = "units.kill-leaves-no-life", Run = UnitCases.KillLeavesNoLife });
            list.Add(new Case { Name = "units.life-below-threshold", Run = UnitCases.LifeBelowThreshold });
            list.Add(new Case { Name = "units.pause", Run = UnitCases.Pause });
            list.Add(new Case { Name = "units.scale", Run = UnitCases.Scale });
            list.Add(new Case { Name = "units.tint", Run = UnitCases.Tint });
            list.Add(new Case { Name = "units.fly-height", Run = UnitCases.FlyHeight });
            list.Add(new Case { Name = "units.gone-reads-last", Run = UnitCases.GoneReadsLast });
            list.Add(new Case { Name = "units.fly-height-ground", Run = UnitCases.FlyHeightGroundUnit });
            list.Add(new Case { Name = "units.max-clamps", Run = UnitCases.MaxClampsLifeAndMana });
            list.Add(new Case { Name = "units.max-last-values", Run = UnitCases.MaxLastValues });
            list.Add(new Case { Name = "units.fly-height-has-amrf", Run = UnitCases.FlyHeightWithAmrf });
            list.Add(new Case { Name = "units.paused-gone-before-sweep", Run = UnitCases.PausedGoneBeforeSweep });
            list.Add(new Case { Name = "units.gone-before-sweep", Run = UnitCases.GoneBeforeSweep });
            list.Add(new Case { Name = "units.gone-before-sweep-local", Run = UnitCases.GoneBeforeSweepInLocal });
            list.Add(new Case { Name = "units.order-immediate", Run = UnitCases.OrderImmediate });
            list.Add(new Case { Name = "units.order-point", Run = UnitCases.OrderPoint });
            list.Add(new Case { Name = "units.order-target", Run = UnitCases.OrderTarget });
            list.Add(new Case { Name = "units.order-gone", Run = UnitCases.OrderGone });
            list.Add(new Case { Name = "units.name", Run = UnitCases.NameReadAndSet });
            list.Add(new Case { Name = "units.level-set-with-handle", Run = UnitCases.LevelSetWithHandleDoesNothing });
            list.Add(new Case { Name = "units.shared-without-handle", Run = UnitCases.SharedWithoutHandle });
            list.Add(new Case { Name = "defaults.reads-type-values", Run = UnitCases.DefaultReadsTypeValues });
            list.Add(new Case { Name = "defaults.once-per-type", Run = UnitCases.DefaultOncePerType });
            list.Add(new Case { Name = "defaults.removed-unit", Run = UnitCases.DefaultOfRemovedUnit });
            list.Add(new Case { Name = "defaults.live-actions-do-nothing", Run = UnitCases.DefaultLiveActionsDoNothing });
            list.Add(new Case { Name = "defaults.remove-does-nothing", Run = UnitCases.DefaultRemoveDoesNothing });
            list.Add(new Case { Name = "defaults.read-in-its-constructor", Run = UnitCases.DefaultReadInItsConstructor });
            list.Add(new Case { Name = "defaults.class-registered-later", Run = UnitCases.DefaultOfClassRegisteredLater });
            list.Add(new Case { Name = "defaults.class-without-its-maker-yet", Run = UnitCases.DefaultOfClassWithoutItsMakerYet });
            list.Add(new Case { Name = "defaults.unregistered-from-object", Run = UnitCases.DefaultOfUnregisteredFromObject });
            list.Add(new Case { Name = "defaults.unknown-type-name-empty", Run = UnitCases.DefaultNameOfUnknownTypeIsEmpty });
            list.Add(new Case { Name = "units.inventory", Run = ItemCases.Inventory });
            list.Add(new Case { Name = "units.inventory-limits", Run = ItemCases.InventoryLimits });
            list.Add(new Case { Name = "units.inventory-gone", Run = ItemCases.InventoryGone });
        }

        /// <summary>Adds the frames package's cases.</summary>
        private static void AddFrames(List<Case> list)
        {
            list.Add(new Case { Name = "frames.install", Run = FrameCases.Install });
            list.Add(new Case { Name = "frames.add-makes", Run = FrameCases.AddMakes });
            list.Add(new Case { Name = "frames.child-in-parent", Run = FrameCases.ChildInParent });
            list.Add(new Case { Name = "frames.tree-before-add", Run = FrameCases.TreeBeforeAdd });
            list.Add(new Case { Name = "frames.ended-parent", Run = FrameCases.EndedParent });
            list.Add(new Case { Name = "frames.shown-to", Run = FrameCases.ShownTo });
            list.Add(new Case { Name = "frames.child-shown-to", Run = FrameCases.ChildShownTo });
            list.Add(new Case { Name = "frames.anchor-default", Run = FrameCases.AnchorDefault });
            list.Add(new Case { Name = "frames.anchor-offset", Run = FrameCases.AnchorOffset });
            list.Add(new Case { Name = "frames.attach", Run = FrameCases.Attach });
            list.Add(new Case { Name = "frames.attach-later-sibling", Run = FrameCases.AttachLaterSibling });
            list.Add(new Case { Name = "frames.fill", Run = FrameCases.Fill });
            list.Add(new Case { Name = "frames.size-scale", Run = FrameCases.SizeScale });
            list.Add(new Case { Name = "frames.visible-enabled-alpha-level", Run = FrameCases.VisibleEnabledAlphaLevel });
            list.Add(new Case { Name = "frames.set-before-add", Run = FrameCases.SetBeforeAdd });
            list.Add(new Case { Name = "frames.after-end", Run = FrameCases.AfterEnd });
            list.Add(new Case { Name = "frames.label", Run = FrameCases.Label });
            list.Add(new Case { Name = "frames.label-lets-clicks-through", Run = FrameCases.LabelLetsClicksThrough });
            list.Add(new Case { Name = "frames.show-local", Run = FrameCases.ShowLocal });
            list.Add(new Case { Name = "frames.image", Run = FrameCases.Image });
            list.Add(new Case { Name = "frames.panel-look", Run = FrameCases.PanelLook });
            list.Add(new Case { Name = "frames.panel-empty", Run = FrameCases.PanelEmpty });
            list.Add(new Case { Name = "frames.install-loads-toc", Run = FrameCases.InstallLoadsToc });
            list.Add(new Case { Name = "frames.row", Run = FrameCases.Row });
            list.Add(new Case { Name = "frames.column", Run = FrameCases.Column });
            list.Add(new Case { Name = "frames.grid", Run = FrameCases.Grid });
            list.Add(new Case { Name = "frames.row-closes-up", Run = FrameCases.RowClosesUp });
            list.Add(new Case { Name = "frames.wheel-notches", Run = FrameCases.WheelNotches });
            list.Add(new Case { Name = "frames.event-for-ended-frame", Run = FrameCases.EventForEndedFrame });
            list.Add(new Case { Name = "frames.event-for-reused-frame", Run = FrameCases.EventForReusedFrame });
            list.Add(new Case { Name = "frames.event-from-another-player", Run = FrameCases.EventFromAnotherPlayer });
            list.Add(new Case { Name = "frames.handler-throws", Run = FrameCases.HandlerThrows });
            list.Add(new Case { Name = "frames.listening-label-takes-mouse", Run = FrameCases.ListeningLabelTakesMouse });
            list.Add(new Case { Name = "frames.listening-image-gets-catcher", Run = FrameCases.ListeningImageGetsCatcher });
            list.Add(new Case { Name = "frames.button", Run = FrameCases.Button });
            list.Add(new Case { Name = "frames.double-click", Run = FrameCases.DoubleClick });
            list.Add(new Case { Name = "frames.click-gives-keyboard-back", Run = FrameCases.ClickGivesKeyboardBack });
            list.Add(new Case { Name = "frames.icon-button", Run = FrameCases.IconButton });
            list.Add(new Case { Name = "frames.check-box", Run = FrameCases.CheckBox });
            list.Add(new Case { Name = "frames.slider", Run = FrameCases.Slider });
            list.Add(new Case { Name = "frames.text-box", Run = FrameCases.TextBox });
            list.Add(new Case { Name = "frames.value-control-for-everyone-reported", Run = FrameCases.ValueControlForEveryoneReported });
            list.Add(new Case { Name = "frames.set-value-runs-nothing", Run = FrameCases.SetValueRunsNothing });
            list.Add(new Case { Name = "frames.slider-fast", Run = FrameCases.SliderFast });
            list.Add(new Case { Name = "frames.text-box-keeps-keyboard", Run = FrameCases.TextBoxKeepsKeyboard });
            list.Add(new Case { Name = "frames.text-area", Run = FrameCases.TextArea });
            list.Add(new Case { Name = "frames.progress-bar", Run = FrameCases.ProgressBar });
            list.Add(new Case { Name = "frames.label-colour", Run = FrameCases.LabelColour });
            list.Add(new Case { Name = "frames.tooltip", Run = FrameCases.Tooltip });
            list.Add(new Case { Name = "frames.tooltip-on-image", Run = FrameCases.TooltipOnImage });
            list.Add(new Case { Name = "frames.tooltip-for-one-player", Run = FrameCases.TooltipForOnePlayer });
            list.Add(new Case { Name = "frames.tooltip-ends-with-its-frame", Run = FrameCases.TooltipEndsWithItsFrame });
            list.Add(new Case { Name = "frames.tooltip-cleared", Run = FrameCases.TooltipCleared });
            list.Add(new Case { Name = "frames.tooltip-over-layer", Run = FrameCases.TooltipOverLayer });
            list.Add(new Case { Name = "frames.tooltip-ended-by-map", Run = FrameCases.TooltipEndedByMap });
            list.Add(new Case { Name = "frames.tooltip-never-given-twice", Run = FrameCases.TooltipNeverGivenTwice });
            list.Add(new Case { Name = "frames.tooltip-left-by-ended-frame", Run = FrameCases.TooltipLeftByEndedFrame });
            list.Add(new Case { Name = "frames.taken-away-tooltips-share-nothing", Run = FrameCases.TakenAwayTooltipsShareNothing });
            list.Add(new Case { Name = "frames.pool-keeps-parents", Run = FrameCases.PoolKeepsParents });
            list.Add(new Case { Name = "frames.tooltip-given-twice-refused", Run = FrameCases.TooltipGivenTwiceRefused });
            list.Add(new Case { Name = "frames.frame-added-twice-refused", Run = FrameCases.FrameAddedTwiceRefused });
            list.Add(new Case { Name = "frames.kept-keyboard-gives-nothing-back", Run = FrameCases.KeptKeyboardGivesNothingBack });
            list.Add(new Case { Name = "frames.late-listening-keeps-catcher-beneath", Run = FrameCases.LateListeningKeepsCatcherBeneath });
            list.Add(new Case { Name = "frames.slider-range-keeps-value", Run = FrameCases.SliderRangeKeepsValue });
            list.Add(new Case { Name = "frames.click-on-disabled-gives-nothing-back", Run = FrameCases.ClickOnDisabledGivesNothingBack });
            list.Add(new Case { Name = "frames.end-hides", Run = FrameCases.EndHides });
            list.Add(new Case { Name = "frames.pool-reuse", Run = FrameCases.PoolReuse });
            list.Add(new Case { Name = "frames.pool-fits", Run = FrameCases.PoolFits });
            list.Add(new Case { Name = "frames.made-keeps-no-children", Run = FrameCases.MadeKeepsNoChildren });
            list.Add(new Case { Name = "frames.unknown-template", Run = FrameCases.UnknownTemplate });
            list.Add(new Case { Name = "frames.screen-edges", Run = FrameCases.ScreenEdges });
            list.Add(new Case { Name = "frames.grid-set-after-add", Run = FrameCases.GridSetAfterAdd });
            list.Add(new Case { Name = "frames.progress-bar-filled-over", Run = FrameCases.ProgressBarFilledOver });
            list.Add(new Case { Name = "frames.slider-echo-after-drag", Run = FrameCases.SliderEchoAfterDrag });
            list.Add(new Case { Name = "frames.text-box-echo-after-typing", Run = FrameCases.TextBoxEchoAfterTyping });
            list.Add(new Case { Name = "frames.attach-to-unmade", Run = FrameCases.AttachToUnmade });
            list.Add(new Case { Name = "frames.fill-unmade", Run = FrameCases.FillUnmade });
            list.Add(new Case { Name = "frames.tied-to-ended", Run = FrameCases.TiedToEnded });
            list.Add(new Case { Name = "frames.tied-still-pooled", Run = FrameCases.TiedStillPooled });
            list.Add(new Case { Name = "hud.looked-up-at-install", Run = HudCases.LookedUpAtInstall });
            list.Add(new Case { Name = "hud.piece-for-one-player", Run = HudCases.PieceForOnePlayer });
            list.Add(new Case { Name = "hud.command-button-by-scale", Run = HudCases.CommandButtonByScale });
            list.Add(new Case { Name = "hud.whole-hud", Run = HudCases.WholeHud });
            list.Add(new Case { Name = "hud.move-piece", Run = HudCases.MovePiece });
            list.Add(new Case { Name = "hud.frame-to-piece", Run = HudCases.FrameToPiece });
            list.Add(new Case { Name = "fdf.child-found", Run = FdfCases.ChildFound });
            list.Add(new Case { Name = "fdf.found-not-pooled", Run = FdfCases.FoundNotPooled });
            list.Add(new Case { Name = "fdf.child-refused", Run = FdfCases.ChildRefused });
        }

        /// <summary>Adds the special effect cases.</summary>
        private static void AddEffects(List<Case> list)
        {
            list.Add(new Case { Name = "effects.create", Run = EffectCases.Create });
            list.Add(new Case { Name = "effects.end-plays-death", Run = EffectCases.EndPlaysDeath });
            list.Add(new Case { Name = "effects.end-at-once", Run = EffectCases.EndAtOnce });
            list.Add(new Case { Name = "effects.scope-end", Run = EffectCases.ScopeEnd });
            list.Add(new Case { Name = "effects.ended-scope", Run = EffectCases.EndedScope });
            list.Add(new Case { Name = "effects.place", Run = EffectCases.Place });
            list.Add(new Case { Name = "effects.height-follows-ground", Run = EffectCases.HeightFollowsGround });
            list.Add(new Case { Name = "effects.place-at", Run = EffectCases.PlaceAt });
            list.Add(new Case { Name = "effects.after-end", Run = EffectCases.AfterEnd });
            list.Add(new Case { Name = "effects.facing-wraps", Run = EffectCases.FacingWraps });
            list.Add(new Case { Name = "effects.on-unit-ended-scope", Run = EffectCases.OnUnitEndedScope });
            list.Add(new Case { Name = "effects.facing", Run = EffectCases.Facing });
            list.Add(new Case { Name = "effects.scale", Run = EffectCases.Scale });
            list.Add(new Case { Name = "effects.animations", Run = EffectCases.Animations });
            list.Add(new Case { Name = "effects.animation-speed", Run = EffectCases.AnimationSpeed });
            list.Add(new Case { Name = "effects.tint", Run = EffectCases.Tint });
            list.Add(new Case { Name = "effects.on-unit", Run = EffectCases.OnUnit });
            list.Add(new Case { Name = "effects.at-point-has-no-unit", Run = EffectCases.AtPointHasNoUnit });
            list.Add(new Case { Name = "effects.ends-with-unit", Run = EffectCases.EndsWithUnit });
            list.Add(new Case { Name = "effects.unit-swept", Run = EffectCases.UnitSwept });
            list.Add(new Case { Name = "effects.unit-is-scope", Run = EffectCases.UnitIsScope });
            list.Add(new Case { Name = "effects.on-gone-unit", Run = EffectCases.OnGoneUnit });
            list.Add(new Case { Name = "effects.play-once", Run = EffectCases.PlayOnce });
            list.Add(new Case { Name = "effects.play-once-on-unit", Run = EffectCases.PlayOnceOnUnit });
            list.Add(new Case { Name = "effects.shown-to", Run = EffectCases.ShownTo });
            list.Add(new Case { Name = "effects.shown-to-other", Run = EffectCases.ShownToOther });
        }

        /// <summary>Adds the cases for giving and taking abilities and setting their level.</summary>
        private static void AddUnitAbilities(List<Case> list)
        {
            list.Add(new Case { Name = "abilities.add-gives-level-one", Run = UnitAbilityCases.AddGivesLevelOne });
            list.Add(new Case { Name = "abilities.add-has-remove-generic", Run = UnitAbilityCases.Generic });
            list.Add(new Case { Name = "abilities.add-when-had-keeps-level", Run = UnitAbilityCases.AddWhenHadKeepsLevel });
            list.Add(new Case { Name = "abilities.add-kept-through-morph", Run = UnitAbilityCases.AddKeptThroughMorph });
            list.Add(new Case { Name = "abilities.remove-takes-it", Run = UnitAbilityCases.RemoveTakesIt });
            list.Add(new Case { Name = "abilities.gone", Run = UnitAbilityCases.Gone });
            list.Add(new Case { Name = "abilities.level-set", Run = UnitAbilityCases.LevelSet });
            list.Add(new Case { Name = "abilities.level-kept-in-range", Run = UnitAbilityCases.LevelKeptInRange });
            list.Add(new Case { Name = "abilities.hero-add-runs-learn", Run = UnitAbilityCases.HeroAddRunsLearn });
            list.Add(new Case { Name = "abilities.hero-level-runs-learn-per-level", Run = UnitAbilityCases.HeroLevelRunsLearnPerLevel });
            list.Add(new Case { Name = "abilities.hero-level-past-last-stops-at-last", Run = UnitAbilityCases.HeroLevelPastLastStopsAtLast });
            list.Add(new Case { Name = "abilities.hero-level-lowered-runs-nothing", Run = UnitAbilityCases.HeroLevelLoweredRunsNothing });
            list.Add(new Case { Name = "abilities.hero-reaction-lowering-ends-steps", Run = UnitAbilityCases.HeroReactionLoweringEndsSteps });
            list.Add(new Case { Name = "abilities.only-hero-skills-of-heroes-learn", Run = UnitAbilityCases.OnlyHeroSkillsOfHeroesLearn });
            list.Add(new Case { Name = "abilities.remove-runs-on-lost", Run = UnitAbilityCases.RemoveRunsOnLost });
            list.Add(new Case { Name = "abilities.only-hero-skills-of-heroes-are-lost", Run = UnitAbilityCases.OnlyHeroSkillsOfHeroesAreLost });
            list.Add(new Case { Name = "abilities.remove-ends-running-cast", Run = UnitAbilityCases.RemoveEndsRunningCast });
            list.Add(new Case { Name = "abilities.lost-listens-for-nothing", Run = UnitAbilityCases.LostListensForNothing });
        }

        /// <summary>Adds the hero cases.</summary>
        private static void AddHeroes(List<Case> list)
        {
            list.Add(new Case { Name = "heroes.unregistered-is-hero", Run = HeroCases.UnregisteredIsHero });
            list.Add(new Case { Name = "heroes.experience", Run = HeroCases.Experience });
            list.Add(new Case { Name = "heroes.level", Run = HeroCases.Level });
            list.Add(new Case { Name = "heroes.level-up", Run = HeroCases.LevelUp });
            list.Add(new Case { Name = "heroes.add-experience", Run = HeroCases.AddExperience });
            list.Add(new Case { Name = "heroes.experience-to-next-level", Run = HeroCases.ExperienceToNextLevel });
            list.Add(new Case { Name = "heroes.installed-experience", Run = HeroCases.InstalledExperience });
            list.Add(new Case { Name = "heroes.experience-gone", Run = HeroCases.ExperienceGone });
            list.Add(new Case { Name = "heroes.experience-without-handle", Run = HeroCases.ExperienceWithoutHandle });
            list.Add(new Case { Name = "heroes.experience-gives-levels", Run = HeroCases.ExperienceGivesLevels });
            list.Add(new Case { Name = "heroes.level-gives-experience", Run = HeroCases.LevelGivesExperience });
            list.Add(new Case { Name = "heroes.installed-experience-gives-levels", Run = HeroCases.InstalledExperienceGivesLevels });
            list.Add(new Case { Name = "heroes.stats", Run = HeroCases.Stats });
            list.Add(new Case { Name = "heroes.stat-bonus", Run = HeroCases.StatBonus });
            list.Add(new Case { Name = "heroes.stat-bonus-without-handle", Run = HeroCases.StatBonusWithoutHandle });
            list.Add(new Case { Name = "heroes.skill-points", Run = HeroCases.SkillPoints });
            list.Add(new Case { Name = "heroes.revive", Run = HeroCases.Revive });
            list.Add(new Case { Name = "heroes.gone", Run = HeroCases.Gone });
            list.Add(new Case { Name = "heroes.gone-before-sweep", Run = HeroCases.GoneBeforeSweep });
            list.Add(new Case { Name = "heroes.proper-name", Run = HeroCases.ProperName });
            list.Add(new Case { Name = "heroes.shared-without-handle", Run = HeroCases.SharedWithoutHandle });
            list.Add(new Case { Name = "heroes.level-up-order", Run = HeroReactionCases.LevelUpOrder });
            list.Add(new Case { Name = "heroes.level-jump", Run = HeroReactionCases.LevelJump });
            list.Add(new Case { Name = "heroes.lowered-in-jump", Run = HeroReactionCases.LoweredInJump });
            list.Add(new Case { Name = "heroes.level-up-from-experience", Run = HeroReactionCases.LevelUpFromExperience });
            list.Add(new Case { Name = "heroes.level-up-through-set-level", Run = HeroReactionCases.LevelUpThroughSetLevel });
            list.Add(new Case { Name = "heroes.level-up-from-taken-in", Run = HeroReactionCases.LevelUpFromTakenIn });
            list.Add(new Case { Name = "heroes.level-up-after-quiet-levels", Run = HeroReactionCases.LevelUpAfterQuietLevels });
            list.Add(new Case { Name = "heroes.level-up-raised-in-reaction", Run = HeroReactionCases.LevelUpRaisedInReaction });
            list.Add(new Case { Name = "heroes.level-up-first-seen", Run = HeroReactionCases.LevelUpFirstSeen });
            list.Add(new Case { Name = "heroes.level-up-takes-in-entering-heroes", Run = HeroReactionCases.LevelUpTakesInEnteringHeroes });
            list.Add(new Case { Name = "heroes.level-up-stops-when-removed", Run = HeroReactionCases.LevelUpStopsWhenRemoved });
            list.Add(new Case { Name = "heroes.level-up-listens", Run = HeroReactionCases.LevelUpListens });
            list.Add(new Case { Name = "heroes.skill-listens", Run = HeroReactionCases.SkillListens });
            list.Add(new Case { Name = "heroes.skill-order", Run = HeroReactionCases.SkillOrder });
            list.Add(new Case { Name = "heroes.skill-raised", Run = HeroReactionCases.SkillRaised });
            list.Add(new Case { Name = "heroes.ability-learned-listens", Run = HeroReactionCases.AbilityLearnedListens });
            list.Add(new Case { Name = "heroes.skill-without-point", Run = HeroReactionCases.SkillWithoutPoint });
        }

        /// <summary>Adds the item, destructable, damage, ability and buff cases, and the rest (Lua allows 200 locals in a function, and each case made here is one).</summary>
        private static void AddLast(List<Case> list)
        {
            list.Add(new Case { Name = "items.registered-class", Run = ItemCases.RegisteredClass });
            list.Add(new Case { Name = "items.fallback-class", Run = ItemCases.FallbackClass });
            list.Add(new Case { Name = "items.pickup-order", Run = ItemCases.PickupOrder });
            list.Add(new Case { Name = "items.drop-order", Run = ItemCases.DropOrder });
            list.Add(new Case { Name = "items.listen", Run = ItemCases.Listens });
            list.Add(new Case { Name = "items.sweep", Run = ItemCases.SweepDetectsRemoval });
            list.Add(new Case { Name = "items.owned-create", Run = ItemCases.OwnedByOwner });
            list.Add(new Case { Name = "items.create-of-class", Run = ItemCases.CreateOfClass });
            list.Add(new Case { Name = "shadowed-fields.owner", Run = ShadowFieldCases.Scope });
            list.Add(new Case { Name = "shadowed-fields.hero", Run = ShadowFieldCases.Hero });
            list.Add(new Case { Name = "shadowed-fields.item", Run = ShadowFieldCases.Item });
            list.Add(new Case { Name = "damage.deal-from-code", Run = DamageCases.DealFromCode });
            list.Add(new Case { Name = "damage.deal-runs-reactions", Run = DamageCases.DealRunsReactions });
            list.Add(new Case { Name = "damage.deal-refused", Run = DamageCases.DealRefused });
            list.Add(new Case { Name = "damage.deal-to-the-dead", Run = DamageCases.DealToTheDead });
            list.Add(new Case { Name = "damage.deal-is-attack", Run = DamageCases.DealIsAttack });
            list.Add(new Case { Name = "damage.destructable", Run = DamageCases.DealToDestructable });
            list.Add(new Case { Name = "damage.destructable-gone", Run = DamageCases.DealToDestructableGone });
            list.Add(new Case { Name = "items.charges", Run = ItemCases.Charges });
            list.Add(new Case { Name = "items.player", Run = ItemCases.PlayerOf });
            list.Add(new Case { Name = "items.owner-follows-the-pick-up", Run = ItemCases.OwnerFollowsThePickUp });
            list.Add(new Case { Name = "items.owner-follows-an-unseen-pick-up", Run = ItemCases.OwnerFollowsAnUnseenPickUp });
            list.Add(new Case { Name = "items.owner-follows-a-new-carrier", Run = ItemCases.OwnerFollowsANewCarrier });
            list.Add(new Case { Name = "items.position", Run = ItemCases.Position });
            list.Add(new Case { Name = "items.move-keeps-the-place-the-game-gives", Run = ItemCases.MoveToKeepsThePlaceTheGameGives });
            list.Add(new Case { Name = "items.move-removed-keeps-the-last-place", Run = ItemCases.MoveToOnARemovedItemKeepsTheLastPlace });
            list.Add(new Case { Name = "items.carrier", Run = ItemCases.Carrier });
            list.Add(new Case { Name = "items.carrier-in-hooks", Run = ItemCases.CarrierInHooks });
            list.Add(new Case { Name = "items.carrier-dies", Run = ItemCases.CarrierDies });
            list.Add(new Case { Name = "items.carrier-unseen", Run = ItemCases.CarrierUnseen });
            list.Add(new Case { Name = "items.carrier-slot-move", Run = ItemCases.CarrierSlotMove });
            list.Add(new Case { Name = "items.carrier-hero-dies", Run = ItemCases.CarrierHeroDies });
            list.Add(new Case { Name = "items.dying-unit-drops-before-its-death", Run = ItemCases.DyingUnitDropsBeforeItsDeath });
            list.Add(new Case { Name = "items.dying-unit-drops-last-slot-first", Run = ItemCases.DyingUnitDropsLastSlotFirst });
            list.Add(new Case { Name = "items.dying-units-item-reads-its-place-when-dropped", Run = ItemCases.DyingUnitsItemReadsItsPlaceWhenDropped });
            list.Add(new Case { Name = "items.place-follows-the-carrier", Run = ItemCases.PlaceFollowsTheCarrier });
            list.Add(new Case { Name = "items.place-in-the-bag-follows-the-holder", Run = ItemCases.PlaceInTheBagFollowsTheHolder });
            list.Add(new Case { Name = "items.place-of-an-unseen-pick-up", Run = ItemCases.PlaceOfAnUnseenPickUpIsTheGames });
            list.Add(new Case { Name = "items.removed-unit-drops-after", Run = ItemCases.RemovedUnitDropsAfterTheCall });
            list.Add(new Case { Name = "items.give-into-a-slot", Run = ItemCases.GiveIntoASlot });
            list.Add(new Case { Name = "items.give-into-a-taken-slot-swaps", Run = ItemCases.GiveIntoATakenSlotSwaps });
            list.Add(new Case { Name = "items.give-a-held-item-into-a-slot", Run = ItemCases.GiveAHeldItemIntoASlot });
            list.Add(new Case { Name = "items.give-into-a-slot-refused", Run = ItemCases.GiveIntoASlotRefused });
            list.Add(new Case { Name = "items.create-for-a-unit", Run = ItemCases.CreateForAUnit });
            list.Add(new Case { Name = "items.create-for-a-full-unit", Run = ItemCases.CreateForAFullUnit });
            list.Add(new Case { Name = "items.create-into-a-slot", Run = ItemCases.CreateIntoASlot });
            list.Add(new Case { Name = "items.give-full-inventory", Run = ItemCases.GiveFullInventory });
            list.Add(new Case { Name = "items.give-held-again", Run = ItemCases.GiveHeldItemAgain });
            list.Add(new Case { Name = "items.freed-slot-first", Run = ItemCases.FreedSlotFirst });
            list.Add(new Case { Name = "items.player-gone-before-read", Run = ItemCases.PlayerGoneBeforeRead });
            list.Add(new Case { Name = "items.gone-reads-last", Run = ItemCases.ItemGoneReadsLast });
            list.Add(new Case { Name = "items.gone-before-sweep", Run = ItemCases.GoneBeforeSweep });
            list.Add(new Case { Name = "items.shared-without-handle", Run = ItemCases.SharedWithoutHandle });
            list.Add(new Case { Name = "destructable.position", Run = DestructableCases.Position });
            list.Add(new Case { Name = "destructables.watch", Run = DestructableCases.Watch });
            list.Add(new Case { Name = "destructables.sweep", Run = DestructableCases.SweepDetectsRemoval });
            list.Add(new Case { Name = "destructables.sweep-capped", Run = DestructableCases.SweepCapped });
            list.Add(new Case { Name = "destructables.sweep-takes-turns", Run = DestructableCases.SweepTakesTurns });
            list.Add(new Case { Name = "destructables.sweep-at-the-cap", Run = DestructableCases.SweepAtTheCap });
            list.Add(new Case { Name = "destructables.sweep-ends-neighbours", Run = DestructableCases.SweepEndsNeighbours });
            list.Add(new Case { Name = "destructables.sweep-while-the-list-empties", Run = DestructableCases.SweepWhileTheListEmpties });
            list.Add(new Case { Name = "destructables.remove-after-death", Run = DestructableCases.RemoveAfterDeath });
            list.Add(new Case { Name = "destructables.remove-living", Run = DestructableCases.RemoveLiving });
            list.Add(new Case { Name = "destructables.create-in-scope", Run = DestructableCases.CreateInScope });
            list.Add(new Case { Name = "destructables.create-class", Run = DestructableCases.CreateClass });
            list.Add(new Case { Name = "destructables.create-unmakeable", Run = DestructableCases.CreateUnmakeable });
            list.Add(new Case { Name = "destructables.create-in-ended-scope", Run = DestructableCases.CreateInEndedScope });
            list.Add(new Case { Name = "destructables.kill-runs-death", Run = DestructableCases.KillRunsDeath });
            list.Add(new Case { Name = "destructables.kill-dead-or-gone", Run = DestructableCases.KillDeadOrGone });
            list.Add(new Case { Name = "destructables.life-sets", Run = DestructableCases.LifeSets });
            list.Add(new Case { Name = "destructables.life-low-kills", Run = DestructableCases.LifeLowKills });
            list.Add(new Case { Name = "destructables.life-on-dead", Run = DestructableCases.LifeOnDead });
            list.Add(new Case { Name = "destructables.life-gone", Run = DestructableCases.LifeGone });
            list.Add(new Case { Name = "destructables.max-life-sets", Run = DestructableCases.MaxLifeSets });
            list.Add(new Case { Name = "destructables.max-life-without-handle", Run = DestructableCases.MaxLifeWithoutHandle });
            list.Add(new Case { Name = "destructables.restore-runs-restored", Run = DestructableCases.RestoreRunsRestored });
            list.Add(new Case { Name = "destructables.restore-then-kill", Run = DestructableCases.RestoreThenKill });
            list.Add(new Case { Name = "destructables.restore-living", Run = DestructableCases.RestoreLiving });
            list.Add(new Case { Name = "destructables.restored-reports-errors", Run = DestructableCases.RestoredReportsErrors });
            list.Add(new Case { Name = "components.ended-in-onremoved", Run = EndedCases.ReadInOnRemoved });
            list.Add(new Case { Name = "damage.order", Run = DamageCases.Order });
            list.Add(new Case { Name = "damage.change-amount", Run = DamageCases.ChangeAmount });
            list.Add(new Case { Name = "damage.unknown-units", Run = DamageCases.UnknownUnits });
            list.Add(new Case { Name = "damage.listen", Run = DamageCases.Listen });
            list.Add(new Case { Name = "damage.nested", Run = DamageCases.Nested });
            list.Add(new Case { Name = "damage.actor-ends-mid-dispatch", Run = DamageCases.ActorEndsMidDispatch });
            list.Add(new Case { Name = "damage.error-isolated", Run = DamageCases.ErrorIsolated });
            list.Add(new Case { Name = "damage.same-player", Run = DamageCases.SamePlayer });
            list.Add(new Case { Name = "damage.deal-tag-in-reactions", Run = DamageCases.DealTagInReactions });
            list.Add(new Case { Name = "damage-package.no-trigger-without-need", Run = InstallCases.NoTriggerWithoutNeed });
            list.Add(new Case { Name = "damage-package.listens-for-an-interface", Run = InstallCases.ListensForAnInterface });
            list.Add(new Case { Name = "damage-package.listens-for-on-damaged", Run = InstallCases.ListensForOnDamaged });
            list.Add(new Case { Name = "damage-package.order", Run = StageCases.Order });
            list.Add(new Case { Name = "damage-package.before-armor-changes", Run = StageCases.BeforeArmorChanges });
            list.Add(new Case { Name = "damage-package.armor-for-one-hit", Run = StageCases.ArmorForOneHit });
            list.Add(new Case { Name = "damage-package.after-armor-changes", Run = StageCases.AfterArmorChanges });
            list.Add(new Case { Name = "damage-package.type-set-after-armor-reported", Run = StageCases.TypeSetAfterArmorReported });
            list.Add(new Case { Name = "damage-package.removed-mid-stage", Run = StageCases.RemovedMidStage });
            list.Add(new Case { Name = "damage-package.melee-or-ranged", Run = StageCases.MeleeOrRanged });
            list.Add(new Case { Name = "damage-package.attack-type-change", Run = StageCases.AttackTypeChange });
            list.Add(new Case { Name = "damage-package.defense-type-for-one-hit", Run = StageCases.DefenseTypeForOneHit });
            list.Add(new Case { Name = "damage-package.attack-type-reads-late", Run = StageCases.AttackTypeReadsLate });
            list.Add(new Case { Name = "damage-package.lethal-spare", Run = LandingCases.LethalSpare });
            list.Add(new Case { Name = "damage-package.lethal-only-when-lethal", Run = LandingCases.LethalOnlyWhenLethal });
            list.Add(new Case { Name = "damage-package.landed-after-zero", Run = LandingCases.LandedAfterZero });
            list.Add(new Case { Name = "damage-package.burst-counts", Run = LandingCases.BurstCounts });
            list.Add(new Case { Name = "damage-package.immunity-stops", Run = LandingCases.ImmunityStops });
            list.Add(new Case { Name = "damage-package.immunity-after-type-change", Run = LandingCases.ImmunityAfterTypeChange });
            list.Add(new Case { Name = "damage-package.immunity-inside-hit", Run = LandingCases.ImmunityInsideHit });
            list.Add(new Case { Name = "damage-package.nested-keeps-outer", Run = LandingCases.NestedKeepsOuter });
            list.Add(new Case { Name = "damage-package.stray-open-ended", Run = LandingCases.StrayOpenEnded });
            list.Add(new Case { Name = "damage-package.lethal-after-bonus", Run = LandingCases.LethalAfterBonus });
            list.Add(new Case { Name = "damage-package.killed-only-killing-hit", Run = LandingCases.KilledOnlyKillingHit });
            list.Add(new Case { Name = "damage-package.bonus-kill-credits-the-bonus", Run = LandingCases.BonusKillCreditsTheBonus });
            list.Add(new Case { Name = "damage-package.lethal-stops-when-spared", Run = LandingCases.LethalStopsWhenSpared });
            list.Add(new Case { Name = "damage-package.removed-target-not-killed", Run = LandingCases.RemovedTargetNotKilled });
            list.Add(new Case { Name = "damage-package.outer-found-below-stray", Run = LandingCases.OuterFoundBelowStray });
            list.Add(new Case { Name = "damage-package.spare-after-lethal-deal", Run = LandingCases.SpareAfterLethalDeal });
            list.Add(new Case { Name = "damage-package.spare-after-own-lethal-deal", Run = LandingCases.SpareAfterOwnLethalDeal });
            list.Add(new Case { Name = "damage-package.lowered-still-lethal-after-deal", Run = LandingCases.LoweredStillLethalAfterDeal });
            list.Add(new Case { Name = "damage-package.type-out-of-range-refused", Run = StageCases.TypeOutOfRangeRefused });
            list.Add(new Case { Name = "damage-package.deal-stopped-returns-null", Run = DealCases.DealStoppedReturnsNull });
            list.Add(new Case { Name = "damage-package.raw-deal-chain-cut", Run = DealCases.RawDealChainCut });
            list.Add(new Case { Name = "damage-package.code-deal-of-zero-runs", Run = DealCases.CodeDealOfZeroRuns });
            list.Add(new Case { Name = "damage-package.zero-hit-quiet", Run = StageCases.ZeroHitQuiet });
            list.Add(new Case { Name = "damage-package.sourceless-hit-runs", Run = StageCases.SourcelessHitRuns });
            list.Add(new Case { Name = "damage-package.late-reaction-runs", Run = StageCases.LateReactionRuns });
            list.Add(new Case { Name = "damage-package.zero-attack-runs", Run = StageCases.ZeroAttackRuns });
            list.Add(new Case { Name = "damage-package.lethal-reaction-kill-credits-its-hit", Run = LandingCases.LethalReactionKillCreditsItsHit });
            list.Add(new Case { Name = "damage-package.deal-returns-hit", Run = DealCases.DealReturnsHit });
            list.Add(new Case { Name = "damage-package.deal-attack-type", Run = DealCases.DealAttackType });
            list.Add(new Case { Name = "damage-package.deal-starts-listening", Run = DealCases.DealStartsListening });
            list.Add(new Case { Name = "damage-package.deal-inside-reaction-nests", Run = DealCases.DealInsideReactionNests });
            list.Add(new Case { Name = "damage-package.chain-cut", Run = DealCases.ChainCut });
            list.Add(new Case { Name = "damage-package.chain-across-frames", Run = DealCases.ChainAcrossFrames });
            list.Add(new Case { Name = "damage-package.chain-through-on-damaged", Run = DealCases.ChainThroughOnDamaged });
            list.Add(new Case { Name = "death.order", Run = DeathCases.Order });
            list.Add(new Case { Name = "death.self", Run = DeathCases.Self });
            list.Add(new Case { Name = "death.same-player", Run = DeathCases.SamePlayer });
            list.Add(new Case { Name = "death.not-an-end", Run = DeathCases.NotAnEnd });
            list.Add(new Case { Name = "abilities.registered", Run = AbilityCases.Registered });
            list.Add(new Case { Name = "abilities.generic", Run = AbilityCases.Generic });
            list.Add(new Case { Name = "cast.order", Run = AbilityCases.Order });
            list.Add(new Case { Name = "cast.fields", Run = AbilityCases.Fields });
            list.Add(new Case { Name = "cast.ability-owns-what-it-starts", Run = AbilityCases.AbilityOwnsWhatItStarts });
            list.Add(new Case { Name = "cast.next-ends-previous", Run = AbilityCases.NextEndsPrevious });
            list.Add(new Case { Name = "cast.listen", Run = AbilityCases.Listens });
            list.Add(new Case { Name = "cast.ends-with-unit", Run = AbilityCases.EndsWithUnit });
            list.Add(new Case { Name = "cast.other-ability-end-keeps", Run = AbilityCases.OtherAbilityEndKeeps });
            list.Add(new Case { Name = "cast.map-cast-listens", Run = AbilityCases.MapCastListens });
            list.Add(new Case { Name = "buff.ticks-then-expiry", Run = BuffCases.TicksThenExpiry });
            list.Add(new Case { Name = "buff.refresh", Run = BuffCases.Refresh });
            list.Add(new Case { Name = "buff.separate", Run = BuffCases.Separate });
            list.Add(new Case { Name = "buff.per-source", Run = BuffCases.PerSource });
            list.Add(new Case { Name = "buff.display", Run = BuffCases.Display });
            list.Add(new Case { Name = "buff.unit-ends", Run = BuffCases.UnitEnds });
            list.Add(new Case { Name = "buff.reapply-in-onexpire", Run = BuffCases.ReapplyInOnExpire });
            list.Add(new Case { Name = "buff.until-removed", Run = BuffCases.UntilRemoved });
            list.Add(new Case { Name = "buff.ends-on-death", Run = BuffCases.EndsOnDeath });
            list.Add(new Case { Name = "buff.kept-through-death", Run = BuffCases.KeptThroughDeath });
            list.Add(new Case { Name = "buff.listens-for-death", Run = BuffCases.ListensForDeath });
            list.Add(new Case { Name = "floats.whole-literal", Run = FloatCases.WholeLiteral });
            list.Add(new Case { Name = "floats.no-wrap", Run = FloatCases.NoWrap });
            list.Add(new Case { Name = "floats.folded", Run = FloatCases.Folded });
            list.Add(new Case { Name = "floats.stored", Run = FloatCases.Stored });
            list.Add(new Case { Name = "floats.converted", Run = FloatCases.Converted });
            list.Add(new Case { Name = "floats.bare", Run = FloatCases.Bare });
            list.Add(new Case { Name = "floats.beyond-the-rewrite", Run = FloatCases.Beyond });
            list.Add(new Case { Name = "floats.ints-stay", Run = FloatCases.IntsStay });
            list.Add(new Case { Name = "floats.ints-start-as-integers", Run = FloatCases.IntsStart });
            list.Add(new Case { Name = "floats.made-floats-in-calls", Run = FloatCases.MadeFloatsInCalls });
            list.Add(new Case { Name = "floats.unit-whole-values", Run = FloatCases.UnitWholeValues });
            list.Add(new Case { Name = "text.nullable-concat", Run = TextCases.NullableConcat });
            list.Add(new Case { Name = "text.conditional-access-concat", Run = TextCases.ConditionalAccessConcat });
            list.Add(new Case { Name = "text.nullable-interpolation", Run = TextCases.NullableInterpolation });
            list.Add(new Case { Name = "text.nullable-operands", Run = TextCases.NullableOperands });
            list.Add(new Case { Name = "text.nullable-from-int", Run = TextCases.NullableFromInt });
            list.Add(new Case { Name = "text.nullable-char", Run = TextCases.NullableChar });
            list.Add(new Case { Name = "text.nullable-struct", Run = TextCases.NullableStruct });
            list.Add(new Case { Name = "text.nullable-to-string", Run = TextCases.NullableToString });
            list.Add(new Case { Name = "text.plain", Run = TextCases.Plain });
            list.Add(new Case { Name = "text.nullable-get-value-or-default", Run = TextCases.NullableGetValueOrDefault });
            list.Add(new Case { Name = "text.nullable-enum-to-string", Run = TextCases.NullableEnumToString });
            list.Add(new Case { Name = "collection-expressions.everywhere", Run = CollectionExpressionCases.Everywhere });
            list.Add(new Case { Name = "collection-expressions.spread", Run = CollectionExpressionCases.Spread });
            list.Add(new Case { Name = "templates.static-through-using-static", Run = TemplateCases.StaticThroughUsingStatic });
            list.Add(new Case { Name = "templates.instance-without-receiver", Run = TemplateCases.InstanceWithoutReceiver });
            list.Add(new Case { Name = "templates.instance-put", Run = TemplateCases.InstancePut });
            list.Add(new Case { Name = "strings.escapes", Run = StringCases.Escapes });
            list.Add(new Case { Name = "strings.raw-and-verbatim", Run = StringCases.RawAndVerbatim });
            list.Add(new Case { Name = "strings.utf8", Run = StringCases.Utf8 });
            list.Add(new Case { Name = "strings.interpolated", Run = StringCases.Interpolated });
            list.Add(new Case { Name = "strings.percent", Run = StringCases.Percent });
            list.Add(new Case { Name = "strings.folded", Run = StringCases.Folded });
            list.Add(new Case { Name = "conditionals.nullable-first-branch", Run = ConditionalCases.NullableFirstBranch });
            list.Add(new Case { Name = "conditionals.nullable-bool-first-branch", Run = ConditionalCases.NullableBoolFirstBranch });
            list.Add(new Case { Name = "conditionals.struct-type-parameter", Run = ConditionalCases.StructTypeParameter });
            list.Add(new Case { Name = "conditionals.still-right", Run = ConditionalCases.StillRight });
        }

        /// <summary>Adds the cases for what the compiler writes. A function of more than 200 cases does not load: each is a local in its Lua.</summary>
        private static void AddCompiler(List<Case> list)
        {
            list.Add(new Case { Name = "compiler.out-discard-lambda", Run = CompilerCases.OutDiscardLambda });
            list.Add(new Case { Name = "compiler.patterns.is-parentheses", Run = PatternCases.IsParentheses });
            list.Add(new Case { Name = "compiler.patterns.type-names", Run = PatternCases.TypeNames });
            list.Add(new Case { Name = "compiler.patterns.not-in-property", Run = PatternCases.NotInProperty });
            list.Add(new Case { Name = "compiler.patterns.extended-property", Run = PatternCases.ExtendedProperty });
            list.Add(new Case { Name = "compiler.patterns.positional-on-value", Run = PatternCases.PositionalOnValue });
            list.Add(new Case { Name = "compiler.return-last-in-loop", Run = CompilerCases.ReturnLastInLoop });
            list.Add(new Case { Name = "compiler.continue-in-switch", Run = CompilerCases.ContinueInSwitch });
            list.Add(new Case { Name = "compiler.primary-constructors", Run = CompilerCases.PrimaryConstructors });
            list.Add(new Case { Name = "compiler.initializer-temporaries", Run = CompilerCases.InitializerTemporaries });
            list.Add(new Case { Name = "compiler.foreach-disposes", Run = CompilerCases.ForeachDisposes });
            list.Add(new Case { Name = "compiler.private-base-fields", Run = CompilerCases.PrivateBaseFields });
            list.Add(new Case { Name = "compiler.local-function-before-declaration", Run = CompilerCases.LocalFunctionBeforeDeclaration });
            list.Add(new Case { Name = "compiler.using-declaration-returns", Run = CompilerCases.UsingDeclarationReturns });
            list.Add(new Case { Name = "compiler.two-using-declarations", Run = CompilerCases.TwoUsingDeclarations });
            list.Add(new Case { Name = "compiler.usings-in-one-statement", Run = UsingCases.InOneStatement });
            list.Add(new Case { Name = "compiler.nullable-equals-null", Run = CompilerCases.NullableEqualsNull });
            list.Add(new Case { Name = "compiler.switch-statement-patterns", Run = CompilerCases.SwitchStatementPatterns });
            list.Add(new Case { Name = "compiler.negated-negative-constant", Run = CompilerCases.NegatedNegativeConstant });
            list.Add(new Case { Name = "compiler.compound-conditional", Run = CompilerCases.CompoundConditional });
            list.Add(new Case { Name = "compiler.forms.patterns", Run = FormCases.Patterns });
            list.Add(new Case { Name = "compiler.forms.struct-copies", Run = FormCases.StructCopies });
            list.Add(new Case { Name = "compiler.forms.tuples", Run = FormCases.Tuples });
            list.Add(new Case { Name = "compiler.forms.declarations", Run = FormCases.Declarations });
            list.Add(new Case { Name = "compiler.forms.ranges", Run = FormCases.Ranges });
            list.Add(new Case { Name = "compiler.forms.ref-elements", Run = FormCases.RefElements });
            list.Add(new Case { Name = "compiler.forms.params", Run = FormCases.Params });
            list.Add(new Case { Name = "compiler.forms.strings", Run = FormCases.Strings });
            list.Add(new Case { Name = "compiler.forms.boxed-text", Run = FormCases.BoxedText });
            list.Add(new Case { Name = "compiler.forms.catches", Run = FormCases.Catches });
            list.Add(new Case { Name = "compiler.forms.checked", Run = FormCases.Checked });
            list.Add(new Case { Name = "compiler.forms.int-minimum", Run = FormCases.IntMinimum });
            list.Add(new Case { Name = "compiler.forms.core-int-minimum", Run = FormCases.CoreIntMinimum });
            list.Add(new Case { Name = "compiler.forms.generic-method-groups", Run = FormCases.GenericMethodGroups });
            list.Add(new Case { Name = "compiler.forms.logic-grouping", Run = FormCases.LogicGrouping });
            list.Add(new Case { Name = "compiler.forms.enum-text", Run = FormCases.EnumText });
            list.Add(new Case { Name = "compiler.forms.handle-casts", Run = FormCases.HandleCasts });
            list.Add(new Case { Name = "compiler.forms.silent", Run = FormCases.Silent });
            list.Add(new Case { Name = "compiler.library.collections", Run = LibraryCases.Collections });
            list.Add(new Case { Name = "compiler.library.linq", Run = LibraryCases.Linq });
            list.Add(new Case { Name = "compiler.library.text", Run = LibraryCases.Text });
            list.Add(new Case { Name = "compiler.library.numbers", Run = LibraryCases.Numbers });
            list.Add(new Case { Name = "compiler.library.types", Run = LibraryCases.Types });
            list.Add(new Case { Name = "compiler.library.forms", Run = LibraryCases.Forms });
            list.Add(new Case { Name = "flags.core-enum", Run = FlagCases.CoreEnum });
            list.Add(new Case { Name = "flags.ignored-enum", Run = FlagCases.IgnoredEnum });
            list.Add(new Case { Name = "flags.top-bits", Run = FlagCases.TopBits });
        }

        /// <summary>Runs every case on a fresh world, and returns a PASS or FAIL line for each, then DONE.</summary>
        public static List<string> RunAll()
        {
            var lines = new List<string>();
            var cases = All();
            foreach (var testCase in cases)
            {
                var world = FakeWorld.Install();
                try
                {
                    testCase.Run(world);
                    lines.Add("PASS " + testCase.Name);
                }
                catch (Exception error)
                {
                    lines.Add("FAIL " + testCase.Name + ": " + error.Message);
                }
            }

            lines.Add("DONE " + cases.Count);
            return lines;
        }

        /// <summary>The map's entry point: prints every line of RunAll.</summary>
        public static void Main()
        {
            foreach (var line in RunAll())
            {
                Emit(line);
            }
        }

        // Writes one line of output; in Lua this is the print global, which the Lua test host captures.
        /// @CSharpLua.Template = "print({0})"
        public static void Emit(string line)
        {
            Console.WriteLine(line);
        }
    }
}
