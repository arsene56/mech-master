using System;
using MechMaster.Runtime;
using MechMaster.Runtime.UI;
using UnityEditor;
using UnityEngine;

namespace MechMaster.Editor
{
    public static class UIPixelLayoutValidator
    {
        [MenuItem("机械大师/验证文字像素布局")]
        public static void Validate()
        {
            Vector2Int[] sizes =
            {
                new Vector2Int(1280, 720), new Vector2Int(1920, 1080),
                new Vector2Int(2560, 1440), new Vector2Int(3840, 2160),
                new Vector2Int(2560, 1259), new Vector2Int(2531, 1327),
                new Vector2Int(2360, 1080), new Vector2Int(1080, 1920)
            };
            foreach (Vector2Int size in sizes)
            {
                var layout = new PixelUILayout(size.x, size.y);
                Require(layout.FontSize(20) == Mathf.RoundToInt(20 * layout.Scale), "native font size");
                Rect viewport = layout.ToPixels(new Rect(0, 0, 1920, 1080));
                Require(viewport.xMin >= 0 && viewport.yMin >= 0
                    && viewport.xMax <= size.x + 1 && viewport.yMax <= size.y + 1, "letterbox bounds");
                for (int index = 0; index < 14; index++)
                {
                    Rect cell = layout.ToPixels(new Rect(375 + index % 7 * 158,
                        838 + index / 7 * 62, 151, 54));
                    Require(cell.xMin == Mathf.Round(cell.xMin)
                        && cell.yMin == Mathf.Round(cell.yMin)
                        && cell.xMax == Mathf.Round(cell.xMax)
                        && cell.yMax == Mathf.Round(cell.yMax), "pixel-aligned tray");
                }
            }
            Require(new PixelUILayout(3840, 2160).FontSize(20) == 40, "4K glyph resolution");
            Require(new PixelUILayout(1920, 1080).FontSize(20) == 20, "1080p glyph resolution");

            if (EditorApplication.isPlaying)
            {
                var live = new PixelUILayout(Screen.width, Screen.height);
                for (int index = 0; index < BicycleAssemblyInfo.OrderedIds.Length; index++)
                {
                    Rect cell = live.ToPixels(new Rect(375 + index % 7 * 158,
                        838 + index / 7 * 62, 151, 54));
                    Vector2 input = new Vector2(cell.center.x, Screen.height - cell.center.y);
                    Require(PrototypeUI.TrayAssemblyAtScreenPosition(input)
                        == BicycleAssemblyInfo.OrderedIds[index], "live tray hit " + index);
                }
            }
            Debug.Log("MECH_MASTER_UI_PIXEL_VALIDATION_OK resolutions=8, nativeFonts=20/40"
                + ", liveTrayHits=" + (EditorApplication.isPlaying ? "14" : "not-in-play-mode"));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("UI pixel validation: " + message);
        }
    }
}
