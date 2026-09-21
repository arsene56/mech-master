using System;
using System.Collections.Generic;
using System.Linq;
using MechMaster.Domain;
using MechMaster.Runtime.UI;
using UnityEngine;

namespace MechMaster.Runtime
{
    public sealed class BikeModelView : MonoBehaviour
    {
        private readonly Dictionary<string, MechanicalPartView> parts =
            new Dictionary<string, MechanicalPartView>(StringComparer.Ordinal);
        private readonly List<MechanicalPartView> orderedParts =
            new List<MechanicalPartView>();
        private readonly Dictionary<string, Renderer> trayRenderers =
            new Dictionary<string, Renderer>(StringComparer.Ordinal);
        private Dictionary<string, Transform> namedTransforms;
        private Bounds assembledPartBounds;
        private bool globalExplosionActive;
        private string localExplosionPartId;
        private string highlightedTrayAssemblyId;
        private bool highlightedTrayCorrect;
        private bool highlightedTrayReady;

        public IReadOnlyDictionary<string, MechanicalPartView> Parts => parts;
        public bool GlobalExplosionActive => globalExplosionActive;
        public string LocalExplosionPartId => localExplosionPartId;
        public int ExplosionTargetCount =>
            orderedParts.Count(view => view.IsInspectionExplosionTarget);

        public void Bind(DisassemblyPlan plan)
        {
            parts.Clear();
            orderedParts.Clear();
            globalExplosionActive = false;
            localExplosionPartId = null;
            Dictionary<string, int> assemblySlotIndices =
                new Dictionary<string, int>(StringComparer.Ordinal);
            namedTransforms = GetComponentsInChildren<Transform>(true)
                .GroupBy(item => item.name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            for (int index = 0; index < plan.Steps.Count; index++)
            {
                PartDefinition definition = plan.Steps[index];
                string[] objectNames = ResolveObjectNames(definition, plan.Difficulty);
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
                int slotIndex;
                if (!assemblySlotIndices.TryGetValue(definition.AssemblyId, out slotIndex))
                {
                    slotIndex = 0;
                }
                assemblySlotIndices[definition.AssemblyId] = slotIndex + 1;
                Vector3 trayPosition = BicycleAssemblyInfo.PartPosition(
                    definition.AssemblyId,
                    slotIndex);
                Vector3 worldOffset = trayPosition - anchor.position;
                List<Renderer> targetRenderers = targets
                    .SelectMany(target => target.GetComponentsInChildren<Renderer>(true))
                    .Distinct()
                    .ToList();
                view.Initialize(definition, targets, targetRenderers, worldOffset);

                foreach (Transform target in targets)
                {
                    AttachPreciseHitTargets(target, view);
                }

                parts.Add(definition.Id, view);
                orderedParts.Add(view);
            }

            assembledPartBounds = CombinedBounds(orderedParts);
            BuildDisplayTray();
        }

        private static void AttachPreciseHitTargets(
            Transform target,
            MechanicalPartView owner)
        {
            bool attached = false;
            MeshFilter[] meshFilters = target.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter meshFilter in meshFilters)
            {
                if (meshFilter.sharedMesh == null)
                {
                    continue;
                }

                MeshCollider meshCollider = meshFilter.GetComponent<MeshCollider>();
                if (meshCollider == null)
                {
                    meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
                }
                meshCollider.sharedMesh = meshFilter.sharedMesh;
                meshCollider.convex = false;
                meshCollider.isTrigger = false;
                AttachHitProxy(meshFilter.gameObject, owner);
                attached = true;
            }

            SkinnedMeshRenderer[] skinnedRenderers =
                target.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (SkinnedMeshRenderer skinnedRenderer in skinnedRenderers)
            {
                if (skinnedRenderer.sharedMesh == null)
                {
                    continue;
                }

                MeshCollider meshCollider = skinnedRenderer.GetComponent<MeshCollider>();
                if (meshCollider == null)
                {
                    meshCollider = skinnedRenderer.gameObject.AddComponent<MeshCollider>();
                }
                meshCollider.sharedMesh = skinnedRenderer.sharedMesh;
                meshCollider.convex = false;
                meshCollider.isTrigger = false;
                AttachHitProxy(skinnedRenderer.gameObject, owner);
                attached = true;
            }

            if (attached)
            {
                return;
            }

            // Non-mesh helper objects still need a small selectable volume.
            BoxCollider fallback = target.GetComponent<BoxCollider>();
            if (fallback == null)
            {
                fallback = target.gameObject.AddComponent<BoxCollider>();
            }
            fallback.isTrigger = false;
            AttachHitProxy(target.gameObject, owner);
        }

        private static void AttachHitProxy(GameObject target, MechanicalPartView owner)
        {
            MechanicalPartHitProxy proxy = target.GetComponent<MechanicalPartHitProxy>();
            if (proxy == null)
            {
                proxy = target.AddComponent<MechanicalPartHitProxy>();
            }
            proxy.Initialize(owner);
        }

        public void Refresh(DisassemblyPlan plan, bool immediate)
        {
            foreach (PartDefinition part in plan.Steps)
            {
                MechanicalPartView view;
                if (parts.TryGetValue(part.Id, out view))
                {
                    view.SetRemoved(plan.IsRemoved(part.Id), immediate);
                    view.SetExpected(false);
                }
            }
        }

        public MechanicalPartView FindPart(string partId)
        {
            MechanicalPartView view;
            return parts.TryGetValue(partId, out view) ? view : null;
        }

        public void SetGlobalExplosion(bool exploded, bool immediate)
        {
            if (!exploded)
            {
                ClearInspectionExplosion(immediate);
                return;
            }

            ClearInspectionExplosion(true);
            globalExplosionActive = true;
            List<MechanicalPartView> available = orderedParts
                .Where(view => !view.IsRemoved)
                .ToList();
            if (available.Count == 0)
            {
                globalExplosionActive = false;
                return;
            }

            Bounds activeBounds = CombinedBounds(available);
            float baseDistance = Mathf.Clamp(
                activeBounds.size.magnitude * 0.22f,
                0.32f,
                0.62f);
            foreach (IGrouping<string, MechanicalPartView> assemblyGroup in available
                .GroupBy(view => view.Definition.AssemblyId, StringComparer.Ordinal))
            {
                List<MechanicalPartView> assemblyParts = assemblyGroup.ToList();
                Bounds assemblyBounds = CombinedBounds(assemblyParts);
                Vector3 direction = ResolveExplosionDirection(
                    assemblyGroup.Key,
                    assemblyBounds.center - activeBounds.center);
                Vector3 tangent = Vector3.Cross(direction, Vector3.up);
                if (tangent.sqrMagnitude < 0.01f)
                {
                    tangent = Vector3.Cross(direction, Vector3.forward);
                }
                tangent.Normalize();

                for (int index = 0; index < assemblyParts.Count; index++)
                {
                    MechanicalPartView view = assemblyParts[index];
                    float centeredIndex = index - (assemblyParts.Count - 1) * 0.5f;
                    float sizeBias = Mathf.Clamp(
                        view.GetWorldBounds().extents.magnitude * 0.16f,
                        0f,
                        0.12f);
                    Vector3 offset = direction
                        * (baseDistance + sizeBias + Mathf.Abs(centeredIndex) * 0.025f)
                        + tangent * centeredIndex * 0.095f
                        + Vector3.up * ((index % 2 == 0 ? 1f : -1f) * 0.035f);
                    view.SetInspectionExplosion(offset, true, immediate);
                }
            }
        }

        public bool ToggleLocalExplosion(string partId, bool immediate)
        {
            MechanicalPartView selected = FindPart(partId);
            if (selected == null || selected.IsRemoved)
            {
                return false;
            }

            if (globalExplosionActive)
            {
                ClearInspectionExplosion(true);
            }

            bool shouldExplode = !string.Equals(
                localExplosionPartId,
                partId,
                StringComparison.Ordinal)
                || !selected.IsInspectionExplosionTarget;
            foreach (MechanicalPartView view in orderedParts)
            {
                if (view == selected && shouldExplode)
                {
                    continue;
                }

                view.SetInspectionExplosion(
                    view.InspectionWorldOffset,
                    false,
                    immediate);
            }

            if (!shouldExplode)
            {
                localExplosionPartId = null;
                return false;
            }

            Vector3 direction = ResolveExplosionDirection(
                selected.Definition.AssemblyId,
                selected.GetWorldBounds().center - assembledPartBounds.center);
            direction.y *= 0.55f;
            direction.Normalize();
            float distance = Mathf.Clamp(
                assembledPartBounds.size.magnitude * 0.09f
                + selected.GetWorldBounds().extents.magnitude * 0.08f,
                0.17f,
                0.29f);
            selected.SetInspectionExplosion(direction * distance, true, immediate);
            localExplosionPartId = partId;
            globalExplosionActive = false;
            return true;
        }

        public void ClearInspectionExplosion(bool immediate)
        {
            foreach (MechanicalPartView view in orderedParts)
            {
                view.SetInspectionExplosion(
                    view.InspectionWorldOffset,
                    false,
                    immediate);
            }

            globalExplosionActive = false;
            localExplosionPartId = null;
        }

        public Vector3[] GetExplosionFramingPoints()
        {
            var points = new List<Vector3>(orderedParts.Count * 8);
            foreach (MechanicalPartView view in orderedParts)
            {
                Bounds bounds = view.GetWorldBounds();
                Vector3 pendingOffset = view.PendingInspectionWorldOffset;
                for (int index = 0; index < 8; index++)
                {
                    points.Add(bounds.center + pendingOffset + Vector3.Scale(
                        bounds.extents,
                        new Vector3(
                            (index & 1) == 0 ? -1f : 1f,
                            (index & 2) == 0 ? -1f : 1f,
                            (index & 4) == 0 ? -1f : 1f)));
                }
            }

            return points.ToArray();
        }

        private static Bounds CombinedBounds(IEnumerable<MechanicalPartView> views)
        {
            bool hasBounds = false;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (MechanicalPartView view in views)
            {
                Bounds partBounds = view.GetWorldBounds();
                if (!hasBounds)
                {
                    bounds = partBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(partBounds);
                }
            }

            return bounds;
        }

        private static Vector3 ResolveExplosionDirection(
            string assemblyId,
            Vector3 radialDirection)
        {
            if (radialDirection.sqrMagnitude >= 0.012f)
            {
                return radialDirection.normalized;
            }

            int assemblyIndex = Mathf.Max(0, BicycleAssemblyInfo.IndexOf(assemblyId));
            float angle = assemblyIndex * Mathf.PI * 2f
                / BicycleAssemblyInfo.OrderedIds.Length;
            float vertical = (assemblyIndex % 3 - 1) * 0.32f;
            return new Vector3(Mathf.Cos(angle), vertical, Mathf.Sin(angle)).normalized;
        }

        public void SetTrayHighlight(
            string assemblyId,
            bool correctAssembly,
            bool ready)
        {
            if (string.Equals(
                    highlightedTrayAssemblyId,
                    assemblyId,
                    StringComparison.Ordinal)
                && highlightedTrayCorrect == correctAssembly
                && highlightedTrayReady == ready)
            {
                return;
            }

            if (!string.IsNullOrEmpty(highlightedTrayAssemblyId))
            {
                ApplyTrayColor(highlightedTrayAssemblyId, DefaultTrayColor(highlightedTrayAssemblyId));
            }

            highlightedTrayAssemblyId = assemblyId;
            highlightedTrayCorrect = correctAssembly;
            highlightedTrayReady = ready;
            if (string.IsNullOrEmpty(assemblyId))
            {
                return;
            }

            Color color = !correctAssembly
                ? WorkshopTheme.Wrong
                : ready
                    ? WorkshopTheme.Ready
                    : WorkshopTheme.Blocked;
            ApplyTrayColor(assemblyId, color);
        }

        private void BuildDisplayTray()
        {
            trayRenderers.Clear();
            highlightedTrayAssemblyId = null;
            highlightedTrayCorrect = false;
            highlightedTrayReady = false;
            Transform existing = transform.Find("DisassembledPartsTray");
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }

            GameObject trayRoot = new GameObject("DisassembledPartsTray");
            trayRoot.transform.SetParent(transform, false);
            Shader trayShader = Shader.Find("Standard");
            Material trayMaterial = trayShader == null ? null : new Material(trayShader);
            if (trayMaterial != null)
            {
                trayMaterial.color = WorkshopTheme.TrayColor(0);
            }

            for (int index = 0; index < BicycleAssemblyInfo.OrderedIds.Length; index++)
            {
                string assemblyId = BicycleAssemblyInfo.OrderedIds[index];
                GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cell.name = "TrayCell_" + assemblyId;
                cell.transform.SetParent(trayRoot.transform, false);
                cell.transform.localPosition =
                    BicycleAssemblyInfo.TrayCellPosition(assemblyId) + new Vector3(0f, -0.02f, 0f);
                cell.transform.localScale = new Vector3(0.34f, 0.025f, 0.25f);

                Collider collider = cell.GetComponent<Collider>();
                if (collider != null)
                {
                    cell.layer = 2;
                    Destroy(collider);
                }

                Renderer cellRenderer = cell.GetComponent<Renderer>();
                if (cellRenderer != null && trayMaterial != null)
                {
                    cellRenderer.sharedMaterial = trayMaterial;
                    trayRenderers[assemblyId] = cellRenderer;
                    ApplyTrayColor(assemblyId, DefaultTrayColor(assemblyId));
                }
            }
        }

        private void ApplyTrayColor(string assemblyId, Color color)
        {
            Renderer targetRenderer;
            if (!trayRenderers.TryGetValue(assemblyId, out targetRenderer)
                || targetRenderer == null)
            {
                return;
            }

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            block.SetColor("_BaseColor", color);
            targetRenderer.SetPropertyBlock(block);
        }

        private static Color DefaultTrayColor(string assemblyId)
        {
            int index = BicycleAssemblyInfo.IndexOf(assemblyId);
            return WorkshopTheme.TrayColor(index);
        }

        private static string[] ResolveObjectNames(
            PartDefinition definition,
            DifficultyLevel difficulty)
        {
            if (definition.ModelObjectNames.Count > 0)
            {
                return definition.ModelObjectNames.ToArray();
            }

            string partId = definition.Id;
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
