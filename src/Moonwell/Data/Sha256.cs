using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// SHA-256 and HMAC-SHA256 (FIPS 180-4, RFC 2104) over bytes, each 0 to 255, for the check of a sealed save or file
    /// (decision 398). A word is an <c>int</c> whose low 32 bits count: .NET and the game's 32-bit integers wrap a sum, the
    /// tests' 64-bit Lua carries it higher, and every right shift is masked, so the three agree (game facts, 11-03).
    /// </summary>
    internal static class Sha256
    {
        private static readonly int[] K =
        {
            0x428a2f98, 0x71374491, unchecked((int)0xb5c0fbcf), unchecked((int)0xe9b5dba5), 0x3956c25b, 0x59f111f1, unchecked((int)0x923f82a4), unchecked((int)0xab1c5ed5),
            unchecked((int)0xd807aa98), 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, unchecked((int)0x80deb1fe), unchecked((int)0x9bdc06a7), unchecked((int)0xc19bf174),
            unchecked((int)0xe49b69c1), unchecked((int)0xefbe4786), 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
            unchecked((int)0x983e5152), unchecked((int)0xa831c66d), unchecked((int)0xb00327c8), unchecked((int)0xbf597fc7), unchecked((int)0xc6e00bf3), unchecked((int)0xd5a79147), 0x06ca6351, 0x14292967,
            0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, unchecked((int)0x81c2c92e), unchecked((int)0x92722c85),
            unchecked((int)0xa2bfe8a1), unchecked((int)0xa81a664b), unchecked((int)0xc24b8b70), unchecked((int)0xc76c51a3), unchecked((int)0xd192e819), unchecked((int)0xd6990624), unchecked((int)0xf40e3585), 0x106aa070,
            0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
            0x748f82ee, 0x78a5636f, unchecked((int)0x84c87814), unchecked((int)0x8cc70208), unchecked((int)0x90befffa), unchecked((int)0xa4506ceb), unchecked((int)0xbef9a3f7), unchecked((int)0xc67178f2),
        };

        /// <summary>The hash of the bytes, as 32 bytes.</summary>
        internal static List<int> Hash(List<int> message)
        {
            var h = new[]
            {
                0x6a09e667, unchecked((int)0xbb67ae85), 0x3c6ef372, unchecked((int)0xa54ff53a),
                0x510e527f, unchecked((int)0x9b05688c), 0x1f83d9ab, 0x5be0cd19,
            };

            // The message, a byte of 128, zeros to 56 past a block, and the length in bits as 8 bytes.
            var padded = new List<int>(message);
            padded.Add(128);
            while (padded.Count % 64 != 56)
            {
                padded.Add(0);
            }

            var bits = message.Count * 8;
            padded.Add(0);
            padded.Add(0);
            padded.Add(0);
            padded.Add(0);
            padded.Add((bits >> 24) & 255);
            padded.Add((bits >> 16) & 255);
            padded.Add((bits >> 8) & 255);
            padded.Add(bits & 255);

            var w = new int[64];
            for (var at = 0; at < padded.Count; at += 64)
            {
                Block(h, w, padded, at);
            }

            var hash = new List<int>();
            for (var i = 0; i < 8; i++)
            {
                var word = h[i];
                hash.Add((word >> 24) & 255);
                hash.Add((word >> 16) & 255);
                hash.Add((word >> 8) & 255);
                hash.Add(word & 255);
            }

            return hash;
        }

        /// <summary>The keyed hash of a message, as 32 bytes: no one without the key can make it for a changed message.</summary>
        internal static List<int> Hmac(List<int> key, List<int> message)
        {
            var block = key.Count > 64 ? Hash(key) : key;
            var inner = new List<int>();
            var outer = new List<int>();
            for (var i = 0; i < 64; i++)
            {
                var value = i < block.Count ? block[i] : 0;
                inner.Add(value ^ 0x36);
                outer.Add(value ^ 0x5c);
            }

            inner.AddRange(message);
            outer.AddRange(Hash(inner));
            return Hash(outer);
        }

        private static void Block(int[] h, int[] w, List<int> bytes, int at)
        {
            for (var i = 0; i < 16; i++)
            {
                var p = at + (i * 4);
                w[i] = (bytes[p] << 24) | (bytes[p + 1] << 16) | (bytes[p + 2] << 8) | bytes[p + 3];
            }

            for (var i = 16; i < 64; i++)
            {
                var x = w[i - 15];
                var y = w[i - 2];
                var s0 = (((x >> 7) & 0x1ffffff) | (x << 25)) ^ (((x >> 18) & 0x3fff) | (x << 14)) ^ ((x >> 3) & 0x1fffffff);
                var s1 = (((y >> 17) & 0x7fff) | (y << 15)) ^ (((y >> 19) & 0x1fff) | (y << 13)) ^ ((y >> 10) & 0x3fffff);
                w[i] = w[i - 16] + s0 + w[i - 7] + s1;
            }

            var a = h[0];
            var b = h[1];
            var c = h[2];
            var d = h[3];
            var e = h[4];
            var f = h[5];
            var g = h[6];
            var last = h[7];
            for (var i = 0; i < 64; i++)
            {
                var s1 = (((e >> 6) & 0x3ffffff) | (e << 26)) ^ (((e >> 11) & 0x1fffff) | (e << 21)) ^ (((e >> 25) & 0x7f) | (e << 7));
                var t1 = last + s1 + ((e & f) ^ (~e & g)) + K[i] + w[i];
                var s0 = (((a >> 2) & 0x3fffffff) | (a << 30)) ^ (((a >> 13) & 0x7ffff) | (a << 19)) ^ (((a >> 22) & 0x3ff) | (a << 10));
                var t2 = s0 + ((a & b) ^ (a & c) ^ (b & c));
                last = g;
                g = f;
                f = e;
                e = d + t1;
                d = c;
                c = b;
                b = a;
                a = t1 + t2;
            }

            h[0] += a;
            h[1] += b;
            h[2] += c;
            h[3] += d;
            h[4] += e;
            h[5] += f;
            h[6] += g;
            h[7] += last;
        }
    }
}
