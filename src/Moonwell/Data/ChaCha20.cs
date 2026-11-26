using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// The ChaCha20 cipher (RFC 8439) for a sealed save or file (decision 398): a stream of bytes from a key and a nonce,
    /// mixed into the bytes; mixing again with the same key and nonce undoes it. Words as in <see cref="Sha256"/>: the low
    /// 32 bits of an <c>int</c> count, and every right shift is masked.
    /// </summary>
    internal static class ChaCha20
    {
        /// <summary>
        /// Mixes the bytes from <paramref name="start"/> on with the stream of a key of 8 words and a nonce of 3, in place, the
        /// first block numbered <paramref name="counter"/>.
        /// </summary>
        internal static void Mix(int[] key, int[] nonce, int counter, List<int> bytes, int start)
        {
            var x = new int[16];
            var at = start;
            while (at < bytes.Count)
            {
                Block(x, key, nonce, counter);
                counter++;
                for (var i = 0; i < 16 && at < bytes.Count; i++)
                {
                    var word = x[i];
                    for (var shift = 0; shift < 32 && at < bytes.Count; shift += 8)
                    {
                        bytes[at] = bytes[at] ^ ((word >> shift) & 255);
                        at++;
                    }
                }
            }
        }

        /// <summary>One block of the stream, 16 words, into <paramref name="x"/>.</summary>
        internal static void Block(int[] x, int[] key, int[] nonce, int counter)
        {
            x[0] = 0x61707865;
            x[1] = 0x3320646e;
            x[2] = 0x79622d32;
            x[3] = 0x6b206574;
            for (var i = 0; i < 8; i++)
            {
                x[4 + i] = key[i];
            }

            x[12] = counter;
            x[13] = nonce[0];
            x[14] = nonce[1];
            x[15] = nonce[2];
            for (var round = 0; round < 10; round++)
            {
                Quarter(x, 0, 4, 8, 12);
                Quarter(x, 1, 5, 9, 13);
                Quarter(x, 2, 6, 10, 14);
                Quarter(x, 3, 7, 11, 15);
                Quarter(x, 0, 5, 10, 15);
                Quarter(x, 1, 6, 11, 12);
                Quarter(x, 2, 7, 8, 13);
                Quarter(x, 3, 4, 9, 14);
            }

            x[0] += 0x61707865;
            x[1] += 0x3320646e;
            x[2] += 0x79622d32;
            x[3] += 0x6b206574;
            for (var i = 0; i < 8; i++)
            {
                x[4 + i] += key[i];
            }

            x[12] += counter;
            x[13] += nonce[0];
            x[14] += nonce[1];
            x[15] += nonce[2];
        }

        private static void Quarter(int[] x, int ia, int ib, int ic, int id)
        {
            var a = x[ia];
            var b = x[ib];
            var c = x[ic];
            var d = x[id];
            a += b;
            d ^= a;
            d = (d << 16) | ((d >> 16) & 0xffff);
            c += d;
            b ^= c;
            b = (b << 12) | ((b >> 20) & 0xfff);
            a += b;
            d ^= a;
            d = (d << 8) | ((d >> 24) & 0xff);
            c += d;
            b ^= c;
            b = (b << 7) | ((b >> 25) & 0x7f);
            x[ia] = a;
            x[ib] = b;
            x[ic] = c;
            x[id] = d;
        }
    }
}
