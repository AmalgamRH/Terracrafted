using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace TerraCraft.Core.Utils
{
    public static class RecipeGroupResolver
    {
        /// <summary>
        /// 根据 string 返回配方组的 ValidItems<br/>
        /// "Wood" / "AnyWood" - 原版配方组注册名<br/>
        /// "Terraria:Wood" - 明确指定原版（等同上面）<br/>
        /// "[ModName]:[GroupName]" - 模组配方组注册名<br/>
        /// </summary>
        public static HashSet<int> ParseRecipeGroupItems(string groupString)
        {
            if (string.IsNullOrWhiteSpace(groupString))
                throw new Exception("Recipe group string cannot be null or empty.");

            // 规范化：把"Terraria:Wood"转成"Wood"（原版组不带前缀存储）
            string resolvedName = groupString;
            if (groupString.Contains(':'))
            {
                var parts = groupString.Split(':');
                if (parts.Length != 2)
                    throw new Exception($"Invalid recipe group format: '{groupString}'. Expected '[ModName]:[GroupName]' or '[GroupName]'.");

                if (parts[0] == "Terraria")
                    resolvedName = parts[1]; // 原版去掉前缀
                                             // 模组组保持原样，如 "ExampleMod:AnyGem"
            }

            if (RecipeGroup.recipeGroupIDs.TryGetValue(resolvedName, out int groupId) &&
                RecipeGroup.recipeGroups.TryGetValue(groupId, out RecipeGroup group))
                return group.ValidItems;

            throw new Exception($"Recipe group '{groupString}' (resolved: '{resolvedName}') does not exist. " +
                                $"Make sure the mod is loaded and the group is registered.");
        }
        public static HashSet<int> GetRecipeGroupItems(string groupName)
            => ParseRecipeGroupItems(groupName);

        public static bool ItemMatchesGroup(int itemType, string groupName)
            => ParseRecipeGroupItems(groupName).Contains(itemType);

        public static string GetDisplayText(string groupName)
        {
            if (string.IsNullOrEmpty(groupName)) return null;

            string resolvedName = groupName;
            if (groupName.Contains(':'))
            {
                var parts = groupName.Split(':');
                if (parts.Length == 2 && parts[0] == "Terraria")
                    resolvedName = parts[1];
            }

            if (RecipeGroup.recipeGroupIDs.TryGetValue(resolvedName, out int id) &&
                RecipeGroup.recipeGroups.TryGetValue(id, out RecipeGroup group))
                return group.GetText();

            return groupName;
        }
    }
}