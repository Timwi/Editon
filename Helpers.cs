using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Editon
{
    static class Helpers
    {
        public static void BitwiseOrSafe<TKey1, TKey2>(this Dictionary<TKey1, Dictionary<TKey2, LineChars>> source, TKey1 key1, TKey2 key2, LineChars value)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            if (!source.ContainsKey(key1))
                source[key1] = new Dictionary<TKey2, LineChars>();
            if (!source[key1].ContainsKey(key2))
                source[key1][key2] = value;
            else
                source[key1][key2] |= value;
        }

        public static LineChars At(this LineType lineType, LineLocation location)
        {
            return (LineChars) ((int) lineType << (2 * (int) location));
        }
    }
}
