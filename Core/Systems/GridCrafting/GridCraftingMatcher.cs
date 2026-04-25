using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terraria;
using TerraCraft.Core.DataStructures.GridCrafting;
using TerraCraft.Core.Loaders;
using Terraria.ID;
using TerraCraft.Core.Utils;
using Terraria.ModLoader;

namespace TerraCraft.Core.Systems.GridCrafting
{
    public class GridCraftingMatcher
    {
        private readonly int _tileId;
        private readonly int _gridWidth;
        private readonly int _gridHeight;
        private readonly Item[] _gridSlots;

        // 缓存上次匹配结果
        private static (GriddedRecipe? Recipe, Dictionary<int, int> Consumptions, List<ReplacementAction> Replacements) _lastMatchResult;
        private static string _lastGridHash;
        private static int _lastTileId;
        private static int _lastGridWidth;
        private static int _lastGridHeight;

        private const bool EnableDebug = false;
        private Mod ModLog => EnableDebug ? TerraCraft.Instance : null;
        public GridCraftingMatcher(int tileId, int gridWidth, int gridHeight, Item[] gridSlots)
        {
            _tileId = tileId;
            _gridWidth = gridWidth;
            _gridHeight = gridHeight;
            _gridSlots = gridSlots;
        }

        public (GriddedRecipe? Recipe, Dictionary<int, int> Consumptions, List<ReplacementAction> Replacements) Match()
        {
            ModLog?.Logger.Debug($"[GridCrafting] Match() started. Tile: {_tileId} ({TileID.Search.GetName(_tileId)}), Grid: {_gridWidth}x{_gridHeight}");

            // 检查缓存
            string currentHash = CalculateGridHash();
            ModLog?.Logger.Debug($"[GridCrafting] Current grid hash: {currentHash}");

            if (currentHash == _lastGridHash &&
                _tileId == _lastTileId &&
                _gridWidth == _lastGridWidth &&
                _gridHeight == _lastGridHeight)
            {
                ModLog?.Logger.Debug("[GridCrafting] Cache HIT! Returning cached result.");
                return _lastMatchResult;
            }

            ModLog?.Logger.Debug("[GridCrafting] Cache miss. Performing full match...");

            // 更新缓存键
            _lastGridHash = currentHash;
            _lastTileId = _tileId;
            _lastGridWidth = _gridWidth;
            _lastGridHeight = _gridHeight;

            // 使用优化后的方法进行匹配
            _lastMatchResult = MatchWithCache();

            // 记录最终结果
            if (_lastMatchResult.Recipe != null)
                ModLog?.Logger.Debug($"[GridCrafting] Match SUCCESS. Recipe ID: {_lastMatchResult.Recipe.Value.Id}");
            else
                ModLog?.Logger.Debug("[GridCrafting] Match FAILED. No valid recipe found.");

            return _lastMatchResult;
        }

        public List<(GriddedRecipe Recipe, Dictionary<int, int> Consumptions, List<ReplacementAction> Replacements)> MatchAll()
        {
            var results = new List<(GriddedRecipe Recipe, Dictionary<int, int> Consumptions, List<ReplacementAction> Replacements)>();
            var seenIds = new HashSet<string>();

            var tileRecipes = GridRecipeLoader.RecipeDB.GetRecipesForTile(_tileId);
            if (tileRecipes.Count == 0)
                return results;

            foreach (var recipe in tileRecipes.Where(r => !r.Shaped))
            {
                var result = MatchShapeless(recipe);
                if (result != null && seenIds.Add(recipe.Id))
                    results.Add(result.Value);
            }

            var shapedRecipes = tileRecipes
                .Where(r => r.Shaped && r.GridWidth <= _gridWidth && r.GridHeight <= _gridHeight)
                .OrderByDescending(r => r.GridWidth * r.GridHeight)
                .ToList();

            foreach (var recipe in shapedRecipes)
            {
                var result = MatchShaped(recipe);
                if (result != null && seenIds.Add(recipe.Id))
                    results.Add(result.Value);
            }

            return results;
        }

        private (GriddedRecipe? Recipe, Dictionary<int, int> Consumptions, List<ReplacementAction> Replacements) MatchWithCache()
        {
            // 获取该工作台的配方列表（使用缓存）
            var tileRecipes = GridRecipeLoader.RecipeDB.GetRecipesForTile(_tileId);
            ModLog?.Logger.Debug($"[GridCrafting] Found {tileRecipes.Count} recipes for tile {_tileId}.");

            if (tileRecipes.Count == 0)
            {
                ModLog?.Logger.Debug("[GridCrafting] No recipes for this tile. Returning null.");
                return (null, null, null);
            }

            // 先检查无形状配方
            ModLog?.Logger.Debug("[GridCrafting] Trying shapeless recipes...");
            var shapelessResult = MatchShapelessRecipes(tileRecipes);
            if (shapelessResult.Recipe.HasValue)
            {
                ModLog?.Logger.Debug($"[GridCrafting] Shapeless match succeeded with recipe {shapelessResult.Recipe.Value.Id}");
                return shapelessResult;
            }

            // 检查有形状配方
            ModLog?.Logger.Debug("[GridCrafting] Trying shaped recipes...");
            return MatchShapedRecipes(tileRecipes);
        }

        private (GriddedRecipe? Recipe, Dictionary<int, int> Consumptions, List<ReplacementAction> Replacements) MatchShapelessRecipes(List<GriddedRecipe> tileRecipes)
        {
            var shapelessRecipes = tileRecipes.Where(r => !r.Shaped).ToList();
            ModLog?.Logger.Debug($"[GridCrafting] {shapelessRecipes.Count} shapeless recipes to test.");

            foreach (var recipe in shapelessRecipes)
            {
                ModLog?.Logger.Debug($"[GridCrafting] Testing shapeless recipe ID: {recipe.Id}");
                var result = MatchShapeless(recipe);
                if (result != null)
                    return result.Value;
                else
                    ModLog?.Logger.Debug($"[GridCrafting] Recipe {recipe.Id} did not match.");
            }
            ModLog?.Logger.Debug("[GridCrafting] No shapeless recipe matched.");
            return (null, null, null);
        }

        private (GriddedRecipe? Recipe, Dictionary<int, int> Consumptions, List<ReplacementAction> Replacements) MatchShapedRecipes(List<GriddedRecipe> tileRecipes)
        {
            // 获取适合当前网格尺寸的有形状配方
            var shapedRecipes = tileRecipes
                .Where(r => r.Shaped && r.GridWidth <= _gridWidth && r.GridHeight <= _gridHeight)
                .ToList();

            ModLog?.Logger.Debug($"[GridCrafting] {shapedRecipes.Count} shaped recipes fit within grid size.");

            // 按尺寸排序，大尺寸配方优先
            shapedRecipes.Sort((a, b) =>
            {
                int areaA = a.GridWidth * a.GridHeight;
                int areaB = b.GridWidth * b.GridHeight;
                return areaB.CompareTo(areaA);
            });

            foreach (var recipe in shapedRecipes)
            {
                ModLog?.Logger.Debug($"[GridCrafting] Testing shaped recipe ID: {recipe.Id} (Size: {recipe.GridWidth}x{recipe.GridHeight})");
                var result = MatchShaped(recipe);
                if (result != null)
                    return result.Value;
                else
                    ModLog?.Logger.Debug($"[GridCrafting] Recipe {recipe.Id} did not match.");
            }
            ModLog?.Logger.Debug("[GridCrafting] No shaped recipe matched.");
            return (null, null, null);
        }

        private bool IsTileAllowed(List<int> requiredTileIds)
        {
            if (requiredTileIds == null || requiredTileIds.Count == 0)
            {
                ModLog?.Logger.Debug($"[GridCrafting] Tile check: No required tiles → allowed.");
                return true;
            }

            bool allowed = TileCompatibilitySystem.IsTileCompatibleWithAny(_tileId, requiredTileIds);
            ModLog?.Logger.Debug($"[GridCrafting] Tile check: Tile {_tileId} compatibility with [{string.Join(",", requiredTileIds)}] → {allowed}");
            return allowed;
        }

        private (GriddedRecipe, Dictionary<int, int>, List<ReplacementAction>)? MatchShaped(GriddedRecipe recipe)
        {
            ModLog?.Logger.Debug($"[GridCrafting] MatchShaped for recipe {recipe.Id}");

            // 检查工作台兼容性
            if (!IsTileAllowed(recipe.RequiredTileIds))
                return null;

            int maxOffsetX = _gridWidth - recipe.GridWidth;
            int maxOffsetY = _gridHeight - recipe.GridHeight;
            ModLog?.Logger.Debug($"[GridCrafting] Possible offsets: X=0..{maxOffsetX}, Y=0..{maxOffsetY}");

            for (int offsetY = 0; offsetY <= maxOffsetY; offsetY++)
            {
                for (int offsetX = 0; offsetX <= maxOffsetX; offsetX++)
                {
                    ModLog?.Logger.Debug($"[GridCrafting] Trying offset ({offsetX},{offsetY})");
                    var result = TryMatchShapedAt(recipe, offsetX, offsetY);
                    if (result != null)
                    {
                        ModLog?.Logger.Debug($"[GridCrafting] Match succeeded at offset ({offsetX},{offsetY})");
                        return result;
                    }
                }
            }
            ModLog?.Logger.Debug($"[GridCrafting] No valid offset for recipe {recipe.Id}");
            return null;
        }

        private (GriddedRecipe, Dictionary<int, int>, List<ReplacementAction>)? TryMatchShapedAt(GriddedRecipe recipe, int offsetX, int offsetY)
        {
            var consumption = new Dictionary<int, int>();
            var replacements = new List<ReplacementAction>();

            for (int y = 0; y < _gridHeight; y++)
            {
                for (int x = 0; x < _gridWidth; x++)
                {
                    int slotIndex = y * _gridWidth + x;
                    Item slotItem = _gridSlots[slotIndex];

                    int recipeX = x - offsetX;
                    int recipeY = y - offsetY;
                    bool inRecipeArea = recipeX >= 0 && recipeX < recipe.GridWidth
                                    && recipeY >= 0 && recipeY < recipe.GridHeight;

                    if (!inRecipeArea)
                    {
                        // 配方区域外的格子必须为空
                        if (slotItem != null && !slotItem.IsAir)
                        {
                            ModLog?.Logger.Debug($"[GridCrafting]   Slot ({x},{y}) outside recipe area is not empty (item {slotItem.type} x{slotItem.stack}) → fail.");
                            return null;
                        }
                        continue;
                    }

                    // 查找配方中该位置的配料
                    var ing = recipe.Ingredients.FirstOrDefault(i => i.X == recipeX && i.Y == recipeY);
                    if (ing.Equals(default(RecipeIngredient)))
                    {
                        // 配方内无要求的位置必须为空
                        if (slotItem != null && !slotItem.IsAir)
                        {
                            ModLog?.Logger.Debug($"[GridCrafting]   Slot ({x},{y}) inside recipe area but no ingredient required, yet has item {slotItem.type} → fail.");
                            return null;
                        }
                        continue;
                    }

                    // 有配料要求，检查格子物品
                    if (!MatchesIngredient(slotItem, ing))
                    {
                        ModLog?.Logger.Debug($"[GridCrafting]   Slot ({x},{y}) item does not match ingredient: expected {(ing.ItemType != 0 ? $"ItemID {ing.ItemType}" : $"Group {ing.RecipeGroup}")} x{ing.Amount}, got {(slotItem?.IsAir == false ? $"{slotItem.type} x{slotItem.stack}" : "Air")} → fail.");
                        return null;
                    }
                    if (slotItem.stack < ing.Amount)
                    {
                        ModLog?.Logger.Debug($"[GridCrafting]   Slot ({x},{y}) stack {slotItem.stack} < required {ing.Amount} → fail.");
                        return null;
                    }

                    consumption[slotIndex] = ing.Amount;
                }
            }

            // 处理替换动作
            foreach (var rep in recipe.Replacements)
            {
                if (rep.X.HasValue && rep.Y.HasValue)
                {
                    int idx = (rep.Y.Value + offsetY) * _gridWidth + rep.X.Value + offsetX;
                    replacements.Add(new ReplacementAction
                    {
                        SlotIndex = idx,
                        ReplaceWithItem = rep.ReplaceWithType,
                        ReplaceAmount = rep.ReplaceAmount
                    });
                    ModLog?.Logger.Debug($"[GridCrafting]   Replacement planned at slot {idx} -> ItemID {rep.ReplaceWithType} x{rep.ReplaceAmount}");
                }
            }

            return (recipe, consumption, replacements);
        }

        private (GriddedRecipe, Dictionary<int, int>, List<ReplacementAction>)? MatchShapeless(GriddedRecipe recipe)
        {
            ModLog?.Logger.Debug($"[GridCrafting] MatchShapeless for recipe {recipe.Id}");

            if (!IsTileAllowed(recipe.RequiredTileIds))
                return null;

            // 收集所有非空格子
            var available = new List<(int slotIdx, Item item)>();
            for (int i = 0; i < _gridSlots.Length; i++)
            {
                if (!_gridSlots[i].IsAir)
                    available.Add((i, _gridSlots[i]));
            }
            ModLog?.Logger.Debug($"[GridCrafting] Grid has {available.Count} non-empty slots.");

            var requirements = recipe.Ingredients.Select(ing => new IngredientRequirement
            {
                Ingredient = ing,
                Needed = ing.Amount
            }).ToList();

            ModLog?.Logger.Debug($"[GridCrafting] Requirements: {requirements.Count} ingredient types.");

            var consumption = new Dictionary<int, int>();
            var usedSlots = new HashSet<int>();

            foreach (var req in requirements)
            {
                bool found = false;
                ModLog?.Logger.Debug($"[GridCrafting] Looking for ingredient: {(req.Ingredient.ItemType != 0 ? $"ItemID {req.Ingredient.ItemType}" : $"Group {req.Ingredient.RecipeGroup}")} x{req.Needed}");

                foreach (var (slotIdx, item) in available)
                {
                    if (usedSlots.Contains(slotIdx)) continue;
                    if (!MatchesIngredient(item, req.Ingredient)) continue;
                    if (item.stack < req.Needed) continue;

                    consumption[slotIdx] = req.Needed;
                    usedSlots.Add(slotIdx);
                    found = true;
                    ModLog?.Logger.Debug($"[GridCrafting]   Matched using slot {slotIdx} (item {item.type} x{item.stack})");
                    break;
                }
                if (!found)
                {
                    ModLog?.Logger.Debug($"[GridCrafting]   Could not match requirement → fail.");
                    return null;
                }
            }

            if (usedSlots.Count != available.Count) return null;

            var replacements = new List<ReplacementAction>();
            foreach (var rep in recipe.Replacements)
            {
                if (rep.OriginalItemType != 0)
                {
                    foreach (var (slotIdx, item) in available)
                    {
                        if (item.type == rep.OriginalItemType && consumption.ContainsKey(slotIdx))
                        {
                            replacements.Add(new ReplacementAction
                            {
                                SlotIndex = slotIdx,
                                ReplaceWithItem = rep.ReplaceWithType,
                                ReplaceAmount = rep.ReplaceAmount
                            });
                            ModLog?.Logger.Debug($"[GridCrafting]   Replacement planned for slot {slotIdx} (original item {rep.OriginalItemType}) -> ItemID {rep.ReplaceWithType} x{rep.ReplaceAmount}");
                            break;
                        }
                    }
                }
            }

            return (recipe, consumption, replacements);
        }

        // 计算网格哈希值用于缓存
        private string CalculateGridHash()
        {
            if (_gridSlots == null || _gridSlots.Length == 0)
                return "empty";

            var hashParts = new StringBuilder();
            for (int i = 0; i < _gridSlots.Length; i++)
            {
                var item = _gridSlots[i];
                if (item != null && !item.IsAir)
                {
                    hashParts.Append($"{i}:{item.type}:{item.stack};");
                }
            }
            return hashParts.ToString();
        }

        private bool MatchesIngredient(Item item, RecipeIngredient ing)
        {
            if (item == null || item.IsAir) return false;
            if (ing.ItemType != 0)
                return item.type == ing.ItemType;
            if (!string.IsNullOrEmpty(ing.RecipeGroup))
                return RecipeGroupResolver.ItemMatchesGroup(item.type, ing.RecipeGroup);
            return false;
        }

        public class ReplacementAction
        {
            public int SlotIndex;
            public int? ReplaceWithItem;
            public int ReplaceAmount;
        }

        private class IngredientRequirement
        {
            public RecipeIngredient Ingredient;
            public int Needed;
        }
    }
}