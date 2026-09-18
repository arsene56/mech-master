using MechMaster.Domain;
using UnityEngine;

namespace MechMaster.Runtime.UI
{
    public sealed class PrototypeUI : MonoBehaviour
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
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
        private GUIStyle subtitleStyle;
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
            Vector2 guiPosition = ScreenToReferencePosition(screenPosition);

            for (int index = 0; index < BicycleAssemblyInfo.OrderedIds.Length; index++)
            {
                if (TrayCellRect(index).Contains(guiPosition))
                {
                    return BicycleAssemblyInfo.OrderedIds[index];
                }
            }

            return null;
        }

        public static bool IsScreenPositionOverPanel(Vector2 screenPosition)
        {
            Vector2 guiPosition = ScreenToReferencePosition(screenPosition);
            Rect left = new Rect(20f, 96f, 330f, 870f);
            Rect right = new Rect(1490f, 96f, 410f, 870f);
            Rect top = new Rect(0f, 0f, ReferenceWidth, 92f);
            Rect bottom = new Rect(355f, 970f, 1130f, 90f);
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
            Matrix4x4 previousMatrix = GUI.matrix;
            float scale = GetUniformScale();
            Vector2 offset = GetLetterboxOffset(scale);
            GUI.matrix = Matrix4x4.TRS(
                new Vector3(offset.x, offset.y, 0f),
                Quaternion.identity,
                new Vector3(scale, scale, 1f));

            DrawHeader();
            DrawControls();
            DrawKnowledgePanel();
            DrawPartsTray();
            DrawStatus();
            GUI.matrix = previousMatrix;
        }

        private static Vector2 ScreenToReferencePosition(Vector2 screenPosition)
        {
            float scale = GetUniformScale();
            Vector2 offset = GetLetterboxOffset(scale);
            return new Vector2(
                (screenPosition.x - offset.x) / scale,
                (Screen.height - screenPosition.y - offset.y) / scale);
        }

        private static float GetUniformScale()
        {
            return Mathf.Max(0.01f, Mathf.Min(
                Screen.width / ReferenceWidth,
                Screen.height / ReferenceHeight));
        }

        private static Vector2 GetLetterboxOffset(float scale)
        {
            return new Vector2(
                (Screen.width - ReferenceWidth * scale) * 0.5f,
                (Screen.height - ReferenceHeight * scale) * 0.5f);
        }

        private void DrawHeader()
        {
            GUI.Label(new Rect(24f, 14f, 520f, 48f), "机械大师", titleStyle);
            GUI.Label(new Rect(26f, 60f, 520f, 26f), "拆解万物，解锁机秘", subtitleStyle);
            GUI.Label(
                new Rect(690f, 24f, 540f, 44f),
                "27.5 英寸 2×10 工程拆装整车",
                headingStyle);
        }

        private void DrawControls()
        {
            MechMasterApp app = MechMasterApp.Instance;
            GUILayout.BeginArea(new Rect(20f, 96f, 330f, 870f), boxStyle);
            GUILayout.Label("难度", headingStyle);
            DifficultyButton("启蒙 6–8 岁", DifficultyLevel.Simple);
            DifficultyButton("探索 9–12 岁", DifficultyLevel.Standard);
            DifficultyButton("进阶 9–12 岁", DifficultyLevel.Advanced);

            GUILayout.Space(22f);
            GUILayout.Label("工具", headingStyle);
            ToolButton("手", ToolKind.Hand);
            ToolButton("内六角扳手", ToolKind.HexKey);
            ToolButton("梅花扳手", ToolKind.TorxKey);

            GUILayout.Space(22f);
            GUILayout.Label("流程", headingStyle);
            GUILayout.Label(
                (app.Plan.Mode == AssemblyMode.Disassemble ? "拆解" : "组装")
                + "进度  " + app.GetProgressText(),
                bodyStyle);
            if (GUILayout.Button(
                app.Plan.Mode == AssemblyMode.Disassemble ? "完成后开始组装" : "完成后重新拆解",
                buttonStyle,
                GUILayout.Height(54f)))
            {
                app.ToggleMode();
            }

            if (GUILayout.Button("重置整车进度", buttonStyle, GUILayout.Height(48f)))
            {
                app.ResetCurrentPlan();
            }

            GUILayout.Space(16f);
            bool narration = app.NarrationEnabled;
            bool changed = GUILayout.Toggle(narration, " 开启讲解提示", bodyStyle);
            if (changed != narration)
            {
                app.SetNarrationEnabled(changed);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("操作：零件不限制顺序。选择匹配工具，再把零件拖入对应分类槽；组装时反向拖回。", bodyStyle);
            GUILayout.EndArea();
        }

        private void DrawPartsTray()
        {
            MechMasterApp app = MechMasterApp.Instance;
            GUI.Box(new Rect(355f, 790f, 1130f, 176f), GUIContent.none, trayPanelStyle);
            GUI.Label(
                new Rect(375f, 799f, 700f, 32f),
                "拆下零件展示区 · 绿色可放入 / 橙色需更换工具",
                headingStyle);

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

                Rect cell = TrayCellRect(index);
                string label = BicycleAssemblyInfo.DisplayName(assemblyId)
                    + "  " + removed + " / " + total
                    + "\n" + (string.IsNullOrEmpty(latestPartName) ? "空" : latestPartName);
                GUIStyle cellStyle = ResolveTrayCellStyle(app, assemblyId, removed > 0);
                GUI.Box(
                    cell,
                    new GUIContent(label, string.IsNullOrEmpty(latestPartName)
                        ? "尚未拆下零件"
                        : "最近拆下：" + latestPartName),
                    cellStyle);
            }
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

            GUILayout.BeginArea(new Rect(1490f, 96f, 410f, 870f), boxStyle);
            GUILayout.Label("零件知识", headingStyle);
            if (part == null)
            {
                GUILayout.Label("本轮已经完成。", bodyStyle);
            }
            else
            {
                GUILayout.Label(part.DisplayName, titleStyle);
                GUILayout.Space(8f);
                GUILayout.Label(
                    "所需工具：" + DisassemblyPlan.ToolDisplayName(part.RequiredTool),
                    subtitleStyle);
                GUILayout.Space(18f);
                GUILayout.Label(part.GetKnowledge(app.Plan.Difficulty), bodyStyle);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(
                app.Plan.Mode == AssemblyMode.Disassemble
                    ? "自由拆解：可选择任意尚未拆下的零件"
                    : "自由组装：可选择任意托盘中的零件",
                headingStyle);
            GUILayout.EndArea();
        }

        private void DrawStatus()
        {
            GUI.Box(new Rect(355f, 970f, 1130f, 90f), GUIContent.none, boxStyle);
            GUI.Label(new Rect(385f, 990f, 1070f, 52f), MechMasterApp.Instance.StatusMessage, statusStyle);
        }

        private void DifficultyButton(string label, DifficultyLevel value)
        {
            bool selected = MechMasterApp.Instance.Plan.Difficulty == value;
            if (GUILayout.Button(label, selected ? selectedButtonStyle : buttonStyle, GUILayout.Height(54f)))
            {
                MechMasterApp.Instance.SetDifficulty(value);
            }
        }

        private void ToolButton(string label, ToolKind value)
        {
            bool selected = MechMasterApp.Instance.SelectedTool == value;
            if (GUILayout.Button(label, selected ? selectedButtonStyle : buttonStyle, GUILayout.Height(50f)))
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

            Font chineseFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC", "Arial" },
                26);
            titleStyle = CreateLabelStyle(chineseFont, 32, FontStyle.Bold, Color.white);
            subtitleStyle = CreateLabelStyle(chineseFont, 20, FontStyle.Normal, new Color(0.62f, 0.76f, 0.9f));
            headingStyle = CreateLabelStyle(chineseFont, 24, FontStyle.Bold, new Color(0.91f, 0.95f, 1f));
            bodyStyle = CreateLabelStyle(chineseFont, 20, FontStyle.Normal, new Color(0.85f, 0.89f, 0.94f));
            bodyStyle.wordWrap = true;
            bodyStyle.richText = true;
            statusStyle = CreateLabelStyle(chineseFont, 22, FontStyle.Normal, Color.white);
            statusStyle.alignment = TextAnchor.MiddleCenter;

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                font = chineseFont,
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(3, 3, 5, 5),
                normal = { textColor = Color.white }
            };
            selectedButtonStyle = new GUIStyle(buttonStyle);
            selectedButtonStyle.normal.background = MakeTexture(new Color(0.1f, 0.42f, 0.68f, 1f));
            selectedButtonStyle.hover.background = MakeTexture(new Color(0.13f, 0.5f, 0.78f, 1f));
            boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(20, 20, 20, 20),
                normal = { background = MakeTexture(new Color(0.035f, 0.055f, 0.085f, 0.94f)) }
            };
            trayCellStyle = new GUIStyle(GUI.skin.box)
            {
                font = chineseFont,
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal =
                {
                    textColor = new Color(0.67f, 0.73f, 0.82f),
                    background = MakeTexture(new Color(0.055f, 0.075f, 0.105f, 0.58f))
                }
            };
            trayPanelStyle = new GUIStyle(boxStyle);
            trayPanelStyle.normal.background = MakeTexture(new Color(0.035f, 0.055f, 0.085f, 0.56f));
            trayCellActiveStyle = new GUIStyle(trayCellStyle);
            trayCellActiveStyle.normal.textColor = Color.white;
            trayCellActiveStyle.normal.background = MakeTexture(new Color(0.08f, 0.38f, 0.29f, 0.72f));
            trayCellReadyStyle = new GUIStyle(trayCellStyle);
            trayCellReadyStyle.normal.textColor = Color.white;
            trayCellReadyStyle.normal.background = MakeTexture(new Color(0.08f, 0.72f, 0.32f, 0.92f));
            trayCellBlockedStyle = new GUIStyle(trayCellStyle);
            trayCellBlockedStyle.normal.textColor = Color.white;
            trayCellBlockedStyle.normal.background = MakeTexture(new Color(0.82f, 0.48f, 0.06f, 0.9f));
            trayCellWrongStyle = new GUIStyle(trayCellStyle);
            trayCellWrongStyle.normal.textColor = Color.white;
            trayCellWrongStyle.normal.background = MakeTexture(new Color(0.72f, 0.12f, 0.12f, 0.9f));
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
                normal = { textColor = color }
            };
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
