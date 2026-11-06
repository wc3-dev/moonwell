namespace Moonwell.Analyzers.Tests;

/// <summary>MW0001: a player's Selection and a unit's AnimationSpeed are read inside Local.For; selecting and animating are changes (decisions 200 and 203).</summary>
public class SelectionTests
{
    [Fact]
    public void reads_are_allowed_inside_and_changes_are_not()
    {
        var found = MapCode.Check("""
            var w = Unit.Of(u)!;
            Local.For(p, () =>
            {
                var picked = p.Selection;
                var speed = w.AnimationSpeed;
                p.Select(w);
                p.AddToSelection(w);
                p.Deselect(w);
                p.ClearSelection();
                w.PlayAnimation("attack");
                w.PlayAnimation(2);
                w.QueueAnimation("stand");
                w.AnimationSpeed = 2f;
                w.AddAnimationTag(AnimationTag.Defend);
                w.RemoveAnimationTag(AnimationTag.Defend);
            });
            """);

        Assert.Equal(10, found.Count);
        Assert.All(found, f => Assert.StartsWith("MW0001: ", f));
    }
}
