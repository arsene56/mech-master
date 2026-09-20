using System.Collections.Generic;
using MechMaster.Domain;
using UnityEngine;

namespace MechMaster.Runtime
{
    public static class LocalProgressStore
    {
        private const string Prefix = "mech_master.v1.";

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

        public static int LoadRemovedCount(DifficultyLevel difficulty)
        {
            return Mathf.Max(0, PlayerPrefs.GetInt(ProgressKey(difficulty, "removed"), 0));
        }

        public static string[] LoadRemovedPartIds(DifficultyLevel difficulty)
        {
            string value = PlayerPrefs.GetString(
                ProgressKey(difficulty, "removedIds"),
                string.Empty);
            return string.IsNullOrEmpty(value)
                ? new string[0]
                : value.Split('|');
        }

        public static AssemblyMode LoadMode(DifficultyLevel difficulty)
        {
            int value = PlayerPrefs.GetInt(
                ProgressKey(difficulty, "mode"),
                (int)AssemblyMode.Disassemble);
            return value == (int)AssemblyMode.Assemble
                ? AssemblyMode.Assemble
                : AssemblyMode.Disassemble;
        }

        public static void Save(
            DisassemblyPlan plan,
            bool narrationEnabled)
        {
            PlayerPrefs.SetInt(Prefix + "difficulty", (int)plan.Difficulty);
            PlayerPrefs.SetInt(Prefix + "narration", narrationEnabled ? 1 : 0);
            PlayerPrefs.SetInt(ProgressKey(plan.Difficulty, "removed"), plan.RemovedCount);
            var removedIds = new List<string>();
            foreach (PartDefinition part in plan.Steps)
            {
                if (plan.IsRemoved(part.Id))
                {
                    removedIds.Add(part.Id);
                }
            }
            PlayerPrefs.SetString(
                ProgressKey(plan.Difficulty, "removedIds"),
                string.Join("|", removedIds));
            PlayerPrefs.SetInt(ProgressKey(plan.Difficulty, "mode"), (int)plan.Mode);
            PlayerPrefs.Save();
        }

        public static void ClearProgress(DifficultyLevel difficulty)
        {
            PlayerPrefs.DeleteKey(ProgressKey(difficulty, "removed"));
            PlayerPrefs.DeleteKey(ProgressKey(difficulty, "removedIds"));
            PlayerPrefs.DeleteKey(ProgressKey(difficulty, "mode"));
            PlayerPrefs.Save();
        }

        private static string ProgressKey(DifficultyLevel difficulty, string field)
        {
            return Prefix + "progress." + difficulty + "." + field;
        }
    }
}
