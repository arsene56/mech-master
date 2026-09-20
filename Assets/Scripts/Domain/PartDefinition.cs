using System;
using System.Collections.Generic;
using System.Linq;

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
        public string AssemblyId { get; private set; }
        public string ComponentId { get; private set; }
        public IReadOnlyList<string> ModelObjectNames { get; private set; }

        public PartDefinition(
            string id,
            string displayName,
            ToolKind requiredTool,
            string simpleSummary,
            string mechanism,
            string advancedNote,
            string assemblyId = "",
            string componentId = "",
            IEnumerable<string> modelObjectNames = null)
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
            AssemblyId = assemblyId ?? string.Empty;
            ComponentId = componentId ?? string.Empty;
            ModelObjectNames = (modelObjectNames ?? Enumerable.Empty<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        public string GetKnowledge(DifficultyLevel difficulty)
        {
            if (difficulty == DifficultyLevel.Simple || string.IsNullOrWhiteSpace(Mechanism))
            {
                return LimitKnowledge(SimpleSummary);
            }

            if (difficulty == DifficultyLevel.Standard || string.IsNullOrWhiteSpace(AdvancedNote))
            {
                return LimitKnowledge(Mechanism);
            }

            return LimitKnowledge(AdvancedNote);
        }

        private static string LimitKnowledge(string value)
        {
            string text = (value ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
            const int maxCharacters = 50;
            if (text.Length <= maxCharacters)
            {
                return text;
            }

            return text.Substring(0, maxCharacters - 1).TrimEnd() + "…";
        }
    }
}
