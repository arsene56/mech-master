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
    // Exercises the imported model through the same runtime input and save paths.
    // Restore preferences after leaving Play, when runtime shutdown has finished.
    [InitializeOnLoad]
    public static class MoveoImportValidator
    {
        private const string ModelId = "arm.bcn3d.moveo.v1";
        private const string RunKey = "MechMaster.MoveoImportValidation";
        private const string BackupKey = RunKey + ".preferences";
        private const string ResultKey = RunKey + ".result";
        private static int tier;

        [Serializable]
        private sealed class Preference
        {
            public string key;
            public bool exists;
            public bool text;
            public string stringValue;
            public int intValue;
        }

        [Serializable]
        private sealed class Preferences
        {
            public List<Preference> values = new List<Preference>();
        }

        static MoveoImportValidator()
        {
            EditorApplication.playModeStateChanged += OnPlayStateChanged;
        }

        public static void ValidateFromCommandLine()
        {
            var backup = new Preferences();
            Capture(backup, "mech_master.v1.modelId", true);
            Capture(backup, "mech_master.v1.difficulty", false);
            Capture(backup, "mech_master.v1.narration", false);
            foreach (MechanicalModelDefinition model in MechanicalModelRegistry.Models)
                foreach (DifficultyLevel level in Enum.GetValues(typeof(DifficultyLevel)))
                    foreach (string field in new[] { "removed", "removedIds", "mode" })
                        Capture(backup, "mech_master.v1.progress." + model.id + "." + level + "." + field,
                            field == "removedIds");
            SessionState.SetString(BackupKey, JsonUtility.ToJson(backup));
            SessionState.SetInt(ResultKey, 1);
            SessionState.SetBool(RunKey, true);
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void Capture(Preferences backup, string key, bool text)
        {
            backup.values.Add(new Preference { key = key, exists = PlayerPrefs.HasKey(key), text = text,
                stringValue = text ? PlayerPrefs.GetString(key) : string.Empty,
                intValue = text ? 0 : PlayerPrefs.GetInt(key) });
        }

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
            Preferences backup = JsonUtility.FromJson<Preferences>(SessionState.GetString(BackupKey, ""));
            foreach (Preference preference in backup.values)
            {
                if (!preference.exists) PlayerPrefs.DeleteKey(preference.key);
                else if (preference.text) PlayerPrefs.SetString(preference.key, preference.stringValue);
                else PlayerPrefs.SetInt(preference.key, preference.intValue);
            }
            PlayerPrefs.Save();
            int result = SessionState.GetInt(ResultKey, 1);
            SessionState.EraseBool(RunKey);
            SessionState.EraseString(BackupKey);
            SessionState.EraseInt(ResultKey);
            EditorApplication.Exit(result);
        }

        private static void Later(Action action)
        {
            EditorApplication.delayCall += () => EditorApplication.delayCall += () => {
                try { action(); }
                catch (Exception error)
                {
                    Debug.LogException(error);
                    EditorApplication.isPlaying = false;
                }
            };
        }

        private static void BeginTier()
        {
            var app = MechMasterApp.Instance;
            app.SetDifficulty((DifficultyLevel)tier);
            app.ResetCurrentPlan();
            Later(ValidateTier);
        }

        private static void ValidateTier()
        {
            var app = MechMasterApp.Instance;
            var root = GameObject.Find("MechanicalModel_" + ModelId);
            Require(root != null, "Moveo root missing");
            var view = root.GetComponent<MechanicalModelView>();
            Require(view.Parts.Count == new[] { 9, 20, 42 }[tier], "Tier binding count");
            Require(app.MotionAvailable && root.GetComponent<MoveoMotionController>().IsReady,
                "Arm motion rig missing");
            Require(!app.MotionGuide.Contains("刹") && app.MotionSpeedLabel.Contains("×"),
                "Arm displays bicycle controls");
            MeshFilter[] meshes = root.GetComponentsInChildren<MeshFilter>()
                .Where(mesh => mesh.name.StartsWith("MM_moveo_", StringComparison.Ordinal)).ToArray();
            Require(meshes.Length == 366, "Independent FBX nodes were lost");
            Require(meshes.Select(mesh => mesh.name).Distinct().Count() == 366, "Duplicate FBX names");
            Require(meshes.All(mesh => mesh.sharedMesh.vertexCount > 0
                && mesh.GetComponent<Renderer>().sharedMaterials.All(material => material != null)),
                "Missing geometry or material");
            Vector3 wood = meshes.First(mesh => mesh.name.Contains("base_fusta")).sharedMesh.bounds.size;
            Require(Mathf.Abs(Mathf.Max(wood.x, wood.y, wood.z) - .55f) < .003f, "Metre unit / base scale");
            var positions = meshes.ToDictionary(mesh => mesh.name, mesh => mesh.transform.position);

            app.ToggleGlobalExplosion();
            Require(view.ExplosionTargetCount == app.Plan.Steps.Count && app.Plan.RemovedCount == 0,
                "Explosion changed progress or missed a group");
            app.FrameWholeModel();
            Require(view.ExplosionTargetCount == 0, "Explosion reset failed");
            ValidateMotion(app, root, meshes);
            DragOnePart(app);
            app.ToggleMotion();
            Require(!app.IsMotionActive && app.Plan.RemovedCount == 1, "A partial arm started motion");
            foreach (PartDefinition step in app.Plan.Steps.Reverse())
                if (!app.Plan.IsRemoved(step.Id)) app.Operate(step.Id);
            view.Refresh(app.Plan, true);
            Require(app.Plan.IsDisassemblyComplete && view.Parts.Values.All(part => part.IsRemoved),
                "Arbitrary full disassembly failed");
            Require(LocalProgressStore.LoadRemovedPartIds(ModelId, app.Plan.Difficulty).Length == app.Plan.Steps.Count,
                "Exact removed-ID save failed");
            app.ToggleMode();
            foreach (PartDefinition step in app.Plan.Steps) app.Operate(step.Id);
            view.Refresh(app.Plan, true);
            Require(app.Plan.IsAssemblyComplete && view.Parts.Values.All(part => !part.IsRemoved),
                "Arbitrary reassembly failed");
            Require(meshes.All(mesh => Vector3.Distance(mesh.transform.position, positions[mesh.name]) < 1e-5f),
                "Reassembly changed source positions");
            Debug.Log("MECH_MASTER_MOVEO_TIER_OK level=" + app.Plan.Difficulty + " steps=" + view.Parts.Count);
            if (++tier < 3) Later(BeginTier);
            else
            {
                app.SetModel("bike.hardtail.27_5.2x10.v1");
                Later(() => {
                    Require(MechMasterApp.Instance.Model.id.StartsWith("bike.")
                        && MechMasterApp.Instance.MotionAvailable, "Return to bicycle failed");
                    Debug.Log("MECH_MASTER_MOVEO_RUNTIME_OK switch,bind,ray-pick,drag,explode,save,reassemble");
                    SessionState.SetInt(ResultKey, 0);
                    EditorApplication.isPlaying = false;
                });
            }
        }

        private static void ValidateMotion(MechMasterApp app, GameObject root, MeshFilter[] meshes)
        {
            var controller = root.GetComponent<MoveoMotionController>();
            var start = meshes.ToDictionary(mesh => mesh.name, mesh => mesh.transform.position);
            var rotations = meshes.ToDictionary(mesh => mesh.name, mesh => mesh.transform.rotation);
            var parents = meshes.ToDictionary(mesh => mesh.name, mesh => mesh.transform.parent);
            var named = meshes.ToDictionary(mesh => mesh.name, mesh => mesh.transform);
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            bool[] enabled = colliders.Select(collider => collider.enabled).ToArray();
            MoveoMotionRig rig = JsonUtility.FromJson<MoveoMotionRig>(Resources.Load<TextAsset>(app.Model.motion.rigResourcePath).text);
            MethodInfo advance = typeof(MoveoMotionController).GetMethod("Advance", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo phase = typeof(MoveoMotionController).GetMethod("ApplyPhase", BindingFlags.NonPublic | BindingFlags.Instance);
            app.ToggleMotion();
            Require(app.IsMotionPlaying && colliders.All(collider => !collider.enabled), "Motion did not start / disable picking");
            var jaw = meshes.First(mesh => mesh.name.StartsWith("MM_moveo_gripper_left_"));
            advance.Invoke(controller, new object[] { 4f });
            Require(controller.JointAngles.All(angle => Mathf.Abs(angle) > 1)
                && Vector3.Distance(jaw.transform.position, start[jaw.name]) > .02f, "Serial joints did not articulate");
            app.ToggleMotion();
            Vector3 paused = jaw.transform.position;
            float pausedPhase = controller.CyclePhase;
            advance.Invoke(controller, new object[] { 2f });
            Require(app.IsMotionActive && !app.IsMotionPlaying && controller.CyclePhase == pausedPhase
                && Vector3.Distance(jaw.transform.position, paused) < 1e-6f, "Paused arm moved");
            app.ToggleMotion();
            app.ChangeMotionSpeed(25);
            Require(app.MotionSpeedValue == 125, "Arm speed control");
            advance.Invoke(controller, new object[] { 2f });
            Require(Mathf.Abs(controller.CyclePhase - pausedPhase - .125f) < 1e-5f, "Speed changed the phase discontinuously");
            app.ChangeMotionSpeed(-1000);
            Require(app.MotionSpeedValue == 50, "Arm minimum speed");
            app.ChangeMotionSpeed(1000);
            Require(app.MotionSpeedValue == 150, "Arm maximum speed");
            app.ChangeMotionSpeed(-50);

            string[] shaftPrefixes = { "MM_moveo_smooth_bar_8mm_x_140mm_", "MM_moveo_smooth_bar_8mm_x_121mm_",
                "MM_moveo_smooth_bar_8mm_x_50mm_", "MM_moveo_barra_llisa_8mm_x_80mm_" };
            Vector3 center(Transform item) => item.TransformPoint(item.GetComponent<MeshFilter>().sharedMesh.bounds.center);
            var lengths = new float[2, 3];
            MoveoJawDefinition[] jaws = { rig.gripper.left, rig.gripper.right };
            phase.Invoke(controller, new object[] { 0f });
            for (int side = 0; side < 2; side++)
            {
                MoveoJawDefinition definition = jaws[side];
                lengths[side, 0] = Vector3.Distance(center(named[definition.driverPivotObject]), center(named[definition.driverTipObject]));
                lengths[side, 1] = Vector3.Distance(center(named[definition.followerPivotObject]), center(named[definition.followerTipObject]));
                lengths[side, 2] = Vector3.Distance(center(named[definition.driverTipObject]), center(named[definition.followerTipObject]));
            }
            for (int sample = 0; sample <= 80; sample++)
            {
                phase.Invoke(controller, new object[] { sample / 80f });
                foreach (MoveoMotionBinding binding in rig.bindings.Where(binding => binding.jointId == "fixed"))
                    Require(Vector3.Distance(named[binding.objectName].position, start[binding.objectName]) < 1e-5f,
                        "Fixed base / controller moved: " + binding.objectName);
                for (int joint = 1; joint < 5; joint++)
                {
                    MoveoJointDefinition definition = rig.joints[joint];
                    Vector3 first = center(named[definition.axisStartObject]), last = center(named[definition.axisEndObject]);
                    Vector3 shaft = center(meshes.First(mesh => mesh.name.StartsWith(shaftPrefixes[joint - 1])).transform);
                    Require(Vector3.Cross(shaft - first, (last - first).normalized).magnitude < .001f,
                        "Shaft left its supporting bearings: " + definition.id);
                }
                for (int side = 0; side < 2; side++)
                {
                    MoveoJawDefinition definition = jaws[side];
                    float[] actual = { Vector3.Distance(center(named[definition.driverPivotObject]), center(named[definition.driverTipObject])),
                        Vector3.Distance(center(named[definition.followerPivotObject]), center(named[definition.followerTipObject])),
                        Vector3.Distance(center(named[definition.driverTipObject]), center(named[definition.followerTipObject])) };
                    for (int link = 0; link < 3; link++)
                        Require(Mathf.Abs(actual[link] - lengths[side, link]) < .0001f, "Four-bar link detached");
                }
            }
            Require(controller.GripperDegrees == 0
                && meshes.All(mesh => Vector3.Distance(mesh.transform.position, start[mesh.name]) < 1e-5f),
                "Closed cycle accumulated drift");
            if (tier == 0 && Environment.GetCommandLineArgs().Contains("-moveoMotionPreview"))
                CapturePreview(root, controller, phase);
            app.StopMotion();
            for (int index = 0; index < colliders.Length; index++)
                Require(colliders[index].enabled == enabled[index], "Collider state was lost");
            Require(meshes.All(mesh => Vector3.Distance(mesh.transform.position, start[mesh.name]) < 1e-5f
                && Quaternion.Angle(mesh.transform.rotation, rotations[mesh.name]) < .02f
                && mesh.transform.parent == parents[mesh.name]), "Motion changed original poses / parents");
            Require(app.Plan.RemovedCount == 0 && LocalProgressStore.LoadRemovedPartIds(ModelId, app.Plan.Difficulty).Length == 0,
                "Motion wrote disassembly progress");
            // Every transition away from the motion view must restore geometry.
            app.ToggleMotion();
            advance.Invoke(controller, new object[] { 1f });
            app.ToggleGlobalExplosion();
            Require(!app.IsMotionActive && app.IsGlobalExplosionActive, "Explosion did not end motion");
            app.FrameWholeModel();
            app.ToggleMotion();
            advance.Invoke(controller, new object[] { 1f });
            app.SetInteractionViewMode(false);
            Require(!app.IsMotionActive, "Disassembly view did not end motion");
            app.SetInteractionViewMode(true);
            Debug.Log("MECH_MASTER_MOVEO_MOTION_OK level=" + app.Plan.Difficulty
                + " axes=5 fourbars=2 pause,speed,cycle,restore,transitions");
        }

        private static void CapturePreview(GameObject root, MoveoMotionController controller, MethodInfo phase)
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Preview/MoveoMotion"));
            Directory.CreateDirectory(output);
            var other = root.GetComponentsInChildren<Renderer>().Where(renderer => !renderer.name.StartsWith("MM_moveo_"))
                .ToDictionary(renderer => renderer, renderer => renderer.enabled);
            foreach (Renderer renderer in other.Keys) renderer.enabled = false;
            GameObject cameraObject = new GameObject("MoveoValidationCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.08f, .11f, .14f);
            camera.orthographic = true;
            camera.orthographicSize = .58f;
            var texture = new RenderTexture(900, 800, 24);
            camera.targetTexture = texture;
            phase.Invoke(controller, new object[] { 0f });
            Bounds bounds = root.GetComponentsInChildren<MeshFilter>().Where(mesh => mesh.name.StartsWith("MM_moveo_"))
                .First().GetComponent<Renderer>().bounds;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>().Where(renderer => renderer.name.StartsWith("MM_moveo_")))
                bounds.Encapsulate(renderer.bounds);
            camera.transform.position = bounds.center + new Vector3(-1.2f, .8f, -1.7f);
            camera.transform.LookAt(bounds.center);
            RenderTexture previous = RenderTexture.active;
            try
            {
                for (int frame = 0; frame < 40; frame++)
                {
                    phase.Invoke(controller, new object[] { frame / 40f });
                    camera.Render();
                    RenderTexture.active = texture;
                    var pixels = new Texture2D(900, 800, TextureFormat.RGB24, false);
                    pixels.ReadPixels(new Rect(0, 0, 900, 800), 0, 0);
                    pixels.Apply();
                    File.WriteAllBytes(Path.Combine(output, "frame-" + frame.ToString("D2") + ".png"), pixels.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(pixels);
                }
            }
            finally
            {
                phase.Invoke(controller, new object[] { 0f });
                RenderTexture.active = previous;
                camera.targetTexture = null;
                texture.Release();
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                foreach (var item in other) item.Key.enabled = item.Value;
            }
        }

        private static void DragOnePart(MechMasterApp app)
        {
            var input = app.GetComponent<PartInteractionController>();
            PartInteractionController.SetViewMode(false);
            Physics.SyncTransforms();
            var pixels = new PixelUILayout(Screen.width, Screen.height);
            Rect area = pixels.ToPixels(WorkshopLayout.ModelArea);
            Vector2? hitPoint = null;
            for (float y = area.yMin + 30; y < area.yMax - 30 && !hitPoint.HasValue; y += 24)
                for (float x = area.xMin + 30; x < area.xMax - 30; x += 24)
                {
                    Vector2 point = new Vector2(x, Screen.height - y);
                    if (!PrototypeUI.IsScreenPositionOverPanel(point)
                        && Physics.RaycastAll(Camera.main.ScreenPointToRay(point)).Any(hit =>
                            hit.collider.GetComponent<MechanicalPartHitProxy>() != null))
                    { hitPoint = point; break; }
                }
            Require(hitPoint.HasValue, "No pickable part in the workspace");
            Invoke(input, "BeginPointer", hitPoint.Value, -1);
            string selected = app.SelectedPart.Id;
            Rect tray = pixels.ToPixels(WorkshopLayout.Tray);
            Vector2? drop = null;
            for (float y = tray.yMin + 10; y < tray.yMax - 10 && !drop.HasValue; y += 10)
                for (float x = tray.xMin + 10; x < tray.xMax - 10; x += 10)
                {
                    Vector2 point = new Vector2(x, Screen.height - y);
                    if (PrototypeUI.TrayAssemblyAtScreenPosition(point) == app.SelectedPart.AssemblyId)
                    { drop = point; break; }
                }
            Require(drop.HasValue, "No matching tray");
            Invoke(input, "MovePointer", drop.Value);
            Require(PartInteractionController.IsDraggingPart, "Pointer failed to begin dragging");
            Invoke(input, "EndPointer", drop.Value);
            Require(app.Plan.IsRemoved(selected) && app.Plan.RemovedCount == 1, "Tray drop did not remove selected part");
            PartInteractionController.SetViewMode(true);
        }

        private static void Invoke(PartInteractionController input, string method, params object[] args) =>
            typeof(PartInteractionController).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(input, args);

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Moveo import validation: " + message);
        }
    }
}
