using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using MechMaster.Domain;
using MechMaster.Runtime;
using MechMaster.Runtime.UI;
using UnityEditor;
using UnityEngine;

namespace MechMaster.Editor
{
    // No physics tick or test-side transform sync between restoring motion,
    // exploding a real picked part and picking it again at its displayed pose.
    public static class MotionInteractionRegression
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int queuedGuiEvents;

        public static void ValidateGui(MechMasterApp app, Action complete, Action<Exception> failed)
        {
            // Queue native game events across frames. This covers the actual
            // toolbar and OnGUI dispatch, not just calls to the app's methods.
            var window = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            window.Focus();
            int receivedGuiEvents = 0;
            Action<Event> acknowledge = pointer => {
                if (pointer.rawType != EventType.MouseDown && pointer.rawType != EventType.MouseUp
                    && pointer.rawType != EventType.MouseDrag) return;
                receivedGuiEvents++;
                Debug.Log("MECH_MASTER_GUI_EVENT n=" + receivedGuiEvents + " type=" + pointer.type + " raw=" + pointer.rawType
                    + " point=" + pointer.mousePosition + " hot=" + GUIUtility.hotControl + " enabled=" + GUI.enabled
                    + " screen=" + Screen.width + "x" + Screen.height + " view=" + PartInteractionController.ViewMode);
            };
            PartInteractionController.EditorMouseEventReceived += acknowledge;
            queuedGuiEvents = 0;
            IEnumerator steps = GuiSteps(app);
            int previousFrame = Time.frameCount;
            double deadline = EditorApplication.timeSinceStartup + 60;
            EditorApplication.CallbackFunction tick = null;
            tick = () => {
                try
                {
                    window.Repaint();
                    EditorApplication.QueuePlayerLoopUpdate();
                    if (EditorApplication.timeSinceStartup > deadline)
                        throw new TimeoutException("Motion GUI regression did not receive game frames");
                    if (receivedGuiEvents < queuedGuiEvents) return;
                    if (Time.frameCount <= previousFrame + 1) return;
                    previousFrame = Time.frameCount;
                    if (steps.MoveNext()) return;
                    EditorApplication.update -= tick;
                    PartInteractionController.EditorMouseEventReceived -= acknowledge;
                    complete();
                }
                catch (Exception error)
                {
                    EditorApplication.update -= tick;
                    PartInteractionController.EditorMouseEventReceived -= acknowledge;
                    app.StopMotion();
                    app.GetComponent<PartInteractionController>().CancelGesture();
                    failed(error);
                }
            };
            EditorApplication.update += tick;
        }

        private static IEnumerator GuiSteps(MechMasterApp app)
        {
            var input = app.GetComponent<PartInteractionController>();
            bool originalViewMode = PartInteractionController.ViewMode;
            if (app.Plan.Mode == AssemblyMode.Assemble) app.ToggleMode();
            app.FrameWholeModel();
            app.SetInteractionViewMode(true);
            yield return null;
            QueueMouse(EventType.MouseDown, GuiPoint(WorkshopLayout.MotionButton));
            yield return null;
            QueueMouse(EventType.MouseUp, GuiPoint(WorkshopLayout.MotionButton));
            yield return null;
            RequireDrag(app.IsMotionPlaying, "Actual toolbar motion button did not start motion");
            float stopAt = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < stopAt) yield return null;
            QueueMouse(EventType.MouseDown, GuiPoint(WorkshopLayout.MotionStopButton));
            yield return null;
            QueueMouse(EventType.MouseUp, GuiPoint(WorkshopLayout.MotionStopButton));
            yield return null;
            RequireDrag(!app.IsMotionActive, "Actual toolbar stop button did not end motion");
            // Fault injection: the explicit Part button must also repair stale
            // disabled hit targets, even when motion has already ended.
            var root = GameObject.Find("MechanicalModel_" + app.Model.id);
            foreach (Collider target in root.GetComponentsInChildren<Collider>())
                if (target.GetComponent<MechanicalPartHitProxy>()?.Owner != null) target.enabled = false;
            QueueMouse(EventType.MouseDown, GuiPoint(WorkshopLayout.PartButton));
            yield return null;
            QueueMouse(EventType.MouseUp, GuiPoint(WorkshopLayout.PartButton));
            yield return null;
            RequireDrag(!PartInteractionController.ViewMode, "Actual toolbar Part button left view mode enabled");
            Vector2 point = PickablePoint(root);
            QueueMouse(EventType.MouseDown, point);
            yield return null;
            var part = (MechanicalPartView)typeof(PartInteractionController).GetField("activePart", Members).GetValue(input);
            RequireDrag(part != null, "Actual OnGUI mouse down missed a displayed part after toolbar stop/Part");
            Vector2 angles = Camera.main.GetComponent<OrbitCameraController>().Angles;
            Vector2 drop = MatchingTrayPoint(part.Definition.AssemblyId);
            QueueMouse(EventType.MouseDrag, drop);
            yield return null;
            RequireDrag(PartInteractionController.IsDraggingPart
                && Camera.main.GetComponent<OrbitCameraController>().Angles == angles,
                "Actual OnGUI drag rotated the camera instead of moving the part");
            QueueMouse(EventType.MouseUp, drop);
            yield return null;
            RequireDrag(app.Plan.RemovedCount == 1 && app.Plan.IsRemoved(part.PartId),
                "Actual OnGUI drop did not disassemble its part");
            Debug.Log("MECH_MASTER_MOTION_GUI_OK model=" + app.Model.id + " level=" + app.Plan.Difficulty
                + " native-events: play-button,end-button,part-button,pick,drag,drop stale-hit-target-recovery");
            app.ResetCurrentPlan();
            PartInteractionController.SetViewMode(originalViewMode);
            yield return null;
        }

        private static Vector2 GuiPoint(Rect rectangle)
        {
            Vector2 point = new PixelUILayout(Screen.width, Screen.height).ToPixels(rectangle).center;
            return new Vector2(point.x, Screen.height - point.y);
        }

        private static void QueueMouse(EventType type, Vector2 point)
        {
            queuedGuiEvents++;
            EditorGUIUtility.QueueGameViewInputEvent(new Event { type = type, button = 0,
                mousePosition = new Vector2(point.x, Screen.height - point.y) });
        }

        public static void Validate(MechMasterApp app, Component controller)
        {
            var input = app.GetComponent<PartInteractionController>();
            var root = controller.gameObject;
            var view = root.GetComponent<MechanicalModelView>();
            var start = root.GetComponentsInChildren<MeshFilter>()
                .ToDictionary(mesh => mesh.name, mesh => mesh.transform.position);
            string[] savedIds = LocalProgressStore.LoadRemovedPartIds(app.Model.id, app.Plan.Difficulty);
            SimulationMode simulationMode = Physics.simulationMode;
            bool autoSync = Physics.autoSyncTransforms;
            Physics.simulationMode = SimulationMode.Script;
            Physics.autoSyncTransforms = false;
            typeof(PartInteractionController).GetField("mouseButton", Members).SetValue(input, 0);
            try
            {
                app.FrameWholeModel();
                Physics.SyncTransforms();
                for (int repetition = 0; repetition < 6; repetition++)
                {
                    app.ToggleMotion();
                    controller.GetType().GetMethod("ApplyPhase", Members)
                        .Invoke(controller, new object[] { .37f });
                    if (repetition % 3 == 0) app.StopMotion();
                    else if (repetition % 3 == 1) app.ToggleMotion(); // paused -> explode
                    app.ToggleLocalExplosionMode(); // also tests playing -> explode
                    Require(!app.IsMotionActive && app.IsLocalExplosionMode, "Motion-to-local transition");

                    Vector2 point = PickablePoint(root);
                    Invoke(input, "BeginPointer", point, -1);
                    var part = (MechanicalPartView)typeof(PartInteractionController)
                        .GetField("activePart", Members).GetValue(input);
                    Require(part != null, "No actual part picked after motion");
                    RaycastHit surface = Physics.RaycastAll(Camera.main.ScreenPointToRay(point))
                        .First(hit => hit.collider.GetComponent<MechanicalPartHitProxy>()?.Owner == part);
                    Transform surfaceTransform = surface.collider.transform;
                    Vector3 localSurface = surfaceTransform.InverseTransformPoint(surface.point);
                    Invoke(input, "EndPointer", point);
                    Require(view.ExplosionTargetCount == 1 && view.LocalExplosionPartId == part.PartId,
                        "First tap did not explode its picked part");
                    part.SetInspectionExplosion(part.InspectionWorldOffset, true, true);

                    Vector3 displayed = Camera.main.WorldToScreenPoint(surfaceTransform.TransformPoint(localSurface));
                    var second = new Vector2(displayed.x, displayed.y);
                    Require(displayed.z > 0 && !PrototypeUI.IsScreenPositionOverPanel(second),
                        "Displayed explosion surface is outside the interaction area");
                    bool unsyncedHit = Physics.RaycastAll(Camera.main.ScreenPointToRay(second))
                        .Any(hit => hit.collider.GetComponent<MechanicalPartHitProxy>()?.Owner == part);
                    Invoke(input, "BeginPointer", second, -1);
                    Invoke(input, "EndPointer", second);
                    if (view.ExplosionTargetCount != 0)
                    {
                        // Failure diagnostics distinguish stale physics from a
                        // visibility/occlusion problem without hiding the failure.
                        Physics.SyncTransforms();
                        bool syncedHit = Physics.RaycastAll(Camera.main.ScreenPointToRay(second))
                            .Any(hit => hit.collider.GetComponent<MechanicalPartHitProxy>()?.Owner == part);
                        throw new InvalidOperationException("Motion picking regression: second tap did not retract "
                            + part.PartId + "; unsyncedHit=" + unsyncedHit + ", syncedHit=" + syncedHit);
                    }
                    Require(view.LocalExplosionPartId == null && app.Plan.RemovedCount == 0
                        && savedIds.SequenceEqual(LocalProgressStore.LoadRemovedPartIds(app.Model.id, app.Plan.Difficulty)),
                        "Local explosion changed progress");
                    app.FrameWholeModel();
                    Require(root.GetComponentsInChildren<MeshFilter>().All(mesh =>
                        Vector3.Distance(mesh.transform.position, start[mesh.name]) < 1e-5f),
                        "Motion/explosion sequence did not restore source positions");
                }
                Debug.Log("MECH_MASTER_MOTION_PICKING_OK model=" + app.Model.id
                    + " level=" + app.Plan.Difficulty + " cycles=6 end,pause,direct,explode,reclick no-physics-tick");
            }
            finally
            {
                app.StopMotion();
                app.FrameWholeModel();
                input.CancelGesture();
                Physics.autoSyncTransforms = autoSync;
                Physics.simulationMode = simulationMode;
            }
        }

        public static void ValidateDisassembly(MechMasterApp app, Component controller)
        {
            var input = app.GetComponent<PartInteractionController>();
            var root = controller.gameObject;
            var view = root.GetComponent<MechanicalModelView>();
            var start = root.GetComponentsInChildren<MeshFilter>()
                .ToDictionary(mesh => mesh.name, mesh => mesh.transform.position);
            bool originalViewMode = PartInteractionController.ViewMode;
            SimulationMode simulationMode = Physics.simulationMode;
            bool autoSync = Physics.autoSyncTransforms;
            Physics.simulationMode = SimulationMode.Script;
            Physics.autoSyncTransforms = false;
            RequireDrag(app.Plan.IsAssemblyComplete && app.Plan.Mode == AssemblyMode.Disassemble,
                "Validation needs a fully assembled disassembly plan");
            var collisionGuard = new GameObject("UnrelatedDisabledCollider");
            collisionGuard.transform.SetParent(root.transform, false);
            var guardCollider = collisionGuard.AddComponent<BoxCollider>();
            guardCollider.enabled = false;
            try
            {
                foreach (int scenario in new[] { 2, 3, 4, 0, 1, 5 })
                {
                    app.FrameWholeModel();
                    app.SetInteractionViewMode(scenario >= 2);
                    if (scenario == 5)
                        foreach (Collider target in root.GetComponentsInChildren<Collider>())
                            if (target.GetComponent<MechanicalPartHitProxy>()?.Owner != null) target.enabled = false;
                    app.ToggleMotion();
                    RequireDrag(app.IsMotionPlaying && PartInteractionController.ViewMode,
                        "Motion did not temporarily enter view mode");
                    controller.GetType().GetMethod("Advance", Members).Invoke(controller, new object[] { 1.8f });
                    if (scenario == 1 || scenario == 4) app.ToggleMotion();
                    if (scenario == 1) app.ToggleMotion(); // Resume must retain the original interaction mode.
                    if (scenario == 3 || scenario == 4) app.SetInteractionViewMode(false);
                    else
                    {
                        app.StopMotion();
                        app.StopMotion(); // Idempotent: do not restore a stale mode on a second click.
                        if (scenario >= 2)
                        {
                            RequireDrag(PartInteractionController.ViewMode, "View mode was not restored");
                            app.SetInteractionViewMode(false); // The same action as the toolbar's Part button.
                        }
                    }
                    RequireDrag(!app.IsMotionActive && !PartInteractionController.IsDraggingPart
                        && PrototypeUI.HoveredTrayAssemblyId == null, "Stopped motion left an active gesture");
                    RequireDrag(root.GetComponentsInChildren<Collider>()
                        .Where(target => target.GetComponent<MechanicalPartHitProxy>()?.Owner != null)
                        .All(target => target.enabled) && !guardCollider.enabled,
                        "Stopped motion left disabled part hit targets or enabled an unrelated collider");

                    Vector2 point;
                    MechanicalPartView picked = PressVisiblePart(input, root, out point);
                    string pickedId = picked.PartId;
                    Vector2 drop = MatchingTrayPoint(picked.Definition.AssemblyId);
                    Vector2 orbitAngles = Camera.main.GetComponent<OrbitCameraController>().Angles;
                    Mouse(input, EventType.MouseDrag, drop);
                    RequireDrag(PartInteractionController.IsDraggingPart && !PartInteractionController.ViewMode,
                        "Drag became camera orbit after ending motion; scenario=" + scenario
                        + " viewMode=" + PartInteractionController.ViewMode + " part=" + pickedId);
                    RequireDrag(Camera.main.GetComponent<OrbitCameraController>().Angles == orbitAngles
                        && PrototypeUI.HoveredTrayAssemblyId == picked.Definition.AssemblyId,
                        "Part drag rotated the camera or missed the matching tray");
                    Mouse(input, EventType.MouseUp, drop);
                    RequireDrag(app.Plan.IsRemoved(pickedId) && app.Plan.RemovedCount == 1
                        && !PartInteractionController.IsDraggingPart
                        && PrototypeUI.HoveredTrayAssemblyId == null
                        && LocalProgressStore.LoadRemovedPartIds(app.Model.id, app.Plan.Difficulty).SequenceEqual(new[] { pickedId }),
                        "Pointer drop did not remove and save exactly its picked part");

                    // Complete the rest through the normal app operations, then
                    // use the actual public assembly-mode transition and drag.
                    foreach (PartDefinition part in app.Plan.Steps)
                        if (!app.Plan.IsRemoved(part.Id)) app.Operate(part.Id);
                    view.Refresh(app.Plan, true);
                    app.ToggleMode();
                    RequireDrag(app.Plan.Mode == AssemblyMode.Assemble, "Assembly transition was blocked");
                    picked = PressVisiblePart(input, root, out point);
                    pickedId = picked.PartId;
                    RequireDrag(app.Plan.IsRemoved(pickedId), "Assembly picked an installed part");
                    Rect area = new PixelUILayout(Screen.width, Screen.height).ToPixels(WorkshopLayout.ModelArea);
                    // A storage part can already project near the workspace
                    // center at larger GameView sizes. Choose a distant valid
                    // drop so this really crosses the runtime drag threshold.
                    drop = new[] { new Vector2(.2f, .2f), new Vector2(.8f, .2f),
                        new Vector2(.2f, .8f), new Vector2(.8f, .8f) }
                        .Select(offset => new Vector2(area.xMin + area.width * offset.x,
                            Screen.height - area.yMin - area.height * offset.y))
                        .Where(value => !PrototypeUI.IsScreenPositionOverPanel(value))
                        .OrderByDescending(value => (value - point).sqrMagnitude).First();
                    RequireDrag(!PrototypeUI.IsScreenPositionOverPanel(drop), "Assembly drop is covered by UI");
                    Mouse(input, EventType.MouseDrag, drop);
                    RequireDrag(PartInteractionController.IsDraggingPart, "Assembly drag did not start");
                    Mouse(input, EventType.MouseUp, drop);
                    RequireDrag(!app.Plan.IsRemoved(pickedId) && app.Plan.RemovedCount == app.Plan.Steps.Count - 1,
                        "Pointer assembly drop did not reinstall its picked part");
                    foreach (PartDefinition part in app.Plan.Steps)
                        if (app.Plan.IsRemoved(part.Id)) app.Operate(part.Id);
                    view.Refresh(app.Plan, true);
                    app.ToggleMode();
                    RequireDrag(app.Plan.Mode == AssemblyMode.Disassemble && app.Plan.RemovedCount == 0
                        && LocalProgressStore.LoadRemovedPartIds(app.Model.id, app.Plan.Difficulty).Length == 0
                        && root.GetComponentsInChildren<MeshFilter>().All(mesh =>
                            Vector3.Distance(mesh.transform.position, start[mesh.name]) < 1e-5f),
                        "Repeated motion/disassembly/assembly cycle did not restore the source plan and pose");
                }
                Debug.Log("MECH_MASTER_MOTION_DISASSEMBLY_OK model=" + app.Model.id
                    + " level=" + app.Plan.Difficulty + " cycles=6 restore-mode,resume,explicit,direct,paused,stale-hit-target pointer-drag,save,reassemble no-physics-tick");
            }
            finally
            {
                app.StopMotion();
                input.CancelGesture();
                app.FrameWholeModel();
                PartInteractionController.SetViewMode(originalViewMode);
                Physics.autoSyncTransforms = autoSync;
                Physics.simulationMode = simulationMode;
                UnityEngine.Object.Destroy(collisionGuard);
            }
        }

        private static MechanicalPartView PressVisiblePart(PartInteractionController input, GameObject root, out Vector2 point)
        {
            Rect area = new PixelUILayout(Screen.width, Screen.height).ToPixels(WorkshopLayout.ModelArea);
            var points = Enumerable.Range(0, Mathf.Max(1, (int)(area.width / 12) - 2))
                .SelectMany(x => Enumerable.Range(0, Mathf.Max(1, (int)(area.height / 12) - 2))
                    .Select(y => new Vector2(area.xMin + 18 + x * 12, area.yMin + 18 + y * 12)))
                .OrderBy(value => (value - area.center).sqrMagnitude);
            bool logged = false;
            foreach (Vector2 guiPoint in points)
            {
                point = new Vector2(guiPoint.x, Screen.height - guiPoint.y);
                if (PrototypeUI.IsScreenPositionOverPanel(point)) continue;
                // Let the real runtime pointer handler synchronize physics and
                // resolve a part; do not sync or choose its collider for it.
                Mouse(input, EventType.MouseDown, point);
                var part = (MechanicalPartView)typeof(PartInteractionController).GetField("activePart", Members).GetValue(input);
                if (part != null && part.transform.IsChildOf(root.transform)) return part;
                var hits = Physics.RaycastAll(Camera.main.ScreenPointToRay(point));
                if (!logged && hits.Any(hit => hit.collider.GetComponent<MechanicalPartHitProxy>() != null))
                {
                    logged = true;
                    Debug.Log("MECH_MASTER_DRAG_INPUT_DIAGNOSTIC point=" + point + " hits=" + hits.Length
                        + " pointerActive=" + typeof(PartInteractionController).GetField("pointerActive", Members).GetValue(input)
                        + " camera=" + ((Camera)typeof(PartInteractionController).GetField("interactionCamera", Members).GetValue(input) == Camera.main)
                        + " view=" + PartInteractionController.ViewMode);
                }
                Mouse(input, EventType.MouseUp, point);
            }
            throw new InvalidOperationException("Motion disassembly regression: no displayed part could be picked; colliders="
                + root.GetComponentsInChildren<Collider>().Count(item => item.enabled) + " screen=" + Screen.width + "x" + Screen.height);
        }

        private static Vector2 MatchingTrayPoint(string assemblyId)
        {
            Rect tray = new PixelUILayout(Screen.width, Screen.height).ToPixels(WorkshopLayout.Tray);
            for (float y = tray.yMin + 4; y < tray.yMax; y += 6)
                for (float x = tray.xMin + 4; x < tray.xMax; x += 6)
                {
                    var point = new Vector2(x, Screen.height - y);
                    if (PrototypeUI.TrayAssemblyAtScreenPosition(point) == assemblyId) return point;
                }
            throw new InvalidOperationException("Motion disassembly regression: missing matching tray " + assemblyId);
        }

        private static void Mouse(PartInteractionController input, EventType type, Vector2 point)
        {
            // Editor delayCall has no IMGUI context: a synthetic Event's type
            // can be Ignore there. Exercise the shared mouse/touch gesture path
            // with its real physics pick, movement, drop and plan persistence.
            if (type == EventType.MouseDown)
            {
                typeof(PartInteractionController).GetField("mouseButton", Members).SetValue(input, 0);
                Invoke(input, "BeginPointer", point, -1);
            }
            else if (type == EventType.MouseDrag) Invoke(input, "MovePointer", point);
            else if (type == EventType.MouseUp) Invoke(input, "EndPointer", point);
            else throw new ArgumentOutOfRangeException(nameof(type));
        }

        private static void RequireDrag(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Motion disassembly regression: " + message);
        }

        private static Vector2 PickablePoint(GameObject root)
        {
            Rect area = new PixelUILayout(Screen.width, Screen.height).ToPixels(WorkshopLayout.ModelArea);
            var points = Enumerable.Range(0, Mathf.Max(1, (int)(area.width / 18) - 2))
                .SelectMany(x => Enumerable.Range(0, Mathf.Max(1, (int)(area.height / 18) - 2))
                    .Select(y => new Vector2(area.xMin + 24 + x * 18, area.yMin + 24 + y * 18)))
                .OrderBy(point => (point - area.center).sqrMagnitude);
            foreach (Vector2 guiPoint in points)
            {
                Vector2 point = new Vector2(guiPoint.x, Screen.height - guiPoint.y);
                if (!PrototypeUI.IsScreenPositionOverPanel(point)
                    && Physics.RaycastAll(Camera.main.ScreenPointToRay(point))
                        .Any(hit => hit.collider.transform.IsChildOf(root.transform)
                            && hit.collider.GetComponent<MechanicalPartHitProxy>() != null))
                    return point;
            }
            throw new InvalidOperationException("Motion picking regression: no pickable displayed model");
        }

        private static void Invoke(PartInteractionController input, string method, params object[] args) =>
            typeof(PartInteractionController).GetMethod(method, Members).Invoke(input, args);

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Motion picking regression: " + message);
        }
    }

}
