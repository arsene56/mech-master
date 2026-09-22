using System.Collections.Generic;
using MechMaster.Domain;
using UnityEngine;

namespace MechMaster.Runtime.UI
{
    public sealed class PrototypeUI : MonoBehaviour
    {
        private static string draggedPartId;
        private static string draggedAssemblyId;
        private static string hoveredAssemblyId;
        private static int trayPage;
        private static bool modelMenuOpen;
        private const int TrayCellsPerPage = 14;
        private GUIStyle titleStyle;
        private GUIStyle brandStyle;
        private GUIStyle sloganStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle statusStyle;
        private GUIStyle buttonStyle;
        private GUIStyle selectedButtonStyle;
        private GUIStyle modelButtonStyle;
        private GUIStyle selectedModelButtonStyle;
        private GUIStyle headerGuideStyle;
        private GUIStyle boxStyle;
        private GUIStyle panPanelStyle;
        private GUIStyle panButtonStyle;
        private GUIStyle cadenceButtonStyle;
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
        private Vector2 knowledgeScroll;
        private Vector2 modelMenuScroll;
        private string knowledgePartId;
        private static PixelUILayout CurrentLayout => new PixelUILayout(Screen.width, Screen.height);

        private sealed class StyleMetrics
        {
            public GUIStyle Style;
            public int FontSize;
            public RectOffset Padding;
            public RectOffset Margin;
        }

        public static string HoveredTrayAssemblyId => hoveredAssemblyId;

        public static void ResetTrayPage()
        {
            trayPage = 0;
            modelMenuOpen = false;
            ClearTrayDragFeedback();
        }

        public static void SetTrayDragFeedback(
            string partId,
            string partAssemblyId,
            Vector2 screenPosition)
        {
            draggedPartId = partId;
            draggedAssemblyId = partAssemblyId;
            MechanicalModelDefinition model = MechMasterApp.Instance?.Model;
            if (model != null)
            {
                for (int index = 0; index < model.assemblies.Length; index++)
                {
                    if (model.assemblies[index].id == partAssemblyId)
                    {
                        trayPage = index / TrayCellsPerPage;
                        break;
                    }
                }
            }
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

            MechanicalModelDefinition model = MechMasterApp.Instance?.Model;
            if (model == null) return null;
            int start = trayPage * TrayCellsPerPage;
            int end = Mathf.Min(start + TrayCellsPerPage, model.assemblies.Length);
            for (int index = start; index < end; index++)
            {
                if (CurrentLayout.ToPixels(TrayCellRect(index - start)).Contains(guiPosition))
                {
                    return model.assemblies[index].id;
                }
            }

            return null;
        }

        public static bool IsScreenPositionOverPanel(Vector2 screenPosition)
        {
            Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            Rect left = CurrentLayout.ToPixels(WorkshopLayout.ToolsCollapsed ? WorkshopLayout.ToolsTab : WorkshopLayout.Tools);
            Rect right = CurrentLayout.ToPixels(WorkshopLayout.KnowledgeCollapsed ? WorkshopLayout.KnowledgeTab : WorkshopLayout.Knowledge);
            Rect top = CurrentLayout.ToPixels(WorkshopLayout.Header);
            Rect bottom = CurrentLayout.ToPixels(WorkshopLayout.Status);
            return modelMenuOpen
                || left.Contains(guiPosition)
                || right.Contains(guiPosition)
                || top.Contains(guiPosition)
                || CurrentLayout.ToPixels(WorkshopLayout.Tray).Contains(guiPosition)
                || CurrentLayout.ToPixels(WorkshopLayout.PanControls).Contains(guiPosition)
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
                DrawPanControls();
                DrawPartsTray();
                DrawStatus();
                DrawModelMenu();
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
            MechMasterApp app = MechMasterApp.Instance;
            GUI.Box(CurrentLayout.ToPixels(WorkshopLayout.Header), GUIContent.none, boxStyle);
            GUI.DrawTexture(PixelRect(28, 18, 46, 46), gearTexture);
            GUI.Label(CurrentLayout.ToPixels(WorkshopLayout.BrandTitle), "机械大师", brandStyle);
            GUI.Label(CurrentLayout.ToPixels(WorkshopLayout.BrandSlogan), "拆解万物，解锁机秘", sloganStyle);
            Rect modelButton = CurrentLayout.ToPixels(WorkshopLayout.ModelSelector);
            GUIStyle modelStyle = modelMenuOpen ? selectedModelButtonStyle : modelButtonStyle;
            string modelLabel = FitTrayLine("模型 ▾  " + app.Model.displayName,
                modelStyle, modelButton.width - Pixels(8));
            if (GUI.Button(modelButton, new GUIContent(modelLabel, app.Model.displayName), modelStyle))
                modelMenuOpen = !modelMenuOpen;
            bool view = PartInteractionController.ViewMode;
            if (GUI.Button(CurrentLayout.ToPixels(WorkshopLayout.ViewButton), "旋转视角", view ? selectedButtonStyle : buttonStyle))
                app.SetInteractionViewMode(true);
            if (GUI.Button(CurrentLayout.ToPixels(WorkshopLayout.PartButton), "拆装零件", view ? buttonStyle : selectedToolStyle))
                app.SetInteractionViewMode(false);
            if (GUI.Button(CurrentLayout.ToPixels(WorkshopLayout.WholeButton), "整机归位", buttonStyle))
                app.FrameWholeModel();
            if (GUI.Button(CurrentLayout.ToPixels(WorkshopLayout.StorageButton), "查看收纳", buttonStyle))
                app.FrameStorage();
            if (app.MotionAvailable)
            {
                string motionButton = app.IsMotionPlaying ? "Ⅱ  暂停演示"
                    : app.IsMotionActive ? "▶  继续演示" : "▶  运转演示";
                if (GUI.Button(CurrentLayout.ToPixels(WorkshopLayout.MotionButton),
                    motionButton, app.IsMotionActive ? selectedButtonStyle : buttonStyle))
                    app.ToggleMotion();
                bool enabled = GUI.enabled;
                GUI.enabled = app.IsMotionActive;
                if (GUI.Button(CurrentLayout.ToPixels(WorkshopLayout.MotionStopButton),
                    "结束", buttonStyle))
                    app.StopMotion();
                GUI.enabled = enabled;
            }
            string guide = app.IsMotionActive
                ? "原地踩踏 · 旋转缩放"
                : app.IsGlobalExplosionActive
                ? "全局爆炸 · 拖动旋转 · 双指缩放"
                : app.IsLocalExplosionMode
                    ? "轻点零件爆炸 / 收回 · 拖动旋转"
                    : view
                        ? "拖动旋转 · 点击听讲解 · 双指缩放"
                        : "拖动零件 · 空白处旋转 · 双指缩放";
            Rect guideRect = CurrentLayout.ToPixels(
                app.MotionAvailable ? WorkshopLayout.MotionGuide : WorkshopLayout.HeaderGuide);
            GUI.Label(guideRect,
                FitTrayLine(guide, headerGuideStyle, guideRect.width), headerGuideStyle);
        }

        private void DrawModelMenu()
        {
            if (!modelMenuOpen) return;
            IReadOnlyList<MechanicalModelDefinition> models = MechanicalModelRegistry.Models;
            Rect popup = CurrentLayout.ToPixels(WorkshopLayout.ModelMenu(models.Count));
            GUI.Box(popup, GUIContent.none, boxStyle);
            float padding = Pixels(6);
            float rowHeight = Pixels(44);
            Rect viewport = new Rect(popup.x + padding, popup.y + padding,
                popup.width - padding * 2, popup.height - padding * 2);
            float contentWidth = viewport.width - (models.Count > 6 ? Pixels(18) : 0);
            modelMenuScroll = GUI.BeginScrollView(viewport, modelMenuScroll,
                new Rect(0, 0, contentWidth, models.Count * rowHeight), false, models.Count > 6);
            for (int index = 0; index < models.Count; index++)
            {
                MechanicalModelDefinition model = models[index];
                bool selected = MechMasterApp.Instance.Model == model;
                GUIStyle style = selected ? selectedModelButtonStyle : modelButtonStyle;
                string label = FitTrayLine((selected ? "●  " : "○  ") + model.displayName,
                    style, contentWidth - Pixels(8));
                if (GUI.Button(new Rect(0, index * rowHeight, contentWidth, Pixels(40)), label, style))
                {
                    modelMenuOpen = false;
                    MechMasterApp.Instance.SetModel(model.id);
                }
            }
            GUI.EndScrollView();

            if (Event.current.type == EventType.MouseDown
                && !popup.Contains(Event.current.mousePosition)
                && !CurrentLayout.ToPixels(WorkshopLayout.ModelSelector)
                    .Contains(Event.current.mousePosition))
            {
                modelMenuOpen = false;
                Event.current.Use();
            }
        }

        private void DrawPanControls()
        {
            OrbitCameraController orbit = Camera.main?.GetComponent<OrbitCameraController>();
            if (orbit == null) return;
            Rect area = WorkshopLayout.PanControls;
            GUI.Box(CurrentLayout.ToPixels(area), GUIContent.none, panPanelStyle);
            GUI.Label(PixelRect(area.x + 10, area.y + 2, 140, 28), "画面平移", captionStyle);
            DrawPanButton(area, 1, 0, "↑", Vector2.up, orbit);
            DrawPanButton(area, 0, 1, "←", Vector2.left, orbit);
            DrawPanButton(area, 2, 1, "→", Vector2.right, orbit);
            DrawPanButton(area, 1, 2, "↓", Vector2.down, orbit);
            if (GUI.Button(PixelRect(area.x + 56, area.y + 74, 44, 40), "中", panButtonStyle))
                orbit.ResetPan();
        }

        private void DrawPanButton(Rect area, int column, int row, string label, Vector2 direction, OrbitCameraController orbit)
        {
            if (GUI.Button(PixelRect(area.x + 8 + column * 48, area.y + 30 + row * 44, 44, 40), label, panButtonStyle))
                orbit.Pan(direction * 48f);
        }

        private void DrawPanelShadows()
        {
            foreach (Rect rect in new[] {
                WorkshopLayout.ToolsCollapsed ? WorkshopLayout.ToolsTab : WorkshopLayout.Tools,
                WorkshopLayout.KnowledgeCollapsed ? WorkshopLayout.KnowledgeTab : WorkshopLayout.Knowledge,
                WorkshopLayout.Tray })
                GUI.Box(PixelRect(rect.x, rect.y + 4, rect.width, rect.height), GUIContent.none, shadowStyle);
        }

        private void DrawControls()
        {
            MechMasterApp app = MechMasterApp.Instance;
            if (WorkshopLayout.ToolsCollapsed)
            {
                if (GUI.Button(CurrentLayout.ToPixels(WorkshopLayout.ToolsTab), "菜单", buttonStyle))
                    WorkshopLayout.ToolsCollapsed = false;
                return;
            }
            GUILayout.BeginArea(CurrentLayout.ToPixels(WorkshopLayout.Tools), boxStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("拆解等级", headingStyle, GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("‹", buttonStyle,
                GUILayout.Width(Pixels(36)), GUILayout.Height(Pixels(32))))
                WorkshopLayout.ToolsCollapsed = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(Pixels(8));
            DifficultyButton("简单", DifficultyLevel.Simple);
            DifficultyButton("进阶", DifficultyLevel.Standard);
            DifficultyButton("探索", DifficultyLevel.Advanced);

            GUILayout.Space(Pixels(12f));
            GUILayout.Label("三维爆炸视图", headingStyle);
            bool globalExplosion = app.IsGlobalExplosionActive;
            if (GUILayout.Button(
                (globalExplosion ? "●  " : "○  ") + "全局一键爆炸",
                globalExplosion ? selectedButtonStyle : buttonStyle,
                GUILayout.Height(Pixels(40f))))
            {
                app.ToggleGlobalExplosion();
            }
            bool localExplosion = app.IsLocalExplosionMode;
            if (GUILayout.Button(
                (localExplosion ? "●  " : "○  ") + "点击单件爆炸",
                localExplosion ? selectedButtonStyle : buttonStyle,
                GUILayout.Height(Pixels(40f))))
            {
                app.ToggleLocalExplosionMode();
            }
            GUILayout.Label("仅用于观察，不改变拆解进度", captionStyle);

            if (app.MotionAvailable)
            {
                GUILayout.Space(Pixels(8f));
                GUILayout.Label("演示调速：" + app.MotionCadenceRpm + " 转/分", captionStyle);
                bool cadenceEnabled = GUI.enabled;
                GUI.enabled = cadenceEnabled && app.IsMotionActive;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("慢 −15", cadenceButtonStyle,
                    GUILayout.Height(Pixels(36f))))
                    app.ChangeMotionCadence(-15);
                if (GUILayout.Button("快 +15", cadenceButtonStyle,
                    GUILayout.Height(Pixels(36f))))
                    app.ChangeMotionCadence(15);
                GUILayout.EndHorizontal();
                GUI.enabled = cadenceEnabled;
            }

            GUILayout.Space(Pixels(8f));
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
                app.Plan.Mode == AssemblyMode.Disassemble ? "开始组装" : "重新拆解",
                actionStyle,
                GUILayout.Height(Pixels(44f))))
            {
                app.ToggleMode();
            }

            if (GUILayout.Button("重置进度", buttonStyle, GUILayout.Height(Pixels(40f))))
            {
                app.ResetCurrentPlan();
            }

            GUILayout.Space(Pixels(8f));
            bool narration = app.NarrationEnabled;
            if (GUILayout.Button(narration ? (app.NarrationAvailable ? "中文讲解 · 开" : "语音待就绪") : "中文讲解 · 关",
                narration ? selectedButtonStyle : buttonStyle, GUILayout.Height(Pixels(44f))))
            {
                app.SetNarrationEnabled(!narration);
            }
            GUILayout.Label(app.NarrationStatus, captionStyle);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = narration && app.NarrationAvailable;
            if (GUILayout.Button("再次讲解", buttonStyle, GUILayout.Height(Pixels(36)))) app.ReplayNarration();
            GUI.enabled = wasEnabled;

            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
        }

        private void DrawPartsTray()
        {
            MechMasterApp app = MechMasterApp.Instance;
            GUI.Box(CurrentLayout.ToPixels(WorkshopLayout.Tray), GUIContent.none, trayPanelStyle);
            GUI.Label(
                PixelRect(32, 902, 300, 34),
                "零件收纳站",
                headingStyle);
            GUI.Label(PixelRect(1000, 908, 850, 26),
                "绿色：可放入    红色：换个分类", captionStyle);

            int pageCount = Mathf.CeilToInt(app.Model.assemblies.Length / (float)TrayCellsPerPage);
            trayPage = Mathf.Clamp(trayPage, 0, pageCount - 1);
            if (pageCount > 1)
            {
                if (GUI.Button(PixelRect(740, 906, 52, 26), "‹", buttonStyle))
                    trayPage = (trayPage + pageCount - 1) % pageCount;
                GUI.Label(PixelRect(798, 907, 78, 26),
                    (trayPage + 1) + " / " + pageCount, captionStyle);
                if (GUI.Button(PixelRect(880, 906, 52, 26), "›", buttonStyle))
                    trayPage = (trayPage + 1) % pageCount;
            }

            int start = trayPage * TrayCellsPerPage;
            int end = Mathf.Min(start + TrayCellsPerPage, app.Model.assemblies.Length);
            for (int index = start; index < end; index++)
            {
                string assemblyId = app.Model.assemblies[index].id;
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

                Rect cell = CurrentLayout.ToPixels(TrayCellRect(index - start));
                string label = app.AssemblyDisplayName(assemblyId)
                    + "  " + removed + "/" + total
                    + "\n" + (string.IsNullOrEmpty(latestPartName) ? "空" : latestPartName);
                GUIStyle cellStyle = ResolveTrayCellStyle(app, assemblyId, removed > 0);
                if (hoveredAssemblyId == assemblyId && !string.IsNullOrEmpty(draggedPartId))
                {
                    label = app.AssemblyDisplayName(assemblyId) + "\n"
                        + (cellStyle == trayCellReadyStyle ? "松手放入"
                            : cellStyle == trayCellWrongStyle ? "换个分类" : "该零件已完成操作");
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
            bool ready = stateAllowsOperation;
            return ready ? trayCellReadyStyle : trayCellBlockedStyle;
        }

        private static Rect TrayCellRect(int index)
        {
            return WorkshopLayout.TrayCell(index);
        }

        private void DrawKnowledgePanel()
        {
            MechMasterApp app = MechMasterApp.Instance;
            PartDefinition part = app.SelectedPart ?? app.Plan.ExpectedPart;

            if (WorkshopLayout.KnowledgeCollapsed)
            {
                if (GUI.Button(CurrentLayout.ToPixels(WorkshopLayout.KnowledgeTab), "百科", buttonStyle))
                    WorkshopLayout.KnowledgeCollapsed = false;
                return;
            }
            GUILayout.BeginArea(CurrentLayout.ToPixels(WorkshopLayout.Knowledge), boxStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("零件小百科", headingStyle, GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("›", buttonStyle,
                GUILayout.Width(Pixels(36)), GUILayout.Height(Pixels(32))))
                WorkshopLayout.KnowledgeCollapsed = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(Pixels(12f));
            if (knowledgePartId != part?.Id) { knowledgePartId = part?.Id; knowledgeScroll = Vector2.zero; }
            knowledgeScroll = GUILayout.BeginScrollView(knowledgeScroll, GUIStyle.none, GUI.skin.verticalScrollbar);
            if (part == null)
            {
                GUILayout.Label("本轮已经完成。", bodyStyle);
            }
            else
            {
                GUILayout.Label(part.DisplayName, titleStyle);
                GUILayout.Space(Pixels(8f));
                GUILayout.Label(
                    "操作方式：直接拖动拆装",
                    hintStyle);
                GUILayout.Space(Pixels(18f));
                GUILayout.Label(part.GetKnowledge(app.Plan.Difficulty), bodyStyle);
            }
            GUILayout.EndScrollView();
            GUILayout.Label(
                app.IsMotionActive
                    ? "动态演示中：停止后可继续拆装"
                    : app.Plan.Mode == AssemblyMode.Disassemble
                    ? "自由拆解：可选择任意尚未拆下的零件"
                    : "自由组装：可选择任意托盘中的零件",
                hintStyle);
            GUILayout.EndArea();
        }

        private void DrawStatus()
        {
            GUI.Box(CurrentLayout.ToPixels(WorkshopLayout.Status), GUIContent.none, statusPanelStyle);
            GUI.Label(PixelRect(32, 1035, 120, 32), "工坊提示", headingStyle);
            Rect messageRect = PixelRect(158, 1035, 1724, 34);
            string message = MechMasterApp.Instance.StatusMessage;
            GUI.Label(messageRect, new GUIContent(FitTrayLine(message, statusStyle, messageRect.width), message), statusStyle);
        }

        private void DifficultyButton(string label, DifficultyLevel value)
        {
            bool selected = MechMasterApp.Instance.Plan.Difficulty == value;
            if (GUILayout.Button((selected ? "●  " : "○  ") + label,
                selected ? selectedButtonStyle : buttonStyle, GUILayout.Height(Pixels(44f))))
            {
                MechMasterApp.Instance.SetDifficulty(value);
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
            brandStyle = CreateLabelStyle(chineseFont, 30, FontStyle.Bold, WorkshopTheme.Ink);
            brandStyle.padding = new RectOffset();
            brandStyle.margin = new RectOffset();
            brandStyle.wordWrap = false;
            brandStyle.alignment = TextAnchor.MiddleLeft;
            sloganStyle = CreateLabelStyle(chineseFont, 16, FontStyle.Normal, WorkshopTheme.MutedInk);
            sloganStyle.padding = new RectOffset();
            sloganStyle.margin = new RectOffset();
            sloganStyle.wordWrap = false;
            sloganStyle.alignment = TextAnchor.MiddleLeft;
            headingStyle = CreateLabelStyle(chineseFont, 22, FontStyle.Bold, WorkshopTheme.Teal);
            bodyStyle = CreateLabelStyle(chineseFont, 20, FontStyle.Normal, WorkshopTheme.Ink);
            bodyStyle.richText = true;
            captionStyle = CreateLabelStyle(chineseFont, 18, FontStyle.Normal, WorkshopTheme.MutedInk);
            headerGuideStyle = new GUIStyle(captionStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                padding = new RectOffset()
            };
            statusStyle = CreateLabelStyle(chineseFont, 18, FontStyle.Normal, WorkshopTheme.Ink);
            statusStyle.wordWrap = false;
            statusStyle.alignment = TextAnchor.MiddleLeft;

            buttonStyle = CreateButton(WorkshopTheme.Sky, WorkshopTheme.Ink, WorkshopTheme.Border);
            selectedButtonStyle = CreateButton(WorkshopTheme.Teal, Color.white, WorkshopTheme.Teal);
            modelButtonStyle = new GUIStyle(buttonStyle)
            {
                fontSize = 18,
                wordWrap = false,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(5, 5, 4, 4)
            };
            selectedModelButtonStyle = new GUIStyle(selectedButtonStyle)
            {
                fontSize = 18,
                wordWrap = false,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(5, 5, 4, 4)
            };
            selectedToolStyle = CreateButton(WorkshopTheme.Orange, WorkshopTheme.OrangeInk,
                new Color32(223, 154, 67, 255));
            actionStyle = new GUIStyle(selectedToolStyle) { fontStyle = FontStyle.Bold };
            boxStyle = CreateSurface(WorkshopTheme.Surface, WorkshopTheme.Border, 20, WorkshopTheme.Ink);
            boxStyle.padding = new RectOffset(12, 12, 12, 12);
            panPanelStyle = CreateSurface(
                new Color(WorkshopTheme.Surface.r, WorkshopTheme.Surface.g, WorkshopTheme.Surface.b, 0.42f),
                new Color(WorkshopTheme.Border.r, WorkshopTheme.Border.g, WorkshopTheme.Border.b, 0.55f),
                20,
                WorkshopTheme.Ink);
            panButtonStyle = CreateButton(
                new Color(WorkshopTheme.Sky.r, WorkshopTheme.Sky.g, WorkshopTheme.Sky.b, 0.62f),
                WorkshopTheme.Ink,
                new Color(WorkshopTheme.Border.r, WorkshopTheme.Border.g, WorkshopTheme.Border.b, 0.72f));
            cadenceButtonStyle = new GUIStyle(buttonStyle)
            {
                fontSize = 17,
                wordWrap = false,
                padding = new RectOffset(2, 2, 2, 2)
            };
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
            hintStyle.padding = new RectOffset(8, 8, 8, 8);
            shadowStyle = CreateSurface(new Color(0.22f, 0.38f, 0.42f, 0.09f), Color.clear, 16, WorkshopTheme.Ink);
            progressTrackStyle = CreateSurface(WorkshopTheme.Sky, Color.clear, 16, WorkshopTheme.Ink);
            progressFillStyle = CreateSurface(WorkshopTheme.Teal, Color.clear, 16, WorkshopTheme.Ink);
            statusPanelStyle = CreateSurface(WorkshopTheme.Mint, WorkshopTheme.Border, 20, WorkshopTheme.Ink);
            gearTexture = MakeGearTexture();
            foreach (GUIStyle style in new[]
            {
                titleStyle, brandStyle, sloganStyle, headingStyle, bodyStyle, captionStyle, headerGuideStyle,
                statusStyle, buttonStyle, selectedButtonStyle, modelButtonStyle, selectedModelButtonStyle,
                cadenceButtonStyle,
                selectedToolStyle, actionStyle, boxStyle, panPanelStyle, panButtonStyle, trayPanelStyle,
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
