using System;
using System.Security.Cryptography;

namespace CSharpUtils
{
    public static class Rnd
    {
        const int BufferSize = sizeof(int) * 1024;
        static readonly byte[] Buffer = new byte[BufferSize];
        static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();
        static int _bufferIndex = BufferSize;

        static ReadOnlySpan<byte> Get4Bt()
        {
            lock (Buffer)
            {
                if (_bufferIndex == BufferSize)
                {
                    Rng.GetNonZeroBytes(Buffer);
                    _bufferIndex = 0;
                }

                // can be concurrent
                var span = new ReadOnlySpan<byte>(Buffer, _bufferIndex, sizeof(int));
                _bufferIndex += sizeof(int);
                return span;
            }
        }

        public static int NextInt() => BitConverter.ToInt32(Get4Bt());
        public static uint NextUInt() => BitConverter.ToUInt32(Get4Bt());

        public static int NextInt(int minValue, int maxValue)
        {
            if (minValue > maxValue) throw new ArgumentException("minValue > maxValue", nameof(minValue));

            // Casting to long is easier then magic with uint
            var diff = (long) maxValue - minValue;
            if (diff == 0) return minValue;

            var shift = NextUInt() % (diff + 1);
            return (int) (minValue + shift);
        }

        // [0, 1)
        public static double NextDouble() => (double) Math.Abs(NextInt()) / ((long) int.MaxValue + 1);

        public static bool Probe(double probability)
        {
            if (!probability.IsInClosedSegment(0, 1)) throw new ArgumentOutOfRangeException(nameof(probability));

            return NextDouble() >= (1 - probability);
        }

        public static double NextGaussian(double mean, double stdDev)
        {
            // https://stackoverflow.com/a/218600

            var u1 = 1.0 - NextDouble(); // uniform(0,1] random doubles
            var u2 = 1.0 - NextDouble();
            var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2); // random normal(0,1)
            var randNormal = mean + stdDev * randStdNormal; // random normal(mean,stdDev^2)

            return randNormal;
        }
    }
}
