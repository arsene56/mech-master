using System;
using System.Collections.Generic;
using System.Linq;
using MechMaster.Domain;
using UnityEngine;

namespace MechMaster.Runtime
{
    public static class EngineeringBicycleCatalogLoader
    {
        [Serializable]
        private sealed class CatalogAsset
        {
            public int schemaVersion;
            public string moduleId;
            public PlanAsset[] plans;
        }

        [Serializable]
        private sealed class PlanAsset
        {
            public string difficulty;
            public StepAsset[] steps;
        }

        [Serializable]
        private sealed class StepAsset
        {
            public string id;
            public string displayName;
            public string tool;
            public string simpleSummary;
            public string mechanism;
            public string advancedNote;
            public string assemblyId;
            public string componentId;
            public string[] objectNames;
        }

        public static readonly string[] ModuleResourcePaths =
        {
            "Models/Bicycle/Modules/frame_LOD0",
            "Models/Bicycle/Modules/cockpit_headset_LOD0",
            "Models/Bicycle/Modules/fork_LOD0",
            "Models/Bicycle/Modules/wheel_front_LOD0",
            "Models/Bicycle/Modules/wheel_rear_LOD0",
            "Models/Bicycle/Modules/brake_front_LOD0",
            "Models/Bicycle/Modules/brake_rear_LOD0",
            "Models/Bicycle/Modules/crank_bottom_bracket_LOD0",
            "Models/Bicycle/Modules/front_derailleur_LOD0",
            "Models/Bicycle/Modules/rear_derailleur_LOD0",
            "Models/Bicycle/Modules/chain_LOD0",
            "Models/Bicycle/Modules/pedals_LOD0",
            "Models/Bicycle/Modules/saddle_seatpost_LOD0",
            "Models/Bicycle/Modules/controls_cables_LOD0"
        };

        private static CatalogAsset cachedAsset;

        public static DisassemblyPlan CreatePlan(DifficultyLevel difficulty)
        {
            CatalogAsset asset = LoadAsset();
            PlanAsset selected = asset.plans.FirstOrDefault(
                plan => string.Equals(plan.difficulty, difficulty.ToString(), StringComparison.Ordinal));
            if (selected == null || selected.steps == null || selected.steps.Length == 0)
            {
                throw new InvalidOperationException("工程自行车目录缺少拆解等级：" + difficulty);
            }

            IEnumerable<PartDefinition> steps = selected.steps.Select(step =>
                new PartDefinition(
                    step.id,
                    step.displayName,
                    ParseTool(step.tool),
                    step.simpleSummary,
                    step.mechanism,
                    step.advancedNote,
                    step.assemblyId,
                    step.componentId,
                    step.objectNames));
            return new DisassemblyPlan(difficulty, steps);
        }

        private static CatalogAsset LoadAsset()
        {
            if (cachedAsset != null)
            {
                return cachedAsset;
            }

            TextAsset text = Resources.Load<TextAsset>(
                "MechanicalCatalog/BicycleInteractionCatalog");
            if (text == null)
            {
                throw new InvalidOperationException(
                    "未找到工程自行车交互目录 Resources/MechanicalCatalog/BicycleInteractionCatalog.json。");
            }

            cachedAsset = JsonUtility.FromJson<CatalogAsset>(text.text);
            if (cachedAsset == null || cachedAsset.schemaVersion != 1 || cachedAsset.plans == null)
            {
                throw new InvalidOperationException("工程自行车交互目录格式无效。");
            }

            return cachedAsset;
        }

        private static ToolKind ParseTool(string value)
        {
            ToolKind parsed;
            return Enum.TryParse(value, false, out parsed) ? parsed : ToolKind.Hand;
        }
    }
}
