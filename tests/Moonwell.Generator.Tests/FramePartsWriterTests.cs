using Moonwell.Generator;

namespace Moonwell.Generator.Tests;

public class FramePartsWriterTests
{
    private const string Panel = """
        // A template at the top: { not a block }
        String "TitleTemplate" {
            Font "MasterFont", 0.01,
        }
        Frame "SIMPLEFRAME" "UnitDetail" {
            String "NameValue" INHERITS "TitleTemplate" {
                Text "{",
            }
            Frame "SIMPLEFRAME" "StatsPanel" {
                Layer "ARTWORK" {
                    Texture "Cover" {
                        File "Cover",
                    }
                }
                String "ClassValue" INHERITS "TitleTemplate" {
                }
            }
            Texture {
                File "Unnamed",
            }
        }
        """;

    private const string Observer = """
        Frame "SIMPLEFRAME" "ObserverDetail" {
            String "NameValue" {
            }
        }
        Frame "TEXT" "TitleTemplate" {
        }
        """;

    [Fact]
    public void reads_each_string_and_texture_with_the_frame_it_sits_in()
    {
        var parts = FramePartsWriter.Read([Panel]);

        Assert.Equal(new[] { "ClassValue", "Cover", "NameValue", "TitleTemplate" }, parts.Keys);
        Assert.Equal(("String", "StatsPanel"), parts["ClassValue"]);
        Assert.Equal(("Texture", "StatsPanel"), parts["Cover"]);
        Assert.Equal(("String", "UnitDetail"), parts["NameValue"]);
        Assert.Equal(("String", (string?)null), parts["TitleTemplate"]);
    }

    [Fact]
    public void a_name_in_two_frames_has_none_and_a_name_also_a_frame_is_left_out()
    {
        var parts = FramePartsWriter.Read([Panel, Observer]);

        Assert.Equal(("String", (string?)null), parts["NameValue"]);
        Assert.False(parts.ContainsKey("TitleTemplate"));
    }
}
