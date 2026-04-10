using System.Collections.Generic;
using TerraCraft.Core.UI.GridCrafting;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraCraft.Core.Systems.GridCrafting
{
    public class CraftingTileGlobal : GlobalTile
    {
        private GridCraftingUIRegister UIRegister => ModContent.GetInstance<GridCraftingUIRegister>();

        private readonly List<int> EnableGridCraftingTiles = [TileID.WorkBenches, TileID.Anvils, TileID.HeavyWorkBench, TileID.MythrilAnvil];
        private readonly List<int> EnableGridCraftingItems = [ItemID.WorkBench, ItemID.IronAnvil, ItemID.HeavyWorkBench, ItemID.MythrilAnvil];
        public override void MouseOver(int i, int j, int type)
        {
            int index = EnableGridCraftingTiles.IndexOf(type);
            if (index != -1)
            {
                Player player = Main.LocalPlayer;
                player.cursorItemIconEnabled = true;
                player.cursorItemIconID = index < EnableGridCraftingItems.Count ? EnableGridCraftingItems[index] : ItemID.WorkBench;
                player.mouseInterface = true;
            }
        }

        public override void RightClick(int i, int j, int type)
        {
            if (EnableGridCraftingTiles.Contains(type))
            {
                Main.playerInventory = true;
                UIRegister.OpenGridCraftingUI(type);
            }
        }
    }
}
