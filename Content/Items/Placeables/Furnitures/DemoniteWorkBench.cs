using TerraCraft.Content.Tiles.Furnitures;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraCraft.Content.Items.Placeables.Furnitures
{
    public class DemoniteWorkBench : ModItem
    {
        public sealed override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }
        public override void SetDefaults()
        {
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(0, 0, 40, 0);
            Item.DefaultToPlaceableTile(ModContent.TileType<EvilWorkBenches>(), tileStyleToPlace: 0);
        }
    }
}