using System;
using System.Collections.Generic;
using System.Linq;
using MechMaster.Domain;
using UnityEngine;

namespace MechMaster.Runtime
{
    public sealed class BikeModelView : MonoBehaviour
    {
        private readonly Dictionary<string, MechanicalPartView> parts =
            new Dictionary<string, MechanicalPartView>(StringComparer.Ordinal);
        private Dictionary<string, Transform> namedTransforms;

        public IReadOnlyDictionary<string, MechanicalPartView> Parts => parts;

        public void Bind(DisassemblyPlan plan)
        {
            parts.Clear();
            namedTransforms = GetComponentsInChildren<Transform>(true)
                .GroupBy(item => item.name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            for (int index = 0; index < plan.Steps.Count; index++)
            {
                PartDefinition definition = plan.Steps[index];
                string[] objectNames = ResolveObjectNames(definition.Id, plan.Difficulty);
                if (objectNames.Length == 0)
                {
                    Debug.LogWarning("没有为零件配置模型映射：" + definition.Id);
                    continue;
                }

                List<Transform> targets = objectNames
                    .Where(name => namedTransforms.ContainsKey(name))
                    .Select(name => namedTransforms[name])
                    .Distinct()
                    .ToList();
                if (targets.Count == 0)
                {
                    Debug.LogWarning("模型中找不到零件对象：" + definition.Id);
                    continue;
                }

                Transform anchor = targets[0];
                MechanicalPartView view = anchor.gameObject.AddComponent<MechanicalPartView>();
                Vector3 direction = ExplodedDirection(index, plan.Steps.Count);
                List<Renderer> targetRenderers = targets
                    .SelectMany(target => target.GetComponentsInChildren<Renderer>(true))
                    .Distinct()
                    .ToList();
                view.Initialize(definition, targets, targetRenderers, direction);

                Collider collider = anchor.GetComponent<Collider>();
                if (collider == null)
                {
                    BoxCollider boxCollider = anchor.gameObject.AddComponent<BoxCollider>();
                    boxCollider.isTrigger = false;
                }

                parts.Add(definition.Id, view);
            }
        }

        public void Refresh(DisassemblyPlan plan, bool immediate)
        {
            foreach (PartDefinition part in plan.Steps)
            {
                MechanicalPartView view;
                if (parts.TryGetValue(part.Id, out view))
                {
                    view.SetRemoved(plan.IsRemoved(part.Id), immediate);
                }
            }
        }

        public MechanicalPartView FindPart(string partId)
        {
            MechanicalPartView view;
            return parts.TryGetValue(partId, out view) ? view : null;
        }

        private static Vector3 ExplodedDirection(int index, int count)
        {
            float angle = Mathf.Lerp(-65f, 65f, count <= 1 ? 0.5f : index / (float)(count - 1));
            Vector3 radial = Quaternion.Euler(0f, angle, 0f) * new Vector3(0.35f, 0.12f, 0.12f);
            radial.y -= 0.18f + index * 0.025f;
            return radial;
        }

        private static string[] ResolveObjectNames(string partId, DifficultyLevel difficulty)
        {
            switch (partId)
            {
                case "front_thru_axle":
                    return new[] { "Front_ThruAxle" };
                case "front_wheel":
                    return difficulty == DifficultyLevel.Advanced
                        ? new[] { "Front_Tire", "Front_Rim", "Front_Spokes", "Front_Hub" }
                        : new[] { "Front_Tire", "Front_Rim", "Front_Spokes", "Front_Hub", "Front_EndCaps" };
                case "front_brake_assembly":
                    return new[]
                    {
                        "Front_Caliper", "LeftPad", "RightPad", "PadSpring", "PadPin",
                        "RetainingClip", "CaliperMountBolts", "BrakeHose"
                    };
                case "front_rotor_assembly":
                    return new[] { "Front_Rotor", "RotorBolts" };
                case "pad_retaining_clip":
                    return new[] { "RetainingClip" };
                case "pad_retaining_pin":
                    return new[] { "PadPin" };
                case "brake_pads_group":
                    return new[] { "LeftPad", "RightPad", "PadSpring" };
                case "pad_spring":
                    return new[] { "PadSpring" };
                case "left_brake_pad":
                    return new[] { "LeftPad" };
                case "right_brake_pad":
                    return new[] { "RightPad" };
                case "caliper_mount_bolts":
                    return new[] { "CaliperMountBolts" };
                case "front_caliper":
                    return new[] { "Front_Caliper", "BrakeHose" };
                case "rotor_bolts_group":
                    return new[] { "RotorBolts" };
                case "front_rotor":
                    return new[] { "Front_Rotor" };
                case "hub_end_caps":
                    return new[] { "Front_EndCaps" };
                default:
                    return Array.Empty<string>();
            }
        }
    }
}

