using Moonwell.GameData;

namespace Moonwell.Generator.Tests;

/// <summary>The cells a pathing texture gives a building (J60): the embedded table of the game's textures, and a TGA read in each of its layouts.</summary>
public class PathTexturesTests
{
    [Fact]
    public void the_games_textures_are_embedded_by_any_spelling_of_their_path()
    {
        var hall = PathTextures.Standard(@"PathTextures\16x16Simple.tga")!;
        Assert.Equal((16, 16, true), (hall.Width, hall.Height, hall.Full));
        Assert.Same(hall, PathTextures.Standard("pathtextures/16X16SIMPLE"));
        var cross = PathTextures.Standard(@"PathTextures\8x8Cross.tga")!;
        Assert.False(cross.Full);
        Assert.Equal("8x8:c3c3ffffffffc3c3", cross.Text());
        Assert.Null(PathTextures.Standard(@"PathTextures\NoSuch.tga"));
    }

    [Fact]
    public void the_text_reads_back()
    {
        var cells = new[] { true, false, false, true, false, true };
        var footprint = new PathTextures.Footprint(3, 2, cells);
        var again = PathTextures.Footprint.Parse(footprint.Text());
        Assert.Equal("3x2:92", footprint.Text());
        Assert.Equal(cells, again.Cells);
        Assert.Equal("4x4", PathTextures.Footprint.Parse("4x4").Text());
    }

    [Fact]
    public void a_tga_is_read_uncompressed_or_run_length_and_from_either_end()
    {
        // 2 by 2, 24 bits: the first pixel stored white, the rest black.
        var plain = Tga(2, 0x00, new byte[] { 255, 255, 255, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
        Assert.Equal(new[] { true, false, false, false }, PathTextures.Read(plain).Cells);

        // The same stored from the top: the first pixel is the upper left, so the cells' first row (the lowest) is black.
        var fromTop = Tga(2, 0x20, new byte[] { 255, 255, 255, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
        Assert.Equal(new[] { false, false, true, false }, PathTextures.Read(fromTop).Cells);

        // Run length: a run of three blue pixels, then one raw black one.
        var runs = Tga(10, 0x00, new byte[] { 0x82, 255, 0, 0, 0x00, 0, 0, 0 });
        Assert.Equal(new[] { true, true, true, false }, PathTextures.Read(runs).Cells);
        Assert.Throws<InvalidDataException>(() => PathTextures.Read(new byte[] { 0, 0, 1 }));
    }

    private static byte[] Tga(int type, byte descriptor, byte[] pixels)
    {
        var header = new byte[18];
        header[2] = (byte)type;
        header[12] = 2;
        header[14] = 2;
        header[16] = 24;
        header[17] = descriptor;
        return header.Concat(pixels).ToArray();
    }
}
