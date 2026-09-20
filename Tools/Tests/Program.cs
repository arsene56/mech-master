using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MechMaster.Domain;

internal static class Program
{
    private static int failures;

    private static int Main()
    {
        Run("Difficulty step counts", DifficultyStepCounts);
        Run("Any part can be disassembled", AnyPartCanBeDisassembled);
        Run("Wrong tool does not advance", WrongToolDoesNotAdvance);
        Run("Disassembly supports arbitrary order", DisassemblySupportsArbitraryOrder);
        Run("Assembly supports arbitrary order", AssemblySupportsArbitraryOrder);
        Run("Knowledge is layered", KnowledgeIsLayered);
        Run("Engineering catalog structure", EngineeringCatalogStructure);
        Run("Engineering catalog dimensions", EngineeringCatalogDimensions);
        Run("Engineering catalog quantities", EngineeringCatalogQuantities);
        Run("Engineering interaction plan counts", EngineeringInteractionPlanCounts);
        Run("Engineering model bindings", EngineeringModelBindings);

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

    private static void AnyPartCanBeDisassembled()
    {
        DisassemblyPlan plan = FrontBrakeCatalog.CreatePlan(DifficultyLevel.Standard);
        PartDefinition part = plan.Steps[plan.Steps.Count - 1];
        OperationResult result = plan.TryOperate(part.Id, part.RequiredTool);
        True(result.Succeeded);
        True(plan.IsRemoved(part.Id));
        Equal(1, plan.RemovedCount);
    }

    private static void WrongToolDoesNotAdvance()
    {
        DisassemblyPlan plan = FrontBrakeCatalog.CreatePlan(DifficultyLevel.Simple);
        OperationResult result = plan.TryOperate(plan.ExpectedPart.Id, ToolKind.Hand);
        False(result.Succeeded);
        Equal(OperationFailure.WrongTool, result.Failure);
        Equal(0, plan.RemovedCount);
    }

    private static void DisassemblySupportsArbitraryOrder()
    {
        DisassemblyPlan plan = FrontBrakeCatalog.CreatePlan(DifficultyLevel.Advanced);
        for (int index = plan.Steps.Count - 1; index >= 0; index--)
        {
            PartDefinition part = plan.Steps[index];
            True(plan.TryOperate(part.Id, part.RequiredTool).Succeeded);
        }

        True(plan.IsDisassemblyComplete);
        Equal(plan.Steps.Count, plan.RemovedCount);
    }

    private static void AssemblySupportsArbitraryOrder()
    {
        DisassemblyPlan plan = FrontBrakeCatalog.CreatePlan(DifficultyLevel.Standard);
        foreach (PartDefinition step in plan.Steps)
        {
            True(plan.TryOperate(step.Id, step.RequiredTool).Succeeded);
        }

        plan.SetMode(AssemblyMode.Assemble);
        for (int index = 0; index < plan.Steps.Count; index++)
        {
            PartDefinition part = plan.Steps[index];
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

    private static void EngineeringCatalogStructure()
    {
        using JsonDocument document = LoadEngineeringCatalog();
        JsonElement root = document.RootElement;
        Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Equal("bike.hardtail.27_5.2x10.v1", root.GetProperty("moduleId").GetString());
        Equal("millimetre", root.GetProperty("unit").GetString());
        Equal("repair-training", root.GetProperty("serviceAccuracy").GetString());

        JsonElement assemblies = root.GetProperty("assemblies");
        Equal(14, assemblies.GetArrayLength());
        var assemblyIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement assembly in assemblies.EnumerateArray())
        {
            string assemblyId = assembly.GetProperty("id").GetString();
            True(assemblyIds.Add(assemblyId));
            True(!string.IsNullOrWhiteSpace(assembly.GetProperty("name").GetString()));
        }

        foreach (JsonElement assembly in assemblies.EnumerateArray())
        {
            if (assembly.TryGetProperty("inheritComponentsFrom", out JsonElement inherited))
            {
                True(assemblyIds.Contains(inherited.GetString()));
                continue;
            }

            var componentIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement component in assembly.GetProperty("components").EnumerateArray())
            {
                True(componentIds.Add(component.GetProperty("id").GetString()));
                True(component.GetProperty("quantity").GetInt32() > 0);
                True(!string.IsNullOrWhiteSpace(component.GetProperty("serviceBoundary").GetString()));
                True(!string.IsNullOrWhiteSpace(component.GetProperty("tool").GetString()));
            }
        }
    }

    private static void EngineeringCatalogDimensions()
    {
        using JsonDocument document = LoadEngineeringCatalog();
        JsonElement dimensions = document.RootElement.GetProperty("dimensions");
        Equal(584, dimensions.GetProperty("wheelBeadSeatDiameter").GetInt32());
        Equal(57, dimensions.GetProperty("nominalTireWidth").GetInt32());
        Equal(698, dimensions.GetProperty("wheelOuterDiameter").GetInt32());
        Equal(1120, dimensions.GetProperty("wheelbase").GetInt32());
        Equal(120, dimensions.GetProperty("frontTravel").GetInt32());
        Equal(73, dimensions.GetProperty("bottomBracketShellWidth").GetInt32());
        Equal(110, dimensions.GetProperty("frontHubSpacing").GetInt32());
        Equal(148, dimensions.GetProperty("rearHubSpacing").GetInt32());
        Equal(15, dimensions.GetProperty("frontAxleDiameter").GetInt32());
        Equal(12, dimensions.GetProperty("rearAxleDiameter").GetInt32());
        Equal(180, dimensions.GetProperty("frontRotorDiameter").GetInt32());
        Equal(160, dimensions.GetProperty("rearRotorDiameter").GetInt32());
        Equal(32, dimensions.GetProperty("spokesPerWheel").GetInt32());
        Equal(2, dimensions.GetProperty("frontChainringTeeth").GetArrayLength());
        Equal(10, dimensions.GetProperty("cassetteTeeth").GetArrayLength());
    }

    private static void EngineeringCatalogQuantities()
    {
        using JsonDocument document = LoadEngineeringCatalog();
        JsonElement assemblies = document.RootElement.GetProperty("assemblies");
        var componentsByAssembly = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonElement assembly in assemblies.EnumerateArray())
        {
            if (assembly.TryGetProperty("components", out JsonElement components))
            {
                componentsByAssembly[assembly.GetProperty("id").GetString()] = components;
            }
        }

        int physicalUnits = 0;
        var stableIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement assembly in assemblies.EnumerateArray())
        {
            string assemblyId = assembly.GetProperty("id").GetString();
            JsonElement components;
            if (!assembly.TryGetProperty("components", out components))
            {
                string inheritedId = assembly.GetProperty("inheritComponentsFrom").GetString();
                components = componentsByAssembly[inheritedId];
            }

            foreach (JsonElement component in components.EnumerateArray())
            {
                physicalUnits += component.GetProperty("quantity").GetInt32();
                string logicalId = "bike." + assemblyId + "." + component.GetProperty("id").GetString();
                True(stableIds.Add(logicalId));
            }
        }

        Equal(595, physicalUnits);
        Equal(32, GetQuantity(componentsByAssembly["wheel_front"], "spoke"));
        Equal(32, GetQuantity(componentsByAssembly["wheel_rear"], "spoke"));
        Equal(10, GetQuantity(componentsByAssembly["wheel_rear"], "cassette_sprocket"));
        Equal(110, GetQuantity(componentsByAssembly["chain"], "chain_link"));
        Equal(6, GetQuantity(componentsByAssembly["wheel_front"], "rotor_bolt"));
        Equal(6, GetQuantity(componentsByAssembly["wheel_rear"], "rotor_bolt"));
    }

    private static int GetQuantity(JsonElement components, string componentId)
    {
        foreach (JsonElement component in components.EnumerateArray())
        {
            if (component.GetProperty("id").GetString() == componentId)
            {
                return component.GetProperty("quantity").GetInt32();
            }
        }

        throw new InvalidOperationException("Missing component " + componentId + ".");
    }

    private static JsonDocument LoadEngineeringCatalog()
    {
        string path = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Assets",
            "StreamingAssets",
            "MechanicalCatalog",
            "bicycle_engineering.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Engineering catalog not found.", path);
        }

        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static void EngineeringInteractionPlanCounts()
    {
        using JsonDocument document = LoadJson(
            "Assets", "Resources", "MechanicalCatalog", "BicycleInteractionCatalog.json");
        JsonElement plans = document.RootElement.GetProperty("plans");
        Equal(3, plans.GetArrayLength());
        var expected = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "Simple", 14 },
            { "Standard", 195 },
            { "Advanced", 338 }
        };
        foreach (JsonElement plan in plans.EnumerateArray())
        {
            string difficulty = plan.GetProperty("difficulty").GetString();
            Equal(expected[difficulty], plan.GetProperty("steps").GetArrayLength());
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement step in plan.GetProperty("steps").EnumerateArray())
            {
                True(ids.Add(step.GetProperty("id").GetString()));
                True(step.GetProperty("objectNames").GetArrayLength() > 0);
                True(!string.IsNullOrWhiteSpace(step.GetProperty("simpleSummary").GetString()));
            }
        }
    }

    private static void EngineeringModelBindings()
    {
        using JsonDocument manifest = LoadJson(
            "Assets", "StreamingAssets", "MechanicalCatalog", "bicycle_model_manifest.json");
        var modelObjects = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement part in manifest.RootElement.GetProperty("parts").EnumerateArray())
        {
            True(modelObjects.Add(part.GetProperty("object").GetString()));
        }
        Equal(595, modelObjects.Count);

        using JsonDocument interaction = LoadJson(
            "Assets", "Resources", "MechanicalCatalog", "BicycleInteractionCatalog.json");
        JsonElement advanced = default(JsonElement);
        foreach (JsonElement plan in interaction.RootElement.GetProperty("plans").EnumerateArray())
        {
            if (plan.GetProperty("difficulty").GetString() == "Advanced")
            {
                advanced = plan;
                break;
            }
        }
        True(advanced.ValueKind == JsonValueKind.Object);
        var boundObjects = new HashSet<string>(StringComparer.Ordinal);
        bool hasGroupedRepeatParts = false;
        foreach (JsonElement step in advanced.GetProperty("steps").EnumerateArray())
        {
            JsonElement names = step.GetProperty("objectNames");
            True(names.GetArrayLength() > 0);
            hasGroupedRepeatParts |= names.GetArrayLength() > 1;
            foreach (JsonElement name in names.EnumerateArray())
            {
                string objectName = name.GetString();
                True(modelObjects.Contains(objectName));
                True(boundObjects.Add(objectName));
            }
        }
        True(hasGroupedRepeatParts);
        Equal(modelObjects.Count, boundObjects.Count);
    }

    private static JsonDocument LoadJson(params string[] pathSegments)
    {
        string path = Directory.GetCurrentDirectory();
        foreach (string segment in pathSegments)
        {
            path = Path.Combine(path, segment);
        }
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Required JSON file not found.", path);
        }
        return JsonDocument.Parse(File.ReadAllText(path));
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
