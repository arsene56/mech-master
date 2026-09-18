namespace MechMaster.Domain
{
    public enum DifficultyLevel
    {
        Simple,
        Standard,
        Advanced
    }

    public enum AssemblyMode
    {
        Disassemble,
        Assemble
    }

    public enum ToolKind
    {
        Hand,
        HexKey,
        TorxKey
    }

    public enum OperationFailure
    {
        None,
        UnknownPart,
        WrongOrder,
        WrongTool,
        AlreadyComplete
    }
}

