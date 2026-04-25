using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using TerraCraft.Core.DataStructures.GridCrafting;
using TerraCraft.Core.Utils;
using Terraria.ModLoader;
using System;
using System.Linq;
using System.Text;
using Terraria.Localization;
using System.Reflection;

namespace TerraCraft.Core.UI.GridCrafting.Preview
{
    /// <summary>
    /// 只读配方预览面板，展示一个合成配方的输入网格和输出物品。
    /// 不包含任何存取、合成逻辑，仅用于显示。
    /// </summary>
    internal class UICraftPreviewPanel : UIPanel
    {
        private readonly List<UICraftPreviewSlot> _inputSlots = new();
        private readonly UICraftPreviewSlot _outputSlot;
        private readonly int _gridWidth;
        private readonly int _gridHeight;
        public int OutputItemType => _outputSlot?.Item?.type ?? 0;
        public List<string> Conditions;
        /// <summary>
        /// 构造预览面板
        /// </summary>
        /// <param name="gridWidth">配方网格宽度</param>
        /// <param name="gridHeight">配方网格高度</param>
        /// <param name="inputs">输入物品数组（按先行后列顺序）</param>
        /// <param name="output">输出物品</param>
        /// <param name="stationTileId">工作台TileID（用于显示图标）</param>
        /// <param name="stationItemIcon">工作台物品图标ID（可选）</param>
        public UICraftPreviewPanel(int gridWidth, int gridHeight, Item[] inputs, Item output, List<string> conditions,
                             int stationTileId = 0, int stationItemIcon = ItemID.None)
        {
            Conditions = conditions;
            _gridWidth = gridWidth;
            _gridHeight = gridHeight;
            BackgroundColor = new Color(45, 55, 95) * 0.85f;
            BorderColor = new Color(30, 40, 70);
            SetPadding(0);

            const float slotSpacing = 6f;
            const float padding = 12f;
            const float outputSpacing = 40f;
            const float slotSize = 44.2f;

            // 创建输入槽
            for (int i = 0; i < gridWidth * gridHeight; i++)
            {
                int x = i % gridWidth;
                int y = i / gridWidth;
                var slot = new UICraftPreviewSlot(0.85f);
                slot.Left.Set(padding + x * slotSpacing + x * slotSize, 0f);
                slot.Top.Set(padding + y * slotSpacing + y * slotSize, 0f);
                if (i < inputs.Length && inputs[i] != null && !inputs[i].IsAir)
                    slot.Item = inputs[i].Clone();
                _inputSlots.Add(slot);
                Append(slot);
            }

            // 创建输出槽
            _outputSlot = new UICraftPreviewSlot(0.85f);
            if (output != null && !output.IsAir)
                _outputSlot.Item = output.Clone();

            float gridWidthPx = gridWidth * slotSize + (gridWidth - 1) * slotSpacing;
            float gridHeightPx = gridHeight * slotSize + (gridHeight - 1) * slotSpacing;
            float outputLeft = padding + gridWidthPx + outputSpacing;
            float outputTop = padding + (gridHeightPx - slotSize) / 2f;
            _outputSlot.Left.Set(outputLeft, 0f);
            _outputSlot.Top.Set(outputTop, 0f);
            Append(_outputSlot);

            // 箭头装饰
            var arrowTex = TextureAssets.GolfBallArrow;
            var arrowRect = new Rectangle(0, 0, arrowTex.Width() / 2 - 2, arrowTex.Height());
            var arrow = new UIImageNeo(arrowTex)
            {
                NormalizedOrigin = new Vector2(0.5f),
                IgnoresMouseInteraction = true,
                Color = new Color(200, 200, 255) * 0.6f,
                Rotation = -MathHelper.PiOver2,
                Rectangle = arrowRect,
                ImageScale = 1f
            };
            arrow.SetSize(slotSize, slotSize);
            float arrowLeft = outputLeft - outputSpacing;
            float arrowTop = outputTop + (arrowRect.Height - slotSize) / 2f;
            arrow.Left.Set(arrowLeft, 0f);
            arrow.Top.Set(arrowTop, 0f);
            Append(arrow);

            // 工作台图标（位置计算改为前面代码的方式）
            float outputBottom = _inputSlots[_inputSlots.Count - 1].GetDimensions().Y + slotSize;
            float contentBottom = outputBottom;

            string conditionsText = GetConditionsTooltip(conditions) ?? "";
            if (stationItemIcon > ItemID.None)
            {
                Main.instance.LoadItem(stationItemIcon);
                float iconLeft = outputLeft;
                float iconTop = outputTop + slotSize;

                var workstation = new UICraftPreviewSlot(0.85f);
                workstation.Left.Set(iconLeft, 0f);
                workstation.Top.Set(iconTop, 0f);

                Item workstationItem = new Item(stationItemIcon);
                string stationText = TerraCraft.GetLocalizedText("UI.CraftStation") ?? "";
                string stationName = Lang.GetItemNameValue(stationItemIcon) ?? "";
                string fullTooltip = $"{stationText}{stationName}\n{conditionsText}";
                workstationItem.SetNameOverride(fullTooltip);
                workstation.Item = workstationItem;
                workstation.Context = ItemSlot.Context.ChatItem;
                Append(workstation);
                float iconBottom = iconTop + slotSize;
                contentBottom = Math.Max(contentBottom, iconBottom);
            }
            else if (!String.IsNullOrEmpty(conditionsText))
            {
                conditionsText = GetConditionsTooltip(conditions);
                _outputSlot.Item.SetNameOverride($"{Lang.GetItemNameValue(_outputSlot.Item.type)}\n{conditionsText}");
            }

            // 设置面板尺寸（宽度不变，高度取内容最大底部 + 下边距）
            float totalWidth = outputLeft + slotSize + padding;
            float totalHeight = contentBottom + padding;
            Width.Set(totalWidth, 0f);
            Height.Set(totalHeight, 0f);
        }

        public UICraftPreviewPanel(GriddedRecipe recipe, int stationTileId = 0, int stationItemIcon = ItemID.None)
            : this(recipe.GridWidth, recipe.GridHeight,
            ConvertInputsToItemArray(recipe, out var nameOverrides),
            nameOverrides,
            ConvertOutputToItem(recipe),
            recipe.Conditions,
            stationTileId, stationItemIcon)
        {
        }

        public UICraftPreviewPanel(int gridWidth, int gridHeight, Item[] inputs, string[] displayNameOverrides, Item output, List<string> conditions,
                     int stationTileId = 0, int stationItemIcon = ItemID.None)
            : this(gridWidth, gridHeight, inputs, output, conditions, stationTileId, stationItemIcon)
        {
            if (displayNameOverrides != null)
            {
                for (int i = 0; i < _inputSlots.Count && i < displayNameOverrides.Length; i++)
                {
                    if (!string.IsNullOrEmpty(displayNameOverrides[i]))
                        _inputSlots[i].NameOverride = displayNameOverrides[i];
                }
            }
        }

        private static int GetDisplayItemType(RecipeIngredient ing)
        {
            if (ing.ItemType != 0) return ing.ItemType;
            if (!string.IsNullOrEmpty(ing.RecipeGroup))
            {
                try
                {
                    var items = RecipeGroupResolver.GetRecipeGroupItems(ing.RecipeGroup);
                    if (items.Count > 0) return items.First();
                }
                catch { } // 忽略异常，返回0
            }
            return 0;
        }

        private string GetConditionsTooltip(List<string> conditions)
        {
            if (conditions == null || conditions.Count == 0)
                return null;

            var sb = new StringBuilder();
            foreach (string condStr in conditions)
            {
                Condition cond = ConditionResolver.Parse(condStr);
                if (cond != null)
                    sb.AppendLine("- " + cond.Description.Value);
                else
                    sb.AppendLine("- " + condStr);
            }
            return sb.ToString().TrimEnd('\r', '\n');
        }
        private static Item ConvertOutputToItem(GriddedRecipe recipe)
        {
            if (recipe.Outputs != null && recipe.Outputs.Count > 0)
            {
                var output = recipe.Outputs[0];
                return new Item(output.ItemType, output.Amount);
            }
            return new Item();
        }

        private static Item[] ConvertInputsToItemArray(GriddedRecipe recipe, out string[] nameOverrides)
        {
            var items = new Item[recipe.GridWidth * recipe.GridHeight];
            nameOverrides = new string[items.Length];
            if (recipe.Ingredients != null)
            {
                foreach (var ing in recipe.Ingredients)
                {
                    int displayType = GetDisplayItemType(ing);
                    if (displayType == 0) continue;

                    // 判断是否需要显示配方组名称
                    string overrideName = null;
                    if (ing.ItemType == 0 && !string.IsNullOrEmpty(ing.RecipeGroup))
                        overrideName = RecipeGroupResolver.GetDisplayText(ing.RecipeGroup);

                    if (recipe.Shaped && ing.X.HasValue && ing.Y.HasValue)
                    {
                        int index = ing.Y.Value * recipe.GridWidth + ing.X.Value;
                        if (index >= 0 && index < items.Length)
                        {
                            items[index] = new Item(displayType, ing.Amount);
                            nameOverrides[index] = overrideName;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < items.Length; i++)
                        {
                            if (items[i] == null || items[i].IsAir)
                            {
                                items[i] = new Item(displayType, ing.Amount);
                                nameOverrides[i] = overrideName;
                                break;
                            }
                        }
                    }
                }
            }
            return items;
        }
    }
}