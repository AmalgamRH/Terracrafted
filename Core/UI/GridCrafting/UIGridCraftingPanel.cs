using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraCraft.Core.DataStructures.GridCrafting;
using TerraCraft.Core.Systems.Durability;
using TerraCraft.Core.Systems.GridCrafting;
using TerraCraft.Core.Utils;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace TerraCraft.Core.UI.GridCrafting
{
    internal class UIGridCraftingPanel : UIPanel
    {
        public int ItemIcon { get; set; } = ItemID.None;
        public int TileId { get; set; }
        public int GridWidth { get; private set; }
        public int GridHeight { get; private set; }

        private UICustomItemSlot outputSlot;
        private List<UICustomItemSlot> inputSlots = new List<UICustomItemSlot>();
        private Player Player => Main.LocalPlayer;

        private GridCraftingMatcher _currentMatcher;
        private List<(GriddedRecipe Recipe, Dictionary<int, int> Consumptions, List<GridCraftingMatcher.ReplacementAction> Replacements)> _allMatches;
        private int _currentMatchIndex;
        private string _previousRecipeId;

        private GriddedRecipe? _currentRecipe => _allMatches != null && _allMatches.Count > 0 && _currentMatchIndex < _allMatches.Count ? _allMatches[_currentMatchIndex].Recipe : null;
        private Dictionary<int, int> _currentConsumptions => _allMatches != null && _allMatches.Count > 0 && _currentMatchIndex < _allMatches.Count ? _allMatches[_currentMatchIndex].Consumptions : null;
        private List<GridCraftingMatcher.ReplacementAction> _currentReplacements => _allMatches != null && _allMatches.Count > 0 && _currentMatchIndex < _allMatches.Count ? _allMatches[_currentMatchIndex].Replacements : null;

        // 导航箭头
        private UIImageNeo _leftArrow;
        private UIImageNeo _rightArrow;

        // 动态面板尺寸
        private float _originalPanelWidth;
        private float _originalPanelHeight;
        private float _expandedPanelWidth;
        private float _expandedPanelHeight;

        // 鼠标交互
        private bool _wasMouseLeftPressed;
        private bool _wasMouseRightPressed;
        private bool _wasMouseOverOutputLastFrame;
        private int _craftRepeatTimer;
        private const int CraftRepeatDelay = 30;

        // 输入槽交互处理器（与预览分离）
        private CustomItemSlotInputHandler _inputHandler;

        private Item[] _lastGridItems;

        public void InitializeGrid(int tileId, int itemiconid)
        {
            BackgroundColor = new Color(63, 82, 151) * 0.8f;
            TileId = tileId;
            ItemIcon = itemiconid;
            (GridWidth, GridHeight) = CraftingStationSize.GetGridSize(tileId);
            RecreateSlots();

            _inputHandler = new CustomItemSlotInputHandler(inputSlots, RefreshMatching);
        }

        private void RecreateSlots()
        {
            SetPadding(0);

            _previousRecipeId = null;
            _allMatches = null;
            _currentMatchIndex = 0;

            foreach (var slot in inputSlots)
                RemoveChild(slot);
            if (outputSlot != null)
                RemoveChild(outputSlot);
            if (_leftArrow != null)
                RemoveChild(_leftArrow);
            if (_rightArrow != null)
                RemoveChild(_rightArrow);
            inputSlots.Clear();

            const float spacing = 8f;
            const float padding = 16;
            const float outputSpacing = 48f;
            const float iconSpacing = 16f;
            const float navSpacing = 4f;

            Vector2 slotSize = new Vector2(44.2f);
            float actualSpacing = spacing;

            for (int y = 0; y < GridHeight; y++)
            {
                for (int x = 0; x < GridWidth; x++)
                {
                    var slot = new UICustomItemSlot(ItemSlot.Context.BankItem, 0.85f);
                    if (x == 0 && y == 0)
                    {
                        slotSize = slot.GetSize(true);
                        actualSpacing = slotSize.X + spacing;
                    }
                    slot.Left.Set(padding + x * actualSpacing, 0f);
                    slot.Top.Set(padding + y * actualSpacing, 0f);
                    inputSlots.Add(slot);
                    Append(slot);
                }
            }

            outputSlot = new UICustomItemSlot(4, 0.85f);
            float outputSlotHeight = outputSlot.Height.Pixels;
            float gridActualHeight = (GridHeight - 1) * actualSpacing + slotSize.Y;
            float outputLeft = padding + GridWidth * actualSpacing + outputSpacing;
            float outputTop = padding + (gridActualHeight - outputSlotHeight) / 2f;
            outputSlot.Left.Set(outputLeft, 0f);
            outputSlot.Top.Set(outputTop, 0f);
            Append(outputSlot);

            var arrowTex = TextureAssets.GolfBallArrow;
            var arrowRect = new Rectangle(0, 0, arrowTex.Width() / 2 - 2, arrowTex.Height());
            var arrow = new UIImageNeo(arrowTex)
            {
                NormalizedOrigin = new Vector2(0.5f),
                IgnoresMouseInteraction = true,
                Color = new Color(47, 56, 106) * 0.7f,
                Rotation = -MathHelper.PiOver2,
                Rectangle = arrowRect,
                ImageScale = 1f
            };
            float arrowLeft = outputLeft - outputSpacing;
            float arrowTop = outputTop + (arrowRect.Height - outputSlot.Height.Pixels) / 2;
            arrow.SetSize(slotSize);
            arrow.Left.Set(arrowLeft, 0f);
            arrow.Top.Set(arrowTop, 0f);
            Append(arrow);

            var arrowSmallTex = TerraCraft.GetTexture("TerraCraft/Assets/UI/GridCrafting/ArrowSmall");
            var arrowSmallHoveringTex = TerraCraft.GetTexture("TerraCraft/Assets/UI/GridCrafting/ArrowSmall_Glow");
            _leftArrow = new UIImageNeo(arrowSmallTex)
            {
                NormalizedOrigin = new Vector2(0.5f),
                IgnoresMouseInteraction = false,
                Rotation = MathHelper.PiOver2,
                AllowResizingDimensions = false
            };
            _leftArrow.OnLeftClick += (evt, elem) => NavigateMatch(-1);
            _leftArrow.SetSize(arrowSmallTex.Width, arrowSmallTex.Height);
            var leftArrowGlow = new UIImageNeo(arrowSmallHoveringTex)
            {
                NormalizedOrigin = new Vector2(0.5f),
                IgnoresMouseInteraction = true,
                Color = Color.Transparent,
                Rotation = MathHelper.PiOver2,
                AllowResizingDimensions = false
            };
            leftArrowGlow.SetSize(arrowSmallTex.Width, arrowSmallTex.Height);
            _leftArrow.OnMouseOver += (evt, elem) =>
            {
                leftArrowGlow.Color = Color.White * 0.9f; 
                SoundEngine.PlaySound(SoundID.MenuTick);
            };
            _leftArrow.OnMouseOut += (evt, elem) => leftArrowGlow.Color = Color.Transparent;
            _leftArrow.Append(leftArrowGlow);
            Append(_leftArrow);

            _rightArrow = new UIImageNeo(arrowSmallTex)
            {
                NormalizedOrigin = new Vector2(0.5f),
                IgnoresMouseInteraction = false,
                Rotation = -MathHelper.PiOver2,
                AllowResizingDimensions = false
            };
            _rightArrow.OnLeftClick += (evt, elem) => NavigateMatch(1);
            _rightArrow.SetSize(arrowSmallTex.Width, arrowSmallTex.Height);


            var rightArrowGlow = new UIImageNeo(arrowSmallHoveringTex)
            {
                NormalizedOrigin = new Vector2(0.5f),
                IgnoresMouseInteraction = true,
                Color = Color.Transparent,
                Rotation = -MathHelper.PiOver2,
                AllowResizingDimensions = false
            };
            rightArrowGlow.SetSize(arrowSmallTex.Width, arrowSmallTex.Height);
            _rightArrow.OnMouseOver += (evt, elem) =>
            {
                rightArrowGlow.Color = Color.White * 0.9f;
                SoundEngine.PlaySound(SoundID.MenuTick);
            };


            _rightArrow.OnMouseOut += (evt, elem) => rightArrowGlow.Color = Color.Transparent;
            _rightArrow.Append(rightArrowGlow);
            Append(_rightArrow);

            float navArrowW = _leftArrow.Width.Pixels;
            float navArrowH = _leftArrow.Height.Pixels;

            float iconTop = outputTop + outputSlot.Height.Pixels + iconSpacing;

            _leftArrow.Left.Set(outputLeft - navArrowW - navSpacing, 0f);
            _leftArrow.Top.Set(iconTop, 0f);

            _rightArrow.Left.Set(outputLeft + outputSlot.Width.Pixels + navSpacing, 0f);
            _rightArrow.Top.Set(iconTop, 0f);

            if (TextureAssets.Item[ItemIcon] != null)
            {
                var iconTexture = TextureAssets.Item[ItemIcon];
                var craftstationIcon = new UIImageNeo(iconTexture)
                {
                    IgnoresMouseInteraction = true,
                    Color = Color.White * 0.8f
                };
                float iconLeft = outputLeft + Math.Abs(iconTexture.Width() - outputSlot.Width.Pixels) / 2;
                craftstationIcon.SetSize(slotSize);
                craftstationIcon.Left.Set(iconLeft, 0f);
                craftstationIcon.Top.Set(iconTop, 0f);
                Append(craftstationIcon);
            }

            _originalPanelWidth = outputLeft + outputSlot.Width.Pixels + padding + 4f;
            _originalPanelHeight = padding + gridActualHeight + padding + 4f;

            float navRightEdge = outputLeft + outputSlot.Width.Pixels + navSpacing + navArrowW + padding + 4f;
            float contentBottom = _originalPanelHeight;
            float iconHeight = ItemIcon > ItemID.None ? slotSize.Y : 0f;
            float iconArrowBottom = iconTop + Math.Max(iconHeight, navArrowH) + padding + 4f;
            if (iconArrowBottom > contentBottom)
                contentBottom = iconArrowBottom;

            _expandedPanelWidth = Math.Max(_originalPanelWidth, navRightEdge);
            _expandedPanelHeight = contentBottom;

            this.SetSize(_originalPanelWidth, _originalPanelHeight);

            UpdateArrowVisibility();
        }

        private void NavigateMatch(int direction)
        {
            if (_allMatches == null || _allMatches.Count <= 1) return;

            _currentMatchIndex += direction;
            if (_currentMatchIndex < 0) _currentMatchIndex = _allMatches.Count - 1;
            if (_currentMatchIndex >= _allMatches.Count) _currentMatchIndex = 0;

            UpdateOutputFromMatch();
            _previousRecipeId = _allMatches[_currentMatchIndex].Recipe.Id;
            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        private void UpdateOutputFromMatch()
        {
            if (_allMatches != null && _allMatches.Count > 0 && _currentMatchIndex < _allMatches.Count)
            {
                var output = _allMatches[_currentMatchIndex].Recipe.Outputs[0];
                outputSlot.Item.SetDefaults(output.ItemType);
                outputSlot.Item.stack = output.Amount;
            }
            else
            {
                outputSlot.Item.TurnToAir();
            }
        }

        private void UpdateArrowVisibility()
        {
            bool showArrows = _allMatches != null && _allMatches.Count > 1;
            if (_leftArrow != null)
            {
                if (showArrows)
                {
                    _leftArrow.Color = Color.White * 0.9f;
                    _leftArrow.IgnoresMouseInteraction = false;
                }
                else
                {
                    _leftArrow.Color = Color.Transparent;
                    _leftArrow.IgnoresMouseInteraction = true;
                }
            }
            if (_rightArrow != null)
            {
                if (showArrows)
                {
                    _rightArrow.Color = Color.White * 0.9f;
                    _rightArrow.IgnoresMouseInteraction = false;
                }
                else
                {
                    _rightArrow.Color = Color.Transparent;
                    _rightArrow.IgnoresMouseInteraction = true;
                }
            }

            if (showArrows)
                this.SetSize(_expandedPanelWidth, _expandedPanelHeight);
            else
                this.SetSize(_originalPanelWidth, _originalPanelHeight);
        }

        public override void Update(GameTime gameTime)
        {
            // ���� InputHandler ��������۽�������ֹԭ���Ԥ
            _inputHandler?.Update();
            // �ٴ��������
            HandleOutputSlotInteraction();
            // ���� base��ԭ�� UI ϵͳ��ʱ mouseLeftRelease �ѱ����Ǵ�������
            base.Update(gameTime);

            if (GridWidth == 0) return;
            RefreshMatching();
        }

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
                if (cur == null && last != null) return SetAndReturnTrue(currentGrid);
                if (cur != null && last == null) return SetAndReturnTrue(currentGrid);
                if (cur != null && last != null && (cur.type != last.type || cur.stack != last.stack))
                    return SetAndReturnTrue(currentGrid);
            }
            return false;

            bool SetAndReturnTrue(Item[] items)
            {
                _lastGridItems = items.Select(item => item?.Clone()).ToArray();
                return true;
            }
        }

        private void RefreshMatching()
        {
            Item[] gridItems = inputSlots.Select(s => s.Item).ToArray();

            bool hasDynamicCondition = _allMatches != null && _allMatches.Count > 0 &&
                               _allMatches.Any(m => m.Recipe.Conditions != null &&
                                                    m.Recipe.Conditions.Count > 0);

            if (!hasDynamicCondition && !HasGridChanged(gridItems)) return;

            string prevRecipeId = _previousRecipeId;

            _currentMatcher = new GridCraftingMatcher(TileId, GridWidth, GridHeight, gridItems);
            _allMatches = _currentMatcher.MatchAll();
            _allMatches = _allMatches.Where(m => AreConditionsMet(m.Recipe)).ToList();

            if (prevRecipeId != null)
            {
                int foundIndex = _allMatches.FindIndex(m => m.Recipe.Id == prevRecipeId);
                _currentMatchIndex = foundIndex >= 0 ? foundIndex : 0;
            }
            else
            {
                _currentMatchIndex = 0;
            }

            if (_allMatches.Count > 0)
            {
                var output = _allMatches[_currentMatchIndex].Recipe.Outputs[0];
                outputSlot.Item.SetDefaults(output.ItemType);
                outputSlot.Item.stack = output.Amount;
                _previousRecipeId = _allMatches[_currentMatchIndex].Recipe.Id;
            }
            else
            {
                outputSlot.Item.TurnToAir();
                _previousRecipeId = null;
            }

            UpdateArrowVisibility();

            _lastGridItems = gridItems.Select(item => item?.Clone()).ToArray();
        }

        private bool AreConditionsMet(GriddedRecipe recipe)
        {
            if (recipe.Conditions == null || recipe.Conditions.Count == 0)
                return true;

            foreach (string condStr in recipe.Conditions)
            {
                Condition condition = ConditionResolver.Parse(condStr);
                if (condition == null || !condition.Predicate())
                    return false;
            }
            return true;
        }

        // ================= ����۽��� =================
        private void HandleOutputSlotInteraction()
        {
            bool leftDown = Main.mouseLeft;

            Rectangle outputRect = outputSlot.GetInnerDimensions().ToRectangle();
            bool mouseOverOutput = outputRect.Contains(Main.MouseScreen.ToPoint());

            if (!mouseOverOutput || PlayerInput.IgnoreMouseInterface)
            {
                _craftRepeatTimer = 0;
                _wasMouseOverOutputLastFrame = false;
                _wasMouseLeftPressed = leftDown; // �ؼ����뿪ʱҲҪ����
                return;
            }

            bool leftJustPressed = leftDown && !_wasMouseLeftPressed;
            bool rightHeld = Main.mouseRight;
            bool shiftHeld = Main.keyState.IsKeyDown(Keys.LeftShift) || Main.keyState.IsKeyDown(Keys.RightShift);

            if (leftJustPressed && shiftHeld && _currentRecipe.HasValue)
            {
                CraftAll();
                Main.mouseLeftRelease = false;
            }
            else if (leftJustPressed && !shiftHeld && _currentRecipe.HasValue && _currentRecipe.Value.Outputs?.Count > 0)
            {
                int amount = _currentRecipe.Value.Outputs[0].Amount;
                if (TryCraftAndGiveToMouse(amount))
                {
                    RefreshMatching();
                    Main.mouseLeftRelease = false;
                }
            }

            if (rightHeld && _currentRecipe.HasValue)
            {
                if (_craftRepeatTimer <= 0)
                {
                    if (TryCraftAndGiveToMouse(1))
                    {
                        RefreshMatching();
                        _craftRepeatTimer = CraftRepeatDelay;
                    }
                }
                else
                    _craftRepeatTimer--;
                Main.mouseRightRelease = false;
            }
            else
            {
                _craftRepeatTimer = 0;
            }

            _wasMouseLeftPressed = leftDown; // ͳһ���������
            _wasMouseOverOutputLastFrame = true;
        }

        private void CraftAll()
        {
            while (_currentRecipe.HasValue && CanConsumeInputs())
            {
                if (!TryCraftAndGiveToMouse(_currentRecipe.Value.Outputs[0].Amount, noSound: true))
                    break;
            }
            SoundEngine.PlaySound(SoundID.Grab);
            RefreshMatching();
        }

        // ================= �ϳɺ����߼� =================
        private bool TryCraftAndGiveToMouse(int takeAmount, bool noSound = false)
        {
            if (!_currentRecipe.HasValue || _currentConsumptions == null) return false;
            if (!CanConsumeInputs()) return false;

            var output = _currentRecipe.Value.Outputs[0];
            int itemType = output.ItemType;
            if (itemType == 0) return false;

            Item craftedItem = new Item(itemType, takeAmount, prefix: -1);
            if (output.UseDurability)
            {
                int max = output.MaxDurability ?? 100;
                int initial = output.InitialDurability ?? max;
                if (max > 0)
                    craftedItem.durability().EnableDurability(initial, max);
            }

            Item mouseItem = Main.mouseItem;
            if (mouseItem.IsAir)
            {
                Main.mouseItem = craftedItem.Clone();
            }
            else if (mouseItem.type == craftedItem.type && mouseItem.stack < mouseItem.maxStack)
            {
                int canAdd = Math.Min(craftedItem.stack, mouseItem.maxStack - mouseItem.stack);
                mouseItem.stack += canAdd;
                craftedItem.stack -= canAdd;
                if (craftedItem.stack > 0) return false;
            }
            else return false;

            PerformConsumption();
            if (!noSound)
            {
                SoundEngine.PlaySound(SoundID.Grab);
            }
            return true;
        }

        private bool CanConsumeInputs()
        {
            foreach (var kv in _currentConsumptions)
            {
                if (kv.Key >= inputSlots.Count) return false;
                if (inputSlots[kv.Key].Item.stack < kv.Value) return false;
            }
            return true;
        }

        private void PerformConsumption()
        {
            // ��ִ�����ģ����ٶѵ�����������գ�
            foreach (var kv in _currentConsumptions)
            {
                Item slotItem = inputSlots[kv.Key].Item;
                slotItem.stack -= kv.Value;
                if (slotItem.stack <= 0)
                    slotItem.TurnToAir();
            }

            // �����滻�߼�
            if (_currentReplacements != null)
            {
                foreach (var rep in _currentReplacements)
                {
                    Item currentItem = inputSlots[rep.SlotIndex].Item;
                    bool slotIsEmpty = currentItem.IsAir || currentItem.stack <= 0;

                    if (slotIsEmpty)
                    {
                        // ��λΪ�գ�Ӧ���滻
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
                    else
                    {
                        if (rep.ReplaceWithItem.HasValue)
                        {
                            Player.QuickSpawnItem(Player.GetSource_FromThis(), rep.ReplaceWithItem.Value, rep.ReplaceAmount);
                        }
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
                    Player.QuickSpawnItem(new EntitySource_OverfullInventory(Player, "GridCrafting"), slot.Item, slot.Item.stack);
                    slot.Item.TurnToAir();
                }
            }
        }
    }
}