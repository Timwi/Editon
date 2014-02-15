using System.Collections.Generic;
using RT.Util.ExtensionMethods;
using RT.Util.Serialization;

namespace Editon
{
    abstract class Item
    {
        public abstract int CenterX { get; }
        public abstract int CenterY { get; }

        public abstract int PosX1 { get; }  // inclusive
        public abstract int PosY1 { get; }  // inclusive
        public abstract int PosX2 { get; }  // exclusive
        public abstract int PosY2 { get; }  // exclusive
    }

    [ClassifyIgnoreIfDefault, ClassifyIgnoreIfEmpty]
    sealed class Box : Item
    {
        public int X, Y;
        public int Width, Height;
        public string Content;

        [ClassifyNotNull]
        public Dictionary<LineLocation, LineType> LineTypes = new Dictionary<LineLocation, LineType>(4);

        public LineType this[LineLocation loc] { get { return LineTypes.Get(loc, LineType.None); } }

        public override int CenterX { get { return X + (Width + 1) / 2; } }
        public override int CenterY { get { return Y + (Height + 1) / 2; } }
        public override int PosX1 { get { return X; } }
        public override int PosX2 { get { return X + Width + 1; } }
        public override int PosY1 { get { return Y; } }
        public override int PosY2 { get { return Y + Height + 1; } }
    }

    [ClassifyIgnoreIfDefault, ClassifyIgnoreIfEmpty]
    sealed class HLine : Item
    {
        public int X1, X2, Y;
        public LineType LineType;
        public override int CenterX { get { return (X1 + X2) / 2; } }
        public override int CenterY { get { return Y; } }
        public override int PosX1 { get { return X1; } }
        public override int PosX2 { get { return X2; } }
        public override int PosY1 { get { return Y; } }
        public override int PosY2 { get { return Y; } }
    }

    [ClassifyIgnoreIfDefault, ClassifyIgnoreIfEmpty]
    sealed class VLine : Item
    {
        public int X, Y1, Y2;
        public LineType LineType;
        public override int CenterX { get { return X; } }
        public override int CenterY { get { return (Y1 + Y2) / 2; } }
        public override int PosX1 { get { return X; } }
        public override int PosX2 { get { return X; } }
        public override int PosY1 { get { return Y1; } }
        public override int PosY2 { get { return Y2; } }
    }
}
