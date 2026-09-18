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
    public static class PrototypeValidator
    {
        [MenuItem("机械大师/运行工程自行车")]
        public static void OpenAndPlay()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            PrepareHighResolutionGameView();
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorApplication.isPlaying = true;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode)
            {
                return;
            }

            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.delayCall += PrepareHighResolutionGameView;
        }

        private static void PrepareHighResolutionGameView()
        {
            System.Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null)
            {
                return;
            }

            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            var serializedView = new SerializedObject(gameView);
            SerializedProperty lowResolution =
                serializedView.FindProperty("m_LowResolutionForAspectRatios")
                ?? serializedView.FindProperty("m_lowResolutionForAspectRatios");
            if (lowResolution != null)
            {
                lowResolution.boolValue = false;
                serializedView.ApplyModifiedPropertiesWithoutUndo();
            }

            PropertyInfo maximizedProperty = typeof(EditorWindow).GetProperty(
                "maximized",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (maximizedProperty != null && maximizedProperty.CanWrite)
            {
                maximizedProperty.SetValue(gameView, true, null);
            }

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
                    Vector2 nativeScale = Vector2.one
                        / Mathf.Max(1f, EditorGUIUtility.pixelsPerPoint);
                    scaleProperty.SetValue(zoomArea, nativeScale, null);
                }
                else
                {
                    FieldInfo scaleField = zoomArea.GetType().GetField(
                        "m_Scale",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    if (scaleField != null)
                    {
                        Vector2 nativeScale = Vector2.one
                            / Mathf.Max(1f, EditorGUIUtility.pixelsPerPoint);
                        scaleField.SetValue(zoomArea, nativeScale);
                    }
                }
            }

            gameView.Focus();
            gameView.Repaint();
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
            DisassemblyPlan simple = EngineeringBicycleCatalogLoader.CreatePlan(
                DifficultyLevel.Simple);
            DisassemblyPlan standard = EngineeringBicycleCatalogLoader.CreatePlan(
                DifficultyLevel.Standard);
            DisassemblyPlan advanced = EngineeringBicycleCatalogLoader.CreatePlan(
                DifficultyLevel.Advanced);
            if (simple.Steps.Count != 14
                || standard.Steps.Count != 195
                || advanced.Steps.Count != 595)
            {
                return "三档难度步骤数不符合 14/195/595 约定。";
            }

            var importedNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (string resourcePath in EngineeringBicycleCatalogLoader.ModuleResourcePaths)
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
                return "进阶模式模型绑定数量不是 595。";
            }

            return string.Empty;
        }
    }
}
