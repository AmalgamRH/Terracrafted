using System;
using System.Numerics;
using System.Reflection;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

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
        public static int ParseTileType(string idString)
        {
            // 如果输入是纯数字，直接解析为ID
            if (int.TryParse(idString, out int id))
                return id;

            // 检查是否包含命名空间分隔符
            if (idString.Contains(':'))
            {
                var parts = idString.Split(':');
                string modName = parts[0];
                string tileName = parts[1];

                if (modName == "Terraria") //原版命名空间
                {
                    if (int.TryParse(tileName, out int terrariaId))  //先尝试数字
                        return terrariaId;
                    return ResolveVanillaTileByName(tileName); //非数字按照名字
                }

                Mod mod = ModLoader.GetMod(modName); //其他模组
                if (mod != null)
                    return mod.Find<ModTile>(tileName)?.Type ?? 0;
                return 0;
            }

            // 如果是无命名空间的字符串，尝试按原版方块名解析
            return ResolveVanillaTileByName(idString);
        }

        private static int ResolveVanillaTileByName(string name)
        {
            FieldInfo field = typeof(TileID).GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (field == null)
                return 0;

            object value = field.GetValue(null);
            if (value is ushort ushortValue)
                return ushortValue;
            if (value is short shortValue)
                return shortValue;
            if (value is int intValue)
                return intValue;

            return Convert.ToInt32(value);
        }
    }
}