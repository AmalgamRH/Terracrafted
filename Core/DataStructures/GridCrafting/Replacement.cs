namespace TerraCraft.Core.DataStructures.GridCrafting
{
    /// <summary>替换规则（容器物品）</summary>
    public struct Replacement
    {
        public int? X { get; set; }
        public int? Y { get; set; }

        public int OriginalItemType { get; set; }      // 原始物品 ID
        public int? ReplaceWithType { get; set; }      // 替换成的物品 ID，null 表示直接移除
        public int ReplaceAmount { get; set; } = 1;

        public Replacement()
        {
            ReplaceAmount = 1;
        }
    }
}