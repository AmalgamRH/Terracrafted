using Microsoft.Xna.Framework;
using Terraria.UI;

namespace TerraCraft.Core.Utils
{
    public static class UIHelper
    {
        private static void ApplyLayout(UIElement element,
            float? left = null, float? top = null,
            float? width = null, float? height = null)
        {
            if (element == null) return;

            if (left.HasValue) element.Left.Set(left.Value, 0f);
            if (top.HasValue) element.Top.Set(top.Value, 0f);
            if (width.HasValue) element.Width.Set(width.Value, 0f);
            if (height.HasValue) element.Height.Set(height.Value, 0f);
        }

        // ---------- 位置 ----------
        public static void SetPos(this UIElement element, Vector2 start)
            => ApplyLayout(element, left: start.X, top: start.Y);

        public static void SetPos(this UIElement element, float x, float y)
            => ApplyLayout(element, left: x, top: y);

        // ---------- 尺寸 ----------
        public static void SetSize(this UIElement element, float width, float height)
            => ApplyLayout(element, width: width, height: height);

        public static void SetSize(this UIElement element, Vector2 size)
            => ApplyLayout(element, width: size.X, height: size.Y);

        // ---------- 矩形 ----------
        public static void SetRectangle(this UIElement element, Vector2 start, float width, float height)
            => ApplyLayout(element, left: start.X, top: start.Y, width: width, height: height);

        public static void SetRectangle(this UIElement element, int x, int y, Vector2 size)
            => ApplyLayout(element, left: x, top: y, width: size.X, height: size.Y);

        public static void SetRectangle(this UIElement element, float x, float y, float width, float height)
            => ApplyLayout(element, left: x, top: y, width: width, height: height);

        public static void SetRectangle(this UIElement element, Vector2 position, Vector2 size)
            => ApplyLayout(element, left: position.X, top: position.Y, width: size.X, height: size.Y);

        public static void SetRectangle(this UIElement element, Rectangle rect)
            => ApplyLayout(element, left: rect.X, top: rect.Y, width: rect.Width, height: rect.Height);

        public static Vector2 GetSize(this UIElement element, bool recalculate = false)
        {
            if (recalculate)
                element.Recalculate();
            var dims = element.GetDimensions();
            return new Vector2(dims.Width, dims.Height);
        }
    }
}