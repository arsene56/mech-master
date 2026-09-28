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
        Run("Direct operation needs no tool", DirectOperationNeedsNoTool);
        Run("Disassembly supports arbitrary order", DisassemblySupportsArbitraryOrder);
        Run("Assembly supports arbitrary order", AssemblySupportsArbitraryOrder);
        Run("Knowledge is concise", KnowledgeIsConcise);
        Run("Engineering catalog structure", EngineeringCatalogStructure);
        Run("Engineering catalog dimensions", EngineeringCatalogDimensions);
        Run("Engineering catalog quantities", EngineeringCatalogQuantities);
        Run("Engineering interaction plan counts", EngineeringInteractionPlanCounts);
        Run("Engineering model bindings", EngineeringModelBindings);
        Run("Bicycle motion configuration", BicycleMotionConfiguration);
        Run("Discoverable mechanical model manifests", DiscoverableModelManifests);
        Run("Moveo source and complete tier coverage", MoveoSourceAndTierCoverage);
        Run("Moveo articulation rig and closed teaching cycle", MoveoArticulationRig);

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
        OperationResult result = plan.TryOperate(part.Id);
        True(result.Succeeded);
        True(plan.IsRemoved(part.Id));
        Equal(1, plan.RemovedCount);
    }

    private static void DirectOperationNeedsNoTool()
    {
        DisassemblyPlan plan = FrontBrakeCatalog.CreatePlan(DifficultyLevel.Simple);
        OperationResult result = plan.TryOperate(plan.ExpectedPart.Id);
        True(result.Succeeded);
        Equal(OperationFailure.None, result.Failure);
        Equal(1, plan.RemovedCount);
    }

    private static void DisassemblySupportsArbitraryOrder()
    {
        DisassemblyPlan plan = FrontBrakeCatalog.CreatePlan(DifficultyLevel.Advanced);
        for (int index = plan.Steps.Count - 1; index >= 0; index--)
        {
            PartDefinition part = plan.Steps[index];
            True(plan.TryOperate(part.Id).Succeeded);
        }

        True(plan.IsDisassemblyComplete);
        Equal(plan.Steps.Count, plan.RemovedCount);
    }

    private static void AssemblySupportsArbitraryOrder()
    {
        DisassemblyPlan plan = FrontBrakeCatalog.CreatePlan(DifficultyLevel.Standard);
        foreach (PartDefinition step in plan.Steps)
        {
            True(plan.TryOperate(step.Id).Succeeded);
        }

        plan.SetMode(AssemblyMode.Assemble);
        for (int index = 0; index < plan.Steps.Count; index++)
        {
            PartDefinition part = plan.Steps[index];
            True(plan.TryOperate(part.Id).Succeeded);
        }

        True(plan.IsAssemblyComplete);
    }

    private static void KnowledgeIsConcise()
    {
        PartDefinition part = FrontBrakeCatalog.CreatePlan(DifficultyLevel.Advanced).Steps[0];
        string simple = part.GetKnowledge(DifficultyLevel.Simple);
        string standard = part.GetKnowledge(DifficultyLevel.Standard);
        string advanced = part.GetKnowledge(DifficultyLevel.Advanced);
        True(simple.Length > 0);
        True(standard.Length > 0);
        True(advanced.Length > 0);
        True(simple.Length <= 50);
        True(standard.Length <= 50);
        True(advanced.Length <= 50);
        True(simple != standard);
        True(standard != advanced);
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
            { "Simple", 15 },
            { "Standard", 30 },
            { "Advanced", 45 }
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
                True(step.GetProperty("simpleSummary").GetString().Length <= 50);
                True(step.GetProperty("mechanism").GetString().Length <= 50);
                True(step.GetProperty("advancedNote").GetString().Length <= 50);
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
        foreach (JsonElement plan in interaction.RootElement.GetProperty("plans").EnumerateArray())
        {
            var boundObjects = new HashSet<string>(StringComparer.Ordinal);
            bool hasGroupedParts = false;
            foreach (JsonElement step in plan.GetProperty("steps").EnumerateArray())
            {
                JsonElement names = step.GetProperty("objectNames");
                True(names.GetArrayLength() > 0);
                hasGroupedParts |= names.GetArrayLength() > 1;
                foreach (JsonElement name in names.EnumerateArray())
                {
                    string objectName = name.GetString();
                    True(modelObjects.Contains(objectName));
                    True(boundObjects.Add(objectName));
                }
            }
            True(hasGroupedParts);
            Equal(modelObjects.Count, boundObjects.Count);
        }
    }

    private static void DiscoverableModelManifests()
    {
        string root = Directory.GetCurrentDirectory();
        string manifestDirectory = Path.Combine(root, "Assets", "Resources",
            "MechanicalCatalog", "Models");
        string[] manifests = Directory.GetFiles(manifestDirectory, "*.json");
        True(manifests.Length > 0);
        var modelIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in manifests)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement model = document.RootElement;
            Equal(1, model.GetProperty("schemaVersion").GetInt32());
            string modelId = model.GetProperty("id").GetString();
            True(!string.IsNullOrWhiteSpace(modelId));
            True(modelIds.Add(modelId));
            True(!string.IsNullOrWhiteSpace(model.GetProperty("displayName").GetString()));
            string catalogPath = model.GetProperty("catalogResourcePath").GetString();
            string catalogFile = Path.Combine(root, "Assets", "Resources",
                catalogPath.Replace('/', Path.DirectorySeparatorChar) + ".json");
            True(File.Exists(catalogFile));

            var modules = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement module in model.GetProperty("moduleResourcePaths").EnumerateArray())
            {
                string resourcePath = module.GetString();
                True(modules.Add(resourcePath));
                string assetPath = Path.Combine(root, "Assets", "Resources",
                    resourcePath.Replace('/', Path.DirectorySeparatorChar));
                True(File.Exists(assetPath + ".fbx") || File.Exists(assetPath + ".prefab"));
            }
            True(modules.Count > 0);

            var assemblies = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement assembly in model.GetProperty("assemblies").EnumerateArray())
            {
                True(assemblies.Add(assembly.GetProperty("id").GetString()));
                True(!string.IsNullOrWhiteSpace(assembly.GetProperty("displayName").GetString()));
            }
            True(assemblies.Count > 0);

            using JsonDocument catalog = JsonDocument.Parse(File.ReadAllText(catalogFile));
            Equal(modelId, catalog.RootElement.GetProperty("moduleId").GetString());
            var difficulties = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement plan in catalog.RootElement.GetProperty("plans").EnumerateArray())
            {
                True(difficulties.Add(plan.GetProperty("difficulty").GetString()));
                var stepIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonElement step in plan.GetProperty("steps").EnumerateArray())
                {
                    True(stepIds.Add(step.GetProperty("id").GetString()));
                    True(assemblies.Contains(step.GetProperty("assemblyId").GetString()));
                    True(step.GetProperty("objectNames").GetArrayLength() > 0);
                }
                True(stepIds.Count > 0);
            }
            True(difficulties.SetEquals(new[] { "Simple", "Standard", "Advanced" }));
        }
    }

    private static void MoveoSourceAndTierCoverage()
    {
        using JsonDocument manifest = LoadJson("Assets", "StreamingAssets", "MechanicalCatalog",
            "moveo_model_manifest.json");
        JsonElement source = manifest.RootElement;
        Equal("0866a92501277636f76000a195d8a16d44b5b476", source.GetProperty("sourceCommit").GetString());
        Equal("m", source.GetProperty("units").GetString());
        Equal(87, source.GetProperty("partTypeCount").GetInt32());
        Equal(366, source.GetProperty("partObjectCount").GetInt32());
        var names = new HashSet<string>(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int servos = 0;
        var electronics = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement part in source.GetProperty("parts").EnumerateArray())
        {
            True(names.Add(part.GetProperty("object").GetString()));
            True(ids.Add(part.GetProperty("id").GetString()));
            True(part.GetProperty("runtimeTriangles").GetInt32() > 0);
            if (part.GetProperty("sourceName").GetString() == "Servo Futaba S3003") servos++;
            if (part.GetProperty("role").GetString() == "boards")
                electronics.Add(part.GetProperty("object").GetString());
        }
        Equal(366, names.Count);
        Equal(1, servos);
        using JsonDocument catalog = LoadJson("Assets", "Resources", "MechanicalCatalog",
            "MoveoInteractionCatalog.json");
        int[] counts = { 9, 20, 42 };
        int level = 0;
        foreach (JsonElement plan in catalog.RootElement.GetProperty("plans").EnumerateArray())
        {
            Equal(counts[level++], plan.GetProperty("steps").GetArrayLength());
            var bound = new HashSet<string>(StringComparer.Ordinal);
            int boardGroups = 0;
            foreach (JsonElement step in plan.GetProperty("steps").EnumerateArray())
            {
                foreach (string field in new[] { "simpleSummary", "mechanism", "advancedNote" })
                    True(step.GetProperty(field).GetString().Length <= 50);
                var group = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonElement name in step.GetProperty("objectNames").EnumerateArray())
                {
                    True(bound.Add(name.GetString()));
                    group.Add(name.GetString());
                }
                if (group.Overlaps(electronics))
                {
                    // Soldered parts must move with the boards in every tier.
                    True(electronics.IsSubsetOf(group));
                    boardGroups++;
                }
            }
            True(bound.SetEquals(names));
            Equal(1, boardGroups);
        }
        Equal(3, level);
    }

    private static void MoveoArticulationRig()
    {
        using JsonDocument model = LoadJson("Assets", "Resources", "MechanicalCatalog", "Models", "Moveo.json");
        Equal("moveo-articulation-v1", model.RootElement.GetProperty("motion").GetProperty("kind").GetString());
        Equal("MechanicalCatalog/MoveoMotionRig", model.RootElement.GetProperty("motion").GetProperty("rigResourcePath").GetString());
        using JsonDocument document = LoadJson("Assets", "Resources", "MechanicalCatalog", "MoveoMotionRig.json");
        JsonElement rig = document.RootElement;
        Equal("arm.bcn3d.moveo.v1", rig.GetProperty("modelId").GetString());
        True(rig.GetProperty("cycleSeconds").GetSingle() >= 10);
        using JsonDocument manifest = LoadJson("Assets", "StreamingAssets", "MechanicalCatalog", "moveo_model_manifest.json");
        var source = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonElement part in manifest.RootElement.GetProperty("parts").EnumerateArray())
            source.Add(part.GetProperty("object").GetString(), part);
        var joints = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonElement joint in rig.GetProperty("joints").EnumerateArray())
        {
            string parent = joint.GetProperty("parentId").GetString();
            True(parent == "" || joints.ContainsKey(parent));
            joints.Add(joint.GetProperty("id").GetString(), joint);
            foreach (string anchor in new[] { "pivotObject", "axisStartObject", "axisEndObject" })
                True(source.ContainsKey(joint.GetProperty(anchor).GetString()));
            True(joint.GetProperty("axisStartObject").GetString() != joint.GetProperty("axisEndObject").GetString());
        }
        Equal(5, joints.Count);
        var names = new HashSet<string>(StringComparer.Ordinal);
        var jawGroups = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement binding in rig.GetProperty("bindings").EnumerateArray())
        {
            string name = binding.GetProperty("objectName").GetString();
            True(names.Add(name));
            string joint = binding.GetProperty("jointId").GetString();
            True(joint == "fixed" || joints.ContainsKey(joint));
            if (binding.GetProperty("gripperGroup").GetString() != "")
            {
                Equal("wrist_pitch", joint);
                jawGroups.Add(binding.GetProperty("gripperGroup").GetString());
            }
            string sourceName = source[name].GetProperty("sourceName").GetString();
            if (sourceName == "1M2A" || sourceName == "Nema 17 Llarg" || sourceName == "Base fusta")
                Equal("fixed", joint);
            if (sourceName == "Smooth bar 8mm x 140mm") Equal("shoulder", joint);
            if (sourceName == "Smooth bar 8mm x 121mm") Equal("elbow", joint);
            if (sourceName == "Barra llisa 8mm x 80mm") Equal("wrist_pitch", joint);
        }
        True(names.SetEquals(source.Keys));
        Equal(6, jawGroups.Count);
        JsonElement gripper = rig.GetProperty("gripper");
        Equal(-gripper.GetProperty("left").GetProperty("direction").GetInt32(),
            gripper.GetProperty("right").GetProperty("direction").GetInt32());
        foreach (string side in new[] { "left", "right" })
            foreach (string anchor in new[] { "driverPivotObject", "driverTipObject", "followerPivotObject", "followerTipObject" })
                True(names.Contains(gripper.GetProperty(side).GetProperty(anchor).GetString()));
        JsonElement frames = rig.GetProperty("keyframes");
        Equal(0f, frames[0].GetProperty("phase").GetSingle());
        Equal(1f, frames[frames.GetArrayLength() - 1].GetProperty("phase").GetSingle());
        float previous = -1;
        bool closes = false;
        foreach (JsonElement frame in frames.EnumerateArray())
        {
            float phase = frame.GetProperty("phase").GetSingle();
            True(float.IsFinite(phase) && phase > previous);
            previous = phase;
            int index = 0;
            foreach (JsonElement joint in joints.Values)
            {
                float angle = frame.GetProperty("angles")[index++].GetSingle();
                True(float.IsFinite(angle) && angle >= joint.GetProperty("minimumDegrees").GetSingle()
                    && angle <= joint.GetProperty("maximumDegrees").GetSingle());
                if (phase == 0 || phase == 1) Equal(0f, angle);
            }
            float closure = frame.GetProperty("gripperDegrees").GetSingle();
            True(closure >= 0 && closure <= gripper.GetProperty("maximumDegrees").GetSingle());
            closes |= closure > 0;
            if (phase == 0 || phase == 1) Equal(0f, closure);
        }
        True(closes);
    }

    private static void BicycleMotionConfiguration()
    {
        using JsonDocument model = LoadJson("Assets", "Resources", "MechanicalCatalog",
            "Models", "Bicycle.json");
        JsonElement motion = model.RootElement.GetProperty("motion");
        Equal("bicycle-pedaling-v1", motion.GetProperty("kind").GetString());
        using JsonDocument engineering = LoadEngineeringCatalog();
        JsonElement dimensions = engineering.RootElement.GetProperty("dimensions");
        Equal(dimensions.GetProperty("frontChainringTeeth")[0].GetInt32(),
            motion.GetProperty("frontTeeth").GetInt32());
        Equal(dimensions.GetProperty("cassetteTeeth")[6].GetInt32(),
            motion.GetProperty("rearTeeth").GetInt32());
        Equal(110, motion.GetProperty("chainLinks").GetInt32());

        using JsonDocument parts = LoadJson("Assets", "StreamingAssets", "MechanicalCatalog",
            "bicycle_model_manifest.json");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement part in parts.RootElement.GetProperty("parts").EnumerateArray())
            names.Add(part.GetProperty("object").GetString());
        foreach (string required in new[] {
            "MM_crank_spindle", "MM_crank_chainring_1", "MM_wheel_front_hub_shell",
            "MM_wheel_rear_hub_shell", "MM_wheel_front_rotor", "MM_wheel_rear_rotor",
            "MM_wheel_rear_cassette_sprocket_07", "MM_pedal_axle_1", "MM_pedal_axle_2",
            "MM_rear_derailleur_jockey_wheel_1", "MM_rear_derailleur_jockey_wheel_2",
            "MM_brake_front_lever_blade", "MM_brake_rear_lever_blade",
            "MM_brake_front_lever_body", "MM_brake_rear_lever_body",
            "MM_brake_front_lever_pivot", "MM_brake_rear_lever_pivot",
            "MM_brake_front_brake_pad_1", "MM_brake_front_brake_pad_2",
            "MM_brake_rear_brake_pad_1", "MM_brake_rear_brake_pad_2",
            "MM_chain_link_001", "MM_chain_link_110", "MM_chain_quick_link_1"
        })
            True(names.Contains(required));
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
