using Terraria.ID;

namespace TerraCraft.Core.Systems.GridCrafting
{
    public static class CraftingStationSize
    {
        /// <summary>
        /// 根据物块ID返回合成网格尺寸（宽, 高）
        /// </summary>
        public static (int width, int height) GetGridSize(int tileType)
        {
            if (tileType == TileID.WorkBenches)
                return (3, 3);
            if (tileType == TileID.Anvils)
                return (3, 3);
            return (3, 3);
        }
    }
}