using System;

namespace CSharpUtils
{
    public static class SegmentExtensions
    {
        public static bool IsInClosedSegment<T>(this T x, T l, T r) where T : IComparable<T>
        {
            if (l.CompareTo(r) > 0) throw new ArgumentOutOfRangeException(nameof(r));

            return l.CompareTo(x) <= 0 && x.CompareTo(r) <= 0;
        }
    }
}
