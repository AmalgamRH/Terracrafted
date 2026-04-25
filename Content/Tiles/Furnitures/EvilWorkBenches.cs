using Microsoft.Xna.Framework;
using TerraCraft.Content.Items.Placeables.Furnitures;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace TerraCraft.Content.Tiles.Furnitures
{
    public class EvilWorkBenches : ModTile
    {
        public override void SetStaticDefaults()
        {
            Main.tileTable[Type] = true;
            Main.tileSolidTop[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = true;
            Main.tileFrameImportant[Type] = true;
            TileID.Sets.DisableSmartCursor[Type] = true;
            TileID.Sets.IgnoredByNpcStepUp[Type] = true;

            DustType = DustID.Demonite;
            AdjTiles = [TileID.WorkBenches, TileID.HeavyWorkBench];

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x1);
            TileObjectData.newTile.CoordinateHeights = [18];
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.addTile(Type);

            AddToArray(ref TileID.Sets.RoomNeeds.CountsAsTable);

            AddMapEntry(new Color(166, 187, 153), TerraCraft.GetOriginLocalizedText("Items.DemoniteWorkBench.DisplayName"));
            AddMapEntry(new Color(241, 129, 249), TerraCraft.GetOriginLocalizedText("Items.CrimtaneWorkBench.DisplayName"));

            RegisterItemDrop(ModContent.ItemType<DemoniteWorkBench>(), 0);
            RegisterItemDrop(ModContent.ItemType<CrimtaneWorkBench>(), 1);
        }
        public override ushort GetMapOption(int i, int j)
        {
            Tile tile = Main.tile[i, j];
            return (ushort)TileObjectData.GetTileStyle(tile);
        }
        public override bool CreateDust(int i, int j, ref int type)
        {
            Tile tile = Main.tile[i, j];
            type = TileObjectData.GetTileStyle(tile) switch
            {
                1 => DustID.Demonite,
                _ => DustID.Crimstone
            };
            return base.CreateDust(i, j, ref type);
        }
        public override void NumDust(int x, int y, bool fail, ref int num)
        {
            num = fail ? 3 : 9;
        }
    }
}