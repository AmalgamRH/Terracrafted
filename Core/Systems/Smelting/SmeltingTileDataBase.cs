using System.Collections.Generic;
using Terraria.ID;

namespace TerraCraft.Core.Systems.Smelting
{
    public static class SmeltingLabel
    {
        public const string Ore = "Ore";
        public const string Food = "Foods";
    }
    /// <summary>单个熔炉的配置数据</summary>
    public struct SmeltingTileData
    {
        /// <summary>基础烧炼速度倍率（1.0 = 正常速度，>1 加快，<1 减慢）</summary>
        public float BaseSpeedMultiplier { get; set; }

        /// <summary>按原料标签的额外速度倍率（例如 "ore": 2.0 表示烧矿石速度加倍）</summary>
        /// <remarks>如果标签未在此字典中定义，则使用 BaseSpeedMultiplier</remarks>
        public Dictionary<string, float> LabelSpeedMultipliers { get; set; }

        public SmeltingTileData(float baseSpeed = 1.0f, Dictionary<string, float> labelMultipliers = null)
        {
            BaseSpeedMultiplier = baseSpeed;
            LabelSpeedMultipliers = labelMultipliers ?? new Dictionary<string, float>();
        }
    }

    public static class SmeltingTileDataBase
    {
        /// <summary>
        /// key: tileType（熔炉图格ID）
        /// value: 熔炉配置数据（速率 + 标签速率映射）
        /// 所有支持的熔炉都在这里注册，新增只需加一行
        /// </summary>
        public static readonly Dictionary<int, SmeltingTileData> TileData = new()
        {
            // 普通熔炉（基础速度1.0，没有特殊标签加成）
            { TileID.Furnaces, new SmeltingTileData(1.0f) },

            // 玻璃熔炉（无法熔炼其他配方，但对"Ore"标签1.5倍速度）
            { TileID.GlassKiln, new SmeltingTileData(0f, new Dictionary<string, float> { { SmeltingLabel.Ore, 1.5f } }) },

            { TileID.Hellforge, new SmeltingTileData(1.2f, new Dictionary<string, float> { { SmeltingLabel.Ore, 1.5f } }) },

            { TileID.AdamantiteForge, new SmeltingTileData(1.5f) },
        };

        /// <summary>获取指定熔炉的基础速度倍率</summary>
        public static float GetBaseSpeedMultiplier(int tileType)
            => TileData.TryGetValue(tileType, out var data) ? data.BaseSpeedMultiplier : 1.0f;

        /// <summary>获取指定熔炉对某个原料标签的最终速度倍率（若标签有特定倍率则返回该倍率，否则返回基础倍率）</summary>
        public static float? GetSpeedMultiplier(int tileType, string label)
        {
            if (!TileData.TryGetValue(tileType, out var data))
                return 1f; // 未注册默认1倍
            if (!string.IsNullOrEmpty(label) && data.LabelSpeedMultipliers.TryGetValue(label, out var labelMulti))
                return labelMulti;
            if (data.BaseSpeedMultiplier > 0f)
                return data.BaseSpeedMultiplier;
            return null; // 基础速度为0且没有标签倍率，不支持
        }
        public static bool CanSmelt(int tileType, string label)
        {
            if (!TileData.TryGetValue(tileType, out var data))
                return true; // 未注册的熔炉默认允许
            if (data.BaseSpeedMultiplier <= 0f && !data.LabelSpeedMultipliers.ContainsKey(label))
                return false; // 基础速度为0且没有标签倍率，禁止
            return true;
        }
        /// <summary>检查某个熔炉是否被本系统支持（即是否在 TileData 中有注册）</summary>
        public static bool IsSupported(int tileType) => TileData.ContainsKey(tileType);
    }
}