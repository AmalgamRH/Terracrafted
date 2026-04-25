using TerraCraft.Content.Tiles.Furnitures;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraCraft.Content.Items.Placeables.Furnitures
{
    public class MythrilWorkBench : ModItem
    {
        public sealed override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }
        public override void SetDefaults()
        {
            Item.CloneDefaults(ItemID.MythrilAnvil);
            Item.DefaultToPlaceableTile(ModContent.TileType<MythrilWorkBenches>(), tileStyleToPlace: 0);
        }
    }
}