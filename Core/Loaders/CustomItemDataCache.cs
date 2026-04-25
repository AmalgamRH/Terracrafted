using System.Collections.Generic;
using TerraCraft.Core.DataStructures.GridCrafting;
using TerraCraft.Core.DataStructures.Smelting;
using Terraria.ID;

namespace TerraCraft.Core.VanillaExt
{
    public static class CustomItemDataCache
    {
        public static HashSet<int> MaterialItemIds { get; private set; } = new();
        public static HashSet<int> FuelItemIds { get; private set; } = new();
        public static void LoadGridMaterialItem(List<GriddedRecipe> recipes)
        {
            MaterialItemIds.Clear();

            foreach (var recipe in recipes)
            {
                if (recipe.Ingredients == null) continue;

                foreach (var ing in recipe.Ingredients)
                {
                    if (ing.ItemType > 0)   //物品ID
                    {
                        MaterialItemIds.Add(ing.ItemType);
                    }
                    else if (!string.IsNullOrEmpty(ing.RecipeGroup))
                    {   // 配方组
                        try
                        {
                            var items = Utils.RecipeGroupResolver.GetRecipeGroupItems(ing.RecipeGroup);
                            foreach (int id in items)
                                MaterialItemIds.Add(id);
                        }
                        catch { }
                    }
                }
            }
            TerraCraft.Instance.Logger.Info($"Cached {MaterialItemIds.Count} material items.");
        }
        public static void LoadFuelItem(List<SmeltingFuel> fuels)
        {
            FuelItemIds.Clear();

            foreach (var fuel in fuels)
            {
                FuelItemIds.Add(fuel.ItemType);
            }
            TerraCraft.Instance.Logger.Info($"Cached {MaterialItemIds.Count} material items.");
        }
        public static void UnloadGridMaterialItem()
        {
            MaterialItemIds.Clear();
        }
        public static void UnloadFuelItem()
        {
            FuelItemIds.Clear();
        }
    }
}