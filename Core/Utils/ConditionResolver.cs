using System;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace TerraCraft.Core.Utils
{
    /// <summary>
    /// 对一个 Condition 的"引用描述"，可序列化存储。<br/>
    /// 格式示例：<br/>
    ///   "InJungle"                          → Condition.InJungle（无参静态字段）<br/>
    ///   "Terraria:InJungle"                 → 同上，明确命名空间<br/>
    ///   "PlayerCarriesItem(71)"             → Condition.PlayerCarriesItem(71)（带 int 参数）<br/>
    ///   "BestiaryFilledPercent(50)"         → Condition.BestiaryFilledPercent(50)<br/>
    ///   "ExampleMod:ExampleConditions.InExampleBiome" → 模组注册的静态Condition<br/>
    ///   "ExampleMod:ExampleConditions.SomeCondition(42)" → 模组注册的动态Condition<br/>
    /// </summary>
    public static class ConditionResolver
    {
        public static Condition Parse(string conditionString)
        {
            if (string.IsNullOrWhiteSpace(conditionString))
                throw new Exception("Condition string cannot be null or empty.");

            string modName = "Terraria";
            string body = conditionString;

            if (conditionString.Contains(':'))
            {
                var parts = conditionString.Split(':', 2);
                modName = parts[0];
                body = parts[1];
            }

            // 拆类名（仅模组需要）和成员名
            // 原版：body = "InJungle" 或 "PlayerCarriesItem(71)"
            // 模组：body = "ExampleConditions.InExampleBiome"
            string className = null;
            string memberBody = body;

            int dotIdx = body.LastIndexOf('.'); // 用 LastIndexOf 防止类名本身带点
            int parenIdx = body.IndexOf('(');
            // 只有在点出现在括号之前（或没有括号）时才认为有类名
            if (dotIdx >= 0 && (parenIdx < 0 || dotIdx < parenIdx))
            {
                className = body[..dotIdx];
                memberBody = body[(dotIdx + 1)..];
            }

            // 拆成员名和参数
            string memberName;
            object[] rawArgs;

            int parenStart = memberBody.IndexOf('(');
            if (parenStart >= 0)
            {
                if (!memberBody.EndsWith(")"))
                    throw new Exception($"Invalid condition format (unclosed parenthesis): '{conditionString}'.");
                memberName = memberBody[..parenStart];
                string argsStr = memberBody[(parenStart + 1)..^1];
                rawArgs = string.IsNullOrWhiteSpace(argsStr)
                    ? Array.Empty<object>()
                    : argsStr.Split(',').Select(s => (object)s.Trim()).ToArray();
            }
            else
            {
                memberName = memberBody;
                rawArgs = Array.Empty<object>();
            }

            // 定位 Type
            Type type;
            if (modName == "Terraria")
            {
                type = typeof(Condition); // 原版直接定位
            }
            else
            {
                if (className == null)
                    throw new Exception(
                        $"Mod conditions require a class name: '[ModName]:[ClassName].[Member]'. Got: '{conditionString}'.");

                Mod mod = ModLoader.GetMod(modName)
                    ?? throw new Exception($"Mod '{modName}' is not loaded. Condition: '{conditionString}'.");

                // 在该 mod 的程序集里找类
                type = mod.Code.GetType(className)           // 尝试直接全名
                    ?? mod.Code.GetTypes()
                           .FirstOrDefault(t => t.Name == className)
                    ?? throw new Exception(
                        $"Class '{className}' not found in mod '{modName}'. Condition: '{conditionString}'.");
            }

            return ResolveMember(type, memberName, rawArgs, conditionString);
        }

        private static Condition ResolveMember(Type type, string memberName, object[] rawArgs, string original)
        {
            if (rawArgs.Length == 0)
            {
                // 无参 → 静态字段
                var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Static);
                if (field != null && field.FieldType == typeof(Condition))
                    return (Condition)field.GetValue(null);

                throw new Exception($"Condition field '{memberName}' not found on '{type.FullName}'. Original: '{original}'.");
            }
            else
            {
                // 有参 → 静态方法，按参数数量匹配
                var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == memberName && m.GetParameters().Length == rawArgs.Length)
                    ?? throw new Exception(
                        $"Condition method '{memberName}' with {rawArgs.Length} arg(s) not found on '{type.FullName}'. Original: '{original}'.");

                var parameters = method.GetParameters();
                var converted = new object[rawArgs.Length];
                for (int i = 0; i < rawArgs.Length; i++)
                    converted[i] = Convert.ChangeType(rawArgs[i], parameters[i].ParameterType);

                return (Condition)method.Invoke(null, converted);
            }
        }

        /// <summary>
        /// 将字符串参数列表解析为 object[]（暂时全部存为 string，后续按目标类型转换）
        /// </summary>
        private static object[] ParseArgs(string argsStr)
        {
            if (string.IsNullOrWhiteSpace(argsStr))
                return Array.Empty<object>();

            var parts = argsStr.Split(',');
            var result = new object[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                result[i] = parts[i].Trim(); // 先存为 string
            return result;
        }

        private static object[] ConvertArgs(object[] rawArgs, ParameterInfo[] parameters, string original)
        {
            var converted = new object[rawArgs.Length];
            for (int i = 0; i < rawArgs.Length; i++)
            {
                string raw = (string)rawArgs[i];
                var targetType = parameters[i].ParameterType;
                try
                {
                    converted[i] = Convert.ChangeType(raw, targetType);
                }
                catch (Exception ex)
                {
                    throw new Exception(
                        $"Cannot convert argument '{raw}' to {targetType.Name} " +
                        $"for parameter '{parameters[i].Name}'. Original: '{original}'.", ex);
                }
            }
            return converted;
        }
    }
}