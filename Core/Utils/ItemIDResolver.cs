using System;
using System.Reflection;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraCraft.Core.Utils
{
    public static class ItemIDResolver
    {
        /// <summary>
        /// 根据 string 返回物品ID<br/>
        /// "[id]" - 直接使用物品id，对于非原版风险性大<br/>
        /// "Terraria:[id]/[ItemID]" - 使用原版ItemID的名字或者id<br/>
        /// "[ModName]:[ItemTypeName]" - 使用模组物品的类名<br/>
        /// </summary>
        /// <param name="idString"></param>
        /// <returns></returns>
        /// <exception cref="Exception">Thrown when the item ID string cannot be resolved.</exception>
        public static int ParseItemType(string idString)
        {
            // If input is a pure number, parse directly as ID
            if (int.TryParse(idString, out int id))
                return id;

            // Check if it contains a namespace separator
            if (idString.Contains(':'))
            {
                var parts = idString.Split(':');
                if (parts.Length != 2)
                    throw new Exception($"Invalid item ID format: '{idString}'. Expected format '[ModName]:[ItemName]' or '[id]'.");

                string modName = parts[0];
                string itemName = parts[1];

                if (modName == "Terraria") // Vanilla namespace
                {
                    if (int.TryParse(itemName, out int terrariaId))
                        return terrariaId;

                    return ResolveVanillaItemByName(itemName);
                }

                // Other mods
                Mod mod = ModLoader.GetMod(modName);
                if (mod == null)
                    throw new Exception($"Mod '{modName}' is not loaded. Item ID string: '{idString}'.");

                int? type = mod.Find<ModItem>(itemName)?.Type;
                if (type == null || type == 0)
                    throw new Exception($"Item '{itemName}' not found in mod '{modName}'. Item ID string: '{idString}'.");

                return type.Value;
            }

            // No namespace: treat as vanilla item name
            return ResolveVanillaItemByName(idString);
        }

        private static int ResolveVanillaItemByName(string name)
        {
            FieldInfo field = typeof(ItemID).GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (field == null)
                throw new Exception($"Vanilla item '{name}' does not exist in ItemID.");

            object value = field.GetValue(null);
            if (value is short shortValue)
                return shortValue;
            if (value is int intValue)
                return intValue;

            try
            {
                return Convert.ToInt32(value);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to convert vanilla item field '{name}' to int. Value type: {value?.GetType().Name}.", ex);
            }
        }
    }
}