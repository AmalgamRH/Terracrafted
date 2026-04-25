using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using TerraCraft.Core.DataStructures.GridCrafting;
using TerraCraft.Core.Loaders;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.ObjectData;
using TerraCraft.Core.Utils;
using Terraria.DataStructures;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria.GameContent;
using Terraria.Map;

namespace TerraCraft.Core.UI.GridCrafting.Preview
{
    /// <summary>
    /// 配方预览器UI状态
    /// 包含一个物品槽用于选择物品，下方UIList展示所有相关配方
    /// </summary>
    internal class UICraftPreviewerState : UIState
    {
        private UIItemSlot _targetItemSlot;   // 用于放置物品的交互槽
        private UIList _recipeList;
        private UIScrollbar _scrollbar;
        private Item _lastTargetItem = new Item();
        public bool IsFromGuide = false;
        public UICraftPreviewerState(bool isFromGuide = false)
        {
            IsFromGuide = isFromGuide;
        }
        private Player Player => Main.LocalPlayer;

        public override void OnInitialize()
        {
            int num54 = (Main.screenHeight - 600) / 2;
            if (Main.screenHeight < 700)
            {
                num54 = (Main.screenHeight - 508) / 2;
            }

            int inventoryX = 73;
            int inventoryY = 331 + num54 - 140;

            // 主面板
            var mainPanel = new UIElement
            {
                Width = new StyleDimension(500f, 0f),
                Height = new StyleDimension(0, 1f),
            };
            mainPanel.SetPos(inventoryX, inventoryY);
            Append(mainPanel);

            // 目标物品槽（可交互，用于放入物品）
            _targetItemSlot = new UIItemSlot(ItemSlot.Context.GuideItem, 0.85f);
            _targetItemSlot.SetPos(Vector2.Zero);
            _targetItemSlot.ValidItemFunc = (item) => true;
            mainPanel.Append(_targetItemSlot);

            // 提示文字
            var hint = new UIText(Lang.inter[24].Value, 1f);
            hint.SetPos(50, 12);
            mainPanel.Append(hint);

            // 配方列表容器（带滚动条）
            var listContainer = new UIPanel
            {
                BackgroundColor = Color.Transparent,
                BorderColor = Color.Transparent
            };
            listContainer.SetSize(500, 500);
            listContainer.SetPos(-36, 42);
            mainPanel.Append(listContainer);
            listContainer.Height.Pixels = (Main.screenHeight - listContainer.GetDimensions().Y);

            _recipeList = new UIList
            {
                Width = new StyleDimension(0f, 1f),
                Height = new StyleDimension(0f, 1f),
                ListPadding = 10f
            };
            listContainer.Append(_recipeList);

            _scrollbar = new UIScrollbar();
            _scrollbar.SetView(100f, 1000f);
            _scrollbar.Height.Set(0f, 1f);
            _scrollbar.HAlign = 1f;
            _recipeList.SetScrollbar(_scrollbar);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (IsFromGuide)
            {
                if (Player.chest != -1 || Main.npcShop != 0 || Player.talkNPC == -1 || Main.InReforgeMenu)
                {
                    Main.InGuideCraftMenu = false;
                    Main.playerInventory = false;
                    Player.dropItemCheck();
                    ModContent.GetInstance<WorkstationUIRegister>().CloseWorkstationUI();
                    Recipe.FindRecipes();
                    return;
                }
            }

            if (Player.controlInv)
            {
                ModContent.GetInstance<WorkstationUIRegister>().CloseWorkstationUI();
                Player.SetTalkNPC(-1);
                Main.playerInventory = false;
                return;
            }

            // 检测目标物品槽中的物品是否变化
            Item currentItem = _targetItemSlot.Item;
            if (!ItemEquals(currentItem, _lastTargetItem))
            {
                _lastTargetItem = currentItem?.Clone() ?? new Item();
                RefreshRecipeList();
            }
        }

        private void RefreshRecipeList()
        {
            _recipeList.Clear();

            if (_targetItemSlot.Item.IsAir)
                return;

            int targetItemType = _targetItemSlot.Item.type;
            var recipes = GetRecipesByOutputOrInput(targetItemType);

            foreach (var recipeInfo in recipes)
            {
                var previewPanel = CreatePreviewPanelFromRecipe(recipeInfo);
                if (previewPanel != null)
                    _recipeList.Add(previewPanel);
            }

            var sortedChildren = _recipeList._items
                .Cast<UICraftPreviewPanel>()
                .OrderBy(panel => panel.OutputItemType)
                .ToList(); //对UIList内部的子元素列表按输出ID排序

            _recipeList._items.Clear();
            foreach (var panel in sortedChildren)
                _recipeList._items.Add(panel);

            // 强制重新计算布局
            _recipeList.Recalculate();
        }

        /// <summary>
        /// 根据物品ID获取所有相关配方（作为输出或作为输入）
        /// </summary>
        private List<RecipePreviewData> GetRecipesByOutputOrInput(int itemType)
        {
            var result = new List<RecipePreviewData>();

            if (GridRecipeLoader.RecipeDB == null)
                return result;

            var uniqueRecipes = new HashSet<GriddedRecipe>();

            var outputRecipes = GridRecipeLoader.RecipeDB.GetRecipesByOutput(itemType);
            foreach (var recipe in outputRecipes)
                uniqueRecipes.Add(recipe);

            var inputRecipes = GridRecipeLoader.RecipeDB.GetRecipesByInput(itemType);
            foreach (var recipe in inputRecipes)
                uniqueRecipes.Add(recipe);

            foreach (var recipe in uniqueRecipes)
            {
                var previewData = ConvertToPreviewData(recipe);
                if (previewData != null)
                    result.Add(previewData);
            }

            return result;
        }

        /// <summary>
        /// 转换GriddedRecipe为RecipePreviewData
        /// </summary>
        private RecipePreviewData ConvertToPreviewData(GriddedRecipe recipe)
        {
            if (recipe.Outputs == null || recipe.Outputs.Count == 0) return null;

            var output = recipe.Outputs[0];
            var outputItem = new Item(output.ItemType, output.Amount);

            // 工作台信息
            int stationTileId = 0;
            int stationItemIcon = ItemID.None;
            if (recipe.RequiredTileIds != null && recipe.RequiredTileIds.Count > 0)
            {
                stationTileId = recipe.RequiredTileIds[0];
                stationItemIcon = TileLoader.GetItemDropFromTypeAndStyle(stationTileId);
            }

            if (recipe.Shaped)
            {
                int totalCells = recipe.GridWidth * recipe.GridHeight;
                Item[] inputs = new Item[totalCells];
                string[] nameOverrides = new string[totalCells];
                if (recipe.Ingredients != null)
                {
                    foreach (var ing in recipe.Ingredients)
                    {
                        int displayType = GetDisplayItemType(ing);
                        if (displayType == 0) continue;

                        string overrideName = null;
                        if (ing.ItemType == 0 && !string.IsNullOrEmpty(ing.RecipeGroup))
                            overrideName = RecipeGroupResolver.GetDisplayText(ing.RecipeGroup);

                        if (ing.X.HasValue && ing.Y.HasValue)
                        {
                            int index = ing.Y.Value * recipe.GridWidth + ing.X.Value;
                            if (index >= 0 && index < inputs.Length)
                            {
                                inputs[index] = new Item(displayType, ing.Amount);
                                nameOverrides[index] = overrideName;
                            }
                        }
                        else
                        {
                            // 有序配方但没有坐标？按顺序填充
                            for (int i = 0; i < inputs.Length; i++)
                            {
                                if (inputs[i] == null || inputs[i].IsAir)
                                {
                                    inputs[i] = new Item(displayType, ing.Amount);
                                    nameOverrides[i] = overrideName;
                                    break;
                                }
                            }
                        }
                    }
                }

                return new RecipePreviewData
                {
                    GridWidth = recipe.GridWidth,
                    GridHeight = recipe.GridHeight,
                    Inputs = inputs,
                    DisplayNameOverrides = nameOverrides,
                    Output = outputItem,
                    Conditions = recipe.Conditions,
                    StationTileId = stationTileId,
                    StationItemIcon = stationItemIcon
                };
            }
            else // 无序配方：动态计算网格尺寸，忽略原始 GridWidth/Height
            {
                var validIngredients = new List<(int type, int amount, string overrideName)>();
                if (recipe.Ingredients != null)
                {
                    foreach (var ing in recipe.Ingredients)
                    {
                        int displayType = GetDisplayItemType(ing);
                        if (displayType == 0) continue;

                        string overrideName = null;
                        if (ing.ItemType == 0 && !string.IsNullOrEmpty(ing.RecipeGroup))
                            overrideName = RecipeGroupResolver.GetDisplayText(ing.RecipeGroup);

                        validIngredients.Add((displayType, ing.Amount, overrideName));
                    }
                }

                int n = validIngredients.Count;
                if (n == 0) return null;

                int gridSize = (int)Math.Ceiling(Math.Sqrt(n));
                int totalCells = gridSize * gridSize;

                Item[] inputs = new Item[totalCells];
                string[] nameOverrides = new string[totalCells];

                for (int i = 0; i < n; i++)
                {
                    var ing = validIngredients[i];
                    inputs[i] = new Item(ing.type, ing.amount);
                    nameOverrides[i] = ing.overrideName;
                }
                // 剩余格子保持 null (IsAir)

                return new RecipePreviewData
                {
                    GridWidth = gridSize,
                    GridHeight = gridSize,
                    Inputs = inputs,
                    DisplayNameOverrides = nameOverrides,
                    Output = outputItem,
                    StationTileId = stationTileId,
                    Conditions = recipe.Conditions,
                    StationItemIcon = stationItemIcon
                };
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

        /// <summary>
        /// 根据配方数据创建CraftingPreviewPanel实例
        /// </summary>
        private UICraftPreviewPanel CreatePreviewPanelFromRecipe(RecipePreviewData data)
        {
            if (data == null || data.Output?.IsAir != false)
                return null;

            return new UICraftPreviewPanel(
                data.GridWidth,
                data.GridHeight,
                data.Inputs,
                data.DisplayNameOverrides,
                data.Output,
                data.Conditions,
                data.StationTileId,
                data.StationItemIcon
            );
        }

        // 辅助方法：比较两个Item是否相同（类型和堆叠数）
        private bool ItemEquals(Item a, Item b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.type == b.type && a.stack == b.stack;
        }

        public override void OnDeactivate()
        {
            base.OnDeactivate();
            if (!_targetItemSlot.Item.IsAir)
            {
                Player.QuickSpawnItem(new EntitySource_OverfullInventory(Player, "GridCrafting"), 
                    _targetItemSlot.Item, _targetItemSlot.Item.stack);
                _targetItemSlot.Item.TurnToAir();
            }
        }
    }

    /// <summary>
    /// 配方预览数据的简要包装类，用于在GetRecipesByOutput中传递信息
    /// </summary>
    internal class RecipePreviewData
    {
        public int GridWidth { get; set; }
        public int GridHeight { get; set; }
        public Item[] Inputs { get; set; }      // 长度应为GridWidth * GridHeight
        public Item Output { get; set; }
        public int StationTileId { get; set; }
        public int StationItemIcon { get; set; }
        public string[] DisplayNameOverrides { get; set; }
        public List<string> Conditions { get; set; }
    }
}
