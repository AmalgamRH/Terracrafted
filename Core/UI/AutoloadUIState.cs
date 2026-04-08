using System.Collections.Generic;
using Terraria.UI;

namespace TerraCraft.Core.UI
{
    public abstract class AutoloadUIState : UIState
    {
        public virtual bool Visible { get; set; } = true;
        public virtual InterfaceScaleType Scale { get; } = InterfaceScaleType.UI;
        public virtual int InsertIndex(List<GameInterfaceLayer> layers) => 
            layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
    }
}