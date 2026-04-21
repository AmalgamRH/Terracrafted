using Microsoft.Xna.Framework;
using System;
using System.IO;
using TerraCraft.Core.DataStructures.Smelting;
using TerraCraft.Core.Loaders;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace TerraCraft.Core.Systems.Smelting
{
    public class TEFurnace : ModTileEntity
    {
        public const int MAX_MATERIALS = 3;

        public float progress;
        public int burnTime;
        public int maxBurnTime;
        public int tileID;
        public Item[] material = new Item[MAX_MATERIALS];
        public Item fuel = new Item();
        public Item output = new Item();

        private SmeltingRecipe? _currentRecipe;
        private float _requiredTotalTime;
        private float _currentFuelSpeed = 1f;
        private int _currentFuelLevel = 0;
        private int _currentFuelType = 0;
        private int _cachedMainItemType = -1;
        private bool _needSync;
        private (int type, int stack)[] _cachedMaterialSlots = new (int, int)[MAX_MATERIALS];
        public float GetProgressRatio()
        {
            if (_requiredTotalTime <= 0f) return 0f;
            return Math.Clamp(progress / _requiredTotalTime, 0f, 1f);
        }

        public float GetFuelRatio()
        {
            if (maxBurnTime <= 0) return 0f;
            return Math.Clamp((float)burnTime / maxBurnTime, 0f, 1f);
        }

        public TEFurnace()
        {
            for (int i = 0; i < MAX_MATERIALS; i++)
            {
                material[i] = new Item();
                _cachedMaterialSlots[i] = (0, 0);
            }
        }

        public override void Update()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient) UpdateSmelting();
            if (_needSync && Main.netMode == NetmodeID.Server) SendSync();
        }

        private void UpdateSmelting()
        {
            // 1. 检测材料槽内容是否变化（类型或数量）
            bool materialsChanged = false;
            for (int i = 0; i < MAX_MATERIALS; i++)
            {
                int curType = material[i]?.type ?? 0;
                int curStack = material[i]?.stack ?? 0;
                if (curType != _cachedMaterialSlots[i].type || curStack != _cachedMaterialSlots[i].stack)
                {
                    materialsChanged = true;
                    _cachedMaterialSlots[i] = (curType, curStack);
                }
            }

            if (materialsChanged)
            {
                // 尝试匹配新配方（基于当前材料）
                int mainType = GetMainMaterialType();
                SmeltingRecipe? newRecipe = null;
                if (mainType != -1)
                    newRecipe = SmeltingMatcher.GetBestRecipe(tileID, mainType, material, fuel);

                // 判断是否可以继续使用当前配方而不重置进度
                bool canContinue = false;
                if (_currentRecipe.HasValue && newRecipe.HasValue && _currentRecipe.Value.Id == newRecipe.Value.Id)
                {
                    // 配方 ID 相同，再检查材料数量、输出空间、燃料等是否仍满足
                    canContinue = CanSmelt();
                }

                if (!canContinue)
                {
                    // 无法继续：配方改变或材料不足 → 重置进度
                    _currentRecipe = newRecipe;
                    progress = 0;
                    _requiredTotalTime = 0;
                    MarkDirty();
                }
                // 注意：如果 canContinue == true，则保留原 _currentRecipe 和 progress，不做任何改动
                _cachedMainItemType = mainType; // 更新主材料缓存
            }

            // 2. 正常配方匹配（处理燃料变化等情况）
            int currentMainType = GetMainMaterialType();
            SmeltingRecipe? matchedRecipe = null;
            if (currentMainType != -1)
                matchedRecipe = SmeltingMatcher.GetBestRecipe(tileID, currentMainType, material, fuel);

            if (!AreRecipesEqual(_currentRecipe, matchedRecipe))
            {
                // 配方发生变化（例如燃料类型改变导致可用配方不同）
                _currentRecipe = matchedRecipe;
                progress = 0;
                _requiredTotalTime = 0;
                MarkDirty();
            }

            if (_currentRecipe.HasValue && _currentRecipe.Value.Id != null)
            {
                float totalSpeed = SmeltingMatcher.GetTotalSpeed(tileID, _currentRecipe.Value, _currentFuelSpeed);
                if (totalSpeed > 0)
                {
                    float newRequired = _currentRecipe.Value.BaseSmeltTime / totalSpeed;
                    if (Math.Abs(_requiredTotalTime - newRequired) > 0.01f)
                    {
                        _requiredTotalTime = newRequired;
                        if (progress >= _requiredTotalTime) progress = _requiredTotalTime - 1;
                        MarkDirty();
                    }
                }
            }

            bool canSmelt = CanSmelt();
            if (burnTime <= 0 && canSmelt)
            {
                ConsumeFuel();
                if (_currentRecipe.HasValue && _currentRecipe.Value.Id != null)
                {
                    float totalSpeed = SmeltingMatcher.GetTotalSpeed(tileID, _currentRecipe.Value, _currentFuelSpeed);
                    if (totalSpeed > 0) _requiredTotalTime = _currentRecipe.Value.BaseSmeltTime / totalSpeed;
                }
                MarkDirty();
            }

            canSmelt = CanSmelt();

            if (burnTime > 0) burnTime--;

            if (canSmelt && burnTime > 0 && _currentRecipe.HasValue && _currentRecipe.Value.Id != null)
            {
                float increment = SmeltingMatcher.GetTotalSpeed(tileID, _currentRecipe.Value, _currentFuelSpeed);
                if (increment <= 0f) return; // 不支持的标签，increment==0 时 0>=0 会误触发完成
                progress += increment;
                if (progress >= _requiredTotalTime)
                {
                    CompleteSmelting();
                    progress = 0;
                    _cachedMainItemType = -1;
                }
                MarkDirty();
            }
            else if (progress > 0)
            {
                float backwardSpeed = (_currentRecipe.HasValue && _currentRecipe.Value.Id != null)
                    ? SmeltingMatcher.GetTotalSpeed(tileID, _currentRecipe.Value, _currentFuelSpeed) * 4f
                    : 0.5f;
                if (backwardSpeed < 0.1f) backwardSpeed = 0.5f;
                progress -= backwardSpeed;
                if (progress < 0) progress = 0;
                MarkDirty();
            }

            if (burnTime < 0) burnTime = 0;
            if (progress < 0) progress = 0;
        }

        private bool CanSmelt()
        {
            if (!_currentRecipe.HasValue || _currentRecipe.Value.Id == null) return false;
            var recipe = _currentRecipe.Value;

            float totalSpeed = SmeltingMatcher.GetTotalSpeed(tileID, recipe, _currentFuelSpeed);
            if (totalSpeed <= 0f) return false;

            // 多材料检查：按顺序匹配每个材料槽
            for (int i = 0; i < recipe.Ingredients.Count; i++)
            {
                var ing = recipe.Ingredients[i];
                if (i >= material.Length) return false;
                var slot = material[i];
                if (slot == null || slot.IsAir) return false;
                if (slot.type != ing.ItemType) return false;
                if (slot.stack < ing.Amount) return false;
            }

            if (!SmeltingMatcher.CanAddOutput(recipe, output)) return false;

            if (burnTime > 0)
            {
                if (!SmeltingMatcher.IsFuelValidForRecipe(recipe, _currentFuelType, _currentFuelLevel))
                    return false;
            }
            else
            {
                if (fuel.IsAir) return false;
                SmeltingMatcher.GetFuelData(fuel.type, out int burn, out _, out int level);
                if (burn <= 0) return false;
                if (!SmeltingMatcher.IsFuelValidForRecipe(recipe, fuel.type, level))
                    return false;
            }
            return true;
        }

        private void ConsumeFuel()
        {
            if (fuel.IsAir) return;
            SmeltingMatcher.GetFuelData(fuel.type, out int burnAdd, out float speed, out int level);
            if (burnAdd <= 0) return;

            //储存当前燃烧周期的燃料数据
            _currentFuelSpeed = speed;
            _currentFuelLevel = level;
            _currentFuelType = fuel.type;
            burnTime += burnAdd;
            maxBurnTime = burnTime;

            var replacement = SmeltingLoader.Database.GetFuelReplacement(fuel.type);

            fuel.stack--;
            bool fuelSlotEmpty = fuel.stack <= 0;
            if (fuelSlotEmpty) fuel.TurnToAir();

            //带有替换规则的燃料
            if (replacement.ReplaceWithType.HasValue && replacement.ReplaceWithType.Value != 0)
            {
                Item replaceItem = new Item();
                replaceItem.SetDefaults(replacement.ReplaceWithType.Value);
                replaceItem.stack = replacement.ReplaceAmount;

                if (fuelSlotEmpty) // 如果燃料槽为空，直接放入；否则掉落
                    fuel = replaceItem;
                else
                {
                    Item.NewItem(new EntitySource_TileBreak(Position.X, Position.Y),
                    Position.X * 16, Position.Y * 16, 16, 16,
                    replacement.ReplaceWithType.Value, replacement.ReplaceAmount);
                }
            }
            MarkDirty();
        }

        private void CompleteSmelting()
        {
            if (!_currentRecipe.HasValue) return;
            var recipe = _currentRecipe.Value;
            DeductIngredients(recipe);
            AddOutput(recipe.Outputs[0]);
            ApplyReplacements(recipe);
            MarkDirty();

            progress = 0;
            _cachedMainItemType = -1;

            for (int i = 0; i < MAX_MATERIALS; i++)
                _cachedMaterialSlots[i] = (material[i]?.type ?? 0, material[i]?.stack ?? 0);
            MarkDirty();
        }

        private void DeductIngredients(SmeltingRecipe recipe)
        {
            for (int i = 0; i < recipe.Ingredients.Count; i++)
            {
                var ing = recipe.Ingredients[i];
                if (i >= material.Length) continue;
                var slot = material[i];
                if (slot != null && !slot.IsAir && slot.type == ing.ItemType)
                {
                    slot.stack -= ing.Amount;
                    if (slot.stack <= 0) slot.TurnToAir();
                }
            }
        }

        private void AddOutput(SmeltingOutput outputInfo)
        {
            if (output.IsAir)
            {
                output.SetDefaults(outputInfo.ItemType);
                output.stack = outputInfo.Amount;
            }
            else if (output.type == outputInfo.ItemType)
            {
                output.stack += outputInfo.Amount;
                if (output.stack > output.maxStack) output.stack = output.maxStack;
            }
        }

        private void ApplyReplacements(SmeltingRecipe recipe)
        {
            if (recipe.Replacements == null) return;
            foreach (var rep in recipe.Replacements)
            {
                for (int i = 0; i < MAX_MATERIALS; i++)
                {
                    if (material[i] != null && !material[i].IsAir && material[i].type == rep.OriginalItemType)
                    {
                        material[i].stack -= rep.ReplaceAmount;
                        if (material[i].stack <= 0) material[i].TurnToAir();
                        if (rep.ReplaceWithType.HasValue && rep.ReplaceWithType.Value != 0)
                        {
                            Item replace = new Item();
                            replace.SetDefaults(rep.ReplaceWithType.Value);
                            replace.stack = rep.ReplaceAmount;
                            bool placed = false;
                            for (int j = 0; j < MAX_MATERIALS; j++)
                            {
                                if (material[j].IsAir)
                                {
                                    material[j] = replace.Clone();
                                    placed = true;
                                    break;
                                }
                            }
                            if (!placed)
                            {
                                Item.NewItem(new EntitySource_TileBreak(Position.X, Position.Y),
                                    Position.X * 16, Position.Y * 16, 16, 16, replace.type, replace.stack);
                            }
                        }
                        break;
                    }
                }
            }
        }

        private int GetMainMaterialType()
        {
            foreach (var item in material)
                if (item != null && !item.IsAir) return item.type;
            return -1;
        }

        private bool AreRecipesEqual(SmeltingRecipe? a, SmeltingRecipe? b)
        {
            if (!a.HasValue && !b.HasValue) return true;
            if (!a.HasValue || !b.HasValue) return false;
            return a.Value.Id == b.Value.Id;
        }

        private void MarkDirty() => _needSync = true;
        private void SendSync() => NetMessage.SendData(MessageID.TileEntitySharing, number: ID, number2: Position.X, number3: Position.Y);

        #region 网络 IO
        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(progress);
            writer.Write(burnTime);
            writer.Write(maxBurnTime);
            writer.Write(tileID);

            for (int i = 0; i < MAX_MATERIALS; i++)
                ItemIO.Send(material[i], writer, writeStack: true);

            ItemIO.Send(fuel, writer, writeStack: true);
            ItemIO.Send(output, writer, writeStack: true);
        }
        public override void NetReceive(BinaryReader reader)
        {
            progress = reader.ReadSingle();
            burnTime = reader.ReadInt32();
            maxBurnTime = reader.ReadInt32();
            tileID = reader.ReadInt32();

            for (int i = 0; i < MAX_MATERIALS; i++)
                material[i] = ItemIO.Receive(reader, readStack: true);

            fuel = ItemIO.Receive(reader, readStack: true);
            output = ItemIO.Receive(reader, readStack: true);

            _cachedMainItemType = -1;

            for (int i = 0; i < MAX_MATERIALS; i++)
                _cachedMaterialSlots[i] = (material[i]?.type ?? 0, material[i]?.stack ?? 0);
        }
        public override void SaveData(TagCompound tag)
        {
            tag["progress"] = progress;
            tag["burnTime"] = burnTime;
            tag["maxBurnTime"] = maxBurnTime;
            tag["tileID"] = tileID;

            var materialList = new TagCompound[MAX_MATERIALS];
            for (int i = 0; i < MAX_MATERIALS; i++)
                materialList[i] = ItemIO.Save(material[i]);
            tag["material"] = materialList;
            tag["fuel"] = ItemIO.Save(fuel);
            tag["output"] = ItemIO.Save(output);
        }
        public override void LoadData(TagCompound tag)
        {
            progress = tag.GetFloat("progress");
            burnTime = tag.GetInt("burnTime");
            maxBurnTime = tag.GetInt("maxBurnTime");
            tileID = tag.GetInt("tileID");

            var materialList = tag.Get<TagCompound[]>("material");
            if (materialList != null)
            {
                for (int i = 0; i < MAX_MATERIALS && i < materialList.Length; i++)
                    material[i] = ItemIO.Load(materialList[i]);
            }

            var fuelTag = tag.Get<TagCompound>("fuel");
            if (fuelTag != null) fuel = ItemIO.Load(fuelTag);

            var outputTag = tag.Get<TagCompound>("output");
            if (outputTag != null) output = ItemIO.Load(outputTag);

            _cachedMainItemType = -1;

            for (int i = 0; i < MAX_MATERIALS; i++)
                _cachedMaterialSlots[i] = (material[i]?.type ?? 0, material[i]?.stack ?? 0);
        }
        #endregion

        public override void OnNetPlace()
        {
            NetMessage.SendData(MessageID.TileEntitySharing, number: ID, number2: Position.X, number3: Position.Y);
        }
        public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
        {
            TileObjectData tileData = TileObjectData.GetTileData(type, style, alternate);
            int topLeftX = i - tileData.Origin.X;
            int topLeftY = j - tileData.Origin.Y;

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                NetMessage.SendTileSquare(Main.myPlayer, topLeftX, topLeftY, tileData.Width, tileData.Height);
                NetMessage.SendData(MessageID.TileEntityPlacement, number: topLeftX, number2: topLeftY, number3: Type);
                return -1;
            }

            return Place(topLeftX, topLeftY);
        }
        public override void NetPlaceEntityAttempt(int x, int y)
        {
            int ID = Place(x, y);
            NetMessage.SendData(MessageID.TileEntitySharing, number: ID, number2: x, number3: y);
        }
        public new int Place(int i, int j)
        {
            return base.Place(i, j);
        }
        public override bool IsTileValidForEntity(int x, int y)
        {
            TileObjectData tileData = TileObjectData.GetTileData(Main.tile[x, y]);
            return Main.tile[x, y].HasTile && Main.tile[x, y].TileType == tileID;
        }
        public new void Kill(int x, int y)
        {
            DropContents();
            base.Kill(x, y);
        }
        public void DropContents()
        {
            for (int i = 0; i < MAX_MATERIALS; i++)
            {
                if (material[i] != null && !material[i].IsAir)
                {
                    Item.NewItem(new EntitySource_TileBreak(Position.X, Position.Y),
                        Position.X * 16, Position.Y * 16, 16, 16, material[i].type,
                        material[i].stack, false, material[i].prefix);
                    material[i].TurnToAir();
                }
            }
            if (fuel != null && !fuel.IsAir)
            {
                Item.NewItem(new EntitySource_TileBreak(Position.X, Position.Y),
                    Position.X * 16, Position.Y * 16, 16, 16, fuel.type, fuel.stack, false, fuel.prefix);
                fuel.TurnToAir();
            }
            if (output != null && !output.IsAir)
            {
                Item.NewItem(new EntitySource_TileBreak(Position.X, Position.Y),
                    Position.X * 16, Position.Y * 16, 16, 16, output.type, output.stack, false, output.prefix);
                output.TurnToAir();
            }
        }
    }
}