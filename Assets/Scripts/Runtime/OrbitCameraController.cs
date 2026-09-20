using MechMaster.Runtime.UI;
using UnityEngine;

namespace MechMaster.Runtime
{
    public sealed class OrbitCameraController : MonoBehaviour
    {
        private Transform target;
        private Camera sceneCamera;
        private Bounds assembledBounds;
        private Vector3[] framingPoints;
        private Vector3[] activeFramingPoints;
        private bool dirty = true;
        private Rect lastArea;
        private Vector2Int lastSize;
        private float yaw = -22f;
        private float pitch = 14f;
        private float zoom = 1f;
        private Vector2 panOffset;
        public Vector2 Angles => new Vector2(yaw, pitch);
        public float ZoomFactor => zoom;
        public Vector2 PanOffset => panOffset;

        // Screen-space image movement in reference pixels, independent of orbit and zoom.
        public void Pan(Vector2 delta)
        {
            panOffset += delta;
            Rect area = WorkshopLayout.ModelArea;
            float maxX = Mathf.Max(0f, area.width * .45f);
            float maxY = Mathf.Max(0f, area.height * .45f);
            panOffset.x = Mathf.Clamp(panOffset.x, -maxX, maxX);
            panOffset.y = Mathf.Clamp(panOffset.y, -maxY, maxY);
            dirty = true;
            ApplyTransform();
        }

        public void ResetPan()
        {
            panOffset = Vector2.zero;
            dirty = true;
            ApplyTransform();
        }

        public void Initialize(Transform orbitTarget, Bounds modelBounds, Vector3[] points)
        {
            target = orbitTarget;
            assembledBounds = modelBounds;
            framingPoints = points;
            sceneCamera = GetComponent<Camera>();
            FrameWholeBike();
        }

        public void Rotate(Vector2 pixelDelta)
        {
            float scale = new PixelUILayout(Screen.width, Screen.height).Scale;
            yaw += pixelDelta.x / scale * 0.18f;
            pitch = Mathf.Clamp(pitch - pixelDelta.y / scale * 0.14f, -70f, 75f);
            dirty = true;
            ApplyTransform();
        }

        public void Zoom(float delta)
        {
            if (Mathf.Approximately(delta, 0f)) return;
            zoom = Mathf.Clamp(zoom * Mathf.Exp(-delta), 0.4f, 2.5f);
            dirty = true;
            ApplyTransform();
        }

        public void FrameWholeBike()
        {
            activeFramingPoints = framingPoints;
            if (target != null) target.position = assembledBounds.center;
            yaw = -22f;
            pitch = 14f;
            zoom = 1f;
            panOffset = Vector2.zero;
            dirty = true;
            ApplyTransform();
        }

        public void FrameContents(Vector3[] points)
        {
            if (points == null || points.Length == 0 || target == null) return;
            Bounds bounds = new Bounds(points[0], Vector3.zero);
            foreach (Vector3 point in points) bounds.Encapsulate(point);
            target.position = bounds.center;
            activeFramingPoints = points;
            yaw = -22f;
            pitch = 24f;
            zoom = 1f;
            panOffset = Vector2.zero;
            dirty = true;
            ApplyTransform();
        }

        private void LateUpdate() => ApplyTransform();

        private void ApplyTransform()
        {
            if (target == null || sceneCamera == null) return;
            Rect area = new PixelUILayout(Screen.width, Screen.height).ToPixels(WorkshopLayout.ModelArea);
            Vector2Int size = new Vector2Int(Screen.width, Screen.height);
            if (!dirty && area == lastArea && size == lastSize) return;
            dirty = false;
            lastArea = area;
            lastSize = size;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            float tanVertical = Mathf.Tan(sceneCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float tanHorizontal = tanVertical * sceneCamera.aspect;
            float usableX = tanHorizontal * area.width / Screen.width * 0.94f;
            float usableY = tanVertical * area.height / Screen.height * 0.94f;
            float fitDistance = 0.8f;
            Quaternion inverse = Quaternion.Inverse(rotation);
            int count = activeFramingPoints == null ? 8 : activeFramingPoints.Length;
            for (int i = 0; i < count; i++)
            {
                Vector3 corner = activeFramingPoints != null ? activeFramingPoints[i] : assembledBounds.center + Vector3.Scale(assembledBounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 local = inverse * (corner - target.position);
                fitDistance = Mathf.Max(fitDistance,
                    Mathf.Max(Mathf.Abs(local.x) / usableX, Mathf.Abs(local.y) / usableY) - local.z);
            }
            transform.SetPositionAndRotation(target.position + rotation * new Vector3(0, 0, -fitDistance * zoom), rotation);

            // Keep a full-screen camera: grabbed parts must remain visible over the tray.
            // Shift the optical center into the free workspace instead of behind the panels.
            sceneCamera.ResetProjectionMatrix();
            Matrix4x4 projection = sceneCamera.projectionMatrix;
            projection.m02 = 1f - 2f * area.center.x / Screen.width;
            projection.m12 = 2f * area.center.y / Screen.height - 1f;
            float scale = new PixelUILayout(Screen.width, Screen.height).Scale;
            projection.m02 -= 2f * panOffset.x * scale / Screen.width;
            projection.m12 -= 2f * panOffset.y * scale / Screen.height;
            sceneCamera.projectionMatrix = projection;
        }

        private void OnDisable()
        {
            if (sceneCamera != null) sceneCamera.ResetProjectionMatrix();
        }
    }
}
