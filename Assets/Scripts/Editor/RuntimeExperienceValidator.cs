using System;
using System.Linq;
using System.Reflection;
using MechMaster.Runtime;
using MechMaster.Runtime.UI;
using UnityEditor;
using UnityEngine;

namespace MechMaster.Editor
{
    public static class RuntimeExperienceValidator
    {
        [MenuItem("机械大师/验证视角与面板交互")]
        public static void Validate()
        {
            Require(EditorApplication.isPlaying && MechMasterApp.Instance != null, "Run Play first.");
            var app = MechMasterApp.Instance;
            var input = app.GetComponent<PartInteractionController>();
            var voice = app.GetComponent<VoiceNarrator>();
            var camera = Camera.main;
            var orbit = camera.GetComponent<OrbitCameraController>();
            bool oldMode = PartInteractionController.ViewMode;
            bool oldVoice = voice.Enabled;
            bool oldTools = WorkshopLayout.ToolsCollapsed;
            bool oldKnowledge = WorkshopLayout.KnowledgeCollapsed;
            int removed = app.Plan.RemovedCount;
            try
            {
                voice.Enabled = false;
                input.CancelGesture();
                WorkshopLayout.ToolsCollapsed = WorkshopLayout.KnowledgeCollapsed = false;
                orbit.FrameWholeBike();
                VerifyFraming(camera);
                VerifyPan(camera, orbit);
                app.FrameStorage();
                VerifyFraming(camera, true);
                WorkshopLayout.ToolsCollapsed = WorkshopLayout.KnowledgeCollapsed = true;
                orbit.FrameWholeBike();
                VerifyFraming(camera);
                WorkshopLayout.ToolsCollapsed = WorkshopLayout.KnowledgeCollapsed = false;
                orbit.FrameWholeBike();
                PartInteractionController.SetViewMode(true);
                Vector2 point = FindPartPoint(camera);
                Vector2 before = orbit.Angles;
                Invoke(input, "BeginPointer", point, -1);
                Invoke(input, "MovePointer", point + new Vector2(130, 45));
                Require(Vector2.Distance(before, orbit.Angles) > 1, "View drag must rotate over a part.");
                Require(!PartInteractionController.IsDraggingPart, "View drag must not grab a part.");
                input.CancelGesture();
                orbit.FrameWholeBike();

                // Mouse down and up may be delivered within one rendered frame.
                // Their own event coordinates must still produce a real drag.
                before = orbit.Angles;
                Vector2 guiPoint = new Vector2(point.x, Screen.height - point.y);
                Invoke(input, "HandleMouseEvent", new Event {
                    type = EventType.MouseDown, button = 0, mousePosition = guiPoint });
                Invoke(input, "HandleMouseEvent", new Event {
                    type = EventType.MouseUp, button = 0, mousePosition = guiPoint + Vector2.right * 120 });
                Require(before != orbit.Angles, "A short mouse drag must retain its press position.");
                orbit.FrameWholeBike();

                Vector2 header = new PixelUILayout(Screen.width, Screen.height).ToPixels(WorkshopLayout.Header).center;
                header.y = Screen.height - header.y;
                before = orbit.Angles;
                Invoke(input, "BeginPointer", header, -1);
                Invoke(input, "MovePointer", header + Vector2.right * 100);
                Require(before == orbit.Angles, "UI press must not rotate.");
                input.CancelGesture();

                Rect panPanel = new PixelUILayout(Screen.width, Screen.height).ToPixels(WorkshopLayout.PanControls);
                Vector2 panPoint = new Vector2(panPanel.center.x, Screen.height - panPanel.center.y);
                before = orbit.Angles;
                Invoke(input, "BeginPointer", panPoint, -1);
                Invoke(input, "MovePointer", panPoint + Vector2.right * 100);
                Require(before == orbit.Angles && !PartInteractionController.IsDraggingPart,
                    "Pan buttons must not start orbit or part dragging.");
                input.CancelGesture();

                PartInteractionController.SetViewMode(false);
                point = FindPartPoint(camera);
                before = orbit.Angles;
                Invoke(input, "BeginPointer", point, -1);
                Invoke(input, "MovePointer", point + Vector2.right * 100);
                Require(PartInteractionController.IsDraggingPart, "Part mode must drag a part.");
                Require(before == orbit.Angles, "Part drag must not rotate.");
                input.CancelGesture();
                Require(!PartInteractionController.IsDraggingPart, "Cancellation must release the drag.");

                PartInteractionController.SetViewMode(true);
                Touch touch = new Touch { fingerId = 7, position = point, phase = TouchPhase.Began };
                Invoke(input, "HandleTouch", touch);
                touch.position += Vector2.right * 100;
                touch.phase = TouchPhase.Moved;
                Invoke(input, "HandleTouch", touch);
                Require(before != orbit.Angles, "Single-finger drag must rotate.");
                touch.phase = TouchPhase.Canceled;
                Invoke(input, "HandleTouch", touch);
                Require(app.Plan.RemovedCount == removed, "Tests must not change saved progress.");
                Require(voice.Available, "Local Chinese narrator must be available.");
                UIPixelLayoutValidator.Validate();
                Debug.Log("MECH_MASTER_EXPERIENCE_VALIDATION_OK framing,pan-directions,pan-reset,pan-ui-exclusion,collapsed-panels,mouse-routing,touch-cancel,voice-ready");
            }
            finally
            {
                input.CancelGesture();
                WorkshopLayout.ToolsCollapsed = oldTools;
                WorkshopLayout.KnowledgeCollapsed = oldKnowledge;
                PartInteractionController.SetViewMode(oldMode);
                voice.Enabled = oldVoice;
                orbit.FrameWholeBike();
            }
        }

        private static void VerifyPan(Camera camera, OrbitCameraController orbit)
        {
            Vector3 world = GameObject.Find("BicycleEngineering_Runtime").transform.position;
            foreach (Vector2 direction in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
            {
                Vector2 angles = orbit.Angles;
                float zoom = orbit.ZoomFactor;
                Vector3 position = camera.WorldToScreenPoint(world);
                orbit.Pan(direction * 48);
                Vector2 movement = (Vector2)(camera.WorldToScreenPoint(world) - position);
                Vector2 expected = direction * 48 * new PixelUILayout(Screen.width, Screen.height).Scale;
                Require(Vector2.Distance(movement, expected) < .1f, "Pan direction / screen distance mismatch.");
                Require(angles == orbit.Angles && zoom == orbit.ZoomFactor, "Pan must preserve orbit and zoom.");
                Ray ray = camera.ScreenPointToRay(camera.WorldToScreenPoint(world));
                Require(Vector3.Cross(world - ray.origin, ray.direction).magnitude < .001f,
                    "Panned picking rays must still match visible geometry.");
                orbit.ResetPan();
                Require(Vector3.Distance(camera.WorldToScreenPoint(world), position) < .1f, "Pan reset failed.");
            }
            orbit.Pan(new Vector2(100000, 100000));
            float maxX = WorkshopLayout.ModelArea.width * .45f;
            float maxY = WorkshopLayout.ModelArea.height * .45f;
            Require(Mathf.Abs(orbit.PanOffset.x) <= maxX + .01f &&
                Mathf.Abs(orbit.PanOffset.y) <= maxY + .01f,
                "Pan must be bounded (actual=" + orbit.PanOffset + ", max=" + maxX + "," + maxY + ").");
            orbit.FrameWholeBike();
            Require(orbit.PanOffset == Vector2.zero, "Whole bike reset must clear pan.");
        }

        private static void VerifyFraming(Camera camera, bool includeTray = false)
        {
            Rect area = new PixelUILayout(Screen.width, Screen.height).ToPixels(WorkshopLayout.ModelArea);
            foreach (var renderer in GameObject.Find("BicycleEngineering_Runtime").GetComponentsInChildren<Renderer>())
            {
                if (!includeTray && renderer.name.StartsWith("TrayCell_", StringComparison.Ordinal)) continue;
                if (!includeTray && renderer.GetComponent<MechanicalPartHitProxy>()?.Owner?.IsRemoved == true) continue;
                Bounds bounds = renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = camera.WorldToScreenPoint(corner);
                    Require(p.z > 0 && area.Contains(new Vector2(p.x, Screen.height - p.y)), "Model clipped: " + renderer.name);
                }
            }
        }

        private static Vector2 FindPartPoint(Camera camera)
        {
            Physics.SyncTransforms();
            Rect area = new PixelUILayout(Screen.width, Screen.height).ToPixels(WorkshopLayout.ModelArea);
            for (float y = area.yMin + 30; y < area.yMax - 30; y += 24)
                for (float x = area.xMin + 30; x < area.xMax - 30; x += 24)
                {
                    Vector2 point = new Vector2(x, Screen.height - y);
                    if (Physics.RaycastAll(camera.ScreenPointToRay(point)).Any(hit =>
                        hit.collider.GetComponent<MechanicalPartHitProxy>() != null)) return point;
                }
            throw new InvalidOperationException("No selectable part in the workspace.");
        }

        private static void Invoke(PartInteractionController target, string method, params object[] args) =>
            typeof(PartInteractionController).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Experience validation: " + message);
        }
    }
}
