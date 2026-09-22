using System.Collections.Generic;
using MechMaster.Domain;
using UnityEngine;

namespace MechMaster.Runtime
{
    public static class LocalProgressStore
    {
        private const string Prefix = "mech_master.v1.";
        private const string BicycleModelId = "bike.hardtail.27_5.2x10.v1";

        public static string LoadModelId()
        {
            return PlayerPrefs.GetString(Prefix + "modelId", BicycleModelId);
        }

        public static DifficultyLevel LoadDifficulty()
        {
            int value = PlayerPrefs.GetInt(Prefix + "difficulty", (int)DifficultyLevel.Simple);
            return value >= 0 && value <= (int)DifficultyLevel.Advanced
                ? (DifficultyLevel)value
                : DifficultyLevel.Simple;
        }

        public static bool LoadNarrationEnabled()
        {
            return PlayerPrefs.GetInt(Prefix + "narration", 1) == 1;
        }

        public static int LoadRemovedCount(string modelId, DifficultyLevel difficulty)
        {
            return Mathf.Max(0, PlayerPrefs.GetInt(
                ProgressKey(modelId, difficulty, "removed"),
                LegacyInt(modelId, difficulty, "removed", 0)));
        }

        public static string[] LoadRemovedPartIds(string modelId, DifficultyLevel difficulty)
        {
            string value = PlayerPrefs.GetString(
                ProgressKey(modelId, difficulty, "removedIds"),
                modelId == BicycleModelId
                    ? PlayerPrefs.GetString(LegacyProgressKey(difficulty, "removedIds"), string.Empty)
                    : string.Empty);
            return string.IsNullOrEmpty(value)
                ? new string[0]
                : value.Split('|');
        }

        public static AssemblyMode LoadMode(string modelId, DifficultyLevel difficulty)
        {
            int value = PlayerPrefs.GetInt(
                ProgressKey(modelId, difficulty, "mode"),
                LegacyInt(modelId, difficulty, "mode", (int)AssemblyMode.Disassemble));
            return value == (int)AssemblyMode.Assemble
                ? AssemblyMode.Assemble
                : AssemblyMode.Disassemble;
        }

        public static void Save(
            string modelId,
            DisassemblyPlan plan,
            bool narrationEnabled)
        {
            PlayerPrefs.SetString(Prefix + "modelId", modelId);
            PlayerPrefs.SetInt(Prefix + "difficulty", (int)plan.Difficulty);
            PlayerPrefs.SetInt(Prefix + "narration", narrationEnabled ? 1 : 0);
            PlayerPrefs.SetInt(ProgressKey(modelId, plan.Difficulty, "removed"), plan.RemovedCount);
            var removedIds = new List<string>();
            foreach (PartDefinition part in plan.Steps)
            {
                if (plan.IsRemoved(part.Id))
                {
                    removedIds.Add(part.Id);
                }
            }
            PlayerPrefs.SetString(
                ProgressKey(modelId, plan.Difficulty, "removedIds"),
                string.Join("|", removedIds));
            PlayerPrefs.SetInt(ProgressKey(modelId, plan.Difficulty, "mode"), (int)plan.Mode);
            PlayerPrefs.Save();
        }

        public static void ClearProgress(string modelId, DifficultyLevel difficulty)
        {
            foreach (string field in new[] { "removed", "removedIds", "mode" })
            {
                PlayerPrefs.DeleteKey(ProgressKey(modelId, difficulty, field));
                if (modelId == BicycleModelId)
                    PlayerPrefs.DeleteKey(LegacyProgressKey(difficulty, field));
            }
            PlayerPrefs.Save();
        }

        private static int LegacyInt(string modelId, DifficultyLevel difficulty, string field, int fallback)
        {
            return modelId == BicycleModelId
                ? PlayerPrefs.GetInt(LegacyProgressKey(difficulty, field), fallback)
                : fallback;
        }

        private static string LegacyProgressKey(DifficultyLevel difficulty, string field)
        {
            return Prefix + "progress." + difficulty + "." + field;
        }

        private static string ProgressKey(string modelId, DifficultyLevel difficulty, string field)
        {
            return Prefix + "progress." + modelId + "." + difficulty + "." + field;
        }
    }
}
