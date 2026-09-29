using System;

namespace MechMaster.Runtime
{
    // Ideal fixed-ring epicyclic motion. Angles are absolute, measured from
    // the CAD rest phase; the planet angle is in the stationary world frame.
    public sealed class PlanetaryGearKinematics
    {
        public readonly struct Angles
        {
            public readonly double Sun, Carrier, Planet;
            public Angles(double sun, double carrier, double planet)
            { Sun = sun; Carrier = carrier; Planet = planet; }
        }

        public int SunTeeth { get; }
        public int PlanetTeeth { get; }
        public int RingTeeth { get; }
        public double ReductionRatio => 1.0 + (double)RingTeeth / SunTeeth;
        public int ClosedCycleInputTurns { get; }

        public PlanetaryGearKinematics(int sunTeeth, int planetTeeth, int ringTeeth)
        {
            if (sunTeeth <= 0 || planetTeeth <= 0 || ringTeeth <= 0
                || sunTeeth > 10000 || planetTeeth > 10000 || ringTeeth > 10000
                || ringTeeth != sunTeeth + 2 * planetTeeth)
                throw new ArgumentException("Invalid coaxial planetary gear teeth");
            SunTeeth = sunTeeth; PlanetTeeth = planetTeeth; RingTeeth = ringTeeth;
            long denominator = (long)planetTeeth * (sunTeeth + ringTeeth);
            long carrierPeriod = (sunTeeth + ringTeeth) / Gcd(sunTeeth, sunTeeth + ringTeeth);
            long planetPeriod = denominator / Gcd((long)sunTeeth * (planetTeeth - ringTeeth), denominator);
            ClosedCycleInputTurns = checked((int)(carrierPeriod / Gcd(carrierPeriod, planetPeriod) * planetPeriod));
        }

        public Angles Evaluate(double inputDegrees)
        {
            if (double.IsNaN(inputDegrees) || double.IsInfinity(inputDegrees))
                throw new ArgumentException("Input angle must be finite");
            double carrier = inputDegrees / ReductionRatio;
            double planet = carrier - (inputDegrees - carrier) * SunTeeth / PlanetTeeth;
            return new Angles(inputDegrees, carrier, planet);
        }

        private static long Gcd(long a, long b)
        {
            a = Math.Abs(a); b = Math.Abs(b);
            while (b != 0) { long remainder = a % b; a = b; b = remainder; }
            return a;
        }
    }
}
