using System.Collections.Generic;
using System.Linq;
using Terraria;
using TerraCraft.Core.Utils;
using Terraria.ModLoader;
using Newtonsoft.Json;
using System.IO;
using System;
using TerraCraft.Core.DataStructures.GridCrafting;
using Terraria.ID;
using TerraCraft.Core.VanillaExt;

namespace TerraCraft.Core.Loaders
{
    public class GridRecipeLoader : ModSystem
    {
        public const string AssetPath = "Assets/Recipes/";
        public static string FilePath = Path.Combine(Path.GetDirectoryName(ModLoader.ModPath), "TerraCraft", "Recipes");
        public static RecipeDatabase RecipeDB { get; private set; }

        // 在PostAddRecipes()加载，等待其他模组物品id全部加载完毕
        public override void PostAddRecipes()
        {
            LoadGridRecipes();
        }
        public void LoadGridRecipes()
        {
            var allRecipes = new List<GriddedRecipe>();

            // 加载嵌入式资源
            foreach (string assetPath in Mod.GetFileNames()
                         .Where(p => p.StartsWith(AssetPath) && p.EndsWith(".json")))
            {
                try
                {
                    using Stream stream = Mod.GetFileStream(assetPath);
                    using StreamReader reader = new StreamReader(stream);
                    string jsonContent = reader.ReadToEnd();
                    ProcessJsonContent(jsonContent, assetPath, allRecipes);
                }
                catch (Exception e)
                {
                    Mod.Logger.Warn($"[TerraCraft] 加载嵌入式配方失败: {assetPath}\n{e.Message}");
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
                        ProcessJsonContent(json, filePath, allRecipes);
                    }
                    catch (Exception e)
                    {
                        Mod.Logger.Warn($"[TerraCraft] 加载外部配方失败: {filePath}\n{e.Message}");
                    }
                }
            }

            RecipeDB = new RecipeDatabase { Recipes = allRecipes };
            // 初始化缓存
            RecipeDB.InitializeCache();

            CustomItemDataCache.LoadGridMaterialItem(allRecipes);

            Mod.Logger.Info($"[GridRecipeLoader] Successfully loaded {allRecipes.Count} grid recipes");
        }
        public override void Unload()
        {
            CustomItemDataCache.UnloadGridMaterialItem();
            RecipeDB = null;
        }
        // 处理JSON内容，自动检测格式
        private void ProcessJsonContent(string jsonContent, string sourcePath, List<GriddedRecipe> allRecipes)
        {
            try // 尝试解析为新格式
            {
                var dbDTO = JsonConvert.DeserializeObject<RecipeDatabaseDTO>(jsonContent);
                if (dbDTO != null)
                {
                    ProcessRecipeDatabase(dbDTO, allRecipes);
                    return;
                }
            }
            catch { } // 新格式解析失败，尝试旧格式

            try // 尝试解析为旧格式
            {
                var legacyDbDTO = JsonConvert.DeserializeObject<LegacyRecipeDatabaseDTO>(jsonContent);
                if (legacyDbDTO != null)
                {
                    ProcessLegacyRecipeDatabase(legacyDbDTO, allRecipes);
                    return;
                }
            }
            catch (Exception e)
            {
                Mod.Logger.Warn($"[TerraCraft] 无法解析JSON文件: {sourcePath}\n{e.Message}");
            }
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

            // 处理模板配方（新格式）
            if (dbDTO.MaterialDefinitions != null && dbDTO.RecipeGroups != null)
            {
                // 构建材料映射
                var materialDefs = dbDTO.MaterialDefinitions.ToDictionary(md => md.Id, md => md);
                
                foreach (var group in dbDTO.RecipeGroups)
                {
                    if (string.IsNullOrEmpty(group.MaterialSource) || !materialDefs.ContainsKey(group.MaterialSource))
                    {
                        Mod.Logger.Warn($"[TerraCraft] 配方组 {group.Id} 引用了不存在的材料源: {group.MaterialSource}");
                        continue;
                    }

                    var materialDef = materialDefs[group.MaterialSource];
                    var generated = GenerateRecipesFromMaterialGroup(group, materialDef);
                    allRecipes.AddRange(generated);
                }
            }
        }

        private void ProcessLegacyRecipeDatabase(LegacyRecipeDatabaseDTO dbDTO, List<GriddedRecipe> allRecipes)
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

            // 处理模板配方（旧格式）
            if (dbDTO.RecipeGroups != null)
            {
                foreach (var group in dbDTO.RecipeGroups)
                {
                    var generated = GenerateRecipesFromLegacyTemplate(group);
                    allRecipes.AddRange(generated);
                }
            }
        }

        #region 新格式模板生成逻辑
        private List<GriddedRecipe> GenerateRecipesFromMaterialGroup(TemplateGroupDTO group, MaterialDefinitionDTO materialDef)
        {
            var results = new List<GriddedRecipe>();
            if (group.Template == null || materialDef.Materials == null) return results;

            foreach (var material in materialDef.Materials)
            {
                var replacements = BuildReplacementsFromMaterial(material, group.PlaceholderMappings);
                var recipeDTO = CloneTemplateWithReplacements(group.Template, replacements);

                // 检查是否有空的产出物品ID
                if (recipeDTO.Outputs != null && recipeDTO.Outputs.Any(o => string.IsNullOrWhiteSpace(o.ItemId)))
                {
                    // 如果留空，静默跳过
                    continue;
                }

                var converted = ConvertToStruct(recipeDTO);
                if (converted.HasValue)
                    results.Add(converted.Value);
            }
            return results;
        }

        private Dictionary<string, string> BuildReplacementsFromMaterial(Dictionary<string, string> material, Dictionary<string, string> placeholderMappings)
        {
            var replacements = new Dictionary<string, string>();
            
            if (placeholderMappings != null)
            {
                foreach (var mapping in placeholderMappings)
                {
                    string placeholder = mapping.Key;
                    string materialProperty = mapping.Value;
                    
                    if (material.ContainsKey(materialProperty))
                    {
                        replacements[placeholder] = material[materialProperty];
                    }
                    else
                    {
                        // 如果Material属性不存在，记录警告
                        Mod.Logger.Warn($"[TerraCraft] 材料缺少属性: {materialProperty}");
                    }
                }
            }
            
            return replacements;
        }
        #endregion

        #region 旧格式模板生成逻辑（向后兼容）
        private List<GriddedRecipe> GenerateRecipesFromLegacyTemplate(LegacyTemplateGroupDTO group)
        {
            var results = new List<GriddedRecipe>();
            if (group.Template == null || group.Variants == null) return results;

            foreach (var variant in group.Variants)
            {
                var recipeDTO = CloneTemplateWithReplacements(group.Template, variant);

                // 检查是否有空的产出物品ID
                if (recipeDTO.Outputs != null && recipeDTO.Outputs.Any(o => string.IsNullOrWhiteSpace(o.ItemId)))
                {
                    continue;
                }

                var converted = ConvertToStruct(recipeDTO);
                if (converted.HasValue)
                    results.Add(converted.Value);
            }
            return results;
        }
        #endregion

        #region 共享模板逻辑
        private GriddedRecipeDTO CloneTemplateWithReplacements(TemplateDTO template, Dictionary<string, string> replacements)
        {
            var dto = new GriddedRecipeDTO
            {
                Id = ReplacePlaceholders(template.Id, replacements),
                Shaped = template.Shaped,
                RequiredTiles = template.RequiredTiles == null ? null : new List<string>(),
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

            // 替换物块
            if (template.RequiredTiles != null)
            {
                dto.RequiredTiles = new List<string>();
                foreach (var tile in template.RequiredTiles)
                {
                    string replaced = ReplacePlaceholders(tile, replacements);
                    if (!string.IsNullOrWhiteSpace(replaced))
                        dto.RequiredTiles.Add(replaced);
                }
            }

            // 替换Condition
            if (template.Conditions != null)
            {
                dto.Conditions = new List<string>();
                foreach (var cond in template.Conditions)
                {
                    dto.Conditions.Add(ReplacePlaceholders(cond, replacements));
                }
            }

            // 复制Pattern
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

                        // 先进行占位符替换
                        var newCell = new PatternCellDTO
                        {
                            ItemId = ReplacePlaceholders(cell.ItemId, replacements),
                            RecipeGroup = ReplacePlaceholders(cell.RecipeGroup, replacements),
                            Amount = cell.Amount
                        };

                        // 智能解析：如果前面带有RecipeGroup前缀则解析为配方组
                        if (string.IsNullOrEmpty(newCell.RecipeGroup) && !string.IsNullOrEmpty(newCell.ItemId))
                        {
                            string raw = newCell.ItemId;
                            if (raw.StartsWith("RecipeGroup:", StringComparison.OrdinalIgnoreCase))
                            {
                                newCell.RecipeGroup = raw.Substring("RecipeGroup:".Length);
                                newCell.ItemId = null;
                            }
                            else
                            {
                                //否则，尝试解析为物品ID
                                int id = ItemIDResolver.ParseItemType(raw);
                                if (id == 0)
                                {
                                    // 解析失败，当作RecipeGroup
                                    newCell.RecipeGroup = raw;
                                    newCell.ItemId = null;
                                }
                                // 解析成功，保留ItemId
                            }
                        }
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

        #region DTO转换逻辑
        private GriddedRecipe? ConvertToStruct(GriddedRecipeDTO dto)
        {
            try
            {
                // 转换RequiredTiles，允许为空
                List<int> tileIds = null;
                if (dto.RequiredTiles != null)
                {
                    tileIds = new List<int>();
                    foreach (string tileStr in dto.RequiredTiles)
                    {
                        if (string.IsNullOrWhiteSpace(tileStr))
                            continue; // 忽略空字符串
                        int id = TileIDResolver.ParseTileType(tileStr);
                        if (id != 0)
                            tileIds.Add(id);
                        }
                }

                // 如果解析后仍为空，则视为通用，赋值为 null。
                if (tileIds.Count == 0)
                    tileIds = null;

                // 转换Ingredients（从Pattern或Ingredients）
                List<RecipeIngredient> ingredients = new List<RecipeIngredient>();
                int gridWidth = 1;   // 默认尺寸，仅当Shaped = true且无Pattern时可能被AutoComputeDimensions覆盖
                int gridHeight = 1;

                // 优先使用Pattern（适用于Shaped配方）
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

                        ingredients.Add(new RecipeIngredient
                        {
                            X = ingDTO.X,
                            Y = ingDTO.Y,
                            ItemType = itemType,
                            RecipeGroup = ingDTO.RecipeGroup,
                            Amount = ingDTO.Amount
                        });
                    }
                }

                // 转换Outputs
                List<RecipeOutput> outputs = new List<RecipeOutput>();
                if (dto.Outputs != null)
                {
                    foreach (var outDTO in dto.Outputs)
                    {
                        int itemType = ItemIDResolver.ParseItemType(outDTO.ItemId);
                        outputs.Add(new RecipeOutput
                        {
                            ItemType = itemType,
                            Amount = outDTO.Amount,
                            UseDurability = outDTO.UseDurability,
                            MaxDurability = outDTO.MaxDurability,
                            InitialDurability = outDTO.InitialDurability
                        });
                    }
                }

                // 转换Replacements
                List<RecipeReplacement> replacements = new List<RecipeReplacement>();
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

                        replacements.Add(new RecipeReplacement
                        {
                            X = repDTO.X,
                            Y = repDTO.Y,
                            OriginalItemType = originalType,
                            ReplaceWithType = replaceWithType,
                            ReplaceAmount = repDTO.ReplaceAmount
                        });
                    }
                }

                // 如果是有形状配方但未使用Pattern，且Ingredients中有坐标，自动计算尺寸
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

                List<string> conditionStrings = null;
                if (dto.Conditions != null && dto.Conditions.Count > 0)
                {
                    conditionStrings = new List<string>(dto.Conditions);
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
                    Replacements = replacements,
                    Conditions = conditionStrings
                };

                string tileInfo = tileIds == null ? "None" : string.Join(", ", tileIds.Select(id => $"{id}"));
                string ingredientsInfo = ingredients.Count == 0 ? "None" : string.Join(", ", ingredients.Select(ing => {
                    string itemInfo = ing.ItemType != 0 ? $"ItemID:{ing.ItemType}" : ing.RecipeGroup;
                    return $"({ing.X},{ing.Y}):{itemInfo}×{ing.Amount}";
                }));
                string outputsInfo = outputs.Count == 0 ? "None" : string.Join(", ", outputs.Select(output => $"ItemID:{output.ItemType}×{output.Amount}"));
                // Mod.Logger.Debug($"[Recipe] ID: {dto.Id} | Type: {(dto.Shaped ? "Shaped" : "Shapeless")} | Size: {gridWidth}x{gridHeight} | Tiles: {tileInfo} | Ingredients: {ingredientsInfo} | Outputs: {outputsInfo}");
                return recipe;
            }
            catch (Exception e)
            {
                Mod.Logger.Warn($"[TerraCraft] 转换配方失败: {dto.Id}\n{e.Message}");
                return null;
            }
        }

        private List<RecipeIngredient> ParsePattern(List<List<PatternCellDTO>> pattern, out int width, out int height)
        {
            var ingredients = new List<RecipeIngredient>();
            height = pattern.Count;
            width = 0;

            // 确保所有行长度一致（取最大宽度）
            foreach (var row in pattern)
            {
                if (row.Count > width) width = row.Count;
            }

            for (int y = 0; y < height; y++)
            {
                var row = pattern[y];
                for (int x = 0; x < width; x++)
                {
                    if (x < row.Count && row[x] != null)
                    {
                        var cell = row[x];
                        int itemType = 0;
                        if (!string.IsNullOrEmpty(cell.ItemId))
                            itemType = ItemIDResolver.ParseItemType(cell.ItemId);

                        ingredients.Add(new RecipeIngredient
                        {
                            X = x,
                            Y = y,
                            ItemType = itemType,
                            RecipeGroup = cell.RecipeGroup,
                            Amount = cell.Amount ?? 1
                        });
                    }
                }
            }

            return ingredients;
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
        #endregion
    }
}
