namespace Moonwell.Analyzers.Tests;

/// <summary>MW0055: Remove&lt;T&gt;() in an item's or item component's OnDropped or OnUnequipped, the item side of the two auras trap (decision 216).</summary>
public class ItemRemoveTests
{
    private const string Source = """
        using Moonwell;

        namespace Map
        {
            public sealed class Haste : UnitComponent { }

            public sealed class Boots : Item
            {
                public override void OnPickedUp(Unit carrierUnit) { carrierUnit.Add(new Haste()); }
                public override void OnDropped(Unit carrierUnit) { carrierUnit.Remove<Haste>(); }
                public override void OnUnequipped(Unit carrierUnit) { carrierUnit.Remove<Haste>(); }
            }

            public sealed class Swift : ItemComponent
            {
                public override void OnDropped(Unit carrierUnit) { carrierUnit.Remove<Haste>(); }
            }

            public sealed class Guard : Unit
            {
                public override void OnItemDropped(Item droppedItem) { Remove<Haste>(); }
            }

            public sealed class Ring : Item
            {
                public override void OnPickedUp(Unit carrierUnit) { carrierUnit.Remove<Haste>(); }
                public override void OnDropped(Unit carrierUnit) { carrierUnit.Add(new Haste(), heldBy: Carry); }
            }
        }
        """;

    [Fact]
    public void remove_in_an_item_drop_or_unequip_is_warned_and_nowhere_else()
    {
        Assert.Equal(new[] { "MW0055: carrierUnit.Remove<Haste>()", "MW0055: carrierUnit.Remove<Haste>()", "MW0055: carrierUnit.Remove<Haste>()" }, MapCode.CheckFile(Source));
    }

    [Fact]
    public void the_message_names_the_fix()
    {
        var message = MapCode.MessagesOfFile(Source)[0];
        Assert.Contains("heldBy: Item.Carry", message);
        Assert.Contains("OnDropped", message);
    }
}
