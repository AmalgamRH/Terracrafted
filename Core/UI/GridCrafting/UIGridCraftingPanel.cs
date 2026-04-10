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

        public void InitializeGrid(int tileId)
        {
            TileId = tileId;
            (GridWidth, GridHeight) = CraftingStationSize.GetGridSize(tileId);
            RecreateSlots();
        }

        private void RecreateSlots()
        {
            SetPadding(0);

            foreach (var slot in inputSlots)
                RemoveChild(slot);
            if (outputSlot != null)
                RemoveChild(outputSlot);
            inputSlots.Clear();

            const float spacing = 8f;
            const float padding = 16;
            const float outputSpacing = 40f;

            Vector2 slotSize = new Vector2(44.2f);
            float actualSpacing = spacing;

            for (int y = 0; y < GridHeight; y++)
            {
                for (int x = 0; x < GridWidth; x++)
                {
                    var slot = new VanillaItemSlotWrapper(4, 0.85f);
                    if (x == 0 && y == 0)
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

            outputSlot = new VanillaItemSlotWrapper(4, 0.85f);
            float outputSlotHeight = outputSlot.Height.Pixels;
            float gridActualHeight = (GridHeight - 1) * actualSpacing + slotSize.Y;
            float outputLeft = padding + GridWidth * actualSpacing + outputSpacing;
            float outputTop = padding + (gridActualHeight - outputSlotHeight) / 2f;
            outputSlot.Left.Set(outputLeft, 0f);
            outputSlot.Top.Set(outputTop, 0f);
            outputSlot.ValidItemFunc = item => item.IsAir;
            outputSlot.CanTakeItem = false;
            Append(outputSlot);

            float totalHeight = padding + gridActualHeight + padding;
            float totalWidth = outputLeft + outputSlot.Width.Pixels + padding;
            this.SetSize(totalWidth, totalHeight);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (TileId == 0 || GridWidth == 0) return;

            RefreshMatching();
            HandleOutputSlotInteraction();
        }

        private Item[] _lastGridItems;

        private bool HasGridChanged(Item[] currentGrid)
        {
            if (_lastGridItems == null || _lastGridItems.Length != currentGrid.Length)
            {
                _lastGridItems = currentGrid.Select(item => item?.Clone()).ToArray();
                return true;
            }

            for (int i = 0; i < currentGrid.Length; i++)
            {
                var cur = currentGrid[i];
                var last = _lastGridItems[i];

                if (cur == null && last != null)
                {
                    _lastGridItems = currentGrid.Select(item => item?.Clone()).ToArray();
                    return true;
                }
                if (cur != null && last == null)
                {
                    _lastGridItems = currentGrid.Select(item => item?.Clone()).ToArray();
                    return true;
                }
                if (cur != null && last != null)
                {
                    if (cur.type != last.type || cur.stack != last.stack)
                    {
                        _lastGridItems = currentGrid.Select(item => item?.Clone()).ToArray();
                        return true;
                    }
                }
            }

            return false;
        }

        private void RefreshMatching()
        {
            Item[] gridItems = inputSlots.Select(s => s.Item).ToArray();
            bool changed = HasGridChanged(gridItems);

            if (!changed)
            {
                return;
            }

            _currentMatcher = new GridCraftingMatcher(TileId, GridWidth, GridHeight, gridItems);
            var match = _currentMatcher.Match();
            _currentRecipe = match.Recipe;
            _currentConsumptions = match.Consumptions;
            _currentReplacements = match.Replacements;

            if (_currentRecipe.HasValue && _currentRecipe.Value.Outputs?.Count > 0)
            {
                var output = _currentRecipe.Value.Outputs[0];
                outputSlot.Item.SetDefaults(output.ItemType);
                outputSlot.Item.stack = output.Amount;
            }
            else
            {
                outputSlot.Item.TurnToAir();
            }

            _lastGridItems = gridItems.Select(item => item?.Clone()).ToArray();
        }

        private bool _wasMouseLeftPressed;
        private bool _wasMouseRightPressed;
        private bool _wasMouseOverOutputLastFrame;

        private void HandleOutputSlotInteraction()
        {
            Rectangle outputRect = outputSlot.GetInnerDimensions().ToRectangle();
            bool mouseOverOutput = outputRect.Contains(Main.MouseScreen.ToPoint());

            if (!mouseOverOutput || PlayerInput.IgnoreMouseInterface)
            {
                _wasMouseOverOutputLastFrame = false;
                return;
            }

            bool leftJustPressed = Main.mouseLeft && !_wasMouseLeftPressed;
            bool rightJustPressed = Main.mouseRight && !_wasMouseRightPressed;

            if ((leftJustPressed || rightJustPressed) && _currentRecipe.HasValue && _currentRecipe.Value.Outputs?.Count > 0)
            {
                int amountToTake = leftJustPressed ? _currentRecipe.Value.Outputs[0].Amount : 1;
                if (TryCraftAndGiveToMouse(amountToTake))
                {
                    RefreshMatching();
                    Main.mouseLeftRelease = false;
                    Main.mouseRightRelease = false;
                }
            }

            _wasMouseLeftPressed = Main.mouseLeft;
            _wasMouseRightPressed = Main.mouseRight;
            _wasMouseOverOutputLastFrame = true;
        }

        private bool TryCraftAndGiveToMouse(int takeAmount)
        {
            if (!_currentRecipe.HasValue || _currentConsumptions == null)
            {
                return false;
            }

            if (!CanConsumeInputs())
            {
                return false;
            }

            var output = _currentRecipe.Value.Outputs[0];
            int itemType = output.ItemType;
            if (itemType == 0)
            {
                return false;
            }

            Item craftedItem = new Item(itemType, takeAmount, -1);

            bool useDurability = output.UseDurability;
            int maxDurability = 0;
            int initialDurability = 0;
            if (useDurability)
            {
                maxDurability = output.MaxDurability ?? 100;
                initialDurability = output.InitialDurability ?? maxDurability;
                if (maxDurability > 0)
                {
                    craftedItem.durability().EnableDurability(initialDurability, maxDurability);
                }
            }

            Item mouseItem = Main.mouseItem;
            if (mouseItem.IsAir)
            {
                Main.mouseItem = craftedItem.Clone();
            }
            else if (mouseItem.type == craftedItem.type && mouseItem.stack < mouseItem.maxStack)
            {
                int canAdd = Math.Min(craftedItem.stack, mouseItem.maxStack - mouseItem.stack);
                if (canAdd > 0)
                {
                    mouseItem.stack += canAdd;
                    craftedItem.stack -= canAdd;
                }
                if (craftedItem.stack > 0)
                {
                    return false;
                }
                Main.mouseItem = mouseItem;
            }
            else
            {
                return false;
            }

            PerformConsumption();
            SoundEngine.PlaySound(SoundID.Grab);
            return true;
        }

        private bool CanConsumeInputs()
        {
            foreach (var kv in _currentConsumptions)
            {
                int slotIdx = kv.Key;
                int amount = kv.Value;
                if (slotIdx >= inputSlots.Count)
                {
                    return false;
                }
                if (inputSlots[slotIdx].Item.stack < amount)
                {
                    return false;
                }
            }
            return true;
        }

        private void PerformConsumption()
        {
            foreach (var kv in _currentConsumptions)
            {
                int slotIdx = kv.Key;
                int amount = kv.Value;
                Item slotItem = inputSlots[slotIdx].Item;
                slotItem.stack -= amount;
                if (slotItem.stack <= 0)
                {
                    slotItem.TurnToAir();
                }
            }

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