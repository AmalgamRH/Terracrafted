using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using TerraCraft.Core.DataStructures.GridCrafting;
using TerraCraft.Core.Systems.Durability;
using TerraCraft.Core.Systems.GridCrafting;
using TerraCraft.Core.Utils;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraCraft.Core.UI.GridCrafting
{
    internal class UIGridCraftingPanel : UIPanel
    {
        public int TileId { get; set; }
        public int GridWidth { get; private set; }
        public int GridHeight { get; private set; }

        private VanillaItemSlotWrapper outputSlot;
        private List<VanillaItemSlotWrapper> inputSlots = new List<VanillaItemSlotWrapper>();
        private Player Player => Main.LocalPlayer;

        private GridCraftingMatcher _currentMatcher;
        private GriddedRecipe? _currentRecipe;
        private Dictionary<int, int> _currentConsumptions;
        private List<GridCraftingMatcher.ReplacementAction> _currentReplacements;

        // 初始化网格（在设置TileId后调用）
        public void InitializeGrid(int tileId)
        {
            TileId = tileId;
            (GridWidth, GridHeight) = CraftingStationSize.GetGridSize(tileId);
            RecreateSlots();
        }

        #region UI布局
        private void RecreateSlots()
        {
            SetPadding(0);

            foreach (var slot in inputSlots)  // 清除原有槽位
                RemoveChild(slot);
            if (outputSlot != null)
                RemoveChild(outputSlot);
            inputSlots.Clear();

            const float spacing = 8f;
            const float padding = 16;
            const float outputSpacing = 40f;

            Vector2 slotSize = new Vector2(44.2f);  // 用于记录第一个槽位的实际高度（将在循环中首次创建时获取）
            float actualSpacing = spacing;

            for (int y = 0; y < GridHeight; y++)
            {
                for (int x = 0; x < GridWidth; x++)
                {
                    var slot = new VanillaItemSlotWrapper(4, 0.85f);
                    if (x == 0 && y == 0)   // 第一个槽位创建时，记录其实际尺寸
                    {
                        slotSize = slot.GetSize(true);
                        actualSpacing = slotSize.X + spacing;
                    }
                    slot.Left.Set(padding + x * actualSpacing, 0f);
                    slot.Top.Set(padding + y * actualSpacing, 0f);
                    slot.ValidItemFunc = item => true;
                    inputSlots.Add(slot);
                    Append(slot);
                }
            }

            outputSlot = new VanillaItemSlotWrapper(4, 0.85f);  // 创建输出槽，垂直居中于真实网格区域
            float outputSlotHeight = outputSlot.Height.Pixels;

            // 网格真实占据高度 = (行数 - 1) * 间距 + 第一个槽位的高度
            float gridActualHeight = (GridHeight - 1) * actualSpacing + slotSize.Y;

            float outputLeft = padding + GridWidth * actualSpacing + outputSpacing;
            float outputTop = padding + (gridActualHeight - outputSlotHeight) / 2f;

            outputSlot.Left.Set(outputLeft, 0f);
            outputSlot.Top.Set(outputTop, 0f);
            outputSlot.ValidItemFunc = item => item.IsAir;
            outputSlot.CanTakeItem = false;
            Append(outputSlot);

            // 面板总尺寸
            float totalHeight = padding + gridActualHeight + padding;
            float totalWidth = outputLeft + outputSlot.Width.Pixels + padding;

            this.SetSize(totalWidth, totalHeight);
        }
        #endregion

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (TileId == 0 || GridWidth == 0) return;

            RefreshMatching();
            HandleOutputSlotInteraction();
        }

        private void RefreshMatching()
        {
            Item[] gridItems = inputSlots.Select(s => s.Item).ToArray();
            _currentMatcher = new GridCraftingMatcher(TileId, GridWidth, GridHeight, gridItems);
            var match = _currentMatcher.Match();
            _currentRecipe = match.Recipe;          // 可为 null
            _currentConsumptions = match.Consumptions;
            _currentReplacements = match.Replacements;

            if (_currentRecipe.HasValue && _currentRecipe.Value.Outputs?.Count > 0)
            {
                var output = _currentRecipe.Value.Outputs[0];
                if (output.ItemType != 0)
                {
                    outputSlot.Item.SetDefaults(output.ItemType);
                    outputSlot.Item.stack = output.Amount;
                }
                else
                    outputSlot.Item.TurnToAir();
            }
            else
                outputSlot.Item.TurnToAir();
        }

        private bool _wasMouseLeftPressed;
        private bool _wasMouseRightPressed;
        private bool _wasMouseOverOutputLastFrame;

        private void HandleOutputSlotInteraction()
        {
            Rectangle outputRect = outputSlot.GetInnerDimensions().ToRectangle();
            bool mouseOverOutput = outputRect.Contains(Main.MouseScreen.ToPoint());

            // 仅在鼠标位于输出槽内且未受 UI 阻挡时处理
            if (!mouseOverOutput || PlayerInput.IgnoreMouseInterface)
            {
                _wasMouseOverOutputLastFrame = false;
                return;
            }

            // 边缘检测：按键刚按下的那一帧才触发一次合成
            bool leftJustPressed = Main.mouseLeft && !_wasMouseLeftPressed;
            bool rightJustPressed = Main.mouseRight && !_wasMouseRightPressed;

            if ((leftJustPressed || rightJustPressed) && _currentRecipe.HasValue && _currentRecipe.Value.Outputs?.Count > 0)
            {
                int amountToTake = leftJustPressed ? _currentRecipe.Value.Outputs[0].Amount : 1;
                if (TryCraftAndGiveToMouse(amountToTake))
                {
                    // 合成成功后刷新网格预览
                    RefreshMatching();
                    // 防止原版物品槽继续处理该点击
                    Main.mouseLeftRelease = false;
                    Main.mouseRightRelease = false;
                }
            }

            // 更新状态记录
            _wasMouseLeftPressed = Main.mouseLeft;
            _wasMouseRightPressed = Main.mouseRight;
            _wasMouseOverOutputLastFrame = true;
        }

        /// <summary>
        /// 执行合成并尝试将产物放入玩家鼠标
        /// </summary>
        /// <param name="takeAmount">想要拿取的数量</param>
        /// <returns>是否成功合成</returns>
        private bool TryCraftAndGiveToMouse(int takeAmount)
        {
            if (!_currentRecipe.HasValue || _currentConsumptions == null)
                return false;

            // 检查原料是否足够
            if (!CanConsumeInputs())
                return false;

            // 确定输出物品
            var output = _currentRecipe.Value.Outputs[0];
            int itemType = _currentRecipe.Value.Outputs[0].ItemType;
            if (itemType == 0) return false;

            Item craftedItem = new Item(itemType, takeAmount, -1);

            bool useDurability = output.UseDurability;
            int maxDurability = 0;
            int initialDurability = 0;

            if (useDurability)
            {
                // 优先使用配方中指定的值，否则使用默认映射表
                maxDurability = output.MaxDurability ?? 100;
                initialDurability = output.InitialDurability ?? maxDurability;
                if (maxDurability > 0)
                {
                    craftedItem.durability().EnableDurability(initialDurability, maxDurability);
                }
            }

            // 处理鼠标物品
            Item mouseItem = Main.mouseItem;
            if (mouseItem.IsAir)
            {
                Main.mouseItem = craftedItem.Clone();
            }
            else if (mouseItem.type == craftedItem.type && mouseItem.stack < mouseItem.maxStack)
            {
                // 同种物品，尝试堆叠
                int canAdd = Math.Min(craftedItem.stack, mouseItem.maxStack - mouseItem.stack);
                if (canAdd > 0)
                {
                    mouseItem.stack += canAdd;
                    craftedItem.stack -= canAdd;
                }
                if (craftedItem.stack > 0)
                {
                    return false; // 未消耗原料，直接返回
                }
                Main.mouseItem = mouseItem; // 更新引用
            }
            else
            {
                return false;
            }

            PerformConsumption();
            SoundEngine.PlaySound(SoundID.Grab);
            return true;
        }

        // 检查原料是否足够（不实际扣除）
        private bool CanConsumeInputs()
        {
            foreach (var kv in _currentConsumptions)
            {
                int slotIdx = kv.Key;
                int amount = kv.Value;
                if (slotIdx >= inputSlots.Count) return false;
                if (inputSlots[slotIdx].Item.stack < amount) return false;
            }
            return true;
        }

        // 实际扣除原料并执行替换
        private void PerformConsumption()
        {
            // 扣除原料
            foreach (var kv in _currentConsumptions)
            {
                int slotIdx = kv.Key;
                int amount = kv.Value;
                Item slotItem = inputSlots[slotIdx].Item;
                slotItem.stack -= amount;
                if (slotItem.stack <= 0)
                    slotItem.TurnToAir();
            }

            // 执行替换（如水桶 → 空桶）
            if (_currentReplacements != null)
            {
                foreach (var rep in _currentReplacements)
                {
                    if (rep.ReplaceWithItem.HasValue)
                    {
                        inputSlots[rep.SlotIndex].Item.SetDefaults(rep.ReplaceWithItem.Value);
                        inputSlots[rep.SlotIndex].Item.stack = rep.ReplaceAmount;
                    }
                    else
                    {
                        inputSlots[rep.SlotIndex].Item.TurnToAir();
                    }
                }
            }
        }


        public override void OnDeactivate()
        {
            base.OnDeactivate();
            foreach (var slot in inputSlots)
            {
                if (!slot.Item.IsAir)
                {
                    Player.QuickSpawnItem(new EntitySource_OverfullInventory(Player, "MCWorkbench"), slot.Item, slot.Item.stack);
                    slot.Item.TurnToAir();
                }
            }
        }
    }
}