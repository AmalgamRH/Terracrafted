using System.Collections.Generic;
using TerraCraft.Content.Tiles.Furnitures;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraCraft.Core.Systems.GridCrafting
{
    public static class CraftingStationSize
    {
        /// <summary>
        /// key: tileType，value: 合成网格尺寸（宽, 高）
        /// 所有支持的合成台都在这里注册，新增只需加一行
        /// </summary>
        public static readonly Dictionary<int, (int width, int height)> GridSizes = new()
        {
            { TileID.WorkBenches,    (3, 3) },
            { TileID.HeavyWorkBench, (3, 3) },
            { ModContent.TileType<EvilWorkBenches>(),    (4, 4) },
            { ModContent.TileType<MythrilWorkBenches>(), (5, 5) },

            { TileID.Sawmill,        (5, 3) },
            { TileID.Loom,           (3, 3) },
            { TileID.LivingLoom,     (5, 3) },
            { TileID.SkyMill,        (5, 3) },
            { TileID.IceMachine,     (5, 3) },
            { TileID.Solidifier,     (5, 3) },
            { TileID.HoneyDispenser, (5, 3) },
            { TileID.BoneWelder,     (5, 3) },
            { TileID.FleshCloningVat,(5, 3) },
            { TileID.LesionStation,  (5, 3) },

            { TileID.LunarCraftingStation,  (8, 8) }
        };

        public static bool IsSupported(int tileType)
            => GridSizes.ContainsKey(tileType);

        public static (int width, int height) GetGridSize(int tileType)
            => GridSizes.TryGetValue(tileType, out var size) ? size : (2, 2);
    }
}