using System;
using RT.Util.ExtensionMethods;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RT.Util.Serialization;

namespace Editon
{
    [ClassifyIgnoreIfDefault, ClassifyIgnoreIfEmpty]
    sealed class FncFile
    {
        [ClassifyNotNull]
        public List<Item> Items = new List<Item>();

        public bool HasAnyLine(int x, int y)
        {
            if (Items.Any(i => i.X == x && i.Y == y && !(i is Box)))
                return true;

            var hLeft = Items.OfType<Node>().Where(n => n.Y == y && n.X < x && n.LineTypes[Direction.Right] != LineType.None).MaxElementOrDefault(n => n.X);
            var hEndLeft = Items.OfType<LineEnd>().Where(n => n.Y == y && n.X <= x && n.Direction == Direction.Right).MaxElementOrDefault(n => n.X);
            if (hLeft != null || hEndLeft != null)
            {
                var hx1 = hLeft != null ? hEndLeft != null ? Math.Max(hLeft.X, hEndLeft.X) : hLeft.X : hEndLeft.X;
                var hRight = Items.OfType<Node>().Where(n => n.Y == y && n.X > hx1 && n.LineTypes[Direction.Left] != LineType.None).MinElementOrDefault(n => n.X);
                var hEndRight = Items.OfType<LineEnd>().Where(n => n.Y == y && n.X >= hx1 && n.Direction == Direction.Left).MinElementOrDefault(n => n.X);
                if (hRight != null || hEndRight != null)
                {
                    var hx2 = hRight != null ? hEndRight != null ? Math.Min(hRight.X, hEndRight.X) : hRight.X : hEndRight.X;
                    if (hx2 > x)
                        return true;
                }
            }

            var vUp = Items.OfType<Node>().Where(n => n.X == x && n.Y < y && n.LineTypes[Direction.Down] != LineType.None).MaxElementOrDefault(n => n.Y);
            var vEndUp = Items.OfType<LineEnd>().Where(n => n.X == x && n.Y <= y && n.Direction == Direction.Down).MaxElementOrDefault(n => n.Y);
            if (vUp != null || vEndUp != null)
            {
                var vy1 = vUp != null ? vEndUp != null ? Math.Max(vUp.Y, vEndUp.Y) : vUp.Y : vEndUp.Y;
                var vDown = Items.OfType<Node>().Where(n => n.X == x && n.Y > vy1 && n.LineTypes[Direction.Up] != LineType.None).MinElementOrDefault(n => n.Y);
                var vEndDown = Items.OfType<LineEnd>().Where(n => n.X == x && n.Y >= vy1 && n.Direction == Direction.Up).MinElementOrDefault(n => n.Y);
                if (vDown != null || vEndDown != null)
                {
                    var vy2 = vDown != null ? vEndDown != null ? Math.Min(vDown.Y, vEndDown.Y) : vDown.Y : vEndDown.Y;
                    if (vy2 > y)
                        return true;
                }
            }

            return false;
        }
    }
}
