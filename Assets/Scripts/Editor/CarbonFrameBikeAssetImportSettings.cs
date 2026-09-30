using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MechMaster.Editor
{
    public sealed class CarbonFrameBikeAssetImportSettings : AssetPostprocessor
    {
        public override uint GetVersion() => 2;
        private bool IsCarbon => assetPath.StartsWith("Assets/Resources/Models/CarbonFrameBike/", StringComparison.Ordinal);

        private void OnPreprocessModel()
        {
            if (!IsCarbon) return;
            var importer = (ModelImporter)assetImporter;
            importer.preserveHierarchy = true;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.globalScale = 1;
            importer.useFileScale = true;
            // Only the chain module needs CPU mesh access for the rear-end
            // suspension-following deformation; keep other modules GPU-only.
            if (assetPath.EndsWith("/chain_guide_LOD0.fbx", StringComparison.Ordinal))
                importer.isReadable = true;
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!IsCarbon) return;
            const string table = "Assets/StreamingAssets/MechanicalCatalog/carbon_frame_bike_engineering.json";
            if (!File.Exists(table)) throw new InvalidOperationException("Missing generated CarbonBike material table");
            var data = JsonUtility.FromJson<MaterialTable>(File.ReadAllText(table));
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    var entry = Array.Find(data.materials, row => row.name == material.name);
                    if (entry == null) continue;
                    material.SetFloat("_Metallic", entry.metallic);
                    material.SetFloat("_Glossiness", 1 - entry.roughness);
                }
        }

        [Serializable] private sealed class MaterialTable { public MaterialEntry[] materials; }
        [Serializable] private sealed class MaterialEntry { public string name; public float metallic, roughness; }
    }
}
