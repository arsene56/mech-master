using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MechMaster.Domain;
using MechMaster.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MechMaster.Editor
{
    [InitializeOnLoad]
    public static class OpenTorqueImportValidator
    {
        private const string ModelId = "gearbox.opentorque.planetary.v1", RunKey = "MechMaster.OpenTorqueValidation";
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int tier;
        private static Dictionary<string, Vector3> previousExplosionOffsets;
        [Serializable] private sealed class Preference { public string key, value; public bool exists, text; public int number; }
        [Serializable] private sealed class Preferences { public List<Preference> values = new List<Preference>(); }

        static OpenTorqueImportValidator() { EditorApplication.playModeStateChanged += OnPlayStateChanged; }

        public static void ValidateFromCommandLine()
        {
            var backup = new Preferences();
            Capture(backup, "mech_master.v1.modelId", true);
            Capture(backup, "mech_master.v1.difficulty", false);
            Capture(backup, "mech_master.v1.narration", false);
            foreach (MechanicalModelDefinition model in MechanicalModelRegistry.Models)
                foreach (DifficultyLevel level in Enum.GetValues(typeof(DifficultyLevel)))
                    foreach (string field in new[] { "removed", "removedIds", "mode" })
                        Capture(backup, "mech_master.v1.progress." + model.id + "." + level + "." + field, field == "removedIds");
            SessionState.SetString(RunKey + ".backup", JsonUtility.ToJson(backup));
            SessionState.SetInt(RunKey + ".result", 1);
            SessionState.SetBool(RunKey, true);
            previousExplosionOffsets = null;
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void Capture(Preferences preferences, string key, bool text) =>
            preferences.values.Add(new Preference { key = key, exists = PlayerPrefs.HasKey(key), text = text,
                value = text ? PlayerPrefs.GetString(key) : "", number = text ? 0 : PlayerPrefs.GetInt(key) });

        private static void OnPlayStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(RunKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
                Later(() => { MechMasterApp.Instance.SetNarrationEnabled(false); MechMasterApp.Instance.SetModel(ModelId); tier = 0; BeginTier(); });
            if (state != PlayModeStateChange.EnteredEditMode) return;
            Preferences backup = JsonUtility.FromJson<Preferences>(SessionState.GetString(RunKey + ".backup", ""));
            foreach (Preference preference in backup.values)
            {
                if (!preference.exists) PlayerPrefs.DeleteKey(preference.key);
                else if (preference.text) PlayerPrefs.SetString(preference.key, preference.value);
                else PlayerPrefs.SetInt(preference.key, preference.number);
            }
            PlayerPrefs.Save();
            int result = SessionState.GetInt(RunKey + ".result", 1);
            SessionState.EraseBool(RunKey); SessionState.EraseString(RunKey + ".backup"); SessionState.EraseInt(RunKey + ".result");
            EditorApplication.Exit(result);
        }

        private static void Later(Action action)
        {
            EditorApplication.delayCall += () => EditorApplication.delayCall += () => {
                try { action(); }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; }
            };
        }

        private static void BeginTier()
        {
            MechMasterApp.Instance.SetDifficulty((DifficultyLevel)tier);
            MechMasterApp.Instance.ResetCurrentPlan();
            Later(ValidateTier);
        }

        private static MeshFilter[] Meshes(GameObject root) => root.GetComponentsInChildren<MeshFilter>()
            .Where(mesh => mesh.name.StartsWith("MM_opentorque_", StringComparison.Ordinal)).ToArray();

        private static void ValidateTier()
        {
            var app = MechMasterApp.Instance;
            var root = GameObject.Find("MechanicalModel_" + ModelId);
            Require(root != null, "Missing model root");
            var view = root.GetComponent<MechanicalModelView>();
            var meshes = Meshes(root);
            Require(view.Parts.Count == new[] { 5, 10, 17 }[tier], "Tier count");
            Require(meshes.Length == 19 && meshes.Select(mesh => mesh.name).Distinct().Count() == 19, "CAD occurrence identity");
            Require(meshes.All(mesh => mesh.sharedMesh.vertexCount > 0 && mesh.GetComponent<Renderer>().sharedMaterials.All(mat => mat != null)), "Geometry / materials");
            foreach (Material material in meshes.SelectMany(mesh => mesh.GetComponent<Renderer>().sharedMaterials))
                Require(material.GetFloat("_Metallic") == (material.name == "OpenTorque steel bearings and pins" ? .85f : 0),
                    "Printed polymer / steel material distinction: " + material.name + " metallic=" + material.GetFloat("_Metallic"));
            Require(app.MotionAvailable && app.MotionGuide.Contains("8∶1") && !app.MotionHint.Contains("刹"), "Gearbox UI controls");
            Bounds bounds = meshes[0].GetComponent<Renderer>().bounds;
            foreach (MeshFilter mesh in meshes) bounds.Encapsulate(mesh.GetComponent<Renderer>().bounds);
            var dimensions = new[] { bounds.size.x, bounds.size.y, bounds.size.z }.OrderBy(value => value).ToArray();
            Require(Mathf.Abs(dimensions[0] - .095f) < .0001f && Mathf.Abs(dimensions[1] - .11f) < .0001f
                && Mathf.Abs(dimensions[2] - .11f) < .0001f, "FBX metre scale / dimensions");
            var start = meshes.ToDictionary(mesh => mesh.name, mesh => mesh.transform.position);
            ValidateMotion(app, root, meshes);
            ValidateExplosion(app, view, meshes);
            MotionInteractionRegression.ValidateGui(app, () => FinishTier(start), error => {
                Debug.LogException(error); EditorApplication.isPlaying = false;
            });
        }

        private static void ValidateExplosion(MechMasterApp app, MechanicalModelView view, MeshFilter[] meshes)
        {
            var source = meshes.ToDictionary(mesh => mesh.name, mesh => (mesh.transform.position, mesh.transform.rotation));
            var saved = LocalProgressStore.LoadRemovedPartIds(ModelId, app.Plan.Difficulty);
            Require(view.GlobalExplosionViewAngles.HasValue, "Specialized explosion layout missing");
            app.ToggleGlobalExplosion();
            Require(view.ExplosionTargetCount == app.Plan.Steps.Count && app.Plan.RemovedCount == 0, "Global explosion changes state");
            Vector3[] predicted = view.GetExplosionFramingPoints();
            foreach (MechanicalPartView part in view.Parts.Values)
                part.SetInspectionExplosion(part.InspectionWorldOffset, true, true);
            Require(predicted.Zip(view.GetExplosionFramingPoints(), Vector3.Distance).All(distance => distance < 1e-5f),
                "Animated target framing differs from final explosion");

            var offsets = new Dictionary<string, Vector3>();
            foreach (MechanicalPartView part in view.Parts.Values)
            {
                foreach (string name in part.Definition.ModelObjectNames)
                {
                    var mesh = meshes.Single(item => item.name == name);
                    Vector3 offset = mesh.transform.position - source[name].position;
                    Require(Vector3.Distance(offset, part.InspectionWorldOffset) < 1e-6f
                        && Quaternion.Angle(mesh.transform.rotation, source[name].rotation) < .02f,
                        "Explosion broke an intact service group or rotated a CAD part");
                    offsets.Add(name, offset);
                }
            }
            Require(offsets.Count == 19 && view.Parts.Values.Select(part => part.InspectionWorldOffset).Distinct().Count() == app.Plan.Steps.Count,
                "Logical explosion units still share the same translation");
            if (previousExplosionOffsets != null)
                Require(offsets.Count(item => Vector3.Distance(item.Value, previousExplosionOffsets[item.Key]) > .01f) >= 12,
                    "Neighbouring explosion tiers are not materially different");
            previousExplosionOffsets = offsets;

            // Large separation should expose actual service units, not just shift
            // an indistinguishable pile. Bounds are conservative for hollow parts.
            MechanicalPartView[] units = view.Parts.Values.ToArray();
            for (int first = 0; first < units.Length; first++)
                for (int second = first + 1; second < units.Length; second++)
                    Require(!units[first].GetWorldBounds().Intersects(units[second].GetWorldBounds()),
                        "Exploded service units overlap: " + units[first].Definition.DisplayName + " / " + units[second].Definition.DisplayName
                        + " " + units[first].GetWorldBounds().ToString("F4") + " / " + units[second].GetWorldBounds().ToString("F4"));
            ValidateExplosionVisibility(view);
            CaptureExplosionPreview(app.Plan.Difficulty, meshes);

            // Repeat clicks, entering disassembly and returning from motion must
            // all use the original CAD pose rather than capture exploded offsets.
            app.ToggleGlobalExplosion(); view.ClearInspectionExplosion(true);
            Require(!app.IsGlobalExplosionActive && view.ExplosionTargetCount == 0, "Repeated click failed to retract");
            Require(meshes.All(mesh => Vector3.Distance(mesh.transform.position, source[mesh.name].position) < 1e-6f), "Explosion pose restore");
            app.ToggleGlobalExplosion(); app.SetInteractionViewMode(false);
            Require(view.ExplosionTargetCount == 0 && meshes.All(mesh => Vector3.Distance(mesh.transform.position, source[mesh.name].position) < 1e-6f),
                "Explosion-to-disassembly did not restore source pose");
            Require(app.Plan.RemovedCount == 0 && LocalProgressStore.LoadRemovedPartIds(ModelId, app.Plan.Difficulty).SequenceEqual(saved),
                "Explosion wrote progress");
            app.SetInteractionViewMode(true);
            app.FrameWholeModel();
            Debug.Log("MECH_MASTER_OPENTORQUE_EXPLOSION_OK level=" + app.Plan.Difficulty + " units=" + units.Length
                + " distinct-offsets,no-overlap,all-visible,source-grouping,framing,retract,restore,unchanged-progress");
        }

        private static void ValidateExplosionVisibility(MechanicalModelView view)
        {
            // Sync only the explosion pose here. The separate stop/drag regression
            // deliberately never receives a test-side physics sync.
            Physics.SyncTransforms();
            Camera camera = Camera.main;
            foreach (MechanicalPartView part in view.Parts.Values)
            {
                Bounds bounds = part.GetWorldBounds();
                var projected = Enumerable.Range(0, 8).Select(index => camera.WorldToScreenPoint(
                    bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (index & 1) == 0 ? -1 : 1, (index & 2) == 0 ? -1 : 1, (index & 4) == 0 ? -1 : 1)))).ToArray();
                float minX = projected.Min(point => point.x), maxX = projected.Max(point => point.x);
                float minY = projected.Min(point => point.y), maxY = projected.Max(point => point.y);
                Require(projected.All(point => point.z > camera.nearClipPlane && point.x >= 0 && point.x <= Screen.width
                    && point.y >= 0 && point.y <= Screen.height), "Explosion clipped: " + part.Definition.DisplayName);
                int surfaceSamples = 0, visibleSamples = 0;
                for (float y = minY; y <= maxY; y += Mathf.Max(.5f, (maxY - minY) / 36f))
                    for (float x = minX; x <= maxX; x += Mathf.Max(.5f, (maxX - minX) / 36f))
                    {
                        var hits = Physics.RaycastAll(camera.ScreenPointToRay(new Vector3(x, y, 0)))
                            .Where(item => item.collider.GetComponent<MechanicalPartHitProxy>()?.Owner != null)
                            .OrderBy(item => item.distance).ToArray();
                        if (!hits.Any(hit => hit.collider.GetComponent<MechanicalPartHitProxy>().Owner == part)) continue;
                        surfaceSamples++;
                        if (hits[0].collider.GetComponent<MechanicalPartHitProxy>().Owner == part) visibleSamples++;
                    }
                Require(surfaceSamples > 0 && visibleSamples >= surfaceSamples * .35f,
                    "Exploded unit obscured from the default view: " + part.Definition.DisplayName
                    + " visible=" + visibleSamples + "/" + surfaceSamples);
            }
        }

        private static void CaptureExplosionPreview(DifficultyLevel level, MeshFilter[] meshes)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Preview"));
            Directory.CreateDirectory(directory);
            // Same viewing angle as runtime, but a consistent 1600x900 comparison
            // camera: opening the native GUI test can change the Game View size.
            var cameraObject = new GameObject("OpenTorqueExplosionComparisonCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.CopyFrom(Camera.main); camera.ResetProjectionMatrix();
            camera.aspect = 1600f / 900f;
            var texture = new RenderTexture(1600, 900, 24);
            Bounds bounds = meshes[0].GetComponent<Renderer>().bounds;
            foreach (MeshFilter mesh in meshes) bounds.Encapsulate(mesh.GetComponent<Renderer>().bounds);
            Quaternion rotation = Camera.main.transform.rotation;
            float tanY = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f), tanX = tanY * camera.aspect;
            float distance = .01f;
            foreach (MeshFilter mesh in meshes)
                for (int index = 0; index < 8; index++)
                {
                    Bounds partBounds = mesh.GetComponent<Renderer>().bounds;
                    Vector3 local = Quaternion.Inverse(rotation) * (partBounds.center + Vector3.Scale(partBounds.extents,
                        new Vector3((index & 1) == 0 ? -1 : 1, (index & 2) == 0 ? -1 : 1, (index & 4) == 0 ? -1 : 1)) - bounds.center);
                    distance = Mathf.Max(distance, Mathf.Max(Mathf.Abs(local.x) / tanX, Mathf.Abs(local.y) / tanY) - local.z);
                }
            camera.transform.SetPositionAndRotation(bounds.center + rotation * new Vector3(0, 0, -distance * 1.18f), rotation);
            RenderTexture previousActive = RenderTexture.active;
            var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(directory, "OpenTorqueExplosion" + level + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previousActive;
                texture.Release(); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static void FinishTier(Dictionary<string, Vector3> start)
        {
            var app = MechMasterApp.Instance;
            string removed = app.Plan.Steps.Last().Id;
            app.Operate(removed);
            var root = GameObject.Find("MechanicalModel_" + ModelId);
            root.GetComponent<MechanicalModelView>().Refresh(app.Plan, true);
            ValidatePartialExplosion(app, root, removed);
            app.ToggleMotion();
            Require(!app.IsMotionActive && app.Plan.RemovedCount == 1, "Incomplete gearbox started motion");
            typeof(MechMasterApp).GetMethod("CreatePlan", Members).Invoke(app, new object[] { app.Plan.Difficulty, true });
            Later(() => FinishRestoredTier(start, removed));
        }

        private static void ValidatePartialExplosion(MechMasterApp app, GameObject root, string removed)
        {
            var view = root.GetComponent<MechanicalModelView>();
            MeshFilter[] meshes = Meshes(root);
            var before = meshes.ToDictionary(mesh => mesh.name, mesh => mesh.transform.position);
            string[] saved = LocalProgressStore.LoadRemovedPartIds(ModelId, app.Plan.Difficulty);
            app.ToggleGlobalExplosion();
            Require(view.ExplosionTargetCount == app.Plan.Steps.Count - 1 && !view.FindPart(removed).IsInspectionExplosionTarget,
                "Partial explosion included a removed unit");
            foreach (MechanicalPartView part in view.Parts.Values.Where(part => !part.IsRemoved))
            {
                part.SetInspectionExplosion(part.InspectionWorldOffset, true, true);
                foreach (string name in part.Definition.ModelObjectNames)
                    Require(Vector3.Distance(part.InspectionWorldOffset, previousExplosionOffsets[name]) < 1e-6f,
                        "Removing a unit reshuffled the explosion layout");
            }
            foreach (string name in view.FindPart(removed).Definition.ModelObjectNames)
                Require(Vector3.Distance(meshes.Single(mesh => mesh.name == name).transform.position, before[name]) < 1e-6f,
                    "Explosion moved a unit out of storage");
            app.FrameWholeModel();
            Require(app.Plan.RemovedCount == 1 && LocalProgressStore.LoadRemovedPartIds(ModelId, app.Plan.Difficulty).SequenceEqual(saved)
                && meshes.All(mesh => Vector3.Distance(mesh.transform.position, before[mesh.name]) < 1e-6f),
                "Partial explosion changed progress or failed to restore");
            Debug.Log("MECH_MASTER_OPENTORQUE_PARTIAL_EXPLOSION_OK level=" + app.Plan.Difficulty + " stable-layout,removed-unit-in-storage,unchanged-progress");
        }

        private static void FinishRestoredTier(Dictionary<string, Vector3> start, string removed)
        {
            var app = MechMasterApp.Instance;
            var root = GameObject.Find("MechanicalModel_" + ModelId);
            var view = root.GetComponent<MechanicalModelView>();
            var meshes = Meshes(root);
            Require(app.Plan.RemovedCount == 1 && app.Plan.IsRemoved(removed) && view.Parts[removed].IsRemoved, "Exact saved state restore");
            foreach (PartDefinition step in app.Plan.Steps.Reverse()) if (!app.Plan.IsRemoved(step.Id)) app.Operate(step.Id);
            view.Refresh(app.Plan, true);
            Require(app.Plan.IsDisassemblyComplete && view.Parts.Values.All(part => part.IsRemoved), "Arbitrary full disassembly");
            Require(new HashSet<string>(LocalProgressStore.LoadRemovedPartIds(ModelId, app.Plan.Difficulty))
                .SetEquals(app.Plan.Steps.Select(step => step.Id)), "Saved removed ID set");
            app.ToggleMode(); foreach (PartDefinition step in app.Plan.Steps) app.Operate(step.Id);
            view.Refresh(app.Plan, true);
            Require(app.Plan.IsAssemblyComplete && meshes.All(mesh => Vector3.Distance(mesh.transform.position, start[mesh.name]) < 1e-6f), "Full source pose restore");
            app.ToggleMotion();
            Require(app.IsMotionPlaying, "Saved and reassembled gearbox cannot play");
            typeof(OpenTorqueMotionController).GetMethod("Advance", Members).Invoke(root.GetComponent<OpenTorqueMotionController>(), new object[] { 1f });
            app.StopMotion();
            Require(meshes.All(mesh => Vector3.Distance(mesh.transform.position, start[mesh.name]) < 1e-6f), "Captured saved tray as motion rest pose");
            Debug.Log("MECH_MASTER_OPENTORQUE_TIER_OK level=" + app.Plan.Difficulty + " steps=" + view.Parts.Count + " instances=19");
            if (++tier < 3) Later(BeginTier);
            else SwitchSmoke(0);
        }

        private static void SwitchSmoke(int index)
        {
            var ids = new[] { "robot.odri.bolt.6dof.v1", "arm.bcn3d.moveo.v1", "bike.hardtail.27_5.2x10.v1", ModelId };
            var app = MechMasterApp.Instance;
            app.SetModel(ids[index]);
            Later(() => {
                Require(app.Model.id == ids[index] && app.MotionAvailable, "Existing model switch / motion binding regression");
                app.ResetCurrentPlan();
                Later(() => {
                    var root = GameObject.Find("MechanicalModel_" + app.Model.id);
                    var motion = root.GetComponents<Component>().OfType<IMechanicalMotionController>().Single();
                    var source = root.GetComponentsInChildren<MeshFilter>().Select(mesh => mesh.transform)
                        .ToDictionary(item => item, item => (item.position, item.rotation));
                    app.ToggleMotion(); Require(app.IsMotionPlaying, "Existing model motion failed to start");
                    ((Component)motion).GetType().GetMethod("Advance", Members).Invoke(motion, new object[] { .5f });
                    app.ToggleMotion(); Require(app.IsMotionActive && !app.IsMotionPlaying, "Existing model pause regression");
                    app.ToggleMotion(); Require(app.IsMotionPlaying, "Existing model resume regression");
                    app.StopMotion();
                    Require(!app.IsMotionActive && source.All(item => Vector3.Distance(item.Key.position, item.Value.position) < 1e-5f
                        && Quaternion.Angle(item.Key.rotation, item.Value.rotation) < .08f), "Existing model stop / restore regression");
                    var view = root.GetComponent<MechanicalModelView>();
                    Require(view.GlobalExplosionViewAngles.HasValue == (app.Model.id == ModelId), "Specialized explosion leaked to another model");
                    app.ToggleGlobalExplosion();
                    Require(view.ExplosionTargetCount == app.Plan.Steps.Count, "Other model explosion count regression");
                    app.FrameWholeModel();
                    Debug.Log("MECH_MASTER_OPENTORQUE_SWITCH_OK model=" + app.Model.id + " play,pause,resume,stop,restore");
                    if (index + 1 < ids.Length) { SwitchSmoke(index + 1); return; }
                    Debug.Log("MECH_MASTER_OPENTORQUE_RUNTIME_OK bind,dimensions,ray-pick,drag,explode,save,reassemble,gear-motion,materials,toolbar,switch");
                    SessionState.SetInt(RunKey + ".result", 0); EditorApplication.isPlaying = false;
                });
            });
        }

        private static void ValidateMotion(MechMasterApp app, GameObject root, MeshFilter[] meshes)
        {
            var controller = root.GetComponent<OpenTorqueMotionController>();
            var rig = JsonUtility.FromJson<OpenTorqueMotionRig>(Resources.Load<TextAsset>(app.Model.motion.rigResourcePath).text);
            var named = meshes.ToDictionary(mesh => mesh.name, mesh => mesh.transform);
            var positions = meshes.ToDictionary(mesh => mesh.name, mesh => mesh.transform.position);
            var rotations = meshes.ToDictionary(mesh => mesh.name, mesh => mesh.transform.rotation);
            var parents = meshes.ToDictionary(mesh => mesh.name, mesh => mesh.transform.parent);
            var materials = meshes.ToDictionary(mesh => mesh.name, mesh => mesh.GetComponent<Renderer>().sharedMaterials);
            var shadows = meshes.ToDictionary(mesh => mesh.name, mesh => mesh.GetComponent<Renderer>().shadowCastingMode);
            var receiveShadows = meshes.ToDictionary(mesh => mesh.name, mesh => mesh.GetComponent<Renderer>().receiveShadows);
            if (tier == 0)
                foreach (MeshFilter mesh in meshes)
                    foreach (Material material in materials[mesh.name])
                        Debug.Log("MECH_MASTER_OPENTORQUE_MATERIAL object=" + mesh.name + " material=" + material.name
                            + " shader=" + material.shader.name + " color=" + material.color
                            + " mode=" + (material.HasProperty("_Mode") ? material.GetFloat("_Mode") : -1)
                            + " metallic=" + material.GetFloat("_Metallic") + " smoothness=" + material.GetFloat("_Glossiness"));
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            bool[] enabled = colliders.Select(collider => collider.enabled).ToArray();
            MethodInfo advance = typeof(OpenTorqueMotionController).GetMethod("Advance", Members);
            MethodInfo phase = typeof(OpenTorqueMotionController).GetMethod("ApplyPhase", Members);
            Vector3 Center(string name) => named[name].TransformPoint(named[name].GetComponent<MeshFilter>().sharedMesh.bounds.center);
            Vector3 axis = (Center(rig.axisEndObject) - Center(rig.axisStartObject)).normalized;
            Vector3 pivot = Center(rig.sunObject);
            Require(controller.Speed == 100 && app.MotionSpeedLabel == "1.00×" && Math.Abs(controller.CycleSeconds - 24) < 1e-6, "Default speed / true closed cycle");
            app.ToggleMotion();
            Require(app.IsMotionPlaying && colliders.All(collider => !collider.enabled), "Start and collider gating");
            foreach (string name in rig.transparentObjects)
            {
                Renderer renderer = named[name].GetComponent<Renderer>();
                Require(renderer.sharedMaterials.All(material => material.color.a < .2f && material.renderQueue == 3000), "Transparent viewing layer");
                Require(!renderer.sharedMaterials.SequenceEqual(materials[name]), "Mutated source material asset");
                Require(renderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off && !renderer.receiveShadows,
                    "Ghost supports still obscure gears with opaque shadows");
            }
            advance.Invoke(controller, new object[] { 1f });
            Require(Math.Abs(controller.GearAngles.Sun - 360) < .001 && Math.Abs(controller.GearAngles.Carrier - 45) < .001
                && Math.Abs(controller.GearAngles.Planet + 60) < .001, "Input, output and planet rates");
            app.ToggleMotion(); float paused = controller.CyclePhase;
            advance.Invoke(controller, new object[] { 2f });
            Require(app.IsMotionActive && !app.IsMotionPlaying && controller.CyclePhase == paused, "Pause advanced motion");
            app.ToggleMotion(); app.ChangeMotionSpeed(25); advance.Invoke(controller, new object[] { 2f });
            Require(app.MotionSpeedLabel == "1.25×" && Math.Abs(controller.CyclePhase - paused - 2.5 / 24) < 1e-6, "Speed continuity");
            app.ChangeMotionSpeed(-1000); Require(controller.Speed == 50 && app.MotionSpeedLabel == "0.50×", "Minimum speed");
            app.ChangeMotionSpeed(1000); Require(controller.Speed == 150 && app.MotionSpeedLabel == "1.50×", "Maximum speed");
            app.ChangeMotionSpeed(-50);
            var drive = new PlanetaryGearKinematics(9, 27, 63);
            for (int sample = 0; sample <= 240; sample++)
            {
                float value = sample / 240f; phase.Invoke(controller, new object[] { value });
                var angles = drive.Evaluate((double)value * 24 * 360);
                Quaternion carrier = Quaternion.AngleAxis((float)(angles.Carrier % 360), axis);
                foreach (OpenTorqueMotionBinding binding in rig.bindings)
                {
                    string name = binding.objectName;
                    double degrees = binding.role == "planet" ? angles.Planet : binding.role == "carrier" ? angles.Carrier : binding.role == "sun" ? angles.Sun : 0;
                    Quaternion expectedRotation = Quaternion.AngleAxis((float)(degrees % 360), axis) * rotations[name];
                    Require(Quaternion.Angle(named[name].rotation, expectedRotation) < .08f, "Gear phase / binding rotation: " + name);
                    if (binding.role == "fixed" || binding.role == "sun")
                        Require(Vector3.ProjectOnPlane(named[name].position - positions[name], axis).magnitude < 1e-6f, "Fixed axis moved");
                    else
                        Require(Vector3.Distance(named[name].position, pivot + carrier * (positions[name] - pivot)) < 1e-6f, "Planet orbit / carrier supports moved apart");
                }
                foreach (string name in rig.planetObjects)
                    Require(Mathf.Abs(Vector3.ProjectOnPlane(Center(name) - pivot, axis).magnitude - .027f) < .0001f, "27mm orbit radius");
            }
            Require(meshes.All(mesh => Vector3.Distance(mesh.transform.position, positions[mesh.name]) < 1e-6f
                && Quaternion.Angle(mesh.transform.rotation, rotations[mesh.name]) < .08f), "Closed-cycle transform drift");
            if (tier == 0) CapturePreview(root, controller, phase);
            app.StopMotion();
            for (int index = 0; index < colliders.Length; index++) Require(colliders[index].enabled == enabled[index], "Collider restore");
            Require(meshes.All(mesh => mesh.transform.parent == parents[mesh.name]
                && Vector3.Distance(mesh.transform.position, positions[mesh.name]) < 1e-6f
                && Quaternion.Angle(mesh.transform.rotation, rotations[mesh.name]) < .02f
                && mesh.GetComponent<Renderer>().sharedMaterials.SequenceEqual(materials[mesh.name])
                && mesh.GetComponent<Renderer>().shadowCastingMode == shadows[mesh.name]
                && mesh.GetComponent<Renderer>().receiveShadows == receiveShadows[mesh.name]), "Source pose / parent / opaque material / shadow restore");
            Require(app.Plan.RemovedCount == 0 && LocalProgressStore.LoadRemovedPartIds(ModelId, app.Plan.Difficulty).Length == 0, "Motion wrote progress");
            app.ToggleMotion(); advance.Invoke(controller, new object[] { 1f }); app.ToggleGlobalExplosion();
            Require(!app.IsMotionActive && app.IsGlobalExplosionActive, "Explosion did not stop motion"); app.FrameWholeModel();
            app.ToggleMotion(); advance.Invoke(controller, new object[] { 1f }); app.SetInteractionViewMode(false);
            Require(!app.IsMotionActive && meshes.All(mesh => mesh.GetComponent<Renderer>().sharedMaterials.SequenceEqual(materials[mesh.name])), "Disassembly transition left transparent materials");
            app.SetInteractionViewMode(true);
            MotionInteractionRegression.Validate(app, controller);
            MotionInteractionRegression.ValidateDisassembly(app, controller);
            Debug.Log("MECH_MASTER_OPENTORQUE_MOTION_OK level=" + app.Plan.Difficulty + " samples=241 ratio=8:1 orbit=27mm input=60rpm continuous,pause,speed,cycle,materials,restore");
        }

        private static void CapturePreview(GameObject root, OpenTorqueMotionController controller, MethodInfo phase)
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/MechMaster/OpenTorqueSource/converted/preview"));
            Directory.CreateDirectory(output);
            Renderer[] renderers = Meshes(root).Select(mesh => mesh.GetComponent<Renderer>()).ToArray();
            var others = root.GetComponentsInChildren<Renderer>().Except(renderers).ToDictionary(renderer => renderer, renderer => renderer.enabled);
            foreach (Renderer renderer in others.Keys) renderer.enabled = false;
            var cameraObject = new GameObject("OpenTorqueValidationCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.1f, .13f, .17f);
            camera.orthographic = true; camera.orthographicSize = .082f;
            // This mechanism is only 110 mm: the default 0.3 m near plane
            // clips the gears in a close-up verification camera.
            camera.nearClipPlane = .001f; camera.farClipPlane = 10;
            var texture = new RenderTexture(1000, 1000, 24); camera.targetTexture = texture;
            Bounds bounds = renderers[0].bounds; foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Vector3 axis = (Vector3)typeof(OpenTorqueMotionController).GetField("axis", Members).GetValue(controller);
            Debug.Log("MECH_MASTER_OPENTORQUE_PREVIEW axis=" + axis + " lights="
                + string.Join(";", UnityEngine.Object.FindObjectsOfType<Light>().Select(light =>
                    light.name + ":" + light.transform.forward + ":" + light.intensity)));
            camera.transform.position = bounds.center - axis * .24f + Vector3.right * .085f + Vector3.up * .045f;
            camera.transform.LookAt(bounds.center);
            RenderTexture previous = RenderTexture.active;
            try
            {
                for (int index = 0; index < 3; index++)
                {
                    phase.Invoke(controller, new object[] { new[] { 0f, .013f, .04f }[index] });
                    camera.Render(); RenderTexture.active = texture;
                    var pixels = new Texture2D(1000, 1000, TextureFormat.RGB24, false);
                    pixels.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0); pixels.Apply();
                    File.WriteAllBytes(Path.Combine(output, "motion-" + index + ".png"), pixels.EncodeToPNG());
                    if (index == 1) File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath,
                        "../Docs/Preview/OpenTorqueMotion.png")), pixels.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(pixels);
                }
            }
            finally
            {
                phase.Invoke(controller, new object[] { 0f }); RenderTexture.active = previous;
                camera.targetTexture = null; texture.Release();
                UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(cameraObject);
                foreach (var item in others) item.Key.enabled = item.Value;
            }
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("OpenTorque import validation: " + message); }
    }
}
