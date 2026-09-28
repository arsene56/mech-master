using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MechMaster.Runtime
{
    [Serializable]
    public sealed class MoveoJointDefinition
    {
        public string id, displayName, parentId, pivotObject, axisStartObject, axisEndObject;
        public float minimumDegrees, maximumDegrees;
    }

    [Serializable]
    public sealed class MoveoJawDefinition
    {
        public string driverPivotObject, driverTipObject, followerPivotObject, followerTipObject;
        public int direction;
    }

    [Serializable]
    public sealed class MoveoGripperDefinition
    {
        public string parentId, axisStartObject, axisEndObject;
        public float minimumDegrees, maximumDegrees;
        public MoveoJawDefinition left, right;
    }

    [Serializable]
    public sealed class MoveoMotionBinding
    {
        public string objectName, jointId, gripperGroup;
    }

    [Serializable]
    public sealed class MoveoMotionKeyframe
    {
        public float phase, gripperDegrees;
        public float[] angles;
    }

    [Serializable]
    public sealed class MoveoMotionRig
    {
        public int schemaVersion;
        public string modelId;
        public float cycleSeconds;
        public MoveoJointDefinition[] joints;
        public MoveoGripperDefinition gripper;
        public MoveoMotionBinding[] bindings;
        public MoveoMotionKeyframe[] keyframes;
    }

    // Apply serial joint transforms to the flat disassembly nodes without
    // reparenting them. Each frame starts from the original pose, avoiding drift.
    public sealed class MoveoMotionController : MonoBehaviour, IMechanicalMotionController
    {
        private sealed class Pose
        {
            public Transform Transform;
            public Vector3 LocalPosition, RestPosition;
            public Quaternion LocalRotation, RestRotation;
            public int Joint, Gripper;
            public Vector3[] RestCorners;
        }

        private sealed class Joint
        {
            public Vector3 Pivot, Axis;
            public int Parent;
            public Matrix4x4 Matrix;
            public Quaternion Rotation;
        }

        // Solve the four-bar closure, retaining the assembly's original branch.
        // The finger remains a rigid link between the two moving pivot pins.
        private sealed class Jaw
        {
            public Vector3 Pivot, Tip, FollowerPivot, FollowerTip, Axis;
            public float FollowerRadius, FingerRadius, Branch;
            public int Direction;

            public bool Solve(float degrees, out Matrix4x4 driver,
                out Matrix4x4 follower, out Matrix4x4 finger,
                out Quaternion driverRotation, out Quaternion followerRotation, out Quaternion fingerRotation)
            {
                driverRotation = Quaternion.AngleAxis(degrees * Direction, Axis);
                driver = Around(Pivot, driverRotation);
                Vector3 tip = driver.MultiplyPoint3x4(Tip);
                Vector3 planePivot = FollowerPivot + Axis * Vector3.Dot(Tip - FollowerPivot, Axis);
                Vector3 separation = tip - planePivot;
                float distance = separation.magnitude;
                follower = finger = Matrix4x4.identity;
                followerRotation = fingerRotation = Quaternion.identity;
                if (distance < 1e-6f) return false;
                float along = (FollowerRadius * FollowerRadius - FingerRadius * FingerRadius
                    + distance * distance) / (2f * distance);
                float heightSquared = FollowerRadius * FollowerRadius - along * along;
                if (heightSquared < -1e-7f) return false;
                Vector3 direction = separation / distance;
                Vector3 otherTip = planePivot + direction * along
                    + Vector3.Cross(Axis, direction) * (Branch * Mathf.Sqrt(Mathf.Max(0, heightSquared)))
                    + Axis * Vector3.Dot(FollowerTip - Tip, Axis);
                followerRotation = Quaternion.AngleAxis(Vector3.SignedAngle(
                    Vector3.ProjectOnPlane(FollowerTip - FollowerPivot, Axis),
                    Vector3.ProjectOnPlane(otherTip - FollowerPivot, Axis), Axis), Axis);
                follower = Around(FollowerPivot, followerRotation);
                fingerRotation = Quaternion.AngleAxis(Vector3.SignedAngle(
                    Vector3.ProjectOnPlane(FollowerTip - Tip, Axis),
                    Vector3.ProjectOnPlane(otherTip - tip, Axis), Axis), Axis);
                finger = Matrix4x4.TRS(tip - fingerRotation * Tip, fingerRotation, Vector3.one);
                return true;
            }
        }

        private MoveoMotionRig rig;
        private Joint[] joints;
        private Jaw[] jaws;
        private Pose[] poses;
        private float[] angles;
        private readonly Matrix4x4[] jawMatrices = new Matrix4x4[6];
        private readonly Quaternion[] jawRotations = new Quaternion[6];
        private Collider[] colliders;
        private bool[] colliderEnabled;
        private double elapsed;
        private int gripperParent;

        public bool IsReady { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsPlaying { get; private set; }
        public int Speed { get; private set; } = 100;
        public float CyclePhase => rig == null ? 0 : (float)(elapsed / rig.cycleSeconds);
        public float GripperDegrees { get; private set; }
        public IReadOnlyList<float> JointAngles => angles;

        public bool Initialize(string resourcePath, string modelId)
        {
            try
            {
                TextAsset asset = Resources.Load<TextAsset>(resourcePath);
                if (asset == null) throw new InvalidOperationException("缺少关节配置 " + resourcePath);
                rig = JsonUtility.FromJson<MoveoMotionRig>(asset.text);
                if (rig == null || rig.schemaVersion != 1 || rig.modelId != modelId
                    || rig.cycleSeconds <= 0 || rig.joints == null || rig.joints.Length != 5
                    || rig.bindings == null || rig.bindings.Length != 366
                    || rig.keyframes == null || rig.keyframes.Length < 2 || rig.gripper == null)
                    throw new InvalidOperationException("关节配置格式无效");
                var named = GetComponentsInChildren<MeshFilter>(true)
                    .Where(mesh => mesh.name.StartsWith("MM_moveo_", StringComparison.Ordinal))
                    .ToDictionary(mesh => mesh.name, mesh => mesh.transform, StringComparer.Ordinal);
                if (!new HashSet<string>(rig.bindings.Select(binding => binding.objectName))
                    .SetEquals(named.Keys) || rig.bindings.Select(binding => binding.objectName).Distinct().Count() != 366)
                    throw new InvalidOperationException("关节配置与拆装对象不一致");
                var indices = new Dictionary<string, int>(StringComparer.Ordinal) { { "fixed", -1 } };
                joints = new Joint[rig.joints.Length];
                angles = new float[joints.Length];
                for (int index = 0; index < joints.Length; index++)
                {
                    MoveoJointDefinition definition = rig.joints[index];
                    int parent = string.IsNullOrEmpty(definition.parentId) ? -1 : indices[definition.parentId];
                    Vector3 axis = Center(named[definition.axisEndObject]) - Center(named[definition.axisStartObject]);
                    if (axis.magnitude < .002f || definition.minimumDegrees >= definition.maximumDegrees)
                        throw new InvalidOperationException("无效关节轴 " + definition.id);
                    joints[index] = new Joint { Parent = parent, Pivot = Center(named[definition.pivotObject]),
                        Axis = axis.normalized, Matrix = Matrix4x4.identity, Rotation = Quaternion.identity };
                    indices.Add(definition.id, index);
                }
                gripperParent = indices[rig.gripper.parentId];
                Vector3 jawAxis = (Center(named[rig.gripper.axisEndObject])
                    - Center(named[rig.gripper.axisStartObject])).normalized;
                if (jawAxis.sqrMagnitude < .99f) throw new InvalidOperationException("无效夹爪轴");
                jaws = new[] { CreateJaw(rig.gripper.left, named, jawAxis), CreateJaw(rig.gripper.right, named, jawAxis) };
                var groups = new Dictionary<string, int>(StringComparer.Ordinal) {
                    { "", -1 }, { "left_driver", 0 }, { "left_follower", 1 }, { "left_finger", 2 },
                    { "right_driver", 3 }, { "right_follower", 4 }, { "right_finger", 5 } };
                poses = rig.bindings.Select(binding => {
                    Transform item = named[binding.objectName];
                    Bounds bounds = item.GetComponent<MeshFilter>().sharedMesh.bounds;
                    var corners = new Vector3[8];
                    for (int corner = 0; corner < 8; corner++)
                        corners[corner] = transform.InverseTransformPoint(item.TransformPoint(bounds.center
                            + Vector3.Scale(bounds.extents, new Vector3((corner & 1) == 0 ? -1 : 1,
                                (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1))));
                    return new Pose { Transform = item, LocalPosition = item.localPosition,
                        LocalRotation = item.localRotation, RestPosition = transform.InverseTransformPoint(item.position),
                        RestRotation = Quaternion.Inverse(transform.rotation) * item.rotation,
                        Joint = indices[binding.jointId], Gripper = groups[binding.gripperGroup ?? ""], RestCorners = corners };
                }).ToArray();
                for (int index = 0; index < rig.keyframes.Length; index++)
                {
                    MoveoMotionKeyframe frame = rig.keyframes[index];
                    if (frame.angles == null || frame.angles.Length != joints.Length
                        || (index > 0 && frame.phase <= rig.keyframes[index - 1].phase)
                        || frame.gripperDegrees < rig.gripper.minimumDegrees || frame.gripperDegrees > rig.gripper.maximumDegrees)
                        throw new InvalidOperationException("无效演示关键帧");
                    for (int joint = 0; joint < joints.Length; joint++)
                        if (frame.angles[joint] < rig.joints[joint].minimumDegrees
                            || frame.angles[joint] > rig.joints[joint].maximumDegrees)
                            throw new InvalidOperationException("演示超出关节范围");
                }
                if (rig.keyframes[0].phase != 0 || rig.keyframes[rig.keyframes.Length - 1].phase != 1)
                    throw new InvalidOperationException("演示循环不完整");
                // Check the whole permitted gripper range before enabling playback.
                for (int sample = 0; sample <= 40; sample++)
                    SolveJaws(Mathf.Lerp(rig.gripper.minimumDegrees, rig.gripper.maximumDegrees, sample / 40f));
                IsReady = true;
                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning("Moveo 动态演示绑定失败：" + error.Message);
                IsReady = false;
                return false;
            }
        }

        private Vector3 Center(Transform item) => transform.InverseTransformPoint(
            item.TransformPoint(item.GetComponent<MeshFilter>().sharedMesh.bounds.center));

        private Jaw CreateJaw(MoveoJawDefinition definition, Dictionary<string, Transform> named, Vector3 axis)
        {
            var jaw = new Jaw { Pivot = Center(named[definition.driverPivotObject]), Tip = Center(named[definition.driverTipObject]),
                FollowerPivot = Center(named[definition.followerPivotObject]), FollowerTip = Center(named[definition.followerTipObject]),
                Axis = axis, Direction = definition.direction };
            jaw.FollowerRadius = Vector3.ProjectOnPlane(jaw.FollowerTip - jaw.FollowerPivot, axis).magnitude;
            jaw.FingerRadius = Vector3.ProjectOnPlane(jaw.FollowerTip - jaw.Tip, axis).magnitude;
            jaw.Branch = Mathf.Sign(Vector3.Dot(axis, Vector3.Cross(jaw.Tip - jaw.FollowerPivot, jaw.FollowerTip - jaw.FollowerPivot)));
            if (jaw.FollowerRadius < .01f || jaw.FingerRadius < .005f || Mathf.Abs(jaw.Direction) != 1)
                throw new InvalidOperationException("无效夹爪连杆");
            return jaw;
        }

        public void SetSpeed(int value) => Speed = Mathf.Clamp(value, 50, 150);

        public Vector3[] GetFramingPoints()
        {
            if (!IsReady) return Array.Empty<Vector3>();
            Bounds sweep = new Bounds(poses[0].RestPosition, Vector3.zero);
            for (int sample = 0; sample <= 40; sample++)
            {
                CalculateMatrices(sample / 40f);
                foreach (Pose pose in poses)
                {
                    Matrix4x4 matrix = PoseMatrix(pose);
                    foreach (Vector3 corner in pose.RestCorners)
                        sweep.Encapsulate(matrix.MultiplyPoint3x4(corner));
                }
            }
            CalculateMatrices(0);
            sweep.Expand(sweep.size * .04f);
            var points = new Vector3[8];
            for (int corner = 0; corner < 8; corner++)
                points[corner] = transform.TransformPoint(sweep.center + Vector3.Scale(sweep.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
            return points;
        }

        public void Play()
        {
            if (!IsReady || IsPlaying) return;
            if (!IsActive)
            {
                elapsed = 0;
                colliders = GetComponentsInChildren<Collider>(true);
                colliderEnabled = new bool[colliders.Length];
                for (int index = 0; index < colliders.Length; index++)
                    if (colliders[index] != null)
                    {
                        colliderEnabled[index] = colliders[index].enabled;
                        colliders[index].enabled = false;
                    }
                ApplyPhase(0);
                IsActive = true;
            }
            IsPlaying = true;
        }

        public void Pause() { if (IsActive) IsPlaying = false; }

        public void Stop()
        {
            if (!IsActive) return;
            IsPlaying = IsActive = false;
            foreach (Pose pose in poses)
                if (pose.Transform != null)
                {
                    pose.Transform.localPosition = pose.LocalPosition;
                    pose.Transform.localRotation = pose.LocalRotation;
                }
            for (int index = 0; index < colliders.Length; index++)
                if (colliders[index] != null) colliders[index].enabled = colliderEnabled[index];
            elapsed = 0;
            GripperDegrees = 0;
            Array.Clear(angles, 0, angles.Length);
        }

        private void OnDisable() => Stop();
        private void Update() => Advance(Time.deltaTime);

        private void Advance(float deltaTime)
        {
            if (!IsPlaying || deltaTime <= 0) return;
            elapsed = (elapsed + deltaTime * Speed / 100.0) % rig.cycleSeconds;
            ApplyPhase(CyclePhase);
        }

        private void ApplyPhase(float phase)
        {
            CalculateMatrices(phase);
            foreach (Pose pose in poses)
            {
                Quaternion rotation = pose.Joint < 0 ? Quaternion.identity : joints[pose.Joint].Rotation;
                if (pose.Gripper >= 0) rotation = joints[gripperParent].Rotation * jawRotations[pose.Gripper];
                pose.Transform.SetPositionAndRotation(transform.TransformPoint(PoseMatrix(pose).MultiplyPoint3x4(pose.RestPosition)),
                    transform.rotation * rotation * pose.RestRotation);
            }
        }

        private Matrix4x4 PoseMatrix(Pose pose) => pose.Gripper >= 0
            ? joints[gripperParent].Matrix * jawMatrices[pose.Gripper]
            : pose.Joint < 0 ? Matrix4x4.identity : joints[pose.Joint].Matrix;

        private void CalculateMatrices(float phase)
        {
            int segment = 1;
            while (segment < rig.keyframes.Length - 1 && phase > rig.keyframes[segment].phase) segment++;
            MoveoMotionKeyframe from = rig.keyframes[segment - 1], to = rig.keyframes[segment];
            float amount = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from.phase, to.phase, phase));
            for (int index = 0; index < joints.Length; index++)
            {
                Joint joint = joints[index];
                angles[index] = Mathf.Lerp(from.angles[index], to.angles[index], amount);
                Quaternion rotation = Quaternion.AngleAxis(angles[index], joint.Axis);
                joint.Rotation = (joint.Parent < 0 ? Quaternion.identity : joints[joint.Parent].Rotation) * rotation;
                joint.Matrix = (joint.Parent < 0 ? Matrix4x4.identity : joints[joint.Parent].Matrix)
                    * Around(joint.Pivot, rotation);
            }
            GripperDegrees = Mathf.Lerp(from.gripperDegrees, to.gripperDegrees, amount);
            SolveJaws(GripperDegrees);
        }

        private void SolveJaws(float degrees)
        {
            for (int side = 0; side < 2; side++)
            {
                int index = side * 3;
                if (!jaws[side].Solve(degrees, out jawMatrices[index], out jawMatrices[index + 1], out jawMatrices[index + 2],
                    out jawRotations[index], out jawRotations[index + 1], out jawRotations[index + 2]))
                    throw new InvalidOperationException("夹爪四连杆无法闭合");
            }
        }

        private static Matrix4x4 Around(Vector3 pivot, Quaternion rotation) =>
            Matrix4x4.TRS(pivot, rotation, Vector3.one) * Matrix4x4.Translate(-pivot);
    }
}
