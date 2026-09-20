using System.Collections.Generic;
using MechMaster.Domain;
using UnityEngine;

namespace MechMaster.Runtime.UI
{
    public sealed class PrototypeUI : MonoBehaviour
    {
        private const float TrayStartX = 375f;
        private const float TrayStartY = 838f;
        private const float TrayCellWidth = 151f;
        private const float TrayCellHeight = 54f;
        private const float TrayGapX = 7f;
        private const float TrayGapY = 8f;
        private static string draggedPartId;
        private static string draggedAssemblyId;
        private static string hoveredAssemblyId;
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle statusStyle;
        private GUIStyle buttonStyle;
        private GUIStyle selectedButtonStyle;
        private GUIStyle boxStyle;
        private GUIStyle trayPanelStyle;
        private GUIStyle trayCellStyle;
        private GUIStyle trayCellActiveStyle;
        private GUIStyle trayCellReadyStyle;
        private GUIStyle trayCellBlockedStyle;
        private GUIStyle trayCellWrongStyle;
        private GUIStyle selectedToolStyle;
        private GUIStyle actionStyle;
        private GUIStyle captionStyle;
        private GUIStyle chipStyle;
        private GUIStyle hintStyle;
        private GUIStyle shadowStyle;
        private GUIStyle progressTrackStyle;
        private GUIStyle progressFillStyle;
        private GUIStyle statusPanelStyle;
        private Texture2D gearTexture;
        private Font chineseFont;
        private readonly List<Texture2D> ownedTextures = new List<Texture2D>();
        private readonly List<StyleMetrics> styleMetrics = new List<StyleMetrics>();
        private float appliedStyleScale = -1f;
        private static PixelUILayout CurrentLayout => new PixelUILayout(Screen.width, Screen.height);

        private sealed class StyleMetrics
        {
            public GUIStyle Style;
            public int FontSize;
            public RectOffset Padding;
            public RectOffset Margin;
        }

        public static string HoveredTrayAssemblyId => hoveredAssemblyId;

        public static void SetTrayDragFeedback(
            string partId,
            string partAssemblyId,
            Vector2 screenPosition)
        {
            draggedPartId = partId;
            draggedAssemblyId = partAssemblyId;
            hoveredAssemblyId = TrayAssemblyAtScreenPosition(screenPosition);
        }

        public static void ClearTrayDragFeedback()
        {
            draggedPartId = null;
            draggedAssemblyId = null;
            hoveredAssemblyId = null;
        }

        public static string TrayAssemblyAtScreenPosition(Vector2 screenPosition)
        {
            Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);

            for (int index = 0; index < BicycleAssemblyInfo.OrderedIds.Length; index++)
            {
                if (CurrentLayout.ToPixels(TrayCellRect(index)).Contains(guiPosition))
                {
                    return BicycleAssemblyInfo.OrderedIds[index];
                }
            }

            return null;
        }

        public static bool IsScreenPositionOverPanel(Vector2 screenPosition)
        {
            Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            Rect left = PixelRect(20f, 96f, 330f, 870f);
            Rect right = PixelRect(1490f, 96f, 410f, 870f);
            Rect top = PixelRect(0f, 0f, PixelUILayout.ReferenceWidth, 92f);
            Rect bottom = PixelRect(355f, 970f, 1130f, 90f);
            return left.Contains(guiPosition)
                || right.Contains(guiPosition)
                || top.Contains(guiPosition)
                || bottom.Contains(guiPosition);
        }

        private void OnGUI()
        {
            if (MechMasterApp.Instance == null || MechMasterApp.Instance.Plan == null)
            {
                return;
            }

            EnsureStyles();
            UpdateStyleScale();
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            try
            {
                DrawPanelShadows();
                DrawHeader();
                DrawControls();
                DrawKnowledgePanel();
                DrawPartsTray();
                DrawStatus();
            }
            finally
            {
                GUI.matrix = previousMatrix;
            }
        }

        private static Rect PixelRect(float x, float y, float width, float height) =>
            CurrentLayout.ToPixels(new Rect(x, y, width, height));

        private static float Pixels(float length) => CurrentLayout.Length(length);

        private void DrawHeader()
        {
            GUI.Box(PixelRect(20f, 8f, 1880f, 78f), GUIContent.none, boxStyle);
            GUI.DrawTexture(PixelRect(36f, 20f, 52f, 52f), gearTexture);
            GUI.Label(PixelRect(102f, 10f, 480f, 42f), "机械大师", titleStyle);
            GUI.Label(PixelRect(105f, 51f, 480f, 28f), "拆解万物，解锁机秘", captionStyle);
            GUI.Label(
                PixelRect(660f, 25f, 750f, 40f),
                "自行车工坊  /  27.5 英寸 · 2×10",
                headingStyle);
            GUI.Box(PixelRect(1585f, 27f, 285f, 38f), "动手探索 · 发现机械奥秘", chipStyle);
        }

        private void DrawPanelShadows()
        {
            GUI.Box(PixelRect(20f, 101f, 330f, 870f), GUIContent.none, shadowStyle);
            GUI.Box(PixelRect(1490f, 101f, 410f, 870f), GUIContent.none, shadowStyle);
            GUI.Box(PixelRect(355f, 795f, 1130f, 176f), GUIContent.none, shadowStyle);
        }

        private void DrawControls()
        {
            MechMasterApp app = MechMasterApp.Instance;
            GUILayout.BeginArea(PixelRect(20f, 96f, 330f, 870f), boxStyle);
            GUILayout.Label("选择探索难度", headingStyle);
            DifficultyButton("启蒙 · 6–8 岁", DifficultyLevel.Simple);
            DifficultyButton("探索 · 9–12 岁", DifficultyLevel.Standard);
            DifficultyButton("进阶 · 9–12 岁", DifficultyLevel.Advanced);

            GUILayout.Space(Pixels(22f));
            GUILayout.Label("我的工具箱", headingStyle);
            ToolButton("手", ToolKind.Hand);
            ToolButton("内六角扳手", ToolKind.HexKey);
            ToolButton("梅花扳手", ToolKind.TorxKey);

            GUILayout.Space(Pixels(22f));
            GUILayout.Label("拆装工作台", headingStyle);
            GUILayout.Label(
                (app.Plan.Mode == AssemblyMode.Disassemble ? "拆解" : "组装")
                + "进度  " + app.GetProgressText(),
                bodyStyle);
            Rect progressRect = GUILayoutUtility.GetRect(1f, Pixels(10f), GUILayout.ExpandWidth(true));
            GUI.Box(progressRect, GUIContent.none, progressTrackStyle);
            float progress = app.Plan.RemovedCount / (float)app.Plan.Steps.Count;
            if (app.Plan.Mode == AssemblyMode.Assemble) progress = 1f - progress;
            if (progress > 0f)
            {
                GUI.Box(new Rect(progressRect.x, progressRect.y,
                    progressRect.width * progress, progressRect.height), GUIContent.none, progressFillStyle);
            }
            GUILayout.Space(Pixels(8f));
            if (GUILayout.Button(
                app.Plan.Mode == AssemblyMode.Disassemble ? "完成后开始组装" : "完成后重新拆解",
                actionStyle,
                GUILayout.Height(Pixels(54f))))
            {
                app.ToggleMode();
            }

            if (GUILayout.Button("重置整车进度", buttonStyle, GUILayout.Height(Pixels(48f))))
            {
                app.ResetCurrentPlan();
            }

            GUILayout.Space(Pixels(16f));
            bool narration = app.NarrationEnabled;
            if (GUILayout.Button(narration ? "讲解提示  ·  已开启" : "讲解提示  ·  已关闭",
                narration ? selectedButtonStyle : buttonStyle, GUILayout.Height(Pixels(44f))))
            {
                app.SetNarrationEnabled(!narration);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("试一试\n选好工具，把零件拖进同类收纳格。变绿后松手！", hintStyle);
            GUILayout.EndArea();
        }

        private void DrawPartsTray()
        {
            MechMasterApp app = MechMasterApp.Instance;
            GUI.Box(PixelRect(355f, 790f, 1130f, 176f), GUIContent.none, trayPanelStyle);
            GUI.Label(
                PixelRect(375f, 799f, 320f, 32f),
                "零件收纳站",
                headingStyle);
            GUI.Label(PixelRect(720f, 805f, 745f, 27f),
                "绿色：可放入    橙色：检查工具    红色：换个分类", captionStyle);

            for (int index = 0; index < BicycleAssemblyInfo.OrderedIds.Length; index++)
            {
                string assemblyId = BicycleAssemblyInfo.OrderedIds[index];
                int removed = 0;
                int total = 0;
                string latestPartName = string.Empty;
                foreach (PartDefinition part in app.Plan.Steps)
                {
                    if (part.AssemblyId != assemblyId)
                    {
                        continue;
                    }

                    total++;
                    if (app.Plan.IsRemoved(part.Id))
                    {
                        removed++;
                        latestPartName = part.DisplayName;
                    }
                }

                Rect cell = CurrentLayout.ToPixels(TrayCellRect(index));
                string label = BicycleAssemblyInfo.DisplayName(assemblyId)
                    + "  " + removed + "/" + total
                    + "\n" + (string.IsNullOrEmpty(latestPartName) ? "空" : latestPartName);
                GUIStyle cellStyle = ResolveTrayCellStyle(app, assemblyId, removed > 0);
                if (hoveredAssemblyId == assemblyId && !string.IsNullOrEmpty(draggedPartId))
                {
                    label = BicycleAssemblyInfo.DisplayName(assemblyId) + "\n"
                        + (cellStyle == trayCellReadyStyle ? "松手放入"
                            : cellStyle == trayCellWrongStyle ? "换个分类" : "检查工具 / 零件状态");
                }
                // Keep each cell to two lines even with three-digit counts or long part names.
                string[] lines = label.Split('\n');
                label = FitTrayLine(lines[0], cellStyle, cell.width) + "\n"
                    + FitTrayLine(lines[1], cellStyle, cell.width);
                GUI.Box(
                    cell,
                    new GUIContent(label, string.IsNullOrEmpty(latestPartName)
                        ? "尚未拆下零件"
                        : "最近拆下：" + latestPartName),
                    cellStyle);
            }
        }

        private static string FitTrayLine(string text, GUIStyle style, float width)
        {
            if (style.CalcSize(new GUIContent(text)).x <= width) return text;
            for (int length = text.Length - 1; length > 0; length--)
            {
                string shortened = text.Substring(0, length) + "…";
                if (style.CalcSize(new GUIContent(shortened)).x <= width) return shortened;
            }
            return "…";
        }

        private GUIStyle ResolveTrayCellStyle(
            MechMasterApp app,
            string assemblyId,
            bool hasParts)
        {
            if (hoveredAssemblyId != assemblyId || string.IsNullOrEmpty(draggedPartId))
            {
                return hasParts ? trayCellActiveStyle : trayCellStyle;
            }

            if (draggedAssemblyId != assemblyId)
            {
                return trayCellWrongStyle;
            }

            PartDefinition draggedPart = null;
            foreach (PartDefinition part in app.Plan.Steps)
            {
                if (part.Id == draggedPartId)
                {
                    draggedPart = part;
                    break;
                }
            }
            bool stateAllowsOperation = draggedPart != null
                && (app.Plan.Mode == AssemblyMode.Disassemble
                    ? !app.Plan.IsRemoved(draggedPart.Id)
                    : app.Plan.IsRemoved(draggedPart.Id));
            bool ready = stateAllowsOperation
                && draggedPart.RequiredTool == app.SelectedTool;
            return ready ? trayCellReadyStyle : trayCellBlockedStyle;
        }

        private static Rect TrayCellRect(int index)
        {
            int column = index % 7;
            int row = index / 7;
            return new Rect(
                TrayStartX + column * (TrayCellWidth + TrayGapX),
                TrayStartY + row * (TrayCellHeight + TrayGapY),
                TrayCellWidth,
                TrayCellHeight);
        }

        private void DrawKnowledgePanel()
        {
            MechMasterApp app = MechMasterApp.Instance;
            PartDefinition part = app.SelectedPart ?? app.Plan.ExpectedPart;

            GUILayout.BeginArea(PixelRect(1490f, 96f, 410f, 870f), boxStyle);
            GUILayout.Label("零件小百科", headingStyle);
            GUILayout.Space(Pixels(18f));
            if (part == null)
            {
                GUILayout.Label("本轮已经完成。", bodyStyle);
            }
            else
            {
                GUILayout.Label(part.DisplayName, titleStyle);
                GUILayout.Space(Pixels(8f));
                GUILayout.Label(
                    "所需工具：" + DisassemblyPlan.ToolDisplayName(part.RequiredTool),
                    hintStyle);
                GUILayout.Space(Pixels(18f));
                GUILayout.Label(part.GetKnowledge(app.Plan.Difficulty), bodyStyle);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(
                app.Plan.Mode == AssemblyMode.Disassemble
                    ? "自由拆解：可选择任意尚未拆下的零件"
                    : "自由组装：可选择任意托盘中的零件",
                hintStyle);
            GUILayout.EndArea();
        }

        private void DrawStatus()
        {
            GUI.Box(PixelRect(355f, 970f, 1130f, 90f), GUIContent.none, statusPanelStyle);
            GUI.Label(PixelRect(376f, 996f, 120f, 34f), "工坊提示", headingStyle);
            GUI.Label(PixelRect(505f, 981f, 955f, 68f), MechMasterApp.Instance.StatusMessage, statusStyle);
        }

        private void DifficultyButton(string label, DifficultyLevel value)
        {
            bool selected = MechMasterApp.Instance.Plan.Difficulty == value;
            if (GUILayout.Button((selected ? "●  " : "○  ") + label,
                selected ? selectedButtonStyle : buttonStyle, GUILayout.Height(Pixels(54f))))
            {
                MechMasterApp.Instance.SetDifficulty(value);
            }
        }

        private void ToolButton(string label, ToolKind value)
        {
            bool selected = MechMasterApp.Instance.SelectedTool == value;
            if (GUILayout.Button((selected ? "●  " : "○  ") + label,
                selected ? selectedToolStyle : buttonStyle, GUILayout.Height(Pixels(50f))))
            {
                MechMasterApp.Instance.SetTool(value);
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            chineseFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC", "Arial" },
                26);
            titleStyle = CreateLabelStyle(chineseFont, 32, FontStyle.Bold, WorkshopTheme.Ink);
            headingStyle = CreateLabelStyle(chineseFont, 24, FontStyle.Bold, WorkshopTheme.Teal);
            bodyStyle = CreateLabelStyle(chineseFont, 20, FontStyle.Normal, WorkshopTheme.Ink);
            bodyStyle.richText = true;
            captionStyle = CreateLabelStyle(chineseFont, 18, FontStyle.Normal, WorkshopTheme.MutedInk);
            statusStyle = CreateLabelStyle(chineseFont, 21, FontStyle.Normal, WorkshopTheme.Ink);
            statusStyle.alignment = TextAnchor.MiddleLeft;

            buttonStyle = CreateButton(WorkshopTheme.Sky, WorkshopTheme.Ink, WorkshopTheme.Border);
            selectedButtonStyle = CreateButton(WorkshopTheme.Teal, Color.white, WorkshopTheme.Teal);
            selectedToolStyle = CreateButton(WorkshopTheme.Orange, WorkshopTheme.OrangeInk,
                new Color32(223, 154, 67, 255));
            actionStyle = new GUIStyle(selectedToolStyle) { fontStyle = FontStyle.Bold };
            boxStyle = CreateSurface(WorkshopTheme.Surface, WorkshopTheme.Border, 20, WorkshopTheme.Ink);
            boxStyle.padding = new RectOffset(22, 22, 24, 22);
            trayPanelStyle = CreateSurface(new Color(0.96f, 0.985f, 0.97f, 0.9f),
                WorkshopTheme.Border, 20, WorkshopTheme.Ink);
            trayCellStyle = CreateSurface(WorkshopTheme.Sky, WorkshopTheme.Border, 16, WorkshopTheme.Ink);
            trayCellActiveStyle = CreateSurface(WorkshopTheme.Mint, WorkshopTheme.Border, 16, WorkshopTheme.Ink);
            trayCellReadyStyle = CreateSurface(WorkshopTheme.Ready, WorkshopTheme.Ready, 17, Color.white);
            trayCellBlockedStyle = CreateSurface(WorkshopTheme.Blocked, new Color32(212, 146, 45, 255),
                16, WorkshopTheme.OrangeInk);
            trayCellWrongStyle = CreateSurface(WorkshopTheme.Wrong, WorkshopTheme.Wrong, 17, Color.white);
            foreach (GUIStyle cellStyle in new[] { trayCellStyle, trayCellActiveStyle,
                trayCellReadyStyle, trayCellBlockedStyle, trayCellWrongStyle })
            {
                cellStyle.wordWrap = false;
                cellStyle.padding = new RectOffset(4, 4, 2, 2);
            }
            chipStyle = CreateSurface(WorkshopTheme.Mint, Color.clear, 18, WorkshopTheme.Teal);
            hintStyle = CreateSurface(new Color32(239, 246, 243, 255), Color.clear, 19, WorkshopTheme.MutedInk);
            hintStyle.alignment = TextAnchor.MiddleLeft;
            hintStyle.padding = new RectOffset(14, 14, 12, 12);
            shadowStyle = CreateSurface(new Color(0.22f, 0.38f, 0.42f, 0.09f), Color.clear, 16, WorkshopTheme.Ink);
            progressTrackStyle = CreateSurface(WorkshopTheme.Sky, Color.clear, 16, WorkshopTheme.Ink);
            progressFillStyle = CreateSurface(WorkshopTheme.Teal, Color.clear, 16, WorkshopTheme.Ink);
            statusPanelStyle = CreateSurface(WorkshopTheme.Mint, WorkshopTheme.Border, 20, WorkshopTheme.Ink);
            gearTexture = MakeGearTexture();
            foreach (GUIStyle style in new[]
            {
                titleStyle, headingStyle, bodyStyle, captionStyle, statusStyle, buttonStyle,
                selectedButtonStyle, selectedToolStyle, actionStyle, boxStyle, trayPanelStyle,
                trayCellStyle, trayCellActiveStyle, trayCellReadyStyle, trayCellBlockedStyle,
                trayCellWrongStyle, chipStyle, hintStyle, shadowStyle, progressTrackStyle,
                progressFillStyle, statusPanelStyle
            })
            {
                styleMetrics.Add(new StyleMetrics
                {
                    Style = style, FontSize = style.fontSize,
                    Padding = new RectOffset(style.padding.left, style.padding.right,
                        style.padding.top, style.padding.bottom),
                    Margin = new RectOffset(style.margin.left, style.margin.right,
                        style.margin.top, style.margin.bottom)
                });
            }
        }

        private void UpdateStyleScale()
        {
            PixelUILayout layout = CurrentLayout;
            if (Mathf.Approximately(appliedStyleScale, layout.Scale)) return;
            foreach (StyleMetrics metrics in styleMetrics)
            {
                metrics.Style.fontSize = layout.FontSize(metrics.FontSize);
                metrics.Style.padding = layout.ScaleInsets(metrics.Padding);
                metrics.Style.margin = layout.ScaleInsets(metrics.Margin);
            }
            appliedStyleScale = layout.Scale;
            Debug.Log("MECH_MASTER_UI_PIXELS screen=" + Screen.width + "x" + Screen.height
                + ", layoutScale=" + layout.Scale.ToString("F3")
                + ", bodyFontPixels=" + bodyStyle.fontSize + ", matrixScale=1");
        }

        private static GUIStyle CreateLabelStyle(
            Font font,
            int size,
            FontStyle fontStyle,
            Color color)
        {
            return new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = size,
                fontStyle = fontStyle,
                wordWrap = true,
                normal = { textColor = color }
            };
        }

        private GUIStyle CreateButton(Color fill, Color ink, Color outline)
        {
            GUIStyle style = CreateSurface(fill, outline, 20, ink);
            style.margin = new RectOffset(0, 0, 5, 5);
            Texture2D hover = MakeRoundedTexture(Color.Lerp(fill, Color.white, 0.15f), outline);
            Texture2D pressed = MakeRoundedTexture(Color.Lerp(fill, WorkshopTheme.Teal, 0.13f), outline);
            SetState(style.hover, hover, ink);
            SetState(style.onHover, hover, ink);
            SetState(style.active, pressed, ink);
            SetState(style.onActive, pressed, ink);
            // Explicit focus colors prevent the Editor's dark default skin bleeding through.
            SetState(style.focused, hover, ink);
            SetState(style.onFocused, hover, ink);
            return style;
        }

        private GUIStyle CreateSurface(Color fill, Color outline, int fontSize, Color ink)
        {
            GUIStyle style = new GUIStyle
            {
                font = chineseFont,
                fontSize = fontSize,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                border = new RectOffset(16, 16, 16, 16),
                padding = new RectOffset(8, 8, 4, 4)
            };
            Texture2D texture = MakeRoundedTexture(fill, outline);
            SetState(style.normal, texture, ink);
            SetState(style.hover, texture, ink);
            SetState(style.active, texture, ink);
            SetState(style.focused, texture, ink);
            SetState(style.onNormal, texture, ink);
            SetState(style.onHover, texture, ink);
            SetState(style.onActive, texture, ink);
            SetState(style.onFocused, texture, ink);
            return style;
        }

        private static void SetState(GUIStyleState state, Texture2D texture, Color ink)
        {
            state.background = texture;
            state.textColor = ink;
        }

        private Texture2D MakeRoundedTexture(Color fill, Color outline)
        {
            const int size = 64;
            const float radius = 14f;
            Texture2D texture = NewTexture(size);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 q = new Vector2(Mathf.Abs(x + 0.5f - size / 2f),
                        Mathf.Abs(y + 0.5f - size / 2f)) - Vector2.one * (size / 2f - radius - 1f);
                    float distance = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude
                        + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
                    Color color = outline.a > 0f && distance > -1.25f ? outline : fill;
                    color.a *= Mathf.Clamp01(0.5f - distance);
                    pixels[y * size + x] = color;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private Texture2D MakeGearTexture()
        {
            const int size = 96;
            Texture2D texture = NewTexture(size);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size / 2f;
                    float dy = y + 0.5f - size / 2f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float teeth = Mathf.Cos(Mathf.Atan2(dy, dx) * 10f);
                    float outer = teeth > 0.25f ? 45f : 37f;
                    Color color = distance < 20f ? WorkshopTheme.Orange : WorkshopTheme.Teal;
                    color.a *= Mathf.Clamp01(outer + 0.5f - distance);
                    pixels[y * size + x] = color;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private Texture2D NewTexture(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.DontSave;
            ownedTextures.Add(texture);
            return texture;
        }

        private void OnDestroy()
        {
            foreach (Texture2D texture in ownedTextures)
            {
                if (texture != null) Destroy(texture);
            }
            ownedTextures.Clear();
            if (chineseFont != null) Destroy(chineseFont);
        }
    }
}
