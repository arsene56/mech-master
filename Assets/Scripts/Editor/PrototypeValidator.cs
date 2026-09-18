using System;
using System.Collections.Generic;
using System.Linq;
using MechMaster.Domain;
using UnityEditor;
using UnityEngine;

namespace MechMaster.Editor
{
    public static class PrototypeValidator
    {
        private static readonly string[] RequiredObjectNames =
        {
            "Frame",
            "Front_Tire",
            "Front_Rim",
            "Front_Hub",
            "Front_Spokes",
            "Front_EndCaps",
            "Front_ThruAxle",
            "Front_Rotor",
            "RotorBolts",
            "Front_Caliper",
            "CaliperMountBolts",
            "LeftPad",
            "RightPad",
            "PadSpring",
            "PadPin",
            "RetainingClip",
            "Rear_Tire",
            "Cassette_10Speed",
            "Chainring_Large"
        };

        [MenuItem("机械大师/验证首版样片")]
        public static void ValidateFromMenu()
        {
            string error = Validate();
            if (string.IsNullOrEmpty(error))
            {
                Debug.Log("MECH_MASTER_PROTOTYPE_VALIDATION_OK");
                EditorUtility.DisplayDialog("机械大师", "首版样片结构验证通过。", "确定");
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
                Debug.Log("MECH_MASTER_PROTOTYPE_VALIDATION_OK");
                EditorApplication.Exit(0);
            }

            Debug.LogError(error);
            EditorApplication.Exit(1);
        }

        private static string Validate()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/BicyclePrototype.fbx");
            if (prefab == null)
            {
                return "缺少 Assets/Resources/BicyclePrototype.fbx。";
            }

            HashSet<string> objectNames = new HashSet<string>(
                prefab.GetComponentsInChildren<Transform>(true).Select(item => item.name),
                StringComparer.Ordinal);
            string[] missing = RequiredObjectNames
                .Where(name => !objectNames.Contains(name))
                .ToArray();
            if (missing.Length > 0)
            {
                return "模型缺少稳定对象名：" + string.Join(", ", missing);
            }

            if (FrontBrakeCatalog.CreatePlan(DifficultyLevel.Simple).Steps.Count != 4
                || FrontBrakeCatalog.CreatePlan(DifficultyLevel.Standard).Steps.Count != 8
                || FrontBrakeCatalog.CreatePlan(DifficultyLevel.Advanced).Steps.Count != 12)
            {
                return "三档难度步骤数不符合 4/8/12 约定。";
            }

            return string.Empty;
        }
    }
}
