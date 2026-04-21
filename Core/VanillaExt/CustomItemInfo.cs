using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TerraCraft.Core.VanillaExt
{
    public class CustomItemInfo : GlobalItem
    {
        private bool isFuel = false;
        public override bool InstancePerEntity => true;
        public override void SetDefaults(Item item)
        {
            if (CustomItemDataCache.MaterialItemIds.Contains(item.type))
            {
                item.material = true;
            }
            if (CustomItemDataCache.FuelItemIds.Contains(item.type))
            {
                isFuel = true;
            }
        }
        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            if (isFuel)
                InsertFuelTooltip(tooltips);

            switch (item.type)
            {
                case ItemID.GlassKiln:
                    AddOrReplaceTooltipLine(tooltips, "SmeltingInfo", TerraCraft.GetLocalizedText("Tooltips.GlassKiln"));
                    break;
                case ItemID.Hellforge:
                    tooltips.Add(new TooltipLine(Mod, "SmeltingInfo", TerraCraft.GetLocalizedText("Tooltips.Hellforge")));
                    break;
                case ItemID.IronAnvil:
                case ItemID.LeadAnvil:
                    AddOrReplaceTooltipLine(tooltips, "SmeltingInfo", TerraCraft.GetLocalizedText("Tooltips.IronAnvil"));
                    break;
                case ItemID.HeavyWorkBench:
                    AddOrReplaceTooltipLine(tooltips, "SmeltingInfo", TerraCraft.GetLocalizedText("Tooltips.HeavyWorkbench"));
                    break;
            }
        }

        private void InsertFuelTooltip(List<TooltipLine> tooltips)
        {
            int materialIndex = tooltips.FindIndex(t => t.Mod == "Terraria" && t.Name == "Material");
            int firstPrefixIndex = tooltips.FindIndex(t => t.Mod == "Terraria" && t.Name.StartsWith("Prefix"));

            int insertIndex;
            if (materialIndex != -1)
                insertIndex = materialIndex + 1;
            else if (firstPrefixIndex != -1)
                insertIndex = firstPrefixIndex;
            else
                insertIndex = tooltips.Count;

            var newLine = new TooltipLine(Mod, "Fuel", TerraCraft.GetLocalizedText("Tooltips.Fuel"));
            tooltips.Insert(insertIndex, newLine);
        }

        private void AddOrReplaceTooltipLine(List<TooltipLine> tooltips, string lineName, string text)
        {
            var legacyLine = tooltips.Find(t => t.Mod == "Terraria" && t.Name.StartsWith("Tooltip"));
            if (legacyLine == null)
            {
                tooltips.Add(new TooltipLine(Mod, lineName, text));
            }
            else
            {
                legacyLine.Text = text;
            }
        }
    }
}