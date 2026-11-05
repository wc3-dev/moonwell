using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Moonwell
{
    /// <summary>
    /// A save the map writes, as Unreal's <c>USaveGame</c> (decisions 180 to 184): a class deriving from it, with
    /// <c>[Saved(n)]</c> on each field to keep. <see cref="Save"/> gives it a code the player can type and writes it to a file
    /// on their computer; a scope's <c>Load</c> reads the file back, and <see cref="FromCode{T}"/> a typed code. A save is tied
    /// to the player's name and the map's <c>SaveKey</c>, so it loads only for the player who made it.
    /// </summary>
    public abstract class SaveGame
    {
        /// <summary>The slot a save goes in when none is named.</summary>
        public const string DefaultSlot = "save";

        // A chat line holds 127 characters, and "-load " takes 6 (game facts, 11-03).
        private const int ChatLetters = 121;

        private const int Format = 1;

        private static Dictionary<Type, Codec>? codecs;

        // No initializer: a public type with static setup breaks a map's script in Lua (StaticSetupTests).
        private static string? key;

        /// <summary>The map's SaveKey, which every save's check covers; set as the runtime installs the map.</summary>
        internal static string Key
        {
            get { return key ?? ""; }
            set { key = value; }
        }

        /// <summary>The code of the save's last <see cref="Save"/>, which the player can type to load it on another computer; empty until then.</summary>
        [LocalSafe]
        public string Code { get; internal set; } = "";

        /// <summary>
        /// Saves for a player into a slot: sets <see cref="Code"/> on every machine, and writes the code to the slot's file on
        /// the player's computer. A slot is letters, digits, <c>-</c> and <c>_</c>.
        /// </summary>
        public static void Save(Player player, SaveGame save, string slot = DefaultSlot)
        {
            var codec = CodecOf(Names.TypeOf(save), "SaveGame.Save");
            if (codec == null)
            {
                return;
            }

            save.Code = Encode(codec, player, save);
            if (IsSlot(slot, "SaveGame.Save"))
            {
                PlayerFile.Write(player, slot + ".pld", save.Code);
            }
        }

        /// <summary>
        /// Reads a code the player typed, at once, as chat reaches every machine; null when it is no code of this class made for
        /// this player under the map's key. Lower case, spaces and missing dashes are read, and O as 0, I and L as 1.
        /// </summary>
        public static T? FromCode<T>(Player player, string text)
            where T : SaveGame, new()
        {
            var codec = CodecOf(typeof(T), "SaveGame.FromCode");
            return codec == null ? null : (T?)Decode(codec, player, text);
        }

        /// <summary>Registers a save class's id and the code that writes and reads it: the code the build writes for each save class calls it.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void Register<T>(string id, Func<T> make, Action<T, SaveWriter> write, Action<T, SaveReader> read)
            where T : SaveGame
        {
            if (codecs == null)
            {
                codecs = new Dictionary<Type, Codec>();
            }

            codecs[typeof(T)] = new Codec(id, () => make(), (save, writer) => write((T)save, writer), (save, reader) => read((T)save, reader));
        }

        /// <summary>Forgets the registered classes and the key; Runtime.Reset calls it.</summary>
        internal static void Reset()
        {
            codecs = null;
            key = null;
        }

        /// <summary>Reads a player's save from a slot's file on their computer, shares it, and hands it to every machine, as a child of the scope.</summary>
        internal static void Load<T>(Scope scope, Player player, string slot, Action<T?> arrived)
            where T : SaveGame
        {
            const string label = "Load";
            var codec = CodecOf(typeof(T), label);
            if (codec == null || !IsSlot(slot, label))
            {
                scope.OwnAfter(0.0f, () => arrived(null));
                return;
            }

            scope.ReadFile(player, slot + ".pld", text =>
            {
                if (text == null)
                {
                    arrived(null);
                    return;
                }

                var save = (T?)Decode(codec, player, text);
                if (save == null)
                {
                    Failed(label, "the file " + slot + ".pld of " + player.Name + " fails its check, so it gives null: it was changed, or made under another SaveKey, for another name or for another save class");
                }

                arrived(save);
            });
        }

        private static string Encode(Codec codec, Player player, SaveGame save)
        {
            var writer = new SaveWriter();
            writer.Bytes.Add(Format);
            codec.Write(save, writer);
            var check = SaveCheck.Of(Key, codec.Id, player.StartName, writer.Bytes, 0);
            var bytes = new List<int>(check);
            bytes.AddRange(writer.Bytes);
            SaveCheck.Mix(Key, check, bytes, 4);

            var dashed = CodeLetters.Encode(bytes);
            if (dashed.Length <= ChatLetters)
            {
                return dashed;
            }

            var plain = CodeLetters.Encode(bytes, false);
            if (plain.Length > ChatLetters)
            {
                Dev.Report("SaveGame.Save", Names.NameOf(save) + "'s code is " + plain.Length + " letters, more than the " + ChatLetters + " a chat line holds after \"-load \": it still loads from its file, but cannot be typed");
            }

            return plain;
        }

        private static SaveGame? Decode(Codec codec, Player player, string text)
        {
            var bytes = CodeLetters.Decode(text);
            if (bytes == null || bytes.Count < 5)
            {
                return null;
            }

            var check = new List<int> { bytes[0], bytes[1], bytes[2], bytes[3] };
            SaveCheck.Mix(Key, check, bytes, 4);
            var expected = SaveCheck.Of(Key, codec.Id, player.StartName, bytes, 4);
            for (var i = 0; i < 4; i++)
            {
                if (check[i] != expected[i])
                {
                    return null;
                }
            }

            if (bytes[4] != Format)
            {
                return null;
            }

            var save = codec.Make();
            try
            {
                codec.Read(save, new SaveReader(new ByteReader(bytes, 5, bytes.Count)));
            }
            catch (FormatException)
            {
                return null;
            }

            return save;
        }

        private static Codec? CodecOf(Type type, string label)
        {
            if (codecs != null && codecs.TryGetValue(type, out var codec))
            {
                return codec;
            }

            Dev.Report(label, type.Name + " has no save code: the build writes it for each class deriving from SaveGame in the map's code");
            return null;
        }

        private static bool IsSlot(string slot, string label)
        {
            var named = slot.Length > 0;
            for (var i = 0; named && i < slot.Length; i++)
            {
                var c = slot[i];
                named = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
            }

            if (!named)
            {
                Dev.Report(label, "\"" + slot + "\" is no slot: letters, digits, - and _");
            }

            return named;
        }

        // A file that fails its check is written to the log in every build, so "no file" and "changed" can be told apart.
        private static void Failed(string label, string message)
        {
#if DEBUG
            Dev.Report(label, message);
#else
            Runtime.World.Log("Moonwell: " + label + ": " + message);
#endif
        }

        private sealed class Codec
        {
            internal Codec(string id, Func<SaveGame> make, Action<SaveGame, SaveWriter> write, Action<SaveGame, SaveReader> read)
            {
                Id = id;
                Make = make;
                Write = write;
                Read = read;
            }

            internal string Id { get; }

            internal Func<SaveGame> Make { get; }

            internal Action<SaveGame, SaveWriter> Write { get; }

            internal Action<SaveGame, SaveReader> Read { get; }
        }
    }
}
