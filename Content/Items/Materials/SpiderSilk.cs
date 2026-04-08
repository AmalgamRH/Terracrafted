using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraCraft.Content.Items.Materials
{
    public class SpiderSilk : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = Item.height = 20;
            Item.maxStack = Item.CommonMaxStack;
            Item.value = Item.sellPrice(0, 0, 0, 20);
            Item.rare = ItemRarityID.White;
        }
    }
}
