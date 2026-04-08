using Iced.Intel;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace TerraCraft.Core.UI.GridCrafting
{
    public class GridCraftingUIRegister : ModSystem
    {
        internal UserInterface GridCraftingUI;

        public void OpenGridCraftingUI(int tileId)
        {
            if (GridCraftingUI == null) return;
            if (GridCraftingUI.CurrentState == null)
            {
                GridCraftingUI.SetState(new UIGridCraftingState(tileId));
                SoundEngine.PlaySound(SoundID.MenuOpen);
            }
        }

        public void CloseGridCraftingUI()
        {
            if (GridCraftingUI == null) return;
            GridCraftingUI.SetState(null);
            SoundEngine.PlaySound(SoundID.MenuClose);
        }

        public override void Load()
        {
            GridCraftingUI = new UserInterface();
        }
        public override void Unload()
        {
            GridCraftingUI = null;
        }
        public override void UpdateUI(GameTime gameTime)
        {
            GridCraftingUI?.Update(gameTime);
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int inventoryIndex = layers.FindIndex((layer) => layer.Name.Equals("Vanilla: Inventory"));
            if (inventoryIndex != -1)
            {
                layers.Insert(inventoryIndex, new LegacyGameInterfaceLayer("TerraCraft: Minecraft Crafting Panel", delegate ()
                {
                    GridCraftingUI.Draw(Main.spriteBatch, new GameTime());
                    return true;
                },
                InterfaceScaleType.UI));
            }
        }
    }
}
