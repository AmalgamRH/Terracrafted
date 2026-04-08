using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TerraCraft.Core.Utils;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace TerraCraft.Core.UI.GridCrafting
{
    internal class UIGridCraftingState : UIState
    {
        private UIGridCraftingPanel panel;
        public UIGridCraftingState(int tileId)
        {
            TileId = tileId;
        }
        public int TileId { get; private set; }

        public override void OnInitialize()
        {
            panel = new UIGridCraftingPanel();
            panel.SetPos(150f, 270f);
            panel.InitializeGrid(TileId);
            Append(panel);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Main.LocalPlayer.controlInv)
                ModContent.GetInstance<GridCraftingUIRegister>().GridCraftingUI.SetState(null);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);
            Main.hidePlayerCraftingMenu = true;
        }
    }
}