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
        private Vector3 explodedOffset;
        private Coroutine animationRoutine;
        private bool selected;

        public PartDefinition Definition { get; private set; }
        public string PartId => Definition == null ? string.Empty : Definition.Id;
        public bool IsRemoved { get; private set; }

        public void Initialize(
            PartDefinition definition,
            IEnumerable<Transform> movingTransforms,
            IEnumerable<Renderer> targetRenderers,
            Vector3 offset)
        {
            Definition = definition;
            explodedOffset = offset;
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

        public void SetRemoved(bool value, bool immediate)
        {
            IsRemoved = value;
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

        private float CurrentFactor()
        {
            if (transformStates.Count == 0 || explodedOffset.sqrMagnitude < 0.000001f)
            {
                return IsRemoved ? 1f : 0f;
            }

            TransformState state = transformStates[0];
            Vector3 delta = state.Transform.localPosition - state.LocalPosition;
            return Mathf.Clamp01(Vector3.Dot(delta, explodedOffset) / explodedOffset.sqrMagnitude);
        }

        private void ApplyPosition(float factor)
        {
            foreach (TransformState state in transformStates)
            {
                if (state.Transform != null)
                {
                    state.Transform.localPosition = state.LocalPosition + explodedOffset * factor;
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

                if (!selected)
                {
                    targetRenderer.SetPropertyBlock(null);
                    continue;
                }

                targetRenderer.GetPropertyBlock(propertyBlock);
                Color color = IsRemoved
                    ? new Color(0.35f, 0.9f, 1f, 1f)
                    : new Color(1f, 0.67f, 0.12f, 1f);
                propertyBlock.SetColor("_Color", color);
                propertyBlock.SetColor("_BaseColor", color);
                targetRenderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}

