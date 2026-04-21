using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using TerraCraft.Core.UI;
using TerraCraft.Core.UI.GridCrafting;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;

namespace TerraCraft.Core.Systems.Smelting
{
    internal class SmeltingInputHandler : CustomItemSlotInputHandler
    {
        private readonly TEFurnace _furnace;
        private readonly List<UICustomItemSlot> _materialSlots;
        private readonly UICustomItemSlot _fuelSlot;
        private readonly UICustomItemSlot _outputSlot;

        private Item[] _prevMaterials;
        private Item _prevFuel;
        private Item _prevOutput;

        private bool _lastMouseLeft;

        public SmeltingInputHandler(
            List<UICustomItemSlot> allInputSlots,
            List<UICustomItemSlot> materialSlots,
            UICustomItemSlot fuelSlot,
            UICustomItemSlot outputSlot,
            TEFurnace furnace)
            : base(allInputSlots, onChanged: null)
        {
            _furnace = furnace;
            _materialSlots = materialSlots;
            _fuelSlot = fuelSlot;
            _outputSlot = outputSlot;

            _prevMaterials = new Item[materialSlots.Count];
            for (int i = 0; i < materialSlots.Count; i++)
                _prevMaterials[i] = materialSlots[i].Item;
            _prevFuel = fuelSlot.Item;
            _prevOutput = outputSlot.Item;
        }

        public override void Update()
        {
            HandleOutputSlot();
            base.Update();
            SyncToFurnace();

            _lastMouseLeft = Main.mouseLeft;
        }

        private void HandleOutputSlot()
        {
            if (_outputSlot == null || _outputSlot.Item.IsAir) return;

            var rect = _outputSlot.GetInnerDimensions().ToRectangle();
            if (!rect.Contains(Main.MouseScreen.ToPoint())) return;

            Main.LocalPlayer.mouseInterface = true;

            bool leftJustPressed = Main.mouseLeft && !_lastMouseLeft;
            if (!leftJustPressed) return;

            // Shift+左键：直接存入背包
            if (Main.keyState.IsKeyDown(Keys.LeftShift) || Main.keyState.IsKeyDown(Keys.RightShift))
            {
                Item itemToTake = _outputSlot.Item.Clone(); // 克隆当前输出槽物品，避免直接修改原物品
                // 尝试将物品放入玩家背包（包括热键栏和普通背包）
                var Item = Main.LocalPlayer.GetItem(Main.myPlayer, itemToTake, GetItemSettings.InventoryUIToInventorySettings);
                int leftover = Item.stack;
                if (leftover <= 0)
                {
                    _outputSlot.Item.TurnToAir();       // 全部取出成功，清空槽位
                }
                else
                {
                    _outputSlot.Item.stack = leftover;  // 部分取出失败，保留剩余数量
                }
                SoundEngine.PlaySound(SoundID.Grab);
                Main.mouseLeftRelease = false;
                return;
            }

            // 普通左键逻辑（拿走+合并）
            Item mouseItem = Main.mouseItem;
            Item slotItem = _outputSlot.Item;

            if (mouseItem.IsAir)
            {
                Main.mouseItem = slotItem.Clone();
                _outputSlot.Item.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
            }
            else if (IsSameItem(mouseItem, slotItem))
            {
                int canAdd = Math.Min(slotItem.stack, mouseItem.maxStack - mouseItem.stack);
                mouseItem.stack += canAdd;
                slotItem.stack -= canAdd;
                if (slotItem.stack <= 0) _outputSlot.Item.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
            }

            Main.mouseLeftRelease = false;
        }

        public void SyncFromFurnace() // 在 SyncFromFurnace 中增加索引安全
        {
            if (Main.mouseItem.IsAir && !Main.mouseLeft) // 仅在玩家未与 UI 交互时同步
            {
                if (_outputSlot != null && !_outputSlot.GetInnerDimensions().ToRectangle().Contains(Main.MouseScreen.ToPoint()))
                    _outputSlot.Item = _furnace.output;

                for (int i = 0; i < _materialSlots.Count && i < _furnace.material.Length; i++)
                    _materialSlots[i].Item = _furnace.material[i];

                _fuelSlot.Item = _furnace.fuel;
            }
        }

        private void SyncToFurnace()
        {
            if (_furnace == null) return;
            bool dirty = false;

            for (int i = 0; i < _materialSlots.Count && i < _furnace.material.Length; i++)
            {
                if (_materialSlots[i].Item != _prevMaterials[i])
                {
                    _furnace.material[i] = _materialSlots[i].Item;
                    _prevMaterials[i] = _materialSlots[i].Item;
                    dirty = true;
                }
            }

            if (_fuelSlot.Item != _prevFuel)
            {
                _furnace.fuel = _fuelSlot.Item;
                _prevFuel = _fuelSlot.Item;
                dirty = true;
            }

            if (_outputSlot.Item != _prevOutput)
            {
                _furnace.output = _outputSlot.Item;
                _prevOutput = _outputSlot.Item;
                dirty = true;
            }

            if (dirty) SendSync();
        }

        private void SendSync()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient) return;
            NetMessage.SendData(MessageID.TileEntitySharing,
                number: _furnace.ID,
                number2: _furnace.Position.X,
                number3: _furnace.Position.Y);
        }
    }
}