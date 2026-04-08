namespace TerraCraft.Core.DataStructures.GridCrafting
{
    public struct Ingredient
    {
        /// <summary>位置（仅当 Shaped = true 时有效）</summary>
        public int? X { get; set; }
        public int? Y { get; set; }

        public int ItemType { get; set; }          // 预先解析好的物品 ID
        public string RecipeGroup { get; set; }    // 配方组（仍为字符串，动态匹配）

        public int Amount { get; set; } = 1;

        public Ingredient()
        {
            Amount = 1;
        }
    }
}