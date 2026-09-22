using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MechMaster.Domain;
using MechMaster.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MechMaster.Editor
{
    [InitializeOnLoad]
    public static class PrototypeValidator
    {
        private const BindingFlags InstanceMembers =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string RuntimeSmokeKey = "MechMaster.RuntimeSmoke";
        private const string RuntimeSmokeExitCodeKey = "MechMaster.RuntimeSmokeExitCode";

        static PrototypeValidator()
        {
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        [MenuItem("机械大师/运行工程自行车")]
        public static void OpenAndPlay()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            PrepareHighResolutionGameView();
            EditorApplication.isPlaying = true;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (SessionState.GetBool(RuntimeSmokeKey, false))
            {
                if (state == PlayModeStateChange.EnteredPlayMode)
                    EditorApplication.delayCall += ValidateRuntimeBootstrap;
                else if (state == PlayModeStateChange.EnteredEditMode)
                {
                    int code = SessionState.GetInt(RuntimeSmokeExitCodeKey, 1);
                    SessionState.EraseBool(RuntimeSmokeKey);
                    SessionState.EraseInt(RuntimeSmokeExitCodeKey);
                    EditorApplication.Exit(code);
                }
                return;
            }

            if (state != PlayModeStateChange.EnteredPlayMode)
            {
                return;
            }

            // Run after GameView has applied its own play-mode layout and zoom.
            if (!Application.isBatchMode)
                EditorApplication.delayCall += RestoreNativePreview;
        }

        // Batch-mode smoke test of the same startup path used by the local game.
        // It does not operate any part or reset the user's saved progress.
        public static void ValidateRuntimeBootstrapFromCommandLine()
        {
            SessionState.SetBool(RuntimeSmokeKey, true);
            SessionState.SetInt(RuntimeSmokeExitCodeKey, 1);
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void ValidateRuntimeBootstrap()
        {
            try
            {
                MechMasterApp app = MechMasterApp.Instance;
                if (app == null || app.Model == null || app.Plan == null)
                    throw new InvalidOperationException("运行时未创建模型与拆装计划。");
                MechanicalModelView view = UnityEngine.Object.FindObjectOfType<MechanicalModelView>();
                if (view == null || view.Parts.Count != app.Plan.Steps.Count)
                    throw new InvalidOperationException("运行时零件绑定数量与拆装计划不一致。");
                if (GameObject.Find("MechanicalModel_" + app.Model.id) == null)
                    throw new InvalidOperationException("运行时模型根节点未创建。");
                Debug.Log("MECH_MASTER_RUNTIME_MODEL_SMOKE_OK model=" + app.Model.id
                    + " parts=" + view.Parts.Count);
                SessionState.SetInt(RuntimeSmokeExitCodeKey, 0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                EditorApplication.isPlaying = false;
            }
        }

        [MenuItem("机械大师/修复 Game 预览清晰度")]
        private static void PrepareHighResolutionGameView()
        {
            System.Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null)
            {
                return;
            }

            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            PropertyInfo maximizedProperty = typeof(EditorWindow).GetProperty(
                "maximized",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (maximizedProperty != null && maximizedProperty.CanWrite)
            {
                maximizedProperty.SetValue(gameView, true, null);
            }

            EditorApplication.delayCall += RestoreNativePreview;
        }

        private static void RestoreNativePreview()
        {
            Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null) return;
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);

            // In 2022 LTS the serialized field is bool[] (one entry per build-target group),
            // not a bool. Use the property so the engine also updates its zoom constraints.
            PropertyInfo lowResolution = gameViewType.GetProperty(
                "lowResolutionForAspectRatios", InstanceMembers);
            if (lowResolution == null || !lowResolution.CanWrite)
            {
                Debug.LogWarning("无法自动关闭低分辨率预览；请在 Game 菜单取消 Low Resolution Aspect Ratios。");
                return;
            }
            lowResolution.SetValue(gameView, false, null);

            MethodInfo snapZoom = gameViewType.GetMethod("SnapZoom", InstanceMembers,
                null, new[] { typeof(float) }, null);
            // GameView zoom already accounts for display DPI: native pixels mean 1x,
            // not 1 / pixelsPerPoint (which would downsample the finished frame).
            if (snapZoom != null) snapZoom.Invoke(gameView, new object[] { 1f });

            FieldInfo zoomAreaField = gameViewType.GetField(
                "m_ZoomArea",
                BindingFlags.Instance | BindingFlags.NonPublic);
            object zoomArea = zoomAreaField == null ? null : zoomAreaField.GetValue(gameView);
            if (zoomArea != null)
            {
                PropertyInfo scaleProperty = zoomArea.GetType().GetProperty(
                    "scale",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (scaleProperty != null && scaleProperty.CanWrite)
                {
                    if (snapZoom == null) scaleProperty.SetValue(zoomArea, Vector2.one, null);
                }
                else
                {
                    FieldInfo scaleField = zoomArea.GetType().GetField(
                        "m_Scale",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    if (scaleField != null)
                    {
                        if (snapZoom == null) scaleField.SetValue(zoomArea, Vector2.one);
                    }
                }
            }

            gameView.Focus();
            gameView.Repaint();
            object renderSize = gameViewType.GetProperty("targetRenderSize", InstanceMembers)
                ?.GetValue(gameView, null);
            object zoom = zoomArea?.GetType().GetProperty("scale", InstanceMembers)
                ?.GetValue(zoomArea, null);
            Debug.Log("MECH_MASTER_GAME_VIEW lowResolution=" + lowResolution.GetValue(gameView, null)
                + ", zoom=" + zoom + ", renderSize=" + renderSize
                + ", displayPixelsPerPoint=" + EditorGUIUtility.pixelsPerPoint);
        }

        [MenuItem("机械大师/验证工程自行车")]
        public static void ValidateFromMenu()
        {
            string error = Validate();
            if (string.IsNullOrEmpty(error))
            {
                Debug.Log("MECH_MASTER_ENGINEERING_BICYCLE_VALIDATION_OK");
                EditorUtility.DisplayDialog("机械大师", "工程自行车目录与模型绑定验证通过。", "确定");
            }
            else
            {
                Debug.LogError(error);
                EditorUtility.DisplayDialog("机械大师", error, "确定");
            }
        }

        public static void ValidateFromCommandLine()
        {
            string error = Validate();
            if (string.IsNullOrEmpty(error))
            {
                Debug.Log("MECH_MASTER_ENGINEERING_BICYCLE_VALIDATION_OK");
                EditorApplication.Exit(0);
                return;
            }

            Debug.LogError(error);
            EditorApplication.Exit(1);
        }

        private static string Validate()
        {
            string modelError = ValidateAllModels();
            if (!string.IsNullOrEmpty(modelError)) return modelError;

            MechanicalModelDefinition bicycle = MechanicalModelRegistry.Find("bike.hardtail.27_5.2x10.v1");
            if (bicycle == null) return "缺少工程自行车模型清单。";
            DisassemblyPlan simple = MechanicalCatalogLoader.CreatePlan(bicycle,
                DifficultyLevel.Simple);
            DisassemblyPlan standard = MechanicalCatalogLoader.CreatePlan(bicycle,
                DifficultyLevel.Standard);
            DisassemblyPlan advanced = MechanicalCatalogLoader.CreatePlan(bicycle,
                DifficultyLevel.Advanced);
            if (simple.Steps.Count != 15
                || standard.Steps.Count != 30
                || advanced.Steps.Count != 45)
            {
                return "三档拆解等级步骤数不符合 15/30/45 约定。";
            }

            var importedNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (string resourcePath in bicycle.moduleResourcePaths)
            {
                GameObject prefab = Resources.Load<GameObject>(resourcePath);
                if (prefab == null)
                {
                    return "缺少工程自行车模块：" + resourcePath;
                }

                foreach (Transform transform in prefab.GetComponentsInChildren<Transform>(true))
                {
                    importedNames.Add(transform.name);
                }
            }

            if (Resources.Load<GameObject>("Models/Bicycle/BicycleEngineering_LOD1") == null
                || Resources.Load<GameObject>("Models/Bicycle/BicycleEngineering_LOD2") == null)
            {
                return "缺少整车 LOD1 或 LOD2。";
            }

            string[] missing = advanced.Steps
                .SelectMany(step => step.ModelObjectNames)
                .Where(name => !importedNames.Contains(name))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (missing.Length > 0)
            {
                return "导入后的 FBX 缺少绑定对象：" + string.Join(", ", missing.Take(12).ToArray());
            }

            int advancedBindings = advanced.Steps.Sum(step => step.ModelObjectNames.Count);
            if (advancedBindings != 595)
            {
                return "探索等级模型绑定数量不是 595。";
            }

            return string.Empty;
        }

        private static string ValidateAllModels()
        {
            foreach (MechanicalModelDefinition model in MechanicalModelRegistry.Models)
            {
                var importedNames = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (string resourcePath in model.moduleResourcePaths)
                {
                    GameObject prefab = Resources.Load<GameObject>(resourcePath);
                    if (prefab == null)
                        return model.displayName + "缺少模型模块：" + resourcePath;
                    foreach (Transform item in prefab.GetComponentsInChildren<Transform>(true))
                    {
                        int count;
                        importedNames.TryGetValue(item.name, out count);
                        importedNames[item.name] = count + 1;
                    }
                }

                foreach (DifficultyLevel difficulty in new[]
                    { DifficultyLevel.Simple, DifficultyLevel.Standard, DifficultyLevel.Advanced })
                {
                    DisassemblyPlan plan = MechanicalCatalogLoader.CreatePlan(model, difficulty);
                    var boundNames = new HashSet<string>(StringComparer.Ordinal);
                    foreach (PartDefinition part in plan.Steps)
                    {
                        foreach (string objectName in part.ModelObjectNames)
                        {
                            int count;
                            if (!importedNames.TryGetValue(objectName, out count))
                                return model.displayName + "缺少绑定对象：" + objectName;
                            if (count != 1)
                                return model.displayName + "绑定对象名不唯一：" + objectName;
                            if (!boundNames.Add(objectName))
                                return model.displayName + "同等级重复绑定模型对象：" + objectName;
                        }
                    }
                }
            }

            return string.Empty;
        }
    }
}
