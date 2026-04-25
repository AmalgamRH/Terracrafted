using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Terraria.ID;

namespace TerraCraft.Core.Loaders
{
    #region DTO类（仅用于反序列化）
    internal class IngredientDTO
    {
        public int? X { get; set; }
        public int? Y { get; set; }
        public string ItemId { get; set; }
        public string RecipeGroup { get; set; }
        public int Amount { get; set; } = 1;
    }

    internal class OutputDTO
    {
        public string ItemId { get; set; }
        public int Amount { get; set; } = 1;
        public bool UseDurability { get; set; } = false;
        public int? MaxDurability { get; set; }
        public int? InitialDurability { get; set; }
    }

    internal class ReplacementDTO
    {
        public int? X { get; set; }
        public int? Y { get; set; }
        public string OriginalItemId { get; set; }
        public string ReplaceWith { get; set; }
        public int ReplaceAmount { get; set; } = 1;
    }

    // 表示Pattern单元格的DTO（可以是string或对象）
    [JsonConverter(typeof(PatternCellConverter))]
    internal class PatternCellDTO
    {
        public string ItemId { get; set; }
        public string RecipeGroup { get; set; }
        public int? Amount { get; set; }
    }

    internal class PatternCellConverter : JsonConverter<PatternCellDTO>
    {
        public override bool CanWrite => false; // 禁止写入，避免递归

        public override PatternCellDTO ReadJson(JsonReader reader, Type objectType, PatternCellDTO existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            // 处理字符串形式："ItemId"
            if (reader.TokenType == JsonToken.String)
            {
                string value = reader.Value?.ToString();
                if (string.IsNullOrEmpty(value))
                    return null;
                return new PatternCellDTO { ItemId = value };
            }

            // 处理对象形式：{ "ItemId": "...", "RecipeGroup": "...", "Amount": ... }
            if (reader.TokenType == JsonToken.StartObject)
            {
                JObject obj = JObject.Load(reader);
                var dto = new PatternCellDTO();
                if (obj.TryGetValue("ItemId", StringComparison.OrdinalIgnoreCase, out JToken itemIdToken))
                    dto.ItemId = itemIdToken.Value<string>();
                if (obj.TryGetValue("RecipeGroup", StringComparison.OrdinalIgnoreCase, out JToken groupToken))
                    dto.RecipeGroup = groupToken.Value<string>();
                if (obj.TryGetValue("Amount", StringComparison.OrdinalIgnoreCase, out JToken amountToken))
                    dto.Amount = amountToken.Value<int?>();
                return dto;
            }
            return null;
        }

        public override void WriteJson(JsonWriter writer, PatternCellDTO value, JsonSerializer serializer)
        {
            throw new NotImplementedException("Serialization is not required for PatternCellDTO.");
        }
    }


    // 增加Pattern字段，原有的Ingredients保留以兼容无序配方
    internal class GriddedRecipeDTO
    {
        public string Id { get; set; }
        public bool Shaped { get; set; } = true;
        public List<string> RequiredTiles { get; set; } = new List<string>();
        public List<List<PatternCellDTO>> Pattern { get; set; }  // 新增：二维网格
        public List<IngredientDTO> Ingredients { get; set; }     // 保留用于无序配方
        public List<OutputDTO> Outputs { get; set; }
        public List<ReplacementDTO> Replacements { get; set; } = new List<ReplacementDTO>();
        public List<string> Conditions { get; set; }
    }

    internal class TemplateDTO
    {
        public string Id { get; set; }
        public bool Shaped { get; set; } = true;
        public List<string> RequiredTiles { get; set; } = new List<string>();
        public List<List<PatternCellDTO>> Pattern { get; set; }
        public List<IngredientDTO> Ingredients { get; set; }
        public List<OutputDTO> Outputs { get; set; }
        public List<ReplacementDTO> Replacements { get; set; } = new List<ReplacementDTO>();
        public List<string> Conditions { get; set; }
    }

    // 旧格式模板组DTO（向后兼容）
    internal class LegacyTemplateGroupDTO
    {
        public string Id { get; set; }
        public TemplateDTO Template { get; set; }
        public List<Dictionary<string, string>> Variants { get; set; }
    }

    // 材料定义DTO
    internal class MaterialDefinitionDTO
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public List<Dictionary<string, string>> Materials { get; set; }
    }

    // 模板组DTO
    internal class TemplateGroupDTO
    {
        public string Id { get; set; }
        public string MaterialSource { get; set; }  // 指向MaterialDefinition的Id
        public TemplateDTO Template { get; set; }
        public Dictionary<string, string> PlaceholderMappings { get; set; }  // 模板占位符 -> 材料属性映射
    }

    // 完整数据库DTO
    internal class RecipeDatabaseDTO
    {
        public List<GriddedRecipeDTO> Recipes { get; set; }
        public List<MaterialDefinitionDTO> MaterialDefinitions { get; set; }
        public List<TemplateGroupDTO> RecipeGroups { get; set; }
    }

    // 旧格式：向后兼容的数据库DTO
    internal class LegacyRecipeDatabaseDTO
    {
        public List<GriddedRecipeDTO> Recipes { get; set; }
        public List<LegacyTemplateGroupDTO> RecipeGroups { get; set; }
    }
    #endregion
}
