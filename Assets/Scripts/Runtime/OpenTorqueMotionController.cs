using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace MechMaster.Runtime
{
    [Serializable]
    public sealed class OpenTorqueMotionBinding
    {
        public string objectName, role;
        public int planetIndex;
    }

    [Serializable]
    public sealed class OpenTorqueMotionRig
    {
        public int schemaVersion, sunTeeth, planetTeeth, ringTeeth, closedCycleInputTurns;
        public string modelId, axisStartObject, axisEndObject, sunObject;
        public float inputRpm, orbitRadiusM;
        public string[] planetObjects, transparentObjects;
        public OpenTorqueMotionBinding[] bindings;
    }

    // Continuous analytical gear motion, never independent eased keyframes.
    // Every transform is calculated from its original CAD pose without reparenting.
    public sealed class OpenTorqueMotionController : MonoBehaviour, IMechanicalMotionController
    {
        private sealed class Pose
        {
            public Transform Transform;
            public string Role;
            public Vector3 LocalPosition, RestPosition;
            public Quaternion LocalRotation, RestRotation;
            public Renderer Renderer;
            public Material[] Materials;
            public MaterialPropertyBlock Block;
            public bool Transparent;
            public ShadowCastingMode Shadows;
            public bool ReceiveShadows;
        }

        private OpenTorqueMotionRig rig;
        private PlanetaryGearKinematics kinematics;
        private Pose[] poses;
        private Vector3 pivot, axis;
        private Collider[] colliders;
        private bool[] colliderEnabled;
        private readonly List<Material> viewingMaterials = new List<Material>();
        private double elapsed;

        public bool IsReady { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsPlaying { get; private set; }
        public int Speed { get; private set; } = 100;
        public double CycleSeconds => rig == null ? 0 : rig.closedCycleInputTurns * 60.0 / rig.inputRpm;
        public float CyclePhase => rig == null ? 0 : (float)(elapsed / CycleSeconds);
        public PlanetaryGearKinematics.Angles GearAngles { get; private set; }

        public bool Initialize(string resourcePath, string modelId)
        {
            try
            {
                TextAsset asset = Resources.Load<TextAsset>(resourcePath);
                if (asset == null) throw new InvalidOperationException("缺少行星减速器演示配置");
                rig = JsonUtility.FromJson<OpenTorqueMotionRig>(asset.text);
                if (rig == null || rig.schemaVersion != 1 || rig.modelId != modelId
                    || rig.inputRpm <= 0 || float.IsNaN(rig.inputRpm) || float.IsInfinity(rig.inputRpm)
                    || rig.bindings == null || rig.bindings.Length != 19
                    || rig.planetObjects == null || rig.planetObjects.Length != 3
                    || rig.transparentObjects == null || rig.transparentObjects.Length == 0)
                    throw new InvalidOperationException("行星减速器配置格式无效");
                kinematics = new PlanetaryGearKinematics(rig.sunTeeth, rig.planetTeeth, rig.ringTeeth);
                if (rig.closedCycleInputTurns != kinematics.ClosedCycleInputTurns
                    || rig.sunTeeth != 9 || rig.planetTeeth != 27 || rig.ringTeeth != 63)
                    throw new InvalidOperationException("标准版齿数或闭合周期不一致");
                var named = GetComponentsInChildren<MeshFilter>(true)
                    .Where(mesh => mesh.name.StartsWith("MM_opentorque_", StringComparison.Ordinal))
                    .ToDictionary(mesh => mesh.name, mesh => mesh.transform, StringComparer.Ordinal);
                if (named.Count != 19 || rig.bindings.Select(b => b.objectName).Distinct().Count() != 19
                    || !new HashSet<string>(rig.bindings.Select(b => b.objectName)).SetEquals(named.Keys)
                    || rig.bindings.Any(b => b.role != "fixed" && b.role != "sun" && b.role != "carrier" && b.role != "planet")
                    || rig.planetObjects.Distinct().Count() != 3
                    || rig.transparentObjects.Distinct().Count() != rig.transparentObjects.Length
                    || rig.transparentObjects.Any(name => !named.ContainsKey(name))
                    || !new HashSet<string>(rig.planetObjects).SetEquals(rig.bindings.Where(b => b.role == "planet").Select(b => b.objectName)))
                    throw new InvalidOperationException("行星减速器分件绑定不一致");
                Vector3 Center(string name) => transform.InverseTransformPoint(named[name].TransformPoint(
                    named[name].GetComponent<MeshFilter>().sharedMesh.bounds.center));
                Vector3 lineStart = Center(rig.axisStartObject);
                axis = Center(rig.axisEndObject) - lineStart;
                if (axis.magnitude < .02f) throw new InvalidOperationException("减速器轴线无效");
                axis.Normalize();
                pivot = Center(rig.sunObject);
                if (Vector3.ProjectOnPlane(pivot - lineStart, axis).magnitude > .0001f)
                    throw new InvalidOperationException("太阳轮不在源装配轴线上");
                var planets = rig.planetObjects.Select(Center).ToArray();
                if (Mathf.Abs(rig.orbitRadiusM - .027f) > .000001f || planets.Any(p =>
                    Mathf.Abs(Vector3.ProjectOnPlane(p - pivot, axis).magnitude - rig.orbitRadiusM) > .0001f))
                    throw new InvalidOperationException("行星轮中心距无效");
                for (int index = 0; index < 3; index++)
                    if (Mathf.Abs(Vector3.Angle(Vector3.ProjectOnPlane(planets[index] - pivot, axis),
                        Vector3.ProjectOnPlane(planets[(index + 1) % 3] - pivot, axis)) - 120) > .1f)
                        throw new InvalidOperationException("行星轮间隔无效");
                var transparent = new HashSet<string>(rig.transparentObjects);
                poses = rig.bindings.Select(binding => {
                    Transform item = named[binding.objectName];
                    Renderer renderer = item.GetComponent<Renderer>();
                    if (renderer == null || renderer.sharedMaterials.Any(mat => mat == null))
                        throw new InvalidOperationException("缺少减速器材质");
                    return new Pose { Transform = item, Role = binding.role, LocalPosition = item.localPosition,
                        LocalRotation = item.localRotation, RestPosition = transform.InverseTransformPoint(item.position),
                        RestRotation = Quaternion.Inverse(transform.rotation) * item.rotation,
                        Renderer = renderer, Transparent = transparent.Contains(item.name) };
                }).ToArray();
                IsReady = true;
                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning("行星减速器演示绑定失败：" + error.Message);
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
                colliderEnabled = colliders.Select(collider => collider.enabled).ToArray();
                foreach (Collider collider in colliders) collider.enabled = false;
                foreach (Pose pose in poses)
                {
                    pose.Materials = pose.Renderer.sharedMaterials;
                    pose.Shadows = pose.Renderer.shadowCastingMode;
                    pose.ReceiveShadows = pose.Renderer.receiveShadows;
                    pose.Block = new MaterialPropertyBlock();
                    pose.Renderer.GetPropertyBlock(pose.Block);
                    pose.Renderer.SetPropertyBlock(null);
                    if (!pose.Transparent) continue;
                    // Ghost supports must not leave opaque shadows masking
                    // the gears which the teaching layer is meant to reveal.
                    pose.Renderer.shadowCastingMode = ShadowCastingMode.Off;
                    pose.Renderer.receiveShadows = false;
                    pose.Renderer.sharedMaterials = pose.Materials.Select(original => {
                        var material = new Material(original) { name = original.name + " (teaching view)" };
                        Color color = material.color; color.a = .16f; material.color = color;
                        material.SetFloat("_Mode", 2);
                        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                        material.SetInt("_ZWrite", 0);
                        material.DisableKeyword("_ALPHATEST_ON");
                        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        material.EnableKeyword("_ALPHABLEND_ON");
                        material.SetOverrideTag("RenderType", "Transparent");
                        material.renderQueue = (int)RenderQueue.Transparent;
                        viewingMaterials.Add(material);
                        return material;
                    }).ToArray();
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
            IsPlaying = IsActive = false;
            foreach (Pose pose in poses)
            {
                if (pose.Transform != null)
                {
                    pose.Transform.localPosition = pose.LocalPosition;
                    pose.Transform.localRotation = pose.LocalRotation;
                }
                if (pose.Renderer == null) continue;
                pose.Renderer.sharedMaterials = pose.Materials;
                pose.Renderer.shadowCastingMode = pose.Shadows;
                pose.Renderer.receiveShadows = pose.ReceiveShadows;
                pose.Renderer.SetPropertyBlock(pose.Block.isEmpty ? null : pose.Block);
                pose.Block = null; pose.Materials = null;
            }
            for (int index = 0; index < colliders.Length; index++)
                if (colliders[index] != null) colliders[index].enabled = colliderEnabled[index];
            foreach (Material material in viewingMaterials) Destroy(material);
            viewingMaterials.Clear();
            elapsed = 0;
            GearAngles = kinematics.Evaluate(0);
        }

        private void OnDisable() => Stop();
        private void Update() => Advance(Time.deltaTime);

        private void Advance(float deltaTime)
        {
            if (!IsPlaying || deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            elapsed = (elapsed + deltaTime * Speed / 100.0) % CycleSeconds;
            ApplyPhase(CyclePhase);
        }

        private static Quaternion Rotation(double degrees, Vector3 axis) => Quaternion.AngleAxis((float)(degrees % 360), axis);

        private void ApplyPhase(float phase)
        {
            GearAngles = kinematics.Evaluate((double)phase * rig.closedCycleInputTurns * 360);
            Quaternion sun = Rotation(GearAngles.Sun, axis), carrier = Rotation(GearAngles.Carrier, axis),
                planet = Rotation(GearAngles.Planet, axis);
            foreach (Pose pose in poses)
            {
                Quaternion orbit = pose.Role == "sun" ? sun : pose.Role == "fixed" ? Quaternion.identity : carrier;
                Quaternion rotation = pose.Role == "planet" ? planet : orbit;
                Vector3 position = pose.Role == "fixed" ? pose.RestPosition : pivot + orbit * (pose.RestPosition - pivot);
                pose.Transform.SetPositionAndRotation(transform.TransformPoint(position),
                    transform.rotation * rotation * pose.RestRotation);
            }
        }
    }
}
