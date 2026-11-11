using Moonwell.Hosting;
using Moonwell.Testing;

namespace Moonwell.Tests;

/// <summary>
/// Runs every case as two machines, one after the other: first as written, then as the machine of the next player
/// (FakeWorld.MachineShift). The calls that change the game (GameChanging) must be the same on both, in the same order
/// and with the same values, or the two would fall out of step in a real game. The second machine's own checks are
/// kept, not thrown (Expect.Kept): a case checks what the player at its own machine sees.
///
/// It is not a network: the second machine gets no shared message from the first, and the package worlds
/// (frames, input, terrain and the rest) are not recorded.
/// </summary>
public class TwinTests
{
    /// <summary>The reviewed list of cases whose two machines differ, each with why. A case here must still differ.</summary>
    private static readonly Dictionary<string, string> Differing = new()
    {
        // On purpose: an effect shown to one player is made on every machine, so the handle is the same, with no model at the others.
        ["effects.shown-to"] = "MakeEffect gets the model at the player's machine and an empty one at the others",
        ["effects.shown-to-other"] = "MakeEffect and MakeEffectOn get the model at the player's machine and an empty one at the others",

        // Not a network: what the first person's machine reads and shares never reaches the second machine here.
        ["sounds.music-play-once"] = "a music file's length is read at the first person's machine and shared; with no message from it, the second machine's playlist clock never starts",
        ["transmissions.voice"] = "the voice's length is read at the first person's machine and shared; with no message from it, the line never starts at the second machine, and the case stops at a sound never made",

        // The case's own code cannot go on at the second machine, so its later calls are missing there.
        ["interactions.find.local-untracked"] = "the case reads a list its Local.For never filled at the second machine, and stops before the synced Find that starts the tracking",
        ["save.load-edited"] = "the case edits the file its save wrote, which only the saving player's machine writes, and stops at the second machine, where there is none",
    };

    public static IEnumerable<object[]> Names() => CaseTests.Names();

    [Theory]
    [MemberData(nameof(Names))]
    public void Same_on_both_machines(string name)
    {
        var difference = Difference(Cases.All().Single(c => c.Name == name));
        if (Differing.ContainsKey(name))
        {
            Assert.True(difference != null, "the two machines now make the same calls: take " + name + " off TwinTests.Differing");
        }
        else
        {
            Assert.True(difference == null, difference + Environment.NewLine + "If they differ on purpose, add " + name + " to TwinTests.Differing with why.");
        }
    }

    [Fact]
    public void A_change_inside_Local_For_is_found()
    {
        var difference = Difference(new Case { Run = world => Local.For(Player.Get(0), () => Player.Get(0).Gold = 5) });
        Assert.Contains("SetState(slot 0, Gold, 5)", difference);
    }

    [Fact]
    public void A_check_of_what_one_player_sees_is_no_difference()
    {
        Assert.Null(Difference(new Case
        {
            Run = world =>
            {
                Expect.True(Runtime.World.IsLocal(Player.Get(0).Handle), "at player 0's machine");
                Player.Get(0).Gold = 5;
            },
        }));
    }

    [Fact]
    public void The_reviewed_lists_name_what_exists()
    {
        var cases = Cases.All().Select(c => c.Name).ToHashSet();
        Assert.DoesNotContain(Differing.Keys, name => !cases.Contains(name));

        // A member no longer marked needs no entry, and one that never was has a wrong name.
        var marked = GameChanging.OneMachineMembers.Keys.Select(member => member.Name).ToHashSet();
        Assert.DoesNotContain(GameChanging.OneMachine.Keys, name => !marked.Contains(name));
    }

    [Fact]
    public void The_marks_follow_the_natives_tags()
    {
        var marked = GameChanging.Members.Keys.Select(member => member.Name).ToHashSet();
        Assert.Contains(nameof(IWorld.SetLife), marked);
        Assert.Contains(nameof(IWorld.SpawnUnit), marked);
        Assert.Contains(nameof(IWorld.WakeIn), marked);
        Assert.DoesNotContain(nameof(IWorld.LifeOf), marked);
        Assert.DoesNotContain(nameof(IWorld.IsLocal), marked);
        Assert.DoesNotContain(nameof(IWorld.PrintTo), marked);
    }

    /// <summary>Where the two machines' calls part, with the calls around it; null when they are the same.</summary>
    private static string? Difference(Case testCase)
    {
        var first = Calls(testCase, 0);
        var second = Calls(testCase, 1);
        var at = 0;
        while (at < first.Count && at < second.Count && first[at] == second[at])
        {
            at++;
        }

        if (at == first.Count && at == second.Count)
        {
            return null;
        }

        var lines = new List<string> { "The two machines part at call " + (at + 1) + ":" };
        for (var i = Math.Max(0, at - 3); i < at; i++)
        {
            lines.Add("  both:   " + first[i]);
        }

        for (var i = at; i < at + 4; i++)
        {
            lines.Add("  first:  " + (i < first.Count ? first[i] : "(no more calls)"));
            lines.Add("  second: " + (i < second.Count ? second[i] : "(no more calls)"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>The game-changing calls of one run of a case, as the first machine or the second.</summary>
    private static List<string> Calls(Case testCase, int machine)
    {
        var calls = new List<string>();
        FakeWorld.MachineShift = machine;
        FakeWorld.Around = world => RecordingWorld.Around(world, calls);
        Expect.Kept = machine == 0 ? null : new List<string>();
        try
        {
            testCase.Run(FakeWorld.Install());
        }
        catch (Exception) when (machine != 0)
        {
            // The case's own code could not go on at the second machine, as after a list a Local.For never filled: its calls so far are compared.
        }
        finally
        {
            FakeWorld.MachineShift = 0;
            FakeWorld.Around = null;
            Expect.Kept = null;
        }

        return calls;
    }
}
