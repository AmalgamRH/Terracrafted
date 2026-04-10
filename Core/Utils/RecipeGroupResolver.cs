using System;
using System.Collections.Generic;
using Terraria;

namespace TerraCraft.Core.Utils
{
    public static class RecipeGroupResolver
    {
        /// <summary>
        /// 根据注册名（如 "Wood"）获取原版 RecipeGroup，返回其 ValidItems 集合
        /// </summary>
        /// <param name="groupName"></param>
        /// <returns></returns>
        /// <exception cref="Exception">Thrown when the recipe group does not exist.</exception>
        public static HashSet<int> GetRecipeGroupItems(string groupName)
        {
            if (RecipeGroup.recipeGroupIDs.TryGetValue(groupName, out int groupId))
            {
                if (RecipeGroup.recipeGroups.TryGetValue(groupId, out RecipeGroup group))
                    return group.ValidItems;
            }

            throw new Exception($"Recipe group '{groupName}' does not exist.");
        }

        /// <summary>
        /// 检查一个物品类型是否属于某 RecipeGroup
        /// </summary>
        /// <param name="itemType"></param>
        /// <param name="groupName"></param>
        /// <returns></returns>
        public static bool ItemMatchesGroup(int itemType, string groupName)
        {
            var groupItems = GetRecipeGroupItems(groupName);
            return groupItems.Contains(itemType);
        }
    }
}