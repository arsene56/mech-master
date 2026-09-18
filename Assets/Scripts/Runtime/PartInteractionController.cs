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
        private MechanicalPartView activePart;
        private Vector2 pressPosition;
        private Vector2 currentPosition;
        private Plane dragPlane;
        private Vector3 pressWorldPosition;
        private bool hasDragPlane;

        public static bool IsDraggingPart { get; private set; }

        public void Initialize(Camera targetCamera)
        {
            interactionCamera = targetCamera;
        }

        private void Update()
        {
            if (interactionCamera == null || MechMasterApp.Instance == null)
            {
                return;
            }

            if (Input.touchCount > 0)
            {
                HandleTouch(Input.GetTouch(0));
                return;
            }

            HandleMouse();
        }

        private void HandleTouch(Touch touch)
        {
            if (touch.phase == TouchPhase.Began)
            {
                BeginPointer(touch.position, touch.fingerId);
            }
            else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                MovePointer(touch.position);
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                EndPointer(touch.position);
            }
        }

        private void HandleMouse()
        {
            if (Input.GetMouseButtonDown(0))
            {
                BeginPointer(Input.mousePosition, -1);
            }
            else if (Input.GetMouseButton(0))
            {
                MovePointer(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                EndPointer(Input.mousePosition);
            }
        }

        private void BeginPointer(Vector2 screenPosition, int pointerId)
        {
            if (PrototypeUI.IsScreenPositionOverPanel(screenPosition))
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId))
            {
                return;
            }

            Ray ray = interactionCamera.ScreenPointToRay(screenPosition);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                100f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            if (hits.Length == 0)
            {
                return;
            }
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            RaycastHit selectedHit;
            MechanicalPartView part = ResolveBestPart(hits, out selectedHit);
            if (part == null)
            {
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
            IsDraggingPart = true;
            part.SetSelected(true);
            part.BeginDragPreview();
            PrototypeUI.SetTrayDragFeedback(
                part.PartId,
                part.Definition.AssemblyId,
                screenPosition);
            MechMasterApp.Instance.SelectPart(part.PartId);
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
            if (activePart == null)
            {
                return;
            }

            currentPosition = screenPosition;
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
            if (activePart == null)
            {
                IsDraggingPart = false;
                return;
            }

            MechanicalPartView releasedPart = activePart;
            string hoveredAssemblyId = PrototypeUI.HoveredTrayAssemblyId;
            bool draggedFarEnough = Vector2.Distance(pressPosition, screenPosition) >= DragThreshold;
            bool correctTray = string.Equals(
                releasedPart.Definition.AssemblyId,
                hoveredAssemblyId,
                StringComparison.Ordinal);
            activePart = null;
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

            if (string.IsNullOrEmpty(hoveredAssemblyId))
            {
                MechMasterApp.Instance.Operate(releasedPart.PartId);
            }
            else
            {
                MechMasterApp.Instance.RejectAssemblyDrop(releasedPart.PartId);
            }
        }

        private void OnDisable()
        {
            if (activePart != null)
            {
                activePart.EndDragPreview();
                activePart.SetSelected(false);
            }

            activePart = null;
            IsDraggingPart = false;
            hasDragPlane = false;
            PrototypeUI.ClearTrayDragFeedback();
            if (MechMasterApp.Instance != null)
            {
                MechMasterApp.Instance.SetTrayHover(null, false, false);
            }
        }
    }
}
