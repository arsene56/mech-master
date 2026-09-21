using System.Collections;
using System.Collections.Generic;
using MechMaster.Domain;
using UnityEngine;

namespace MechMaster.Runtime
{
    public sealed class MechanicalPartView : MonoBehaviour
    {
        private sealed class TransformState
        {
            public Transform Transform;
            public Vector3 LocalPosition;
        }

        private readonly List<TransformState> transformStates = new List<TransformState>();
        private readonly List<Renderer> renderers = new List<Renderer>();
        private MaterialPropertyBlock propertyBlock;
        private Vector3 removedWorldOffset;
        private Vector3 inspectionWorldOffset;
        private Vector3 dragPreviewWorldOffset;
        private float displayedFactor;
        private float inspectionFactor;
        private float inspectionTargetFactor;
        private Coroutine animationRoutine;
        private Coroutine inspectionAnimationRoutine;
        private bool selected;
        private bool expected;

        public PartDefinition Definition { get; private set; }
        public string PartId => Definition == null ? string.Empty : Definition.Id;
        public bool IsRemoved { get; private set; }
        public bool IsInspectionExplosionTarget => inspectionTargetFactor > 0.5f;
        public Vector3 InspectionWorldOffset => inspectionWorldOffset;
        public Vector3 PendingInspectionWorldOffset =>
            inspectionWorldOffset * (inspectionTargetFactor - inspectionFactor);

        public void Initialize(
            PartDefinition definition,
            IEnumerable<Transform> movingTransforms,
            IEnumerable<Renderer> targetRenderers,
            Vector3 worldOffset)
        {
            Definition = definition;
            removedWorldOffset = worldOffset;
            inspectionWorldOffset = Vector3.zero;
            dragPreviewWorldOffset = Vector3.zero;
            displayedFactor = 0f;
            inspectionFactor = 0f;
            inspectionTargetFactor = 0f;
            transformStates.Clear();
            renderers.Clear();

            foreach (Transform movingTransform in movingTransforms)
            {
                if (movingTransform == null)
                {
                    continue;
                }

                transformStates.Add(new TransformState
                {
                    Transform = movingTransform,
                    LocalPosition = movingTransform.localPosition
                });
            }

            foreach (Renderer targetRenderer in targetRenderers)
            {
                if (targetRenderer != null && !renderers.Contains(targetRenderer))
                {
                    renderers.Add(targetRenderer);
                }
            }

            propertyBlock = new MaterialPropertyBlock();
        }

        public void SetSelected(bool value)
        {
            selected = value;
            ApplyHighlight();
        }

        public void SetExpected(bool value)
        {
            expected = value;
            ApplyHighlight();
        }

        public void BeginDragPreview()
        {
            dragPreviewWorldOffset = Vector3.zero;
            ApplyPosition(displayedFactor);
        }

        public void UpdateDragPreview(Vector3 worldOffset)
        {
            // The controller calculates this on a camera-facing plane passing
            // through the exact grabbed surface point, so the part stays under
            // the pointer at every perspective depth.
            dragPreviewWorldOffset = worldOffset;
            ApplyPosition(displayedFactor);
        }

        public void EndDragPreview()
        {
            dragPreviewWorldOffset = Vector3.zero;
            ApplyPosition(displayedFactor);
        }

        public void SetRemoved(bool value, bool immediate)
        {
            IsRemoved = value;
            dragPreviewWorldOffset = Vector3.zero;
            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
                animationRoutine = null;
            }

            if (immediate || !gameObject.activeInHierarchy)
            {
                ApplyPosition(value ? 1f : 0f);
            }
            else
            {
                animationRoutine = StartCoroutine(AnimateTo(value ? 1f : 0f));
            }

            ApplyHighlight();
        }

        public void SetInspectionExplosion(
            Vector3 worldOffset,
            bool exploded,
            bool immediate)
        {
            if (inspectionAnimationRoutine != null)
            {
                StopCoroutine(inspectionAnimationRoutine);
                inspectionAnimationRoutine = null;
            }

            inspectionWorldOffset = worldOffset;
            inspectionTargetFactor = exploded ? 1f : 0f;
            if (immediate || !gameObject.activeInHierarchy)
            {
                inspectionFactor = inspectionTargetFactor;
                ApplyCurrentPosition();
                return;
            }

            inspectionAnimationRoutine = StartCoroutine(
                AnimateInspectionTo(inspectionTargetFactor));
        }

        public Bounds GetWorldBounds()
        {
            bool hasBounds = false;
            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            foreach (Renderer targetRenderer in renderers)
            {
                if (targetRenderer == null || !targetRenderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = targetRenderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(targetRenderer.bounds);
                }
            }

            return bounds;
        }

        private IEnumerator AnimateTo(float target)
        {
            float start = CurrentFactor();
            const float duration = 0.32f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                ApplyPosition(Mathf.Lerp(start, target, t));
                yield return null;
            }

            ApplyPosition(target);
            animationRoutine = null;
        }

        private IEnumerator AnimateInspectionTo(float target)
        {
            float start = inspectionFactor;
            const float duration = 0.38f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                inspectionFactor = Mathf.Lerp(start, target, t);
                ApplyCurrentPosition();
                yield return null;
            }

            inspectionFactor = target;
            ApplyCurrentPosition();
            inspectionAnimationRoutine = null;
        }

        private float CurrentFactor()
        {
            return displayedFactor;
        }

        private void ApplyPosition(float factor)
        {
            displayedFactor = Mathf.Clamp01(factor);
            ApplyCurrentPosition();
        }

        private void ApplyCurrentPosition()
        {
            foreach (TransformState state in transformStates)
            {
                if (state.Transform != null)
                {
                    Vector3 localPreviewOffset = state.Transform.parent == null
                        ? removedWorldOffset * displayedFactor
                          + inspectionWorldOffset * inspectionFactor
                          + dragPreviewWorldOffset
                        : state.Transform.parent.InverseTransformVector(
                            removedWorldOffset * displayedFactor
                            + inspectionWorldOffset * inspectionFactor
                            + dragPreviewWorldOffset);
                    state.Transform.localPosition =
                        state.LocalPosition + localPreviewOffset;
                }
            }
        }

        private void ApplyHighlight()
        {
            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            foreach (Renderer targetRenderer in renderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                if (!selected && !expected)
                {
                    targetRenderer.SetPropertyBlock(null);
                    continue;
                }

                targetRenderer.GetPropertyBlock(propertyBlock);
                Color color;
                if (selected)
                {
                    color = IsRemoved
                        ? new Color(0.35f, 0.9f, 1f, 1f)
                        : new Color(1f, 0.67f, 0.12f, 1f);
                }
                else
                {
                    color = new Color(0.2f, 1f, 0.48f, 1f);
                }
                propertyBlock.SetColor("_Color", color);
                propertyBlock.SetColor("_BaseColor", color);
                targetRenderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
