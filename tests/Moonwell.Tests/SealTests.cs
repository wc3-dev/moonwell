using System.Security.Cryptography;

namespace Moonwell.Tests;

/// <summary>Moonwell's SHA-256, HMAC and ChaCha20 (decision 398) give what .NET's own give, over random bytes of every length to a few blocks.</summary>
public class SealTests
{
    [Fact]
    public void Sha256_and_Hmac_are_dotnets()
    {
        var random = new Random(398);
        for (var length = 0; length < 200; length++)
        {
            var message = new byte[length];
            random.NextBytes(message);
            var key = new byte[random.Next(1, 100)];
            random.NextBytes(key);
            Assert.Equal(SHA256.HashData(message), Bytes(Sha256.Hash(List(message))));
            Assert.Equal(HMACSHA256.HashData(key, message), Bytes(Sha256.Hmac(List(key), List(message))));
        }
    }

    [Fact]
    public void ChaCha20_is_dotnets()
    {
        if (!ChaCha20Poly1305.IsSupported)
        {
            return;
        }

        var random = new Random(398);
        for (var length = 0; length < 200; length++)
        {
            var key = new byte[32];
            var nonce = new byte[12];
            var plain = new byte[length];
            random.NextBytes(key);
            random.NextBytes(nonce);
            random.NextBytes(plain);
            var expected = new byte[length];
            using (var dotnets = new ChaCha20Poly1305(key))
            {
                dotnets.Encrypt(nonce, plain, expected, new byte[16]);
            }

            // RFC 8439's sealed text is ChaCha20 from block 1 on; block 0 makes its Poly1305 key.
            var mixed = List(plain);
            ChaCha20.Mix(Words(key), Words(nonce), 1, mixed, 0);
            Assert.Equal(expected, Bytes(mixed));
        }
    }

    private static List<int> List(byte[] bytes) => bytes.Select(b => (int)b).ToList();

    private static byte[] Bytes(List<int> bytes) => bytes.Select(b => (byte)b).ToArray();

    private static int[] Words(byte[] bytes)
    {
        var words = new int[bytes.Length / 4];
        for (var i = 0; i < words.Length; i++)
        {
            words[i] = BitConverter.ToInt32(bytes, i * 4);
        }

        return words;
    }
}
