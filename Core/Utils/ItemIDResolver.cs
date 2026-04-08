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
        public static int ParseItemType(string idString)
        {
            // 如果输入是纯数字，直接解析为ID
            if (int.TryParse(idString, out int id))
                return id;

            // 检查是否包含命名空间分隔符
            if (idString.Contains(':'))
            {
                var parts = idString.Split(':');
                string modName = parts[0];
                string itemName = parts[1];

                if (modName == "Terraria") //原版命名空间
                {
                    if (int.TryParse(itemName, out int terrariaId))  //先尝试数字
                        return terrariaId;
                    return ResolveVanillaItemByName(itemName); //非数字按照名字
                }

                Mod mod = ModLoader.GetMod(modName); //其他模组
                if (mod != null)
                    return mod.Find<ModItem>(itemName)?.Type ?? 0;
                return 0;
            }

            // 如果是无命名空间的字符串，尝试按原版物品名解析
            return ResolveVanillaItemByName(idString);
        }

        private static int ResolveVanillaItemByName(string name)
        {
            FieldInfo field = typeof(ItemID).GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (field == null)
                return 0;

            object value = field.GetValue(null);
            if (value is short shortValue)
                return shortValue;
            if (value is int intValue)
                return intValue;

            return Convert.ToInt32(value);
        }
    }
}
