using System.Collections.Generic;
using System.Linq;
using TerraCraft.Core.Systems.GridCrafting;
using Terraria;
using Terraria.ModLoader;

namespace TerraCraft.Core.DataStructures.GridCrafting
{
    public class RecipeDatabase
    {
        public List<GriddedRecipe> Recipes { get; set; } = new List<GriddedRecipe>();
        
        // 缓存结构 - 按工作台类型分组
        private Dictionary<int, List<GriddedRecipe>> _recipesByTileId;
        
        // 缓存结构 - 按尺寸分组 (有形状配方)
        private Dictionary<(int width, int height), List<GriddedRecipe>> _shapedRecipesBySize;
        
        // 缓存结构 - 无形状配方
        private List<GriddedRecipe> _shapelessRecipes;

        //缓存结构 - 通用配方
        private List<GriddedRecipe> _universalRecipes;

        // 是否已初始化缓存
        private bool _cacheInitialized = false;
        
        // 初始化缓存
        public void InitializeCache()
        {
            if (_cacheInitialized) return;

            // 分离通用配方
            _universalRecipes = Recipes
                .Where(r => r.RequiredTileIds == null || r.RequiredTileIds.Count == 0)
                .ToList();

            // 按工作台类型分组（仅处理有具体Tile要求的配方）
            _recipesByTileId = Recipes
                .Where(r => r.RequiredTileIds != null && r.RequiredTileIds.Count > 0)
                .SelectMany(recipe => recipe.RequiredTileIds.Select(tileId => (tileId, recipe)))
                .GroupBy(x => x.tileId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.recipe).ToList());

            // 按尺寸分组（有形状配方）
            _shapedRecipesBySize = Recipes
                .Where(r => r.Shaped)
                .GroupBy(r => (r.GridWidth, r.GridHeight))
                .ToDictionary(
                    g => g.Key, 
                    g => g.ToList());
            
            // 无形状配方列表
            _shapelessRecipes = Recipes
                .Where(r => !r.Shaped)
                .ToList();
            
            _cacheInitialized = true;
        }
        
        // 获取特定工作台可用的配方
        public List<GriddedRecipe> GetRecipesForTile(int tileId)
        {
            if (!_cacheInitialized) InitializeCache();
            
            // 使用HashSet避免重复配方
            HashSet<GriddedRecipe> recipeSet = new HashSet<GriddedRecipe>();


            // 添加通用配方
            if (_universalRecipes != null)
            {
                foreach (var recipe in _universalRecipes)
                    recipeSet.Add(recipe);
            }


            // 获取当前工作台所有兼容的tile（包括自身和所有父级工作台）
            var compatibleTiles = TileCompatibilitySystem.GetCompatibleTiles(tileId);
            foreach (var compatibleTileId in compatibleTiles)
            {
                // 获取该兼容工作台的配方
                if (_recipesByTileId.TryGetValue(compatibleTileId, out var tileRecipes))
                {
                    foreach (var recipe in tileRecipes)
                    {
                        recipeSet.Add(recipe);
                    }
                }
            }
            
            return recipeSet.ToList();
        }
        
        // 获取特定工作台和尺寸的有形状配方
        public List<GriddedRecipe> GetShapedRecipesForTileAndSize(int tileId, int maxWidth, int maxHeight)
        {
            if (!_cacheInitialized) InitializeCache();
            
            var allTileRecipes = GetRecipesForTile(tileId);
            
            return allTileRecipes
                .Where(r => r.Shaped && r.GridWidth <= maxWidth && r.GridHeight <= maxHeight)
                .ToList();
        }
        
        // 获取特定工作台的无形状配方
        public List<GriddedRecipe> GetShapelessRecipesForTile(int tileId)
        {
            if (!_cacheInitialized) InitializeCache();
            
            var allTileRecipes = GetRecipesForTile(tileId);
            
            return allTileRecipes
                .Where(r => !r.Shaped)
                .ToList();
        }

        // 清空缓存（当配方列表变化时调用）
        public void ClearCache()
        {
            _recipesByTileId = null;
            _shapedRecipesBySize = null;
            _shapelessRecipes = null;
            _universalRecipes = null;
            _cacheInitialized = false;
        }
    }
}


