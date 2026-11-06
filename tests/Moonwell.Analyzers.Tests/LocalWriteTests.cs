namespace Moonwell.Analyzers.Tests;

/// <summary>MW0048: what code of one machine may write, and [MachineLocal] on a field.</summary>
public class LocalWriteTests
{
    private const string Shared = """
            public static int Score;
            public static int Other;
            public static string? Text;
            public static int Level { get; set; }
            public static Panel Main = new Panel();
            public static List<int> Scores = new List<int>();
            public static int[] Cells = new int[4];
            public static Dictionary<int, int> Keys = new Dictionary<int, int>();
            public static HashSet<int> Seen = new HashSet<int>();
            public static Queue<int> Line = new Queue<int>();
            public static Stack<int> Pile = new Stack<int>();
            public static OrderedMap<int, int> Map = new OrderedMap<int, int>();
            public static OrderedSet<int> Set = new OrderedSet<int>();
            public static System.Text.StringBuilder Log = new System.Text.StringBuilder();
            public static Random Rng = new Random(1);
            public static Pt At;
            public static event Action? Changed;

            [MachineLocal] public static int Tab;
            [MachineLocal] public static int Page { get; set; }
            [MachineLocal] public static List<int> Tabs = new List<int>();
            [MachineLocal] public static int[] Slots = new int[4];
            [MachineLocal] public static Pt Mark;

            public static void Show() { }
            public static void Bump() { Score++; }
            public static void Add(ref int to) { to++; }
            public static void Give(out int to) { to = 1; }
            public static Panel Find() => Main;

            public struct Pt { public float X; }

            public sealed class Panel
            {
                public int Count;
                public int Size { get; set; }
                public Pt At;
                public List<int> Items = new List<int>();
                public event Action? Done;
                public void Fill() { Count = 1; }
                public void Ring() => Done?.Invoke();
            }
        """;

    [Fact]
    public void a_field_written_inside_is_rejected()
    {
        var found = MapCode.Check("""
            Local.For(p, () =>
            {
                Score = 1;
                Other += 1;
                Score++;
                --Other;
                Text ??= "x";
            });
            """, Shared);

        Assert.Equal(new[] { "MW0048: Score", "MW0048: Other", "MW0048: Score", "MW0048: Other", "MW0048: Text" }, found);
    }

    [Fact]
    public void a_field_read_inside_is_allowed()
    {
        Assert.Empty(MapCode.Check("""
            Local.For(p, () =>
            {
                var sum = Score + Level + Main.Count + Main.Size;
            });
            """, Shared));
    }

    [Fact]
    public void an_auto_property_and_a_field_of_an_object_are_written_like_a_field()
    {
        var found = MapCode.Check("""
            var panel = Find();
            Local.For(p, () =>
            {
                Level = 1;
                Main.Count = 1;
                Main.Size = 1;
                panel.Count = 1;
            });
            """, Shared);

        Assert.Equal(new[] { "MW0048: Level", "MW0048: Main.Count", "MW0048: Main.Size", "MW0048: panel.Count" }, found);
    }

    [Fact]
    public void a_field_of_this_written_inside_a_block_is_rejected_even_in_a_constructor()
    {
        Assert.Equal(new[] { "MW0048: Count", "MW0048: Count" }, MapCode.CheckFile("""
            using Moonwell;

            namespace Map
            {
                public sealed class Panel
                {
                    public int Count;

                    public Panel(Player p)
                    {
                        Local.For(p, () => Count = 1);
                    }

                    public void Open(Player p)
                    {
                        Local.For(p, () => Count = 2);
                    }
                }
            }
            """));
    }

    [Fact]
    public void marked_fields_and_auto_properties_are_written_inside()
    {
        Assert.Empty(MapCode.Check("""
            Local.For(p, () =>
            {
                Tab = 1;
                Tab += Score;
                Tab++;
                Page = 2;
                Tabs.Add(1);
                Tabs[0] = 2;
                Slots[0] = 3;
                Mark.X = 4f;
                Add(ref Tab);
                Give(out Tab);
            });
            """, Shared));
    }

    [Fact]
    public void a_marked_field_is_read_only_in_code_of_one_machine()
    {
        var found = MapCode.Check("""
            var field = Tab;
            var property = Page;
            var list = Tabs.Count;
            Local.For(p, () => { var inside = Tab + Page + Tabs.Count; });
            """, Shared + """
                [MachineLocal] public static int Read() => Tab;
            """);

        Assert.Equal(new[] { "MW0002: Tab", "MW0002: Page", "MW0002: Tabs" }, found);
    }

    [Fact]
    public void a_plain_write_to_a_marked_field_outside_is_allowed()
    {
        Assert.Empty(MapCode.Check("""
            Tab = 0;
            Page = 0;
            (Tab, Page) = (1, 2);
            Give(out Tab);
            """, Shared));
    }

    [Fact]
    public void a_write_that_reads_a_marked_field_outside_is_mw0002()
    {
        var found = MapCode.Check("""
            Tab += 1;
            Tab++;
            Page++;
            Add(ref Tab);
            Tabs.Add(1);
            Slots[0] = 1;
            Mark.X = 1f;
            """, Shared);

        Assert.Equal(new[] { "MW0002: Tab", "MW0002: Tab", "MW0002: Page", "MW0002: Tab", "MW0002: Tabs", "MW0002: Slots", "MW0002: Mark" }, found);
    }

    [Fact]
    public void a_marked_field_in_a_tuple_of_values_is_read()
    {
        Assert.Equal(new[] { "MW0002: Tab", "MW0002: Page" }, MapCode.Check("(Score, Other) = (Tab, Page);", Shared));
    }

    [Fact]
    public void the_initializer_of_a_marked_field_is_checked_as_code_of_one_machine()
    {
        // The value stays where only one machine's code reads it. The class's statics may first run inside a block, so a handle made there is MW0001.
        Assert.Equal(new[] { "MW0001: Natives.CreateGroup()" }, MapCode.Check(string.Empty, """
                [MachineLocal] public static player Me = Natives.GetLocalPlayer();
                [MachineLocal] public static group Mine = Natives.CreateGroup();
            """));
    }

    [Fact]
    public void a_marked_property_with_a_body_stays_mw0002_when_written_outside()
    {
        // Only a field and an auto-property hold a value: this setter is code of one machine.
        Assert.Equal(new[] { "MW0002: Odd" }, MapCode.Check("Odd = 1;", """
                [MachineLocal] public static int Odd { get => 1; set { } }
            """));
    }

    [Fact]
    public void a_local_or_parameter_from_outside_the_block_is_rejected()
    {
        var found = MapCode.Check(
            """
            var n = 0;
            Local.For(p, () =>
            {
                n = 1;
                n++;
            });
            Take(p, 1);
            """,
            """
                private static void Take(Player who, int amount)
                {
                    Local.For(who, () => amount = 2);
                }
            """);

        Assert.Equal(new[] { "MW0048: n", "MW0048: n", "MW0048: amount" }, found);
    }

    [Fact]
    public void a_local_of_the_block_is_free_at_any_depth()
    {
        Assert.Empty(MapCode.Check("""
            Local.For(p, () =>
            {
                var n = 0;
                n = 1;
                Action add = () => n++;
                add();
                void Up() { n++; }
                Up();
                new List<int>().ForEach(x => n += x);
                Local.For(p, () => n++);
            });
            """));
    }

    [Fact]
    public void a_lambda_through_a_delegate_parameter_is_checked()
    {
        var found = MapCode.Check("ShowTo(p, () => Score = 1);", Shared + """
                public static void ShowTo(Player who, Action action) => Local.For(who, action);
            """);

        Assert.Equal(new[] { "MW0048: Score" }, found);
    }

    [Fact]
    public void a_machine_local_method_is_checked_as_a_block()
    {
        var found = MapCode.Check("""
            var n = 0;
            [MachineLocal] void Draw()
            {
                n = 1;
                Score = 1;
            }
            """, Shared + """
                [MachineLocal]
                public static void Hud(int amount)
                {
                    var mine = 0;
                    mine++;
                    amount = 2;
                    Other = 2;
                }
            """);

        Assert.Equal(new[] { "MW0048: n", "MW0048: Score", "MW0048: Other" }, found);
    }

    [Fact]
    public void what_a_block_calls_is_followed()
    {
        var found = MapCode.Check(
            """
            Local.For(p, () => Bump());
            Local.For(p, Bump);
            Local.For(p, () => { Total = 1; });
            Local.For(p, () => { var made = new Counted(); });
            Base shown = new Derived();
            Local.For(p, () => shown.Show());
            """,
            Shared + """
                private static int total;
                public static int Total { get => total; set { total = value; } }

                public sealed class Counted
                {
                    private static int made;
                    public Counted() { made++; }
                }

                public class Base { public virtual void Show() { } }
                public sealed class Derived : Base { public override void Show() { Score = 1; } }
            """);

        Assert.Equal(new[] { "MW0048: Bump()", "MW0048: Bump", "MW0048: Total", "MW0048: new Counted()", "MW0048: shown.Show()" }, found);
    }

    [Fact]
    public void a_delegate_that_writes_a_local_from_outside_the_block_is_rejected()
    {
        var found = MapCode.Check("""
            var n = 0;
            Action bump = () => n++;
            Local.For(p, bump);
            void Up() { n++; }
            Local.For(p, Up);
            Local.For(p, () => Up());
            """);

        Assert.Equal(new[] { "MW0048: bump", "MW0048: Up", "MW0048: Up()" }, found);
    }

    [Fact]
    public void a_followed_method_writes_its_own_locals()
    {
        Assert.Empty(MapCode.Check("Local.For(p, Sum);", """
                private static void Sum()
                {
                    var n = 0;
                    n++;
                    Action bump = () => n++;
                    bump();
                    void Up() { n++; }
                    Up();
                    new List<int>().ForEach(x => n += x);
                }
            """));
    }

    [Fact]
    public void ref_out_and_tuple_writes_are_writes()
    {
        var found = MapCode.Check("""
            Local.For(p, () =>
            {
                Add(ref Score);
                Give(out Other);
                (Score, Other) = (1, 2);
                int.TryParse("1", out Score);
                var mine = 0;
                Add(ref mine);
                Give(out var made);
                (mine, made) = (1, 2);
            });
            """, Shared);

        Assert.Equal(new[] { "MW0048: Score", "MW0048: Other", "MW0048: Score", "MW0048: Other", "MW0048: Score" }, found);
    }

    [Fact]
    public void a_changing_call_on_a_shared_collection_is_rejected()
    {
        var found = MapCode.Check("""
            var outer = new List<int>();
            Local.For(p, () =>
            {
                Scores.Add(1);
                Scores.Clear();
                Keys.Add(1, 2);
                Seen.Add(1);
                Line.Enqueue(1);
                Pile.Pop();
                Array.Sort(Cells);
                Array.Copy(new int[4], Cells, 1);
                Map.Remove(1);
                Set.Add(1);
                Log.Append("x");
                Rng.Next();
                Scores?.Add(1);
                outer.Add(1);
            });
            """, Shared);

        Assert.Equal(new[]
        {
            "MW0048: Scores",
            "MW0048: Scores",
            "MW0048: Keys",
            "MW0048: Seen",
            "MW0048: Line",
            "MW0048: Pile",
            "MW0048: Cells",
            "MW0048: Cells",
            "MW0048: Map",
            "MW0048: Set",
            "MW0048: Log",
            "MW0048: Rng",
            "MW0048: Scores",
            "MW0048: outer",
        }, found);
    }

    [Fact]
    public void an_element_set_on_a_shared_collection_is_rejected()
    {
        var found = MapCode.Check("""
            Local.For(p, () =>
            {
                Scores[0] = 1;
                Cells[0] = 1;
                Keys[1] = 2;
                Map[1] = 2;
            });
            """, Shared);

        Assert.Equal(new[] { "MW0048: Scores[0]", "MW0048: Cells[0]", "MW0048: Keys[1]", "MW0048: Map[1]" }, found);
    }

    [Fact]
    public void reading_a_shared_collection_inside_is_allowed()
    {
        Assert.Empty(MapCode.Check("""
            Local.For(p, () =>
            {
                var n = Scores.Count + Scores.IndexOf(1) + Scores[0] + Keys[1] + Cells[0] + Map[1] + Scores.Sum();
                var has = Scores.Contains(1) && Keys.ContainsKey(1) && Seen.Contains(1) && Map.ContainsKey(1) && Set.Contains(1);
                var text = Log.ToString();
                foreach (var score in Scores)
                {
                }
            });
            """, Shared));
    }

    [Fact]
    public void reading_backwards_and_a_loop_over_a_library_collection_change_nothing()
    {
        // The map's own enumerator runs only when a GetEnumerator of the map's may: a List's never does.
        Assert.Empty(MapCode.Check("""
            Local.For(p, () =>
            {
                foreach (var n in Sorted.Reverse()) { }
                foreach (var n in Numbers) { }
                Scores.Reverse<int>();
            });
            """, Shared + """
            public static SortedSet<int> Sorted = new SortedSet<int>();
            public static IEnumerable<int> Numbers = new List<int>();

            public sealed class Counter : IEnumerator<int>
            {
                private int index;
                public int Current => index;
                object System.Collections.IEnumerator.Current => index;
                public bool MoveNext() { index++; return index < 3; }
                public void Reset() { index = 0; }
                public void Dispose() { }
            }
            """));
    }

    [Fact]
    public void the_answer_does_not_depend_on_what_was_followed_first()
    {
        // A local function's or lambda's write to a local around it is shared or not by where it runs (round 1's D6).
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(new[] { "MW0048: Bump", "MW0048: add" }, MapCode.Check("""
                Local.For(p, () => Helper(p));
                Local.For(p, () => Other(p));
                """, """
                static void Helper(Player q) { int shown = 0; void Bump() => shown++; Local.For(q, Bump); q.Print(shown.ToString()); }
                static void Other(Player q) { int count = 0; Action add = () => count++; Local.For(q, add); add(); }
                """));
        }
    }

    [Fact]
    public void a_collection_made_in_the_block_is_free()
    {
        Assert.Empty(MapCode.Check("""
            Local.For(p, () =>
            {
                var mine = new List<int>();
                mine.Add(1);
                mine[0] = 2;
                var cells = new int[4];
                cells[0] = 1;
                Array.Sort(cells);
                var map = new OrderedMap<int, int>();
                map[1] = 2;
            });
            """));
    }

    [Fact]
    public void a_copy_in_a_local_that_holds_nothing_else_is_followed()
    {
        var found = MapCode.Check("""
            Local.For(p, () =>
            {
                var copy = Scores;
                copy.Add(1);
                var again = copy;
                again.Clear();
            });
            """, Shared);

        Assert.Equal(new[] { "MW0048: copy", "MW0048: again" }, found);
    }

    [Fact]
    public void a_list_assigned_later_passed_as_a_parameter_or_returned_is_not_seen()
    {
        // The rule's limits: it reads each write where it is, and keeps no track of what a value may hold.
        Assert.Empty(MapCode.Check("""
            Local.For(p, () =>
            {
                var mine = new List<int>();
                mine = Scores;
                mine.Add(1);
                Fill(Scores);
                Listed.Add(1);
                Handed().Add(1);
            });
            """, Shared + """
                public static List<int> Listed => Scores;
                public static List<int> Handed() => Scores;
                public static void Fill(List<int> list) { list.Add(1); }
            """));
    }

    [Fact]
    public void a_call_on_a_property_with_a_body_lands_in_the_object_it_is_read_on()
    {
        Assert.Equal(new[] { "MW0048: Main.Bag" }, MapCode.Check("""
            Local.For(p, () => Main.Bag.Add(1));
            """, """
                public static Panel Main = new Panel();

                public sealed class Panel
                {
                    private readonly List<int> items = new List<int>();
                    public List<int> Bag => items;
                }
            """));
    }

    [Fact]
    public void a_property_with_a_setter_of_its_own_is_followed_and_not_taken_for_a_value()
    {
        Assert.Equal(new[] { "MW0001: Held.Name" }, MapCode.Check("""
            Local.For(p, () =>
            {
                Main.Quiet = 1;
                Held.Name = "Guard";
            });
            """, """
                public static Panel Main = new Panel();
                public static Unit Held = null!;

                public sealed class Panel
                {
                    public int Quiet { get => 0; set { } }
                }
            """));
    }

    [Fact]
    public void a_setter_writes_its_own_value()
    {
        Assert.Empty(MapCode.Check("Local.For(p, () => { Total = 1; });", """
                public static int Total { get => 0; set { value++; } }
            """));
    }

    [Fact]
    public void a_field_of_a_struct_lands_in_what_holds_the_struct()
    {
        var found = MapCode.Check("""
            var outer = new Pt();
            Local.For(p, () =>
            {
                At.X = 1f;
                Main.At.X = 2f;
                outer.X = 3f;
            });
            """, Shared);

        Assert.Equal(new[] { "MW0048: At.X", "MW0048: Main.At.X", "MW0048: outer.X" }, found);
    }

    [Fact]
    public void a_struct_in_a_local_of_the_block_is_free()
    {
        Assert.Empty(MapCode.Check("""
            Local.For(p, () =>
            {
                var at = At;
                at.X = 1f;
                var made = new Pt();
                made.X = 2f;
            });
            """, Shared));
    }

    [Fact]
    public void a_constructor_writes_its_own_object_and_a_static_constructor_its_own_class()
    {
        Assert.Empty(MapCode.Check("Local.For(p, () => { var made = new Made(); });", """
                public class Root
                {
                    protected int Inherited;
                }

                public sealed class Made : Root
                {
                    public static int Total;
                    public int Count;
                    public int Size { get; set; }
                    public List<int> Items = new List<int>();

                    static Made() { Total = 0; }

                    public Made()
                    {
                        Count = 1;
                        this.Size = 2;
                        Inherited = 3;
                        Items.Add(4);
                    }
                }
            """));
    }

    [Fact]
    public void a_constructor_writing_anything_else_is_rejected()
    {
        var found = MapCode.Check(
            """
            var one = new Panel();
            Local.For(p, () => { var made = new Copy(one); });
            Local.For(p, () => { var made = new Later(); });
            Local.For(p, () => { var made = Touched.Value; });
            """,
            Shared + """
                public sealed class Copy
                {
                    public Copy(Panel from) { from.Count = 1; }
                }

                public sealed class Later
                {
                    public int Count;
                    public Action Bump;
                    public Later() { Bump = () => Count++; }
                }

                public static class Touched
                {
                    public static int Value;
                    static Touched() { Score = 1; }
                }
            """);

        Assert.Equal(new[] { "MW0048: new Copy(one)", "MW0048: new Later()", "MW0048: Touched.Value" }, found);
    }

    [Fact]
    public void an_object_made_in_the_block_is_free()
    {
        Assert.Empty(MapCode.Check("""
            Local.For(p, () =>
            {
                var panel = new Panel();
                panel.Count = 1;
                panel.Size = 2;
                panel.At.X = 3f;
                panel.Items.Add(4);
                panel.Done += Show;
                var named = new Panel { Count = 5, Size = 6 };
                var changed = new Stats(7) with { Hits = 8 };
            });
            """, Shared + """
                public sealed record Stats(int Hits);
            """));
    }

    [Fact]
    public void an_object_from_anywhere_else_is_shared()
    {
        var found = MapCode.Check("""
            var outer = new Panel();
            Local.For(p, () =>
            {
                var panel = new Panel();
                panel = Main;
                panel.Count = 1;
                outer.Count = 2;
                Find().Count = 3;
                foreach (var item in new[] { Main })
                {
                    item.Count = 4;
                }
            });
            """, Shared);

        Assert.Equal(new[] { "MW0048: panel.Count", "MW0048: outer.Count", "MW0048: Find().Count", "MW0048: item.Count" }, found);
    }

    [Fact]
    public void a_method_called_on_a_new_object_is_still_checked()
    {
        // The agreed false alarm: a method is checked once, and not for each object it runs on.
        Assert.Equal(new[] { "MW0048: panel.Fill()" }, MapCode.Check("""
            Local.For(p, () =>
            {
                var panel = new Panel();
                panel.Fill();
            });
            """, Shared));
    }

    [Fact]
    public void adding_to_a_map_event_is_a_write()
    {
        var found = MapCode.Check("""
            Local.For(p, () =>
            {
                Changed += Show;
                Changed -= Show;
                Main.Done += Show;
            });
            """, Shared);

        Assert.Equal(new[] { "MW0048: Changed", "MW0048: Changed", "MW0048: Main.Done" }, found);
    }

    [Fact]
    public void sync_unchecked_on_the_block_silences_it_and_a_followed_method_is_still_followed()
    {
        var found = MapCode.Check(
            """
            Local.For(p, [SyncUnchecked] () =>
            {
                Score = 1;
                Scores.Add(1);
                Bump();
            });
            Local.For(p, () => Trusted());
            """,
            Shared + """
                [SyncUnchecked]
                private static void Trusted() { Score = 1; }
            """);

        Assert.Equal(new[] { "MW0048: Trusted()" }, found);
    }

    [Fact]
    public void the_message_says_what_is_written_and_what_to_do()
    {
        var what = "; inside Local.For that happens on one machine only, so code every machine runs would see a different value, which desyncs the game; ";

        Assert.Equal("'Code.Score' is changed" + what + "mark it [MachineLocal] if only code of one machine reads it",
            MapCode.Message("Local.For(p, () => { Score = 1; });", "public static int Score;"));
        Assert.Equal("'Code.Scores' is changed" + what + "mark it [MachineLocal] if only code of one machine reads it",
            MapCode.Message("Local.For(p, () => { Scores.Add(1); });", "public static List<int> Scores = new List<int>();"));
        Assert.Equal("'n' is changed" + what + "declare it inside the block, or use a [MachineLocal] field",
            MapCode.Message("var n = 0; Local.For(p, () => { n = 1; });"));
        Assert.Equal("'Code.Changed' is changed" + what + "an event cannot be marked: add to it outside the block",
            MapCode.Message("Local.For(p, () => { Changed += () => { }; });", "public static event Action? Changed;"));
    }

    [Fact]
    public void the_message_names_the_path_to_a_followed_write()
    {
        var what = "; inside Local.For that happens on one machine only, so code every machine runs would see a different value, which desyncs the game; ";

        Assert.Equal("'Code.Score' is changed (through Code.Bump > Code.Score)" + what + "mark it [MachineLocal] if only code of one machine reads it",
            MapCode.Message("Local.For(p, () => Bump());", """
                public static int Score;
                public static void Bump() { Score++; }
            """));
    }

    [Fact]
    public void a_call_through_two_conditional_accesses_ends_and_lands_in_the_first_shared_field()
    {
        var found = MapCode.Check("""
            Local.For(p, () => Holder?.Items?.Add(1));
            """, """
                public static Panel? Holder;
                public sealed class Panel { public List<int>? Items; }
            """);

        Assert.Equal(new[] { "MW0048: .Items" }, found);
    }

    [Fact]
    public void a_write_through_a_ref_local_lands_in_what_it_refers_to()
    {
        var found = MapCode.Check("""
            var outside = 0;
            Local.For(p, () =>
            {
                ref int r = ref Score;
                r++;
                r = 5;
                ref Pt v = ref At;
                v.X = 1;
                ref List<int> l = ref Scores;
                l = new List<int>();
                ref int c = ref Cells[0];
                c = 1;
                ref int o = ref outside;
                o = 1;
                var mine = 0;
                ref int m = ref mine;
                m++;
            });
            """, Shared);

        Assert.Equal(new[] { "MW0048: r", "MW0048: r", "MW0048: v.X", "MW0048: l", "MW0048: c", "MW0048: o" }, found);
        Assert.StartsWith("'Code.Score' is changed", MapCode.Message("Local.For(p, () => { ref int r = ref Score; r++; });", "public static int Score;"));
    }

    [Fact]
    public void a_constructor_writing_what_its_field_points_to_writes_it_unless_it_made_it()
    {
        var found = MapCode.Check("""
            Local.For(p, () => new Marker(Kept));
            Local.For(p, () => new Owner());
            """, """
            public static Tally Kept = new Tally();
            public sealed class Tally { public int Count; }
            public sealed class Marker { private readonly Tally tally; public Marker(Tally t) { tally = t; tally.Count++; } }
            public sealed class Owner { private readonly Tally tally; public Owner() { tally = new Tally(); tally.Count++; } }
            """);

        Assert.Equal(new[] { "MW0048: new Marker(Kept)" }, found);
    }

    [Fact]
    public void an_element_set_from_the_end_is_written()
    {
        var found = MapCode.Check("""
            Local.For(p, () =>
            {
                Scores[^1] = 1;
                Cells[^1] = 1;
                var mine = new List<int> { 1 };
                mine[^1] = 2;
            });
            """, Shared);

        Assert.Equal(new[] { "MW0048: Scores[^1]", "MW0048: Cells[^1]" }, found);
    }

    [Fact]
    public void the_arrays_a_call_fills_are_written()
    {
        var found = MapCode.Check("""
            Local.For(p, () =>
            {
                Keys.TryAdd(1, 1);
                Waiting.EnqueueRange(new[] { (1, 1) });
                Array.Sort(Cells, Order);
                Scores.CopyTo(Cells);
                Rng.Shuffle(Cells);
                var mine = new int[4];
                Scores.CopyTo(mine);
            });
            """, Shared + """
            public static PriorityQueue<int, int> Waiting = new PriorityQueue<int, int>();
            public static int[] Order = new int[4];
            """);

        Assert.Equal(new[]
        {
            "MW0048: Keys", "MW0048: Waiting", "MW0048: Cells", "MW0048: Order", "MW0048: Cells", "MW0048: Rng", "MW0048: Cells",
        }, found);
    }

    [Fact]
    public void a_local_given_a_shared_list_once_by_deconstruction_or_assignment_is_followed()
    {
        var found = MapCode.Check(
            """
            Local.For(p, () =>
            {
                List<int> s;
                int n;
                (s, n) = (Scores, 1);
                s.Add(1);
                var (t, m) = (Scores, 2);
                t.Add(2);
                List<int> later;
                later = Scores;
                later.Add(3);
                var own = new List<int>();
                (own, n) = (new List<int>(), 3);
                own.Add(4);
            });
            """, Shared);

        Assert.Equal(new[] { "MW0048: s", "MW0048: t", "MW0048: later" }, found);
    }
}
