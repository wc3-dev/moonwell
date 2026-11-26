using System.Collections.Generic;
using System.Text;

namespace Moonwell.Testing
{
    /// <summary>
    /// Cases for the seal of saves and encrypted files (decision 398): SHA-256, HMAC and ChaCha20 against their published
    /// values, so .NET, the tests' Lua and the game's 32-bit integers are each held to them, and the seal itself.
    /// </summary>
    public static class SealCases
    {
        /// <summary>SHA-256 of the empty text, "abc" and a text of two blocks, as FIPS 180-4 gives them.</summary>
        public static void Sha256Published(FakeWorld world)
        {
            Expect.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", Hex(Sha256.Hash(Ascii(""))), "empty");
            Expect.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", Hex(Sha256.Hash(Ascii("abc"))), "abc");
            Expect.Equal("248d6a61d20638b8e5c026930c3e6039a33ce45964ff2167f6ecedd419db06c1", Hex(Sha256.Hash(Ascii("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq"))), "two blocks");
        }

        /// <summary>HMAC-SHA256 as RFC 4231 gives it: its cases 1 and 2, and case 6, a key longer than a block.</summary>
        public static void HmacPublished(FakeWorld world)
        {
            Expect.Equal("b0344c61d8db38535ca8afceaf0bf12b881dc200c9833da726e9376c2e32cff7", Hex(Sha256.Hmac(Repeated(0x0b, 20), Ascii("Hi There"))), "case 1");
            Expect.Equal("5bdcc146bf60754e6a042426089575c75a003f089d2739839dec58b964ec3843", Hex(Sha256.Hmac(Ascii("Jefe"), Ascii("what do ya want for nothing?"))), "case 2");
            Expect.Equal("60e431591ee0b67f0d8a26aacbf5b77f8e0bc6213728c5140546040f0ee37f54", Hex(Sha256.Hmac(Repeated(0xaa, 131), Ascii("Test Using Larger Than Block-Size Key - Hash Key First"))), "case 6");
        }

        /// <summary>ChaCha20 as RFC 8439 gives it: the block of section 2.3.2 and the text of section 2.4.2.</summary>
        public static void ChaChaPublished(FakeWorld world)
        {
            var key = new int[8];
            for (var i = 0; i < 8; i++)
            {
                key[i] = (i * 4) | (((i * 4) + 1) << 8) | (((i * 4) + 2) << 16) | (((i * 4) + 3) << 24);
            }

            var block = new int[16];
            ChaCha20.Block(block, key, new[] { 0x09000000, 0x4a000000, 0 }, 1);
            var bytes = new List<int>();
            for (var i = 0; i < 16; i++)
            {
                for (var shift = 0; shift < 32; shift += 8)
                {
                    bytes.Add((block[i] >> shift) & 255);
                }
            }

            Expect.Equal("10f1e7e4d13b5915500fdd1fa32071c4c7d1f4c733c068030422aa9ac3d46c4ed2826446079faa0914c2d705d98b02a2b5129cd1de164eb9cbd083e8a2503c4e", Hex(bytes), "the block");

            var text = Ascii("Ladies and Gentlemen of the class of '99: If I could offer you only one tip for the future, sunscreen would be it.");
            ChaCha20.Mix(key, new[] { 0, 0x4a000000, 0 }, 1, text, 0);
            Expect.Equal("6e2e359a2568f98041ba0728dd0d6981e97e7aec1d4360c20a27afccfd9fae0bf91b65c5524733ab8f593dabcd62b3571639d624e65152ab8f530c359f0861d807ca0dbf500d6a6156a38e088a22b65e52bc514d16ccf806818ce91ab77937365af90bbf74a35be6b40b8eedf2785e42874d", Hex(text), "the text");
        }

        /// <summary>Bytes of every length round a block's edge come back from their seal, which is 8 bytes longer and shows none of them.</summary>
        public static void RoundTrip(FakeWorld world)
        {
            SaveGame.Key = "the case key";
            foreach (var length in new[] { 0, 1, 55, 56, 63, 64, 65, 200 })
            {
                var bytes = Counted(length);
                var closed = Seal.Close("save", "id", "Åsa", bytes);
                Expect.Equal(length + Seal.CheckBytes, closed.Count, "the sealed length of " + length);
                Expect.Equal(Hex(closed), Hex(Seal.Close("save", "id", "Åsa", bytes)), "the same seal again, of " + length);
                var opened = Seal.Open("save", "id", "Åsa", closed);
                Expect.True(opened != null && Hex(opened) == Hex(bytes), "opened, of " + length);
            }

            var plain = Hex(Counted(200));
            var seal = Hex(Seal.Close("save", "id", "Åsa", Counted(200)));
            Expect.True(!seal.Contains(plain.Substring(0, 16)) && !seal.Contains(plain.Substring(200, 16)), "none of the bytes show");
        }

        /// <summary>A changed byte anywhere, a cut seal, and another kind, id, name or key each fail the check.</summary>
        public static void Refused(FakeWorld world)
        {
            SaveGame.Key = "the case key";
            var closed = Seal.Close("save", "id", "name", Counted(70));
            for (var at = 0; at < closed.Count; at += 7)
            {
                var changed = new List<int>(closed);
                changed[at] = changed[at] ^ 1;
                Expect.True(Seal.Open("save", "id", "name", changed) == null, "byte " + at + " changed");
            }

            Expect.True(Seal.Open("save", "id", "name", closed.GetRange(0, closed.Count - 1)) == null, "a byte short");
            Expect.True(Seal.Open("save", "id", "name", new List<int> { 1, 2, 3 }) == null, "shorter than the check");
            Expect.True(Seal.Open("file", "id", "name", closed) == null, "another kind");
            Expect.True(Seal.Open("save", "other", "name", closed) == null, "another id");
            Expect.True(Seal.Open("save", "id", "other", closed) == null, "another name");
            Expect.True(Seal.Open("save", "idn", "ame", closed) == null, "the id and name cut elsewhere");
            Expect.True(Seal.Open("save", "id", "name", closed) != null, "its own");
            SaveGame.Key = "another key";
            Expect.True(Seal.Open("save", "id", "name", closed) == null, "another key");
        }

        /// <summary>A seal is pinned to its bytes, so a later change to the hash, the cipher or how they are put together shows.</summary>
        public static void Pinned(FakeWorld world)
        {
            SaveGame.Key = "the case key";
            Expect.Equal(PinnedSeal, Hex(Seal.Close("save", "id", "name", Ascii("Moonwell"))), "the seal of \"Moonwell\"");
        }

        /// <summary>Base64 letters as RFC 4648 gives them, back to their bytes; text that is none gives null.</summary>
        public static void Letters(FakeWorld world)
        {
            var texts = new[] { "", "f", "fo", "foo", "foob", "fooba", "foobar" };
            var letters = new[] { "", "Zg==", "Zm8=", "Zm9v", "Zm9vYg==", "Zm9vYmE=", "Zm9vYmFy" };
            for (var i = 0; i < texts.Length; i++)
            {
                Expect.Equal(letters[i], Seal.ToLetters(Ascii(texts[i])), "the letters of " + texts[i]);
                var back = Seal.FromLetters(letters[i]);
                Expect.True(back != null && Hex(back) == Hex(Ascii(texts[i])), "the bytes of " + letters[i]);
            }

            var all = new List<int>();
            for (var i = 0; i < 256; i++)
            {
                all.Add(i);
            }

            Expect.Equal(Hex(all), Hex(Seal.FromLetters(Seal.ToLetters(all))!), "every byte");
            Expect.True(Seal.FromLetters("Zm9") == null, "not four letters");
            Expect.True(Seal.FromLetters("Zm9!") == null, "a letter that is none");
            Expect.True(Seal.FromLetters("Zg==Zm9v") == null, "padding inside");
            Expect.True(Seal.FromLetters("hello, world") == null, "plain text");
        }

        /// <summary>The pinned seal: the key "the case key", the kind "save", the id "id", the name "name" and the bytes of "Moonwell".</summary>
        internal const string PinnedSeal = "7e2ccb8ee8ac06e0aa183aad359c9cd2";

        internal static List<int> Ascii(string text)
        {
            var bytes = new List<int>();
            for (var i = 0; i < text.Length; i++)
            {
                bytes.Add(text[i]);
            }

            return bytes;
        }

        internal static string Hex(List<int> bytes)
        {
            const string digits = "0123456789abcdef";
            var text = new StringBuilder();
            for (var i = 0; i < bytes.Count; i++)
            {
                text.Append(digits[(bytes[i] >> 4) & 15]).Append(digits[bytes[i] & 15]);
            }

            return text.ToString();
        }

        private static List<int> Repeated(int value, int count)
        {
            var bytes = new List<int>();
            for (var i = 0; i < count; i++)
            {
                bytes.Add(value);
            }

            return bytes;
        }

        private static List<int> Counted(int count)
        {
            var bytes = new List<int>();
            for (var i = 0; i < count; i++)
            {
                bytes.Add(((i * 31) + 7) & 255);
            }

            return bytes;
        }
    }
}
