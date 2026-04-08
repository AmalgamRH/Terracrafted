using Microsoft.Xna.Framework;
using Terraria.UI;

namespace TerraCraft.Core.Utils
{
    public static class ColorHelper
    {
        public static Color ConvertHSLToRGB(double h, double s, double l)
        {
            double r, g, b;

            if (s == 0)
            {
                r = g = b = l; // 饱和度为0，即为灰色
            }
            else
            {
                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;
                r = HueToRGB(p, q, h + 1.0 / 3.0);
                g = HueToRGB(p, q, h);
                b = HueToRGB(p, q, h - 1.0 / 3.0);
            }

            int intR = (int)(r * 255);
            int intG = (int)(g * 255);
            int intB = (int)(b * 255);

            return new Color(intR, intG, intB);
        }
        private static double HueToRGB(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
            return p;
        }
    }
}