using System;
using System.Collections.Generic;
using System.Linq;

namespace MechMaster.Domain
{
    public sealed class DisassemblyPlan
    {
        private readonly List<PartDefinition> steps;
        private readonly HashSet<string> removedPartIds =
            new HashSet<string>(StringComparer.Ordinal);

        public DifficultyLevel Difficulty { get; private set; }
        public AssemblyMode Mode { get; private set; }
        public IReadOnlyList<PartDefinition> Steps => steps;
        public int RemovedCount => removedPartIds.Count;
        public bool IsDisassemblyComplete => removedPartIds.Count == steps.Count;
        public bool IsAssemblyComplete => removedPartIds.Count == 0;

        public PartDefinition ExpectedPart
        {
            get
            {
                if (Mode == AssemblyMode.Disassemble)
                {
                    return steps.FirstOrDefault(step => !removedPartIds.Contains(step.Id));
                }

                return steps.LastOrDefault(step => removedPartIds.Contains(step.Id));
            }
        }

        public DisassemblyPlan(
            DifficultyLevel difficulty,
            IEnumerable<PartDefinition> orderedSteps)
        {
            if (orderedSteps == null)
            {
                throw new ArgumentNullException(nameof(orderedSteps));
            }

            Difficulty = difficulty;
            steps = orderedSteps.ToList();

            if (steps.Count == 0)
            {
                throw new ArgumentException("A plan needs at least one step.", nameof(orderedSteps));
            }

            if (steps.Select(step => step.Id).Distinct(StringComparer.Ordinal).Count() != steps.Count)
            {
                throw new ArgumentException("Part ids must be unique within a plan.", nameof(orderedSteps));
            }

            Mode = AssemblyMode.Disassemble;
        }

        public void SetMode(AssemblyMode mode)
        {
            Mode = mode;
        }

        public bool IsRemoved(string partId)
        {
            return removedPartIds.Contains(partId);
        }

        public OperationResult TryOperate(string partId, ToolKind selectedTool)
        {
            PartDefinition part = steps.FirstOrDefault(
                step => string.Equals(step.Id, partId, StringComparison.Ordinal));

            if (part == null)
            {
                return OperationResult.Failed(
                    OperationFailure.UnknownPart,
                    "这个零件不属于当前练习。",
                    null);
            }

            PartDefinition expected = ExpectedPart;
            if (expected == null)
            {
                string completeMessage = Mode == AssemblyMode.Disassemble
                    ? "拆解已经完成，可以开始组装。"
                    : "组装已经完成。";
                return OperationResult.Failed(
                    OperationFailure.AlreadyComplete,
                    completeMessage,
                    part);
            }

            if (!string.Equals(expected.Id, part.Id, StringComparison.Ordinal))
            {
                return OperationResult.Failed(
                    OperationFailure.WrongOrder,
                    "先观察连接关系，下一步应处理“" + expected.DisplayName + "”。",
                    part);
            }

            if (part.RequiredTool != selectedTool)
            {
                return OperationResult.Failed(
                    OperationFailure.WrongTool,
                    "这个工具不合适，请选择“" + ToolDisplayName(part.RequiredTool) + "”。",
                    part);
            }

            if (Mode == AssemblyMode.Disassemble)
            {
                removedPartIds.Add(part.Id);
            }
            else
            {
                removedPartIds.Remove(part.Id);
            }

            return OperationResult.Success(part, Mode);
        }

        public void Reset()
        {
            removedPartIds.Clear();
            Mode = AssemblyMode.Disassemble;
        }

        public static string ToolDisplayName(ToolKind tool)
        {
            switch (tool)
            {
                case ToolKind.HexKey:
                    return "内六角扳手";
                case ToolKind.TorxKey:
                    return "梅花扳手";
                default:
                    return "手";
            }
        }
    }
}

