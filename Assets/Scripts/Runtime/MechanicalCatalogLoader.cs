using System;
using System.Collections.Generic;
using System.Linq;
using MechMaster.Domain;
using UnityEngine;

namespace MechMaster.Runtime
{
    public static class MechanicalCatalogLoader
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

        private static readonly Dictionary<string, CatalogAsset> Cache =
            new Dictionary<string, CatalogAsset>(StringComparer.Ordinal);

        public static DisassemblyPlan CreatePlan(
            MechanicalModelDefinition model,
            DifficultyLevel difficulty)
        {
            CatalogAsset asset = LoadAsset(model);
            PlanAsset selected = asset.plans.FirstOrDefault(
                plan => string.Equals(plan.difficulty, difficulty.ToString(), StringComparison.Ordinal));
            if (selected == null || selected.steps == null || selected.steps.Length == 0)
            {
                throw new InvalidOperationException(model.displayName + "缺少拆解等级：" + difficulty);
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

        private static CatalogAsset LoadAsset(MechanicalModelDefinition model)
        {
            CatalogAsset cached;
            if (Cache.TryGetValue(model.id, out cached))
            {
                return cached;
            }

            TextAsset text = Resources.Load<TextAsset>(model.catalogResourcePath);
            if (text == null)
            {
                throw new InvalidOperationException(
                    "未找到交互目录：" + model.catalogResourcePath);
            }

            CatalogAsset asset = JsonUtility.FromJson<CatalogAsset>(text.text);
            if (asset == null || asset.schemaVersion != 1 || asset.plans == null
                || !string.Equals(asset.moduleId, model.id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(model.displayName + "交互目录格式或模型 ID 无效。");
            }

            var assemblyIds = new HashSet<string>(
                model.assemblies.Select(assembly => assembly.id), StringComparer.Ordinal);
            foreach (PlanAsset plan in asset.plans)
            {
                if (plan == null || plan.steps == null || plan.steps.Length == 0)
                {
                    throw new InvalidOperationException(model.displayName + "交互目录包含空拆解等级。");
                }
                var stepIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (StepAsset step in plan.steps)
                {
                    if (step == null || !stepIds.Add(step.id)
                        || !assemblyIds.Contains(step.assemblyId)
                        || step.objectNames == null || step.objectNames.Length == 0)
                    {
                        throw new InvalidOperationException(model.displayName + "交互目录零件绑定无效：" + step?.id);
                    }
                }
            }

            Cache.Add(model.id, asset);
            return asset;
        }

        private static ToolKind ParseTool(string value)
        {
            ToolKind parsed;
            return Enum.TryParse(value, false, out parsed) ? parsed : ToolKind.Hand;
        }
    }
}
