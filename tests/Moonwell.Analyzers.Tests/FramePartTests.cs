namespace Moonwell.Analyzers.Tests;

/// <summary>MW0057: a native the game crashes on, given one of its Strings or Textures found by name.</summary>
public class FramePartTests
{
    [Fact]
    public void the_crashing_natives_on_a_string_or_texture_by_name_are_refused()
    {
        var found = MapCode.Check("""
            Natives.BlzFrameSetAlpha(Natives.BlzGetFrameByName("SimpleNameValue", 0), 0);
            Natives.BlzFrameSetVisible(Natives.BlzGetFrameByName("InventoryCoverTexture", 0), false);
            Natives.BlzDestroyFrame(Natives.BlzGetFrameByName("SimpleClassValue", 0));
            """);

        Assert.Equal(new[]
        {
            "MW0057: Natives.BlzFrameSetAlpha(Natives.BlzGetFrameByName(\"SimpleNameValue\", 0), 0)",
            "MW0057: Natives.BlzFrameSetVisible(Natives.BlzGetFrameByName(\"InventoryCoverTexture\", 0), false)",
            "MW0057: Natives.BlzDestroyFrame(Natives.BlzGetFrameByName(\"SimpleClassValue\", 0))",
        }, found);
    }

    // BlzFrameIsVisible is machine-local too (MW0002), which is not this rule's.
    [Fact]
    public void a_string_kept_in_a_local_or_a_field_is_followed()
    {
        var found = MapCode.Check("""
            var cover = Natives.BlzGetFrameByName("InventoryCoverTexture", 0);
            var shown = Natives.BlzFrameIsVisible(cover);
            name = Natives.BlzGetFrameByName("SimpleNameValue", 0);
            Natives.BlzFrameSetScale(name, 0.5f);
            Natives.BlzFrameSetLevel(kept, 1);
            """, """
            private static framehandle name;
            private static readonly framehandle kept = Natives.BlzGetFrameByName("SimpleClassValue", 0);
            """).Where(one => one.StartsWith("MW0057", StringComparison.Ordinal));

        Assert.Equal(new[] { "MW0057: Natives.BlzFrameIsVisible(cover)", "MW0057: Natives.BlzFrameSetScale(name, 0.5f)", "MW0057: Natives.BlzFrameSetLevel(kept, 1)" }, found);
    }

    [Fact]
    public void points_size_and_text_on_a_string_and_the_natives_on_a_frame_are_allowed()
    {
        var found = MapCode.Check("""
            var name = Natives.BlzGetFrameByName("SimpleNameValue", 0);
            Natives.BlzFrameClearAllPoints(name);
            Natives.BlzFrameSetAbsPoint(name, Natives.FRAMEPOINT_CENTER, 2f, 2f);
            Natives.BlzFrameSetSize(name, 0.01f, 0.01f);
            Natives.BlzFrameSetText(name, "");
            Natives.BlzFrameSetAlpha(Natives.BlzGetFrameByName("InventoryText", 0), 0);
            Natives.BlzFrameSetVisible(Natives.BlzGetFrameByName("SimpleInfoPanelUnitDetail", 0), false);
            """);

        Assert.Empty(found);
    }

    [Fact]
    public void the_message_names_the_frame_a_string_sits_in()
    {
        var found = MapCode.Messages("""
            var parent = Natives.BlzFrameGetParent(Natives.BlzGetFrameByName("SimpleClassValue", 0));
            """).Where(one => one.Contains("crashes the game", StringComparison.Ordinal));

        Assert.Equal(new[] { "'Natives.BlzFrameGetParent(Natives.BlzGetFrameByName(\"SimpleClassValue\", 0))' crashes the game to the desktop: 'SimpleClassValue' is a String of one of the game's simple frames, not a frame of its own; find the frame it sits in by that frame's own name, BlzGetFrameByName(\"SimpleUnitStatsPanel\", 0)" }, found);
    }
}
