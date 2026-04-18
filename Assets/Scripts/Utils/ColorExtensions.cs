using UnityEngine;

namespace Utils
{
    public static class ColorExtensions
    {
        public static Color WithA(this Color color, float a) => new Color(color.r, color.g, color.b, a);
    }
}