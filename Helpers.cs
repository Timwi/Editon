using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RT.Util.Collections;

namespace Editon
{
    static class Helpers
    {
        public static Direction[] Directions = new[] { Direction.Up, Direction.Right, Direction.Down, Direction.Left };

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

        public static LineChars At(this LineType lineType, Direction lineLocation)
        {
            return (LineChars) ((int) lineType << (2 * (int) lineLocation));
        }

        public static Item FirstPreferNonBox(this IEnumerable<Item> source, Func<Item, bool> predicate)
        {
            Item found = null;
            foreach (var item in source)
            {
                if (!predicate(item))
                    continue;
                if (!(item is Box))
                    return item;
                if (found == null)
                    found = item;
            }
            return found;
        }

        public static int XOffset(this Direction dir) { return dir == Direction.Left ? -1 : dir == Direction.Right ? 1 : 0; }
        public static int YOffset(this Direction dir) { return dir == Direction.Up ? -1 : dir == Direction.Down ? 1 : 0; }
        public static Direction Opposite(this Direction dir) { return (Direction) (((int) dir + 2) % 4); }
        public static Direction Clockwise(this Direction dir) { return (Direction) (((int) dir + 1) % 4); }
        public static Direction CounterClockwise(this Direction dir) { return (Direction) (((int) dir + 3) % 4); }

        public static AutoDictionary<Direction, T> MakeDictionary<T>(T up, T right, T down, T left)
        {
            var result = new AutoDictionary<Direction, T>();
            result[Direction.Up] = up;
            result[Direction.Right] = right;
            result[Direction.Down] = down;
            result[Direction.Left] = left;
            return result;
        }
    }
}
