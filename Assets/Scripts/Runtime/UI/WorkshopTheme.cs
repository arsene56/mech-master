using UnityEngine;

namespace MechMaster.Runtime.UI
{
    // Shared by the interface, camera and physical sorting trays.
    public static class WorkshopTheme
    {
        public static readonly Color Background = Rgb(232, 243, 247);
        public static readonly Color Surface = Rgb(253, 254, 250);
        public static readonly Color Ink = Rgb(32, 57, 74);
        public static readonly Color MutedInk = Rgb(76, 103, 118);
        public static readonly Color Teal = Rgb(12, 113, 125);
        public static readonly Color Mint = Rgb(220, 241, 231);
        public static readonly Color Sky = Rgb(227, 241, 252);
        public static readonly Color Border = Rgb(192, 216, 221);
        public static readonly Color Orange = Rgb(255, 218, 163);
        public static readonly Color OrangeInk = Rgb(108, 58, 17);
        public static readonly Color Ready = Rgb(27, 123, 75);
        public static readonly Color Blocked = Rgb(255, 205, 111);
        public static readonly Color Wrong = Rgb(178, 56, 65);

        public static Color TrayColor(int index)
        {
            return index % 2 == 0 ? Rgb(179, 216, 218) : Rgb(206, 211, 235);
        }

        private static Color Rgb(byte red, byte green, byte blue)
        {
            return new Color32(red, green, blue, 255);
        }
    }
}
