using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraCraft.Core.Systems
{
    /// <summary>
    /// 合成站兼容处理，仅处理原版的兼容
    /// 模组内物块自动读取AdjTiles，无需手动注册
    /// </summary>
    public class TileCompatibilitySystem : ModSystem
    {
        // 正向映射：子tile -> 父tile集合（用于判断兼容性）
        public static Dictionary<int, HashSet<int>> ChildToParents = new();

        // 反向映射：父tile -> 子tile集合（用于构建关系）
        private static Dictionary<int, HashSet<int>> ParentToChildren = new();

        // 所有兼容性关系（包括间接关系）的缓存
        private static Dictionary<int, HashSet<int>> CompatibilityCache = new();

        public override void PostSetupContent()
        {
            BuildTileRelationships();
            BuildCompatibilityCache();
            LogCompatibilityInfo();
        }

        private void BuildTileRelationships()
        {
            // 构建模组tile的AdjTiles关系
            for (int i = 0; i < TileLoader.TileCount; i++)
            {
                ModTile t = TileLoader.GetTile(i);
                if (t?.AdjTiles == null) continue;

                foreach (int adjType in t.AdjTiles)
                {
                    AddRelationship(adjType, i); // adjType是父，i是子
                }
            }

            // 手动添加原版tile的兼容关系
            AddVanillaRelations();
        }

        private void AddVanillaRelations()
        {
            // 工作台
            AddRelationship(TileID.WorkBenches, TileID.HeavyWorkBench);

            // 铁砧
            AddRelationship(TileID.Anvils, TileID.MythrilAnvil);

            // 熔炉
            AddRelationship(TileID.Furnaces, TileID.Hellforge); //地狱熔炉继承熔炉的所有配方
            AddRelationship(TileID.Furnaces, TileID.GlassKiln); //玻璃窑继承熔炉的所有配方
            AddRelationship(TileID.Hellforge, TileID.AdamantiteForge); //精金/钛金熔炉继承地狱熔炉的所有配方

            // 瓶子
            AddRelationship(TileID.Bottles, TileID.AlchemyTable);

            // 织布机
            AddRelationship(TileID.Loom, TileID.LivingLoom);
        }

        private void AddRelationship(int parentTileId, int childTileId)
        {
            if (!ParentToChildren.ContainsKey(parentTileId))
                ParentToChildren[parentTileId] = new HashSet<int>();
            ParentToChildren[parentTileId].Add(childTileId);

            if (!ChildToParents.ContainsKey(childTileId))
                ChildToParents[childTileId] = new HashSet<int>();
            ChildToParents[childTileId].Add(parentTileId);
        }

        private void BuildCompatibilityCache()
        {
            // 为所有在父子关系中出现过的tile构建缓存
            var allRelatedTiles = new HashSet<int>();
            allRelatedTiles.UnionWith(ChildToParents.Keys);
            allRelatedTiles.UnionWith(ParentToChildren.Keys);

            foreach (var tileId in allRelatedTiles)
            {
                var compatibleTiles = new HashSet<int> { tileId };
                FindAllParentTiles(tileId, compatibleTiles);
                // 如果需要双向兼容，加上下面这行：
                // FindAllChildTiles(tileId, compatibleTiles);
                CompatibilityCache[tileId] = compatibleTiles;
            }
        }

        private void FindAllParentTiles(int tileId, HashSet<int> result)
        {
            if (!ChildToParents.ContainsKey(tileId)) return;

            foreach (var parentId in ChildToParents[tileId])
            {
                if (result.Add(parentId))
                {
                    FindAllParentTiles(parentId, result);
                }
            }
        }

        private void FindAllChildTiles(int tileId, HashSet<int> result)
        {
            if (!ParentToChildren.ContainsKey(tileId)) return;

            foreach (var childId in ParentToChildren[tileId])
            {
                if (result.Add(childId))
                {
                    FindAllChildTiles(childId, result);
                }
            }
        }

        /// <summary>
        /// 检查当前tile是否兼容所需tile
        /// </summary>
        public static bool IsTileCompatible(int currentTileId, int requiredTileId)
        {
            if (currentTileId == requiredTileId) return true;

            if (CompatibilityCache.TryGetValue(currentTileId, out var compatibleTiles))
            {
                return compatibleTiles.Contains(requiredTileId);
            }

            // 未在缓存中注册的tile只兼容自身（自身前面已判断，故此处返回false）
            return false;
        }

        /// <summary>
        /// 检查当前tile是否兼容任意所需tile
        /// </summary>
        public static bool IsTileCompatibleWithAny(int currentTileId, IEnumerable<int> requiredTileIds)
        {
            if (requiredTileIds == null || !requiredTileIds.Any()) return true;

            // 先直接检查是否包含自身
            if (requiredTileIds.Contains(currentTileId)) return true;

            // 再从缓存查找兼容性
            if (CompatibilityCache.TryGetValue(currentTileId, out var compatibleTiles))
            {
                foreach (var requiredId in requiredTileIds)
                {
                    if (compatibleTiles.Contains(requiredId))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取当前tile可以替代的所有tile ID
        /// </summary>
        public static HashSet<int> GetCompatibleTiles(int tileId)
        {
            if (CompatibilityCache.TryGetValue(tileId, out var compatibleTiles))
            {
                return new HashSet<int>(compatibleTiles);
            }

            // 未注册tile只兼容自身
            return new HashSet<int> { tileId };
        }

        private void LogCompatibilityInfo()
        {
            Mod.Logger.Debug($"[TileCompatibility] Established {ParentToChildren.Count} parent-child mappings. Parents: {ParentToChildren.Count}, Children: {ChildToParents.Count}");
        }
    }
}