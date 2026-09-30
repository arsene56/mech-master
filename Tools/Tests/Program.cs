using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MechMaster.Domain;
using MechMaster.Runtime;

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
        Run("Bolt STEP identity and complete service-group coverage", BoltSourceAndTierCoverage);
        Run("Bolt active/passive axes and closed teaching cycle", BoltArticulationRig);
        Run("Periodic motion curve continuity and bounded interpolation", PeriodicCurveContinuity);
        Run("Bolt continuous alternating cycle without global holds", BoltContinuousCycle);
        Run("OpenTorque standard source and complete service coverage", OpenTorqueSourceAndTierCoverage);
        Run("OpenTorque motion bindings and source limitations", OpenTorqueMotionRig);
        Run("Fixed-ring planetary ratio, continuous phase and closed cycle", PlanetaryKinematics);
        Run("Carbon bike provenance and conservative complete service groups", CarbonBikeServiceGroups);
        Run("Carbon suspension source bindings and continuous teaching cycle", CarbonSuspensionCycle);

        Console.WriteLine(failures == 0
            ? "All MechMaster domain tests passed."
            : failures + " test(s) failed.");
        return failures == 0 ? 0 : 1;
    }

    private static void CarbonBikeServiceGroups()
    {
        using JsonDocument source = LoadJson("Assets", "StreamingAssets", "MechanicalCatalog", "carbon_frame_bike_model_manifest.json");
        JsonElement manifest = source.RootElement;
        Equal("93c72b11cf78dd6a3cd50b875d752cd7e6dd4ab2", manifest.GetProperty("sourceCommit").GetString());
        Equal("95c016737df48d1beaa7bb5d6eb4789d8102013a246c59bdf6a25021fa264373", manifest.GetProperty("glbSha256").GetString());
        Equal("m", manifest.GetProperty("units").GetString()); Equal(307, manifest.GetProperty("partObjectCount").GetInt32());
        Equal(757, manifest.GetProperty("sourceNodeCount").GetInt32()); Equal(312, manifest.GetProperty("sourceMeshCount").GetInt32());
        False(manifest.GetProperty("authoredImagesRedistributed").GetBoolean());
        var names = new HashSet<string>(); var ids = new HashSet<string>(); var indices = new HashSet<int>();
        var shock = new HashSet<string>(); var bearing = new HashSet<string>();
        foreach (JsonElement part in manifest.GetProperty("parts").EnumerateArray())
        {
            string name = part.GetProperty("object").GetString(); True(names.Add(name)); True(name.StartsWith("MM_carbon_n"));
            True(ids.Add(part.GetProperty("id").GetString())); True(indices.Add(part.GetProperty("sourceNodeIndex").GetInt32()));
            True(part.GetProperty("sourceTriangles").GetInt32() > 0);
            False(part.GetProperty("sourcePath").GetString().ToLowerInvariant().Contains("logo"));
            if (part.GetProperty("assemblyId").GetString() == "shock") shock.Add(name);
            if (part.GetProperty("serviceBoundary").GetString() == "sealed-bearing") bearing.Add(name);
        }
        Equal(307, names.Count); Equal(13, shock.Count); Equal(27, bearing.Count);
        Equal(7, manifest.GetProperty("excludedReferences").GetArrayLength());
        int marks = 0, shadows = 0;
        foreach (JsonElement excluded in manifest.GetProperty("excludedReferences").EnumerateArray())
        {
            False(indices.Contains(excluded.GetProperty("nodeIndex").GetInt32()));
            if (excluded.GetProperty("reason").GetString() == "visible-brand-mark") marks++;
            else if (excluded.GetProperty("reason").GetString() == "display-shadow") shadows++;
            else throw new InvalidOperationException("Unexpected omitted physical geometry");
        }
        Equal(4, marks); Equal(3, shadows);
        using JsonDocument catalog = LoadJson("Assets", "Resources", "MechanicalCatalog", "CarbonFrameBikeInteractionCatalog.json");
        int level = 0;
        foreach (JsonElement plan in catalog.RootElement.GetProperty("plans").EnumerateArray())
        {
            Equal(new[] { "Simple", "Standard", "Advanced" }[level], plan.GetProperty("difficulty").GetString());
            Equal(new[] { 14, 34, 51 }[level++], plan.GetProperty("steps").GetArrayLength());
            var covered = new HashSet<string>(); var stepIds = new HashSet<string>(); int shocks = 0;
            foreach (JsonElement step in plan.GetProperty("steps").EnumerateArray())
            {
                True(stepIds.Add(step.GetProperty("id").GetString()));
                var members = new HashSet<string>();
                foreach (JsonElement name in step.GetProperty("objectNames").EnumerateArray())
                { True(covered.Add(name.GetString())); True(members.Add(name.GetString())); }
                if (step.GetProperty("assemblyId").GetString() == "shock") { shocks++; True(members.SetEquals(shock)); }
                foreach (string field in new[] { "simpleSummary", "mechanism", "advancedNote" })
                { string text = step.GetProperty(field).GetString(); True(!string.IsNullOrWhiteSpace(text) && text.Length <= 50); }
            }
            True(covered.SetEquals(names)); Equal(1, shocks);
        }
        Equal(3, level);
        string folder = Path.Combine("Assets", "StreamingAssets", "MechanicalCatalog", "CarbonFrameBike");
        string attribution = File.ReadAllText(Path.Combine(folder, "ATTRIBUTION.txt"));
        True(attribution.Contains("Robert Schweier") && attribution.Contains("Felix Herbst") && attribution.Contains("CC BY-SA 4.0"));
        True(attribution.Contains("modifications") && attribution.Contains("does not relicense independent"));
        True(File.ReadAllText(Path.Combine(folder, "CC-BY-SA-4.0-LICENSE.txt")).Contains("Attribution-ShareAlike 4.0"));
        using JsonDocument runtime = LoadJson("Assets", "StreamingAssets", "MechanicalCatalog", "carbon_frame_bike_runtime_assets.json");
        Equal(14, runtime.RootElement.GetProperty("modules").GetArrayLength());
        Equal(114871, runtime.RootElement.GetProperty("lod0Triangles").GetInt32());
        Equal(22000, runtime.RootElement.GetProperty("lod2Triangles").GetInt32());
    }

    private static void CarbonSuspensionCycle()
    {
        using JsonDocument data = LoadJson("Assets", "Resources", "MechanicalCatalog", "CarbonFrameBikeMotionRig.json");
        JsonElement rig = data.RootElement;
        Equal("bike.carbon.full-suspension.v1", rig.GetProperty("modelId").GetString());
        Equal(4.0, rig.GetProperty("cycleSeconds").GetDouble()); Equal(8.0, rig.GetProperty("rearMaximumDegrees").GetDouble());
        Equal(.045, rig.GetProperty("forkCompressionM").GetDouble());
        True(Math.Abs(rig.GetProperty("shockEyeDistanceM").GetDouble() - .2) < .00015);
        var bindings = new HashSet<string>(); var roles = new HashSet<string>();
        foreach (JsonElement binding in rig.GetProperty("bindings").EnumerateArray())
        {
            string name = binding.GetProperty("objectName").GetString();
            string role = binding.GetProperty("role").GetString();
            True(bindings.Add(name)); roles.Add(role);
            if (name == "MM_carbon_n0594_chain") Equal("fixed", role);
        }
        Equal(307, bindings.Count); True(roles.SetEquals(new[] { "fixed", "rear", "front_lower", "shock_upper", "shock_lower", "hidden" }));
        var anchors = new HashSet<string>();
        foreach (string key in new[] { "rearPivotAnchor", "rearAxisAnchor", "rearWheelAnchor", "shockUpperAnchor", "shockLowerAnchor", "forkAxisStartAnchor", "forkAxisEndAnchor" })
        { string name = rig.GetProperty(key).GetString(); True(anchors.Add(name)); True(name.StartsWith("MM_carbon_rig_")); }
        Equal(7, anchors.Count); True(rig.GetProperty("limitations").GetArrayLength() >= 4);
        Equal(0.0, SuspensionTeachingCycle.CompressionFraction(0)); Equal(1.0, SuspensionTeachingCycle.CompressionFraction(.5));
        Equal(0.0, SuspensionTeachingCycle.CompressionFraction(1));
        double previous = 0;
        for (int i = 1; i <= 512; i++)
        {
            double phase = i / 1024.0, amount = SuspensionTeachingCycle.CompressionFraction(phase);
            True(amount >= 0 && amount <= 1 && amount > previous);
            True(Math.Abs(amount - SuspensionTeachingCycle.CompressionFraction(phase + 12)) < 1e-12);
            True(Math.Abs(amount - SuspensionTeachingCycle.CompressionFraction(1 - phase)) < 1e-12);
            previous = amount;
        }
        double h = 1e-5;
        foreach (double phase in new[] { 0.0, .25, .5, .75, 1.0 })
        {
            double left = (SuspensionTeachingCycle.CompressionFraction(phase) - SuspensionTeachingCycle.CompressionFraction(phase - h)) / h;
            double right = (SuspensionTeachingCycle.CompressionFraction(phase + h) - SuspensionTeachingCycle.CompressionFraction(phase)) / h;
            True(Math.Abs(left - right) < .0003);
        }
    }

    private static void OpenTorqueSourceAndTierCoverage()
    {
        using JsonDocument manifest = LoadJson("Assets", "StreamingAssets", "MechanicalCatalog", "opentorque_model_manifest.json");
        JsonElement root = manifest.RootElement;
        Equal("gearbox.opentorque.planetary.v1", root.GetProperty("modelId").GetString());
        Equal("412762e9a4ca424564d3ebed882db95ef4b22ed9", root.GetProperty("sourceCommit").GetString());
        Equal("715965dc130555453d7d76d7ab51beeb8a5566a35cc37aae25cbb81151c879a3", root.GetProperty("stepSha256").GetString());
        Equal("m", root.GetProperty("units").GetString());
        Equal(13, root.GetProperty("partTypeCount").GetInt32());
        Equal(19, root.GetProperty("partObjectCount").GetInt32());
        var objects = new HashSet<string>();
        var ids = new HashSet<string>();
        var paths = new HashSet<string>();
        var definitions = new HashSet<string>();
        var frequencies = new Dictionary<string, int>();
        foreach (JsonElement part in root.GetProperty("parts").EnumerateArray())
        {
            True(objects.Add(part.GetProperty("object").GetString()));
            True(ids.Add(part.GetProperty("id").GetString()));
            True(paths.Add(part.GetProperty("sourceInstancePath").GetString()));
            definitions.Add(part.GetProperty("componentId").GetString());
            string name = part.GetProperty("sourceName").GetString();
            frequencies.TryGetValue(name, out int count); frequencies[name] = count + 1;
            True(part.GetProperty("runtimeTriangles").GetInt32() > 0);
            if (name == "RA-8008C Cross Roller Bearing" || name == "F625ZZ")
                Equal("sealed-bearing", part.GetProperty("serviceBoundary").GetString());
            if (name == "Bearing Retainer") Equal("source-part", part.GetProperty("serviceBoundary").GetString());
            False(name.Contains("low backlash"));
        }
        Equal(19, objects.Count); Equal(13, definitions.Count);
        Equal(3, frequencies["Planet Gear"]); Equal(3, frequencies["F625ZZ"]); Equal(3, frequencies["M5x30 Dowel Pin"]);
        Equal(1, frequencies["Actuator Housing"]); False(frequencies.ContainsKey("Ring Gear"));
        using JsonDocument catalog = LoadJson("Assets", "Resources", "MechanicalCatalog", "OpenTorqueInteractionCatalog.json");
        int tier = 0;
        foreach (JsonElement plan in catalog.RootElement.GetProperty("plans").EnumerateArray())
        {
            Equal(new[] { "Simple", "Standard", "Advanced" }[tier], plan.GetProperty("difficulty").GetString());
            Equal(new[] { 5, 10, 17 }[tier++], plan.GetProperty("steps").GetArrayLength());
            var names = new HashSet<string>(); var steps = new HashSet<string>();
            foreach (JsonElement step in plan.GetProperty("steps").EnumerateArray())
            {
                True(steps.Add(step.GetProperty("id").GetString()));
                foreach (string field in new[] { "simpleSummary", "mechanism", "advancedNote" })
                {
                    string text = step.GetProperty(field).GetString();
                    True(!string.IsNullOrWhiteSpace(text) && text.Length <= 50);
                }
                foreach (JsonElement name in step.GetProperty("objectNames").EnumerateArray()) True(names.Add(name.GetString()));
            }
            True(names.SetEquals(objects));
        }
        Equal(3, tier);
        string notices = Path.Combine("Assets", "StreamingAssets", "MechanicalCatalog", "OpenTorque");
        True(File.ReadAllText(Path.Combine(notices, "CC-BY-SA-4.0-LICENSE.txt")).Contains("Attribution-ShareAlike 4.0"));
        string attribution = File.ReadAllText(Path.Combine(notices, "ATTRIBUTION.txt"));
        True(attribution.Contains("Gabrael Levine") && attribution.Contains("modifications") && attribution.Contains("CC BY-SA 4.0"));
    }

    private static void OpenTorqueMotionRig()
    {
        using JsonDocument document = LoadJson("Assets", "Resources", "MechanicalCatalog", "OpenTorqueMotionRig.json");
        JsonElement rig = document.RootElement;
        Equal(9, rig.GetProperty("sunTeeth").GetInt32()); Equal(27, rig.GetProperty("planetTeeth").GetInt32());
        Equal(63, rig.GetProperty("ringTeeth").GetInt32()); Equal(24, rig.GetProperty("closedCycleInputTurns").GetInt32());
        Equal(60, rig.GetProperty("inputRpm").GetInt32()); Equal(.027, rig.GetProperty("orbitRadiusM").GetDouble());
        using JsonDocument manifest = LoadJson("Assets", "StreamingAssets", "MechanicalCatalog", "opentorque_model_manifest.json");
        var objects = new Dictionary<string, string>();
        foreach (JsonElement part in manifest.RootElement.GetProperty("parts").EnumerateArray())
            objects.Add(part.GetProperty("object").GetString(), part.GetProperty("sourceName").GetString());
        var names = new HashSet<string>(); int planets = 0, sun = 0, fixedCount = 0, carriers = 0;
        foreach (JsonElement binding in rig.GetProperty("bindings").EnumerateArray())
        {
            string name = binding.GetProperty("objectName").GetString(); True(names.Add(name)); True(objects.ContainsKey(name));
            switch (binding.GetProperty("role").GetString())
            {
                case "planet": Equal("Planet Gear", objects[name]); planets++; break;
                case "sun": True(objects[name] == "Sun Gear" || objects[name] == "Encoder Magnet Holder"); sun++; break;
                case "carrier": carriers++; break;
                case "fixed": fixedCount++; break;
                default: throw new InvalidOperationException("Unknown motion role");
            }
        }
        Equal(19, names.Count); Equal(3, planets); Equal(2, sun); Equal(9, carriers); Equal(5, fixedCount);
        Equal(8, rig.GetProperty("transparentObjects").GetArrayLength());
        foreach (JsonElement name in rig.GetProperty("transparentObjects").EnumerateArray())
        { True(objects.ContainsKey(name.GetString())); False(objects[name.GetString()] == "Planet Gear" || objects[name.GetString()] == "Sun Gear"); }
        True(rig.GetProperty("limitations").GetArrayLength() >= 4);
    }

    private static void PlanetaryKinematics()
    {
        var drive = new PlanetaryGearKinematics(9, 27, 63);
        Equal(8.0, drive.ReductionRatio); Equal(24, drive.ClosedCycleInputTurns);
        PlanetaryGearKinematics.Angles one = drive.Evaluate(360);
        Equal(360.0, one.Sun); Equal(45.0, one.Carrier); Equal(-60.0, one.Planet);
        PlanetaryGearKinematics.Angles closed = drive.Evaluate(drive.ClosedCycleInputTurns * 360);
        Equal(0.0, closed.Sun % 360); Equal(0.0, closed.Carrier % 360); Equal(0.0, closed.Planet % 360);
        for (int index = -100; index <= 1000; index++)
        {
            double input = index * 13.719;
            var angles = drive.Evaluate(input);
            True(Math.Abs(9 * (angles.Sun - angles.Carrier) - 63 * angles.Carrier) < 1e-9);
            True(Math.Abs(9 * (angles.Sun - angles.Carrier) + 27 * (angles.Planet - angles.Carrier)) < 1e-9);
            var next = drive.Evaluate(input + .001);
            True(Math.Abs((next.Carrier - angles.Carrier) / .001 - .125) < 1e-8);
            True(Math.Abs((next.Planet - angles.Planet) / .001 + 1.0 / 6) < 1e-8);
        }
        bool rejected = false;
        try { new PlanetaryGearKinematics(9, 27, 62); } catch (ArgumentException) { rejected = true; }
        True(rejected); rejected = false;
        try { drive.Evaluate(double.NaN); } catch (ArgumentException) { rejected = true; }
        True(rejected);
        var other = new PlanetaryGearKinematics(20, 30, 80);
        Equal(5.0, other.ReductionRatio);
        var repeat = other.Evaluate(other.ClosedCycleInputTurns * 360.0);
        True(Math.Abs(repeat.Carrier % 360) < 1e-8 && Math.Abs(repeat.Planet % 360) < 1e-8);
    }

    private static void BoltSourceAndTierCoverage()
    {
        using JsonDocument manifest = LoadJson("Assets", "StreamingAssets", "MechanicalCatalog", "bolt_model_manifest.json");
        JsonElement source = manifest.RootElement;
        Equal("66af1522b4fba0ec4a1d7790e66f5e4652208d30", source.GetProperty("sourceCommit").GetString());
        Equal("m", source.GetProperty("units").GetString());
        Equal(56, source.GetProperty("partTypeCount").GetInt32());
        Equal(345, source.GetProperty("partObjectCount").GetInt32());
        Equal(64, source.GetProperty("stepSha256").GetString().Length);
        var objects = new HashSet<string>(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var modules = new HashSet<string>(StringComparer.Ordinal);
        int stators = 0, rotors = 0, belts = 0, ankles = 0, driverBoards = 0;
        foreach (JsonElement part in source.GetProperty("parts").EnumerateArray())
        {
            True(objects.Add(part.GetProperty("object").GetString()));
            True(ids.Add(part.GetProperty("id").GetString()));
            True(paths.Add(part.GetProperty("sourceInstancePath").GetString()));
            modules.Add(part.GetProperty("assemblyId").GetString());
            True(part.GetProperty("runtimeTriangles").GetInt32() > 0);
            True(!string.IsNullOrWhiteSpace(part.GetProperty("displayName").GetString()));
            string name = part.GetProperty("sourceName").GetString();
            if (name.EndsWith("4004_stator")) stators++;
            if (name.EndsWith("4004_rotor")) rotors++;
            if (name.StartsWith("transmission_timing_belt_")) belts++;
            if (name == "pin_5mm_28mm") ankles++;
            if (name == "micro_driver_90_deg_hirose") driverBoards++;
        }
        Equal(345, objects.Count);
        Equal(12, modules.Count);
        Equal(6, stators); Equal(6, rotors); Equal(12, belts); Equal(2, ankles); Equal(3, driverBoards);
        using JsonDocument catalog = LoadJson("Assets", "Resources", "MechanicalCatalog", "BoltInteractionCatalog.json");
        int[] expected = { 12, 23, 42 };
        int level = 0;
        foreach (JsonElement plan in catalog.RootElement.GetProperty("plans").EnumerateArray())
        {
            Equal(expected[level++], plan.GetProperty("steps").GetArrayLength());
            var bound = new HashSet<string>(StringComparer.Ordinal);
            var motorGroups = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (JsonElement step in plan.GetProperty("steps").EnumerateArray())
            {
                foreach (string field in new[] { "simpleSummary", "mechanism", "advancedNote" })
                {
                    True(step.GetProperty(field).GetString().Length > 0);
                    True(step.GetProperty(field).GetString().Length <= 50);
                }
                foreach (JsonElement name in step.GetProperty("objectNames").EnumerateArray())
                    True(bound.Add(name.GetString()));
                if (step.GetProperty("componentId").GetString() == "motor")
                {
                    string group = step.GetProperty("assemblyId").GetString();
                    motorGroups[group] = step.GetProperty("objectNames").GetArrayLength();
                }
            }
            True(bound.SetEquals(objects));
            if (plan.GetProperty("difficulty").GetString() == "Advanced")
            {
                Equal(6, motorGroups.Count);
                foreach (int count in motorGroups.Values) True(count > 4);
            }
        }
    }

    private static void BoltArticulationRig()
    {
        using JsonDocument document = LoadJson("Assets", "Resources", "MechanicalCatalog", "BoltMotionRig.json");
        JsonElement rig = document.RootElement;
        Equal("robot.odri.bolt.6dof.v1", rig.GetProperty("modelId").GetString());
        Equal(8, rig.GetProperty("joints").GetArrayLength());
        Equal(345, rig.GetProperty("bindings").GetArrayLength());
        var ids = new HashSet<string>(StringComparer.Ordinal) { "fixed" };
        var names = new HashSet<string>(StringComparer.Ordinal);
        using JsonDocument manifest = LoadJson("Assets", "StreamingAssets", "MechanicalCatalog", "bolt_model_manifest.json");
        foreach (JsonElement part in manifest.RootElement.GetProperty("parts").EnumerateArray())
            True(names.Add(part.GetProperty("object").GetString()));
        int passive = 0;
        foreach (JsonElement joint in rig.GetProperty("joints").EnumerateArray())
        {
            string parent = joint.GetProperty("parentId").GetString();
            True(string.IsNullOrEmpty(parent) || ids.Contains(parent));
            True(ids.Add(joint.GetProperty("id").GetString()));
            True(names.Contains(joint.GetProperty("pivotObject").GetString()));
            Equal(1, Math.Abs(joint.GetProperty("axisSign").GetInt32()));
            if (joint.GetProperty("passive").GetBoolean())
            {
                passive++;
                True(names.Contains(joint.GetProperty("axisObject").GetString()));
                True(parent.EndsWith("_knee"));
            }
            else
            {
                True(names.Contains(joint.GetProperty("axisStartObject").GetString()));
                True(names.Contains(joint.GetProperty("axisEndObject").GetString()));
            }
        }
        Equal(2, passive);
        var bound = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement binding in rig.GetProperty("bindings").EnumerateArray())
        {
            True(bound.Add(binding.GetProperty("objectName").GetString()));
            True(ids.Contains(binding.GetProperty("jointId").GetString()));
        }
        True(bound.SetEquals(names));
        float phase = -1;
        JsonElement frames = rig.GetProperty("keyframes");
        foreach (JsonElement frame in frames.EnumerateArray())
        {
            True(frame.GetProperty("phase").GetSingle() > phase);
            phase = frame.GetProperty("phase").GetSingle();
            Equal(8, frame.GetProperty("angles").GetArrayLength());
            int index = 0;
            foreach (JsonElement joint in rig.GetProperty("joints").EnumerateArray())
            {
                float angle = frame.GetProperty("angles")[index++].GetSingle();
                True(angle >= joint.GetProperty("minimumDegrees").GetSingle());
                True(angle <= joint.GetProperty("maximumDegrees").GetSingle());
            }
        }
        Equal(0f, frames[0].GetProperty("phase").GetSingle());
        Equal(1f, phase);
        for (int index = 0; index < 8; index++)
        {
            Equal(0f, frames[0].GetProperty("angles")[index].GetSingle());
            Equal(0f, frames[frames.GetArrayLength() - 1].GetProperty("angles")[index].GetSingle());
        }
        foreach (string side in new[] { "left", "right" })
        {
            JsonElement dimensions = rig.GetProperty("dimensions").GetProperty(side);
            True(Math.Abs(dimensions.GetProperty("upperLegAxisDistanceM").GetDouble() - .2) < .0003);
            True(Math.Abs(dimensions.GetProperty("lowerLegAxisDistanceM").GetDouble() - .2) < .0003);
        }
    }

    private static void PeriodicCurveContinuity()
    {
        // Nonuniform phases exercise duration-aware tangents and the seam.
        float[] phases = { 0, .125f, .5f, .75f, 1 };
        float[][] values = { new float[] { 0, 0 }, new float[] { 8, -3 },
            new float[] { 1, 6 }, new float[] { -6, -2 }, new float[] { 0, 0 } };
        var curve = new PeriodicMotionCurve(phases, values);
        CheckCurveContinuity(curve, phases, values);
        var a = new float[2]; var b = new float[2];
        curve.Evaluate(-.125f, a); curve.Evaluate(.875f, b);
        for (int channel = 0; channel < 2; channel++) Equal(a[channel], b[channel]);
        curve.Evaluate(1.125f, a); curve.Evaluate(.125f, b);
        for (int channel = 0; channel < 2; channel++) Equal(a[channel], b[channel]);
        curve.Evaluate(.5f - .0001f, a); curve.Evaluate(.5f + .0001f, b);
        True(Math.Abs((b[0] - a[0]) / .0002f) > 5);
    }

    private static void CheckCurveContinuity(PeriodicMotionCurve curve, float[] phases, float[][] values)
    {
        int channels = values[0].Length;
        var before = new float[channels]; var at = new float[channels]; var after = new float[channels];
        const float epsilon = .0001f;
        for (int frame = 0; frame < phases.Length; frame++)
        {
            float phase = phases[frame];
            curve.Evaluate(phase - epsilon, before); curve.Evaluate(phase, at); curve.Evaluate(phase + epsilon, after);
            for (int channel = 0; channel < channels; channel++)
            {
                True(Math.Abs(at[channel] - values[frame][channel]) < 1e-5f);
                float incoming = (at[channel] - before[channel]) / epsilon;
                float outgoing = (after[channel] - at[channel]) / epsilon;
                True(Math.Abs(incoming - outgoing) < .5f);
            }
            if (frame == 0) continue;
            for (int sample = 0; sample <= 80; sample++)
            {
                curve.Evaluate(phases[frame - 1] + (phase - phases[frame - 1]) * sample / 80f, at);
                for (int channel = 0; channel < channels; channel++)
                {
                    True(at[channel] >= Math.Min(values[frame - 1][channel], values[frame][channel]) - 1e-5f);
                    True(at[channel] <= Math.Max(values[frame - 1][channel], values[frame][channel]) + 1e-5f);
                }
            }
        }
    }

    private static void BoltContinuousCycle()
    {
        using JsonDocument document = LoadJson("Assets", "Resources", "MechanicalCatalog", "BoltMotionRig.json");
        JsonElement rig = document.RootElement, frames = rig.GetProperty("keyframes"), joints = rig.GetProperty("joints");
        Equal(12f, rig.GetProperty("cycleSeconds").GetSingle());
        Equal(17, frames.GetArrayLength());
        var phases = new float[frames.GetArrayLength()]; var values = new float[phases.Length][];
        for (int frame = 0; frame < phases.Length; frame++)
        {
            phases[frame] = frames[frame].GetProperty("phase").GetSingle();
            values[frame] = new float[8];
            for (int channel = 0; channel < 8; channel++)
                values[frame][channel] = frames[frame].GetProperty("angles")[channel].GetSingle();
            for (int side = 0; side < 8; side += 4)
                True(Math.Abs(values[frame][side + 1] + values[frame][side + 2] + values[frame][side + 3]) < 1e-5f);
        }
        var curve = new PeriodicMotionCurve(phases, values);
        CheckCurveContinuity(curve, phases, values);
        var at = new float[8]; var before = new float[8]; var after = new float[8]; var opposite = new float[8];
        for (int sample = 0; sample < 512; sample++)
        {
            float phase = sample / 512f;
            curve.Evaluate(phase, at); curve.Evaluate(phase - .0001f, before); curve.Evaluate(phase + .0001f, after);
            curve.Evaluate(phase + .5f, opposite);
            double activeRateSquared = 0;
            for (int channel = 0; channel < 8; channel++)
            {
                True(float.IsFinite(at[channel]));
                True(at[channel] >= joints[channel].GetProperty("minimumDegrees").GetSingle());
                True(at[channel] <= joints[channel].GetProperty("maximumDegrees").GetSingle());
                if (channel < 4) True(Math.Abs(at[channel] - opposite[channel + 4]) < .001f);
                if (joints[channel].GetProperty("passive").GetBoolean()) continue;
                double rate = (after[channel] - before[channel]) / .0002f;
                activeRateSquared += rate * rate;
            }
            // A true turnaround in one joint is fine; the entire robot must
            // not stop at a pose or at the repeating cycle boundary.
            True(Math.Sqrt(activeRateSquared) > 15);
        }
        curve.Evaluate(.05f, at);
        True(Math.Abs(at[1]) > 1 && Math.Abs(at[2]) > 1 && Math.Abs(at[5]) > 1 && Math.Abs(at[6]) > 1);
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
