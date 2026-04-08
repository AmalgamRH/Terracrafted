using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using TerraCraft.Core.UI;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;
using Terraria.UI;

namespace TerraCraft.Core.Loaders
{
    public class AutoloadUILoader : ModSystem
    {
        public static List<UserInterface> UserInterfaces;
        public static List<AutoloadUIState> UIStates;

        public override void Load()
        {
            if (Main.dedServ) return;

            UserInterfaces = new List<UserInterface>();
            UIStates = new List<AutoloadUIState>();

            foreach (Type type in AssemblyManager.GetLoadableTypes(Mod.Code))
            {
                if (!type.IsAbstract && type.IsSubclassOf(typeof(AutoloadUIState)))
                {
                    var state = (AutoloadUIState)Activator.CreateInstance(type);
                    var ui = new UserInterface();
                    ui.SetState(state);
                    UIStates.Add(state);
                    UserInterfaces.Add(ui);
                }
            }
        }

        public override void Unload()
        {
            UIStates?.ForEach(s => s?.OnDeactivate());
            UserInterfaces = null;
            UIStates = null;
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            for (int i = 0; i < UIStates.Count; i++)
            {
                var state = UIStates[i];
                var ui = UserInterfaces[i];
                int index = state.InsertIndex(layers);
                if (index == -1) continue;

                layers.Insert(index, new LegacyGameInterfaceLayer(
                    $"TerraCraft: {state.GetType().Name}",
                    () => { if (state.Visible) ui.Draw(Main.spriteBatch, new GameTime()); return true; },
                    state.Scale
                ));
            }
        }

        public override void UpdateUI(GameTime gameTime)
        {
            for (int i = 0; i < UserInterfaces.Count; i++)
            {
                var state = UIStates[i];
                if (state?.Visible == true)
                    UserInterfaces[i]?.Update(gameTime);
            }
        }
    }
}