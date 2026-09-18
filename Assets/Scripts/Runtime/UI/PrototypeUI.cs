using MechMaster.Domain;
using UnityEngine;

namespace MechMaster.Runtime.UI
{
    public sealed class PrototypeUI : MonoBehaviour
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle statusStyle;
        private GUIStyle buttonStyle;
        private GUIStyle selectedButtonStyle;
        private GUIStyle boxStyle;

        public static bool IsScreenPositionOverPanel(Vector2 screenPosition)
        {
            float scaleX = Screen.width / ReferenceWidth;
            float scaleY = Screen.height / ReferenceHeight;
            Vector2 guiPosition = new Vector2(
                screenPosition.x / scaleX,
                ReferenceHeight - screenPosition.y / scaleY);
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
            GUI.matrix = Matrix4x4.Scale(new Vector3(
                Screen.width / ReferenceWidth,
                Screen.height / ReferenceHeight,
                1f));

            DrawHeader();
            DrawControls();
            DrawKnowledgePanel();
            DrawStatus();
            GUI.matrix = previousMatrix;
        }

        private void DrawHeader()
        {
            GUI.Label(new Rect(24f, 14f, 520f, 48f), "机械大师", titleStyle);
            GUI.Label(new Rect(26f, 60f, 520f, 26f), "拆解万物，解锁机秘", subtitleStyle);
            GUI.Label(
                new Rect(690f, 24f, 540f, 44f),
                "自行车 · 前轮液压碟刹拆装样片",
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

            if (GUILayout.Button("重置本关", buttonStyle, GUILayout.Height(48f)))
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
            GUILayout.Label("操作：先选择工具，再拖动零件，松手后拆下或装回。", bodyStyle);
            GUILayout.EndArea();
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
            PartDefinition expected = app.Plan.ExpectedPart;
            GUILayout.Label(
                expected == null ? "当前流程已完成" : "下一步：" + expected.DisplayName,
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

