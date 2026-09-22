using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MechMaster.Runtime
{
    [Serializable]
    public sealed class MechanicalAssemblyDefinition
    {
        public string id;
        public string displayName;
    }

    [Serializable]
    public sealed class MechanicalModelDefinition
    {
        public int schemaVersion;
        public string id;
        public string displayName;
        public int displayOrder;
        public string catalogResourcePath;
        public string[] moduleResourcePaths;
        public MechanicalAssemblyDefinition[] assemblies;

        public string AssemblyDisplayName(string assemblyId)
        {
            MechanicalAssemblyDefinition assembly = assemblies.FirstOrDefault(
                item => string.Equals(item.id, assemblyId, StringComparison.Ordinal));
            return assembly == null ? assemblyId : assembly.displayName;
        }
    }

    // Every JSON file under Resources/MechanicalCatalog/Models is discovered at startup.
    // Adding a model does not require registering it in C# or modifying a central list.
    public static class MechanicalModelRegistry
    {
        private static MechanicalModelDefinition[] cachedModels;

        public static IReadOnlyList<MechanicalModelDefinition> Models
        {
            get
            {
                if (cachedModels == null)
                {
                    cachedModels = LoadModels();
                }
                return cachedModels;
            }
        }

        public static MechanicalModelDefinition Find(string modelId)
        {
            return Models.FirstOrDefault(model =>
                string.Equals(model.id, modelId, StringComparison.Ordinal));
        }

        public static MechanicalModelDefinition FindOrDefault(string modelId)
        {
            return Find(modelId) ?? Models[0];
        }

        private static MechanicalModelDefinition[] LoadModels()
        {
            TextAsset[] assets = Resources.LoadAll<TextAsset>("MechanicalCatalog/Models");
            if (assets.Length == 0)
            {
                throw new InvalidOperationException("没有可用机械模型清单。请检查 Resources/MechanicalCatalog/Models。");
            }

            var models = new List<MechanicalModelDefinition>(assets.Length);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (TextAsset asset in assets)
            {
                MechanicalModelDefinition model = JsonUtility.FromJson<MechanicalModelDefinition>(asset.text);
                Validate(model, asset.name);
                if (!ids.Add(model.id))
                {
                    throw new InvalidOperationException("机械模型 ID 重复：" + model.id);
                }
                models.Add(model);
            }

            return models.OrderBy(model => model.displayOrder)
                .ThenBy(model => model.id, StringComparer.Ordinal).ToArray();
        }

        private static void Validate(MechanicalModelDefinition model, string assetName)
        {
            if (model == null || model.schemaVersion != 1
                || string.IsNullOrWhiteSpace(model.id)
                || string.IsNullOrWhiteSpace(model.displayName)
                || string.IsNullOrWhiteSpace(model.catalogResourcePath)
                || model.moduleResourcePaths == null || model.moduleResourcePaths.Length == 0
                || model.assemblies == null || model.assemblies.Length == 0)
            {
                throw new InvalidOperationException("机械模型清单格式无效：" + assetName);
            }

            if (model.moduleResourcePaths.Any(string.IsNullOrWhiteSpace)
                || model.moduleResourcePaths.Distinct(StringComparer.Ordinal).Count()
                    != model.moduleResourcePaths.Length
                || model.assemblies.Any(assembly => assembly == null
                    || string.IsNullOrWhiteSpace(assembly.id)
                    || string.IsNullOrWhiteSpace(assembly.displayName))
                || model.assemblies.Select(assembly => assembly.id)
                    .Distinct(StringComparer.Ordinal).Count() != model.assemblies.Length)
            {
                throw new InvalidOperationException("机械模型清单包含空值或重复项：" + assetName);
            }
        }
    }
}
