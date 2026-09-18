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

        public static ToolKind LoadTool()
        {
            int value = PlayerPrefs.GetInt(Prefix + "tool", (int)ToolKind.Hand);
            return value >= 0 && value <= (int)ToolKind.TorxKey
                ? (ToolKind)value
                : ToolKind.Hand;
        }

        public static bool LoadNarrationEnabled()
        {
            return PlayerPrefs.GetInt(Prefix + "narration", 1) == 1;
        }

        public static int LoadRemovedCount(DifficultyLevel difficulty)
        {
            return Mathf.Max(0, PlayerPrefs.GetInt(ProgressKey(difficulty, "removed"), 0));
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
            ToolKind tool,
            bool narrationEnabled)
        {
            PlayerPrefs.SetInt(Prefix + "difficulty", (int)plan.Difficulty);
            PlayerPrefs.SetInt(Prefix + "tool", (int)tool);
            PlayerPrefs.SetInt(Prefix + "narration", narrationEnabled ? 1 : 0);
            PlayerPrefs.SetInt(ProgressKey(plan.Difficulty, "removed"), plan.RemovedCount);
            PlayerPrefs.SetInt(ProgressKey(plan.Difficulty, "mode"), (int)plan.Mode);
            PlayerPrefs.Save();
        }

        public static void ClearProgress(DifficultyLevel difficulty)
        {
            PlayerPrefs.DeleteKey(ProgressKey(difficulty, "removed"));
            PlayerPrefs.DeleteKey(ProgressKey(difficulty, "mode"));
            PlayerPrefs.Save();
        }

        private static string ProgressKey(DifficultyLevel difficulty, string field)
        {
            return Prefix + "progress." + difficulty + "." + field;
        }
    }
}

