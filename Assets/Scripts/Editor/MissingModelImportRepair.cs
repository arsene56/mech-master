using System.IO;
using MechMaster.Runtime;
using UnityEditor;
using UnityEngine;

namespace MechMaster.Editor
{
    public static class MissingModelImportRepair
    {
        [MenuItem("机械大师/修复缺失的模型导入")]
        public static void Repair()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("请先退出 Play，再修复模型导入。");
                return;
            }

            int repaired = 0;
            int failed = 0;
            foreach (MechanicalModelDefinition model in MechanicalModelRegistry.Models)
            {
                foreach (string resourcePath in model.moduleResourcePaths)
                {
                    if (Resources.Load<GameObject>(resourcePath) != null) continue;
                    string assetPath = "Assets/Resources/" + resourcePath + ".fbx";
                    string sourceFile = Path.Combine(Application.dataPath,
                        "Resources", resourcePath + ".fbx");
                    if (!File.Exists(sourceFile))
                    {
                        Debug.LogError("模型源文件不存在：" + assetPath);
                        failed++;
                        continue;
                    }

                    Debug.LogWarning("重新导入缺失模型模块：" + assetPath);
                    AssetDatabase.ImportAsset(assetPath,
                        ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    if (Resources.Load<GameObject>(resourcePath) != null)
                        repaired++;
                    else
                    {
                        Debug.LogError("重新导入后仍无法加载模型模块：" + assetPath);
                        failed++;
                    }
                }
            }

            Debug.Log("MECH_MASTER_MISSING_MODEL_IMPORT_REPAIR repaired=" + repaired
                + " failed=" + failed);
        }
    }
}
