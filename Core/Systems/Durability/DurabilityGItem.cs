using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using TerraCraft.Core.Network;
using TerraCraft.Core.Utils;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using static Terraria.Localization.Language;

namespace TerraCraft.Core.Systems.Durability
{
    public class DurabilityGItem : GlobalItem
    {
        private int _durability = 0;
        private int _maxDurability = 0;
        private bool _useDurability = false;
        public int Durability => _durability;
        public int MaxDurability => _maxDurability;
        public bool UseDurability => _useDurability;

        /// <summary>仅可对以有耐久度的使用</summary>
        /// <param name="durability"></param>
        public void ModifyDurability(int durability)
        {
            if (!_useDurability) return;
            _durability = Math.Clamp(durability, 0, _maxDurability);
        }
        public void EnableDurability(int currentDurability, int maxDurability)
        {
            _useDurability = true;
            _maxDurability = maxDurability;
            _durability = Math.Clamp(currentDurability, 0, _maxDurability);
        }
        public override void ModifyTooltips(Item Item, List<TooltipLine> tooltips)
        {
            Player Player = Main.LocalPlayer;
            if (Item.durability()._useDurability)
            {
                string dura = GetTextValue(Mod.GetLocalizationKey("Tooltips.Durability"));
                if (Item.durability()._durability >= 0)
                {
                    TooltipLine tooltip = new TooltipLine(Mod, "Durability", $"{dura}: {Item.durability()._durability}/{Item.durability()._maxDurability}");
                    float value = Math.Clamp(_durability / (float)_maxDurability, 0f, 1f);
                    Color color = ColorHelper.ConvertHSLToRGB(value / 3.0, 0.4, 0.6);
                    tooltip.OverrideColor = color;
                    tooltips.Add(tooltip);
                }
            }
        }
        public override void PostDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            bool shouldUse = (item.type == ItemID.FrostDaggerfish || item.type == ItemID.IceBow) && _useDurability;
            if (shouldUse)
            {
                spriteBatch.End();
                spriteBatch.Begin(0, spriteBatch.GraphicsDevice.BlendState, spriteBatch.GraphicsDevice.SamplerStates[0], spriteBatch.GraphicsDevice.DepthStencilState, spriteBatch.GraphicsDevice.RasterizerState, null, Main.UIScaleMatrix);
            }

            if (item.durability()._useDurability)
            {
                Texture2D front = TerraCraft.GetTexture("TerraCraft/Assets/Durability/HealthBar1");
                Texture2D middle = TerraCraft.GetTexture("TerraCraft/Assets/Durability/HealthBar3");
                Texture2D back = TerraCraft.GetTexture("TerraCraft/Assets/Durability/HealthBar2");
                position.Y += 14;
                float value = Math.Clamp(_durability / (float)_maxDurability, 0f, 1f);
                Vector2 Scale = new Vector2(0.92f, 1f) * 0.65f;
                Vector2 Origin = new Vector2(back.Width, back.Height) / 2f;
                Color color = ColorHelper.ConvertHSLToRGB(value / 3.0, 1.0, 0.5) * 1f;

                spriteBatch.Draw(back, position, new Rectangle(0, 0, front.Width, front.Height), color
                    , 0f, Origin, Scale, SpriteEffects.None, 0f);
                bool should = front.Width - (int)(front.Width * value) > 2;
                int width = Math.Max((int)(front.Width * value), 2);
                spriteBatch.Draw(middle, position, new Rectangle(0, 0, width + (should ? 2 : 0), front.Height), color
                   , 0f, Origin, Scale, SpriteEffects.None, 0f);
                spriteBatch.Draw(front, position, new Rectangle(0, 0, width, front.Height), color
                    , 0f, Origin, Scale, SpriteEffects.None, 0f);
            }
        }
        public override void UpdateInventory(Item Item, Player player)
        {
            if (Item != null && Item.active && Item.durability()._useDurability)
            {
                if (Item.durability()._durability <= 0)
                {
                    Item.active = false;
                    Item.TurnToAir();
                    SoundEngine.PlaySound(SoundID.Item37, player.Center);
                }
            }
        }
        public override void ModifyShootStats(Item Item, Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
        {
            if (Item != null && Item.active && Item.durability()._useDurability && Item.shoot > 0)
            {
                float consumeChance = 1f;
                if (Item.useAnimation > 0 && Item.useTime != Item.useAnimation)
                {
                    // 期望完整动画消耗1点耐久 → 每次射击概率 = useTime / useAnimation
                    consumeChance = (float)Item.useTime / Item.useAnimation;
                    if (consumeChance > 1f) consumeChance = 1f;
                }

                if (Main.rand.NextFloat() < consumeChance)
                {
                    Item.durability()._durability--;
                }
            }
        }
        public override void OnHitNPC(Item Item, Player player, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Item != null && Item.active && Item.durability()._useDurability)
            {
                Item.durability()._durability--;
            }
        }
        public override void OnHitPvp(Item Item, Player player, Player target, Player.HurtInfo hurtInfo)
        {
            if (Item != null && Item.active && Item.durability()._useDurability)
            {
                Item.durability()._durability--;
            }
        }
        public override bool InstancePerEntity => true;
        public override GlobalItem Clone(Item item, Item itemClone)
        {
            return base.Clone(item, itemClone);
        }
        public override bool AllowPrefix(Item Item, int pre)
        {
            if (Item.durability()._useDurability && Item.pick <= 0)
            {
                return false;
            }
            return base.AllowPrefix(Item, pre);
        }
        public override void NetSend(Item Item, BinaryWriter writer)
        {
            writer.Write(_useDurability);
            writer.Write(_maxDurability);
            writer.Write(_durability);

            if (_useDurability)
            {
                writer.Write(Item.damage);
                writer.Write(Item.knockBack);
                writer.Write(Item.scale);
                writer.Write(Item.rare);
                writer.Write(Item.Name);
                writer.Write(Item.maxStack);
                writer.Write(Item.consumable);
                writer.Write(Item.useTime);
                writer.Write(Item.useAnimation);
                writer.Write(Item.shoot);
                writer.Write(Item.shootSpeed);
            }
        }
        public override void NetReceive(Item Item, BinaryReader reader)
        {
            _useDurability = reader.ReadBoolean();
            _maxDurability = reader.ReadInt32();
            _durability = reader.ReadInt32();

            if (_useDurability)
            {
                Item.damage = reader.ReadInt32();
                Item.knockBack = reader.ReadSingle();
                Item.scale = reader.ReadSingle();
                Item.rare = reader.ReadInt32();
                Item.SetNameOverride(reader.ReadString());
                Item.maxStack = reader.ReadInt32();
                Item.consumable = reader.ReadBoolean();
                Item.useTime = reader.ReadInt32();
                Item.useAnimation = reader.ReadInt32();
                Item.shoot = reader.ReadInt32();
                Item.shootSpeed = reader.ReadSingle();
            }
        }
        public override void LoadData(Item Item, TagCompound tag)
        {
            _useDurability = tag.GetBool("useDurability");
            _maxDurability = tag.GetInt("maxDurability");
            _durability = tag.GetInt("durability");

            if (_useDurability)
            {
                Item.damage = tag.GetInt("damage");
                Item.knockBack = tag.GetFloat("knockBack");
                Item.scale = tag.GetFloat("scale");
                Item.rare = tag.GetInt("rare");
                Item.SetNameOverride(tag.GetString("Name"));
                Item.maxStack = tag.GetInt("maxStack");
                Item.consumable = tag.GetBool("consumable");
                Item.useTime = tag.GetInt("useTime");
                Item.useAnimation = tag.GetInt("useAnimation");
                Item.shoot = tag.GetInt("shoot");
                Item.shootSpeed = tag.GetFloat("shootSpeed");
            }
        }
        public override void SaveData(Item Item, TagCompound tagCompound)
        {
            tagCompound.Add("useDurability", _useDurability);
            tagCompound.Add("maxDurability", _maxDurability);
            tagCompound.Add("durability", _durability);

            if (_useDurability)
            {
                tagCompound.Add("damage", Item.damage);
                tagCompound.Add("knockBack", Item.knockBack);
                tagCompound.Add("scale", Item.scale);
                tagCompound.Add("rare", Item.rare);
                tagCompound.Add("Name", Item.Name);
                tagCompound.Add("maxStack", Item.maxStack);
                tagCompound.Add("consumable", Item.consumable);
                tagCompound.Add("useTime", Item.useTime);
                tagCompound.Add("useAnimation", Item.useAnimation);
                tagCompound.Add("shoot", Item.shoot);
                tagCompound.Add("shootSpeed", Item.shootSpeed);
            }
        }
    }
    public static class DurabilityExtensions
    {
        public static DurabilityGItem durability(this Item Item)
        {
            return Item.GetGlobalItem<DurabilityGItem>();
        }
    }
}