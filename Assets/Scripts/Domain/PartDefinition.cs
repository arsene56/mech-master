using System;

namespace MechMaster.Domain
{
    [Serializable]
    public sealed class PartDefinition
    {
        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public ToolKind RequiredTool { get; private set; }
        public string SimpleSummary { get; private set; }
        public string Mechanism { get; private set; }
        public string AdvancedNote { get; private set; }

        public PartDefinition(
            string id,
            string displayName,
            ToolKind requiredTool,
            string simpleSummary,
            string mechanism,
            string advancedNote)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Part id is required.", nameof(id));
            }

            Id = id;
            DisplayName = displayName ?? id;
            RequiredTool = requiredTool;
            SimpleSummary = simpleSummary ?? string.Empty;
            Mechanism = mechanism ?? string.Empty;
            AdvancedNote = advancedNote ?? string.Empty;
        }

        public string GetKnowledge(DifficultyLevel difficulty)
        {
            if (difficulty == DifficultyLevel.Simple || string.IsNullOrWhiteSpace(Mechanism))
            {
                return SimpleSummary;
            }

            if (difficulty == DifficultyLevel.Standard || string.IsNullOrWhiteSpace(AdvancedNote))
            {
                return SimpleSummary + "\n\n原理：" + Mechanism;
            }

            return SimpleSummary + "\n\n原理：" + Mechanism + "\n\n进阶：" + AdvancedNote;
        }
    }
}

