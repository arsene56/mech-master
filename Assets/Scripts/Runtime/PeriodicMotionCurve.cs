using System;

namespace MechMaster.Runtime
{
    // Periodic, shape-preserving cubic Hermite interpolation. Shared tangents
    // carry velocity through intermediate poses without overshooting them.
    // Arrays are prepared once; evaluating a frame does not allocate.
    internal sealed class PeriodicMotionCurve
    {
        private readonly float[] phases;
        private readonly float[][] values, tangents;
        private readonly int channels;

        public PeriodicMotionCurve(float[] phases, float[][] values)
        {
            if (phases == null || values == null || phases.Length < 2 || values.Length != phases.Length
                || phases[0] != 0 || phases[phases.Length - 1] != 1
                || values[0] == null || values[0].Length == 0)
                throw new ArgumentException("A periodic curve needs matching, closed frames from phase 0 to 1.");
            this.phases = (float[])phases.Clone();
            this.values = new float[values.Length][];
            tangents = new float[values.Length][];
            channels = values[0].Length;
            for (int frame = 0; frame < values.Length; frame++)
            {
                if (!Finite(phases[frame]) || frame > 0 && phases[frame] <= phases[frame - 1]
                    || values[frame] == null || values[frame].Length != channels)
                    throw new ArgumentException("Invalid periodic curve frame.");
                this.values[frame] = (float[])values[frame].Clone();
                tangents[frame] = new float[channels];
                for (int channel = 0; channel < channels; channel++)
                    if (!Finite(values[frame][channel])
                        || frame == values.Length - 1 && values[frame][channel] != values[0][channel])
                        throw new ArgumentException("Periodic curve values must be finite and closed.");
            }
            int last = phases.Length - 1;
            for (int frame = 0; frame < last; frame++)
            {
                int previous = frame == 0 ? last - 1 : frame - 1;
                float leftSpan = frame == 0 ? 1 - phases[previous] : phases[frame] - phases[previous];
                float rightSpan = phases[frame + 1] - phases[frame];
                for (int channel = 0; channel < channels; channel++)
                {
                    double left = (values[frame][channel] - values[previous][channel]) / leftSpan;
                    double right = (values[frame + 1][channel] - values[frame][channel]) / rightSpan;
                    // Only true extrema / flat segments stop. Same-direction
                    // segments share a bounded weighted harmonic-mean slope.
                    if (left * right <= 0) continue;
                    double leftWeight = 2 * rightSpan + leftSpan, rightWeight = rightSpan + 2 * leftSpan;
                    tangents[frame][channel] = (float)((leftWeight + rightWeight)
                        / (leftWeight / left + rightWeight / right));
                }
            }
            Array.Copy(tangents[0], tangents[last], channels);
        }

        public void Evaluate(float phase, float[] result)
        {
            if (!Finite(phase) || result == null || result.Length != channels)
                throw new ArgumentException("Invalid periodic curve sample.");
            double wrapped = phase - Math.Floor(phase);
            int to = 1;
            while (to < phases.Length - 1 && wrapped > phases[to]) to++;
            int from = to - 1;
            double span = phases[to] - phases[from], t = (wrapped - phases[from]) / span;
            double squared = t * t, cubed = squared * t;
            double h00 = 2 * cubed - 3 * squared + 1, h10 = cubed - 2 * squared + t;
            double h01 = -2 * cubed + 3 * squared, h11 = cubed - squared;
            for (int channel = 0; channel < channels; channel++)
                result[channel] = (float)(h00 * values[from][channel] + h10 * span * tangents[from][channel]
                    + h01 * values[to][channel] + h11 * span * tangents[to][channel]);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
