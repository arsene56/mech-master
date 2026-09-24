using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MechMaster.Runtime
{
    // A dedicated hit area stays usable while the disassembly colliders are disabled.
    public sealed class BicycleBrakeHitTarget : MonoBehaviour
    {
        public bool IsFront { get; private set; }

        public void Initialize(bool isFront) => IsFront = isFront;
    }

    // A kinematic teaching view. The disassembly model and its saved state stay intact.
    public sealed class BicycleMotionController : MonoBehaviour
    {
        private struct ChainCircle
        {
            public Vector2 Center;
            public float Radius;
            public float Lateral;
        }

        private struct ChainTangent
        {
            public Vector2 From;
            public Vector2 To;
        }

        private sealed class Pose
        {
            public Transform Transform;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        private readonly List<Pose> crankParts = new List<Pose>();
        private readonly List<Pose> frontWheelParts = new List<Pose>();
        private readonly List<Pose> rearWheelParts = new List<Pose>();
        private readonly List<Pose>[] pedalParts = { new List<Pose>(), new List<Pose>() };
        private readonly List<Pose>[] brakePads = { new List<Pose>(), new List<Pose>() };
        private readonly List<Pose> jockeyWheels = new List<Pose>();
        private readonly List<Pose> quickLinks = new List<Pose>();
        private Pose[] chainLinks;
        private Renderer[] staticChainRenderers;
        private bool[] staticChainRendererEnabled;
        private Transform[] motionChainLinks;
        private GameObject motionChainRoot;
        private Vector3[] chainPath;
        private float[] chainDistances;
        private float chainPathLength;
        private Quaternion chainMeshCorrection;
        private Collider[] colliders;
        private bool[] colliderEnabled;
        private Vector3 crankPivot;
        private Vector3 frontPivot;
        private Vector3 rearPivot;
        private Vector3 axleAxis;
        private readonly Vector3[] pedalPivots = new Vector3[2];
        private readonly Vector3[] brakePivots = new Vector3[2];
        private readonly Vector3[] brakeRotorCenters = new Vector3[2];
        private readonly Pose[] brakeLevers = new Pose[2];
        private readonly Transform[] brakeHitTargets = new Transform[2];
        private readonly float[] brakePullAngles = new float[2];
        private readonly bool[] brakeEngaged = new bool[2];
        private int frontTeeth;
        private int rearTeeth;
        private double crankTurns;
        private double frontWheelTurns;
        private float effectiveCadenceRpm;
        private float frontWheelRpm;

        public bool IsReady { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsPlaying { get; private set; }
        public int CadenceRpm { get; private set; } = 60;
        public float EffectiveCadenceRpm => effectiveCadenceRpm;
        public float FrontWheelRpm => frontWheelRpm;
        public float RearWheelRpm => effectiveCadenceRpm * RearWheelRatio;
        public bool FrontBrakeEngaged => brakeEngaged[0];
        public bool RearBrakeEngaged => brakeEngaged[1];
        public float RearWheelRatio => frontTeeth / (float)rearTeeth;

        public bool Initialize(int chainringTeeth, int sprocketTeeth, int linkCount)
        {
            frontTeeth = chainringTeeth;
            rearTeeth = sprocketTeeth;
            // This first teaching view is bound to the catalog's 36T / 24T
            // geometry and its 110 stable disassembly links.
            if (frontTeeth != 36 || rearTeeth != 24 || linkCount != 110)
                return false;

            var named = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (Transform part in GetComponentsInChildren<Transform>(true))
                if (!named.ContainsKey(part.name)) named.Add(part.name, part);

            Transform spindle, chainring, frontHub, rearHub, sprocket, firstPedal, secondPedal;
            Transform upperJockey, lowerJockey;
            Transform frontLever, rearLever, frontLeverBody, rearLeverBody;
            Transform frontLeverPivot, rearLeverPivot, frontRotor, rearRotor;
            if (!named.TryGetValue("MM_crank_spindle", out spindle)
                || !named.TryGetValue("MM_crank_chainring_1", out chainring)
                || !named.TryGetValue("MM_wheel_front_hub_shell", out frontHub)
                || !named.TryGetValue("MM_wheel_rear_hub_shell", out rearHub)
                || !named.TryGetValue("MM_wheel_rear_cassette_sprocket_07", out sprocket)
                || !named.TryGetValue("MM_pedal_axle_1", out firstPedal)
                || !named.TryGetValue("MM_pedal_axle_2", out secondPedal)
                || !named.TryGetValue("MM_rear_derailleur_jockey_wheel_1", out upperJockey)
                || !named.TryGetValue("MM_rear_derailleur_jockey_wheel_2", out lowerJockey)
                || !named.TryGetValue("MM_brake_front_lever_blade", out frontLever)
                || !named.TryGetValue("MM_brake_rear_lever_blade", out rearLever)
                || !named.TryGetValue("MM_brake_front_lever_body", out frontLeverBody)
                || !named.TryGetValue("MM_brake_rear_lever_body", out rearLeverBody)
                || !named.TryGetValue("MM_brake_front_lever_pivot", out frontLeverPivot)
                || !named.TryGetValue("MM_brake_rear_lever_pivot", out rearLeverPivot)
                || !named.TryGetValue("MM_wheel_front_rotor", out frontRotor)
                || !named.TryGetValue("MM_wheel_rear_rotor", out rearRotor))
                return false;

            crankPivot = Center(spindle);
            frontPivot = Center(frontHub);
            rearPivot = Center(rearHub);
            axleAxis = (Center(chainring) - crankPivot).normalized;
            if (axleAxis.sqrMagnitude < 0.9f
                || Vector3.Dot(axleAxis, (Center(sprocket) - rearPivot).normalized) < 0.9f)
                return false;
            pedalPivots[0] = Center(firstPedal);
            pedalPivots[1] = Center(secondPedal);
            Transform[] levers = { frontLever, rearLever };
            Transform[] leverBodies = { frontLeverBody, rearLeverBody };
            Transform[] leverPivots = { frontLeverPivot, rearLeverPivot };
            Transform[] rotors = { frontRotor, rearRotor };
            for (int side = 0; side < 2; side++)
            {
                brakeLevers[side] = Capture(levers[side]);
                brakePivots[side] = Center(leverPivots[side]);
                brakeRotorCenters[side] = Center(rotors[side]);
                brakePullAngles[side] = PullAngle(
                    brakePivots[side], Center(levers[side]), Center(leverBodies[side]));
                brakeHitTargets[side] = CreateBrakeHitTarget(levers[side], side == 0);
            }

            chainLinks = new Pose[linkCount];
            for (int index = 0; index < linkCount; index++)
            {
                Transform link;
                if (!named.TryGetValue("MM_chain_link_" + (index + 1).ToString("D3"), out link))
                    return false;
                chainLinks[index] = Capture(link);
            }
            if (Vector3.Distance(chainLinks[0].Position, chainLinks[1].Position) < 0.005f)
                return false;
            for (int index = 1; index <= 2; index++)
            {
                Transform quickLink;
                if (!named.TryGetValue("MM_chain_quick_link_" + index, out quickLink))
                    return false;
                quickLinks.Add(Capture(quickLink));
            }

            if (!BuildMotionChain(chainring, sprocket, lowerJockey, upperJockey))
                return false;

            foreach (KeyValuePair<string, Transform> item in named)
            {
                string name = item.Key;
                if (name.StartsWith("MM_crank_", StringComparison.Ordinal)
                    && !name.StartsWith("MM_crank_bb_", StringComparison.Ordinal))
                    crankParts.Add(Capture(item.Value));

                if (name.StartsWith("MM_wheel_rear_", StringComparison.Ordinal)
                    && !name.Contains("hub_axle") && !name.Contains("thru_axle")
                    && !name.Contains("hub_bearing") && !name.Contains("hub_end_cap")
                    && !name.Contains("freehub_bearing"))
                    rearWheelParts.Add(Capture(item.Value));

                if (name.StartsWith("MM_wheel_front_", StringComparison.Ordinal)
                    && !name.Contains("hub_axle") && !name.Contains("thru_axle")
                    && !name.Contains("hub_bearing") && !name.Contains("hub_end_cap"))
                    frontWheelParts.Add(Capture(item.Value));

                if (name == "MM_brake_front_brake_pad_1"
                    || name == "MM_brake_front_brake_pad_2")
                    brakePads[0].Add(Capture(item.Value));
                if (name == "MM_brake_rear_brake_pad_1"
                    || name == "MM_brake_rear_brake_pad_2")
                    brakePads[1].Add(Capture(item.Value));

                Match pedalMatch = Regex.Match(name,
                    @"^MM_pedal_(?:body|axle|bearing|seal|end_cap|washer|nut|traction_pin)_(1|2)(?:_|$)");
                if (pedalMatch.Success)
                    pedalParts[int.Parse(pedalMatch.Groups[1].Value) - 1].Add(Capture(item.Value));

                if (name.StartsWith("MM_rear_derailleur_jockey_wheel_", StringComparison.Ordinal))
                    jockeyWheels.Add(Capture(item.Value));
            }

            IsReady = crankParts.Count > 0 && frontWheelParts.Count > 0 && rearWheelParts.Count > 0
                && pedalParts[0].Count > 0 && pedalParts[1].Count > 0
                && brakePads[0].Count == 2 && brakePads[1].Count == 2
                && jockeyWheels.Count == 2 && motionChainLinks.Length > 0;
            return IsReady;
        }

        public void SetCadence(int rpm)
        {
            CadenceRpm = Mathf.Clamp(rpm, 30, 90);
        }

        public void Play()
        {
            if (!IsReady || IsPlaying) return;
            if (IsActive)
            {
                IsPlaying = true;
                return;
            }
            crankTurns = 0;
            frontWheelTurns = 0;
            effectiveCadenceRpm = CadenceRpm;
            frontWheelRpm = CadenceRpm * RearWheelRatio;
            brakeEngaged[0] = brakeEngaged[1] = false;
            ApplyBrakeVisuals();
            // The tray creates temporary primitive colliders and destroys them at
            // the end of the frame. Query live colliders when playback begins.
            colliders = GetComponentsInChildren<Collider>(true);
            colliderEnabled = new bool[colliders.Length];
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] == null) continue;
                colliderEnabled[index] = colliders[index].enabled;
                colliders[index].enabled = colliders[index].GetComponent<BicycleBrakeHitTarget>() != null;
            }
            for (int index = 0; index < staticChainRenderers.Length; index++)
            {
                staticChainRendererEnabled[index] = staticChainRenderers[index].enabled;
                staticChainRenderers[index].enabled = false;
            }
            PositionVisualChain();
            motionChainRoot.SetActive(true);
            IsActive = true;
            IsPlaying = true;
        }

        public void Pause()
        {
            if (IsActive) IsPlaying = false;
        }

        public bool SetBrakeHeld(bool isFront, bool held)
        {
            if (!IsActive) return false;
            int side = isFront ? 0 : 1;
            if (brakeEngaged[side] == held) return false;
            brakeEngaged[side] = held;
            ApplyBrakeVisuals();
            return true;
        }

        public void Stop()
        {
            if (!IsActive) return;
            IsPlaying = false;
            IsActive = false;
            Restore(crankParts);
            Restore(frontWheelParts);
            Restore(rearWheelParts);
            Restore(pedalParts[0]);
            Restore(pedalParts[1]);
            Restore(jockeyWheels);
            Restore(quickLinks);
            for (int side = 0; side < 2; side++)
            {
                Restore(new[] { brakeLevers[side] });
                Restore(brakePads[side]);
                brakeEngaged[side] = false;
                brakeHitTargets[side].position = Center(brakeLevers[side].Transform);
            }
            motionChainRoot.SetActive(false);
            for (int index = 0; index < staticChainRenderers.Length; index++)
                if (staticChainRenderers[index] != null)
                    staticChainRenderers[index].enabled = staticChainRendererEnabled[index];
            for (int index = 0; index < colliders.Length; index++)
                if (colliders[index] != null) colliders[index].enabled = colliderEnabled[index];
            crankTurns = 0;
            frontWheelTurns = 0;
            effectiveCadenceRpm = 0f;
            frontWheelRpm = 0f;
        }

        private void OnDisable() => Stop();

        private void Update()
        {
            if (!IsPlaying) return;
            Advance(Time.deltaTime);
        }

        private void Advance(float deltaTime)
        {
            // The left lever brakes the rear wheel and its coupled drivetrain.
            // The right lever brakes the front wheel independently.
            effectiveCadenceRpm = Mathf.MoveTowards(
                effectiveCadenceRpm, brakeEngaged[1] ? 0f : CadenceRpm,
                (brakeEngaged[1] ? 65f : 40f) * deltaTime);
            frontWheelRpm = Mathf.MoveTowards(
                frontWheelRpm, brakeEngaged[0] ? 0f : CadenceRpm * RearWheelRatio,
                (brakeEngaged[0] ? 100f : 60f) * deltaTime);
            crankTurns = (crankTurns + effectiveCadenceRpm * deltaTime / 60.0) % 1100.0;
            frontWheelTurns = (frontWheelTurns
                + frontWheelRpm * deltaTime / 60.0) % 1100.0;
            float crankDegrees = (float)((crankTurns * 360.0) % 360.0);
            Quaternion crankRotation = Quaternion.AngleAxis(crankDegrees, axleAxis);
            RotateAround(crankParts, crankPivot, crankRotation);

            float frontDegrees = (float)((frontWheelTurns * 360.0) % 360.0);
            RotateAround(frontWheelParts, frontPivot,
                Quaternion.AngleAxis(frontDegrees, axleAxis));
            float rearDegrees = (float)((crankTurns * RearWheelRatio * 360.0) % 360.0);
            RotateAround(rearWheelParts, rearPivot,
                Quaternion.AngleAxis(rearDegrees, axleAxis));

            for (int side = 0; side < 2; side++)
            {
                Vector3 pedalPivot = crankPivot
                    + crankRotation * (pedalPivots[side] - crankPivot);
                Quaternion levelingRotation = Quaternion.AngleAxis(-crankDegrees, axleAxis);
                foreach (Pose part in pedalParts[side])
                {
                    Vector3 orbitPosition = crankPivot
                        + crankRotation * (part.Position - crankPivot);
                    Quaternion orbitRotation = crankRotation * part.Rotation;
                    if (!part.Transform.name.StartsWith("MM_pedal_axle_", StringComparison.Ordinal))
                    {
                        part.Transform.SetPositionAndRotation(
                            pedalPivot + levelingRotation * (orbitPosition - pedalPivot),
                            levelingRotation * orbitRotation);
                    }
                    else part.Transform.SetPositionAndRotation(orbitPosition, orbitRotation);
                }
            }

            PositionVisualChain();

            Quaternion jockeyRotation = Quaternion.AngleAxis(
                (float)((crankTurns * frontTeeth / 12.0 * 360.0) % 360.0), axleAxis);
            foreach (Pose part in jockeyWheels)
                part.Transform.SetPositionAndRotation(part.Position,
                    jockeyRotation * part.Rotation);
        }

        private Transform CreateBrakeHitTarget(Transform lever, bool isFront)
        {
            GameObject target = new GameObject(isFront ? "FrontBrakeHitTarget" : "RearBrakeHitTarget");
            target.transform.SetParent(transform, false);
            target.transform.position = Center(lever);
            target.AddComponent<BicycleBrakeHitTarget>().Initialize(isFront);
            SphereCollider collider = target.AddComponent<SphereCollider>();
            collider.radius = 0.07f;
            collider.enabled = false;
            return target.transform;
        }

        private static float PullAngle(
            Vector3 pivot, Vector3 leverCenter, Vector3 bodyCenter)
        {
            Vector3 offset = leverCenter - pivot;
            float positiveDistance = Vector3.Distance(
                pivot + Quaternion.AngleAxis(16f, Vector3.up) * offset, bodyCenter);
            float negativeDistance = Vector3.Distance(
                pivot + Quaternion.AngleAxis(-16f, Vector3.up) * offset, bodyCenter);
            return positiveDistance < negativeDistance ? 16f : -16f;
        }

        private void ApplyBrakeVisuals()
        {
            for (int side = 0; side < 2; side++)
            {
                Pose lever = brakeLevers[side];
                Quaternion pull = Quaternion.AngleAxis(
                    brakeEngaged[side] ? brakePullAngles[side] : 0f, Vector3.up);
                lever.Transform.SetPositionAndRotation(
                    brakePivots[side] + pull * (lever.Position - brakePivots[side]),
                    pull * lever.Rotation);
                brakeHitTargets[side].position = Center(lever.Transform);
                foreach (Pose pad in brakePads[side])
                {
                    float direction = Mathf.Sign(Vector3.Dot(
                        brakeRotorCenters[side] - pad.Position, axleAxis));
                    pad.Transform.SetPositionAndRotation(
                        pad.Position + axleAxis * (brakeEngaged[side] ? direction * 0.0015f : 0f),
                        pad.Rotation);
                }
            }
        }

        private void PositionVisualChain()
        {
            // The demonstration path wraps both jockey wheels. Its visual links
            // are separate from the 110 stable disassembly objects.
            float linkSpacing = chainPathLength / motionChainLinks.Length;
            float chainOffset = Mathf.Repeat(
                (float)(crankTurns * frontTeeth * linkSpacing), chainPathLength);
            for (int index = 0; index < motionChainLinks.Length; index++)
            {
                Vector3 position, tangent;
                SampleChain(index * linkSpacing - chainOffset, out position, out tangent);
                motionChainLinks[index].SetPositionAndRotation(position,
                    ChainOrientation(tangent) * chainMeshCorrection);
            }
            for (int index = 0; index < quickLinks.Count; index++)
            {
                Pose quickLink = quickLinks[index];
                Transform leading = motionChainLinks[0];
                float side = index == 0 ? -0.003f : 0.003f;
                quickLink.Transform.SetPositionAndRotation(
                    leading.position + axleAxis * side,
                    leading.rotation);
            }
        }

        private static Vector3 Center(Transform part)
        {
            Renderer renderer = part.GetComponentInChildren<Renderer>();
            return renderer == null ? part.position : renderer.bounds.center;
        }

        private bool BuildMotionChain(
            Transform chainring, Transform sprocket,
            Transform lowerJockey, Transform upperJockey)
        {
            MeshFilter sourceMesh = chainLinks[0].Transform.GetComponentInChildren<MeshFilter>();
            MeshRenderer sourceRenderer = chainLinks[0].Transform.GetComponentInChildren<MeshRenderer>();
            if (sourceMesh == null || sourceMesh.sharedMesh == null || sourceRenderer == null)
                return false;

            Vector3 origin = Center(sprocket);
            Vector3 horizontal = Vector3.ProjectOnPlane(Center(chainring) - origin, axleAxis).normalized;
            Vector3 vertical = Vector3.ProjectOnPlane(Vector3.up, axleAxis);
            vertical = (vertical - horizontal * Vector3.Dot(vertical, horizontal)).normalized;
            if (horizontal.sqrMagnitude < 0.9f || vertical.sqrMagnitude < 0.9f)
                return false;

            ChainCircle[] circles =
            {
                ProjectCircle(Center(sprocket), 0.049f, origin, horizontal, vertical),
                ProjectCircle(Center(chainring), 0.073f, origin, horizontal, vertical),
                ProjectCircle(Center(lowerJockey), 0.025f, origin, horizontal, vertical),
                ProjectCircle(Center(upperJockey), 0.025f, origin, horizontal, vertical)
            };
            var tangents = new ChainTangent[circles.Length];
            for (int index = 0; index < circles.Length; index++)
            {
                if (!ClockwiseTangent(circles[index],
                    circles[(index + 1) % circles.Length], out tangents[index]))
                    return false;
            }

            var points = new List<Vector3>();
            points.Add(ToWorld(circles[0], tangents[0].From, origin, horizontal, vertical));
            for (int index = 0; index < circles.Length; index++)
            {
                int next = (index + 1) % circles.Length;
                ChainCircle circle = circles[next];
                points.Add(ToWorld(circle, tangents[index].To, origin, horizontal, vertical));
                float start = Mathf.Atan2(tangents[index].To.y - circle.Center.y,
                    tangents[index].To.x - circle.Center.x);
                float end = Mathf.Atan2(tangents[next].From.y - circle.Center.y,
                    tangents[next].From.x - circle.Center.x);
                float sweep = Mathf.Repeat(start - end, Mathf.PI * 2f);
                int segments = Mathf.Max(1, Mathf.CeilToInt(sweep / (Mathf.PI / 36f)));
                for (int step = 1; step <= segments; step++)
                {
                    float angle = start - sweep * step / segments;
                    Vector2 contact = circle.Center + new Vector2(
                        Mathf.Cos(angle), Mathf.Sin(angle)) * circle.Radius;
                    points.Add(ToWorld(circle, contact, origin, horizontal, vertical));
                }
            }
            if ((points[points.Count - 1] - points[0]).sqrMagnitude < 1e-8f)
                points.RemoveAt(points.Count - 1);

            chainPath = points.ToArray();
            chainDistances = new float[chainPath.Length + 1];
            for (int index = 0; index < chainPath.Length; index++)
                chainDistances[index + 1] = chainDistances[index]
                    + Vector3.Distance(chainPath[index], chainPath[(index + 1) % chainPath.Length]);
            chainPathLength = chainDistances[chainPath.Length];
            int visualCount = Mathf.RoundToInt(chainPathLength / 0.0127f);
            if (visualCount % 2 != 0) visualCount++;
            if (visualCount < 100 || visualCount > 160) return false;

            chainMeshCorrection = Quaternion.Inverse(
                ChainOrientation(chainLinks[1].Position - chainLinks[0].Position))
                * chainLinks[0].Rotation;
            motionChainRoot = new GameObject("MotionChainVisual");
            motionChainRoot.transform.SetParent(transform, false);
            motionChainLinks = new Transform[visualCount];
            for (int index = 0; index < visualCount; index++)
            {
                GameObject link = new GameObject("MotionChainLink_" + (index + 1).ToString("D3"));
                link.transform.SetParent(motionChainRoot.transform, false);
                link.AddComponent<MeshFilter>().sharedMesh = sourceMesh.sharedMesh;
                MeshRenderer renderer = link.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = sourceRenderer.sharedMaterials;
                renderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
                renderer.receiveShadows = sourceRenderer.receiveShadows;
                motionChainLinks[index] = link.transform;
            }
            motionChainRoot.SetActive(false);
            var renderers = new List<Renderer>();
            foreach (Pose link in chainLinks)
                renderers.AddRange(link.Transform.GetComponentsInChildren<Renderer>(true));
            staticChainRenderers = renderers.ToArray();
            staticChainRendererEnabled = new bool[staticChainRenderers.Length];
            return true;
        }

        private ChainCircle ProjectCircle(
            Vector3 center, float radius, Vector3 origin,
            Vector3 horizontal, Vector3 vertical)
        {
            Vector3 delta = center - origin;
            return new ChainCircle
            {
                Center = new Vector2(Vector3.Dot(delta, horizontal), Vector3.Dot(delta, vertical)),
                Radius = radius,
                Lateral = Vector3.Dot(delta, axleAxis)
            };
        }

        private static bool ClockwiseTangent(
            ChainCircle from, ChainCircle to, out ChainTangent tangent)
        {
            tangent = new ChainTangent();
            Vector2 delta = to.Center - from.Center;
            float distance = delta.magnitude;
            float radiusDifference = from.Radius - to.Radius;
            if (distance <= Mathf.Abs(radiusDifference)) return false;
            float angle = Mathf.Atan2(delta.y, delta.x)
                - Mathf.Asin(radiusDifference / distance);
            Vector2 normal = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
            tangent.From = from.Center + normal * from.Radius;
            tangent.To = to.Center + normal * to.Radius;
            return true;
        }

        private Vector3 ToWorld(
            ChainCircle circle, Vector2 point, Vector3 origin,
            Vector3 horizontal, Vector3 vertical) =>
            origin + horizontal * point.x + vertical * point.y + axleAxis * circle.Lateral;

        private Quaternion ChainOrientation(Vector3 direction)
        {
            Vector3 tangent = direction.normalized;
            Vector3 forward = Vector3.Cross(tangent, axleAxis).normalized;
            return Quaternion.LookRotation(forward, axleAxis);
        }

        private void SampleChain(float distance, out Vector3 position, out Vector3 tangent)
        {
            float wrapped = Mathf.Repeat(distance, chainPathLength);
            int low = 0, high = chainPath.Length;
            while (low + 1 < high)
            {
                int middle = (low + high) / 2;
                if (chainDistances[middle] <= wrapped) low = middle;
                else high = middle;
            }
            int next = (low + 1) % chainPath.Length;
            float segmentLength = chainDistances[low + 1] - chainDistances[low];
            float fraction = segmentLength <= 1e-6f
                ? 0f : (wrapped - chainDistances[low]) / segmentLength;
            position = Vector3.Lerp(chainPath[low], chainPath[next], fraction);
            tangent = (chainPath[next] - chainPath[low]).normalized;
        }

        private static Pose Capture(Transform part) => new Pose
        {
            Transform = part, Position = part.position, Rotation = part.rotation
        };

        private static void RotateAround(List<Pose> parts, Vector3 pivot, Quaternion rotation)
        {
            foreach (Pose part in parts)
                part.Transform.SetPositionAndRotation(
                    pivot + rotation * (part.Position - pivot), rotation * part.Rotation);
        }

        private static void Restore(IEnumerable<Pose> parts)
        {
            if (parts == null) return;
            foreach (Pose part in parts)
                if (part.Transform != null)
                    part.Transform.SetPositionAndRotation(part.Position, part.Rotation);
        }
    }
}
