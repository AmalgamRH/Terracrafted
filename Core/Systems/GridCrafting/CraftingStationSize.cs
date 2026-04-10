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
            switch (tileType)
            {
                case TileID.WorkBenches:
                case TileID.Anvils: 
                    return (3, 3);
                case TileID.HeavyWorkBench:
                    return (5, 3);
                case TileID.MythrilAnvil:
                    return (5, 5);
            }
            return (2, 2);
        }
    }
}