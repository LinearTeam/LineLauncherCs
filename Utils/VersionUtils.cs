using System;

namespace LMC.Utils
{
    public class VersionUtils
    {
        public static Version Min(Version a, Version b)
        {
            return Bigger(b, a) ? a : b;
        }

        public static Version Max(Version a, Version b)
        {
            return Smaller(b, a) ? a : b;
        }

        public static bool Between(Version a, Version min, Version max)
        {
            return SmallerOrEqual(a, max) && BiggerOrEqual(a, min);
        }

        public static bool SmallerOrEqual(Version ver, Version compareTo)
        {
            return ver.CompareTo(compareTo) <= 0;
        }

        public static bool Smaller(Version ver, Version compareTo)
        {
            return ver.CompareTo(compareTo) < 0;
        }

        public static bool BiggerOrEqual(Version ver, Version compareTo)
        {
            return ver.CompareTo(compareTo) >= 0;
        }

        public static bool Bigger(Version ver, Version compareTo)
        {
            return ver.CompareTo(compareTo) > 0;
        }
    }
}