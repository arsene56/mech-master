using UnityEngine;

namespace MechMaster.Runtime.UI
{
    // Shared by drawing, camera framing and pointer exclusion.
    public static class WorkshopLayout
    {
        public static bool ToolsCollapsed { get; set; }
        public static bool KnowledgeCollapsed { get; set; }
        public static Rect Header => new Rect(16, 8, 1888, 68);
        public static Rect BrandTitle => new Rect(88, 10, 330, 42);
        public static Rect BrandSlogan => new Rect(90, 48, 330, 28);
        public static Rect ModelSelector => new Rect(432, 20, 250, 44);
        public static Rect ViewButton => new Rect(692, 20, 160, 44);
        public static Rect PartButton => new Rect(862, 20, 160, 44);
        public static Rect WholeButton => new Rect(1032, 20, 144, 44);
        public static Rect StorageButton => new Rect(1186, 20, 144, 44);
        public static Rect HeaderGuide => new Rect(1340, 20, 526, 44);
        public static Rect ModelMenu(int modelCount) => new Rect(
            ModelSelector.x, 72, ModelSelector.width,
            12 + Mathf.Min(6, Mathf.Max(1, modelCount)) * 44);
        public static Rect PanControls => new Rect(ModelArea.xMax - 164, ModelArea.yMax - 174, 156, 166);
        public static Rect Tools => new Rect(16, 88, 224, 808);
        public static Rect Knowledge => new Rect(1628, 88, 276, 808);
        public static Rect ToolsTab => new Rect(16, 88, 64, 44);
        public static Rect KnowledgeTab => new Rect(1840, 88, 64, 44);
        public static Rect Tray => new Rect(16, 904, 1888, 122);
        public static Rect Status => new Rect(16, 1032, 1888, 42);
        public static Rect ModelArea => Rect.MinMaxRect(
            ToolsCollapsed ? 92 : 252, 88, KnowledgeCollapsed ? 1828 : 1616, 896);
        public static Rect TrayCell(int index) => new Rect(
            30 + index % 7 * 267, 936 + index / 7 * 44, 261, 40);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            ToolsCollapsed = false;
            KnowledgeCollapsed = false;
        }
    }
}
