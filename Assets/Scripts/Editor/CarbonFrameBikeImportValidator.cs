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
    [InitializeOnLoad]
    public static class CarbonFrameBikeImportValidator
    {
        private const string ModelId = "bike.carbon.full-suspension.v1", Key = "MechMaster.CarbonValidation";
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int tier;
        [Serializable] private sealed class Preference { public string key, value; public bool exists, text; public int number; }
        [Serializable] private sealed class Preferences { public List<Preference> values = new List<Preference>(); }

        static CarbonFrameBikeImportValidator() { EditorApplication.playModeStateChanged += OnPlayStateChanged; }

        public static void ValidateFromCommandLine()
        {
            // A deterministic material reimport also validates first-import ordering.
            foreach (string path in Directory.GetFiles("Assets/Resources/Models/CarbonFrameBike", "*.fbx", SearchOption.AllDirectories))
                AssetDatabase.ImportAsset(path.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
            var backup = new Preferences();
            Capture(backup, "mech_master.v1.modelId", true);
            Capture(backup, "mech_master.v1.difficulty", false);
            Capture(backup, "mech_master.v1.narration", false);
            foreach (MechanicalModelDefinition model in MechanicalModelRegistry.Models)
                foreach (DifficultyLevel level in Enum.GetValues(typeof(DifficultyLevel)))
                    foreach (string field in new[] { "removed", "removedIds", "mode" })
                        Capture(backup, "mech_master.v1.progress." + model.id + "." + level + "." + field, field == "removedIds");
            SessionState.SetString(Key + ".backup", JsonUtility.ToJson(backup));
            SessionState.SetInt(Key + ".result", 1); SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void Capture(Preferences data, string key, bool text) => data.values.Add(new Preference {
            key = key, exists = PlayerPrefs.HasKey(key), text = text, value = text ? PlayerPrefs.GetString(key) : "", number = text ? 0 : PlayerPrefs.GetInt(key) });

        private static void OnPlayStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
                Later(() => { MechMasterApp.Instance.SetNarrationEnabled(false); MechMasterApp.Instance.SetModel(ModelId); tier = 0; BeginTier(); });
            if (state != PlayModeStateChange.EnteredEditMode) return;
            var backup = JsonUtility.FromJson<Preferences>(SessionState.GetString(Key + ".backup", ""));
            foreach (Preference preference in backup.values)
            {
                if (!preference.exists) PlayerPrefs.DeleteKey(preference.key);
                else if (preference.text) PlayerPrefs.SetString(preference.key, preference.value);
                else PlayerPrefs.SetInt(preference.key, preference.number);
            }
            PlayerPrefs.Save();
            int result = SessionState.GetInt(Key + ".result", 1);
            SessionState.EraseBool(Key); SessionState.EraseString(Key + ".backup"); SessionState.EraseInt(Key + ".result");
            EditorApplication.Exit(result);
        }

        private static void Later(Action action) => EditorApplication.delayCall += () => EditorApplication.delayCall += () => {
            try { action(); } catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; }
        };
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Carbon validation: " + message); }
        private static MeshFilter[] Meshes(GameObject root) => root.GetComponentsInChildren<MeshFilter>(true)
            .Where(mesh => mesh.name.StartsWith("MM_carbon_n", StringComparison.Ordinal)).ToArray();

        private static void BeginTier()
        {
            var app = MechMasterApp.Instance;
            app.SetDifficulty((DifficultyLevel)tier); app.ResetCurrentPlan(); Later(ValidateTier);
        }

        private static void ValidateTier()
        {
            var app = MechMasterApp.Instance;
            var root = GameObject.Find("MechanicalModel_" + ModelId);
            Require(root != null, "Missing model");
            var view = root.GetComponent<MechanicalModelView>();
            var meshes = Meshes(root);
            Require(view.Parts.Count == new[] { 14, 34, 51 }[tier] && meshes.Length == 307, "Geometry / tier identity");
            Require(view.Parts.Values.Sum(part => part.Definition.ModelObjectNames.Count) == 307, "Complete tier coverage");
            Require(app.MotionAvailable && app.MotionGuide.Contains("悬架") && !app.MotionHint.Contains("刹"), "Suspension controls");
            foreach (Material material in meshes.SelectMany(mesh => mesh.GetComponent<Renderer>().sharedMaterials))
                Require(material != null && material.name.StartsWith("CarbonBike ") && material.mainTexture == null, "Authored material/map leaked");
            var source = meshes.ToDictionary(mesh => mesh.name, mesh => (mesh.transform.position, mesh.transform.rotation));
            ValidateMotion(app, root, meshes);
            app.ToggleGlobalExplosion();
            Require(view.ExplosionTargetCount == app.Plan.Steps.Count && app.Plan.RemovedCount == 0, "Global explosion progression");
            foreach (var part in view.Parts.Values) part.SetInspectionExplosion(part.InspectionWorldOffset, true, true);
            if (tier == 0) CapturePreview(root, "CarbonFrameBikeEngineExplosion.png");
            app.FrameWholeModel();
            Require(meshes.All(mesh => Vector3.Distance(mesh.transform.position, source[mesh.name].position) < 1e-5f), "Explosion restore");
            var controller = root.GetComponent<CarbonFrameBikeMotionController>();
            MotionInteractionRegression.Validate(app, controller);
            MotionInteractionRegression.ValidateDisassembly(app, controller);
            MotionInteractionRegression.ValidateGui(app, () => SaveReload(source), Fail);
        }

        private static void Fail(Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; }

        private static void ValidateMotion(MechMasterApp app, GameObject root, MeshFilter[] meshes)
        {
            var controller = root.GetComponent<CarbonFrameBikeMotionController>();
            var rig = JsonUtility.FromJson<CarbonSuspensionRig>(Resources.Load<TextAsset>(app.Model.motion.rigResourcePath).text);
            var all = root.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            var source = meshes.ToDictionary(m => m.name, m => (m.transform.position, m.transform.rotation, m.transform.parent));
            var renderers = meshes.ToDictionary(m => m.name, m => m.GetComponent<Renderer>().enabled);
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            bool[] enabled = colliders.Select(c => c.enabled).ToArray();
            MethodInfo phase = typeof(CarbonFrameBikeMotionController).GetMethod("ApplyPhase", Members);
            MethodInfo advance = typeof(CarbonFrameBikeMotionController).GetMethod("Advance", Members);
            Require(Mathf.Abs(Vector3.Distance(all[rig.shockUpperAnchor].position, all[rig.shockLowerAnchor].position) - .2f) < .00015f,
                "FBX metre scale / shock eyes");
            Require(app.MotionSpeedLabel == "1.00×" && controller.Speed == 100, "Default speed");
            if (tier == 0) CapturePreview(root, "CarbonFrameBikeEngine.png");
            app.ToggleMotion(); Require(app.IsMotionPlaying && colliders.All(c => !c.enabled), "Motion start / collision gate");
            var chain = rig.bindings.Single(b => b.objectName == "MM_carbon_n0594_chain");
            Require(chain.role == "fixed" && all[chain.objectName].GetComponent<Renderer>().enabled,
                "Source chain should remain visible during suspension motion");
            MeshFilter chainFilter = all[chain.objectName].GetComponent<MeshFilter>();
            Vector3[] chainRestVertices = chainFilter.sharedMesh.vertices;
            Vector3 chainFront = all["MM_carbon_n0572_chainring"].position;
            Vector3 chainRear = all[rig.rearWheelAnchor].position;
            Vector3 chainSpan = chainRear - chainFront;
            foreach (var binding in rig.bindings.Where(b => b.role == "hidden"))
                Require(!all[binding.objectName].GetComponent<Renderer>().enabled, "Flexible line not hidden");
            advance.Invoke(controller, new object[] { .5f });
            Require(Mathf.Abs(controller.CyclePhase - .25f) < 1e-6f && Mathf.Abs(controller.RearDegrees - 4) < .0001f,
                "Default actual tempo");
            app.ToggleMotion(); float paused = controller.CyclePhase;
            advance.Invoke(controller, new object[] { 2f }); Require(controller.CyclePhase == paused && !app.IsMotionPlaying, "Pause progression");
            app.ChangeMotionSpeed(25); Require(app.MotionSpeedLabel == "1.25×", "Speed label");
            app.ToggleMotion(); advance.Invoke(controller, new object[] { .4f });
            Require(Mathf.Abs(controller.CyclePhase - .5f) < 1e-6f, "Resume / actual speed");
            if (tier == 0) CapturePreview(root, "CarbonFrameBikeEngineSuspension.png");
            Vector3 pivot = all[rig.rearPivotAnchor].position;
            Vector3 upper = all[rig.shockUpperAnchor].position, lower = all[rig.shockLowerAnchor].position;
            for (int sample = 0; sample <= 256; sample++)
            {
                float p = sample / 256f;
                phase.Invoke(controller, new object[] { p });
                float fraction = (float)SuspensionTeachingCycle.CompressionFraction(p);
                Require(Mathf.Abs(controller.ForkCompressionM - .045f * fraction) < 1e-7f
                    && Mathf.Abs(controller.RearDegrees - 8 * fraction) < 1e-6f, "Continuous excursion");
                Vector3 moving = root.transform.TransformPoint(controller.MovingShockLower);
                Require(controller.ShockCompressionM >= -1e-6f && controller.ShockCompressionM < .035f
                    && Mathf.Abs(Vector3.Distance(upper, moving) + controller.ShockCompressionM - rig.shockEyeDistanceM) < 1e-6f,
                    "Shock eye constraint / slide");
                foreach (var binding in rig.bindings)
                {
                    Transform item = all[binding.objectName];
                    var start = source[item.name];
                    if (binding.role == "fixed" || binding.role == "hidden")
                        Require(Vector3.Distance(item.position, start.position) < 1e-6f, "Fixed geometry drift");
                    if (binding.role == "rear") Require(Mathf.Abs(Vector3.Distance(item.position, pivot) - Vector3.Distance(start.position, pivot)) < 1e-6f, "Swingarm rigidity");
                    if (binding.role == "front_lower") Require(Mathf.Abs(Vector3.Distance(item.position, start.position) - controller.ForkCompressionM) < 1e-6f, "Front lower linkage");
                    Require(item.parent == start.parent, "Motion changed hierarchy");
                }
            }
            phase.Invoke(controller, new object[] { .5f });
            Require(controller.ShockCompressionM > .01f, "Rear compression not visible");
            Vector3[] chainCompressedVertices = chainFilter.sharedMesh.vertices;
            int frontCount = 0, rearCount = 0;
            float frontMaximum = 0f, rearMaximum = 0f;
            for (int i = 0; i < chainRestVertices.Length; i++)
            {
                Vector3 sourcePoint = chainFilter.transform.TransformPoint(chainRestVertices[i]);
                float along = Vector3.Dot(sourcePoint - chainFront, chainSpan) / chainSpan.sqrMagnitude;
                float displacement = Vector3.Distance(sourcePoint,
                    chainFilter.transform.TransformPoint(chainCompressedVertices[i]));
                if (along < .1f) { frontCount++; frontMaximum = Mathf.Max(frontMaximum, displacement); }
                if (along > .9f) { rearCount++; rearMaximum = Mathf.Max(rearMaximum, displacement); }
            }
            Require(frontCount > 0 && rearCount > 0 && frontMaximum < 1e-5f && rearMaximum > .02f,
                "Chain front anchor or rear-wheel following failed");
            app.StopMotion(); controller.SetSpeed(100);
            Vector3[] chainRestoredVertices = chainFilter.sharedMesh.vertices;
            Require(chainRestoredVertices.Length == chainRestVertices.Length
                && chainRestoredVertices.Select((point, i) => Vector3.Distance(point, chainRestVertices[i])).Max() < 1e-6f,
                "Stop did not restore source chain shape");
            Require(!app.IsMotionActive && meshes.All(m => Vector3.Distance(m.transform.position, source[m.name].position) < 1e-6f
                && Quaternion.Angle(m.transform.rotation, source[m.name].rotation) < .04f && m.GetComponent<Renderer>().enabled == renderers[m.name]), "Stop source pose / visible lines");
            Require(colliders.Select((c, i) => c.enabled == enabled[i]).All(value => value), "Collider restore");
            Debug.Log("MECH_MASTER_CARBON_MOTION_OK level=" + app.Plan.Difficulty + " source-axes,eyes,continuous,pause,speed,renderer-and-collider-restore");
        }

        private static void SaveReload(Dictionary<string, (Vector3 position, Quaternion rotation)> source)
        {
            var app = MechMasterApp.Instance;
            string id = app.Plan.Steps.Last().Id;
            app.Operate(id);
            app.ToggleMotion(); Require(!app.IsMotionActive && app.Plan.RemovedCount == 1, "Partial model started demonstration");
            typeof(MechMasterApp).GetMethod("CreatePlan", Members).Invoke(app, new object[] { app.Plan.Difficulty, true });
            Later(() => {
                var root = GameObject.Find("MechanicalModel_" + ModelId);
                var view = root.GetComponent<MechanicalModelView>();
                Require(app.Plan.IsRemoved(id) && app.Plan.RemovedCount == 1 && view.Parts[id].IsRemoved, "Exact progress reload");
                foreach (PartDefinition step in app.Plan.Steps.Reverse()) if (!app.Plan.IsRemoved(step.Id)) app.Operate(step.Id);
                view.Refresh(app.Plan, true); Require(app.Plan.IsDisassemblyComplete, "Arbitrary dismantle");
                Require(new HashSet<string>(LocalProgressStore.LoadRemovedPartIds(ModelId, app.Plan.Difficulty)).SetEquals(app.Plan.Steps.Select(p => p.Id)), "Saved identity set");
                app.ToggleMode(); foreach (PartDefinition step in app.Plan.Steps) app.Operate(step.Id);
                view.Refresh(app.Plan, true); Require(app.Plan.IsAssemblyComplete, "Complete assembly");
                app.ToggleMotion(); Require(app.IsMotionPlaying, "Loaded removed pose polluted rig");
                typeof(CarbonFrameBikeMotionController).GetMethod("Advance", Members).Invoke(root.GetComponent<CarbonFrameBikeMotionController>(), new object[] { .8f });
                app.StopMotion();
                Require(Meshes(root).All(m => Vector3.Distance(m.transform.position, source[m.name].position) < 1e-5f), "Reload / motion source pose");
                Debug.Log("MECH_MASTER_CARBON_TIER_OK level=" + app.Plan.Difficulty + " steps=" + view.Parts.Count + " meshes=307");
                if (++tier < 3) Later(BeginTier); else SwitchSmoke(0);
            });
        }

        private static void SwitchSmoke(int index)
        {
            string[] ids = { "bike.hardtail.27_5.2x10.v1", "arm.bcn3d.moveo.v1", "robot.odri.bolt.6dof.v1", "gearbox.opentorque.planetary.v1", ModelId };
            var app = MechMasterApp.Instance; app.SetModel(ids[index]); app.ResetCurrentPlan();
            Later(() => {
                Require(app.Model.id == ids[index] && app.MotionAvailable, "Existing model binding");
                ValidateViewLockedLighting();
                var root = GameObject.Find("MechanicalModel_" + app.Model.id);
                var controller = root.GetComponents<Component>().OfType<IMechanicalMotionController>().Single();
                var source = root.GetComponentsInChildren<MeshFilter>().ToDictionary(m => m.transform, m => (m.transform.position, m.transform.rotation));
                app.ToggleMotion(); Require(app.IsMotionPlaying, "Switch/start");
                controller.GetType().GetMethod("Advance", Members).Invoke(controller, new object[] { .6f });
                app.ToggleMotion(); Require(!app.IsMotionPlaying && app.IsMotionActive, "Switch/pause");
                app.ToggleMotion(); app.StopMotion();
                Require(source.All(p => Vector3.Distance(p.Key.position, p.Value.position) < 1e-5f && Quaternion.Angle(p.Key.rotation, p.Value.rotation) < .08f), "Switch/restore");
                app.ToggleGlobalExplosion(); Require(root.GetComponent<MechanicalModelView>().ExplosionTargetCount == app.Plan.Steps.Count, "Switch/explosion");
                app.FrameWholeModel(); Debug.Log("MECH_MASTER_CARBON_SWITCH_OK model=" + app.Model.id);
                if (index + 1 < ids.Length) SwitchSmoke(index + 1);
                else { Debug.Log("MECH_MASTER_CARBON_RUNTIME_OK tiers,source,motion,pointer,toolbar,save,switch"); SessionState.SetInt(Key + ".result", 0); EditorApplication.isPlaying = false; }
            });
        }

        private static void ValidateViewLockedLighting()
        {
            Camera camera = Camera.main;
            OrbitCameraController orbit = camera.GetComponent<OrbitCameraController>();
            Light key = GameObject.Find("Key Light")?.GetComponent<Light>();
            Light fill = GameObject.Find("Fill Light")?.GetComponent<Light>();
            Require(key != null && fill != null
                && key.transform.parent == camera.transform && fill.transform.parent == camera.transform,
                "View-locked studio lights missing");
            Quaternion referenceCamera = Quaternion.Euler(14f, -22f, 0f);
            Quaternion keyLocal = Quaternion.Inverse(referenceCamera) * Quaternion.Euler(42f, -34f, 0f);
            Quaternion fillLocal = Quaternion.Inverse(referenceCamera) * Quaternion.Euler(25f, 145f, 0f);
            Quaternion keyBefore = key.transform.rotation;
            float scale = new PixelUILayout(Screen.width, Screen.height).Scale;
            orbit.Rotate(new Vector2(1000f * scale, -300f * scale));
            Require(Quaternion.Angle(keyBefore, key.transform.rotation) > 150f,
                "Key light did not respond to the orbit");
            Require(Quaternion.Angle(key.transform.rotation, camera.transform.rotation * keyLocal) < .01f
                && Quaternion.Angle(fill.transform.rotation, camera.transform.rotation * fillLocal) < .01f,
                "Studio lights no longer face the viewer");
            orbit.Rotate(new Vector2(1000f * scale, 600f * scale));
            Require(Quaternion.Angle(key.transform.rotation, camera.transform.rotation * keyLocal) < .01f
                && Quaternion.Angle(fill.transform.rotation, camera.transform.rotation * fillLocal) < .01f,
                "Studio lights lost alignment after reverse pitch/orbit");
            orbit.FrameWholeModel();
        }

        private static void CapturePreview(GameObject root, string filename)
        {
            var meshes = Meshes(root).Where(m => m.GetComponent<Renderer>().enabled).ToArray();
            var cameraObject = new GameObject("Carbon validation camera");
            var camera = cameraObject.AddComponent<Camera>(); camera.CopyFrom(Camera.main);
            var texture = new RenderTexture(1600, 1000, 24); texture.Create();
            Bounds bounds = meshes[0].GetComponent<Renderer>().bounds;
            foreach (var mesh in meshes) bounds.Encapsulate(mesh.GetComponent<Renderer>().bounds);
            camera.orthographic = true; camera.aspect = 1.6f;
            Quaternion rotation = Quaternion.Euler(14, 24, 0);
            float halfWidth = 0, halfHeight = 0;
            foreach (var mesh in meshes)
            {
                Bounds b = mesh.GetComponent<Renderer>().bounds;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 point = Quaternion.Inverse(rotation) * (b.center + Vector3.Scale(b.extents,
                        new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)) - bounds.center);
                    halfWidth = Mathf.Max(halfWidth, Mathf.Abs(point.x)); halfHeight = Mathf.Max(halfHeight, Mathf.Abs(point.y));
                }
            }
            camera.orthographicSize = Mathf.Max(halfHeight, halfWidth / camera.aspect) * 1.14f;
            camera.transform.SetPositionAndRotation(bounds.center + rotation * new Vector3(0, 0, -bounds.size.magnitude * 3), rotation);
            RenderTexture previous = RenderTexture.active;
            var pixels = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); pixels.Apply();
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Preview")); Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, filename), pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous; texture.Release();
                UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
