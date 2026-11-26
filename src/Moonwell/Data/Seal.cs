using System.Collections.Generic;
using System.Text;

namespace Moonwell
{
    /// <summary>
    /// Seals bytes under the map's SaveKey (decision 398): a check of 8 bytes, the start of the HMAC-SHA256 of what the
    /// bytes are (a kind, an id and the player's name) and the bytes themselves, and then the bytes mixed with ChaCha20 under
    /// that check as the nonce. So the same bytes seal the same way on every machine, with no random number, and a changed
    /// byte, another key, id or name fails the check. The key is in the map's script, so this stops whoever has not read it
    /// there, and no one who has.
    /// </summary>
    internal static class Seal
    {
        /// <summary>The check's length in bytes.</summary>
        internal const int CheckBytes = 8;

        private const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

        private static string? keyed;
        private static List<int>? checkKey;
        private static int[]? cipherKey;

        /// <summary>Whether the map has a SaveKey to seal under.</summary>
        internal static bool HasKey => SaveGame.Key.Length > 0;

        /// <summary>The check and the mixed bytes.</summary>
        internal static List<int> Close(string kind, string id, string name, List<int> bytes)
        {
            Keys();
            var check = Check(kind, id, name, bytes, 0);
            var closed = new List<int>(check);
            closed.AddRange(bytes);
            ChaCha20.Mix(cipherKey!, Nonce(closed), 0, closed, CheckBytes);
            return closed;
        }

        /// <summary>The bytes a <see cref="Close"/> sealed; null when the check fails.</summary>
        internal static List<int>? Open(string kind, string id, string name, List<int> closed)
        {
            if (closed.Count < CheckBytes)
            {
                return null;
            }

            Keys();
            var opened = new List<int>(closed);
            ChaCha20.Mix(cipherKey!, Nonce(closed), 0, opened, CheckBytes);
            var check = Check(kind, id, name, opened, CheckBytes);
            for (var i = 0; i < CheckBytes; i++)
            {
                if (check[i] != closed[i])
                {
                    return null;
                }
            }

            opened.RemoveRange(0, CheckBytes);
            return opened;
        }

        /// <summary>Bytes as Base64 letters, which a player's file keeps as they are.</summary>
        internal static string ToLetters(List<int> bytes)
        {
            var text = new StringBuilder();
            for (var i = 0; i < bytes.Count; i += 3)
            {
                var left = bytes.Count - i;
                var three = (bytes[i] << 16) | ((left > 1 ? bytes[i + 1] : 0) << 8) | (left > 2 ? bytes[i + 2] : 0);
                text.Append(Letters[(three >> 18) & 63]).Append(Letters[(three >> 12) & 63]);
                text.Append(left > 1 ? Letters[(three >> 6) & 63] : '=').Append(left > 2 ? Letters[three & 63] : '=');
            }

            return text.ToString();
        }

        /// <summary>The bytes of Base64 letters; null for text that is none.</summary>
        internal static List<int>? FromLetters(string text)
        {
            if (text.Length % 4 != 0)
            {
                return null;
            }

            var bytes = new List<int>();
            for (var i = 0; i < text.Length; i += 4)
            {
                var last = i + 4 == text.Length;
                var pad = last && text[i + 3] == '=' ? (text[i + 2] == '=' ? 2 : 1) : 0;
                var four = 0;
                for (var j = 0; j < 4; j++)
                {
                    var value = j >= 4 - pad ? 0 : Letters.IndexOf(text[i + j]);
                    if (value < 0)
                    {
                        return null;
                    }

                    four = (four << 6) | value;
                }

                bytes.Add((four >> 16) & 255);
                if (pad < 2)
                {
                    bytes.Add((four >> 8) & 255);
                }

                if (pad < 1)
                {
                    bytes.Add(four & 255);
                }
            }

            return bytes;
        }

        /// <summary>A file or save that fails its check is written to the log in every build, so "no file" and "changed" can be told apart.</summary>
        internal static void Failed(string label, string message)
        {
#if DEBUG
            Dev.Report(label, message);
#else
            Runtime.World.Log("Moonwell: " + label + ": " + message);
#endif
        }

        /// <summary>Forgets the keys; Runtime.Reset calls it.</summary>
        internal static void Reset()
        {
            keyed = null;
            checkKey = null;
            cipherKey = null;
        }

        // The two keys, each the keyed hash of a label under the SaveKey, made once for a key.
        private static void Keys()
        {
            if (keyed == SaveGame.Key && checkKey != null)
            {
                return;
            }

            keyed = SaveGame.Key;
            var key = Utf8.Encode(SaveGame.Key);
            checkKey = Sha256.Hmac(key, Utf8.Encode("Moonwell seal: check"));
            var cipher = Sha256.Hmac(key, Utf8.Encode("Moonwell seal: cipher"));
            cipherKey = new int[8];
            for (var i = 0; i < 8; i++)
            {
                cipherKey[i] = Word(cipher, i * 4);
            }
        }

        private static List<int> Check(string kind, string id, string name, List<int> bytes, int start)
        {
            var message = new List<int>();
            AddText(message, kind);
            AddText(message, id);
            AddText(message, name);
            for (var i = start; i < bytes.Count; i++)
            {
                message.Add(bytes[i]);
            }

            var hash = Sha256.Hmac(checkKey!, message);
            hash.RemoveRange(CheckBytes, hash.Count - CheckBytes);
            return hash;
        }

        // A text as its length and its bytes, so that two texts one after another cannot be read as two others.
        private static void AddText(List<int> message, string text)
        {
            var bytes = Utf8.Encode(text);
            message.Add((bytes.Count >> 8) & 255);
            message.Add(bytes.Count & 255);
            message.AddRange(bytes);
        }

        private static int[] Nonce(List<int> check)
        {
            return new[] { Word(check, 0), Word(check, 4), 0 };
        }

        private static int Word(List<int> bytes, int at)
        {
            return bytes[at] | (bytes[at + 1] << 8) | (bytes[at + 2] << 16) | (bytes[at + 3] << 24);
        }
    }
}
