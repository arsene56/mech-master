using System;
using MechMaster.Domain;

internal static class Program
{
    private static int failures;

    private static int Main()
    {
        Run("Difficulty step counts", DifficultyStepCounts);
        Run("Wrong part does not advance", WrongPartDoesNotAdvance);
        Run("Wrong tool does not advance", WrongToolDoesNotAdvance);
        Run("Disassembly completes in order", DisassemblyCompletesInOrder);
        Run("Assembly runs in exact reverse", AssemblyRunsInReverse);
        Run("Knowledge is layered", KnowledgeIsLayered);

        Console.WriteLine(failures == 0
            ? "All MechMaster domain tests passed."
            : failures + " test(s) failed.");
        return failures == 0 ? 0 : 1;
    }

    private static void DifficultyStepCounts()
    {
        Equal(4, FrontBrakeCatalog.CreatePlan(DifficultyLevel.Simple).Steps.Count);
        Equal(8, FrontBrakeCatalog.CreatePlan(DifficultyLevel.Standard).Steps.Count);
        Equal(12, FrontBrakeCatalog.CreatePlan(DifficultyLevel.Advanced).Steps.Count);
    }

    private static void WrongPartDoesNotAdvance()
    {
        DisassemblyPlan plan = FrontBrakeCatalog.CreatePlan(DifficultyLevel.Standard);
        OperationResult result = plan.TryOperate("front_rotor", ToolKind.Hand);
        False(result.Succeeded);
        Equal(OperationFailure.WrongOrder, result.Failure);
        Equal(0, plan.RemovedCount);
    }

    private static void WrongToolDoesNotAdvance()
    {
        DisassemblyPlan plan = FrontBrakeCatalog.CreatePlan(DifficultyLevel.Simple);
        OperationResult result = plan.TryOperate(plan.ExpectedPart.Id, ToolKind.Hand);
        False(result.Succeeded);
        Equal(OperationFailure.WrongTool, result.Failure);
        Equal(0, plan.RemovedCount);
    }

    private static void DisassemblyCompletesInOrder()
    {
        DisassemblyPlan plan = FrontBrakeCatalog.CreatePlan(DifficultyLevel.Advanced);
        while (plan.ExpectedPart != null)
        {
            PartDefinition expected = plan.ExpectedPart;
            True(plan.TryOperate(expected.Id, expected.RequiredTool).Succeeded);
        }

        True(plan.IsDisassemblyComplete);
        Equal(plan.Steps.Count, plan.RemovedCount);
    }

    private static void AssemblyRunsInReverse()
    {
        DisassemblyPlan plan = FrontBrakeCatalog.CreatePlan(DifficultyLevel.Standard);
        foreach (PartDefinition step in plan.Steps)
        {
            True(plan.TryOperate(step.Id, step.RequiredTool).Succeeded);
        }

        plan.SetMode(AssemblyMode.Assemble);
        for (int index = plan.Steps.Count - 1; index >= 0; index--)
        {
            PartDefinition part = plan.Steps[index];
            Equal(part.Id, plan.ExpectedPart.Id);
            True(plan.TryOperate(part.Id, part.RequiredTool).Succeeded);
        }

        True(plan.IsAssemblyComplete);
    }

    private static void KnowledgeIsLayered()
    {
        PartDefinition part = FrontBrakeCatalog.CreatePlan(DifficultyLevel.Advanced).Steps[0];
        string simple = part.GetKnowledge(DifficultyLevel.Simple);
        string standard = part.GetKnowledge(DifficultyLevel.Standard);
        string advanced = part.GetKnowledge(DifficultyLevel.Advanced);
        True(simple.Length < standard.Length);
        True(standard.Length < advanced.Length);
    }

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine("PASS  " + name);
        }
        catch (Exception exception)
        {
            failures++;
            Console.WriteLine("FAIL  " + name + ": " + exception.Message);
        }
    }

    private static void True(bool value)
    {
        if (!value)
        {
            throw new InvalidOperationException("Expected true.");
        }
    }

    private static void False(bool value)
    {
        if (value)
        {
            throw new InvalidOperationException("Expected false.");
        }
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!object.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                "Expected " + expected + " but got " + actual + ".");
        }
    }
}

