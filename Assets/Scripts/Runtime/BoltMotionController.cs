using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MechMaster.Runtime
{
    [Serializable]
    public sealed class BoltJointDefinition
    {
        public string id, displayName, parentId, pivotObject, axisStartObject, axisEndObject, axisObject;
        public bool passive;
        public int axisSign;
        public float minimumDegrees, maximumDegrees;
    }

    [Serializable]
    public sealed class BoltMotionBinding
    {
        public string objectName, jointId;
    }

    [Serializable]
    public sealed class BoltMotionKeyframe
    {
        public float phase;
        public float[] angles;
    }

    [Serializable]
    public sealed class BoltMotionRig
    {
        public int schemaVersion;
        public string modelId;
        public float cycleSeconds;
        public BoltJointDefinition[] joints;
        public BoltMotionBinding[] bindings;
        public BoltMotionKeyframe[] keyframes;
    }

    // The body stays at its source pose. Each side has three active joints and
    // a prescribed passive ankle pose, derived from the actual CAD shafts.
    // Rest-based transforms prevent drift and never alter disassembly progress.
    public sealed class BoltMotionController : MonoBehaviour, IMechanicalMotionController
    {
        // User-facing 1x advances the authored rig timeline at twice its rate.
        private const double DefaultTimelineRate = 2.0;

        private sealed class Pose
        {
            public Transform Transform;
            public Vector3 LocalPosition, RestPosition;
            public Quaternion LocalRotation, RestRotation;
            public Vector3[] RestCorners;
            public int Joint;
        }

        private sealed class Joint
        {
            public Vector3 Pivot, Axis;
            public int Parent;
            public Matrix4x4 Matrix;
            public Quaternion Rotation;
        }

        private BoltMotionRig rig;
        private PeriodicMotionCurve motionCurve;
        private Joint[] joints;
        private Pose[] poses;
        private float[] angles;
        private Collider[] colliders;
        private bool[] colliderEnabled;
        private double elapsed;

        public bool IsReady { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsPlaying { get; private set; }
        public int Speed { get; private set; } = 100;
        public float CyclePhase => rig == null ? 0 : (float)(elapsed / rig.cycleSeconds);
        public IReadOnlyList<float> JointAngles => angles;

        public bool Initialize(string resourcePath, string modelId)
        {
            try
            {
                TextAsset asset = Resources.Load<TextAsset>(resourcePath);
                if (asset == null) throw new InvalidOperationException("缺少 Bolt 关节配置");
                rig = JsonUtility.FromJson<BoltMotionRig>(asset.text);
                if (rig == null || rig.schemaVersion != 1 || rig.modelId != modelId || rig.cycleSeconds <= 0
                    || rig.joints == null || rig.joints.Length != 8 || rig.joints.Count(joint => joint.passive) != 2
                    || rig.bindings == null || rig.bindings.Length != 345
                    || rig.keyframes == null || rig.keyframes.Length < 2)
                    throw new InvalidOperationException("Bolt 关节配置格式无效");
                var named = GetComponentsInChildren<MeshFilter>(true)
                    .Where(mesh => mesh.name.StartsWith("MM_bolt_", StringComparison.Ordinal))
                    .ToDictionary(mesh => mesh.name, mesh => mesh.transform, StringComparer.Ordinal);
                if (rig.bindings.Select(binding => binding.objectName).Distinct().Count() != named.Count
                    || !new HashSet<string>(rig.bindings.Select(binding => binding.objectName)).SetEquals(named.Keys))
                    throw new InvalidOperationException("Bolt 关节与分件绑定不一致");
                var indices = new Dictionary<string, int>(StringComparer.Ordinal) { { "fixed", -1 } };
                joints = new Joint[rig.joints.Length];
                angles = new float[joints.Length];
                for (int index = 0; index < joints.Length; index++)
                {
                    BoltJointDefinition definition = rig.joints[index];
                    int parent = string.IsNullOrEmpty(definition.parentId) ? -1 : indices[definition.parentId];
                    Vector3 axis;
                    if (!string.IsNullOrEmpty(definition.axisObject))
                    {
                        Transform shaft = named[definition.axisObject];
                        Vector3 size = shaft.GetComponent<MeshFilter>().sharedMesh.bounds.size;
                        Vector3 local = size.x >= size.y && size.x >= size.z ? Vector3.right
                            : size.y >= size.z ? Vector3.up : Vector3.forward;
                        if (Mathf.Max(size.x, size.y, size.z) < .02f)
                            throw new InvalidOperationException("被动踝轴尺寸无效");
                        axis = transform.InverseTransformDirection(shaft.TransformDirection(local));
                        // A symmetric shaft's positive direction is ambiguous
                        // after FBX axis conversion. Align the passive hinge
                        // with its parent pitch axis for compensating poses.
                        if (parent < 0) throw new InvalidOperationException("被动踝缺少父关节");
                        axis *= Vector3.Dot(axis, joints[parent].Axis) < 0 ? -1 : 1;
                    }
                    else axis = Center(named[definition.axisEndObject]) - Center(named[definition.axisStartObject]);
                    if (axis.magnitude < .002f || Mathf.Abs(definition.axisSign) != 1
                        || definition.minimumDegrees >= definition.maximumDegrees
                        || definition.passive && (parent < 0 || joints[parent].Parent < 0))
                        throw new InvalidOperationException("Bolt 关节轴无效：" + definition.id);
                    joints[index] = new Joint { Parent = parent, Pivot = Center(named[definition.pivotObject]),
                        Axis = axis.normalized * (definition.passive ? 1 : definition.axisSign), Matrix = Matrix4x4.identity,
                        Rotation = Quaternion.identity };
                    indices.Add(definition.id, index);
                }
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
                        RestCorners = corners, Joint = indices[binding.jointId] };
                }).ToArray();
                for (int index = 0; index < rig.keyframes.Length; index++)
                {
                    BoltMotionKeyframe frame = rig.keyframes[index];
                    if (frame.angles == null || frame.angles.Length != joints.Length
                        || frame.phase < 0 || frame.phase > 1 || float.IsNaN(frame.phase)
                        || index > 0 && frame.phase <= rig.keyframes[index - 1].phase)
                        throw new InvalidOperationException("Bolt 演示关键帧无效");
                    for (int joint = 0; joint < joints.Length; joint++)
                    {
                        if (float.IsNaN(frame.angles[joint]) || frame.angles[joint] < rig.joints[joint].minimumDegrees
                            || frame.angles[joint] > rig.joints[joint].maximumDegrees)
                            throw new InvalidOperationException("Bolt 演示超出教学角度范围");
                        if (rig.joints[joint].passive)
                        {
                            int knee = joints[joint].Parent, hip = joints[knee].Parent;
                            if (Mathf.Abs(frame.angles[joint] + frame.angles[knee] + frame.angles[hip]) > .0001f)
                                throw new InvalidOperationException("Bolt 被动踝关键帧与髋膝补偿不一致");
                        }
                    }
                }
                if (rig.keyframes[0].phase != 0 || rig.keyframes[rig.keyframes.Length - 1].phase != 1
                    || rig.keyframes[0].angles.Any(angle => angle != 0)
                    || rig.keyframes[rig.keyframes.Length - 1].angles.Any(angle => angle != 0))
                    throw new InvalidOperationException("Bolt 演示循环未闭合");
                motionCurve = new PeriodicMotionCurve(rig.keyframes.Select(frame => frame.phase).ToArray(),
                    rig.keyframes.Select(frame => frame.angles).ToArray());
                IsReady = true;
                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning("Bolt 动态演示绑定失败：" + error.Message);
                IsReady = false;
                return false;
            }
        }

        private Vector3 Center(Transform item) => transform.InverseTransformPoint(
            item.TransformPoint(item.GetComponent<MeshFilter>().sharedMesh.bounds.center));

        public void SetSpeed(int value) => Speed = Mathf.Clamp(value, 50, 150);

        public Vector3[] GetFramingPoints()
        {
            if (!IsReady) return Array.Empty<Vector3>();
            Bounds bounds = new Bounds(poses[0].RestPosition, Vector3.zero);
            for (int sample = 0; sample <= 60; sample++)
            {
                CalculateMatrices(sample / 60f);
                foreach (Pose pose in poses)
                    foreach (Vector3 corner in pose.RestCorners)
                        bounds.Encapsulate(PoseMatrix(pose).MultiplyPoint3x4(corner));
            }
            CalculateMatrices(0);
            bounds.Expand(bounds.size * .04f);
            var points = new Vector3[8];
            for (int corner = 0; corner < 8; corner++)
                points[corner] = transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents,
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
            Array.Clear(angles, 0, angles.Length);
        }

        private void OnDisable() => Stop();
        private void Update() => Advance(Time.deltaTime);

        private void Advance(float deltaTime)
        {
            if (!IsPlaying || deltaTime <= 0) return;
            elapsed = (elapsed + deltaTime * DefaultTimelineRate * Speed / 100.0) % rig.cycleSeconds;
            ApplyPhase(CyclePhase);
        }

        private void ApplyPhase(float phase)
        {
            CalculateMatrices(phase);
            foreach (Pose pose in poses)
            {
                Quaternion rotation = pose.Joint < 0 ? Quaternion.identity : joints[pose.Joint].Rotation;
                pose.Transform.SetPositionAndRotation(transform.TransformPoint(PoseMatrix(pose).MultiplyPoint3x4(pose.RestPosition)),
                    transform.rotation * rotation * pose.RestRotation);
            }
        }

        private Matrix4x4 PoseMatrix(Pose pose) => pose.Joint < 0 ? Matrix4x4.identity : joints[pose.Joint].Matrix;

        private void CalculateMatrices(float phase)
        {
            motionCurve.Evaluate(phase, angles);
            for (int index = 0; index < joints.Length; index++)
            {
                Joint joint = joints[index];
                // Compensate the interpolated hip/knee pitch, not a separately
                // eased ankle curve. This keeps the passive foot pose coherent.
                if (rig.joints[index].passive)
                    angles[index] = -(angles[joint.Parent] + angles[joints[joint.Parent].Parent]);
                Quaternion rotation = Quaternion.AngleAxis(angles[index], joint.Axis);
                joint.Rotation = (joint.Parent < 0 ? Quaternion.identity : joints[joint.Parent].Rotation) * rotation;
                joint.Matrix = (joint.Parent < 0 ? Matrix4x4.identity : joints[joint.Parent].Matrix)
                    * Matrix4x4.TRS(joint.Pivot, rotation, Vector3.one) * Matrix4x4.Translate(-joint.Pivot);
            }
        }
    }
}
