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
    }
}
