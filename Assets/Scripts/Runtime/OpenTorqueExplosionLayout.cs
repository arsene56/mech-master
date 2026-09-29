using System;
using System.Collections.Generic;
using System.Linq;
using MechMaster.Domain;
using UnityEngine;

namespace MechMaster.Runtime
{
    // Teaching offsets only: never change CAD geometry, part grouping or progress.
    // Cache them while assembled so removing a part cannot reshuffle its neighbours.
    internal sealed class OpenTorqueExplosionLayout
    {
        private readonly Dictionary<string, Vector3> offsets =
            new Dictionary<string, Vector3>(StringComparer.Ordinal);

        public Vector2 ViewAngles => new Vector2(-58f, 20f);
        public Vector3 OffsetFor(string partId) => offsets[partId];

        public static OpenTorqueExplosionLayout TryCreate(
            MechanicalModelDefinition model,
            DisassemblyPlan plan,
            IReadOnlyDictionary<string, Transform> named,
            float diameter)
        {
            if (model == null || model.id != "gearbox.opentorque.planetary.v1") return null;

            try { return Create(model, plan, named, diameter); }
            catch (Exception error)
            {
                Debug.LogWarning("行星减速器爆炸布局绑定失败，使用通用布局：" + error.Message);
                return null;
            }
        }

        private static OpenTorqueExplosionLayout Create(
            MechanicalModelDefinition model,
            DisassemblyPlan plan,
            IReadOnlyDictionary<string, Transform> named,
            float diameter)
        {

            TextAsset asset = Resources.Load<TextAsset>(model.motion.rigResourcePath);
            var rig = JsonUtility.FromJson<OpenTorqueMotionRig>(asset.text);
            Vector3 Center(string name)
            {
                Transform item = named[name];
                return item.TransformPoint(item.GetComponent<MeshFilter>().sharedMesh.bounds.center);
            }

            Vector3 axis = (Center(rig.axisEndObject) - Center(rig.axisStartObject)).normalized;
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;
            if (up.sqrMagnitude < .5f) up = Vector3.ProjectOnPlane(Vector3.right, axis).normalized;
            Vector3 pivot = Center(rig.sunObject);
            var radial = rig.planetObjects.Select(name =>
                Vector3.ProjectOnPlane(Center(name) - pivot, axis).normalized).ToArray();
            var planetIndices = rig.bindings.Where(binding => binding.planetIndex >= 0)
                .ToDictionary(binding => binding.objectName, binding => binding.planetIndex, StringComparer.Ordinal);

            var layout = new OpenTorqueExplosionLayout();
            foreach (PartDefinition part in plan.Steps)
            {
                float axial = 0f, vertical = 0f, spread = 0f;
                string component = part.ComponentId;
                if (plan.Difficulty == DifficultyLevel.Simple)
                {
                    // Five intact modules: the sun is lifted clear of the three gears.
                    switch (part.AssemblyId)
                    {
                        case "housing": axial = -1.15f; break;
                        case "carrier": axial = 1.15f; break;
                        case "sun": axial = -.1f; vertical = .78f; break;
                        case "planets": axial = .08f; break;
                        case "encoder": axial = -2.3f; break;
                        default: throw new InvalidOperationException("未知 OpenTorque 模块：" + part.AssemblyId);
                    }
                }
                else if (plan.Difficulty == DifficultyLevel.Standard)
                {
                    switch (part.AssemblyId)
                    {
                        case "housing": axial = component == "backplate" ? -1.8f : -.9f; break;
                        case "carrier": axial = component == "bearing" ? .55f : 1.4f; break;
                        case "sun": axial = -.25f; vertical = .78f; break;
                        case "planets": axial = .55f; spread = .6f; break;
                        case "encoder": axial = component == "Encoder Cover" ? -2.7f : -2.2f; break;
                        default: throw new InvalidOperationException("未知 OpenTorque 模块：" + part.AssemblyId);
                    }
                }
                else
                {
                    // Separate service units in their real axial order. Repeated pins
                    // remain one intact group; sealed bearings are never split internally.
                    switch (part.AssemblyId)
                    {
                        case "housing":
                            axial = component == "Actuator Housing" ? -.9f
                                : component == "Backplate" ? -1.8f : 1.9f;
                            break;
                        case "carrier":
                            axial = component == "Planet Carrier C" ? .6f
                                : component == "Planet Carrier B" ? 1.1f
                                : component == "Planet Carrier A" ? 2.35f : 1.5f;
                            break;
                        case "sun": axial = -.25f; vertical = .78f; break;
                        case "planets":
                            if (component == "M5x30 Dowel Pin") { axial = .65f; vertical = -.9f; }
                            else
                            {
                                spread = .8f;
                                bool bearing = component.StartsWith("F625ZZ", StringComparison.Ordinal);
                                axial = bearing ? 1.05f : .55f;
                                // A slight downward stagger exposes the middle
                                // bearing instead of hiding it behind a carrier plate.
                                vertical = bearing ? -.35f : 0f;
                            }
                            break;
                        case "encoder": axial = component == "Encoder Cover" ? -2.9f : -2.35f; break;
                        default: throw new InvalidOperationException("未知 OpenTorque 模块：" + part.AssemblyId);
                    }
                }

                Vector3 offset = axis * axial + up * vertical;
                if (spread > 0f)
                {
                    // All members of a Standard gear+bearing+pin share this offset;
                    // Advanced gear and bearing retain the same 120-degree radial lane.
                    int planetIndex = planetIndices[part.ModelObjectNames[0]];
                    offset += radial[planetIndex] * spread;
                }
                layout.offsets.Add(part.Id, offset * diameter);
            }
            return layout;
        }
    }
}
