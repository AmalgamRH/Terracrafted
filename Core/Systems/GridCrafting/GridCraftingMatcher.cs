using System.Collections.Generic;
using System.Linq;
using Terraria;
using TerraCraft.Core.Utils;
using TerraCraft.Core.DataStructures.GridCrafting;
using TerraCraft.Core.Loaders;
using Terraria.ID;

namespace TerraCraft.Core.Systems.GridCrafting
{
    public class GridCraftingMatcher
    {
        private readonly int _tileId;
        private readonly int _gridWidth;
        private readonly int _gridHeight;
        private readonly Item[] _gridSlots;

        public GridCraftingMatcher(int tileId, int gridWidth, int gridHeight, Item[] gridSlots)
        {
            _tileId = tileId;
            _gridWidth = gridWidth;
            _gridHeight = gridHeight;
            _gridSlots = gridSlots;
        }

        public (GriddedRecipe? Recipe, Dictionary<int, int> Consumptions, List<ReplacementAction> Replacements) Match()
        {
            foreach (var recipe in GridRecipeLoader.RecipeDB.Recipes)
            {
                if (!IsTileAllowed(recipe.RequiredTileIds))
                    continue;

                if (recipe.Shaped)
                {
                    if (recipe.GridWidth > _gridWidth || recipe.GridHeight > _gridHeight)
                        continue;

                    var result = MatchShaped(recipe);
                    if (result != null)
                        return result.Value;
                }
                else
                {
                    var result = MatchShapeless(recipe);
                    if (result != null)
                        return result.Value;
                }
            }
            return (null, null, null);
        }

        private bool IsTileAllowed(List<int> requiredTileIds)
        {
            if (requiredTileIds == null || requiredTileIds.Count == 0)
                return true;
            return requiredTileIds.Contains(_tileId);
        }

        private (GriddedRecipe, Dictionary<int, int>, List<ReplacementAction>)? MatchShaped(GriddedRecipe recipe)
        {
            int maxOffsetX = _gridWidth - recipe.GridWidth;
            int maxOffsetY = _gridHeight - recipe.GridHeight;

            for (int offsetY = 0; offsetY <= maxOffsetY; offsetY++)
            {
                for (int offsetX = 0; offsetX <= maxOffsetX; offsetX++)
                {
                    var result = TryMatchShapedAt(recipe, offsetX, offsetY);
                    if (result != null)
                        return result;
                }
            }
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
                        if (slotItem != null && !slotItem.IsAir)
                            return null;
                        continue;
                    }

                    var ing = recipe.Ingredients.FirstOrDefault(i => i.X == recipeX && i.Y == recipeY);
                    if (ing.Equals(default(Ingredient)))   // 注意 struct 默认比较
                    {
                        if (slotItem != null && !slotItem.IsAir)
                            return null;
                        continue;
                    }

                    if (!MatchesIngredient(slotItem, ing))
                        return null;
                    if (slotItem.stack < ing.Amount)
                        return null;

                    consumption[slotIndex] = ing.Amount;
                }
            }

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
                }
            }

            return (recipe, consumption, replacements);
        }

        private (GriddedRecipe, Dictionary<int, int>, List<ReplacementAction>)? MatchShapeless(GriddedRecipe recipe)
        {
            var available = new List<(int slotIdx, Item item)>();
            for (int i = 0; i < _gridSlots.Length; i++)
            {
                if (!_gridSlots[i].IsAir)
                    available.Add((i, _gridSlots[i]));
            }

            var requirements = recipe.Ingredients.Select(ing => new IngredientRequirement
            {
                Ingredient = ing,
                Needed = ing.Amount
            }).ToList();

            var consumption = new Dictionary<int, int>();
            var usedSlots = new HashSet<int>();

            foreach (var req in requirements)
            {
                bool found = false;
                foreach (var (slotIdx, item) in available)
                {
                    if (usedSlots.Contains(slotIdx)) continue;
                    if (!MatchesIngredient(item, req.Ingredient)) continue;
                    if (item.stack < req.Needed) continue;

                    consumption[slotIdx] = req.Needed;
                    usedSlots.Add(slotIdx);
                    found = true;
                    break;
                }
                if (!found) return null;
            }

            if (usedSlots.Count != available.Count) return null;

            var replacements = new List<ReplacementAction>();
            foreach (var rep in recipe.Replacements)
            {
                if (rep.OriginalItemType != 0)   // 有原始物品 ID
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
                            break;
                        }
                    }
                }
            }

            return (recipe, consumption, replacements);
        }

        private bool MatchesIngredient(Item item, Ingredient ing)
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
            public Ingredient Ingredient;
            public int Needed;
        }
    }
}