using UnityEngine;

namespace MechMaster.Runtime
{
    public sealed class OrbitCameraController : MonoBehaviour
    {
        private Transform target;
        private float yaw = -22f;
        private float pitch = 14f;
        private float distance = 2.35f;
        private float previousPinchDistance;

        public void Initialize(Transform orbitTarget)
        {
            target = orbitTarget;
            ApplyTransform();
        }

        private void Update()
        {
            if (target == null || PartInteractionController.IsDraggingPart)
            {
                return;
            }

            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Moved)
                {
                    yaw += touch.deltaPosition.x * 0.16f;
                    pitch -= touch.deltaPosition.y * 0.12f;
                }
            }
            else if (Input.touchCount >= 2)
            {
                float pinchDistance = Vector2.Distance(
                    Input.GetTouch(0).position,
                    Input.GetTouch(1).position);
                if (previousPinchDistance > 0f)
                {
                    distance -= (pinchDistance - previousPinchDistance) * 0.003f;
                }

                previousPinchDistance = pinchDistance;
            }
            else
            {
                previousPinchDistance = 0f;
                if (Input.GetMouseButton(1))
                {
                    yaw += Input.GetAxis("Mouse X") * 3.2f;
                    pitch -= Input.GetAxis("Mouse Y") * 2.4f;
                }

                distance -= Input.mouseScrollDelta.y * 0.18f;
            }

            pitch = Mathf.Clamp(pitch, -10f, 65f);
            distance = Mathf.Clamp(distance, 1.1f, 4.2f);
        }

        private void LateUpdate()
        {
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            if (target == null)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = target.position + rotation * new Vector3(0f, 0f, -distance);
            transform.rotation = rotation;
        }
    }
}

