namespace MechMaster.Domain
{
    public sealed class OperationResult
    {
        public bool Succeeded { get; private set; }
        public OperationFailure Failure { get; private set; }
        public string Message { get; private set; }
        public PartDefinition Part { get; private set; }

        private OperationResult(
            bool succeeded,
            OperationFailure failure,
            string message,
            PartDefinition part)
        {
            Succeeded = succeeded;
            Failure = failure;
            Message = message;
            Part = part;
        }

        public static OperationResult Success(PartDefinition part, AssemblyMode mode)
        {
            string verb = mode == AssemblyMode.Disassemble ? "已拆下" : "已装回";
            return new OperationResult(true, OperationFailure.None, verb + part.DisplayName + "。", part);
        }

        public static OperationResult Failed(
            OperationFailure failure,
            string message,
            PartDefinition part)
        {
            return new OperationResult(false, failure, message, part);
        }
    }
}

