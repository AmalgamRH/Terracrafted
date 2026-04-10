using System;
using System.Reflection;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraCraft.Core.Utils
{
    public static class TileIDResolver
    {
        /// <summary>
        /// 根据 string 返回方块ID<br/>
        /// "[id]" - 直接使用方块id，对于非原版风险性大<br/>
        /// "Terraria:[id]/[TileID]" - 使用原版TileID的名字或者id<br/>
        /// "[ModName]:[TileTypeName]" - 使用模组方块的类名<br/>
        /// </summary>
        /// <param name="idString"></param>
        /// <returns></returns>
        /// <exception cref="Exception">Thrown when the tile ID string cannot be resolved.</exception>
        public static int ParseTileType(string idString)
        {
            // If input is a pure number, parse directly as ID
            if (int.TryParse(idString, out int id))
                return id;

            // Check if it contains a namespace separator
            if (idString.Contains(':'))
            {
                var parts = idString.Split(':');
                if (parts.Length != 2)
                    throw new Exception($"Invalid tile ID format: '{idString}'. Expected format '[ModName]:[TileName]' or '[id]'.");

                string modName = parts[0];
                string tileName = parts[1];

                if (modName == "Terraria") // Vanilla namespace
                {
                    if (int.TryParse(tileName, out int terrariaId))
                        return terrariaId;

                    return ResolveVanillaTileByName(tileName);
                }

                // Other mods
                Mod mod = ModLoader.GetMod(modName);
                if (mod == null)
                    throw new Exception($"Mod '{modName}' is not loaded. Tile ID string: '{idString}'.");

                int? type = mod.Find<ModTile>(tileName)?.Type;
                if (type == null || type == 0)
                    throw new Exception($"Tile '{tileName}' not found in mod '{modName}'. Tile ID string: '{idString}'.");

                return type.Value;
            }

            // No namespace: treat as vanilla tile name
            return ResolveVanillaTileByName(idString);
        }

        private static int ResolveVanillaTileByName(string name)
        {
            FieldInfo field = typeof(TileID).GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (field == null)
                throw new Exception($"Vanilla tile '{name}' does not exist in TileID.");

            object value = field.GetValue(null);
            if (value is ushort ushortValue)
                return ushortValue;
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
                throw new Exception($"Failed to convert vanilla tile field '{name}' to int. Value type: {value?.GetType().Name}.", ex);
            }
        }
    }
}