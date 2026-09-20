using System;
using UnityEngine;
using UnityEngine.EventSystems;
using MechMaster.Runtime.UI;

namespace MechMaster.Runtime
{
    public sealed class PartInteractionController : MonoBehaviour
    {
        private const float DragThreshold = 18f;
        private Camera interactionCamera;
        private OrbitCameraController orbit;
        private MechanicalPartView activePart;
        private Vector2 pressPosition;
        private Vector2 currentPosition;
        private Plane dragPlane;
        private Vector3 pressWorldPosition;
        private bool hasDragPlane;
        private bool pointerActive;
        private bool orbitGesture;
        private bool moved;
        private bool suppressTap;
        private int mouseButton;
        private int fingerId = -1;
        private bool pinching;
        private bool waitForAllTouchesUp;
        private float previousPinchDistance;
        private Vector2 previousPinchCenter;
        private int pinchFingerA;
        private int pinchFingerB;

        public static bool IsDraggingPart { get; private set; }
        public static bool ViewMode { get; private set; } = true;
        public static PartInteractionController Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            IsDraggingPart = false;
            ViewMode = true;
        }

        public void Initialize(Camera targetCamera)
        {
            interactionCamera = targetCamera;
            orbit = targetCamera.GetComponent<OrbitCameraController>();
            Instance = this;
        }

        public static void SetViewMode(bool value)
        {
            Instance?.CancelGesture();
            ViewMode = value;
        }

        private void Update()
        {
            if (interactionCamera == null || MechMasterApp.Instance == null)
            {
                return;
            }

            if (Input.touchCount >= 2)
            {
                HandlePinch(Input.GetTouch(0), Input.GetTouch(1));
                return;
            }
            if (waitForAllTouchesUp)
            {
                if (Input.touchCount == 0) { waitForAllTouchesUp = false; pinching = false; }
                return;
            }
            if (Input.touchCount == 1)
            {
                HandleTouch(Input.GetTouch(0));
                return;
            }

        }

        private void HandlePinch(Touch first, Touch second)
        {
            if (waitForAllTouchesUp && !pinching) return;
            if (!pinching)
            {
                CancelGesture();
                waitForAllTouchesUp = true;
                if (IsOverUI(first.position, first.fingerId) || IsOverUI(second.position, second.fingerId)) return;
                pinching = true;
                pinchFingerA = first.fingerId;
                pinchFingerB = second.fingerId;
                previousPinchDistance = Vector2.Distance(first.position, second.position);
                previousPinchCenter = (first.position + second.position) * 0.5f;
                return;
            }
            if (first.fingerId != pinchFingerA || second.fingerId != pinchFingerB)
            {
                pinching = false;
                return;
            }
            Vector2 center = (first.position + second.position) * 0.5f;
            float separation = Vector2.Distance(first.position, second.position);
            orbit.Rotate(center - previousPinchCenter);
            if (previousPinchDistance > 1 && separation > 1)
                orbit.Zoom(Mathf.Log(separation / previousPinchDistance));
            previousPinchDistance = separation;
            previousPinchCenter = center;
        }

        private void HandleTouch(Touch touch)
        {
            if (touch.phase == TouchPhase.Began)
            {
                fingerId = touch.fingerId;
                BeginPointer(touch.position, touch.fingerId);
            }
            else if (touch.fingerId != fingerId) return;
            else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                MovePointer(touch.position);
            }
            else if (touch.phase == TouchPhase.Canceled)
            {
                CancelGesture();
            }
            else if (touch.phase == TouchPhase.Ended)
            {
                EndPointer(touch.position);
            }
        }

        private void OnGUI()
        {
            if (interactionCamera == null || MechMasterApp.Instance == null
                || Input.touchCount > 0 || waitForAllTouchesUp) return;
            // Preserve each event's position. Frame polling can see both down/up at
            // the final position of a short drag, especially in a busy Editor.
            HandleMouseEvent(Event.current);
        }

        private void HandleMouseEvent(Event pointerEvent)
        {
            Vector2 position = new Vector2(pointerEvent.mousePosition.x,
                Screen.height - pointerEvent.mousePosition.y);
            if (pointerEvent.type == EventType.MouseDown && pointerEvent.button <= 1)
            {
                mouseButton = pointerEvent.button;
                BeginPointer(position, -1);
            }
            else if (pointerActive && pointerEvent.type == EventType.MouseDrag
                && pointerEvent.button == mouseButton)
            {
                MovePointer(position);
            }
            else if (pointerActive && pointerEvent.type == EventType.MouseUp
                && pointerEvent.button == mouseButton)
            {
                EndPointer(position);
            }
            else if (!pointerActive && pointerEvent.type == EventType.ScrollWheel
                && !IsOverUI(position, -1))
                orbit.Zoom(-pointerEvent.delta.y * 0.04f);
        }

        private static bool IsOverUI(Vector2 position, int pointerId) =>
            PrototypeUI.IsScreenPositionOverPanel(position)
            || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId));

        private void BeginPointer(Vector2 screenPosition, int pointerId)
        {
            CancelGesture();
            if (IsOverUI(screenPosition, pointerId)) return;
            pointerActive = true;
            moved = false;
            suppressTap = pointerId == -1 && mouseButton == 1;
            orbitGesture = ViewMode || suppressTap;
            pressPosition = currentPosition = screenPosition;

            Ray ray = interactionCamera.ScreenPointToRay(screenPosition);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                100f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            if (hits.Length == 0)
            {
                orbitGesture = true;
                return;
            }
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            RaycastHit selectedHit;
            MechanicalPartView part = ResolveBestPart(hits, out selectedHit);
            if (part == null)
            {
                orbitGesture = true;
                return;
            }

            activePart = part;
            pressPosition = screenPosition;
            currentPosition = screenPosition;
            dragPlane = new Plane(interactionCamera.transform.forward, selectedHit.point);
            float pressDistance;
            hasDragPlane = dragPlane.Raycast(ray, out pressDistance);
            pressWorldPosition = hasDragPlane
                ? ray.GetPoint(pressDistance)
                : selectedHit.point;
            if (!orbitGesture)
            {
                part.SetSelected(true);
                MechMasterApp.Instance.SelectPart(part.PartId);
            }
        }

        private static MechanicalPartView ResolveBestPart(
            RaycastHit[] hits,
            out RaycastHit selectedHit)
        {
            MechanicalPartView nearest = null;
            float nearestDistance = float.MaxValue;
            float smallestNearbyVolume = float.MaxValue;
            selectedHit = default(RaycastHit);

            foreach (RaycastHit hit in hits)
            {
                MechanicalPartHitProxy proxy =
                    hit.collider.GetComponent<MechanicalPartHitProxy>();
                MechanicalPartView candidate = proxy != null
                    ? proxy.Owner
                    : hit.collider.GetComponentInParent<MechanicalPartView>();
                if (candidate == null)
                {
                    continue;
                }

                if (nearest == null)
                {
                    nearest = candidate;
                    nearestDistance = hit.distance;
                    smallestNearbyVolume = BoundsVolume(hit.collider.bounds);
                    selectedHit = hit;
                    continue;
                }

                if (hit.distance > nearestDistance + 0.04f)
                {
                    break;
                }

                // Small controls and fasteners sit directly on larger structures.
                // When surfaces are almost coplanar, prefer the tighter mesh target.
                float volume = BoundsVolume(hit.collider.bounds);
                if (volume < smallestNearbyVolume)
                {
                    nearest = candidate;
                    smallestNearbyVolume = volume;
                    selectedHit = hit;
                }
            }

            return nearest;
        }

        private void MovePointer(Vector2 screenPosition)
        {
            if (!pointerActive) return;
            float threshold = DragThreshold * new PixelUILayout(Screen.width, Screen.height).Scale;
            if (!moved && Vector2.Distance(pressPosition, screenPosition) < threshold) return;
            bool justStarted = !moved;
            moved = true;
            Vector2 delta = screenPosition - currentPosition;
            currentPosition = screenPosition;
            if (orbitGesture)
            {
                orbit.Rotate(delta);
                return;
            }
            if (activePart == null) return;
            if (justStarted)
            {
                IsDraggingPart = true;
                activePart.BeginDragPreview();
            }

            Vector3 worldOffset = Vector3.zero;
            if (hasDragPlane)
            {
                Ray pointerRay = interactionCamera.ScreenPointToRay(screenPosition);
                float pointerDistance;
                if (dragPlane.Raycast(pointerRay, out pointerDistance))
                {
                    worldOffset = pointerRay.GetPoint(pointerDistance) - pressWorldPosition;
                }
            }
            activePart.UpdateDragPreview(worldOffset);
            PrototypeUI.SetTrayDragFeedback(
                activePart.PartId,
                activePart.Definition.AssemblyId,
                screenPosition);
            string hoveredAssemblyId = PrototypeUI.HoveredTrayAssemblyId;
            bool correctAssembly = string.Equals(
                activePart.Definition.AssemblyId,
                hoveredAssemblyId,
                StringComparison.Ordinal);
            bool ready = correctAssembly
                && IsPartAvailableForCurrentMode(activePart)
                && activePart.Definition.RequiredTool == MechMasterApp.Instance.SelectedTool;
            MechMasterApp.Instance.SetTrayHover(
                hoveredAssemblyId,
                correctAssembly,
                ready);
        }

        private static bool IsPartAvailableForCurrentMode(MechanicalPartView part)
        {
            bool removed = MechMasterApp.Instance.Plan.IsRemoved(part.PartId);
            return MechMasterApp.Instance.Plan.Mode == MechMaster.Domain.AssemblyMode.Disassemble
                ? !removed
                : removed;
        }

        private static float BoundsVolume(Bounds bounds)
        {
            Vector3 size = bounds.size;
            return Mathf.Max(0.0000001f, size.x * size.y * size.z);
        }

        private void EndPointer(Vector2 screenPosition)
        {
            if (!pointerActive) return;
            MovePointer(screenPosition);
            if (orbitGesture)
            {
                string tappedPartId = !moved && !suppressTap && activePart != null ? activePart.PartId : null;
#if UNITY_EDITOR
                if (moved) Debug.Log("MECH_MASTER_ORBIT_END angles=" + orbit.Angles);
#endif
                CancelGesture();
                if (tappedPartId != null) MechMasterApp.Instance.SelectPart(tappedPartId);
                return;
            }
            if (activePart == null) { CancelGesture(); return; }

            MechanicalPartView releasedPart = activePart;
            string hoveredAssemblyId = PrototypeUI.HoveredTrayAssemblyId;
            bool draggedFarEnough = moved;
            bool correctTray = string.Equals(
                releasedPart.Definition.AssemblyId,
                hoveredAssemblyId,
                StringComparison.Ordinal);
            activePart = null;
            pointerActive = false;
            IsDraggingPart = false;
            hasDragPlane = false;
            releasedPart.EndDragPreview();
            releasedPart.SetSelected(false);
            PrototypeUI.ClearTrayDragFeedback();
            MechMasterApp.Instance.SetTrayHover(null, false, false);

            if (!draggedFarEnough)
            {
                return;
            }

            if (MechMasterApp.Instance.Plan.Mode == MechMaster.Domain.AssemblyMode.Disassemble)
            {
                if (correctTray)
                {
                    MechMasterApp.Instance.Operate(releasedPart.PartId);
                }
                else
                {
                    MechMasterApp.Instance.RejectTrayDrop(
                        releasedPart.PartId,
                        hoveredAssemblyId);
                }
                return;
            }

            if (string.IsNullOrEmpty(hoveredAssemblyId)
                && !PrototypeUI.IsScreenPositionOverPanel(screenPosition))
            {
                MechMasterApp.Instance.Operate(releasedPart.PartId);
            }
            else
            {
                MechMasterApp.Instance.RejectAssemblyDrop(releasedPart.PartId);
            }
        }

        public void CancelGesture()
        {
            if (activePart != null)
            {
                activePart.EndDragPreview();
                activePart.SetSelected(false);
            }

            activePart = null;
            pointerActive = false;
            moved = false;
            IsDraggingPart = false;
            hasDragPlane = false;
            PrototypeUI.ClearTrayDragFeedback();
            if (MechMasterApp.Instance != null)
            {
                MechMasterApp.Instance.SetTrayHover(null, false, false);
            }
        }

        private void OnApplicationFocus(bool focus) { if (!focus) CancelGesture(); }
        private void OnDisable()
        {
            CancelGesture();
            pinching = false;
            waitForAllTouchesUp = false;
            if (Instance == this) Instance = null;
        }
    }
}
