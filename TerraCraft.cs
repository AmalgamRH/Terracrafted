using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.IO;
using System.Reflection;
using TerraCraft.Core.Loaders;
using TerraCraft.Core.Network;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace TerraCraft
{
    public class TerraCraft : Mod
    {
        public static Mod Instance;
        public NetworkPacketHandler networkHandler;
        public override void Load()
        {
            Instance = this;
            networkHandler = new();

            OrderedLoaderManager.AutoRegister(Assembly.GetExecutingAssembly());
            OrderedLoaderManager.ExecuteAllLoad();
        }
        public override void Unload()
        {
            OrderedLoaderManager.ExecuteAllUnload();
            OrderedLoaderManager.Clear();

            networkHandler = null;
            Instance = null;
        }

        public override void HandlePacket(BinaryReader reader, int whoAmI)
        {
            networkHandler?.HandlePacket(reader, whoAmI);
        }

        #region 一些辅助类静态方法
        /// <summary>
        /// 获取本地化文本，文本键示例：Act1.Misc.GatesEnter
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string GetLocalizedText(string key)
        {
            return Language.GetTextValue(Instance.GetLocalizationKey(key));
        }

        /// <summary>
        /// 获取Asset<Texture2D>资源。path示例：Threshold/Assets/Blank
        /// </summary>
        /// <param name="path"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        public static Asset<Texture2D> GetAsset2D(string path, AssetRequestMode mode = AssetRequestMode.AsyncLoad)
        {
            return ModContent.Request<Texture2D>(path, mode);
        }

        /// <summary>
        /// 获取Texture2D资源。path示例：Threshold/Assets/Blank
        /// </summary>
        /// <param name="path"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        public static Texture2D GetTexture(string path, AssetRequestMode mode = AssetRequestMode.AsyncLoad)
        {
            return GetAsset2D(path, mode).Value;
        }

        /// <summary>
        /// 获取Effect资源。path示例：Threshold/Assets/Blank
        /// </summary>
        /// <param name="path"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        public static Effect GetEffect(string path, AssetRequestMode mode = AssetRequestMode.AsyncLoad)
        {
            return GetAssetFx(path, mode).Value;
        }

        /// <summary>
        /// 获取Effect资源。path示例：Threshold/Assets/Blank
        /// </summary>
        /// <param name="path"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        public static Asset<Effect> GetAssetFx(string path, AssetRequestMode mode = AssetRequestMode.AsyncLoad)
        {
            return ModContent.Request<Effect>(path, mode);
        }
        #endregion
    }
}