using System;
using System.Reflection;
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
            Require(WorkshopLayout.BrandTitle.xMax < WorkshopLayout.ModelSelector.xMin,
                "model selector must not overlap brand");
            Require(WorkshopLayout.ModelSelector.xMax < WorkshopLayout.ViewButton.xMin,
                "model selector must be left of view button");
            Require(WorkshopLayout.ViewButton.xMax < WorkshopLayout.PartButton.xMin
                && WorkshopLayout.PartButton.xMax < WorkshopLayout.WholeButton.xMin
                && WorkshopLayout.WholeButton.xMax < WorkshopLayout.StorageButton.xMin
                && WorkshopLayout.StorageButton.xMax < WorkshopLayout.HeaderGuide.xMin
                && WorkshopLayout.HeaderGuide.xMax <= WorkshopLayout.Header.xMax,
                "header controls must fit without overlap");
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
                    Rect cell = layout.ToPixels(WorkshopLayout.TrayCell(index));
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
                var ui = UnityEngine.Object.FindObjectOfType<PrototypeUI>();
                var brand = (GUIStyle)typeof(PrototypeUI).GetField("brandStyle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
                Require(brand != null, "brand style initialized");
                var slogan = (GUIStyle)typeof(PrototypeUI).GetField("sloganStyle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
                Require(slogan != null, "slogan style initialized");
                foreach (Vector2Int size in sizes)
                {
                    var layout = new PixelUILayout(size.x, size.y);
                    var scaled = new GUIStyle(brand) { fontSize = layout.FontSize(30) };
                    Vector2 text = scaled.CalcSize(new GUIContent("机械大师"));
                    Rect rect = layout.ToPixels(WorkshopLayout.BrandTitle);
                    Require(text.x <= rect.width && text.y <= rect.height, "brand title must fit at " + size);
                    var scaledSlogan = new GUIStyle(slogan) { fontSize = layout.FontSize(16) };
                    Vector2 sloganSize = scaledSlogan.CalcSize(new GUIContent("拆解万物，解锁机秘"));
                    Rect sloganRect = layout.ToPixels(WorkshopLayout.BrandSlogan);
                    Require(sloganSize.x <= sloganRect.width && sloganSize.y <= sloganRect.height,
                        "brand slogan must fit at " + size);
                }
                var live = new PixelUILayout(Screen.width, Screen.height);
                MechanicalModelDefinition model = MechMasterApp.Instance.Model;
                for (int index = 0; index < model.assemblies.Length && index < 14; index++)
                {
                    Rect cell = live.ToPixels(WorkshopLayout.TrayCell(index));
                    Vector2 input = new Vector2(cell.center.x, Screen.height - cell.center.y);
                    Require(PrototypeUI.TrayAssemblyAtScreenPosition(input)
                        == model.assemblies[index].id, "live tray hit " + index);
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
