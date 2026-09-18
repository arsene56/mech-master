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
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 100f))
            {
                return;
            }

            MechanicalPartView part = hit.collider.GetComponentInParent<MechanicalPartView>();
            if (part == null)
            {
                return;
            }

            activePart = part;
            pressPosition = screenPosition;
            IsDraggingPart = true;
            part.SetSelected(true);
            MechMasterApp.Instance.SelectPart(part.PartId);
        }

        private void EndPointer(Vector2 screenPosition)
        {
            if (activePart == null)
            {
                IsDraggingPart = false;
                return;
            }

            MechanicalPartView releasedPart = activePart;
            activePart = null;
            IsDraggingPart = false;
            releasedPart.SetSelected(false);

            if (Vector2.Distance(pressPosition, screenPosition) >= DragThreshold)
            {
                MechMasterApp.Instance.Operate(releasedPart.PartId);
            }
        }

        private void OnDisable()
        {
            if (activePart != null)
            {
                activePart.SetSelected(false);
            }

            activePart = null;
            IsDraggingPart = false;
        }
    }
}
