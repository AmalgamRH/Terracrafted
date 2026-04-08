using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using TerraCraft.Core.Systems.Durability;
using TerraCraft.Core.Utils;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using static Terraria.Localization.Language;

namespace TerraCraft.Core.Network
{
    public class NetworkPacketHandler
    {
        public void HandlePacket(BinaryReader reader, int fromWho)
        {
            MessageType msgType = (MessageType)reader.ReadByte();
            switch (msgType)
            {
                case MessageType.SyncDurability:
                    int playerWhoAmI = reader.ReadInt32();
                    int slot = reader.ReadInt32();
                    int durability = reader.ReadInt32();

                    Player targetPlayer = Main.player[playerWhoAmI];
                    if (targetPlayer != null && !targetPlayer.inventory[slot].IsAir && targetPlayer.inventory[slot].durability().UseDurability)
                    {
                        targetPlayer.inventory[slot].durability().ModifyDurability(durability);
                    }
                    break;
            }
        }
        public static void SyncPlayerDurability(Player player, int slot)
        {
            if (Main.netMode == NetmodeID.Server)
            {
                ModPacket packet = TerraCraft.Instance.GetPacket();
                packet.Write((byte)MessageType.SyncDurability);
                packet.Write(player.whoAmI); // 玩家ID
                packet.Write(slot);          // 物品槽位
                packet.Write(player.inventory[slot].durability().Durability);
                packet.Send();
            }
        }
        internal enum MessageType : byte
        {
            SyncDurability
        }
    }
}