using System.Collections.Generic;
using System.Text;

namespace Moonwell.Testing
{
    /// <summary>Cases for saves (decisions 180 to 184): the code, its check, the file and slots. The classes register by hand, as the build's code does.</summary>
    public static class SaveCases
    {
        /// <summary>A save of every field type, with the awkward values, reads back equal.</summary>
        public static void RoundTrip(FakeWorld world)
        {
            var save = Filled();
            Register(world);
            SaveGame.Save(Player.One, save);
            ExpectSame(save, SaveGame.FromCode<CaseSave>(Player.One, save.Code));
        }

        /// <summary>A code typed in lower case, without dashes, with spaces, and with O for 0 and I or L for 1, reads back.</summary>
        public static void TypingTolerated(FakeWorld world)
        {
            var save = Filled();
            Register(world);
            SaveGame.Save(Player.One, save);
            var typed = save.Code.Replace("-", " ").ToLower().Replace("0", "o").Replace("1", "l");
            ExpectSame(save, SaveGame.FromCode<CaseSave>(Player.One, typed));
            ExpectSame(save, SaveGame.FromCode<CaseSave>(Player.One, save.Code.Replace("-", "").Replace("1", "I")));
        }

        /// <summary>One changed letter, a cut-off code and an empty or wrong text give null, never an exception.</summary>
        public static void BadCodes(FakeWorld world)
        {
            var save = Filled();
            Register(world);
            SaveGame.Save(Player.One, save);
            var code = save.Code;
            var at = code.Length / 2;
            while (code[at] == '-')
            {
                at++;
            }

            var changed = code.Substring(0, at) + (code[at] == 'A' ? 'B' : 'A') + code.Substring(at + 1);
            Expect.True(SaveGame.FromCode<CaseSave>(Player.One, changed) == null, "a changed letter");
            Expect.True(SaveGame.FromCode<CaseSave>(Player.One, code.Substring(0, code.Length - 3)) == null, "cut off");
            Expect.True(SaveGame.FromCode<CaseSave>(Player.One, "") == null, "empty");
            Expect.True(SaveGame.FromCode<CaseSave>(Player.One, "hello, world") == null, "no code");
            Expect.True(SaveGame.FromCode<CaseSave>(Player.One, "0000-0000-00") == null, "zeros");
        }

        /// <summary>A code made for one player fails for another, as the check covers the name at the start.</summary>
        public static void OtherName(FakeWorld world)
        {
            Register(world);
            var save = new CaseSave { Gold = 5 };
            SaveGame.Save(Player.One, save);
            Expect.True(SaveGame.FromCode<CaseSave>(Player.Two, save.Code) == null, "player two");
            Player.One.Name = "Renamed";
            Expect.True(SaveGame.FromCode<CaseSave>(Player.One, save.Code) != null, "the name at the start counts, not a new one");
        }

        /// <summary>A code of one save class fails for a class with another id.</summary>
        public static void OtherClass(FakeWorld world)
        {
            Register(world);
            var save = new CaseSave { Gold = 5 };
            SaveGame.Save(Player.One, save);
            Expect.True(SaveGame.FromCode<CaseSettings>(Player.One, save.Code) == null, "another class");
            var settings = new CaseSettings { Volume = 3 };
            SaveGame.Save(Player.One, settings);
            Expect.Equal(3, SaveGame.FromCode<CaseSettings>(Player.One, settings.Code)!.Volume, "its own class");
        }

        /// <summary>A code made under another SaveKey fails.</summary>
        public static void OtherKey(FakeWorld world)
        {
            Register(world);
            var save = new CaseSave { Gold = 5 };
            SaveGame.Save(Player.One, save);
            SaveGame.Key = "another key";
            Expect.True(SaveGame.FromCode<CaseSave>(Player.One, save.Code) == null, "another key");
        }

        /// <summary>A field whose type changed reads as its default; a field the class lacks is skipped; one the save lacks keeps its default.</summary>
        public static void TypeChanged(FakeWorld world)
        {
            Register(world);
            var old = new OldShape { Kept = 7, Changed = "text", Gone = 9 };
            SaveGame.Save(Player.One, old);
            var read = SaveGame.FromCode<NewShape>(Player.One, old.Code)!;
            Expect.Equal(7, read.Kept, "a field kept");
            Expect.Equal(1, read.Changed.Count, "a field whose type changed keeps its default");
            Expect.Equal(5, read.Added, "a field the save lacks keeps its default");
        }

        /// <summary>Saves that differ by one in a number differ in most of their letters, not only the last ones.</summary>
        public static void NoPlainNumbers(FakeWorld world)
        {
            Register(world);
            var a = new CaseSave { Gold = 9999 };
            var b = new CaseSave { Gold = 9998 };
            SaveGame.Save(Player.One, a);
            SaveGame.Save(Player.One, b);
            Expect.Equal(a.Code.Length, b.Code.Length, "the lengths");
            var differ = 0;
            for (var i = 0; i < a.Code.Length; i++)
            {
                differ += a.Code[i] != b.Code[i] ? 1 : 0;
            }

            Expect.True(a.Code[0] != b.Code[0] || a.Code[1] != b.Code[1], "the first letters differ: " + a.Code + " " + b.Code);
            Expect.True(differ * 2 > a.Code.Length, "most letters differ: " + a.Code + " " + b.Code);
        }

        /// <summary>An object id, which would take five bytes, takes four: the check, the format, the tag and the id.</summary>
        public static void IdsTakeFour(FakeWorld world)
        {
            Register(world);
            var save = new CaseSettings { Volume = ObjectTypes.FourCC("I000") };
            SaveGame.Save(Player.One, save);
            Expect.Equal(4 + 1 + 1 + 4, CodeLetters.Decode(save.Code)!.Count, "the bytes");
            Expect.Equal(ObjectTypes.FourCC("I000"), SaveGame.FromCode<CaseSettings>(Player.One, save.Code)!.Volume, "read back");
        }

        /// <summary>A code keeps its dashes when it still fits a chat line with them, and leaves them out when it does not.</summary>
        public static void Dashes(FakeWorld world)
        {
            Register(world);
            var small = new CaseSettings { Volume = 1 };
            SaveGame.Save(Player.One, small);
            Expect.True(small.Code.Contains("-"), "a short code has dashes: " + small.Code);

            // 62 bytes are 100 letters: 124 with dashes, past the 121 a chat line holds after "-load ".
            var longer = new CaseSettings { Note = Letters(53) };
            SaveGame.Save(Player.One, longer);
            Expect.True(!longer.Code.Contains("-") && longer.Code.Length == 100, "no dashes: " + longer.Code);
            Expect.Equal(longer.Note, SaveGame.FromCode<CaseSettings>(Player.One, longer.Code)!.Note, "read back");
            Expect.Equal(0, world.Logged.Count, "nothing reported");
        }

#if DEBUG
        /// <summary>A save whose code no chat line holds is reported in a development build.</summary>
        public static void TooLongReported(FakeWorld world)
        {
            Register(world);
            var save = new CaseSettings { Note = Letters(80) };
            SaveGame.Save(Player.One, save);
            Expect.Equal(1, world.Logged.Count, "reported");
            Expect.True(world.Logged[0].Contains("chat line"), "the report: " + world.Logged[0]);
            Expect.Equal(save.Note, SaveGame.FromCode<CaseSettings>(Player.One, save.Code)!.Note, "it still reads back");
        }
#endif

        /// <summary>A save written to its file loads back through a scope's Load, after the file has gone round.</summary>
        public static void LoadFromFile(FakeWorld world)
        {
            var save = Filled();
            Register(world);
            SaveGame.Save(Player.One, save);
            Expect.True(world.Files.ContainsKey("Map\\save.pld"), "the default slot's file");
            CaseSave? got = null;
            var ran = false;
            Game.Instance.Load<CaseSave>(Player.One, loaded =>
            {
                got = loaded;
                ran = true;
            });
            Expect.True(!ran, "not before the file goes round");
            world.Advance(0.1f);
            Expect.True(ran, "loaded");
            ExpectSame(save, got);
        }

        /// <summary>No file loads as null, with nothing logged.</summary>
        public static void LoadMissing(FakeWorld world)
        {
            Register(world);
            var ran = false;
            CaseSave? got = new CaseSave();
            Game.Instance.Load<CaseSave>(Player.One, loaded =>
            {
                got = loaded;
                ran = true;
            });
            world.Advance(0.1f);
            Expect.True(ran && got == null, "null");
            Expect.Equal(0, world.Logged.Count, "nothing logged");
        }

        /// <summary>A file changed by hand loads as null and is written to the log, so it can be told from no file.</summary>
        public static void LoadEdited(FakeWorld world)
        {
            Register(world);
            var save = new CaseSave { Gold = 100 };
            SaveGame.Save(Player.One, save);
            var pieces = world.Files["Map\\save.pld"];
            pieces[0] = pieces[0].Substring(0, 6) + (pieces[0][6] == 'A' ? 'B' : 'A') + pieces[0].Substring(7);
            var ran = false;
            CaseSave? got = new CaseSave();
            Game.Instance.Load<CaseSave>(Player.One, loaded =>
            {
                got = loaded;
                ran = true;
            });
            world.Advance(0.1f);
            Expect.True(ran && got == null, "null");
            Expect.Equal(1, world.Logged.Count, "one line in the log");
            Expect.True(world.Logged[0].Contains("save.pld") && world.Logged[0].Contains("fails its check"), "the line: " + world.Logged[0]);
        }

        /// <summary>Named slots keep apart, each in a file of its own.</summary>
        public static void Slots(FakeWorld world)
        {
            Register(world);
            SaveGame.Save(Player.One, new CaseSave { Gold = 1 }, "paladin");
            SaveGame.Save(Player.One, new CaseSave { Gold = 2 }, "mage");
            Expect.True(world.Files.ContainsKey("Map\\paladin.pld") && world.Files.ContainsKey("Map\\mage.pld"), "two files");
            var gold = new List<int>();
            Game.Instance.Load<CaseSave>(Player.One, "paladin", loaded => gold.Add(loaded!.Gold));
            Game.Instance.Load<CaseSave>(Player.One, "mage", loaded => gold.Add(loaded!.Gold));
            world.Advance(0.1f);
            Expect.Equal(2, gold.Count, "both loaded");
            Expect.Equal(1, gold[0], "paladin");
            Expect.Equal(2, gold[1], "mage");
        }

        /// <summary>A slot of other letters is reported in a development build: nothing is written, and its load gives null in the next instant.</summary>
        public static void BadSlot(FakeWorld world)
        {
            Register(world);
            var save = new CaseSave { Gold = 1 };
            SaveGame.Save(Player.One, save, "a.b");
            Expect.Equal(0, world.Files.Count, "nothing written");
            Expect.True(save.Code.Length > 0, "the code is still given");
            var ran = false;
            Game.Instance.Load<CaseSave>(Player.One, "a/b", loaded => ran = loaded == null);
            world.Advance(0.1f);
            Expect.True(ran, "null");
#if DEBUG
            Expect.Equal(2, world.Logged.Count, "both reported");
            Expect.True(world.Logged[0].Contains("\"a.b\" is no slot") && world.Logged[1].Contains("\"a/b\" is no slot"), "the reports: " + world.Logged[0] + " | " + world.Logged[1]);
#endif
        }

        /// <summary>A load whose scope ends before the file arrives runs nothing.</summary>
        public static void LoadScopeEnded(FakeWorld world)
        {
            Register(world);
            SaveGame.Save(Player.One, new CaseSave { Gold = 1 });
            var scope = new TestScope("scope", new List<string>());
            Game.Instance.Adopt(scope);
            var ran = false;
            scope.Load<CaseSave>(Player.One, loaded => ran = true);
            scope.End();
            world.Advance(0.1f);
            Expect.True(!ran, "no callback");
        }

        private static void Register(FakeWorld world)
        {
            world.SetSlot(0, SlotState.Playing, Controller.User);
            world.SetSlot(1, SlotState.Playing, Controller.User);
            SaveGame.Key = "the case key";
            SaveGame.Register<CaseSave>("", () => new CaseSave(), (save, writer) =>
            {
                writer.Int(1, save.Gold);
                writer.String(2, save.Name);
                writer.IntList(3, save.Items);
                writer.Float(4, save.Speed);
                writer.Bool(5, save.Alive);
                writer.Time(6, save.Claimed);
                writer.Part(7, save.Hero, WritePart);
                writer.StringList(8, save.Tags);
                writer.PartList(9, save.Party, WritePart);
                writer.Int(10, (int)save.Kind);
                writer.FloatList(11, save.Floats);
                writer.BoolList(12, save.Flags);
                writer.TimeList(13, save.Times);
                writer.String(14, save.Empty);
                writer.Int(15, save.Lowest);
                writer.Int(16, save.Highest);
                writer.Float(17, save.Huge);
                writer.IntList(18, save.Ids);
                var kinds = new List<int>();
                foreach (var kind in save.Kinds)
                {
                    kinds.Add((int)kind);
                }

                writer.IntList(19, kinds);
                writer.String(20, save.Nothing);
            }, (save, reader) =>
            {
                while (reader.Next())
                {
                    switch (reader.Number)
                    {
                        case 1: save.Gold = reader.Int(save.Gold); break;
                        case 2: save.Name = reader.String(save.Name); break;
                        case 3: save.Items = reader.IntList() ?? save.Items; break;
                        case 4: save.Speed = reader.Float(save.Speed); break;
                        case 5: save.Alive = reader.Bool(save.Alive); break;
                        case 6: save.Claimed = reader.Time(save.Claimed); break;
                        case 7: save.Hero = reader.Part(save.Hero, () => new CasePart(), ReadPart); break;
                        case 8: save.Tags = reader.StringList()?.ToArray() ?? save.Tags; break;
                        case 9: save.Party = reader.PartList(() => new CasePart(), ReadPart) ?? save.Party; break;
                        case 10: save.Kind = (CaseKind)reader.Int((int)save.Kind); break;
                        case 11: save.Floats = reader.FloatList() ?? save.Floats; break;
                        case 12: save.Flags = reader.BoolList() ?? save.Flags; break;
                        case 13: save.Times = reader.TimeList() ?? save.Times; break;
                        case 14: save.Empty = reader.String(save.Empty)!; break;
                        case 15: save.Lowest = reader.Int(save.Lowest); break;
                        case 16: save.Highest = reader.Int(save.Highest); break;
                        case 17: save.Huge = reader.Float(save.Huge); break;
                        case 18: save.Ids = reader.IntList() ?? save.Ids; break;
                        case 19:
                            var kinds = reader.IntList();
                            if (kinds != null)
                            {
                                var read = new List<CaseKind>();
                                foreach (var kind in kinds)
                                {
                                    read.Add((CaseKind)kind);
                                }

                                save.Kinds = read;
                            }

                            break;
                        case 20: save.Nothing = reader.String(save.Nothing); break;
                        default: reader.Skip(); break;
                    }
                }
            });
            SaveGame.Register<CaseSettings>("settings", () => new CaseSettings(), (save, writer) =>
            {
                writer.Int(1, save.Volume);
                writer.String(2, save.Note);
            }, (save, reader) =>
            {
                while (reader.Next())
                {
                    switch (reader.Number)
                    {
                        case 1: save.Volume = reader.Int(save.Volume); break;
                        case 2: save.Note = reader.String(save.Note); break;
                        default: reader.Skip(); break;
                    }
                }
            });
            SaveGame.Register<OldShape>("shape", () => new OldShape(), (save, writer) =>
            {
                writer.Int(1, save.Kept);
                writer.String(2, save.Changed);
                writer.Int(3, save.Gone);
            }, (save, reader) =>
            {
                while (reader.Next())
                {
                    reader.Skip();
                }
            });
            SaveGame.Register<NewShape>("shape", () => new NewShape(), (save, writer) =>
            {
            }, (save, reader) =>
            {
                while (reader.Next())
                {
                    switch (reader.Number)
                    {
                        case 1: save.Kept = reader.Int(save.Kept); break;
                        case 2: save.Changed = reader.IntList() ?? save.Changed; break;
                        case 4: save.Added = reader.Int(save.Added); break;
                        default: reader.Skip(); break;
                    }
                }
            });
        }

        private static void WritePart(CasePart part, SaveWriter writer)
        {
            writer.Int(1, part.Level);
            writer.String(2, part.Title);
        }

        private static void ReadPart(CasePart part, SaveReader reader)
        {
            while (reader.Next())
            {
                switch (reader.Number)
                {
                    case 1: part.Level = reader.Int(part.Level); break;
                    case 2: part.Title = reader.String(part.Title); break;
                    default: reader.Skip(); break;
                }
            }
        }

        private static CaseSave Filled()
        {
            return new CaseSave
            {
                Gold = 25,
                Name = "Åsa \"A\" | ~ \\",
                Items = new List<int>(),
                Speed = -0.5f,
                Alive = true,
                Claimed = new RealTime(0),
                Hero = new CasePart { Level = 3, Title = "" },
                Tags = new[] { "a", "", "中" },
                Party = new List<CasePart> { new CasePart { Level = 1, Title = "x" }, new CasePart { Level = -64, Title = "y" } },
                Kind = CaseKind.Second,
                // Floats a single holds exactly, as ByteCases has: the cases' desktop Lua has doubles, where 1e30f is not one.
                Floats = new List<float> { 1099511627776.0f, -0.0f, 0.25f },
                Flags = new List<bool> { true, false },
                Times = new List<RealTime> { new RealTime(1793664000), new RealTime(-1) },
                Empty = "",
                Lowest = int.MinValue,
                Highest = int.MaxValue,
                Huge = 1099511627776.0f,
                Ids = new List<int> { ObjectTypes.FourCC("I000"), ObjectTypes.FourCC("hfoo") },
                Kinds = new List<CaseKind> { CaseKind.Second, CaseKind.First },
            };
        }

        private static void ExpectSame(CaseSave want, CaseSave? got)
        {
            Expect.True(got != null, "read back");
            Expect.Equal(want.Gold, got!.Gold, "Gold");
            Expect.Equal(want.Name, got.Name, "Name");
            Expect.Equal(Joined(want.Items), Joined(got.Items), "Items");
            Expect.True(want.Speed == got.Speed, "Speed " + got.Speed);
            Expect.True(want.Alive == got.Alive, "Alive");
            Expect.Equal(want.Claimed.Seconds, got.Claimed.Seconds, "Claimed");
            Expect.True(got.Hero != null && got.Hero.Level == want.Hero!.Level && got.Hero.Title == want.Hero.Title, "Hero");
            Expect.Equal(string.Join(",", want.Tags), string.Join(",", got.Tags), "Tags");
            Expect.Equal(want.Party.Count, got.Party.Count, "Party");
            for (var i = 0; i < want.Party.Count; i++)
            {
                Expect.True(got.Party[i].Level == want.Party[i].Level && got.Party[i].Title == want.Party[i].Title, "Party " + i);
            }

            Expect.True(want.Kind == got.Kind, "Kind");
            Expect.Equal(want.Floats.Count, got.Floats.Count, "Floats");
            for (var i = 0; i < want.Floats.Count; i++)
            {
                // 1 / x tells -0 from 0.
                Expect.True(want.Floats[i] == got.Floats[i] && 1f / want.Floats[i] == 1f / got.Floats[i], "Floats " + i + ": " + got.Floats[i]);
            }

            Expect.True(want.Flags.Count == got.Flags.Count && want.Flags[0] == got.Flags[0] && want.Flags[1] == got.Flags[1], "Flags");
            Expect.True(want.Times.Count == got.Times.Count && want.Times[0] == got.Times[0] && want.Times[1] == got.Times[1], "Times");
            Expect.Equal(want.Empty, got.Empty, "Empty");
            Expect.True(got.Nothing == null, "Nothing");
            Expect.Equal(want.Lowest, got.Lowest, "Lowest");
            Expect.Equal(want.Highest, got.Highest, "Highest");
            Expect.True(want.Huge == got.Huge, "Huge");
            Expect.Equal(Joined(want.Ids), Joined(got.Ids), "Ids");
            Expect.True(got.Kinds.Count == 2 && got.Kinds[0] == CaseKind.Second && got.Kinds[1] == CaseKind.First, "Kinds");
        }

        private static string Joined(List<int> values)
        {
            var text = new StringBuilder();
            foreach (var value in values)
            {
                text.Append(value).Append(',');
            }

            return text.ToString();
        }

        private static string Letters(int count)
        {
            var text = new StringBuilder();
            for (var i = 0; i < count; i++)
            {
                text.Append('a');
            }

            return text.ToString();
        }
    }

    /// <summary>A save of every field type.</summary>
    public sealed class CaseSave : SaveGame
    {
        [Saved(1)] public int Gold;
        [Saved(2)] public string? Name;
        [Saved(3)] public List<int> Items = new List<int> { 99 };
        [Saved(4)] public float Speed;
        [Saved(5)] public bool Alive;
        [Saved(6)] public RealTime Claimed = new RealTime(5);
        [Saved(7)] public CasePart? Hero;
        [Saved(8)] public string[] Tags = new string[0];
        [Saved(9)] public List<CasePart> Party = new List<CasePart>();
        [Saved(10)] public CaseKind Kind;
        [Saved(11)] public List<float> Floats = new List<float>();
        [Saved(12)] public List<bool> Flags = new List<bool>();
        [Saved(13)] public List<RealTime> Times = new List<RealTime>();
        [Saved(14)] public string Empty = "x";
        [Saved(15)] public int Lowest;
        [Saved(16)] public int Highest;
        [Saved(17)] public float Huge;
        [Saved(18)] public List<int> Ids = new List<int>();
        [Saved(19)] public List<CaseKind> Kinds = new List<CaseKind>();
        [Saved(20)] public string? Nothing;
    }

    /// <summary>A part of a save.</summary>
    public sealed class CasePart
    {
        [Saved(1)] public int Level;
        [Saved(2)] public string? Title = "none";
    }

    /// <summary>A save class with an id of its own.</summary>
    [SaveId("settings")]
    public sealed class CaseSettings : SaveGame
    {
        [Saved(1)] public int Volume;
        [Saved(2)] public string? Note;
    }

    /// <summary>A save class before a change.</summary>
    [SaveId("shape")]
    public sealed class OldShape : SaveGame
    {
        [Saved(1)] public int Kept;
        [Saved(2)] public string Changed = "";
        [Saved(3)] public int Gone;
    }

    /// <summary>The same save class after its field 2 became a list, field 3 left and field 4 came.</summary>
    [SaveId("shape")]
    public sealed class NewShape : SaveGame
    {
        [Saved(1)] public int Kept;
        [Saved(2)] public List<int> Changed = new List<int> { 1 };
        [Saved(4)] public int Added = 5;
    }

    /// <summary>A kind a save keeps.</summary>
    public enum CaseKind
    {
        First,
        Second,
    }
}
