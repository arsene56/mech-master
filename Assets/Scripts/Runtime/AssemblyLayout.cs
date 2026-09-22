using System;
using UnityEngine;

namespace MechMaster.Runtime
{
    public sealed class AssemblyLayout
    {
        private readonly MechanicalModelDefinition model;
        private readonly Bounds modelBounds;
        private readonly float scale;

        public int Count => model.assemblies.Length;

        public AssemblyLayout(MechanicalModelDefinition model, Bounds modelBounds)
        {
            this.model = model;
            this.modelBounds = modelBounds;
            scale = Mathf.Max(0.001f,
                Mathf.Max(modelBounds.size.x, modelBounds.size.y, modelBounds.size.z) / 2f);
        }

        public string IdAt(int index)
        {
            return model.assemblies[index].id;
        }

        public int IndexOf(string assemblyId)
        {
            for (int index = 0; index < model.assemblies.Length; index++)
            {
                if (string.Equals(model.assemblies[index].id, assemblyId, StringComparison.Ordinal))
                    return index;
            }
            return -1;
        }

        public Vector3 TrayCellPosition(string assemblyId)
        {
            int index = Mathf.Max(0, IndexOf(assemblyId));
            int column = index % 7;
            int row = index / 7;
            return new Vector3(
                modelBounds.center.x + (-1.14f + column * 0.38f) * scale,
                modelBounds.min.y - 0.43f * scale,
                modelBounds.center.z + (-0.46f - row * 0.3f) * scale);
        }

        public Vector3 PartPosition(string assemblyId, int slotIndex)
        {
            Vector3 cell = TrayCellPosition(assemblyId);
            int column = slotIndex % 7;
            int row = (slotIndex / 7) % 7;
            int layer = slotIndex / 49;
            return cell + new Vector3(
                (column - 3) * 0.032f * scale,
                (0.055f + layer * 0.018f) * scale,
                (row - 3) * 0.027f * scale);
        }

        public string DisplayName(string assemblyId)
        {
            return model.AssemblyDisplayName(assemblyId);
        }

        public Vector3 CellSize => new Vector3(0.34f, 0.025f, 0.25f) * scale;
        public float CellVerticalOffset => -0.02f * scale;
        public float Scale => scale;
    }
}
