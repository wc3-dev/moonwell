namespace Moonwell.Tests;

/// <summary>A text read from bytes on .NET (build-time code and the test fakes) decodes UTF-8 as .NET does.</summary>
public class Utf8Tests
{
    [Theory]
    [InlineData(new byte[] { 0x80, 0x41 })]
    [InlineData(new byte[] { 0xC3 })]
    [InlineData(new byte[] { 0xC0, 0x80 })]
    [InlineData(new byte[] { 0xE4, 0x41, 0x42 })]
    [InlineData(new byte[] { 0xED, 0xA0, 0x80 })]
    [InlineData(new byte[] { 0xF4, 0x90, 0x80, 0x80 })]
    [InlineData(new byte[] { 0x41, 0xC3, 0xA5, 0xE2, 0x82, 0xAC, 0xF0, 0x9F, 0x98, 0x80 })]
    public void a_text_reads_as_dotnet_decodes_its_bytes(byte[] text)
    {
        Assert.Equal(System.Text.Encoding.UTF8.GetString(text), Read(text));
    }

    [Fact]
    public void random_bytes_read_as_dotnet_decodes_them()
    {
        var random = new Random(52);
        for (var i = 0; i < 5000; i++)
        {
            var text = new byte[random.Next(1, 12)];
            for (var k = 0; k < text.Length; k++)
            {
                text[k] = (byte)(random.Next(3) == 0 ? random.Next(0x80, 0xC0) : random.Next(256));
            }

            Assert.Equal(System.Text.Encoding.UTF8.GetString(text), Read(text));
        }
    }

    // A text as ByteWriter writes it: its length (under 64, one byte) and its bytes.
    private static string Read(byte[] text)
    {
        return new ByteReader(new[] { (byte)(text.Length << 1) }.Concat(text).ToArray()).ReadString();
    }
}
