using System;
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

        public bool Contains(int x, int y)
        {
            return x >= PosX1 && x < PosX2 && y >= PosY1 && y < PosY2;
        }

        public abstract void Move(Direction direction);
        public abstract void Adjust(Direction end, Direction intoDirection);
    }

    [ClassifyIgnoreIfDefault, ClassifyIgnoreIfEmpty]
    sealed class Box : Item
    {
        public int X, Y;
        public int Width, Height;
        public TextLine[][] TextAreas;

        [ClassifyNotNull]
        public Dictionary<LineLocation, LineType> LineTypes = new Dictionary<LineLocation, LineType>(4);

        public LineType this[LineLocation loc] { get { return LineTypes.Get(loc, LineType.None); } }

        public override int CenterX { get { return X + (Width + 1) / 2; } }
        public override int CenterY { get { return Y + (Height + 1) / 2; } }
        public override int PosX1 { get { return X; } }
        public override int PosX2 { get { return X + Width + 1; } }
        public override int PosY1 { get { return Y; } }
        public override int PosY2 { get { return Y + Height + 1; } }

        public override void Move(Direction direction)
        {
            EditonProgram.Invalidate(this);
            var ox = 0;
            var oy = 0;
            switch (direction)
            {
                case Direction.Up: oy = -1; break;
                case Direction.Right: ox = 1; break;
                case Direction.Down: oy = 1; break;
                case Direction.Left: ox = -1; break;
            }
            X += ox;
            Y += oy;
            for (int i = 0; i < TextAreas.Length; i++)
                for (int j = 0; j < TextAreas[i].Length; j++)
                {
                    TextAreas[i][j].X += ox;
                    TextAreas[i][j].Y += oy;
                }
            EditonProgram.Invalidate(this);
        }

        private Action getUndo()
        {
            var x = X;
            var y = Y;
            var width = Width;
            var height = Height;
            var textXs = TextAreas.Select(lines => lines.Select(line => line.X).ToArray()).ToArray();
            var textYs = TextAreas.Select(lines => lines.Select(line => line.Y).ToArray()).ToArray();
            return () =>
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
                for (int i = 0; i < TextAreas.Length; i++)
                    for (int j = 0; j < TextAreas[i].Length; j++)
                    {
                        TextAreas[i][j].X = textXs[i][j];
                        TextAreas[i][j].Y = textYs[i][j];
                    }
            };
        }

        public override void Adjust(Direction end, Direction intoDirection)
        {
            throw new InvalidOperationException("Adjust invalid on boxes.");
        }
    }

    abstract class Line : Item { }

    [ClassifyIgnoreIfDefault, ClassifyIgnoreIfEmpty]
    sealed class HLine : Line
    {
        public int X1, X2, Y;
        public LineType LineType;
        public override int CenterX { get { return (X1 + X2) / 2; } }
        public override int CenterY { get { return Y; } }
        public override int PosX1 { get { return X1; } }
        public override int PosX2 { get { return X2 + 1; } }
        public override int PosY1 { get { return Y; } }
        public override int PosY2 { get { return Y + 1; } }

        public override void Move(Direction direction)
        {
            EditonProgram.Invalidate(this);
            switch (direction)
            {
                case Direction.Up: Y--; break;
                case Direction.Right: X1++; X2++; break;
                case Direction.Down: Y++; break;
                case Direction.Left: X1--; X2--; break;
            }
            EditonProgram.Invalidate(this);
        }

        private Action getUndo()
        {
            var x1 = X1;
            var x2 = X2;
            var y = Y;
            return () =>
            {
                X1 = x1;
                X2 = x2;
                Y = y;
            };
        }

        public override void Adjust(Direction end, Direction intoDirection)
        {
            switch (end)
            {
                case Direction.Up:
                case Direction.Down:
                    throw new InvalidOperationException("Cannot adjust horizontal line in this way.");

                case Direction.Right:
                    if (intoDirection == Direction.Left)
                        X2--;
                    else if (intoDirection == Direction.Right)
                        X2++;
                    else
                        throw new InvalidOperationException("Cannot adjust horizontal line in this way.");
                    break;

                case Direction.Left:
                    if (intoDirection == Direction.Left)
                        X1--;
                    else if (intoDirection == Direction.Right)
                        X1++;
                    else
                        throw new InvalidOperationException("Cannot adjust horizontal line in this way.");
                    break;
            }
        }
    }

    [ClassifyIgnoreIfDefault, ClassifyIgnoreIfEmpty]
    sealed class VLine : Line
    {
        public int X, Y1, Y2;
        public LineType LineType;
        public override int CenterX { get { return X; } }
        public override int CenterY { get { return (Y1 + Y2) / 2; } }
        public override int PosX1 { get { return X; } }
        public override int PosX2 { get { return X + 1; } }
        public override int PosY1 { get { return Y1; } }
        public override int PosY2 { get { return Y2 + 1; } }

        public override void Move(Direction direction)
        {
            EditonProgram.Invalidate(this);
            switch (direction)
            {
                case Direction.Up: Y1--; Y2--; break;
                case Direction.Right: X++; break;
                case Direction.Down: Y1++; Y2++; break;
                case Direction.Left: X--; break;
            }
            EditonProgram.Invalidate(this);
        }

        private Action getUndo()
        {
            var x = X;
            var y1 = Y1;
            var y2 = Y2;
            return () =>
            {
                X = x;
                Y1 = y1;
                Y2 = y2;
            };
        }

        public override void Adjust(Direction edge, Direction intoDirection)
        {
            switch (edge)
            {
                case Direction.Left:
                case Direction.Right:
                    throw new InvalidOperationException("Cannot adjust horizontal line in this way.");

                case Direction.Up:
                    if (intoDirection == Direction.Up)
                        Y1--;
                    else if (intoDirection == Direction.Down)
                        Y1++;
                    else
                        throw new InvalidOperationException("Cannot adjust horizontal line in this way.");
                    break;

                case Direction.Down:
                    if (intoDirection == Direction.Up)
                        Y2--;
                    else if (intoDirection == Direction.Down)
                        Y2++;
                    else
                        throw new InvalidOperationException("Cannot adjust horizontal line in this way.");
                    break;
            }
        }
    }

    sealed class TextLine
    {
        public int X;
        public int Y;
        public string Content;
    }
}
