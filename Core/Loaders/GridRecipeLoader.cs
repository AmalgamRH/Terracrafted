using System.Collections.Generic;
using System.Linq;
using Terraria;
using TerraCraft.Core.Utils;
using Terraria.ModLoader;
using Newtonsoft.Json;
using System.IO;
using System;
using TerraCraft.Core.DataStructures.GridCrafting;

namespace TerraCraft.Core.Loaders
{
    public class GridRecipeLoader : ModSystem
    {
        public const string AssetPath = "Assets/Recipes/";
        public static string FilePath = Path.Combine(Path.GetDirectoryName(ModLoader.ModPath), "TerraCraft", "Recipes");
        public static RecipeDatabase RecipeDB { get; private set; }

        #region DTO 类（仅用于反序列化）
        private class IngredientDTO
        {
            public int? X { get; set; }
            public int? Y { get; set; }
            public string ItemId { get; set; }
            public string RecipeGroup { get; set; }
            public int Amount { get; set; } = 1;
        }

        private class OutputDTO
        {
            public string ItemId { get; set; }
            public int Amount { get; set; } = 1;
            public bool UseDurability { get; set; } = false;
            public int? MaxDurability { get; set; }
            public int? InitialDurability { get; set; }
        }

        private class ReplacementDTO
        {
            public int? X { get; set; }
            public int? Y { get; set; }
            public string OriginalItemId { get; set; }
            public string ReplaceWith { get; set; }
            public int ReplaceAmount { get; set; } = 1;
        }

        // 表示 Pattern 单元格的 DTO（可以是 string 或对象）
        [JsonConverter(typeof(PatternCellConverter))]
        private class PatternCellDTO
        {
            public string ItemId { get; set; }
            public string RecipeGroup { get; set; }
            public int? Amount { get; set; }  // 可选，默认 1
        }
        private class PatternCellConverter : JsonConverter<PatternCellDTO>
        {
            public override PatternCellDTO ReadJson(JsonReader reader, Type objectType, PatternCellDTO existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.String)
                {
                    string value = reader.Value.ToString();
                    if (string.IsNullOrEmpty(value))
                        return null;
                    return new PatternCellDTO { ItemId = value };
                }
                else if (reader.TokenType == JsonToken.StartObject)
                {
                    return serializer.Deserialize<PatternCellDTO>(reader);
                }
                return null;
            }

            public override void WriteJson(JsonWriter writer, PatternCellDTO value, JsonSerializer serializer)
            {
                serializer.Serialize(writer, value);
            }
        }

        // 修改 GriddedRecipeDTO，增加 Pattern 字段，废弃原有的 Ingredients（但仍保留以兼容无序配方）
        private class GriddedRecipeDTO
        {
            public string Id { get; set; }
            public bool Shaped { get; set; } = true;
            public List<string> RequiredTiles { get; set; } = new List<string>();
            public List<List<PatternCellDTO>> Pattern { get; set; }  // 新增：二维网格
            public List<IngredientDTO> Ingredients { get; set; }     // 保留用于无序配方
            public List<OutputDTO> Outputs { get; set; }
            public List<ReplacementDTO> Replacements { get; set; } = new List<ReplacementDTO>();
        }

        private class TemplateDTO
        {
            public string Id { get; set; }
            public bool Shaped { get; set; } = true;
            public List<string> RequiredTiles { get; set; } = new List<string>();
            public List<List<PatternCellDTO>> Pattern { get; set; }
            public List<IngredientDTO> Ingredients { get; set; }
            public List<OutputDTO> Outputs { get; set; }
            public List<ReplacementDTO> Replacements { get; set; } = new List<ReplacementDTO>();
        }

        private class TemplateGroupDTO
        {
            public string Id { get; set; }
            public TemplateDTO Template { get; set; }
            public List<Dictionary<string, string>> Variants { get; set; }
        }

        private class RecipeDatabaseDTO
        {
            public List<GriddedRecipeDTO> Recipes { get; set; }
            public List<TemplateGroupDTO> RecipeGroups { get; set; }
        }
        #endregion

        // 等待其他模组物品id全部加载后
        public override void PostAddRecipes()
        {
            var allRecipes = new List<GriddedRecipe>();

            // 加载内嵌资源
            foreach (string assetPath in Mod.GetFileNames()
                         .Where(p => p.StartsWith(AssetPath) && p.EndsWith(".json")))
            {
                try
                {
                    using Stream stream = Mod.GetFileStream(assetPath);
                    using StreamReader reader = new StreamReader(stream);
                    var dbDTO = JsonConvert.DeserializeObject<RecipeDatabaseDTO>(reader.ReadToEnd());
                    ProcessRecipeDatabase(dbDTO, allRecipes);
                }
                catch (Exception e)
                {
                    Mod.Logger.Warn($"[TerraCraft] 加载内嵌配方失败: {assetPath}\n{e.Message}");
                }
            }

            // 加载外部目录
            if (Directory.Exists(FilePath))
            {
                foreach (string filePath in Directory.GetFiles(FilePath, "*.json", SearchOption.AllDirectories))
                {
                    try
                    {
                        string json = File.ReadAllText(filePath);
                        var dbDTO = JsonConvert.DeserializeObject<RecipeDatabaseDTO>(json);
                        ProcessRecipeDatabase(dbDTO, allRecipes);
                    }
                    catch (Exception e)
                    {
                        Mod.Logger.Warn($"[TerraCraft] 加载外部配方失败: {filePath}\n{e.Message}");
                    }
                }
            }

            RecipeDB = new RecipeDatabase { Recipes = allRecipes };
        }

        private void ProcessRecipeDatabase(RecipeDatabaseDTO dbDTO, List<GriddedRecipe> allRecipes)
        {
            if (dbDTO == null) return;

            // 处理普通配方
            if (dbDTO.Recipes != null)
            {
                foreach (var recipeDTO in dbDTO.Recipes)
                {
                    var converted = ConvertToStruct(recipeDTO);
                    if (converted.HasValue)
                        allRecipes.Add(converted.Value);
                }
            }

            // 处理配方模板组
            if (dbDTO.RecipeGroups != null)
            {
                foreach (var group in dbDTO.RecipeGroups)
                {
                    var generated = GenerateRecipesFromTemplate(group);
                    allRecipes.AddRange(generated);
                }
            }
        }

        #region 模板生成逻辑
        private List<GriddedRecipe> GenerateRecipesFromTemplate(TemplateGroupDTO group)
        {
            var results = new List<GriddedRecipe>();
            if (group.Template == null || group.Variants == null) return results;

            foreach (var variant in group.Variants)
            {
                var recipeDTO = CloneTemplateWithReplacements(group.Template, variant);
                if (recipeDTO != null)
                {
                    var converted = ConvertToStruct(recipeDTO);
                    if (converted.HasValue)
                        results.Add(converted.Value);
                }
            }
            return results;
        }

        private GriddedRecipeDTO CloneTemplateWithReplacements(TemplateDTO template, Dictionary<string, string> replacements)
        {
            var dto = new GriddedRecipeDTO
            {
                Id = ReplacePlaceholders(template.Id, replacements),
                Shaped = template.Shaped,
                RequiredTiles = new List<string>(template.RequiredTiles),
                Ingredients = new List<IngredientDTO>(),
                Outputs = new List<OutputDTO>(),
                Replacements = new List<ReplacementDTO>()
            };

            // 替换原料
            if (template.Ingredients != null)
            {
                foreach (var ing in template.Ingredients)
                {
                    dto.Ingredients.Add(new IngredientDTO
                    {
                        X = ing.X,
                        Y = ing.Y,
                        ItemId = ReplacePlaceholders(ing.ItemId, replacements),
                        RecipeGroup = ing.RecipeGroup,
                        Amount = ing.Amount
                    });
                }
            }

            // 替换产出
            foreach (var outDTO in template.Outputs)
            {
                dto.Outputs.Add(new OutputDTO
                {
                    ItemId = ReplacePlaceholders(outDTO.ItemId, replacements),
                    Amount = outDTO.Amount,
                    UseDurability = outDTO.UseDurability,
                    MaxDurability = outDTO.MaxDurability,
                    InitialDurability = outDTO.InitialDurability
                });
            }

            // 替换替换规则
            foreach (var rep in template.Replacements)
            {
                dto.Replacements.Add(new ReplacementDTO
                {
                    X = rep.X,
                    Y = rep.Y,
                    OriginalItemId = ReplacePlaceholders(rep.OriginalItemId, replacements),
                    ReplaceWith = ReplacePlaceholders(rep.ReplaceWith, replacements),
                    ReplaceAmount = rep.ReplaceAmount
                });
            }

            // 替换 RequiredTiles 中的占位符
            for (int i = 0; i < dto.RequiredTiles.Count; i++)
            {
                dto.RequiredTiles[i] = ReplacePlaceholders(dto.RequiredTiles[i], replacements);
            }

            // 复制 Pattern
            if (template.Pattern != null && template.Pattern.Any())
            {
                dto.Pattern = new List<List<PatternCellDTO>>();
                foreach (var row in template.Pattern)
                {
                    var newRow = new List<PatternCellDTO>();
                    foreach (var cell in row)
                    {
                        if (cell == null)
                        {
                            newRow.Add(null);
                            continue;
                        }
                        var newCell = new PatternCellDTO
                        {
                            ItemId = ReplacePlaceholders(cell.ItemId, replacements),
                            RecipeGroup = ReplacePlaceholders(cell.RecipeGroup, replacements),
                            Amount = cell.Amount
                        };
                        newRow.Add(newCell);
                    }
                    dto.Pattern.Add(newRow);
                }
            }

            return dto;
        }

        private string ReplacePlaceholders(string input, Dictionary<string, string> replacements)
        {
            if (string.IsNullOrEmpty(input)) return input;
            foreach (var kv in replacements)
            {
                input = input.Replace("{" + kv.Key + "}", kv.Value);
            }
            return input;
        }
        #endregion


        /// <summary>
        /// 从 Pattern 二维数组生成 Ingredients 列表，并自动设置 GridWidth/Height
        /// </summary>
        private List<Ingredient> ParsePattern(List<List<PatternCellDTO>> pattern, out int width, out int height)
        {
            var ingredients = new List<Ingredient>();
            height = pattern.Count;
            width = height > 0 ? pattern[0].Count : 0;

            // 确保所有行长度一致（取最大宽度）
            foreach (var row in pattern)
            {
                if (row.Count > width) width = row.Count;
            }

            for (int y = 0; y < pattern.Count; y++)
            {
                var row = pattern[y];
                for (int x = 0; x < width; x++)
                {
                    PatternCellDTO cell = null;
                    if (x < row.Count)
                        cell = row[x];

                    // 空单元格（null 或 ItemId/RecipeGroup 都为空）
                    if (cell == null || (string.IsNullOrEmpty(cell.ItemId) && string.IsNullOrEmpty(cell.RecipeGroup)))
                        continue;

                    int amount = cell.Amount ?? 1;
                    ingredients.Add(new Ingredient
                    {
                        X = x,
                        Y = y,
                        ItemType = string.IsNullOrEmpty(cell.ItemId) ? 0 : ItemIDResolver.ParseItemType(cell.ItemId),
                        RecipeGroup = cell.RecipeGroup,
                        Amount = amount
                    });
                }
            }

            return ingredients;
        }

        /// <summary>
        /// 将 DTO 转换为运行时 struct，同时将字符串 ID 解析为整数 ID
        /// </summary>
        private GriddedRecipe? ConvertToStruct(GriddedRecipeDTO dto)
        {
            if (dto == null) return null;

            // 转换 RequiredTiles
            List<int> tileIds = new List<int>();
            if (dto.RequiredTiles != null)
            {
                foreach (string tileStr in dto.RequiredTiles)
                {
                    int id = TileIDResolver.ParseTileType(tileStr);
                    if (id != 0) tileIds.Add(id);
                }
            }

            List<Ingredient> ingredients = new List<Ingredient>();
            int gridWidth = 1;   // 默认尺寸，仅在 Shaped = true 且无 Pattern 时可能被 AutoComputeDimensions 覆盖
            int gridHeight = 1;

            // 优先使用 Pattern（适用于 Shaped 配方）
            if (dto.Shaped && dto.Pattern != null && dto.Pattern.Count > 0)
            {
                ingredients = ParsePattern(dto.Pattern, out gridWidth, out gridHeight);
            }
            else if (dto.Ingredients != null)
            {
                // 兼容旧的坐标式（Shaped）或无序配方（Shaped = false）
                foreach (var ingDTO in dto.Ingredients)
                {
                    int itemType = 0;
                    if (!string.IsNullOrEmpty(ingDTO.ItemId))
                        itemType = ItemIDResolver.ParseItemType(ingDTO.ItemId);

                    ingredients.Add(new Ingredient
                    {
                        X = ingDTO.X,
                        Y = ingDTO.Y,
                        ItemType = itemType,
                        RecipeGroup = ingDTO.RecipeGroup,
                        Amount = ingDTO.Amount
                    });
                }
            }

            // 转换 Outputs
            List<Output> outputs = new List<Output>();
            if (dto.Outputs != null)
            {
                foreach (var outDTO in dto.Outputs)
                {
                    int itemType = ItemIDResolver.ParseItemType(outDTO.ItemId);
                    outputs.Add(new Output
                    {
                        ItemType = itemType,
                        Amount = outDTO.Amount,
                        UseDurability = outDTO.UseDurability,
                        MaxDurability = outDTO.MaxDurability,
                        InitialDurability = outDTO.InitialDurability
                    });
                }
            }

            // 转换 Replacements
            List<Replacement> replacements = new List<Replacement>();
            if (dto.Replacements != null)
            {
                foreach (var repDTO in dto.Replacements)
                {
                    int originalType = 0;
                    if (!string.IsNullOrEmpty(repDTO.OriginalItemId))
                        originalType = ItemIDResolver.ParseItemType(repDTO.OriginalItemId);

                    int? replaceWithType = null;
                    if (!string.IsNullOrEmpty(repDTO.ReplaceWith))
                        replaceWithType = ItemIDResolver.ParseItemType(repDTO.ReplaceWith);

                    replacements.Add(new Replacement
                    {
                        X = repDTO.X,
                        Y = repDTO.Y,
                        OriginalItemType = originalType,
                        ReplaceWithType = replaceWithType,
                        ReplaceAmount = repDTO.ReplaceAmount
                    });
                }
            }

            // 如果是有序配方但未使用 Pattern，且 Ingredients 中有坐标，自动计算尺寸
            if (dto.Shaped && (dto.Pattern == null || dto.Pattern.Count == 0))
            {
                var tempRecipe = new GriddedRecipe
                {
                    Id = dto.Id,
                    GridWidth = 0,
                    GridHeight = 0,
                    Shaped = true,
                    Ingredients = ingredients,
                };
                AutoComputeDimensions(ref tempRecipe);
                gridWidth = tempRecipe.GridWidth;
                gridHeight = tempRecipe.GridHeight;
            }

            var recipe = new GriddedRecipe
            {
                Id = dto.Id,
                GridWidth = gridWidth,
                GridHeight = gridHeight,
                Shaped = dto.Shaped,
                RequiredTileIds = tileIds,
                Ingredients = ingredients,
                Outputs = outputs,
                Replacements = replacements
            };

            return recipe;
        }

        private void AutoComputeDimensions(ref GriddedRecipe recipe)
        {
            if (!recipe.Shaped) return;
            if (recipe.Ingredients == null || recipe.Ingredients.Count == 0)
            {
                recipe.GridWidth = 0;
                recipe.GridHeight = 0;
                return;
            }

            int maxX = -1, maxY = -1;
            foreach (var ing in recipe.Ingredients)
            {
                if (ing.X.HasValue && ing.X.Value > maxX) maxX = ing.X.Value;
                if (ing.Y.HasValue && ing.Y.Value > maxY) maxY = ing.Y.Value;
            }

            if (maxX == -1 || maxY == -1)
            {
                recipe.GridWidth = 0;
                recipe.GridHeight = 0;
                return;
            }

            recipe.GridWidth = maxX + 1;
            recipe.GridHeight = maxY + 1;
        }
    }
}