using System;

namespace MechMaster.Runtime
{
    // Continuous closed compression/rebound. No stopped keyframe segments.
    // Parameters are teaching excursions, not rated suspension travel/loads.
    public static class SuspensionTeachingCycle
    {
        public static double CompressionFraction(double phase)
        {
            if (double.IsNaN(phase) || double.IsInfinity(phase))
                throw new ArgumentOutOfRangeException(nameof(phase));
            double wrapped = phase - Math.Floor(phase);
            return .5 - .5 * Math.Cos(2 * Math.PI * wrapped);
        }
    }
}
