using UnityEditor;

namespace MechMaster.Editor
{
    // A single-part FBX otherwise becomes its filename-named prefab root,
    // losing the stable CAD object name (notably the sun gear module).
    public sealed class OpenTorqueAssetImportSettings : AssetPostprocessor
    {
        public override uint GetVersion() => 2;

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/Resources/Models/OpenTorque/", System.StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.preserveHierarchy = true;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.globalScale = 1;
            importer.useFileScale = true;
        }

        private void OnPostprocessModel(UnityEngine.GameObject root)
        {
            if (!assetPath.StartsWith("Assets/Resources/Models/OpenTorque/", System.StringComparison.Ordinal)) return;
            // FBX carries diffuse/roughness but Unity's default importer loses
            // Blender's metallic factor. Only actual bearings and pins are metal.
            foreach (UnityEngine.Renderer renderer in root.GetComponentsInChildren<UnityEngine.Renderer>(true))
                foreach (UnityEngine.Material material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    if (material.name == "OpenTorque steel bearings and pins") material.SetFloat("_Metallic", .85f);
                    else if (material.name.StartsWith("OpenTorque printed ", System.StringComparison.Ordinal)) material.SetFloat("_Metallic", 0);
                }
        }
    }
}
