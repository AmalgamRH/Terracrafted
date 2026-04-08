using System.Collections.Generic;

namespace TerraCraft.Core.DataStructures.GridCrafting
{
    public struct GriddedRecipe
    {
        public string Id { get; set; }
        public int GridWidth { get; set; } = 3;
        public int GridHeight { get; set; } = 3;
        public bool Shaped { get; set; } = true;
        public List<int> RequiredTileIds { get; set; }   // 预先解析好的工作台 ID
        public List<Ingredient> Ingredients { get; set; }
        public List<Output> Outputs { get; set; }
        public List<Replacement> Replacements { get; set; }

        public GriddedRecipe()
        {
            GridWidth = 3;
            GridHeight = 3;
            Shaped = true;
            RequiredTileIds = new List<int>();
            Replacements = new List<Replacement>();
        }
    }
}