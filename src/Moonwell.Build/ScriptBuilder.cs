using War3Net.Build;
using War3Net.Build.Extensions;
using War3Net.Build.Providers;
using War3Net.Build.Widget;
using War3Net.CodeAnalysis;
using War3Net.CodeAnalysis.Jass;
using War3Net.CodeAnalysis.Jass.Extensions;
using War3Net.Common.Extensions;

namespace Moonwell.Build
{
    /// <summary>
    /// War3Net's script builder set for CSharp.lua, making the preplaced destructables as World Editor's script does: the game makes
    /// each one of <c>war3map.doo</c> that keeps its state's visible or solid bit, and the script makes the others (World Editor clears
    /// both on the ones its triggers name or that drop items). Those are made in a local, so no global is declared for any of them.
    /// </summary>
    internal sealed class ScriptBuilder : MapScriptBuilder
    {
        private const string ItemId = "itemID";

        public ScriptBuilder()
            : base(triggerData: null!)
        {
            SetDefaultOptionsForCSharpLua();
            ForceGenerateGlobalDestructableVariable = false;
        }

        /// <summary>
        /// Hands the builder only the destructables the script makes while it builds, then gives the map back its own. The builder
        /// numbers a destructable's drop function by its place among the ones it makes and defines them by its place in the whole
        /// list, so the list it reads holds only those.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="build">The build, which reads the map.</param>
        /// <returns>What the build returns.</returns>
        public static T WithScriptDestructables<T>(Map map, Func<T> build)
        {
            var doodads = map.Doodads;
            if (doodads is null)
            {
                return build();
            }

            var made = new MapDoodads(doodads.FormatVersion, doodads.SubVersion, doodads.UseNewFormat)
            {
                Doodads = doodads.Doodads.Where(MadeByScript).ToList(),
                SpecialDoodadVersion = doodads.SpecialDoodadVersion,
                SpecialDoodads = doodads.SpecialDoodads,
            };
            map.Doodads = made;
            try
            {
                return build();
            }
            finally
            {
                map.Doodads = doodads;
            }
        }

        /// <summary>True for a destructable the game leaves to the script: neither its visible nor its solid bit is set.</summary>
        /// <param name="doodad">A destructable or doodad of <c>war3map.doo</c>.</param>
        public static bool MadeByScript(DoodadData doodad)
        {
            return (doodad.State & (DoodadState.NonSolidVisible | DoodadState.Normal)) == 0;
        }

        protected override bool ShouldGenerateDestructableVariable(DoodadData doodadData)
        {
            return true;
        }

        protected override bool ShouldGenerateDestructableVariables(Map map)
        {
            return false;
        }

        /// <summary>The global the script keeps a placed item in, named as World Editor names it: <c>gg_item_ratc_0005</c>.</summary>
        /// <param name="item">A placed item of <c>war3mapUnits.doo</c>.</param>
        public static string ItemGlobal(UnitData item)
        {
            return $"gg_item_{item.TypeId.ToRawcode()}_{item.CreationNumber:D4}";
        }

        // The item globals are declared with the units', which War3Net writes for every placed unit.
        protected override bool ShouldGenerateUnitVariables(Map map)
        {
            return base.ShouldGenerateUnitVariables(map) || PlacedItems(map).Any();
        }

        protected override void GenerateUnitVariables(Map map, IndentedTextWriter writer)
        {
            base.GenerateUnitVariables(map, writer);
            foreach (var item in PlacedItems(map))
            {
                writer.WriteAlignedGlobal("item", ItemGlobal(item), JassKeyword.Null);
            }
        }

        // shortcut: War3Net keeps no placed item, so this is its CreateAllItems with each item set to its global; drop it when the fork writes them.
        protected override void GenerateCreateAllItems(Map map, IndentedTextWriter writer)
        {
            writer.WriteFunction("CreateAllItems");
            writer.WriteLocal(JassKeyword.Integer, ItemId);
            foreach (var item in PlacedItems(map))
            {
                var x = JassLiteral.Real(item.Position.X);
                var y = JassLiteral.Real(item.Position.Y);
                if (!item.IsRandomItem())
                {
                    var hasSkin = item.SkinId != 0 && item.SkinId != item.TypeId;
                    var made = hasSkin
                        ? JassExpression.Invoke("BlzCreateItemWithSkin", JassLiteral.FourCC(item.TypeId), x, y, JassLiteral.FourCC(item.SkinId))
                        : JassExpression.Invoke("CreateItem", JassLiteral.FourCC(item.TypeId), x, y);
                    writer.WriteSet(ItemGlobal(item), made);
                    continue;
                }

                switch (item.RandomData)
                {
                    case RandomUnitAny any:
                        writer.WriteSet(ItemId, JassExpression.InvokeSpaced("ChooseRandomItemEx", JassExpression.Invoke("ConvertItemType", JassLiteral.Int((int)any.Class)), JassLiteral.Int(any.Level)));
                        break;

                    case RandomUnitCustomTable table:
                        writer.WriteCall("RandomDistReset");
                        var summedChance = 0;
                        foreach (var choice in table.RandomUnits)
                        {
                            var id = RandomItemProvider.IsRandomItem(choice.UnitId, out var itemClass, out var level)
                                ? JassExpression.Invoke("ChooseRandomItemEx", JassExpression.Invoke("ConvertItemType", JassLiteral.Int((int)itemClass)), JassLiteral.Int(level))
                                : JassLiteral.FourCC(choice.UnitId);
                            writer.WriteCall("RandomDistAddItem", id, JassLiteral.Int(choice.Chance));
                            summedChance += choice.Chance;
                        }

                        if (summedChance < 100)
                        {
                            writer.WriteCall("RandomDistAddItem", "-1", JassLiteral.Int(100 - summedChance));
                        }

                        writer.WriteSet(ItemId, JassExpression.InvokeSpaced("RandomDistChoose"));
                        break;
                }

                writer.WriteIf(JassExpression.NotEqual(ItemId, "-1"));
                writer.WriteSet(ItemGlobal(item), JassExpression.Invoke("CreateItem", ItemId, x, y));
                writer.WriteEndIf();
            }

            writer.WriteLine();
            writer.EndFunction();
        }


        private IEnumerable<UnitData> PlacedItems(Map map)
        {
            return map.Units?.Units.Where(item => ShouldGenerateCreateAllItemsForItem(map, item)) ?? Enumerable.Empty<UnitData>();
        }
    }
}
