using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.UI;

namespace TerraCraft.Core.UI
{
    internal class VanillaItemSlotWrapper : UIElement
    {
        public bool CanTakeItem = true;
        public VanillaItemSlotWrapper(int context = 4, float scale = 1f)
        {
            _context = context;
            _scale = scale;
            Item = new Item();
            Item.SetDefaults(0);
            Width.Set(TextureAssets.InventoryBack9.Value.Width * scale, 0f);
            Height.Set(TextureAssets.InventoryBack9.Value.Height * scale, 0f);
        }
        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);
            float inventoryScale = Main.inventoryScale;
            Main.inventoryScale = _scale;
            Rectangle rectangle = GetInnerDimensions().ToRectangle();
            if (rectangle.Contains(Main.MouseScreen.ToPoint()) && !PlayerInput.IgnoreMouseInterface)
            {
                Main.LocalPlayer.mouseInterface = true;
                if ((ValidItemFunc == null || ValidItemFunc(Main.mouseItem)))
                {
                    if (CanTakeItem)
                    {
                        ItemSlot.Handle(ref Item, _context);
                    }
                    else
                    {
                        ItemSlot.MouseHover(ref Item, _context);
                    }
                }
            }
            ItemSlot.Draw(spriteBatch, ref Item, _context, rectangle.TopLeft(), default);
            Main.inventoryScale = inventoryScale;
        }

        internal Item Item;

        private readonly int _context;

        private readonly float _scale;

        internal Func<Item, bool> ValidItemFunc;
    }
}
