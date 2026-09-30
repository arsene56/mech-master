using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MechMaster.Runtime
{
    [Serializable]
    public sealed class CarbonSuspensionBinding
    {
        public string objectName, role;
    }

    [Serializable]
    public sealed class CarbonSuspensionRig
    {
        public int schemaVersion;
        public string modelId, rearPivotAnchor, rearAxisAnchor, rearWheelAnchor,
            shockUpperAnchor, shockLowerAnchor, forkAxisStartAnchor, forkAxisEndAnchor;
        public float cycleSeconds, rearMaximumDegrees, forkCompressionM, shockEyeDistanceM;
        public CarbonSuspensionBinding[] bindings;
    }

    // Fixed-frame geometric suspension demonstration. The shock stays attached
    // at both eyes: lower body follows the swingarm, upper rod follows the frame.
    // No reparenting, source asset mutation, pressure or ride/contact simulation.
    public sealed class CarbonFrameBikeMotionController : MonoBehaviour, IMechanicalMotionController
    {
        // The authored rig stays at four timeline seconds; its former 2.00x
        // playback is now the displayed 1.00x default (two real seconds/cycle).
        private const double DefaultTimelineRate = 2.0;
        private const string ChainObjectName = "MM_carbon_n0594_chain";
        private const string ChainringObjectName = "MM_carbon_n0572_chainring";

        private sealed class Pose
        {
            public Transform Transform;
            public string Role;
            public Vector3 LocalPosition, RestPosition;
            public Quaternion LocalRotation, RestRotation;
            public Renderer Renderer;
            public bool RendererEnabled;
        }

        private CarbonSuspensionRig rig;
        private Pose[] poses;
        private Vector3 pivot, axis, upper, lower, forkDirection;
        private Collider[] colliders;
        private bool[] colliderEnabled;
        private double elapsed;
        private MeshFilter chainFilter;
        private Mesh sourceChainMesh, animatedChainMesh;
        private Vector3[] chainRestVertices, chainRestNormals, chainRootVertices, chainRootNormals,
            chainAnimatedVertices, chainAnimatedNormals;
        private float[] chainRearWeights;
        private Matrix4x4 chainRootToLocal;

        public bool IsReady { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsPlaying { get; private set; }
        public int Speed { get; private set; } = 100;
        public float CyclePhase => rig == null ? 0 : (float)(elapsed / rig.cycleSeconds);
        public float RearDegrees { get; private set; }
        public float ForkCompressionM { get; private set; }
        public float ShockCompressionM { get; private set; }
        public Vector3 MovingShockLower => pivot + Quaternion.AngleAxis(RearDegrees, axis) * (lower - pivot);

        public bool Initialize(string resourcePath, string modelId)
        {
            try
            {
                TextAsset asset = Resources.Load<TextAsset>(resourcePath);
                if (asset == null) throw new InvalidOperationException("缺少软尾悬架配置");
                rig = JsonUtility.FromJson<CarbonSuspensionRig>(asset.text);
                bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
                if (rig == null || rig.schemaVersion != 1 || rig.modelId != modelId
                    || !Finite(rig.cycleSeconds) || rig.cycleSeconds <= 0
                    || !Finite(rig.rearMaximumDegrees) || rig.rearMaximumDegrees <= 0 || rig.rearMaximumDegrees > 10
                    || !Finite(rig.forkCompressionM) || rig.forkCompressionM <= 0 || rig.forkCompressionM > .06f
                    || !Finite(rig.shockEyeDistanceM) || Mathf.Abs(rig.shockEyeDistanceM - .2f) > .00015f
                    || rig.bindings == null || rig.bindings.Length != 307)
                    throw new InvalidOperationException("软尾悬架配置无效");
                var named = GetComponentsInChildren<Transform>(true)
                    .Where(item => item.name.StartsWith("MM_carbon_rig_", StringComparison.Ordinal))
                    .GroupBy(item => item.name).ToDictionary(group => group.Key, group => group.Single());
                var geometry = GetComponentsInChildren<MeshFilter>(true)
                    .Where(mesh => mesh.name.StartsWith("MM_carbon_n", StringComparison.Ordinal))
                    .ToDictionary(mesh => mesh.name, mesh => mesh.transform);
                var roles = new HashSet<string> { "fixed", "rear", "front_lower", "shock_upper", "shock_lower", "hidden" };
                if (geometry.Count != 307 || rig.bindings.Select(b => b.objectName).Distinct().Count() != 307
                    || !new HashSet<string>(rig.bindings.Select(b => b.objectName)).SetEquals(geometry.Keys)
                    || rig.bindings.Any(b => !roles.Contains(b.role)) || !roles.SetEquals(rig.bindings.Select(b => b.role)))
                    throw new InvalidOperationException("软尾分件绑定不一致");
                Vector3 Anchor(string name)
                {
                    if (string.IsNullOrWhiteSpace(name) || !named.ContainsKey(name))
                        throw new InvalidOperationException("缺少源装配转轴标记");
                    return transform.InverseTransformPoint(named[name].position);
                }
                pivot = Anchor(rig.rearPivotAnchor);
                axis = Anchor(rig.rearAxisAnchor) - pivot;
                upper = Anchor(rig.shockUpperAnchor); lower = Anchor(rig.shockLowerAnchor);
                forkDirection = Anchor(rig.forkAxisEndAnchor) - Anchor(rig.forkAxisStartAnchor);
                if (Mathf.Abs(axis.magnitude - .05f) > .0001f || forkDirection.magnitude < .15f
                    || Mathf.Abs(Vector3.Distance(upper, lower) - rig.shockEyeDistanceM) > .00001f)
                    throw new InvalidOperationException("软尾转轴尺度或后避震安装轴距无效");
                axis.Normalize(); forkDirection.Normalize();
                // FBX changes handedness. Derive the compression sign from the
                // actual rear axle moving upwards, not a hardcoded Euler sign.
                Vector3 wheelDelta = Anchor(rig.rearWheelAnchor) - pivot;
                if (Vector3.Dot(Quaternion.AngleAxis(1, axis) * wheelDelta - wheelDelta, Vector3.up) < 0) axis = -axis;
                if (forkDirection.y < .7f || Mathf.Abs(axis.y) > .01f)
                    throw new InvalidOperationException("软尾源装配悬架方向无效");
                Vector3 fullLower = pivot + Quaternion.AngleAxis(rig.rearMaximumDegrees, axis) * (lower - pivot);
                float compression = rig.shockEyeDistanceM - Vector3.Distance(upper, fullLower);
                if (compression <= .005f || compression > .035f)
                    throw new InvalidOperationException("后避震教学行程超出保守范围");
                poses = rig.bindings.Select(b => {
                    Transform item = geometry[b.objectName];
                    Renderer renderer = item.GetComponent<Renderer>();
                    if (renderer == null || renderer.sharedMaterials.Any(mat => mat == null))
                        throw new InvalidOperationException("缺少软尾分件材质");
                    return new Pose { Transform = item, Role = b.role, LocalPosition = item.localPosition,
                        LocalRotation = item.localRotation, RestPosition = transform.InverseTransformPoint(item.position),
                        RestRotation = Quaternion.Inverse(transform.rotation) * item.rotation, Renderer = renderer };
                }).ToArray();
                InitializeChain(geometry, Anchor(rig.rearWheelAnchor));
                IsReady = true;
                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning("软尾悬架演示绑定失败：" + error.Message);
                IsReady = false;
                return false;
            }
        }

        public void SetSpeed(int value) => Speed = Mathf.Clamp(value, 50, 150);

        public void Play()
        {
            if (!IsReady || IsPlaying) return;
            if (!IsActive)
            {
                elapsed = 0;
                colliders = GetComponentsInChildren<Collider>(true);
                colliderEnabled = colliders.Select(c => c.enabled).ToArray();
                foreach (Collider collider in colliders) collider.enabled = false;
                foreach (Pose pose in poses)
                {
                    pose.RendererEnabled = pose.Renderer.enabled;
                    if (pose.Role == "hidden") pose.Renderer.enabled = false;
                }
                IsActive = true;
                ApplyPhase(0);
            }
            IsPlaying = true;
        }

        public void Pause() { if (IsActive) IsPlaying = false; }

        public void Stop()
        {
            if (!IsActive) return;
            IsActive = IsPlaying = false;
            RestoreTransforms();
            foreach (Pose pose in poses) if (pose.Renderer != null) pose.Renderer.enabled = pose.RendererEnabled;
            for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = colliderEnabled[i];
            elapsed = 0;
        }

        private void RestoreTransforms()
        {
            foreach (Pose pose in poses)
                if (pose.Transform != null)
                {
                    pose.Transform.localPosition = pose.LocalPosition;
                    pose.Transform.localRotation = pose.LocalRotation;
                }
            RearDegrees = ForkCompressionM = ShockCompressionM = 0;
            if (animatedChainMesh != null)
            {
                animatedChainMesh.SetVertices(chainRestVertices);
                animatedChainMesh.SetNormals(chainRestNormals);
                animatedChainMesh.RecalculateBounds();
            }
        }

        private void InitializeChain(Dictionary<string, Transform> geometry, Vector3 rearAxle)
        {
            if (!geometry.TryGetValue(ChainObjectName, out Transform chain)
                || !geometry.TryGetValue(ChainringObjectName, out Transform chainring)
                || !rig.bindings.Any(binding => binding.objectName == ChainObjectName && binding.role == "fixed"))
                throw new InvalidOperationException("缺少可见链条或牙盘绑定");
            chainFilter = chain.GetComponent<MeshFilter>();
            sourceChainMesh = chainFilter == null ? null : chainFilter.sharedMesh;
            if (sourceChainMesh == null || !sourceChainMesh.isReadable)
                throw new InvalidOperationException("链条网格未启用 CPU 读取，请重新导入 chain_guide_LOD0.fbx");
            Vector3 front = transform.InverseTransformPoint(chainring.position);
            Vector3 frontToRear = rearAxle - front;
            if (frontToRear.sqrMagnitude < .04f)
                throw new InvalidOperationException("链条前后锚点距离无效");

            chainRestVertices = sourceChainMesh.vertices;
            chainRestNormals = sourceChainMesh.normals;
            if (chainRestNormals.Length != chainRestVertices.Length)
                throw new InvalidOperationException("链条网格法线不完整");
            chainRootVertices = new Vector3[chainRestVertices.Length];
            chainRootNormals = new Vector3[chainRestVertices.Length];
            chainAnimatedVertices = new Vector3[chainRestVertices.Length];
            chainAnimatedNormals = new Vector3[chainRestVertices.Length];
            chainRearWeights = new float[chainRestVertices.Length];
            Matrix4x4 localToRoot = transform.worldToLocalMatrix * chain.localToWorldMatrix;
            chainRootToLocal = localToRoot.inverse;
            for (int i = 0; i < chainRestVertices.Length; i++)
            {
                Vector3 point = localToRoot.MultiplyPoint3x4(chainRestVertices[i]);
                chainRootVertices[i] = point;
                chainRootNormals[i] = localToRoot.MultiplyVector(chainRestNormals[i]).normalized;
                float along = Vector3.Dot(point - front, frontToRear) / frontToRear.sqrMagnitude;
                // Preserve the front wrap, move the rear cassette wrap rigidly,
                // and blend the two straight spans of the one-piece source mesh.
                chainRearWeights[i] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.2f, .8f, along));
            }
            animatedChainMesh = Instantiate(sourceChainMesh);
            animatedChainMesh.name = sourceChainMesh.name + " (suspension preview)";
            animatedChainMesh.MarkDynamic();
            chainFilter.sharedMesh = animatedChainMesh;
        }

        private void ApplyChain(Quaternion rear)
        {
            if (animatedChainMesh == null) return;
            for (int i = 0; i < chainRootVertices.Length; i++)
            {
                float weight = chainRearWeights[i];
                if (weight <= 0f)
                {
                    chainAnimatedVertices[i] = chainRestVertices[i];
                    chainAnimatedNormals[i] = chainRestNormals[i];
                    continue;
                }
                Vector3 source = chainRootVertices[i];
                Vector3 rearPoint = pivot + rear * (source - pivot);
                chainAnimatedVertices[i] = chainRootToLocal.MultiplyPoint3x4(Vector3.Lerp(source, rearPoint, weight));
                Vector3 sourceNormal = chainRootNormals[i];
                Vector3 rearNormal = rear * sourceNormal;
                chainAnimatedNormals[i] = chainRootToLocal.MultiplyVector(
                    Vector3.Lerp(sourceNormal, rearNormal, weight)).normalized;
            }
            animatedChainMesh.SetVertices(chainAnimatedVertices);
            animatedChainMesh.SetNormals(chainAnimatedNormals);
            animatedChainMesh.RecalculateBounds();
        }

        public Vector3[] GetFramingPoints()
        {
            if (!IsReady || IsActive) return Array.Empty<Vector3>();
            var points = new List<Vector3>();
            for (int sample = 0; sample <= 8; sample++)
            {
                ApplyPhase(sample / 16f);
                foreach (Pose pose in poses)
                {
                    Bounds bounds = pose.Renderer.bounds;
                    for (int corner = 0; corner < 8; corner++)
                        points.Add(bounds.center + Vector3.Scale(bounds.extents,
                            new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                }
            }
            RestoreTransforms();
            return points.ToArray();
        }

        private void OnDisable() => Stop();
        private void OnDestroy()
        {
            if (animatedChainMesh == null) return;
            if (chainFilter != null && chainFilter.sharedMesh == animatedChainMesh)
                chainFilter.sharedMesh = sourceChainMesh;
            Destroy(animatedChainMesh);
        }
        private void Update() => Advance(Time.deltaTime);

        private void Advance(float deltaTime)
        {
            if (!IsPlaying || deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            elapsed = (elapsed + deltaTime * DefaultTimelineRate * Speed / 100.0) % rig.cycleSeconds;
            ApplyPhase(CyclePhase);
        }

        private void ApplyPhase(float phase)
        {
            float amount = (float)SuspensionTeachingCycle.CompressionFraction(phase);
            RearDegrees = rig.rearMaximumDegrees * amount;
            ForkCompressionM = rig.forkCompressionM * amount;
            Quaternion rear = Quaternion.AngleAxis(RearDegrees, axis);
            Vector3 movingLower = pivot + rear * (lower - pivot);
            Quaternion shock = Quaternion.FromToRotation(upper - lower, upper - movingLower);
            ShockCompressionM = rig.shockEyeDistanceM - Vector3.Distance(upper, movingLower);
            foreach (Pose pose in poses)
            {
                Vector3 position = pose.RestPosition;
                Quaternion rotation = pose.RestRotation;
                if (pose.Role == "rear") { position = pivot + rear * (position - pivot); rotation = rear * rotation; }
                else if (pose.Role == "front_lower") position += forkDirection * ForkCompressionM;
                else if (pose.Role == "shock_upper") { position = upper + shock * (position - upper); rotation = shock * rotation; }
                else if (pose.Role == "shock_lower") { position = movingLower + shock * (position - lower); rotation = shock * rotation; }
                pose.Transform.SetPositionAndRotation(transform.TransformPoint(position), transform.rotation * rotation);
            }
            ApplyChain(rear);
        }
    }
}
