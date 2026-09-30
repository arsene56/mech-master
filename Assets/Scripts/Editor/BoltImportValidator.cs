using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MechMaster.Domain;
using MechMaster.Runtime;
using MechMaster.Runtime.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MechMaster.Editor
{
    // Batch Play Mode validation exercises real input and persistence. Restore
    // every model's preferences only after runtime shutdown has saved state.
    [InitializeOnLoad]
    public static class BoltImportValidator
    {
        private const string ModelId = "robot.odri.bolt.6dof.v1";
        private const string RunKey = "MechMaster.BoltImportValidation";
        private static int tier;

        [Serializable]
        private sealed class Preference
        {
            public string key, value;
            public bool exists, text;
            public int number;
        }

        [Serializable]
        private sealed class Preferences { public List<Preference> values = new List<Preference>(); }

        static BoltImportValidator() { EditorApplication.playModeStateChanged += OnPlayStateChanged; }

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
                Later(() => {
                    MechMasterApp.Instance.SetNarrationEnabled(false);
                    MechMasterApp.Instance.SetModel(ModelId);
                    tier = 0;
                    BeginTier();
                });
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
            SessionState.EraseBool(RunKey);
            SessionState.EraseString(RunKey + ".backup");
            SessionState.EraseInt(RunKey + ".result");
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

        private static void ValidateTier()
        {
            var app = MechMasterApp.Instance;
            var root = GameObject.Find("MechanicalModel_" + ModelId);
            Require(root != null, "Root missing");
            var view = root.GetComponent<MechanicalModelView>();
            Require(view.Parts.Count == new[] { 12, 23, 42 }[tier], "Tier binding count");
            var meshes = root.GetComponentsInChildren<MeshFilter>().Where(mesh => mesh.name.StartsWith("MM_bolt_", StringComparison.Ordinal)).ToArray();
            Require(meshes.Length == 345 && meshes.Select(mesh => mesh.name).Distinct().Count() == 345, "Lost or duplicated FBX nodes");
            Require(meshes.All(mesh => mesh.sharedMesh.vertexCount > 0 && mesh.GetComponent<Renderer>().sharedMaterials.All(material => material != null)), "Missing geometry / material");
            var materials = meshes.SelectMany(mesh => mesh.GetComponent<Renderer>().sharedMaterials).ToArray();
            Require(materials.Any(material => material.name == "Bolt printed transmission" && material.color.b < .45f)
                && materials.Any(material => material.name == "Bolt control PCB" && material.color.g > material.color.r)
                && materials.Any(material => material.name == "Bolt motor stator" && material.color.r > material.color.b),
                "Bolt authored material palette missing");
            Require(Mathf.Abs(Camera.main.GetComponent<OrbitCameraController>().Angles.x - 33f) < .01f,
                "Bolt initial view should show the colored drivetrain side");
            Require(Mathf.Abs(Mathf.DeltaAngle(GameObject.Find("Key Light").transform.eulerAngles.y, 21f)) < .01f,
                "Bolt key light did not follow the initial view");
            Require(app.MotionAvailable && !app.MotionGuide.Contains("刹") && app.MotionSpeedLabel.Contains("×"), "Bolt motion controls");
            var start = meshes.ToDictionary(mesh => mesh.name, mesh => mesh.transform.position);
            ValidateMotion(app, root, meshes);
            app.ToggleGlobalExplosion();
            Require(view.ExplosionTargetCount == app.Plan.Steps.Count && app.Plan.RemovedCount == 0, "Explosion changed progress");
            app.FrameWholeModel();
            Require(view.ExplosionTargetCount == 0, "Explosion did not reset");
            if (tier == 0) CaptureDefaultView();
            MotionInteractionRegression.ValidateGui(app, () => FinishValidatedTier(start), error => {
                Debug.LogException(error);
                EditorApplication.isPlaying = false;
            });
        }

        private static void FinishValidatedTier(Dictionary<string, Vector3> start)
        {
            var app = MechMasterApp.Instance;
            DragOnePart(app);
            app.ToggleMotion();
            Require(!app.IsMotionActive && app.Plan.RemovedCount == 1, "Partial robot started motion");
            string removedId = app.Plan.Steps.Single(step => app.Plan.IsRemoved(step.Id)).Id;
            // Exercise the same restore-and-bind path used at startup. Wait
            // until the old model's deferred destruction has completed.
            typeof(MechMasterApp).GetMethod("CreatePlan", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(app, new object[] { app.Plan.Difficulty, true });
            Later(() => FinishRestoredTier(start, removedId));
        }

        private static void FinishRestoredTier(Dictionary<string, Vector3> start, string removedId)
        {
            var app = MechMasterApp.Instance;
            var root = GameObject.Find("MechanicalModel_" + ModelId);
            var view = root.GetComponent<MechanicalModelView>();
            var meshes = root.GetComponentsInChildren<MeshFilter>()
                .Where(mesh => mesh.name.StartsWith("MM_bolt_", StringComparison.Ordinal)).ToArray();
            Require(app.Plan.RemovedCount == 1 && app.Plan.IsRemoved(removedId)
                && view.Parts[removedId].IsRemoved
                && view.Parts.Values.Count(part => part.IsRemoved) == 1, "Saved partial plan / visual restore");
            foreach (PartDefinition step in app.Plan.Steps.Reverse()) if (!app.Plan.IsRemoved(step.Id)) app.Operate(step.Id);
            view.Refresh(app.Plan, true);
            Require(app.Plan.IsDisassemblyComplete && view.Parts.Values.All(part => part.IsRemoved), "Arbitrary disassembly");
            Require(new HashSet<string>(LocalProgressStore.LoadRemovedPartIds(ModelId, app.Plan.Difficulty)).SetEquals(app.Plan.Steps.Select(step => step.Id)), "Exact removed IDs not saved");
            app.ToggleMode();
            foreach (PartDefinition step in app.Plan.Steps) app.Operate(step.Id);
            view.Refresh(app.Plan, true);
            Require(app.Plan.IsAssemblyComplete && meshes.All(mesh => Vector3.Distance(mesh.transform.position, start[mesh.name]) < 1e-5f), "Reassembly source pose");
            // Motion must retain the assembled rest pose, not the tray pose
            // of the part removed when this model was loaded from its save.
            app.ToggleMotion();
            Require(app.IsMotionPlaying, "Reassembled saved robot cannot play motion");
            typeof(BoltMotionController).GetMethod("Advance", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(root.GetComponent<BoltMotionController>(), new object[] { 1f });
            app.StopMotion();
            Require(meshes.All(mesh => Vector3.Distance(mesh.transform.position, start[mesh.name]) < 1e-5f), "Saved robot captured tray as motion rest pose");
            Debug.Log("MECH_MASTER_BOLT_TIER_OK level=" + app.Plan.Difficulty + " steps=" + view.Parts.Count);
            if (++tier < 3) Later(BeginTier);
            else
            {
                app.SetModel("arm.bcn3d.moveo.v1");
                Later(() => {
                    Require(app.Model.id == "arm.bcn3d.moveo.v1" && app.MotionAvailable, "Moveo switch regression");
                    app.SetModel("bike.hardtail.27_5.2x10.v1");
                    Later(() => {
                        Require(app.Model.id.StartsWith("bike.") && app.MotionAvailable, "Bicycle switch regression");
                        app.ResetCurrentPlan();
                        Require(Mathf.Abs(Camera.main.GetComponent<OrbitCameraController>().Angles.x - 158f) < .01f,
                            "Bicycle initial view must face the opposite side");
                        Require(Mathf.Abs(Mathf.DeltaAngle(Camera.main.transform.eulerAngles.y, 158f)) < .01f,
                            "Bicycle rendered camera did not rotate to the opposite side");
                        Require(Mathf.Abs(Mathf.DeltaAngle(GameObject.Find("Key Light").transform.eulerAngles.y, 146f)) < .01f,
                            "Bicycle key light did not follow the opposite-side view");
                        CaptureBicycleOppositeView(app.Model.id);
                        Later(() => {
                            var bike = GameObject.Find("MechanicalModel_" + app.Model.id);
                            MotionInteractionRegression.ValidateDisassembly(app, bike.GetComponent<BicycleMotionController>());
                            MotionInteractionRegression.ValidateGui(app, () => {
                                Debug.Log("MECH_MASTER_BOLT_RUNTIME_OK switch,bind,ray-pick,drag,explode,save,reassemble,motion,toolbar");
                                SessionState.SetInt(RunKey + ".result", 0);
                                EditorApplication.isPlaying = false;
                            }, error => {
                                Debug.LogException(error);
                                EditorApplication.isPlaying = false;
                            });
                        });
                    });
                });
            }
        }

        private static void ValidateMotion(MechMasterApp app, GameObject root, MeshFilter[] meshes)
        {
            var controller = root.GetComponent<BoltMotionController>();
            var start = meshes.ToDictionary(mesh => mesh.name, mesh => mesh.transform.position);
            var rotations = meshes.ToDictionary(mesh => mesh.name, mesh => mesh.transform.rotation);
            var parents = meshes.ToDictionary(mesh => mesh.name, mesh => mesh.transform.parent);
            var named = meshes.ToDictionary(mesh => mesh.name, mesh => mesh.transform);
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            bool[] enabled = colliders.Select(collider => collider.enabled).ToArray();
            var rig = JsonUtility.FromJson<BoltMotionRig>(Resources.Load<TextAsset>(app.Model.motion.rigResourcePath).text);
            MethodInfo advance = typeof(BoltMotionController).GetMethod("Advance", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo phase = typeof(BoltMotionController).GetMethod("ApplyPhase", BindingFlags.NonPublic | BindingFlags.Instance);
            Require(controller.Speed == 100 && app.MotionSpeedLabel == "1.00×", "Bolt normalized default speed");
            Vector3 Center(string name) => named[name].TransformPoint(named[name].GetComponent<MeshFilter>().sharedMesh.bounds.center);
            Vector3 Axis(BoltJointDefinition joint) => (Center(joint.axisEndObject) - Center(joint.axisStartObject)).normalized;
            float AxisDistance(BoltJointDefinition joint, Vector3 other) => Vector3.ProjectOnPlane(other - Center(joint.pivotObject), Axis(joint)).magnitude;
            app.ToggleMotion();
            Require(app.IsMotionPlaying && colliders.All(collider => !collider.enabled), "Motion start / colliders");
            advance.Invoke(controller, new object[] { 1.8f });
            Require(Mathf.Abs(controller.CyclePhase - .3f) < 1e-5f, "Bolt 1x baseline changed the previous 2x pace");
            Require(Enumerable.Range(0, rig.joints.Length).Where(index => !rig.joints[index].passive)
                .All(index => Mathf.Abs(controller.JointAngles[index]) > 1), "Active joint did not move");
            app.ToggleMotion();
            float paused = controller.CyclePhase;
            advance.Invoke(controller, new object[] { 2f });
            Require(app.IsMotionActive && !app.IsMotionPlaying && controller.CyclePhase == paused, "Paused motion advanced");
            app.ToggleMotion();
            app.ChangeMotionSpeed(25);
            advance.Invoke(controller, new object[] { 1.2f });
            Require(app.MotionSpeedValue == 125 && app.MotionSpeedLabel == "1.25×"
                && Mathf.Abs(controller.CyclePhase - paused - .25f) < 1e-5f, "Speed / phase discontinuity");
            app.ChangeMotionSpeed(-1000);
            Require(app.MotionSpeedValue == 50 && app.MotionSpeedLabel == "0.50×", "Minimum speed");
            float slowPhase = controller.CyclePhase;
            advance.Invoke(controller, new object[] { 1.2f });
            Require(Mathf.Abs(controller.CyclePhase - slowPhase - .1f) < 1e-5f, "Bolt half speed is not relative to the new baseline");
            app.ChangeMotionSpeed(1000);
            Require(app.MotionSpeedValue == 150 && app.MotionSpeedLabel == "1.50×", "Maximum speed");
            float fastPhase = controller.CyclePhase;
            advance.Invoke(controller, new object[] { 1.2f });
            Require(Mathf.Abs(controller.CyclePhase - fastPhase - .3f) < 1e-5f, "Bolt maximum speed is not relative to the new baseline");
            app.ChangeMotionSpeed(-50);
            Require(app.MotionSpeedValue == 100 && app.MotionSpeedLabel == "1.00×", "Baseline speed restore");
            ValidateContinuousMotion(controller, rig);
            for (int sample = 0; sample <= 80; sample++)
            {
                phase.Invoke(controller, new object[] { sample / 80f });
                foreach (BoltMotionBinding binding in rig.bindings.Where(binding => binding.jointId == "fixed"))
                    Require(Vector3.Distance(named[binding.objectName].position, start[binding.objectName]) < 1e-5f, "Fixed body moved");
                foreach (BoltJointDefinition joint in rig.joints.Where(joint => !joint.passive))
                    Require(Vector3.Cross(Center(joint.pivotObject) - Center(joint.axisStartObject), Axis(joint)).magnitude < .0003f, "Pulley left its source support axis");
                foreach (int first in new[] { 0, 4 })
                {
                    Require(Mathf.Abs(AxisDistance(rig.joints[first + 1], Center(rig.joints[first + 2].pivotObject)) - .2f) < .0003f, "Hip-knee axis distance / metres");
                    Require(Mathf.Abs(AxisDistance(rig.joints[first + 2], Center(rig.joints[first + 3].pivotObject)) - .2f) < .0003f, "Knee-ankle axis distance / metres");
                }
            }
            Require(meshes.All(mesh => Vector3.Distance(mesh.transform.position, start[mesh.name]) < 1e-5f), "Closed cycle drift");
            if (tier == 0) CapturePreview(root, controller, phase);
            app.StopMotion();
            for (int index = 0; index < colliders.Length; index++) Require(colliders[index].enabled == enabled[index], "Collider restore");
            Require(meshes.All(mesh => Vector3.Distance(mesh.transform.position, start[mesh.name]) < 1e-5f
                && Quaternion.Angle(mesh.transform.rotation, rotations[mesh.name]) < .02f && mesh.transform.parent == parents[mesh.name]), "Motion restore / parents");
            Require(app.Plan.RemovedCount == 0 && LocalProgressStore.LoadRemovedPartIds(ModelId, app.Plan.Difficulty).Length == 0, "Motion wrote progress");
            app.ToggleMotion(); advance.Invoke(controller, new object[] { 1f }); app.ToggleGlobalExplosion();
            Require(!app.IsMotionActive && app.IsGlobalExplosionActive, "Explosion did not stop motion");
            app.FrameWholeModel(); app.ToggleMotion(); advance.Invoke(controller, new object[] { 1f }); app.SetInteractionViewMode(false);
            Require(!app.IsMotionActive, "Disassembly did not stop motion");
            app.SetInteractionViewMode(true);
            MotionInteractionRegression.Validate(app, controller);
            MotionInteractionRegression.ValidateDisassembly(app, controller);
            Debug.Log("MECH_MASTER_BOLT_MOTION_OK level=" + app.Plan.Difficulty
                + " active=6 passive=2 default=1.00x cycleSeconds=6 axes,dimensions,pause,speed,cycle,restore");
        }

        private static void ValidateContinuousMotion(BoltMotionController controller, BoltMotionRig rig)
        {
            MethodInfo calculate = typeof(BoltMotionController).GetMethod("CalculateMatrices", BindingFlags.NonPublic | BindingFlags.Instance);
            float[] At(float value)
            {
                calculate.Invoke(controller, new object[] { value });
                return controller.JointAngles.ToArray();
            }
            const float epsilon = .0001f;
            foreach (BoltMotionKeyframe frame in rig.keyframes)
            {
                float[] before = At(frame.phase - epsilon), at = At(frame.phase), after = At(frame.phase + epsilon);
                for (int joint = 0; joint < rig.joints.Length; joint++)
                {
                    Require(Mathf.Abs(at[joint] - frame.angles[joint]) < .0001f, "Curve changed an authored pose");
                    Require(Mathf.Abs((at[joint] - before[joint]) / epsilon
                        - (after[joint] - at[joint]) / epsilon) < .5f, "Joint velocity discontinuity at phase " + frame.phase);
                }
            }
            float minimumActiveRate = float.MaxValue;
            var travel = new float[rig.joints.Length];
            for (int sample = 0; sample < 512; sample++)
            {
                float value = sample / 512f;
                float[] before = At(value - epsilon), at = At(value), after = At(value + epsilon);
                float rateSquared = 0;
                for (int joint = 0; joint < rig.joints.Length; joint++)
                {
                    BoltJointDefinition definition = rig.joints[joint];
                    Require(!float.IsNaN(at[joint]) && !float.IsInfinity(at[joint])
                        && at[joint] >= definition.minimumDegrees && at[joint] <= definition.maximumDegrees,
                        "Interpolated angle outside teaching limits: " + definition.id);
                    travel[joint] = Mathf.Max(travel[joint], Mathf.Abs(at[joint]));
                    if (definition.passive) continue;
                    float rate = (after[joint] - before[joint]) / (2 * epsilon);
                    rateSquared += rate * rate;
                }
                for (int side = 0; side < 8; side += 4)
                    Require(Mathf.Abs(at[side + 1] + at[side + 2] + at[side + 3]) < .00001f,
                        "Passive ankle did not compensate interpolated hip/knee pitch");
                float activeRate = Mathf.Sqrt(rateSquared);
                Require(activeRate > 15, "All active joints stalled at phase " + value);
                minimumActiveRate = Mathf.Min(minimumActiveRate, activeRate);
            }
            Require(travel.All(value => value > 1), "Active or passive joint never moved");
            Debug.Log("MECH_MASTER_BOLT_SMOOTH_OK level=" + MechMasterApp.Instance.Plan.Difficulty
                + " samples=512 C1,seam,bounds,passive-compensation no-global-hold minActiveDegreesPerPhase=" + minimumActiveRate);
        }

        private static void CapturePreview(GameObject root, BoltMotionController controller, MethodInfo phase)
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/MechMaster/BoltSource/converted/preview"));
            Directory.CreateDirectory(output);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>().Where(renderer => renderer.name.StartsWith("MM_bolt_")).ToArray();
            var others = root.GetComponentsInChildren<Renderer>().Except(renderers).ToDictionary(renderer => renderer, renderer => renderer.enabled);
            foreach (Renderer renderer in others.Keys) renderer.enabled = false;
            var cameraObject = new GameObject("BoltValidationCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.08f, .11f, .14f);
            camera.orthographic = true;
            camera.orthographicSize = .34f;
            var texture = new RenderTexture(900, 1000, 24);
            camera.targetTexture = texture;
            phase.Invoke(controller, new object[] { 0f });
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            camera.transform.position = bounds.center + new Vector3(-.7f, .4f, -1.1f);
            camera.transform.LookAt(bounds.center);
            RenderTexture previous = RenderTexture.active;
            try
            {
                for (int index = 0; index < 3; index++)
                {
                    phase.Invoke(controller, new object[] { new[] { 0f, .3f, .7f }[index] });
                    camera.Render(); RenderTexture.active = texture;
                    var pixels = new Texture2D(900, 1000, TextureFormat.RGB24, false);
                    pixels.ReadPixels(new Rect(0, 0, 900, 1000), 0, 0); pixels.Apply();
                    File.WriteAllBytes(Path.Combine(output, "pose-" + index + ".png"), pixels.EncodeToPNG());
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

        private static void CaptureDefaultView()
        {
            Camera camera = Camera.main;
            var texture = new RenderTexture(1280, 800, 24);
            var pixels = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture cameraTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
                pixels.Apply();
                string output = Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../Library/MechMaster/BoltSource/converted/preview"));
                Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output, "BoltDefaultView.png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = cameraTarget;
                texture.Release();
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        private static void CaptureBicycleOppositeView(string modelId)
        {
            GameObject root = GameObject.Find("MechanicalModel_" + modelId);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>()
                .Where(renderer => renderer.enabled).ToArray();
            Require(renderers.Length > 0, "Bicycle preview has no renderers");
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);

            var cameraObject = new GameObject("BicycleOppositeViewValidationCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.91f, .96f, .98f);
            camera.orthographic = true;
            camera.aspect = 1.6f;
            Quaternion rotation = Quaternion.Euler(24f, 158f, 0f);
            float halfWidth = 0f;
            float halfHeight = 0f;
            foreach (Renderer renderer in renderers)
            {
                Bounds item = renderer.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = Quaternion.Inverse(rotation) * (item.center
                        + Vector3.Scale(item.extents, new Vector3(
                            (corner & 1) == 0 ? -1 : 1,
                            (corner & 2) == 0 ? -1 : 1,
                            (corner & 4) == 0 ? -1 : 1)) - bounds.center);
                    halfWidth = Mathf.Max(halfWidth, Mathf.Abs(point.x));
                    halfHeight = Mathf.Max(halfHeight, Mathf.Abs(point.y));
                }
            }
            camera.orthographicSize = Mathf.Max(halfHeight, halfWidth / camera.aspect) * 1.12f;
            camera.transform.SetPositionAndRotation(bounds.center
                + rotation * new Vector3(0, 0, -bounds.size.magnitude * 3f), rotation);
            var texture = new RenderTexture(1280, 800, 24);
            var pixels = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
                pixels.Apply();
                string output = Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../Library/MechMaster/BoltSource/converted/preview"));
                Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output, "BicycleOppositeView.png"), pixels.EncodeToPNG());
                Quaternion oldRotation = Quaternion.Euler(24f, -22f, 0f);
                camera.transform.SetPositionAndRotation(bounds.center
                    + oldRotation * new Vector3(0, 0, -bounds.size.magnitude * 3f), oldRotation);
                camera.Render();
                pixels.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, "BicycleOldView.png"), pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                texture.Release();
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static void DragOnePart(MechMasterApp app)
        {
            var input = app.GetComponent<PartInteractionController>();
            PartInteractionController.SetViewMode(false); Physics.SyncTransforms();
            var pixels = new PixelUILayout(Screen.width, Screen.height);
            Rect area = pixels.ToPixels(WorkshopLayout.ModelArea);
            Vector2? hitPoint = null;
            for (float y = area.yMin + 30; y < area.yMax - 30 && !hitPoint.HasValue; y += 20)
                for (float x = area.xMin + 30; x < area.xMax - 30; x += 20)
                {
                    Vector2 point = new Vector2(x, Screen.height - y);
                    if (!PrototypeUI.IsScreenPositionOverPanel(point) && Physics.RaycastAll(Camera.main.ScreenPointToRay(point))
                        .Any(hit => hit.collider.GetComponent<MechanicalPartHitProxy>() != null)) { hitPoint = point; break; }
                }
            Require(hitPoint.HasValue, "No pickable part");
            Invoke(input, "BeginPointer", hitPoint.Value, -1);
            string selected = app.SelectedPart.Id;
            Rect tray = pixels.ToPixels(WorkshopLayout.Tray);
            Vector2? drop = null;
            for (float y = tray.yMin + 10; y < tray.yMax - 10 && !drop.HasValue; y += 10)
                for (float x = tray.xMin + 10; x < tray.xMax - 10; x += 10)
                {
                    Vector2 point = new Vector2(x, Screen.height - y);
                    if (PrototypeUI.TrayAssemblyAtScreenPosition(point) == app.SelectedPart.AssemblyId) { drop = point; break; }
                }
            Require(drop.HasValue, "Matching tray missing");
            Invoke(input, "MovePointer", drop.Value);
            Require(PartInteractionController.IsDraggingPart, "Drag did not start");
            Invoke(input, "EndPointer", drop.Value);
            Require(app.Plan.IsRemoved(selected) && app.Plan.RemovedCount == 1, "Drop did not commit");
            PartInteractionController.SetViewMode(true);
        }

        private static void Invoke(PartInteractionController input, string method, params object[] args) =>
            typeof(PartInteractionController).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, args);

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Bolt import validation: " + message);
        }
    }
}
